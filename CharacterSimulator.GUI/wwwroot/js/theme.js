/**
 * Theme apply helpers — no eval; FOUC-safe boot from localStorage.
 */
(function () {
    const STORAGE_KEY = "cs.uiTheme";
    const DEFAULT_THEME = "midnight";

    function normalize(id) {
        if (!id || typeof id !== "string") return DEFAULT_THEME;
        const t = id.trim().toLowerCase();
        const allowed = ["midnight", "cyberpunk", "matrix", "amber", "obsidian"];
        return allowed.indexOf(t) >= 0 ? t : DEFAULT_THEME;
    }

    window.csTheme = {
        apply: function (themeId) {
            const id = normalize(themeId);
            try {
                document.documentElement.setAttribute("data-theme", id);
                localStorage.setItem(STORAGE_KEY, id);
            } catch (_) {
                document.documentElement.setAttribute("data-theme", id);
            }
            return id;
        },
        get: function () {
            try {
                return normalize(localStorage.getItem(STORAGE_KEY) || DEFAULT_THEME);
            } catch (_) {
                return DEFAULT_THEME;
            }
        },
        /**
         * Soft full-app scene backdrop from portrait/scene art.
         * @param {string|null|undefined} url data URI, http(s), or empty to clear
         */
        setSceneBackdrop: function (url) {
            const root = document.documentElement;
            try {
                if (url && typeof url === "string" && url.trim().length > 0) {
                    // Escape quotes inside data URIs for CSS url("...")
                    const safe = url.trim().replace(/\\/g, "\\\\").replace(/"/g, '\\"');
                    root.style.setProperty("--scene-backdrop-image", 'url("' + safe + '")');
                    root.classList.add("has-scene-backdrop");
                } else {
                    root.style.removeProperty("--scene-backdrop-image");
                    root.classList.remove("has-scene-backdrop");
                }
            } catch (_) {
                root.classList.remove("has-scene-backdrop");
            }
        }
    };

    // Boot before Blazor paints when possible
    try {
        const boot = normalize(localStorage.getItem(STORAGE_KEY) || DEFAULT_THEME);
        document.documentElement.setAttribute("data-theme", boot);
    } catch (_) {
        document.documentElement.setAttribute("data-theme", DEFAULT_THEME);
    }
})();
