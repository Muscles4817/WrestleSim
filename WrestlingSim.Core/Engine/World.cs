using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>How a founder motive behaves, as numbers. Doc 01 §7.</summary>
    public sealed record MotiveProfile(
        // Multiplies the tier's payroll budget.
        double Budget,
        // Multiplies the tier's daily running cost.
        double Overhead,
        // Added to an abstract show's rating: a coherent, well-worked product rates higher.
        double RatingBias,
        // What it offers over the asking price when it bids for somebody else's wrestler.
        double BidPremium,
        // Signs the most over name it can afford (true) or the most over-per-dollar (false).
        bool BuysStars,
        string Reading);

    /// <summary>
    /// The world the promotions share: the default ecosystem, who opens with whom, and what
    /// competition does to wages and audiences. docs/wrestling-reference/01, 11 §8, 26 §3.2.
    /// </summary>
    public static partial class World
    {
        public static MotiveProfile Profile(FounderMotive m) => m switch
        {
            FounderMotive.Business  => new(0.85, 0.85, -3, 1.00, false, "Cost discipline and value signings. Will not overpay; will cut favourites."),
            FounderMotive.Vanity    => new(1.00, 1.00, -4, 1.10, true,  "The owner's favourites are protected. Capped at the owner's own draw."),
            FounderMotive.Ideology  => new(0.80, 1.00,  6, 1.05, false, "A coherent, well-worked product with a devoted core. Refuses to broaden."),
            FounderMotive.Legacy    => new(0.90, 1.00,  1, 1.05, true,  "Steady and traditional. Slow to change, slow to die."),
            FounderMotive.MarkMoney => new(1.50, 1.20,  0, 1.30, true,  "A wealthy fan's money: overspends on names. Typically burns out in 18–36 months."),
            FounderMotive.Platform  => new(1.00, 1.10, -1, 1.15, true,  "A media company's content: consistent television, creative subordinate to the schedule."),
            _                       => new(1.00, 1.00,  0, 1.00, false, "")
        };

        /// <summary>
        /// The default ecosystem before custom databases: a global major, a national challenger
        /// and two indies — the shape of the industry in doc 01 §2. Fictional, because a real
        /// promotion's name belongs in a database the player chose, not in the game's defaults.
        /// The player's promotion takes the place of the rival at its own tier.
        /// </summary>
        public static IReadOnlyList<(string Name, PromotionTier Tier, FounderMotive Motive)> DefaultRivals =>
        [
            ("Vantage Pro Wrestling",         PromotionTier.Global,      FounderMotive.Platform),
            ("Ironclad Wrestling Federation", PromotionTier.National,    FounderMotive.MarkMoney),
            ("Rust Belt Pro",                 PromotionTier.SuperIndie,  FounderMotive.Ideology),
            ("Harbor City Wrestling",         PromotionTier.Independent, FounderMotive.Business),
        ];

        // ── Competition ──────────────────────────────────────────────────────

        /// <summary>
        /// Promotions at National or above that are still running, the player's included. Doc 11
        /// §8: wrestler pay is a function of the number of credible employers. Two is the
        /// mid-2020s — WWE and AEW — which is when every price in this game was taken from.
        /// </summary>
        public static int CredibleEmployers(Career career) =>
            (career.Promotion.Tier >= PromotionTier.National ? 1 : 0)
            + career.Rivals.Count(r => r.IsActive && r.Promotion.Tier >= PromotionTier.National);

        public const int BaselineEmployers = 2;

        // Both factors are 1.0 for a career with no world modelled at all — a career built by
        // hand, or a test — rather than reading an empty world as a monopoly. A world whose
        // rivals have all folded still has them on the list, and is one.

        /// <summary>
        /// What competition does to wages. A third bidder raises every price twelve percent; a
        /// monopoly lowers them as much — the Monday Night Wars against 2002–2018 (doc 11 §8).
        /// </summary>
        public static double WageFactor(Career career) =>
            career.Rivals.Count == 0 ? 1.0 : Math.Clamp(1.0 + 0.12 * (CredibleEmployers(career) - BaselineEmployers), 0.76, 1.36);

        /// <summary>
        /// What competition does to the audience. Doc 26 §3.2: every boom coincided with a war and
        /// every stagnation with a monopoly. Killing your rival is a victory that quietly costs you.
        /// </summary>
        public static double Interest(Career career) =>
            career.Rivals.Count == 0 ? 1.0 : Math.Clamp(1.0 + 0.08 * (CredibleEmployers(career) - BaselineEmployers), 0.84, 1.24);

        // ── The opening ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds the default rivals and signs everybody's opening roster, the player's included,
        /// biggest promotion first: each signs within its budget from whoever the promotions above
        /// it left. Guarantees are exclusive and leave the pool; regulars are not and do not.
        /// </summary>
        public static void Open(Career career, DateOnly on)
        {
            foreach (var (name, tier, motive) in DefaultRivals.Where(r => r.Tier != career.Promotion.Tier))
            {
                var promotion = new Promotion { Name = name, Tier = tier };
                var economics = BoxOffice.For(tier);
                var rival = new RivalPromotion
                {
                    Id        = RivalId(name),
                    Promotion = promotion,
                    Motive    = motive,
                    Books     = new CareerBooks
                    {
                        Cash = economics.OpeningCash, OpeningCash = economics.OpeningCash,
                        Reputation = CareerBooks.OpeningReputation, OpenedOn = on
                    },
                    Television = promotion.HasTelevision ? Television.Open(tier, on) : null,
                    // Staggered off the player's first week so they do not all run on day one.
                    NextShow  = on.AddDays(1 + (int)tier % promotion.TypicalShowIntervalDays)
                };
                career.Rivals.Add(rival);
            }

            var taken = new HashSet<string>(
                career.Contracts.Where(c => c.IsGuaranteed).Select(c => c.WrestlerId)
                    .Concat(career.Rivals.SelectMany(r => r.Contracts).Where(c => c.IsGuaranteed).Select(c => c.WrestlerId)));

            var order = career.Rivals.Select(r => (Tier: r.Promotion.Tier, Rival: (RivalPromotion?)r))
                .Append((Tier: career.Promotion.Tier, Rival: (RivalPromotion?)null))
                .OrderByDescending(x => x.Tier)
                .ToList();

            foreach (var (_, rival) in order)
            {
                var pool = career.Roster.Where(w => !taken.Contains(w.Id)).ToList();
                if (rival is null)
                {
                    if (career.Contracts.Count == 0) career.OpenRoster(new TierOpeningRoster(), pool);
                    foreach (var c in career.Contracts.Where(c => c.IsGuaranteed)) taken.Add(c.WrestlerId);
                }
                else
                {
                    var source = new TierOpeningRoster(Profile(rival.Motive).Budget);
                    rival.Contracts.AddRange(source.Open(rival.Promotion, pool, on));
                    foreach (var c in rival.Contracts.Where(c => c.IsGuaranteed)) taken.Add(c.WrestlerId);
                }
            }
        }

        /// <summary>A stable id from the name, so a re-opened world keeps its rivals' identities.</summary>
        public static string RivalId(string name) =>
            "rival-" + new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
