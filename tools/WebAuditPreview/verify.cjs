const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('playwright');

// No server or live app. Every request is intercepted; only fixture HTML/local assets are served.
(async () => {
  const workspace = path.resolve(process.argv[2]);
  const output = path.join(workspace, 'tmp', 'multiagent-qa', 'web');
  const assets = path.join(workspace, 'src', 'NeoSTP.Web', 'wwwroot');
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const checks = [], captures = [], pageErrors = [], blocked = new Set();
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  page.on('pageerror', e => pageErrors.push(e.message));
  await page.route('**/*', async route => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.hostname !== 'web-audit.test' || request.method() !== 'GET') {
      blocked.add(url.hostname + ' ' + request.method()); return route.abort();
    }
    const root = url.pathname.endsWith('.html') ? output : assets;
    const candidate = path.resolve(root, '.' + decodeURIComponent(url.pathname));
    if (!candidate.startsWith(root + path.sep)) return route.abort();
    try {
      return route.fulfill({ body: await fs.readFile(candidate), contentType: candidate.endsWith('.html')
        ? 'text/html; charset=utf-8' : candidate.endsWith('.css') ? 'text/css'
        : candidate.endsWith('.js') ? 'application/javascript' : 'application/octet-stream' });
    } catch { return route.abort(); }
  });
  async function check(name, action) {
    try { const actual = await action(); checks.push({ name, result: 'PASS', actual }); }
    catch (error) { checks.push({ name, result: 'FAIL', reason: error.message }); }
  }
  async function open(name) { await page.goto('http://web-audit.test/' + name + '.html'); }
  async function capture(name, mobile = false) {
    await page.setViewportSize(mobile ? { width: 390, height: 844 } : { width: 1280, height: 900 });
    const file = name + (mobile ? '-mobile' : '-desktop') + '.png';
    await page.screenshot({ path: path.join(output, file), fullPage: true });
    captures.push(file);
  }
  try {
    await open('login');
    const pw = page.locator('#Password'), toggle = page.locator('#toggle-password');
    await check('Login: contraseña oculta y control accesible, sin submit', async () => {
      assert.equal(await pw.getAttribute('type'), 'password');
      assert.equal(await toggle.getAttribute('type'), 'button');
      assert.equal(await toggle.getAttribute('aria-controls'), 'Password');
    });
    await check('Login: mostrar/ocultar conserva valor y cambia aria-pressed', async () => {
      await pw.fill('QA-SINTETICA-NO-CREDENCIAL'); await toggle.click();
      assert.equal(await pw.getAttribute('type'), 'text');
      assert.equal(await toggle.getAttribute('aria-pressed'), 'true');
      assert.equal(await pw.inputValue(), 'QA-SINTETICA-NO-CREDENCIAL');
      await toggle.focus(); await page.keyboard.press('Space');
      assert.equal(await pw.getAttribute('type'), 'password');
      await page.keyboard.press('Enter');
      assert.equal(await pw.getAttribute('type'), 'text');
      return { activeElement: await page.evaluate(() => document.activeElement.id) };
    });
    await pw.fill(''); await open('login');
    await capture('login'); await capture('login', true);
    await check('Login: 390px sin overflow horizontal', async () => {
      const dims = await page.evaluate(() => ({ scrollWidth: document.documentElement.scrollWidth, width: innerWidth }));
      assert.ok(dims.scrollWidth <= dims.width, JSON.stringify(dims)); return dims;
    });

    await open('dte-procesado'); await capture('dte-procesado');
    await check('DTE PROCESADO: los cinco pasos completados y punto final morado', async () => {
      assert.equal(await page.locator('.ns-step--done').count(), 5);
      const colors = await page.locator('.ns-step-dot').evaluateAll(nodes => nodes.map(n => getComputedStyle(n).backgroundColor));
      assert.deepEqual(colors, Array(5).fill('rgb(107, 56, 212)')); return colors;
    });
    await capture('dte-procesado', true);
    await check('DTE PROCESADO: 390px sin overflow de página', async () => {
      const dims = await page.evaluate(() => ({ scrollWidth: document.documentElement.scrollWidth, width: innerWidth }));
      assert.ok(dims.scrollWidth <= dims.width, JSON.stringify(dims)); return dims;
    });
    for (const code of ['008', '096']) {
      await open('dte-error-' + code); await capture('dte-error-' + code);
      await check('Error ' + code + ': respuesta técnica renderizada', async () => {
        const model = JSON.parse(await fs.readFile(path.join(output, 'dte-error-' + code + '.model.json'), 'utf8'));
        const raw = JSON.parse(model.RespuestaHacienda), text = await page.locator('body').innerText();
        assert.ok(text.includes(raw.codigoMsg)); assert.ok(text.includes(raw.descripcionMsg));
        for (const observation of raw.observaciones || []) assert.ok(text.includes(observation));
      });
      await check('Error ' + code + ': consume mensaje y corrección estructurados de Diagnostico', async () => {
        const expected = JSON.parse(await fs.readFile(path.join(output, 'dte-error-' + code + '.model.json'), 'utf8')).Diagnostico;
        const text = await page.locator('body').innerText();
        assert.ok(text.includes(expected.Mensaje), 'Model.Diagnostico.Mensaje no aparece: ' + expected.Mensaje);
        assert.ok(text.includes(expected.Campos[0].AccionSugerida), 'Model.Diagnostico.Campos.AccionSugerida no aparece');
      });
    }
    for (const state of ['enviado', 'contingencia']) {
      await open('dte-' + state); await capture('dte-' + state);
      await check(state.toUpperCase() + ': guía de conciliación visible para envío incierto', async () => {
        const expected = JSON.parse(await fs.readFile(path.join(output, 'dte-' + state + '.model.json'), 'utf8')).Diagnostico;
        assert.equal(expected.SiguientePaso, 'CONCILIAR_HACIENDA');
        assert.ok((await page.locator('body').innerText()).includes(expected.AccionSugerida), 'Diagnóstico de conciliación ausente');
      });
      await check(state.toUpperCase() + ': no ofrece transmisión cuando requiere consultar Hacienda', async () => {
        assert.equal(await page.getByRole('button', { name: 'Enviar a Hacienda', exact: true }).count(), 0);
      });
    }
    await open('dte-readonly'); await capture('dte-readonly');
    await check('Rol solo DTE.Consultar: no presenta acciones fiscales de escritura', async () => {
      const names = await page.locator('button').allTextContents();
      assert.ok(!names.some(n => /Enviar a Hacienda|Invalidar|Revalidar|Regenerar JSON/.test(n)), JSON.stringify(names));
    });
    await open('dte-error'); await capture('dte-error-before-signing');
    await check('ERROR sin FirmadoAt/JsonFirmado: no afirma firma y validación completadas', async () => {
      const done = await page.locator('.ns-step--done .ns-step-label').allTextContents();
      assert.ok(!done.includes('Firmado'), JSON.stringify(done)); return done;
    });
    await open('dte-invalidado'); await capture('dte-invalidado-before-signing');
    await check('INVALIDADO sin envío/sello: no afirma envío completado', async () => {
      const done = await page.locator('.ns-step--done .ns-step-label').allTextContents();
      assert.ok(!done.includes('Enviado'), JSON.stringify(done)); return done;
    });

    await open('cliente'); await capture('cliente-sv');
    await check('Cliente SV: conserva municipio inicial y muestra solo hijos del departamento', async () => {
      assert.equal(await page.locator('#municipioSelect').inputValue(), '01');
      const visible = await page.locator('#municipioSelect option').evaluateAll(nodes => nodes.filter(n => n.value && !n.hidden && !n.disabled).map(n => n.textContent));
      assert.deepEqual(visible, ['Municipio QA 06-01', 'Municipio QA 06-02']); return visible;
    });
    await check('Cliente SV: cambiar departamento limpia municipio incompatible', async () => {
      await page.locator('#departamentoSelect').selectOption('05');
      assert.equal(await page.locator('#municipioSelect').inputValue(), '');
      const visible = await page.locator('#municipioSelect option').evaluateAll(nodes => nodes.filter(n => n.value && !n.hidden && !n.disabled).map(n => n.textContent));
      assert.deepEqual(visible, ['Municipio QA 05-03']); return visible;
    });
    await check('Cliente extranjero: oculta, deshabilita y limpia departamento/municipio', async () => {
      await page.locator('#municipioSelect').selectOption('03');
      await page.locator('#paisSelect').selectOption('9599');
      for (const id of ['departamentoSelect', 'municipioSelect']) {
        const element = page.locator('#' + id);
        assert.equal(await element.isVisible(), false); assert.equal(await element.isDisabled(), true);
        assert.equal(await element.inputValue(), '');
      }
    });
    await capture('cliente-extranjero'); await capture('cliente-extranjero', true);
    await check('Cliente vuelve a SV: territorios visibles y habilitados', async () => {
      await page.locator('#paisSelect').selectOption('9300');
      for (const id of ['departamentoSelect', 'municipioSelect']) {
        assert.equal(await page.locator('#' + id).isVisible(), true);
        assert.equal(await page.locator('#' + id).isDisabled(), false);
      }
    });
    await check('Cliente: labels enlazan país, documento, departamento y municipio', async () => {
      const bad = await page.locator('label[for]').evaluateAll(nodes => nodes.filter(n => !document.getElementById(n.htmlFor)).map(n => ({ label: n.textContent.trim(), for: n.htmlFor })));
      assert.deepEqual(bad, []); return bad;
    });
    await check('Cliente: clic en label Departamento enfoca su selector', async () => {
      await page.locator('label[for="DepartamentoCodigo"]').click();
      assert.equal(await page.evaluate(() => document.activeElement.id), 'departamentoSelect');
    });

    await open('retorno'); await capture('retorno');
    for (const term of ['DTE-QA-0155', 'GEN-QA-0155', 'Cliente sintético 155', '155.00']) {
      await check('Retorno: filtra documento cargado por ' + term, async () => {
        await page.locator('#retorno-busqueda').fill(term);
        const visible = await page.locator('#DocumentoOrigenId option').evaluateAll(nodes => nodes.filter(n => n.value && !n.hidden).map(n => n.value));
        assert.deepEqual(visible, ['155']); return visible;
      });
    }
    await page.locator('#retorno-busqueda').fill('DTE-QA-0201'); await capture('retorno-doc201');
    checks.push({ name: 'Retorno doc201', result: 'LIMITATION', actual: 'La fixture replica solo 200 opciones del controlador. La búsqueda local no puede localizar DTE-QA-0201 ausente; esto no prueba una consulta real a BD.' });
    await check('Vistas con JS real: sin excepciones de página', async () => { assert.deepEqual(pageErrors, []); });
  } finally {
    await browser.close();
    const report = { scope: 'Razor real+CSS/JS locales, fixtures aisladas. No AppShell, controllers, HTTP API, SQL ni Hacienda. Fonts externos bloqueados; no autenticación real.', browser: 'Edge headless', viewports: ['1280x900', '390x844'], checks, captures, pageErrors, blockedRequests: [...blocked] };
    await fs.writeFile(path.join(output, 'browser-report.json'), JSON.stringify(report, null, 2));
    console.log(JSON.stringify({ pass: checks.filter(c => c.result === 'PASS').length, fail: checks.filter(c => c.result === 'FAIL').length, limitation: checks.filter(c => c.result === 'LIMITATION').length, output }, null, 2));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
