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

        /// <summary>
        /// The recurring definition this came from, or null for a one-off put on the
        /// calendar by hand. Lets an edited definition find and replace its own future
        /// dates without disturbing anything else.
        /// </summary>
        public string? DefinitionId { get; set; }

        public string Name { get; set; } = "Untitled Show";
        public DateOnly Date { get; set; }
        public ShowType Type { get; set; } = ShowType.Television;
        public string Venue { get; set; } = "";

        /// <summary>
        /// The brand running this date, by <see cref="Brand.Id"/>, or null for a
        /// company-wide show. Copied from the definition when the date is materialised and
        /// then owned by the instance, so moving one date to the other brand does not move
        /// the whole series.
        /// </summary>
        public string? BrandId { get; set; }

        /// <summary>Runtime budget in minutes. Seeded from the promotion's tier.</summary>
        public int RuntimeMinutes { get; set; } = 120;

        /// <summary>Expected attendance, seeded from tier and show type.</summary>
        public int Attendance { get; set; }

        /// <summary>The booked card, in running order. Empty until the player books it.</summary>
        public List<ICardItem> Card { get; set; } = new();

        /// <summary>
        /// A run of towns instead of a card, for an untelevised date. Null on every other kind
        /// of show, and on a house show the player chooses to lay out match by match — the two
        /// are alternatives rather than a mode flag, so a date is bookable either way and
        /// whichever one has content is the one that runs.
        /// </summary>
        public HouseShowLoop? Loop { get; set; }

        /// <summary>Whether this date is a run of towns rather than a card.</summary>
        public bool IsLoop => Loop is { IsBookable: true } && Card.Count == 0;

        // ── Result ───────────────────────────────────────────────────────────

        /// <summary>Set once the show has been run. Null means it is still upcoming.</summary>
        public ShowResult? Result { get; set; }

        public bool HasRun => Result != null;

        public bool IsBooked => Card.Count > 0 || IsLoop;

        /// <summary>
        /// Everything on the card is finished. A planned but uncast match keeps the show from
        /// running, which is the whole safety net under letting a card be planned first: you
        /// can leave a night half-built for as long as you like and you cannot run it.
        /// </summary>
        public bool IsRunnable => IsLoop || (Card.Count > 0 && Card.All(i => i.IsComplete));

        /// <summary>The items still waiting to be finished, for the card to say so.</summary>
        public IEnumerable<ICardItem> Unfinished => Card.Where(i => !i.IsComplete);

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
