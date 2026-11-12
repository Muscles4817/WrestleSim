namespace WrestlingSim.Models.World
{
    /// <summary>
    /// How a wrestler is on the promotion's roster. Plain data, keyed by wrestler id, so that a
    /// custom database can declare a promotion's roster and its terms directly — see
    /// <see cref="Engine.IOpeningRoster"/>.
    /// </summary>
    public enum ContractKind
    {
        /// <summary>
        /// Doc 11 §2.4: no contract at all. Somebody the promotion books regularly and pays by
        /// the night, at whatever their name is worth that night. The indie arrangement.
        /// </summary>
        Regular,

        /// <summary>
        /// Doc 11 §2.1: an annual downside guarantee, paid whether or not they work, that every
        /// appearance is earned against. The rate is fixed for the term — which is the whole
        /// value of signing somebody who is getting hotter.
        /// </summary>
        Guaranteed
    }

    public class Contract
    {
        public required string WrestlerId { get; init; }
        public ContractKind Kind { get; init; } = ContractKind.Guaranteed;

        /// <summary>Paid by the clock, a day at a time. Zero for a regular.</summary>
        public decimal AnnualGuarantee { get; init; }

        public DateOnly Start { get; init; }

        /// <summary>
        /// When it runs out. Moves later while the wrestler is injured — doc 11 §2.1's tolling
        /// clause: nobody runs out the clock on the sidelines. Null for a regular, who has no term.
        /// </summary>
        public DateOnly? End { get; set; }

        /// <summary>Days the term has been extended for injury.</summary>
        public int DaysTolled { get; set; }

        public decimal DailyCost => AnnualGuarantee / 365m;

        /// <summary>The term it was signed for, in whole years, not counting injury extensions.</summary>
        public int TermYears => End is { } end
            ? Math.Max(1, (int)Math.Round((end.DayNumber - Start.DayNumber - DaysTolled) / 365.25))
            : 0;

        public bool IsGuaranteed => Kind == ContractKind.Guaranteed;

        public int? DaysLeft(DateOnly today) => End is { } end ? Math.Max(0, end.DayNumber - today.DayNumber) : null;
    }

    /// <summary>Something that happened to the roster, for the dashboard to report.</summary>
    public sealed class ContractEvent
    {
        public required DateOnly Date { get; init; }
        public required string WrestlerId { get; init; }
        public required string What { get; init; }
    }
}
