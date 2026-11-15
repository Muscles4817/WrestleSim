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

    // ── The large store ──────────────────────────────────────────────────────
    // Saves live in IndexedDB, gzip-compressed, each with a small header beside it so the
    // save list never has to unpack a career to show it. localStorage gives a site about
    // 5 MB, which a full-size world outgrows within weeks of play; IndexedDB gives hundreds.
    // Everything is guarded and answers false/null rather than throwing, like the rest.
    store: (function () {
        const DB = 'wrestlesim', STORE = 'saves';

        function open() {
            return new Promise((resolve, reject) => {
                const r = indexedDB.open(DB, 1);
                r.onupgradeneeded = () => r.result.createObjectStore(STORE, { keyPath: 'key' });
                r.onsuccess = () => resolve(r.result);
                r.onerror = () => reject(r.error);
            });
        }

        function run(mode, act) {
            return open().then(db => new Promise((resolve, reject) => {
                const tx = db.transaction(STORE, mode);
                const req = act(tx.objectStore(STORE));
                tx.oncomplete = () => { db.close(); resolve(req ? req.result : undefined); };
                tx.onerror = tx.onabort = () => { db.close(); reject(tx.error); };
            }));
        }

        async function pack(text) {
            if (!window.CompressionStream) return { packed: false, data: text };
            const stream = new Blob([text]).stream().pipeThrough(new CompressionStream('gzip'));
            return { packed: true, data: await new Response(stream).blob() };
        }

        async function unpack(record) {
            if (!record.packed) return record.data;
            const stream = record.data.stream().pipeThrough(new DecompressionStream('gzip'));
            return await new Response(stream).text();
        }

        return {
            available: async function () {
                try { (await open()).close(); return true; } catch (e) { return false; }
            },
            put: async function (key, header, text) {
                try {
                    const p = await pack(text);
                    await run('readwrite', s => s.put({ key, header, packed: p.packed, data: p.data, length: text.length }));
                    return true;
                } catch (e) { console.error('save failed', e); return false; }
            },
            get: async function (key) {
                try {
                    const record = await run('readonly', s => s.get(key));
                    return record ? await unpack(record) : null;
                } catch (e) { console.error('load failed', e); return null; }
            },
            headers: async function () {
                try {
                    const all = await run('readonly', s => s.getAll());
                    return (all || []).map(r => ({ key: r.key, header: r.header }));
                } catch (e) { return []; }
            },
            remove: async function (key) {
                try { await run('readwrite', s => s.delete(key)); return true; } catch (e) { return false; }
            }
        };
    })(),

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
