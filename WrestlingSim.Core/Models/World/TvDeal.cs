namespace WrestlingSim.Models.World
{
    /// <summary>
    /// The promotion's television contract. Doc 08 §2.1: a term of three to five years, an
    /// annual fee, and a renewal at the end that is the most important event in the
    /// promotion's life. The fee is fixed for the term — booking cannot move this term's
    /// money — and what the episodes drew decides the next one.
    /// </summary>
    public class TvDeal
    {
        /// <summary>What the network pays for one televised episode this term.</summary>
        public decimal FeePerEpisode { get; set; }

        public DateOnly Start { get; set; }

        /// <summary>The day the term runs out and the deal is renewed.</summary>
        public DateOnly End { get; set; }

        /// <summary>Every televised episode this term, and how many watched it.</summary>
        public List<EpisodeRating> Episodes { get; set; } = new();

        /// <summary>Every term that has ended, oldest first.</summary>
        public List<TvTerm> History { get; set; } = new();

        public int DaysLeft(DateOnly today) => Math.Max(0, End.DayNumber - today.DayNumber);

        /// <summary>
        /// Doc 08 §3: the twelve months before expiry are when a promotion makes the product
        /// look valuable. They are also the months the renewal weighs most.
        /// </summary>
        public bool InRenewalWindow(DateOnly today) => DaysLeft(today) <= 365;
    }

    public sealed class EpisodeRating
    {
        public DateOnly Date { get; init; }
        public int Viewers { get; init; }
    }

    /// <summary>A term that has ended, and what it renewed at.</summary>
    public sealed class TvTerm
    {
        public DateOnly Start { get; init; }
        public DateOnly End { get; init; }
        public decimal FeePerEpisode { get; init; }
        public int Episodes { get; init; }
        public int AverageViewers { get; init; }

        /// <summary>The fee the next term opened at, as a multiple of this one's.</summary>
        public double RenewedAt { get; init; }
    }
}
