using WrestlingSim.Engine;
using WrestlingSim.Models;

namespace WrestlingSim.Web.Services;

public enum Screen
{
    Menu,
    Roster,
    Feuds,
    Match,
    Segment,
    Show
}

/// <summary>
/// Session state for the web build: the loaded roster plus the one FeudBook every
/// booking flow reads and writes. Mirrors what Program.Main holds in the console app.
/// </summary>
public class GameState
{
    public List<Wrestler> Roster { get; private set; } = new();
    public FeudBook FeudBook { get; } = new();

    public Screen Current { get; private set; } = Screen.Menu;
    public bool Loaded { get; private set; }
    public string? LoadError { get; private set; }

    /// <summary>Raised whenever state changes so the shell can re-render.</summary>
    public event Action? Changed;

    public void Load()
    {
        if (Loaded) return;

        try
        {
            // Read straight out of the engine assembly — no HTTP, so this works
            // identically at any hosting sub-path.
            Roster = DataLoaders.LoadEmbeddedWrestlers();

            if (Roster.Count < 2)
                LoadError = $"Only {Roster.Count} wrestler(s) loaded — need at least two to book anything.";
        }
        catch (Exception ex)
        {
            LoadError = $"Could not load the roster: {ex.Message}";
        }
        finally
        {
            Loaded = true;
            Notify();
        }
    }

    public void Go(Screen screen)
    {
        Current = screen;
        Notify();
    }

    public void Notify() => Changed?.Invoke();

    // ── Derived helpers the screens share ────────────────────────────────────

    public int ActiveFeudCount => FeudBook.All.Count;

    public IEnumerable<Wrestler> RosterByPopularity =>
        Roster.OrderByDescending(w => w.Popularity);
}
