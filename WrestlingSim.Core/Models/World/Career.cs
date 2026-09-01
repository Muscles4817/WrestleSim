using WrestlingSim.Engine;
using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A save. Everything that persists between sessions hangs off this: the promotion,
    /// the world clock, the roster whose state actually changes, the feud book, and the
    /// calendar of shows scheduled and run.
    ///
    /// This is the object the rest of the game was missing. Heat, freshness and momentum
    /// are only meaningful relative to a clock that advances and results that are kept —
    /// see docs/wrestling-reference/17-heat-and-getting-over.md.
    /// </summary>
    public class Career
    {
        /// <summary>Stable id so a browser can hold several saves side by side.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public required Promotion Promotion { get; set; }

        /// <summary>Today, in world time. Advancing this is the only way anything ages.</summary>
        public DateOnly CurrentDate { get; set; }

        /// <summary>The date the save began, so elapsed time can be reported.</summary>
        public DateOnly StartDate { get; set; }

        /// <summary>The roster this promotion can book. Mutated by results and persisted.</summary>
        public List<Wrestler> Roster { get; set; } = new();

        /// <summary>Every feud the booker has going.</summary>
        public FeudBook FeudBook { get; set; } = new();

        /// <summary>Shows scheduled and run, oldest first.</summary>
        public List<ScheduledShow> Shows { get; set; } = new();

        /// <summary>
        /// The promotion's standing commitments — the recurring shows it runs. The
        /// calendar is generated from these; see <see cref="MaterialiseSchedule"/>.
        /// </summary>
        public List<ShowDefinition> ShowDefinitions { get; set; } = new();

        /// <summary>
        /// How far ahead the calendar is filled in from the definitions. Kept as a
        /// rolling window rather than generating years at once, and topped up whenever
        /// the clock moves.
        /// </summary>
        public const int ScheduleHorizonDays = 120;

        /// <summary>Real-world clock, for the save list. Not world time.</summary>
        public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;

        // ── Calendar queries ─────────────────────────────────────────────────

        public IEnumerable<ScheduledShow> Upcoming =>
            Shows.Where(s => !s.HasRun).OrderBy(s => s.Date).ThenBy(s => s.Name);

        public IEnumerable<ScheduledShow> Completed =>
            Shows.Where(s => s.HasRun).OrderByDescending(s => s.Date);

        /// <summary>Shows on a specific date, in the order they were scheduled.</summary>
        public IEnumerable<ScheduledShow> On(DateOnly date) =>
            Shows.Where(s => s.Date == date);

        /// <summary>
        /// The next show that has not been run. This is what the dashboard counts down to
        /// and what "advance to next show" jumps at.
        /// </summary>
        public ScheduledShow? NextShow => Upcoming.FirstOrDefault();

        /// <summary>
        /// Shows that are due — on or before today and not yet run. A career cannot
        /// advance past one of these; you have to run it or cancel it.
        /// </summary>
        public IEnumerable<ScheduledShow> Due =>
            Shows.Where(s => !s.HasRun && s.Date <= CurrentDate).OrderBy(s => s.Date);

        public bool HasShowDue => Due.Any();

        public int DaysUntil(ScheduledShow show) =>
            show.Date.DayNumber - CurrentDate.DayNumber;

        public int WeeksElapsed => (CurrentDate.DayNumber - StartDate.DayNumber) / 7;

        // ── Roster queries ───────────────────────────────────────────────────

        public Wrestler? FindWrestler(string id) =>
            Roster.FirstOrDefault(w => w.Id == id);

        public IEnumerable<Wrestler> RosterByPopularity =>
            Roster.OrderByDescending(w => w.Popularity);

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Moves the clock forward one day. Refuses to step over a show that is due —
        /// the calendar is a commitment, not a suggestion.
        /// </summary>
        public bool AdvanceOneDay()
        {
            if (HasShowDue) return false;

            CurrentDate = CurrentDate.AddDays(1);

            // Keep the rolling window full, so the calendar never runs dry ahead of you.
            MaterialiseSchedule();
            return true;
        }

        /// <summary>
        /// Advances to the next show's date, or by the given cap if that is sooner.
        /// Returns how many days actually passed.
        /// </summary>
        public int AdvanceToNextShow(int maxDays = 60)
        {
            if (HasShowDue) return 0;

            var next = NextShow;
            int target = next != null
                ? Math.Min(DaysUntil(next), maxDays)
                : maxDays;

            int moved = 0;
            while (moved < target && AdvanceOneDay()) moved++;
            return moved;
        }

        /// <summary>
        /// Fills the calendar forward from every active definition, out to the rolling
        /// horizon. Returns the shows it added.
        ///
        /// Only ever adds. An instance the player has renamed, re-venued, booked or run is
        /// never touched, and a date that already has an instance from the same definition
        /// is skipped — so this is safe to call on every clock tick.
        /// </summary>
        public IReadOnlyList<ScheduledShow> MaterialiseSchedule(int? horizonDays = null)
        {
            var added = new List<ScheduledShow>();
            var horizon = CurrentDate.AddDays(horizonDays ?? ScheduleHorizonDays);

            foreach (var definition in ShowDefinitions.Where(d => d.Active))
            {
                // Existing dates for this definition, so a re-run adds nothing.
                var taken = Shows
                    .Where(s => s.DefinitionId == definition.Id)
                    .Select(s => s.Date)
                    .ToHashSet();

                foreach (var date in definition.OccurrencesBetween(CurrentDate, horizon))
                {
                    if (!taken.Add(date)) continue;

                    var show = new ScheduledShow
                    {
                        Name           = definition.Name,
                        Date           = date,
                        Type           = definition.Type,
                        Venue          = definition.Venue,
                        DefinitionId   = definition.Id,
                        RuntimeMinutes = definition.RuntimeMinutes ?? Promotion.DefaultRuntimeFor(definition.Type),
                        Attendance     = Promotion.TypicalAttendanceFor(definition.Type)
                    };

                    Shows.Add(show);
                    added.Add(show);
                }
            }

            return added;
        }

        /// <summary>
        /// Stops a definition producing new dates and clears the ones it has already put
        /// on the calendar that nothing has happened to yet, today's included.
        ///
        /// The line is booked-or-run, not future-or-past: a card you have written is work
        /// and survives, an empty placeholder is a setting and follows the definition.
        /// </summary>
        public int RetireDefinition(ShowDefinition definition)
        {
            definition.Active = false;

            var disposable = Shows
                .Where(s => s.DefinitionId == definition.Id
                            && !s.HasRun
                            && !s.IsBooked
                            && s.Date >= CurrentDate)
                .ToList();

            foreach (var show in disposable) Shows.Remove(show);
            return disposable.Count;
        }

        /// <summary>
        /// Applies an edited definition to the calendar: untouched instances from today
        /// forward are discarded and regenerated, so moving Raw to Tuesdays actually moves
        /// it. Booked and run shows are left where they are.
        /// </summary>
        public void ResyncDefinition(ShowDefinition definition)
        {
            var stale = Shows
                .Where(s => s.DefinitionId == definition.Id
                            && !s.HasRun
                            && !s.IsBooked
                            && s.Date >= CurrentDate)
                .ToList();

            foreach (var show in stale) Shows.Remove(show);

            if (definition.Active) MaterialiseSchedule();
        }

        public ScheduledShow Schedule(string name, DateOnly date, ShowType type, string venue = "")
        {
            var show = new ScheduledShow
            {
                Name           = name,
                Date           = date,
                Type           = type,
                Venue          = venue,
                RuntimeMinutes = Promotion.DefaultRuntimeFor(type),
                Attendance     = Promotion.TypicalAttendanceFor(type)
            };

            Shows.Add(show);
            return show;
        }

        public void Cancel(ScheduledShow show)
        {
            if (!show.HasRun) Shows.Remove(show);
        }
    }
}
