using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Web.Services;

public enum Screen
{
    // Outside a save
    Landing,
    NewSave,

    // Inside a save
    Dashboard,
    Calendar,
    Roster,
    Feuds,
    BookShow,

    // Exhibition — the old sandbox, kept for trying the engine without a career
    Exhibition,
    Match,
    Segment,
    Show
}

public enum AppMode { Landing, Career, Exhibition }

/// <summary>
/// The app's whole state: which mode we are in, the career if there is one, and the
/// roster every mode reads from.
///
/// Before this existed the game had no notion of being inside a save, which meant it had
/// no clock, and without a clock nothing that decays — heat, freshness, momentum — could
/// be expressed. Everything hangs off <see cref="Career"/> now.
/// </summary>
public class GameState
{
    private readonly SaveStore _saves;

    /// <summary>Feuds for exhibition mode, so sandbox booking never touches a career.</summary>
    private readonly FeudBook _exhibitionFeuds = new();

    public GameState(SaveStore saves) => _saves = saves;

    // ── Roster ───────────────────────────────────────────────────────────────

    /// <summary>The shipped roster, loaded once. A career binds its own copy against this.</summary>
    public List<Wrestler> BaseRoster { get; private set; } = new();

    public bool Loaded { get; private set; }
    public string? LoadError { get; private set; }

    // ── Mode ─────────────────────────────────────────────────────────────────

    public Career? Career { get; private set; }
    public Screen Current { get; private set; } = Screen.Landing;

    public AppMode Mode => Current switch
    {
        Screen.Landing or Screen.NewSave => AppMode.Landing,
        Screen.Exhibition or Screen.Match or Screen.Segment or Screen.Show => AppMode.Exhibition,
        _ => AppMode.Career
    };

    public bool InCareer => Career != null;

    /// <summary>
    /// Where "back" goes. Roster and Feuds are shared between career and exhibition, so
    /// they cannot hard-code a destination.
    /// </summary>
    public Screen HomeScreen => Career != null
        ? Screen.Dashboard
        : Mode == AppMode.Exhibition ? Screen.Exhibition : Screen.Landing;

    /// <summary>The show currently being booked or reviewed. Career mode only.</summary>
    public ScheduledShow? ActiveShow { get; private set; }

    /// <summary>Saves found in this browser, refreshed on landing.</summary>
    public List<SaveSlot> Slots { get; private set; } = new();

    public string? SaveMessage { get; private set; }
    public bool StorageBlocked => _saves.StorageAvailable == false;

    public event Action? Changed;

    // ── Roster / feuds the screens read ──────────────────────────────────────

    public List<Wrestler> Roster => Career?.Roster ?? BaseRoster;
    public FeudBook FeudBook => Career?.FeudBook ?? _exhibitionFeuds;
    public int ActiveFeudCount => FeudBook.All.Count;
    public IEnumerable<Wrestler> RosterByPopularity => Roster.OrderByDescending(w => w.Popularity);

    // ── Startup ──────────────────────────────────────────────────────────────

    public async Task InitialiseAsync()
    {
        if (Loaded) return;

        try
        {
            // Straight out of the engine assembly — no HTTP, so this works at any sub-path.
            BaseRoster = DataLoaders.LoadEmbeddedWrestlers();

            if (BaseRoster.Count < 2)
                LoadError = $"Only {BaseRoster.Count} wrestler(s) loaded — need at least two to book anything.";
        }
        catch (Exception ex)
        {
            LoadError = $"Could not load the roster: {ex.Message}";
        }

        Loaded = true;
        Notify();

        await _saves.ProbeStorageAsync();
        await RefreshSlotsAsync();
    }

    public async Task RefreshSlotsAsync()
    {
        Slots = await _saves.ListAsync();
        Notify();
    }

    // ── Career lifecycle ─────────────────────────────────────────────────────

    public async Task StartCareerAsync(string promotionName, PromotionTier tier, DateOnly startDate)
    {
        var promotion = new Promotion
        {
            Name = string.IsNullOrWhiteSpace(promotionName) ? "New Promotion" : promotionName.Trim(),
            Tier = tier
        };

        var career = new Career
        {
            Promotion   = promotion,
            StartDate   = startDate,
            CurrentDate = startDate,
            // A career owns its own roster instances, so career popularity changes never
            // leak into exhibition mode or into another save opened in the same session.
            Roster = DataLoaders.LoadEmbeddedWrestlers()
        };

        SeedOpeningSchedule(career);

        Career = career;
        ActiveShow = null;
        Current = Screen.Dashboard;

        await SaveAsync();
        Notify();
    }

    /// <summary>
    /// Puts the first few shows on the board so a new save opens onto something to do
    /// rather than an empty calendar. Cadence comes from the tier.
    /// </summary>
    private static void SeedOpeningSchedule(Career career)
    {
        var promotion = career.Promotion;

        if (promotion.HasTelevision)
        {
            // Weekly television, starting on the first Monday on or after the start date.
            var first = career.StartDate;
            while (first.DayOfWeek != DayOfWeek.Monday) first = first.AddDays(1);

            for (int i = 0; i < 4; i++)
                career.Schedule($"{promotion.Name} Weekly", first.AddDays(i * 7), ShowType.Television);
        }
        else
        {
            // No TV: space house shows at the tier's natural interval.
            int gap = promotion.TypicalShowIntervalDays;
            for (int i = 0; i < 3; i++)
                career.Schedule($"{promotion.Name} Live", career.StartDate.AddDays(gap * (i + 1)), ShowType.HouseShow);
        }
    }

    public async Task LoadCareerAsync(string key)
    {
        // A career binds against its own roster instances for the same isolation reason
        // as StartCareerAsync.
        var roster = DataLoaders.LoadEmbeddedWrestlers();
        var career = await _saves.LoadAsync(key, roster);

        if (career == null)
        {
            SaveMessage = _saves.LastError ?? "That save could not be opened.";
            Notify();
            return;
        }

        Career = career;
        ActiveShow = null;
        Current = Screen.Dashboard;
        SaveMessage = null;
        Notify();
    }

    public async Task ImportCareerAsync(string json)
    {
        var roster = DataLoaders.LoadEmbeddedWrestlers();
        var career = _saves.Import(json, roster);

        if (career == null)
        {
            SaveMessage = _saves.LastError ?? "That file could not be imported.";
            Notify();
            return;
        }

        Career = career;
        ActiveShow = null;
        Current = Screen.Dashboard;
        SaveMessage = "Save imported.";

        await SaveAsync();
        Notify();
    }

    public async Task SaveAsync()
    {
        if (Career == null) return;

        bool ok = await _saves.SaveAsync(Career);
        SaveMessage = ok ? null : _saves.LastError;
        Notify();
    }

    public async Task ExportAsync()
    {
        if (Career == null) return;
        await _saves.ExportAsync(Career);
        SaveMessage = _saves.LastError;
        Notify();
    }

    public async Task DeleteSaveAsync(string key)
    {
        await _saves.DeleteAsync(key);
        await RefreshSlotsAsync();
    }

    public async Task ExitToLandingAsync()
    {
        if (Career != null) await SaveAsync();

        Career = null;
        ActiveShow = null;
        Current = Screen.Landing;
        await RefreshSlotsAsync();
    }

    public void EnterExhibition()
    {
        Career = null;
        ActiveShow = null;
        Current = Screen.Exhibition;
        Notify();
    }

    // ── Time ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances the clock. Refuses while a show is due — the calendar is a commitment,
    /// which is what makes scheduling a decision rather than a note.
    /// </summary>
    public async Task AdvanceAsync(int days = 1)
    {
        if (Career == null || Career.HasShowDue) return;

        for (int i = 0; i < days; i++)
            if (!Career.AdvanceOneDay()) break;

        await SaveAsync();
        Notify();
    }

    public async Task AdvanceToNextShowAsync()
    {
        if (Career == null || Career.HasShowDue) return;

        Career.AdvanceToNextShow();
        await SaveAsync();
        Notify();
    }

    // ── Shows ────────────────────────────────────────────────────────────────

    public void OpenShow(ScheduledShow show)
    {
        ActiveShow = show;
        Current = Screen.BookShow;
        Notify();
    }

    public async Task ScheduleShowAsync(string name, DateOnly date, ShowType type, string venue = "")
    {
        if (Career == null) return;

        var show = Career.Schedule(name, date, type, venue);
        await SaveAsync();
        OpenShow(show);
    }

    public async Task CancelShowAsync(ScheduledShow show)
    {
        if (Career == null) return;

        Career.Cancel(show);
        if (ActiveShow == show) ActiveShow = null;

        await SaveAsync();
        Notify();
    }

    /// <summary>
    /// Runs a booked show, keeps its result, and moves the clock to its date so the
    /// calendar and the world stay in step.
    /// </summary>
    public async Task RunShowAsync(ScheduledShow show)
    {
        if (Career == null || show.HasRun || show.Card.Count == 0) return;

        var result = new ShowSimulator(Career.FeudBook).Simulate(show.ToShow());
        show.Result = result;

        if (Career.CurrentDate < show.Date) Career.CurrentDate = show.Date;

        ActiveShow = show;
        await SaveAsync();
        Notify();
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public void Go(Screen screen)
    {
        // Leaving the booking screen drops the active show so returning to it later
        // always comes through the calendar with a deliberate choice of date.
        if (screen != Screen.BookShow) ActiveShow = null;

        Current = screen;
        Notify();
    }

    public void ClearMessage()
    {
        SaveMessage = null;
        Notify();
    }

    public void Notify() => Changed?.Invoke();
}
