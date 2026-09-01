// Save-file helpers. localStorage is reachable from .NET directly via IJSRuntime,
// so the only thing that needs bespoke JS is handing the player a file.
window.wrestleSim = {
    // Triggers a download of `text` as `filename`. Revokes the object URL afterwards
    // so a long session exporting repeatedly does not leak blobs.
    downloadText: function (filename, text) {
        const blob = new Blob([text], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(url), 0);
    },

    // localStorage throws rather than returning null in private-browsing modes and
    // wherever site data is blocked, so every access is guarded and the caller is
    // told plainly that persistence is unavailable rather than silently losing a save.
    storageAvailable: function () {
        try {
            const probe = '__wrestlesim_probe__';
            window.localStorage.setItem(probe, probe);
            window.localStorage.removeItem(probe);
            return true;
        } catch (e) {
            return false;
        }
    },

    getItem: function (key) {
        try { return window.localStorage.getItem(key); } catch (e) { return null; }
    },

    setItem: function (key, value) {
        try { window.localStorage.setItem(key, value); return true; } catch (e) { return false; }
    },

    removeItem: function (key) {
        try { window.localStorage.removeItem(key); return true; } catch (e) { return false; }
    },

    // Keys are enumerated so the landing page can list every save in this browser.
    keys: function () {
        try {
            const out = [];
            for (let i = 0; i < window.localStorage.length; i++) out.push(window.localStorage.key(i));
            return out;
        } catch (e) {
            return [];
        }
    }
};
