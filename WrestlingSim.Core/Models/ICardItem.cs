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
    }
}
