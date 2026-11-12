using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A rival's show is a real show: a card of the item types the player books, its own
    /// audience, its own match-up memory and its own belts. docs/rival-ai-plan.md §2A.2a–2A.3.
    /// </summary>
    public class RivalShowTests
    {
        private readonly ITestOutputHelper output;
        public RivalShowTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2025, 1, 6);

        private static Career World(PromotionTier tier = PromotionTier.Local)
        {
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Player Wrestling", Tier = tier },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = DataLoaders.LoadEmbeddedWrestlers()
            };
            career.OpenBooks(Day);
            career.Titles.SeedDefaults(career.Promotion.Name, Day);
            Engine.World.Open(career, Day);
            return career;
        }

        private static RivalPromotion RivalAt(Career career, PromotionTier tier) =>
            career.Rivals.Single(r => r.Promotion.Tier == tier);

        /// <summary>Holds a rival's roster still and cuts it to its most over few a division.</summary>
        private static void CutTo(Career career, RivalPromotion rival, int perDivision)
        {
            rival.Frozen = true;
            var keep = rival.Contracts
                .Select(c => career.FindWrestler(c.WrestlerId)!)
                .GroupBy(w => w.Division)
                .SelectMany(g => g.OrderByDescending(w => w.Overness).Take(perDivision))
                .Select(w => w.Id).ToHashSet();
            rival.Contracts.RemoveAll(c => !keep.Contains(c.WrestlerId));
        }

        private static List<RivalNight> Nights(Career career, RivalPromotion rival, int days)
        {
            var all = new List<RivalNight>();
            for (int d = 0; d < days; d++)
            {
                int before = rival.Nights.Count;
                var last = rival.Nights.LastOrDefault();
                career.AdvanceOneDay();
                if (rival.Nights.LastOrDefault() is { } now && !ReferenceEquals(now, last)) all.Add(now);
            }
            return all;
        }

        [Fact]
        public void ARivalsCardIsMadeOfWhatThePlayerBooks()
        {
            var career = World();
            var global = RivalAt(career, PromotionTier.Global);

            var nights = Nights(career, global, 60);
            var lines = nights.SelectMany(n => n.Card).ToList();
            output.WriteLine(string.Join("\n", nights.Take(3).Select(n => $"{n.Date} {n.Type}: " + string.Join(" | ", n.Card))));

            Assert.Contains(lines, l => l.Contains("Promo") || l.Contains("Confrontation"));
            Assert.Contains(lines, l => l.EndsWith("— tag"));
            Assert.Contains(lines, l => l.Contains("— for the"));
            Assert.Contains(nights, n => n.Type == ShowType.PremiumEvent);
            Assert.All(nights, n => Assert.True(n.Filled >= AudienceModel.FullCard, "A full roster fills its slot."));

            // The big night is about monthly, not every fourth show.
            Assert.InRange(nights.Count(n => n.Type == ShowType.PremiumEvent), 2, 3);
        }

        [Fact]
        public void ARivalCrownsChampions_AndTheBeltsHoldTheirValue()
        {
            var career = World();
            var global = RivalAt(career, PromotionTier.Global);
            Assert.All(global.Titles.Active, t => Assert.True(t.IsVacant));

            Nights(career, global, 365);

            output.WriteLine(string.Join(" / ", global.Titles.Active.Select(t => $"{t.Name}: {t.Champion?.RingName ?? "vacant"} {t.Prestige:F0}, {t.ReignCount} reigns")));
            Assert.All(global.Titles.Active, t => Assert.False(t.IsVacant));
            Assert.All(global.Titles.Active, t => Assert.True(t.Prestige > 40, $"{t.Name} at {t.Prestige:F0}"));
            // Not a hot potato: a year is a handful of reigns, not dozens.
            Assert.All(global.Titles.Active, t => Assert.InRange(t.ReignCount, 1, 6));
        }

        /// <summary>
        /// The owner's test case, on a rival: six wrestlers cannot carry a national promotion.
        /// The same promotion with its full roster, in the same world over the same weeks, can.
        /// </summary>
        [Fact]
        public void ARivalCannotRunOnSixEither()
        {
            var thinWorld = World();
            var thin = RivalAt(thinWorld, PromotionTier.National);
            CutTo(thinWorld, thin, perDivision: 3);

            var fullWorld = World();
            var full = RivalAt(fullWorld, PromotionTier.National);
            full.Frozen = true;

            var six = Nights(thinWorld, thin, 84);
            var deep = Nights(fullWorld, full, 84);

            double Fill(RivalNight n) => n.Attendance / (double)n.Capacity;
            double early = six.Take(3).Average(Fill), late = six.TakeLast(3).Average(Fill);
            double deepEarly = deep.Take(3).Average(Fill), deepLate = deep.TakeLast(3).Average(Fill);
            output.WriteLine($"six:  house {early:P0} → {late:P0}, rating {six.Take(3).Average(n => n.Rating):F0} → {six.TakeLast(3).Average(n => n.Rating):F0}");
            output.WriteLine($"full: house {deepEarly:P0} → {deepLate:P0}, rating {deep.Take(3).Average(n => n.Rating):F0} → {deep.TakeLast(3).Average(n => n.Rating):F0}");

            Assert.True(late < 0.6 * early, "Six wrestlers should lose most of the house in twelve weeks.");
            Assert.True(deepLate / deepEarly > 1.5 * (late / early), "A full roster should hold far more of it.");
        }

        /// <summary>
        /// Doc 29 §12–13: a division needs six to ten performers and a champion needs credible
        /// defences. A champion with one challenger has the same match every month.
        /// </summary>
        [Fact]
        public void AChampionWithNoDivisionRunsOutOfChallengers()
        {
            var thinWorld = World();
            var thin = RivalAt(thinWorld, PromotionTier.National);
            CutTo(thinWorld, thin, perDivision: 2);

            var deepWorld = World();
            var deep = RivalAt(deepWorld, PromotionTier.National);
            deep.Frozen = true;

            Nights(thinWorld, thin, 365);
            Nights(deepWorld, deep, 365);

            double Draw(RivalPromotion r) => r.Nights.Where(n => n.Type == ShowType.PremiumEvent)
                .TakeLast(3).Average(n => n.Attendance / (double)n.Capacity);
            output.WriteLine($"big-night house after a year: two a division {Draw(thin):P0}, full roster {Draw(deep):P0}");
            output.WriteLine($"world title prestige: thin {thin.Titles.Active[0].Prestige:F0}, deep {deep.Titles.Active[0].Prestige:F0}");

            Assert.True(Draw(thin) < 0.7 * Draw(deep));
        }

        /// <summary>One audience per promotion: stale to theirs is fresh to yours.</summary>
        [Fact]
        public void StaleToTheirAudienceIsFreshToYours()
        {
            var career = World();
            var global = RivalAt(career, PromotionTier.Global);
            Nights(career, global, 120);

            var theirs = career.FindWrestler(global.Contracts[0].WrestlerId)!;

            Assert.NotNull(global.Audience.ActOf(theirs));
            Assert.Null(career.Audience.ActOf(theirs));
            Assert.Equal(1.0, career.Response(theirs, ShowType.Television));
        }

        [Fact]
        public void ARivalsBeltsAudienceAndMatchUpsSurviveASave()
        {
            var career = World(PromotionTier.Established);
            for (int i = 0; i < 60; i++) career.AdvanceOneDay();

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());

            foreach (var (was, now) in career.Rivals.Zip(loaded.Rivals))
            {
                Assert.Equal(was.Titles.Active.Select(t => t.Champion?.Id), now.Titles.Active.Select(t => t.Champion?.Id));
                Assert.Equal(was.Titles.Active.Select(t => Math.Round(t.Prestige, 1)), now.Titles.Active.Select(t => Math.Round(t.Prestige, 1)));
                Assert.Equal(was.Audience.Acts.Count, now.Audience.Acts.Count);
                Assert.Equal(was.Audience.ShowSameness, now.Audience.ShowSameness, 2);
                Assert.Equal(was.Feuds.AllIncludingDormant.Count, now.Feuds.AllIncludingDormant.Count);
                Assert.Equal(was.Nights.Last().Card, now.Nights.Last().Card);
            }
        }

        [Fact]
        public void ASaveFromBeforeRivalsHadBelts_SeedsThem()
        {
            var career = World(PromotionTier.Established);
            var dto = SaveSerializer.ToDto(career);
            foreach (var r in dto.Rivals!) { r.Titles = null; r.Audience = null; r.Feuds = null; }

            var loaded = SaveSerializer.FromJson(System.Text.Json.JsonSerializer.Serialize(dto), DataLoaders.LoadEmbeddedWrestlers());

            Assert.All(loaded.Rivals, r => Assert.Equal(3, r.Titles.Active.Count));
        }
    }
}
