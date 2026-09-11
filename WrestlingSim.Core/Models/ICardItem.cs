namespace WrestlingSim.Models
{
    public enum CardItemKind
    {
        Match,
        Segment
    }

    /// <summary>
    /// Anything that can occupy a slot on a show card. Gives the show layer a uniform
    /// view of matches and segments so the card can be ordered, budgeted against the
    /// show's runtime, and scored without runtime type-switching.
    /// </summary>
    public interface ICardItem
    {
        /// <summary>Label shown on the card sheet.</summary>
        string Name { get; }

        /// <summary>Kind of item — drives the same-type-in-a-row fatigue rule.</summary>
        CardItemKind Kind { get; }

        /// <summary>Estimated runtime, spent against Show.TotalDurationMinutes.</summary>
        int DurationMinutes { get; }

        /// <summary>Everyone appearing in this item.</summary>
        IReadOnlyList<Wrestler> Wrestlers { get; }

        /// <summary>
        /// Whether this is finished enough to run.
        ///
        /// A card can hold things that are not, which is the point: a booker plans the shape
        /// of a night — five matches and four segments, in an order — and casts it afterwards.
        /// Nothing else changes. The show still refuses to run until every item on it says
        /// yes, so an unfinished card is a card being worked on rather than a card that will
        /// fall over at the bell.
        /// </summary>
        bool IsComplete { get; }
    }
}
