const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('playwright');
(async () => {
  const workspace = path.resolve(process.argv[2]);
  const output = path.join(workspace, 'tmp/district-mail/web');
  const assets = path.join(workspace, 'src/NeoSTP.Web/wwwroot');
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  const errors = [], checks = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.hostname !== 'web-audit.test' || route.request().method() !== 'GET') return route.abort();
    const root = url.pathname.endsWith('.html') ? output : assets;
    const file = path.resolve(root, '.' + decodeURIComponent(url.pathname));
    if (!file.startsWith(root + path.sep)) return route.abort();
    try { await route.fulfill({ body: await fs.readFile(file), contentType: file.endsWith('.html') ? 'text/html' : file.endsWith('.css') ? 'text/css' : 'application/javascript' }); }
    catch { await route.abort(); }
  });
  async function check(name, test) { await test(); checks.push(name); }
  try {
    await page.goto('http://web-audit.test/cliente.html');
    await check('Cliente conserva distrito guardado', async () => assert.equal(await page.locator('#distritoSelect').inputValue(), 'DIS1'));
    await page.locator('#departamentoSelect').selectOption('DEP2');
    await check('Cambiar departamento limpia municipio y distrito', async () => {
      assert.equal(await page.locator('#municipioSelect').inputValue(), '');
      assert.equal(await page.locator('#distritoSelect').inputValue(), '');
    });
    await page.locator('#municipioSelect').selectOption('MUN2');
    await page.locator('#distritoSelect').selectOption('DIS2');
    await check('Distrito de otro municipio deshabilitado', async () => assert.equal(await page.locator('#distritoSelect option[value="DIS1"]').isDisabled(), true));
    await page.locator('#paisSelect').selectOption('US');
    await check('Extranjero oculta limpia y deshabilita territorio', async () => {
      assert.equal(await page.locator('#distritoSelect').inputValue(), '');
      assert.equal(await page.locator('#distritoSelect').isVisible(), false);
      assert.equal(await page.locator('#distritoSelect').isDisabled(), true);
    });
    await page.locator('#paisSelect').selectOption('SV');
    await check('Regresar a SV habilita territorio', async () => assert.equal(await page.locator('#distritoSelect').isDisabled(), false));
    await page.goto('http://web-audit.test/factura.html');
    await check('Factura autocompleta dirección distrito y correo del cliente', async () => {
      assert.equal(await page.locator('#ReceptorDistritoCodigo').inputValue(), 'DIS1');
      assert.equal(await page.locator('#ReceptorCorreo').inputValue(), 'receiver@example.invalid');
      assert.equal(await page.locator('#ReceptorDireccion').inputValue(), 'Dirección sintética');
      assert.equal(await page.locator('#ReceptorDistritoCodigo').isDisabled(), true);
      assert.equal(await page.locator('#editar-cliente-territorio').isVisible(), true);
    });
    await page.screenshot({ path: path.join(output, 'factura-desktop.png'), fullPage: true });
    await page.locator('#ClienteId').selectOption('778');
    await check('Factura cliente extranjero sin territorio local', async () => assert.equal(await page.locator('#ReceptorDistritoCodigo').isVisible(), false));
    await page.locator('#ClienteId').selectOption('');
    await page.locator('#ReceptorDepartamentoCodigo').selectOption('DEP2');
    await page.locator('#ReceptorMunicipioCodigo').selectOption('MUN2');
    await page.locator('#ReceptorDistritoCodigo').selectOption('DIS2');
    await check('Factura manual solo muestra distritos del municipio seleccionado', async () => assert.equal(await page.locator('#ReceptorDistritoCodigo option[value="DIS1"]').count(), 0));
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'factura-mobile.png'), fullPage: true });
    await check('Formulario receptor sin desbordamiento móvil', async () => {
      const bounds = await page.locator('#ReceptorDistritoCodigo').boundingBox();
      assert.ok(bounds && bounds.x >= 0 && bounds.x + bounds.width <= 390);
    });
    assert.deepEqual(errors, []);
    await fs.writeFile(path.join(output, 'district-browser-report.json'), JSON.stringify({ passed: true, checks, pageErrors: errors, liveRequests: 0 }, null, 2));
    console.log(JSON.stringify({ passed: true, checks: checks.length, pageErrors: errors }));
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
