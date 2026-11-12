using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What a wrestler costs. One price, whoever is buying — a star costs what a star costs, and
    /// a promotion that cannot afford one does not get one. docs/wrestling-reference/11 and 29 §4.
    ///
    /// The price follows <see cref="Wrestler.EffectiveOverness"/>, so momentum moves it: a
    /// wrestler on a hot run asks more next week (doc 11 §7.1). That is what a guarantee buys
    /// out of — the rate is fixed for the term.
    /// </summary>
    public static class TalentMarket
    {
        /// <summary>
        /// A night's work, by how over they are: about $100 at 25, $800 at 50, $4k at 70, $13k at
        /// 85, $30k at 95. Exponential because pay in wrestling is — doc 11 §2.5, the great
        /// majority earn under $25k a year and the top of the card earns millions.
        /// </summary>
        public static decimal PerDateAsk(Wrestler w, double wage = 1.0)
        {
            double o = Math.Clamp(w.EffectiveOverness, 0, 100);
            double ask = 100.0 * Math.Exp(Steepness * (o - 25)) * wage;
            return Math.Max(Floor, Math.Round((decimal)ask / 10m) * 10m);
        }

        /// <summary>ln(300) / 70: a 95 asks three hundred times a 25.</summary>
        private const double Steepness = 0.0815;
        public const decimal Floor = 50m;

        /// <summary>
        /// How many nights a guarantee is worth. A top star at $30k a date asks $3.6M a year,
        /// inside doc 29 §4.1's $3–5M; a mid-carder at $4k asks about $470k, inside $300–700k.
        /// Book somebody more often than this and the guarantee is the cheaper way to pay them.
        /// </summary>
        public const int DatesInAGuarantee = 120;

        /// <summary>
        /// Doc 10 §6.1: security is worth money to a wrestler, so a longer term buys a lower
        /// annual figure — and locks that figure in for longer, which is the booker's gamble.
        /// </summary>
        public static decimal TermFactor(int years) => years switch
        {
            <= 1 => 1.05m,
            2    => 1.00m,
            3    => 0.97m,
            4    => 0.94m,
            _    => 0.92m
        };

        public const int MinTermYears = 1;
        public const int MaxTermYears = 5;

        /// <summary>The annual guarantee they will sign for, for a term of this many years.</summary>
        public static decimal GuaranteeAsk(Wrestler w, int years, double wage = 1.0) =>
            Math.Round(PerDateAsk(w, wage) * DatesInAGuarantee * TermFactor(years) / 1_000m) * 1_000m;

        /// <summary>
        /// What this deal would cost if it were signed today, on the same term. Above the
        /// guarantee means they have got hotter since and the deal is a bargain while it lasts;
        /// below, the promotion is paying for a name that has cooled. Same term on both sides,
        /// or the term discount reads as a change in the wrestler.
        /// </summary>
        public static decimal WorthToday(Wrestler w, Models.World.Contract c, double wage = 1.0) =>
            GuaranteeAsk(w, Math.Max(MinTermYears, c.TermYears), wage);

        /// <summary>
        /// Doc 13 §3.2: a released wrestler is paid through a 90-day non-compete. Releasing
        /// somebody is not free, and it is not the whole contract either.
        /// </summary>
        public const int ReleasePayoutDays = 90;

        /// <summary>How far ahead the dashboard warns that a deal is running out.</summary>
        public const int ExpiryWarningDays = 60;
    }
}
