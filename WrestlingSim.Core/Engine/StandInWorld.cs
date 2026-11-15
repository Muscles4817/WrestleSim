using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Worlds built to be timed, not played: the ninety real wrestlers copied under numbered
    /// names with their popularity scaled down, and promotions handed rosters by rank. Used by
    /// the scaling probe in the tests and by the browser benchmark, so the two measure the same
    /// world. Nothing about how such a world behaves should be read off one.
    /// </summary>
    public static class StandInWorld
    {
        private static readonly DateOnly Day = new(2025, 1, 6);

        /// <summary>The three sizes the plan offers, as stand-ins (docs/living-world-plan.md §0A).</summary>
        public static (int Wrestlers, (PromotionTier Tier, int Count, int Roster)[] Promotions) Size(string size) => size switch
        {
            "small"  => (500,  [(PromotionTier.Global, 1, 60), (PromotionTier.National, 2, 40), (PromotionTier.Established, 2, 25), (PromotionTier.SuperIndie, 4, 22), (PromotionTier.Independent, 6, 16)]),
            "medium" => (1200, [(PromotionTier.Global, 2, 120), (PromotionTier.National, 4, 70), (PromotionTier.Established, 6, 40), (PromotionTier.SuperIndie, 10, 25), (PromotionTier.Independent, 18, 16)]),
            _        => (3000, [(PromotionTier.Global, 2, 200), (PromotionTier.National, 4, 100), (PromotionTier.Established, 6, 50), (PromotionTier.SuperIndie, 20, 25), (PromotionTier.Independent, 60, 16)]),
        };

        public static List<Wrestler> People(int count)
        {
            var all = new List<Wrestler>();
            var rng = new Random(7);
            for (int k = 0; all.Count < count; k++)
                foreach (var w in DataLoaders.LoadEmbeddedWrestlers())
                {
                    if (all.Count >= count) break;
                    if (k > 0)
                    {
                        w.RealName += $" {k}";
                        w.Id = "";
                        w.Gimmick.Name += $" {k}";
                        w.Overness = Math.Clamp(w.Overness * (0.25 + 0.6 * rng.NextDouble()), 5, 100);
                    }
                    all.Add(w);
                }
            return all;
        }

        public static Career Build(int wrestlers, (PromotionTier Tier, int Count, int Roster)[]? promos)
        {
            var career = new Career
            {
                Promotion = new Promotion { Name = "Player Wrestling", Tier = PromotionTier.Local },
                StartDate = Day, CurrentDate = Day, Roster = People(wrestlers)
            };
            career.OpenBooks(Day);
            career.Titles.SeedDefaults(career.Promotion.Name, Day);
            if (promos is null) { World.Open(career, Day); return career; }

            var motives = Enum.GetValues<FounderMotive>();
            var taken = new HashSet<string>();
            int n = 0;
            var byOver = career.Roster.OrderByDescending(w => w.EffectiveOverness).ThenBy(w => w.Id, StringComparer.Ordinal).ToList();
            foreach (var (tier, count, size) in promos.OrderByDescending(p => p.Tier))
                for (int i = 0; i < count; i++, n++)
                {
                    var promotion = new Promotion { Name = $"{tier} Promotion {i + 1}", Tier = tier };
                    var e = BoxOffice.For(tier);
                    var rival = new RivalPromotion
                    {
                        Id = World.RivalId(promotion.Name), Promotion = promotion, Motive = motives[n % motives.Length],
                        Books = new CareerBooks { Cash = e.OpeningCash, OpeningCash = e.OpeningCash, Reputation = CareerBooks.OpeningReputation, OpenedOn = Day },
                        Television = promotion.HasTelevision ? Television.Open(tier, Day) : null,
                        NextShow = Day.AddDays(1 + n % promotion.TypicalShowIntervalDays)
                    };
                    rival.Titles.SeedDefaults(promotion.Name, Day);
                    if (promotion.HasTelevision)
                    {
                        int j = 0;
                        foreach (var w in byOver.Where(w => !taken.Contains(w.Id)).Take(size).ToList())
                        {
                            int years = 1 + j++ % 4;
                            rival.Contracts.Add(new Contract { WrestlerId = w.Id, Kind = ContractKind.Guaranteed, AnnualGuarantee = TalentMarket.GuaranteeAsk(w, years), Start = Day, End = Day.AddYears(years) });
                            taken.Add(w.Id);
                        }
                    }
                    else
                    {
                        var pool = byOver.Where(w => !taken.Contains(w.Id)).ToList();
                        int start = pool.Count == 0 ? 0 : (n * 37) % pool.Count;
                        for (int j = 0; j < size && j < pool.Count; j++)
                            rival.Contracts.Add(new Contract { WrestlerId = pool[(start + j * 3) % pool.Count].Id, Kind = ContractKind.Regular, Start = Day });
                    }
                    career.Rivals.Add(rival);
                }
            career.OpenRoster(new TierOpeningRoster(), career.Roster.Where(w => !taken.Contains(w.Id)).ToList());
            return career;
        }

    }
}
