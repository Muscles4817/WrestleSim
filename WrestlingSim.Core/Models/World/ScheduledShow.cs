using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A show sitting on the calendar. Before its date it is a plan you can book a card
    /// into; on or after its date it can be run, after which it holds its result and
    /// becomes part of the promotion's history.
    /// </summary>
    public class ScheduledShow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "Untitled Show";
        public DateOnly Date { get; set; }
        public ShowType Type { get; set; } = ShowType.Television;
        public string Venue { get; set; } = "";

        /// <summary>Runtime budget in minutes. Seeded from the promotion's tier.</summary>
        public int RuntimeMinutes { get; set; } = 120;

        /// <summary>Expected attendance, seeded from tier and show type.</summary>
        public int Attendance { get; set; }

        /// <summary>The booked card, in running order. Empty until the player books it.</summary>
        public List<ICardItem> Card { get; set; } = new();

        // ── Result ───────────────────────────────────────────────────────────

        /// <summary>Set once the show has been run. Null means it is still upcoming.</summary>
        public ShowResult? Result { get; set; }

        public bool HasRun => Result != null;

        public bool IsBooked => Card.Count > 0;

        // ── Derived ──────────────────────────────────────────────────────────

        public int BookedMinutes => Card.Sum(i => i.DurationMinutes);

        public int RemainingMinutes => RuntimeMinutes - BookedMinutes;

        public bool IsOverrunning => BookedMinutes > RuntimeMinutes;

        /// <summary>Converts to the engine's Show shape so ShowSimulator can run it.</summary>
        public Show ToShow() => new()
        {
            Name                 = Name,
            Date                 = Date.ToDateTime(TimeOnly.MinValue),
            Location             = string.IsNullOrWhiteSpace(Venue) ? "Unknown Arena" : Venue,
            AudienceSize         = Attendance,
            Card                 = Card,
            TotalDurationMinutes = RuntimeMinutes
        };

        public string TypeLabel => Type switch
        {
            ShowType.Television   => "Television",
            ShowType.PremiumEvent => "Premium Event",
            ShowType.HouseShow    => "House Show",
            _                     => Type.ToString()
        };
    }
}
