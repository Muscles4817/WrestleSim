using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>How big a world the player chose (living-world plan §0A, 13 Nov 2026).</summary>
    public enum WorldSize { Small, Medium, Full }

    /// <summary>
    /// Where its people come from. The database is the bundled one of real wrestlers, topped up
    /// with generated people to fill the world; a generated world is generated throughout. A
    /// custom database loaded from a file is the third choice in the plan and is not built.
    /// </summary>
    public enum WorldSource { Database, Generated }

    /// <summary>The world a career was opened in. The seed makes the generated part of it again exactly.</summary>
    public sealed record WorldChoice(WorldSize Size, WorldSource Source, int Seed);

    /// <summary>
    /// The shape of a world of each size: how many promotions at each level, how big their
    /// rosters open, and how many wrestlers there are in all.
    ///
    /// From the planning session (§0A): the full size is two global companies at about 180,
    /// six national at about 80, ten established at about 40, twenty super-indies and sixty
    /// independents booking from a shared pool. Small and medium are the same industry with
    /// fewer of each. Measured in the browser on 15 Nov: an ordinary week runs inside two
    /// seconds at every size.
    /// </summary>
    public static class WorldShape
    {
        public static int Population(WorldSize size) => size switch
        {
            WorldSize.Small  => 500,
            WorldSize.Medium => 1_200,
            _                => 3_000
        };

        public static IReadOnlyList<(PromotionTier Tier, int Count, int Roster)> Promotions(WorldSize size) => size switch
        {
            WorldSize.Small =>
            [
                (PromotionTier.Global, 1, 120), (PromotionTier.National, 2, 60), (PromotionTier.Established, 2, 30),
                (PromotionTier.SuperIndie, 4, 22), (PromotionTier.Independent, 6, 16)
            ],
            WorldSize.Medium =>
            [
                (PromotionTier.Global, 2, 150), (PromotionTier.National, 4, 70), (PromotionTier.Established, 6, 35),
                (PromotionTier.SuperIndie, 10, 22), (PromotionTier.Independent, 18, 16)
            ],
            _ =>
            [
                (PromotionTier.Global, 2, 180), (PromotionTier.National, 6, 80), (PromotionTier.Established, 10, 40),
                (PromotionTier.SuperIndie, 20, 22), (PromotionTier.Independent, 60, 16)
            ]
        };

        /// <summary>How big a promotion at this level opens in a world of this size. Local, which no world shape holds, as the default.</summary>
        public static int RosterFor(WorldSize size, PromotionTier tier) =>
            Promotions(size).FirstOrDefault(p => p.Tier == tier) is { Count: > 0 } p ? p.Roster : TierOpeningRoster.For(tier).Size;

        /// <summary>
        /// How it runs in the browser, measured 15 Nov 2026 (Release build, interpreted
        /// WebAssembly, eight weeks of a real world from <c>Benchmarks.RealWeek</c>): small
        /// averages 0.5 s a week and never passed 2 s; medium averages 1.6 s with busy weeks
        /// at 2–3 s; full averages 3 s with a premium-event week at 9 s. The budget is two
        /// seconds, so the bigger two say so.
        /// </summary>
        public static string Speed(WorldSize size) => size switch
        {
            WorldSize.Small  => "",
            WorldSize.Medium => " (some weeks slow)",
            _                => " (slow in the browser for now)"
        };

        public static string Describe(WorldSize size)
        {
            int promotions = Promotions(size).Sum(p => p.Count);
            return $"about {promotions} promotions, {Population(size):N0} wrestlers";
        }

        // ── Who works where ──────────────────────────────────────────────────

        /// <summary>
        /// The level every member of the world works at, best first: the television rosters
        /// exactly, then everybody else split between the two indie levels in proportion to how
        /// many they book, with a fifth below any promotion's notice.
        /// </summary>
        public static List<PromotionTier> Levels(WorldSize size)
        {
            var levels = new List<PromotionTier>();
            var shape = Promotions(size);
            foreach (var (tier, count, roster) in shape.Where(p => p.Tier >= PromotionTier.Established).OrderByDescending(p => p.Tier))
                levels.AddRange(Enumerable.Repeat(tier, count * roster));

            int rest = Math.Max(0, Population(size) - levels.Count);
            int below = rest / 5;
            var indie = shape.Where(p => p.Tier < PromotionTier.Established).OrderByDescending(p => p.Tier).ToList();
            int slots = indie.Sum(p => p.Count * p.Roster);
            int placed = 0;
            foreach (var (tier, count, roster) in indie)
            {
                int n = slots == 0 ? 0 : (int)Math.Round((rest - below) * (double)(count * roster) / slots);
                levels.AddRange(Enumerable.Repeat(tier, n));
                placed += n;
            }
            levels.AddRange(Enumerable.Repeat(PromotionTier.Local, Math.Max(0, rest - placed)));
            return levels;
        }

        // ── Names ────────────────────────────────────────────────────────────

        /// <summary>
        /// Promotion names, made up. The game's four default rivals keep their names at the top of
        /// their levels; every other promotion is a place-like word and a kind of company. Real
        /// promotions' names belong in a database the player chose, not in the game's defaults.
        /// </summary>
        public static string PromotionName(Random rng, ISet<string> taken)
        {
            for (int attempt = 0; ; attempt++)
            {
                string name = $"{Places[rng.Next(Places.Length)]} {Kinds[rng.Next(Kinds.Length)]}";
                if (attempt > 40) name += $" {attempt}";
                if (taken.Add(name)) return name;
            }
        }

        private static readonly string[] Places =
        [
            "Anvil", "Ashgrove", "Bayside", "Blackwater", "Bluestem", "Boulder Creek", "Brass City", "Briar", "Canyon", "Cedar Point",
            "Cinder", "Coldspring", "Copperline", "Crescent", "Crossroads", "Delta", "Dustbowl", "Eastgate", "Eight Mile Creek", "Ember",
            "Fairhaven", "Flatiron", "Foundry", "Frontline", "Gaslight", "Glass City", "Goldfield", "Granite", "Greenbank", "Gulf Coast",
            "Harborlight", "Hollow Pines", "Ironbridge", "Ironwood", "Junction", "Keystone", "Lakeshore", "Lantern", "Lone Oak", "Magnolia",
            "Midland", "Millrace", "Mesa", "Northstar", "Oak Ridge", "Old Mill", "Palisade", "Pinewood", "Prairie", "Quarry Hill",
            "Redrock", "Riverbend", "Rockport", "Saltmarsh", "Sawtooth", "Shoreline", "Silverline", "Southpaw", "Stonebridge", "Summit",
            "Sundown", "Tidewater", "Timberline", "Twin Rivers", "Union Yard", "Valley Forge Road", "Westbrook", "Whitecap", "Wildwood", "Windmill"
        ];

        private static readonly string[] Kinds =
        [
            "Pro Wrestling", "Wrestling", "Championship Wrestling", "Wrestling Alliance", "Pro", "Wrestling Federation",
            "Grappling Club", "Wrestling Association", "Pro Wrestling League", "Wrestling Company"
        ];
    }

    public static partial class World
    {
        /// <summary>
        /// Fills the career's roster for the world chosen: the database topped up from the bottom,
        /// or generated throughout. The player's region is the one generated people below
        /// television are spread across first, so the player's own scene is never empty.
        /// </summary>
        public static void Populate(Career career, WorldChoice choice, IReadOnlyList<Wrestler> database)
        {
            career.WorldChoice = choice;
            var levels = WorldShape.Levels(choice.Size);
            var rng = new Random(choice.Seed);

            List<Wrestler> people = choice.Source == WorldSource.Database ? [.. career.Roster] : [];

            // Fill from the bottom: the database takes the best places, and nobody generated
            // into a database world is as big a star as the database's top quarter — its main
            // eventers stay the names of the world they are in.
            double cap = 100;
            if (choice.Source == WorldSource.Database && people.Count > 0)
            {
                levels = levels.Skip(people.Count).ToList();
                var sorted = people.Select(w => w.Overness).OrderBy(o => o).ToList();
                cap = sorted[sorted.Count * 3 / 4];
            }

            var generator = new WrestlerGenerator(database, people, rng.Next(), career.StartDate.Year);
            var regions = Enum.GetValues<Region>();
            int i = 0;
            foreach (var level in levels)
            {
                var division = rng.NextDouble() < 0.55 ? Division.Mens : Division.Womens;
                // Everybody has a home. Below television it decides who can book them.
                var region = regions[(i++ + (int)(career.Promotion.Region ?? 0)) % regions.Length];
                people.Add(generator.Make(division, level, region, cap));
            }

            career.Roster = people;
        }

        /// <summary>
        /// Builds a world of the chosen size and signs everybody's opening roster, biggest
        /// promotion first, the same way the four-promotion world is built. The player's
        /// promotion takes the place of one rival at its own level.
        /// </summary>
        public static void Open(Career career, DateOnly on, WorldChoice choice)
        {
            var rng = new Random(choice.Seed ^ 0x5eed);
            var regions = Enum.GetValues<Region>();
            var motives = Enum.GetValues<FounderMotive>();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { career.Promotion.Name };
            var defaults = DefaultRivals.ToDictionary(r => r.Tier);
            int n = 0;

            foreach (var (tier, count, _) in WorldShape.Promotions(choice.Size).OrderByDescending(p => p.Tier))
                for (int i = 0; i < count; i++, n++)
                {
                    if (tier == career.Promotion.Tier && i == 0) continue;   // the player's place

                    string name;
                    FounderMotive motive;
                    if (i == (tier == career.Promotion.Tier ? 1 : 0) && defaults.TryGetValue(tier, out var d) && taken.Add(d.Name))
                        (name, motive) = (d.Name, d.Motive);
                    else
                        (name, motive) = (WorldShape.PromotionName(rng, taken), motives[rng.Next(motives.Length)]);

                    var promotion = new Promotion { Name = name, Tier = tier, Region = regions[n % regions.Length] };
                    career.Rivals.Add(NewRival(promotion, motive, on, n));
                }

            Sign(career, on, tier => WorldShape.RosterFor(choice.Size, tier));
        }

        private static RivalPromotion NewRival(Promotion promotion, FounderMotive motive, DateOnly on, int n)
        {
            var economics = BoxOffice.For(promotion.Tier);
            var rival = new RivalPromotion
            {
                Id        = RivalId(promotion.Name),
                Promotion = promotion,
                Motive    = motive,
                Books     = new CareerBooks
                {
                    Cash = economics.OpeningCash, OpeningCash = economics.OpeningCash,
                    Reputation = CareerBooks.OpeningReputation, OpenedOn = on
                },
                Television = promotion.HasTelevision ? Television.Open(promotion.Tier, on) : null,
                // Staggered off the player's first week so they do not all run on day one.
                NextShow  = on.AddDays(1 + (n + (int)promotion.Tier) % promotion.TypicalShowIntervalDays)
            };
            rival.Titles.SeedDefaults(promotion.Name, on);
            return rival;
        }

        /// <summary>
        /// The opening's talent business, done before the first day rather than on it: every
        /// promotion's first look at the market. In a big world this is the most expensive week
        /// there is — measured at 9 s in the browser at full size, against 0.6 s for an ordinary
        /// one — so a new career does it while it is being made, a promotion at a time, and says
        /// how far it has got. Each step is one promotion's plan.
        /// </summary>
        public static IEnumerable<(int Done, int Of)> PlanOpening(Career career)
        {
            var active = career.Rivals.Where(r => r.IsActive).ToList();
            for (int i = 0; i < active.Count; i++)
            {
                _ticking = true;
                _holdings = null;
                try { Plan(career, active[i]); }
                finally { _ticking = false; _holdings = null; }
                yield return (i + 1, active.Count);
            }
        }
    }
}
