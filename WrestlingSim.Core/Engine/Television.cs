using WrestlingSim.Enums;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// The television deal: who watched, and what that is worth when the term runs out.
    /// docs/wrestling-reference/08-television-and-media.md.
    ///
    /// Viewership has the same two halves as the box office, for the same reason:
    ///
    ///   • **Tune-in** is read off the card as advertised, before the bell — a viewer chooses to
    ///     watch on what was promised, exactly as a ticket buyer does.
    ///   • **Retention** is the show's quality. A bad episode loses its audience across the
    ///     night (doc 08 §4.2's quarter-hours, reduced to one figure), so at a television tier
    ///     quality reaches the money directly and not only through reputation.
    ///
    /// The fee is fixed for the term. What the episodes drew decides the renewal, with the
    /// final year weighted double, because doc 08 §3 says that is the year that counts.
    /// </summary>
    public static class Television
    {
        /// <summary>
        /// The average audience of a typical card at an opening reputation, by tier. Doc 29 §8:
        /// SmackDown 2.0–2.5M and Raw 1.3–1.8M at Tier 0, AEW Dynamite 600–900k at Tier 1, NXT
        /// 550–750k. Established has no reference row; a quarter of a million is a national
        /// cable show with a following.
        /// </summary>
        public static int BaselineViewers(PromotionTier tier) => tier switch
        {
            PromotionTier.Global      => 2_000_000,
            PromotionTier.National    => 800_000,
            PromotionTier.Established => 250_000,
            _                         => 0
        };

        /// <summary>Doc 08 §2.1: three to five years. The bigger the deal, the longer it runs.</summary>
        public static int TermYears(PromotionTier tier) => tier switch
        {
            PromotionTier.Global   => 5,
            PromotionTier.National => 4,
            _                      => 3
        };

        /// <summary>
        /// How much of the audience a typical episode keeps. Tune-in is scaled up by it so
        /// that a typical card's *average* audience is <see cref="BaselineViewers"/>.
        /// </summary>
        public const double TypicalRetention = 0.92;

        /// <summary>
        /// Less elastic than the gate: a television audience is a habit, and a weak card
        /// loses some of it rather than most of it.
        /// </summary>
        public const double TuneInElasticity = 0.8;

        /// <summary>
        /// The market's own drift. Doc 08 §1.1: rights fees rose for a decade while ratings
        /// fell, so a promotion that holds its audience renews above what it had.
        /// </summary>
        public const double RightsInflation = 1.10;

        public const double RenewalFloor = 0.5;
        public const double RenewalCeiling = 2.0;

        /// <summary>How many choose to watch, on the card as advertised.</summary>
        public static int TuneIn(PromotionTier tier, double cardPull, double reputation)
        {
            if (cardPull <= 0) return 0;
            double viewers = BaselineViewers(tier) / TypicalRetention
                           * Math.Pow(cardPull / BoxOffice.TypicalCardPullFor(tier), TuneInElasticity)
                           * BoxOffice.ReputationFactor(reputation);
            return (int)Math.Round(viewers);
        }

        /// <summary>
        /// The share of the tune-in still watching on average, from the show's rating:
        /// 0.92 at 50, 1.04 at 80 — a great show picks people up — 0.80 at 20.
        /// </summary>
        public static double Retention(double showRating) =>
            Math.Clamp(TypicalRetention + 0.004 * (showRating - 50), 0.75, 1.05);

        /// <summary>The term's first deal, at the tier's going rate.</summary>
        public static TvDeal Open(PromotionTier tier, DateOnly on) => new()
        {
            FeePerEpisode = BoxOffice.For(tier).RightsPerEpisode,
            Start         = on,
            End           = on.AddYears(TermYears(tier))
        };

        /// <summary>
        /// The term's audience against the tier's baseline, final year weighted double. 1.0 is
        /// a typical card every week. Zero with no episodes: the network paid for television
        /// nobody produced.
        /// </summary>
        public static double AudienceIndex(TvDeal deal, PromotionTier tier)
        {
            int baseline = BaselineViewers(tier);
            if (deal.Episodes.Count == 0 || baseline == 0) return 0;

            var finalYear = deal.End.AddYears(-1);
            double weighted = 0, weights = 0;
            foreach (var ep in deal.Episodes)
            {
                double w = ep.Date >= finalYear ? 2.0 : 1.0;
                weighted += w * ep.Viewers / baseline;
                weights += w;
            }
            return weighted / weights;
        }

        /// <summary>
        /// Doc 08 §2.2: the network wants predictability — a stable number it can sell
        /// against. A swinging audience costs up to a fifth of the renewal.
        ///
        /// Measured **week to week** — the mean change between consecutive episodes, against
        /// the mean audience — not as spread around the term's average. The first version used
        /// the coefficient of variation, which read a steadily *growing* audience as a swinging
        /// one: building from 0.8× to 1.4× the baseline cost about 9% and raised the warning.
        /// </summary>
        public static double Consistency(TvDeal deal)
        {
            if (deal.Episodes.Count < 2) return 1.0;
            var ordered = deal.Episodes.OrderBy(e => e.Date).ToList();
            double mean = ordered.Average(e => (double)e.Viewers);
            if (mean <= 0) return 1.0;
            double swing = ordered.Zip(ordered.Skip(1), (a, b) => Math.Abs(b.Viewers - a.Viewers)).Average();
            return Math.Clamp(1.0 - 0.5 * swing / mean, 0.8, 1.0);
        }

        /// <summary>What the next term's fee would be, as a multiple of this one's, if it ended today.</summary>
        public static double RenewalMultiple(TvDeal deal, PromotionTier tier) =>
            Math.Clamp(
                RightsInflation * Math.Pow(AudienceIndex(deal, tier), 1.2) * Consistency(deal),
                RenewalFloor, RenewalCeiling);

        /// <summary>
        /// The next term's fee, to the nearest thousand. Divided rather than
        /// <c>Math.Round(x, -3)</c>: decimal rounding takes no negative digits, and the first
        /// version of this threw at the first renewal of every career.
        /// </summary>
        public static decimal RenewedFee(decimal fee, double multiple) =>
            Math.Round(fee * (decimal)multiple / 1_000m) * 1_000m;

        /// <summary>
        /// Closes the term and opens the next at the renewed fee. The network does not walk
        /// away in this version, however bad the term — the floor is half the fee.
        /// </summary>
        public static TvTerm Renew(TvDeal deal, PromotionTier tier)
        {
            double multiple = RenewalMultiple(deal, tier);
            var term = new TvTerm
            {
                Start          = deal.Start,
                End            = deal.End,
                FeePerEpisode  = deal.FeePerEpisode,
                Episodes       = deal.Episodes.Count,
                AverageViewers = deal.Episodes.Count == 0 ? 0 : (int)deal.Episodes.Average(e => e.Viewers),
                RenewedAt      = multiple
            };

            deal.History.Add(term);
            deal.FeePerEpisode = RenewedFee(deal.FeePerEpisode, multiple);
            deal.Start = deal.End;
            deal.End = deal.End.AddYears(TermYears(tier));
            deal.Episodes.Clear();
            return term;
        }
    }
}
