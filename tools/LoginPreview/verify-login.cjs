const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const { chromium } = require("playwright");

(async () => {
    const workspace = path.resolve(process.argv[2]);
    const assets = path.join(workspace, "src", "NeoSTP.Web", "wwwroot");
    const output = path.join(workspace, "tmp", "login-preview");
    const html = await fs.readFile(path.join(output, "login.html"), "utf8");
    const browser = await chromium.launch({ headless: true, channel: "msedge" });
    const checks = [];
    try {
        const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
        const jsErrors = [];
        page.on("pageerror", error => jsErrors.push(error.message));
        await page.route("**/*", async route => {
            const url = new URL(route.request().url());
            // Solo contenido generado y assets locales. Sin red ni servidor del cliente.
            if (url.hostname !== "login-preview.test") return route.abort();
            if (url.pathname === "/Account/Login")
                return route.fulfill({ contentType: "text/html", body: html });
            const asset = path.resolve(assets, "." + decodeURIComponent(url.pathname));
            if (!asset.startsWith(assets + path.sep)) return route.abort();
            try {
                const body = await fs.readFile(asset);
                const type = asset.endsWith(".css") ? "text/css" : asset.endsWith(".js")
                    ? "application/javascript" : "application/octet-stream";
                return route.fulfill({ contentType: type, body });
            } catch { return route.abort(); }
        });
        await page.goto("http://login-preview.test/Account/Login");
        const password = page.locator("#Password");
        const button = page.locator("#toggle-password");
        assert.equal(await password.getAttribute("type"), "password"); checks.push("Contraseña oculta al abrir");
        assert.equal(await button.getAttribute("type"), "button"); checks.push("El visor no envía el formulario");
        assert.equal(await button.getAttribute("aria-controls"), "Password"); checks.push("Control accesible asociado al campo");
        await password.fill("Dato-sintetico-123!");
        await button.click();
        assert.equal(await password.getAttribute("type"), "text");
        assert.equal(await password.inputValue(), "Dato-sintetico-123!"); checks.push("Mostrar conserva exactamente el valor");
        assert.equal(await button.getAttribute("aria-label"), "Ocultar contraseña");
        assert.equal(await button.getAttribute("aria-pressed"), "true"); checks.push("Etiqueta y estado accesibles cambian");
        await button.focus();
        await page.keyboard.press("Space");
        assert.equal(await password.getAttribute("type"), "password"); checks.push("Ocultar funciona con teclado");
        await page.keyboard.press("Enter");
        assert.equal(await password.getAttribute("type"), "text"); checks.push("Mostrar funciona con Enter");
        await page.evaluate(() => {
            const form = document.getElementById("Password").form;
            form.addEventListener("submit", event => event.preventDefault(), { once: true });
            form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
        });
        assert.equal(await password.getAttribute("type"), "password"); checks.push("Se oculta al enviar");
        await button.click();
        await page.evaluate(() => {
            Object.defineProperty(document, "hidden", { configurable: true, value: true });
            document.dispatchEvent(new Event("visibilitychange"));
            Object.defineProperty(document, "hidden", { configurable: true, value: false });
        });
        assert.equal(await password.getAttribute("type"), "password"); checks.push("Se oculta al dejar la pestaña");
        await button.click();
        await page.evaluate(() => window.dispatchEvent(new Event("pagehide")));
        assert.equal(await password.getAttribute("type"), "password"); checks.push("Se oculta al salir de la página");
        assert.equal(await page.locator("#MfaCode").getAttribute("type"), "password"); checks.push("MFA permanece oculto e independiente");
        await password.fill("");
        await page.reload();
        await page.screenshot({ path: path.join(output, "login-desktop.png"), fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        await page.screenshot({ path: path.join(output, "login-mobile.png"), fullPage: true });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
        checks.push("Sin desbordamiento horizontal en móvil");
        assert.deepEqual(jsErrors, []); checks.push("Sin errores JavaScript");
        console.log(JSON.stringify({ passed: checks.length, checks }, null, 2));
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
