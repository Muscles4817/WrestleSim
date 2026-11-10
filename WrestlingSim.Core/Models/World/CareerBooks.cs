namespace WrestlingSim.Models.World
{
    /// <summary>
    /// The promotion's money and its name. Two stocks, deliberately separate:
    ///
    ///   • <see cref="Cash"/> is what the card sold, net of what it cost.
    ///   • <see cref="Reputation"/> is what the shows have been worth — a rolling read of
    ///     their quality, which is what the *next* card sells on.
    ///
    /// That split is C5 in docs/wrestling-reference/31-sim-mapping.md: a five-star show for
    /// an empty room banks reputation and loses money, a sold-out three-star show banks money
    /// and spends reputation, and the two only agree when the booking does both.
    ///
    /// Cash can go negative. Nothing stops a career in the red — the books say so, loudly,
    /// and what happens to a promotion that cannot pay is a later decision.
    /// </summary>
    public class CareerBooks
    {
        public decimal Cash { get; set; }

        /// <summary>What the books opened with, so the dashboard can say how far they have moved.</summary>
        public decimal OpeningCash { get; set; }

        /// <summary>0–100, on the same scale as a show's overall rating. Opens at 50.</summary>
        public double Reputation { get; set; } = OpeningReputation;

        /// <summary>
        /// The date the books were opened. A career saved before money existed opens them on
        /// the day it is loaded, and any show run before that has no takings to report.
        /// </summary>
        public DateOnly OpenedOn { get; set; }

        /// <summary>Every day's running costs charged since the books opened.</summary>
        public decimal Overheads { get; set; }

        public const double OpeningReputation = 50;

        public bool InTheRed => Cash < 0;
    }
}
