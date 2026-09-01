namespace WrestlingSim.Models.World
{
    /// <summary>
    /// One entry in a title's lineage: who held it, from when, for how long, and how
    /// often they defended it.
    ///
    /// Lineage is a genuine asset — docs/wrestling-reference/21-championships.md §7. "The
    /// 42nd champion" carries weight precisely because the 41 before them are recorded,
    /// so a reign is never deleted, only closed.
    /// </summary>
    public class TitleReign
    {
        /// <summary>
        /// The live champion. Held by reference in memory and written to a save by
        /// <see cref="Wrestler.Id"/> — the object graph shares wrestler instances and
        /// serialising one by value here would break identity on load.
        /// </summary>
        public required Wrestler Champion { get; init; }

        /// <summary>Which champion this is, counting from one. Never renumbered.</summary>
        public int ReignNumber { get; init; }

        public DateOnly Won { get; init; }

        /// <summary>Null while the reign is running.</summary>
        public DateOnly? Lost { get; set; }

        /// <summary>The show the title was won on, for the lineage display.</summary>
        public string WonAt { get; init; } = "";

        /// <summary>The show it was lost on, or how it ended if it was not lost in a match.</summary>
        public string LostAt { get; set; } = "";

        /// <summary>
        /// Title matches survived. A retention by count-out or disqualification counts —
        /// the champion still walked out with the belt (doc 21 §8.1) — but it is worth
        /// less to the title than a clean one.
        /// </summary>
        public int Defences { get; set; }

        /// <summary>
        /// The last time the belt was actually put on the line. Null until the first
        /// defence, in which case the reign's start date does the same job. This is what
        /// tells the economy whether the title is being featured or quietly ignored —
        /// docs/wrestling-reference/21-championships.md §4.
        /// </summary>
        public DateOnly? LastDefended { get; set; }

        /// <summary>True when the reign ended by the belt being stripped rather than lost.</summary>
        public bool Vacated { get; set; }

        public bool IsCurrent => Lost == null;

        /// <summary>
        /// How long the reign ran, in days. A running reign is measured to
        /// <paramref name="today"/>, so the number on screen is a live one.
        /// </summary>
        public int DaysHeld(DateOnly today) =>
            Math.Max(0, (Lost ?? today).DayNumber - Won.DayNumber);

        /// <summary>Reign length in plain words, because "412 days" means more than "1.1 years".</summary>
        public string LengthLabel(DateOnly today)
        {
            int days = DaysHeld(today);
            return days == 1 ? "1 day" : $"{days} days";
        }
    }
}
