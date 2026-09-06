(() => {
    "use strict";
    const password = document.getElementById("Password");
    const toggle = document.getElementById("toggle-password");
    if (!password || !toggle) return;

    const hide = () => {
        password.type = "password";
        toggle.setAttribute("aria-pressed", "false");
        toggle.setAttribute("aria-label", "Mostrar contraseña");
        toggle.title = "Mostrar contraseña";
        toggle.querySelector("[data-password-slash]").style.display = "none";
    };
    toggle.addEventListener("click", () => {
        if (password.type === "password") {
            password.type = "text";
            toggle.setAttribute("aria-pressed", "true");
            toggle.setAttribute("aria-label", "Ocultar contraseña");
            toggle.title = "Ocultar contraseña";
            toggle.querySelector("[data-password-slash]").style.display = "";
        } else {
            hide();
        }
    });
    password.form?.addEventListener("submit", hide);
    window.addEventListener("pagehide", hide);
    document.addEventListener("visibilitychange", () => {
        if (document.hidden) hide();
    });
})();
