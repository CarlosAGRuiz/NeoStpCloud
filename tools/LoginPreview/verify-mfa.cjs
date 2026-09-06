const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('playwright');

(async () => {
    const root = path.resolve(process.argv[2]);
    const output = path.join(root, 'tmp', 'login-preview');
    const assets = path.join(root, 'src', 'NeoSTP.Web', 'wwwroot');
    const views = ['mfa-enrollment-initial', 'mfa-enrollment', 'mfa-verification', 'mfa-recovery', 'external-link'];
    const html = Object.fromEntries(await Promise.all(views.map(async v => [v, await fs.readFile(path.join(output, v + '.html'), 'utf8')])));
    const browser = await chromium.launch({ headless: true, channel: 'msedge' });
    const checks = [];
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.route('**/*', async route => {
            const url = new URL(route.request().url());
            if (url.hostname !== 'login-preview.test') return route.abort();
            const view = url.pathname.slice(1);
            if (html[view]) return route.fulfill({ contentType: 'text/html', body: html[view] });
            const file = path.resolve(assets, '.' + decodeURIComponent(url.pathname));
            if (!file.startsWith(assets + path.sep)) return route.abort();
            try { return route.fulfill({ body: await fs.readFile(file), contentType: file.endsWith('.css') ? 'text/css' : 'application/javascript' }); }
            catch { return route.abort(); }
        });
        for (const view of views) {
            for (const [device, width, height] of [['desktop', 1280, 1000], ['mobile', 390, 844]]) {
                await page.setViewportSize({ width, height });
                await page.goto('http://login-preview.test/' + view);
                assert.equal(await page.locator('main h1').count(), 1);
                assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
                assert.equal(await page.locator('.ns-sidebar').count(), 0);
                await page.screenshot({ path: path.join(output, view + '-' + device + '.png'), fullPage: true });
                checks.push(view + ': título, sin menú operativo ni desbordamiento (' + device + ')');
            }
        }
        await page.goto('http://login-preview.test/mfa-enrollment-initial');
        assert.equal(await page.locator('#mfa-secret').count(), 0); checks.push('GET inicial sin secreto');
        await page.goto('http://login-preview.test/mfa-enrollment');
        assert.equal(await page.locator('#mfa-secret').innerText(), 'JBSWY3DPEHPK3PXP');
        assert.equal(await page.locator('input[name="Enrollment.Secret"]').count(), 0); checks.push('Clave visible sin reenviarla en inputs');
        for (const view of ['mfa-enrollment', 'mfa-verification']) {
            await page.goto('http://login-preview.test/' + view);
            assert.equal(await page.locator('#Code').getAttribute('type'), 'password');
            assert.equal(await page.locator('form:not(:has(input[name="__RequestVerificationToken"]))').count(), 0);
            checks.push(view + ': código oculto y antiforgery en formularios');
        }
        await page.goto('http://login-preview.test/mfa-recovery');
        assert.equal(await page.locator('#recovery-codes li').count(), 10); checks.push('Diez códigos sintéticos visibles');
        await page.goto('http://login-preview.test/external-link');
        assert.equal(await page.locator('#Password').getAttribute('type'), 'password');
        assert.equal(await page.locator('#Password').inputValue(), '');
        assert.equal(await page.locator('form:not(:has(input[name="__RequestVerificationToken"]))').count(), 0);
        assert.equal(await page.locator('input[name="Issuer"], input[name="Subject"], input[name="Proveedor"]').count(), 0);
        checks.push('Vinculación SSO: contraseña oculta, CSRF e identidad no reenviada en inputs');
        assert.deepEqual(errors, []); checks.push('Sin errores JavaScript');
        console.log(JSON.stringify({ passed: checks.length, checks }, null, 2));
    } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
