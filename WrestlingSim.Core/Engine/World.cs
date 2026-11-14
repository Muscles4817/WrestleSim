using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What is *physical* about an organisation run for a given reason — what it costs to run,
    /// what its product is like, how much it was given to open with. Not how it decides: that
    /// is its <see cref="Objective"/> and <see cref="Disposition"/>, and the planner.
    ///
    /// This used to carry a bid premium and a "buys stars" switch as well. Both were behaviour
    /// written as constants, and both are gone: what a promotion will pay for somebody, and
    /// whom it wants, now come out of what it is trying to achieve.
    /// </summary>
    public sealed record MotiveTraits(
        // Multiplies the tier's payroll budget when its opening roster is signed. Opening only.
        double OpeningBudget,
        // Multiplies the tier's daily running cost.
        double Overhead,
        // Added to an item's expected rating: a coherent, well-worked product rates higher.
        double RatingBias,
        string Reading);

    /// <summary>
    /// The world the promotions share: the default ecosystem, who opens with whom, and what
    /// competition does to wages and audiences. docs/wrestling-reference/01, 11 §8, 26 §3.2.
    /// </summary>
    public static partial class World
    {
        public static MotiveTraits Profile(FounderMotive m) => m switch
        {
            FounderMotive.Business  => new(0.85, 0.85, -3, "Run for a return. Counts the money first."),
            FounderMotive.Vanity    => new(1.00, 1.00, -4, "Run so that its owner is on top."),
            FounderMotive.Ideology  => new(0.80, 1.00,  6, "Run because wrestling should be good. Counts the shows first."),
            FounderMotive.Legacy    => new(0.90, 1.00,  1, "Run so that it carries on as it is."),
            FounderMotive.MarkMoney => new(1.50, 1.20,  0, "A wealthy fan's money. Wants names, and sees the future brighter than it is."),
            FounderMotive.Platform  => new(1.00, 1.10, -1, "A media company's content. Wants an audience for its network."),
            _                       => new(1.00, 1.00,  0, "")
        };

        // ── What each kind of owner wants, and how it sees ───────────────────
        // The only place a founder motive is turned into behaviour, and it is data. The planner
        // is handed the result and does not know what kind of owner it came from.
        // docs/rival-ai-plan.md §5.2. Starting values: tuned by looking at what worlds do with
        // them, never to make a particular promotion meet a particular end.

        // Favour is every owner's, not only the vanity owner's: nearly every promoter has people
        // they protect. It was 0 for every motive but Vanity, which baked favouritism into one
        // kind of founder. The owner asked for it to be general (14 Nov 2026); the defaults
        // differ by how much each kind of owner lets it steer the card.
        public static Objective ObjectiveFor(FounderMotive m) => m switch
        {
            //                                 wealth franchise stars quality reputation favour stability
            FounderMotive.Business  => new(1.0, 0.6, 0.1, 0.1, 0.2, 0.1, 0.1),
            FounderMotive.Platform  => new(0.5, 1.0, 0.4, 0.2, 0.3, 0.2, 0.3),
            FounderMotive.MarkMoney => new(0.1, 0.2, 1.0, 0.3, 0.5, 0.5, 0.0),
            FounderMotive.Ideology  => new(0.3, 0.1, 0.1, 1.0, 0.6, 0.2, 0.2),
            FounderMotive.Legacy    => new(0.5, 0.3, 0.2, 0.3, 0.4, 0.4, 1.0),
            FounderMotive.Vanity    => new(0.3, 0.2, 0.4, 0.1, 0.3, 1.0, 0.2),
            _                       => new(1.0, 0.5, 0.3, 0.3, 0.3, 0.2, 0.2)
        };

        /// <summary>
        /// Who an owner of this kind takes to: the biggest name for a fan or a vanity owner, the
        /// best worker for one who runs it for the wrestling, the most charismatic for a media
        /// company, the steadiest veteran for a family business, the best value for one run for a
        /// return. Data, like the tables above.
        /// </summary>
        public static Func<Wrestler, double> FavouriteTaste(FounderMotive m) => m switch
        {
            FounderMotive.Ideology => w => w.RingSkills.GetOverallSkill(),
            FounderMotive.Platform => w => w.Charisma,
            FounderMotive.Legacy   => w => w.Mental.Psychology + (w.DebutedIn is { } d ? -d : 0),
            FounderMotive.Business => w => w.EffectiveOverness / (double)Math.Max(1m, TalentMarket.PerDateAsk(w)),
            _                      => w => w.EffectiveOverness
        };

        /// <summary>
        /// Gives a promotion that cares about favourites somebody to favour, when none of the
        /// ones it has is still on its books. Its own choice, by its own taste; a favourite the
        /// player set by hand is kept as long as they are there.
        /// </summary>
        public static void EnsureFavourites(Career career, RivalPromotion rival)
        {
            if (ObjectiveOf(rival).Favour <= 0 || rival.Contracts.Count == 0) return;
            if (rival.Favourites.Any(id => rival.Contracts.Any(c => c.WrestlerId == id))) return;

            var taste = FavouriteTaste(rival.Motive);
            if (rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)).OfType<Wrestler>()
                    .OrderByDescending(taste).ThenBy(w => w.Id, StringComparer.Ordinal)
                    .FirstOrDefault() is { } pick)
                rival.Favourites.Add(pick.Id);
        }

        public static Disposition DispositionFor(FounderMotive m) => m switch
        {
            //                                 horizon optimism runway-floor risk-aversion
            FounderMotive.Business  => new(365, 0.95, 365, 4),
            FounderMotive.Platform  => new(365, 1.00, 270, 3),
            FounderMotive.MarkMoney => new(120, 1.25,  45, 1),
            FounderMotive.Ideology  => new(270, 1.00, 180, 3),
            FounderMotive.Legacy    => new(365, 1.00, 365, 4),
            FounderMotive.Vanity    => new(180, 1.10, 120, 2),
            _                       => new(365, 1.00, 270, 3)
        };

        public static Objective ObjectiveOf(RivalPromotion rival) => rival.Objective ?? ObjectiveFor(rival.Motive);
        public static Disposition DispositionOf(RivalPromotion rival) => rival.Disposition ?? DispositionFor(rival.Motive);

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
                rival.Titles.SeedDefaults(name, on);
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
                    var source = new TierOpeningRoster(Profile(rival.Motive).OpeningBudget);
                    rival.Contracts.AddRange(source.Open(rival.Promotion, pool, on));

                    // Every owner has somebody they take to, by their own taste.
                    EnsureFavourites(career, rival);
                    foreach (var c in rival.Contracts.Where(c => c.IsGuaranteed)) taken.Add(c.WrestlerId);
                }
            }
        }

        /// <summary>A stable id from the name, so a re-opened world keeps its rivals' identities.</summary>
        public static string RivalId(string name) =>
            "rival-" + new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
