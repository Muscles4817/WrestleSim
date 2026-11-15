using Microsoft.JSInterop;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;

namespace WrestlingSim.Web.Services;

/// <summary>One save as the landing page needs to list it, without loading the whole thing.</summary>
public record SaveSlot(
    string Key,
    string CareerId,
    string PromotionName,
    string TierLabel,
    DateOnly CurrentDate,
    DateTime LastPlayedUtc,
    int ShowsRun);

/// <summary>
/// Reads and writes careers to the browser.
///
/// There is no server in this build — it publishes to static files — so a save lives in
/// the browser, with export/import as the portable escape hatch. Since Nov 2026 that is
/// IndexedDB, gzip-compressed, with a small header beside each save for the list: a
/// full-size world outgrows localStorage's ~5 MB within weeks. Saves already in localStorage
/// are moved across the first time the list is read, and localStorage remains the fallback
/// where IndexedDB is unavailable. Every storage call is guarded, because browser storage
/// throws outright in private-browsing modes and wherever site data is blocked; when that
/// happens the game stays playable and says that saving is unavailable rather than
/// pretending it worked.
/// </summary>
public class SaveStore
{
    private const string KeyPrefix = "wrestlesim.save.";

    private readonly IJSRuntime _js;

    public SaveStore(IJSRuntime js) => _js = js;

    /// <summary>Null until probed. False means autosave is off for this browser.</summary>
    public bool? StorageAvailable { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>
    /// Whether the large store (IndexedDB) is open to us. When it is not — some locked-down
    /// browsers — saves fall back to localStorage, which is how every save was kept before
    /// Nov 2026 and still works for a small world.
    /// </summary>
    private bool largeStore;

    public async Task<bool> ProbeStorageAsync()
    {
        try
        {
            largeStore = await _js.InvokeAsync<bool>("wrestleSim.store.available");
            StorageAvailable = largeStore || await _js.InvokeAsync<bool>("wrestleSim.storageAvailable");
        }
        catch
        {
            // Interop itself failed — treat as no storage rather than breaking the app.
            StorageAvailable = false;
        }

        if (StorageAvailable == false)
            LastError = "This browser is blocking site data, so autosave is off. "
                      + "Use Export to keep your career.";

        return StorageAvailable ?? false;
    }

    // ── Listing ──────────────────────────────────────────────────────────────

    public async Task<List<SaveSlot>> ListAsync()
    {
        if (largeStore)
        {
            await MoveOldSavesAsync();
            var stored = await SafeAsync(() => _js.InvokeAsync<StoredHeader[]>("wrestleSim.store.headers").AsTask(), []);
            var listed = new List<SaveSlot>();
            foreach (var h in stored)
            {
                try
                {
                    if (System.Text.Json.JsonSerializer.Deserialize<SaveSlot>(h.Header) is { } slot)
                        listed.Add(slot with { Key = h.Key });
                }
                catch { /* an unreadable header is skipped; Delete is offered in the UI */ }
            }
            return listed.OrderByDescending(s => s.LastPlayedUtc).ToList();
        }

        var slots = new List<SaveSlot>();

        string[] keys;
        try
        {
            keys = await _js.InvokeAsync<string[]>("wrestleSim.keys");
        }
        catch
        {
            return slots;
        }

        foreach (var key in keys.Where(k => k?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true))
        {
            var json = await RawAsync(key);
            if (string.IsNullOrWhiteSpace(json)) continue;

            // A corrupt or half-written slot must not take the whole list down with it.
            try
            {
                var header = SaveHeader.Read(json);
                if (header != null) slots.Add(header with { Key = key });
            }
            catch
            {
                // Skip unreadable slots; Delete is offered in the UI.
            }
        }

        return slots.OrderByDescending(s => s.LastPlayedUtc).ToList();
    }

    // ── Read / write ─────────────────────────────────────────────────────────

    public async Task<bool> SaveAsync(Career career)
    {
        career.LastPlayedUtc = DateTime.UtcNow;

        string json = SaveSerializer.ToJson(career);
        bool ok = largeStore
            ? await PutAsync(KeyFor(career.Id), json)
            : await SetAsync(KeyFor(career.Id), json);

        LastError = ok
            ? null
            : "Could not write the save — browser storage is full or blocked. Export to keep this career.";

        return ok;
    }

    /// <summary>Loads a career by storage key, binding it against a freshly-loaded roster.</summary>
    public async Task<Career?> LoadAsync(string key, IReadOnlyList<Wrestler> roster)
    {
        var json = largeStore
            ? await SafeAsync(() => _js.InvokeAsync<string?>("wrestleSim.store.get", key).AsTask(), null)
            : null;
        json ??= await RawAsync(key);
        if (string.IsNullOrWhiteSpace(json))
        {
            LastError = "That save could not be read.";
            return null;
        }

        try
        {
            LastError = null;
            return SaveSerializer.FromJson(json, roster);
        }
        catch (SaveLoadException ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public async Task DeleteAsync(string key)
    {
        if (largeStore) await SafeAsync(() => _js.InvokeAsync<bool>("wrestleSim.store.remove", key).AsTask(), false);
        try { await _js.InvokeVoidAsync("wrestleSim.removeItem", key); } catch { /* nothing to do */ }
    }

    // ── Export / import ──────────────────────────────────────────────────────

    public async Task ExportAsync(Career career)
    {
        string json = SaveSerializer.ToJson(career, indented: true);
        string safeName = Sanitise(career.Promotion.Name);
        string filename = $"{safeName}-{career.CurrentDate:yyyy-MM-dd}.wrestlesim.json";

        try
        {
            await _js.InvokeVoidAsync("wrestleSim.downloadText", filename, json);
            LastError = null;
        }
        catch
        {
            LastError = "The browser blocked the download.";
        }
    }

    /// <summary>
    /// Parses an imported file. The career gets a fresh id so importing never silently
    /// overwrites a different career that happens to share one.
    /// </summary>
    public Career? Import(string json, IReadOnlyList<Wrestler> roster)
    {
        try
        {
            var career = SaveSerializer.FromJson(json, roster);
            career.Id = Guid.NewGuid().ToString("N");
            LastError = null;
            return career;
        }
        catch (SaveLoadException ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────

    private sealed record StoredHeader(string Key, string Header);

    /// <summary>A save into the large store, with its header worked out here so listing never unpacks it.</summary>
    private async Task<bool> PutAsync(string key, string json)
    {
        var header = SaveHeader.Read(json);
        if (header is null) return false;
        string headerJson = System.Text.Json.JsonSerializer.Serialize(header with { Key = key });
        return await SafeAsync(() => _js.InvokeAsync<bool>("wrestleSim.store.put", key, headerJson, json).AsTask(), false);
    }

    /// <summary>
    /// Saves kept in localStorage before the large store existed are moved into it, once, and
    /// removed from localStorage only after they are safely written.
    /// </summary>
    private async Task MoveOldSavesAsync()
    {
        var keys = await SafeAsync(() => _js.InvokeAsync<string[]>("wrestleSim.keys").AsTask(), []);
        foreach (var key in keys.Where(k => k?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true))
        {
            var json = await RawAsync(key);
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                if (await PutAsync(key, json))
                    await _js.InvokeVoidAsync("wrestleSim.removeItem", key);
            }
            catch { /* left where it was; it still loads from there */ }
        }
    }

    private static async Task<T> SafeAsync<T>(Func<Task<T>> call, T fallback)
    {
        try { return await call(); }
        catch { return fallback; }
    }

    private static string KeyFor(string careerId) => KeyPrefix + careerId;

    private async Task<string?> RawAsync(string key)
    {
        try { return await _js.InvokeAsync<string?>("wrestleSim.getItem", key); }
        catch { return null; }
    }

    private async Task<bool> SetAsync(string key, string value)
    {
        try { return await _js.InvokeAsync<bool>("wrestleSim.setItem", key, value); }
        catch { return false; }
    }

    private static string Sanitise(string name)
    {
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        cleaned = cleaned.Trim('-');
        return string.IsNullOrEmpty(cleaned) ? "career" : cleaned.ToLowerInvariant();
    }
}
