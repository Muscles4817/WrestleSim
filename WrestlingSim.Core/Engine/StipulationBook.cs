using WrestlingSim.Enums;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// When the promotion last ran each gimmick match.
    ///
    /// A promotion-level counter, which is the level doc 20 §9 asks for: "each stipulation
    /// type has a cooldown, and using a cage match monthly should devalue all cage matches."
    /// Not per feud and not per wrestler — the audience's sense that a cage is special is
    /// a fact about the show, not about who is in it, and two different feuds running cages
    /// a fortnight apart devalue each other exactly as much as one feud doing it twice.
    ///
    /// A sibling of <see cref="FeudBook"/> and <see cref="TitleRegistry"/>, and deliberately
    /// the smallest one: a date per rung is the whole model.
    /// </summary>
    public class StipulationBook
    {
        private readonly Dictionary<Stipulation, DateOnly> _lastUsed = new();

        /// <summary>Every rung the promotion has run, and when. For the save file.</summary>
        public IReadOnlyDictionary<Stipulation, DateOnly> LastUsed => _lastUsed;

        /// <summary>Records that the promotion ran one. Later dates win.</summary>
        public void Record(Stipulation stipulation, DateOnly date)
        {
            if (stipulation == Stipulation.None) return;
            if (!_lastUsed.TryGetValue(stipulation, out var seen) || date > seen)
                _lastUsed[stipulation] = date;
        }

        /// <summary>Restores a saved entry without the later-wins rule. Load path only.</summary>
        public void Restore(Stipulation stipulation, DateOnly date)
        {
            if (stipulation != Stipulation.None) _lastUsed[stipulation] = date;
        }

        /// <summary>
        /// How long since the promotion last ran this one, or null if it never has.
        ///
        /// Null rather than a large number, because "never" and "a long time ago" are the
        /// same thing to <see cref="StipulationRules.Scarcity"/> and pretending otherwise
        /// would mean inventing a date for a show that did not happen. A date in the future
        /// — a card run out of calendar order — reads as zero days rather than negative.
        /// </summary>
        public int? DaysSince(Stipulation stipulation, DateOnly today) =>
            _lastUsed.TryGetValue(stipulation, out var seen)
                ? Math.Max(0, today.DayNumber - seen.DayNumber)
                : null;

        /// <summary>What this rung is worth tonight, for the booker to see before booking it.</summary>
        public double FreshnessOf(Stipulation stipulation, DateOnly today) =>
            StipulationRules.Scarcity(DaysSince(stipulation, today));
    }
}
