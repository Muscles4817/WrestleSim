using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A recurring show the promotion runs — "Raw, every Monday", "the monthly premium
    /// event, last Saturday". Definitions are the promotion's standing commitments; the
    /// calendar materialises dated <see cref="ScheduledShow"/> instances from them.
    ///
    /// A promotion is a calendar with obligations attached
    /// (docs/wrestling-reference/02-promotion-anatomy.md §6). This is where the
    /// obligations are declared.
    /// </summary>
    public class ShowDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "New Show";
        public ShowType Type { get; set; } = ShowType.Television;

        public RecurrenceKind Recurrence { get; set; } = RecurrenceKind.Weekly;
        public DayOfWeek Day { get; set; } = DayOfWeek.Monday;

        /// <summary>Which occurrence of <see cref="Day"/> in the month. Monthly only.</summary>
        public WeekOrdinal Ordinal { get; set; } = WeekOrdinal.Last;

        public string Venue { get; set; } = "";

        /// <summary>
        /// The brand that owns this show, by <see cref="Brand.Id"/>, or null for a
        /// company-wide date.
        ///
        /// A brand needs its own television to be a brand at all
        /// (docs/wrestling-reference/22-brand-splits.md §1). Leaving it null is the
        /// inter-brand event of §3.4 — the supercard both rosters work, where nobody is
        /// crossing over because there is no line to cross.
        /// </summary>
        public string? BrandId { get; set; }

        /// <summary>
        /// Runtime override in minutes. Null means take the promotion's default for the
        /// show type, so a tier change moves it without the player editing anything.
        /// </summary>
        public int? RuntimeMinutes { get; set; }

        /// <summary>
        /// Retired definitions stop producing new dates but leave the ones already on the
        /// calendar alone — a show you have booked or run is history, not a setting.
        /// </summary>
        public bool Active { get; set; } = true;

        // ── Occurrence maths ─────────────────────────────────────────────────

        /// <summary>Every date this definition falls on in [from, to], inclusive.</summary>
        public IEnumerable<DateOnly> OccurrencesBetween(DateOnly from, DateOnly to)
        {
            if (to < from) yield break;

            if (Recurrence == RecurrenceKind.Weekly)
            {
                var date = from;

                // Advance to the first matching weekday on or after `from`.
                int shift = ((int)Day - (int)date.DayOfWeek + 7) % 7;
                date = date.AddDays(shift);

                while (date <= to)
                {
                    yield return date;
                    date = date.AddDays(7);
                }

                yield break;
            }

            // Monthly: walk month by month, from the month `from` falls in.
            var cursor = new DateOnly(from.Year, from.Month, 1);
            var last = new DateOnly(to.Year, to.Month, 1);

            while (cursor <= last)
            {
                var occurrence = OrdinalWeekdayIn(cursor.Year, cursor.Month);

                // A "fourth Tuesday" does not exist in every month; skip those.
                if (occurrence is { } date && date >= from && date <= to)
                    yield return date;

                cursor = cursor.AddMonths(1);
            }
        }

        /// <summary>
        /// The Nth <see cref="Day"/> of the given month, or null when the month has no
        /// such occurrence (a fifth weekday, in a month that only has four).
        /// </summary>
        public DateOnly? OrdinalWeekdayIn(int year, int month)
        {
            if (Ordinal == WeekOrdinal.Last)
            {
                var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                int back = ((int)lastDay.DayOfWeek - (int)Day + 7) % 7;
                return lastDay.AddDays(-back);
            }

            var first = new DateOnly(year, month, 1);
            int forward = ((int)Day - (int)first.DayOfWeek + 7) % 7;
            var candidate = first.AddDays(forward + 7 * (int)Ordinal);

            return candidate.Month == month ? candidate : null;
        }

        // ── Display ──────────────────────────────────────────────────────────

        public string RecurrenceLabel => Recurrence == RecurrenceKind.Weekly
            ? $"every {Day}"
            : $"{OrdinalLabel(Ordinal).ToLowerInvariant()} {Day} of the month";

        public static string OrdinalLabel(WeekOrdinal ordinal) => ordinal switch
        {
            WeekOrdinal.First  => "First",
            WeekOrdinal.Second => "Second",
            WeekOrdinal.Third  => "Third",
            WeekOrdinal.Fourth => "Fourth",
            WeekOrdinal.Last   => "Last",
            _                  => ordinal.ToString()
        };

        public ShowDefinition Clone() => (ShowDefinition)MemberwiseClone();
    }
}
