using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;
using WrestlingSim.Models.World;
using SegmentItem = WrestlingSim.Models.Segment.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What a tier's money looks like. Every figure is from
    /// docs/wrestling-reference/09-revenue-and-costs.md or 29-benchmarks-and-numbers.md, and
    /// the comment beside it says which line; they are starting coefficients, not
    /// measurements of this game.
    ///
    /// Game tiers map onto the reference's tiers as Local → 5, Independent → 4,
    /// SuperIndie → 3, Established → 2, National → 1, Global → 0.
    /// </summary>
    public sealed record TierEconomics(
        // Net per head after fees. Doc 29 §7: indie $15–75 gross, arena $25–$1,000+; doc 09
        // §3.1 puts a Tier 0 weekly gate at $150–400k net, which is ~$40 a head in a full arena.
        decimal TicketPrice,
        // Doc 29 §7: $8–20 a head at an indie, $3–8 in an arena.
        decimal MerchPerHead,
        // The enhancement rate per date; card position multiplies it. Doc 29 §4.2.
        decimal AppearanceBase,
        // Building, ring crew, security for a normal night. Doc 09 §3.1: $80–250k at Tier 0.
        decimal Operations,
        // Flat rights fee per televised episode. Doc 09 §3.1: $1.5–8M at Tier 0.
        decimal RightsPerEpisode,
        // Doc 29 §7: $250k–1M+ per weekly episode at Tier 0.
        decimal ProductionPerEpisode,
        decimal OpeningCash,
        // What per-appearance pay leaves out: offices, staff, insurance, the ring truck, and
        // the guarantees of everybody not booked tonight. Charged every day by the clock,
        // show or no show — doc 09 §2's SG&A line, 15–25% of revenue at Tier 0. Set so a
        // typical card at the tier's usual cadence lands where doc 09 §4 puts that tier.
        decimal DailyOverhead);

    /// <summary>
    /// Sells the house. Two jobs, and the order between them is the design:
    ///
    ///   1. <see cref="Draw"/> reads the card **as advertised** — who is over, how hot the
    ///      feud is, how stale the pairing is, what is on the line — and turns it into a
    ///      reason to buy a ticket. Nothing in it reads a match result, because nobody buying
    ///      a ticket has seen one.
    ///   2. <see cref="Night"/> sells that pull into the building, scaled by the promotion's
    ///      reputation, and writes the P&amp;L.
    ///
    /// Quality reaches money only through reputation, after the show, so a great show pays
    /// on the *next* card. Doc 18 §7: story sells the ticket; quality sells the next ticket.
    /// </summary>
    public static class BoxOffice
    {
        // ── Calibration ──────────────────────────────────────────────────────

        /// <summary>
        /// The card pull of a card thrown together from the shipped roster: five singles
        /// matches between people picked at random, no feuds, nothing on the line. A booker
        /// who builds stories draws above it; one who books whoever is free draws at it.
        ///
        /// **Measured, not chosen**, and it moves when the roster does — the same contract as
        /// <see cref="MatchEngine.TypicalInvestment"/>. <c>ATypicalCardFillsTypically</c>
        /// measures the corpus and fails when the two part company; re-measure, do not widen.
        /// </summary>
        public const double TypicalCardPull = 0.7561;

        /// <summary>
        /// How full a building a typical card fills at an opening reputation. Doc 29 §7 puts
        /// break-even at a 50–60% house; a promotion booking whoever is free should clear it,
        /// not by much.
        /// </summary>
        public const double TypicalFill = 0.70;

        /// <summary>
        /// How hard demand answers the card. Above 1 because draw is top-heavy: a card twice
        /// as strong as typical sells out, one half as strong plays to a third of a room.
        /// </summary>
        public const double Elasticity = 1.5;

        /// <summary>The top item sells most of the house; the next two sell the rest.</summary>
        public static readonly IReadOnlyList<double> BillingWeights = [0.60, 0.25, 0.15];

        /// <summary>What the venue and the shirt cost take off merchandise. Doc 09 §3.4.</summary>
        public const decimal MerchNetMargin = 0.55m;

        public static TierEconomics For(PromotionTier tier) => tier switch
        {
            //                                price merch  base      ops       rights/ep  prod/ep      opening    overhead/day
            PromotionTier.Local       => new(12m, 12m,    40m,     600m,         0m,        0m,         5_000m,        13m),
            PromotionTier.Independent => new(18m, 12m,    60m,   2_000m,         0m,        0m,        25_000m,       230m),
            PromotionTier.SuperIndie  => new(25m, 14m,   100m,   8_000m,         0m,        0m,       150_000m,     1_900m),
            PromotionTier.Established => new(30m,  7m,   400m,  50_000m,   350_000m,  120_000m,     3_000_000m,    30_000m),
            PromotionTier.National    => new(35m,  6m,   800m, 120_000m, 1_500_000m,  350_000m,    25_000_000m,   275_000m),
            PromotionTier.Global      => new(40m,  5m, 1_200m, 200_000m, 4_000_000m,  700_000m,   250_000_000m,   630_000m),
            _                         => new(18m, 12m,    60m,   2_000m,         0m,        0m,        25_000m,       230m)
        };

        /// <summary>
        /// One night's fee by card position. Steep on purpose: a main eventer costs twenty
        /// times an enhancement worker, so a Local promotion can afford one star and not a
        /// card of them. At Global that puts a top star at ~$24k a date, or $3.6M over 150
        /// dates — inside doc 29 §4.1's $3–5M.
        /// </summary>
        public static decimal AppearanceFee(Wrestler w, PromotionTier tier) =>
            For(tier).AppearanceBase * w.CardPosition switch
            {
                CardPosition.MainEvent   => 20m,
                CardPosition.UpperCard   => 8m,
                CardPosition.Midcard     => 3m,
                CardPosition.LowerCard   => 1.5m,
                _                        => 1m
            };

        // ── 1. The draw ──────────────────────────────────────────────────────

        /// <summary>What one item gives the public a reason to buy. Read before the bell.</summary>
        public static ItemDraw DrawOf(ICardItem item, FeudBook? feuds, DateOnly? on) => item switch
        {
            BookedMatch m => MatchDraw(m, feuds, on),
            SegmentItem s => TalkingDraw(s.Name, s.Participants, feuds, weight: 0.5),
            RumblePlan r  => CrowdDraw(r.Name, r.Wrestlers),
            RumbleDraw d  => TalkingDraw(d.Name, d.Wrestlers, feuds: null, weight: 0.4),
            _             => TalkingDraw(item.Name, item.Wrestlers, feuds, weight: 0.5)
        };

        /// <summary>Every item, best reason to buy first.</summary>
        public static List<ItemDraw> Draw(IEnumerable<ICardItem> card, FeudBook? feuds, DateOnly? on) =>
            card.Where(i => i.Wrestlers.Count > 0)
                .Select(i => DrawOf(i, feuds, on))
                .OrderByDescending(d => d.Pull)
                .ToList();

        /// <summary>
        /// The card's pull, from its top three items. A card is sold on its main event and
        /// the two matches under it; the fifth match on the card sells nobody a ticket.
        /// </summary>
        public static double CardPull(IReadOnlyList<ItemDraw> draws)
        {
            double pull = 0;
            for (int i = 0; i < Math.Min(draws.Count, BillingWeights.Count); i++)
                pull += draws[i].Pull * BillingWeights[i];
            return pull;
        }

        private static ItemDraw MatchDraw(BookedMatch m, FeudBook? feuds, DateOnly? on)
        {
            var plan = m.Plan;

            // The poster: the most over person on each side, best side first.
            var tops = plan.Sides
                .Where(s => s.Members.Count > 0)
                .Select(s => s.Members.MaxBy(w => w.EffectiveOverness)!)
                .OrderByDescending(w => w.EffectiveOverness)
                .ToList();

            // The plan's own feud when the builder attached one; otherwise whatever the book
            // holds between these people, because the poster sells the story either way.
            var feud = plan.Feud
                    ?? feuds?.Among(plan.AllParticipants.ToList()).MaxBy(f => f.Heat);
            double stakes = 1.0 + (plan.TitleAtStake?.Prestige ?? 0) / 100.0 * 0.35;
            if (plan.Stipulation != Stipulation.None) stakes *= 1.10;

            return new ItemDraw
            {
                Label      = m.Name,
                Headliners = tops.Select(w => w.RingName).ToList(),
                StarPower  = StarPower(tops),
                Build      = BuildOf(feud),
                Freshness  = feud?.Familiarity(on) ?? 1.0,
                Stakes     = stakes
            };
        }

        /// <summary>A promo or an angle: it sells a ticket at half the rate a match does.</summary>
        private static ItemDraw TalkingDraw(
            string label, IReadOnlyList<Wrestler> people, FeudBook? feuds, double weight)
        {
            var tops = people.OrderByDescending(w => w.EffectiveOverness).ToList();
            var feud = feuds?.Among(people).MaxBy(f => f.Heat);

            return new ItemDraw
            {
                Label      = label,
                Headliners = tops.Take(2).Select(w => w.RingName).ToList(),
                StarPower  = StarPower(tops) * weight,
                Build      = BuildOf(feud)
            };
        }

        /// <summary>A battle royal sells on its two biggest names and the spectacle of the rest.</summary>
        private static ItemDraw CrowdDraw(string label, IReadOnlyList<Wrestler> people)
        {
            var tops = people.OrderByDescending(w => w.EffectiveOverness).ToList();
            return new ItemDraw
            {
                Label      = label,
                Headliners = tops.Take(2).Select(w => w.RingName).ToList(),
                StarPower  = StarPower(tops),
                Stakes     = 1.10
            };
        }

        /// <summary>
        /// Doc 18 §7: "both performers are over". The best name counts most, but a star
        /// against nobody sells less than two stars — 60/40 between the top two.
        /// </summary>
        private static double StarPower(IReadOnlyList<Wrestler> bestFirst) => bestFirst.Count switch
        {
            0 => 0,
            1 => 0.6 * bestFirst[0].EffectiveOverness / 100.0,
            _ => (0.6 * bestFirst[0].EffectiveOverness + 0.4 * bestFirst[1].EffectiveOverness) / 100.0
        };

        /// <summary>
        /// The build: up to half again for a feud at Nuclear heat, discounted by how much the
        /// crowd has learned to distrust the booker's endings.
        /// </summary>
        private static double BuildOf(Models.MatchPlan.Feud? feud) =>
            feud is null
                ? 1.0
                : 1.0 + 0.5 * Math.Min(feud.Heat, Models.MatchPlan.Feud.NuclearThreshold)
                          / Models.MatchPlan.Feud.NuclearThreshold * feud.Credibility;

        // ── 2. The night ─────────────────────────────────────────────────────

        /// <summary>What reputation does to demand: ×0.75 at nothing, ×1.0 at 50, ×1.25 at 100.</summary>
        public static double ReputationFactor(double reputation) =>
            0.75 + 0.5 * Math.Clamp(reputation, 0, 100) / 100.0;

        /// <summary>How many want to come to a building of this size for a card this strong.</summary>
        public static int Demand(int capacity, double cardPull, double reputation)
        {
            if (capacity <= 0 || cardPull <= 0) return 0;
            double ratio = TypicalFill
                         * Math.Pow(cardPull / TypicalCardPull, Elasticity)
                         * ReputationFactor(reputation);
            return (int)Math.Round(capacity * ratio);
        }

        public static decimal PriceFor(TierEconomics e, ShowType type) => type switch
        {
            ShowType.PremiumEvent => e.TicketPrice * 1.6m,
            ShowType.HouseShow    => e.TicketPrice * 0.8m,
            _                     => e.TicketPrice
        };

        public static decimal OperationsFor(TierEconomics e, ShowType type) => type switch
        {
            ShowType.PremiumEvent => e.Operations * 2.5m,
            ShowType.HouseShow    => e.Operations * 0.5m,
            _                     => e.Operations
        };

        /// <summary>
        /// Rights money for the night. Only a promotion with television has any, and only a
        /// televised night or a premium event earns it — the premium event is in the deal,
        /// which is the streaming-era arrangement in doc 09 §3.2.
        /// </summary>
        public static decimal RightsFor(Promotion p, ShowType type) =>
            !p.HasTelevision ? 0m : type switch
            {
                ShowType.Television   => For(p.Tier).RightsPerEpisode,
                ShowType.PremiumEvent => For(p.Tier).RightsPerEpisode * 2m,
                _                     => 0m
            };

        public static decimal ProductionFor(Promotion p, ShowType type) =>
            !p.HasTelevision ? 0m : type switch
            {
                ShowType.Television   => For(p.Tier).ProductionPerEpisode,
                ShowType.PremiumEvent => For(p.Tier).ProductionPerEpisode * 2m,
                _                     => 0m
            };

        /// <summary>
        /// Merchandise is top-heavy — doc 09 §3.4, the #1 mover outsells the bottom forty —
        /// so the spend per head follows the most over person in the building.
        /// </summary>
        private static decimal Merch(TierEconomics e, int attendance, IEnumerable<Wrestler> people)
        {
            double top = people.Select(w => w.EffectiveOverness).DefaultIfEmpty(0).Max();
            decimal starFactor = (decimal)(0.5 + top / 100.0);
            return Math.Round(attendance * e.MerchPerHead * starFactor * MerchNetMargin);
        }

        /// <summary>
        /// The night's books, with the run of towns projected from the cast as booked. Once the
        /// run has been worked, <see cref="Worked"/> settles it on the nights actually worked.
        ///
        /// Call this **before** the card is simulated. Familiarity, heat and overness all move
        /// when a match is run, and the house was sold on what they were beforehand.
        /// </summary>
        public static ShowFinances Night(
            ScheduledShow show, Promotion promotion, double reputation, FeudBook? feuds)
        {
            var e = For(promotion.Tier);
            var draws = Draw(show.Card, feuds, show.Date);
            double pull = CardPull(draws);
            bool hasCard = draws.Count > 0;

            int capacity = hasCard ? show.Attendance : 0;
            int demand = Demand(capacity, pull, reputation);
            int attendance = Math.Min(capacity, demand);
            decimal price = PriceFor(e, show.Type);

            var onCard = show.Card.SelectMany(i => i.Wrestlers).Distinct().ToList();

            return new ShowFinances
            {
                Draws            = draws,
                CardPull         = pull,
                Capacity         = capacity,
                Demand           = demand,
                TicketPrice      = price,
                Gate             = attendance * price,
                Merchandise      = Merch(e, attendance, onCard),
                MediaRights      = hasCard ? RightsFor(promotion, show.Type) : 0m,
                Talent           = onCard.Sum(w => AppearanceFee(w, promotion.Tier)),
                Operations       = hasCard ? OperationsFor(e, show.Type) : 0m,
                Production       = hasCard ? ProductionFor(promotion, show.Type) : 0m,
                Road             = Road(show, promotion, reputation, onCard),
                ReputationBefore = reputation,
                ReputationAfter  = reputation
            };
        }

        /// <summary>
        /// A run of towns sold as house shows, one a night. The poster is the cast paired off,
        /// best against next-best; there are no feuds or belts on a house show to sell it with.
        ///
        /// Every town is the same town: markets that cool when they are run (B5) are not
        /// built, so the fifth night sells exactly what the first did.
        /// </summary>
        private static RoadTakings? Road(
            ScheduledShow show, Promotion promotion, double reputation,
            IReadOnlyCollection<Wrestler> onCard)
        {
            if (show.Loop is not { IsBookable: true } loop) return null;

            var e = For(promotion.Tier);
            var cast = loop.Cast.OrderByDescending(w => w.EffectiveOverness).ToList();

            // A house show matches its cast against each other, so the poster is the cast paired
            // off best against next-best. Pricing each name alone, as if they were wrestling
            // nobody, put Rollins, Punk and Orton in a 41% house.
            var poster = cast.Chunk(2)
                .Select(pair => new ItemDraw
                {
                    Label      = string.Join(" vs ", pair.Select(w => w.RingName)),
                    Headliners = pair.Select(w => w.RingName).ToList(),
                    StarPower  = StarPower(pair)
                })
                .ToList();

            int capacity = promotion.TypicalAttendanceFor(ShowType.HouseShow);
            int attendance = Math.Min(capacity, Demand(capacity, CardPull(poster), reputation));
            int towns = loop.Towns;

            var onTelevision = onCard.ToHashSet();
            decimal talent = cast.Sum(w => loop.NightsFor(w, onTelevision) * AppearanceFee(w, promotion.Tier));

            return new RoadTakings
            {
                Towns              = towns,
                CapacityPerNight   = capacity,
                AttendancePerNight = attendance,
                Headliners         = poster.Take(BillingWeights.Count).Select(p => p.Label).ToList(),
                Gate               = towns * attendance * PriceFor(e, ShowType.HouseShow),
                Merchandise        = towns * Merch(e, attendance, cast),
                Talent             = talent,
                Operations         = towns * OperationsFor(e, ShowType.HouseShow)
            };
        }

        /// <summary>
        /// The road as it was actually worked: the houses were sold in advance, but nobody is
        /// paid for a town they went home hurt before.
        /// </summary>
        public static RoadTakings Worked(RoadTakings projected, LoopResult worked, PromotionTier tier) => new()
        {
            Towns              = projected.Towns,
            CapacityPerNight   = projected.CapacityPerNight,
            AttendancePerNight = projected.AttendancePerNight,
            Headliners         = projected.Headliners,
            Gate               = projected.Gate,
            Merchandise        = projected.Merchandise,
            Talent             = worked.Workers.Sum(w => w.NightsWorked * AppearanceFee(w.Wrestler, tier)),
            Operations         = projected.Operations
        };

        // ── After the bell ───────────────────────────────────────────────────

        /// <summary>
        /// How far one show moves reputation toward its own rating. A premium event is
        /// remembered longest; a weekly episode is one of many.
        /// </summary>
        public static double ReputationWeight(ShowType type) => type switch
        {
            ShowType.PremiumEvent => 0.20,
            ShowType.HouseShow    => 0.05,
            _                     => 0.10
        };

        /// <summary>Reputation after a show: a step toward what the show was worth.</summary>
        public static double ReputationAfter(double before, double showRating, ShowType type) =>
            Math.Clamp(before + ReputationWeight(type) * (showRating - before), 0, 100);

        /// <summary>
        /// The one-line reading of whether the night sold and whether it delivered, measured
        /// against the promotion's own reputation rather than an absolute bar — a three-star
        /// show is a triumph for one company and a bad night for another.
        /// Null when the night was unremarkable on both counts.
        /// </summary>
        public static string? Verdict(ShowFinances f, double showRating)
        {
            if (!f.HadCard) return null;

            bool drewWell   = f.Fill >= 0.90;
            bool drewPoorly = f.Fill < 0.55;
            bool ratedWell   = showRating >= f.ReputationBefore + 8;
            bool ratedPoorly = showRating <= f.ReputationBefore - 8;

            return (drewWell, drewPoorly, ratedWell, ratedPoorly) switch
            {
                (true, _, true, _)  => "Sold and delivered. The next card inherits both.",
                (true, _, _, true)  => "The card sold the house and the show let it down. The next one pays for that.",
                (_, true, true, _)  => "A good show for a room that didn't come. Quality sells the next ticket, not this one.",
                (_, true, _, true)  => "Nobody came, and the ones who did saw why.",
                (true, _, _, _)     => "The card sold the house.",
                (_, true, _, _)     => "Below break-even. The card didn't give enough people a reason to come.",
                _                   => null
            };
        }
    }
}
