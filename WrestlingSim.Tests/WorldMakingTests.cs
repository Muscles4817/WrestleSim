using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Part 1 of the living-world plan: where the world comes from. Mechanisms, not fates —
    /// nothing here says who signs whom, only that the world is made by the rules it claims.
    /// </summary>
    public class WorldMakingTests
    {
        private static readonly DateOnly Day = new(2026, 1, 5);

        private static Career Made(WorldSource source, WorldSize size = WorldSize.Small,
                                   PromotionTier tier = PromotionTier.Independent, Region region = Region.Midwest, string seed = "world")
        {
            var database = DataLoaders.LoadEmbeddedWrestlers();
            var career = new Career
            {
                Promotion = new Promotion { Name = "Test Wrestling", Tier = tier, Region = region },
                StartDate = Day, CurrentDate = Day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            var choice = new WorldChoice(size, source, StableSeed.From(seed));
            World.Populate(career, choice, database);
            career.OpenBooks(Day);
            career.Titles.SeedDefaults(career.Promotion.Name, Day);
            World.Open(career, Day, choice);
            return career;
        }

        [Fact]
        public void AWorldIsTheSizeItWasChosenAt()
        {
            var career = Made(WorldSource.Database);

            Assert.Equal(WorldShape.Population(WorldSize.Small), career.Roster.Count);
            // The player's promotion takes the place of one at its own level.
            Assert.Equal(WorldShape.Promotions(WorldSize.Small).Sum(p => p.Count) - 1, career.Rivals.Count);
            foreach (var (tier, count, _) in WorldShape.Promotions(WorldSize.Small))
                Assert.Equal(tier == PromotionTier.Independent ? count - 1 : count, career.Rivals.Count(r => r.Promotion.Tier == tier));
        }

        [Fact]
        public void ADatabaseWorldKeepsEveryRealPersonAndMarksEveryoneElse()
        {
            var database = DataLoaders.LoadEmbeddedWrestlers();
            var career = Made(WorldSource.Database);

            Assert.All(database, real => Assert.Contains(career.Roster, w => w.Id == real.Id && !w.IsGenerated));
            Assert.Equal(career.Roster.Count - database.Count, career.Roster.Count(w => w.IsGenerated));
        }

        [Fact]
        public void AGeneratedWorldHoldsNobodyReal()
        {
            var career = Made(WorldSource.Generated);
            Assert.All(career.Roster, w => Assert.True(w.IsGenerated));
        }

        [Fact]
        public void NobodyGeneratedSharesARealName_OrARealFinishersName()
        {
            var database = DataLoaders.LoadEmbeddedWrestlers();
            var realNames = database.SelectMany(w => new[] { w.RealName, w.RingName }.Concat(w.Gimmick?.PreviousNames ?? []))
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            // A finisher's own name ("Riptide"), not a move's ("Spear", which is somebody's
            // finisher name and also a move anybody does).
            var moves = database.SelectMany(w => w.Signature).Select(s => s.Move?.Name)
                                .Concat(database.SelectMany(w => w.Moveset).Select(m => m.Name))
                                .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var finishers = database.SelectMany(w => w.Signature).Select(s => s.Name).Where(n => !moves.Contains(n))
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var career = Made(WorldSource.Generated, WorldSize.Medium);

            foreach (var w in career.Roster)
            {
                Assert.DoesNotContain(w.RealName, realNames);
                Assert.DoesNotContain(w.RingName, realNames);
                Assert.All(w.Signature, s => Assert.DoesNotContain(s.Name, finishers));
            }
            Assert.Equal(career.Roster.Count, career.Roster.Select(w => w.Id).Distinct().Count());
            Assert.Equal(career.Roster.Count, career.Roster.Select(w => w.RealName).Distinct().Count());
        }

        [Fact]
        public void TheSameSeedMakesTheSameWorld()
        {
            var a = Made(WorldSource.Generated, seed: "same");
            var b = Made(WorldSource.Generated, seed: "same");
            var c = Made(WorldSource.Generated, seed: "other");

            Assert.Equal(a.Roster.Select(w => (w.Id, w.Overness, w.Region)), b.Roster.Select(w => (w.Id, w.Overness, w.Region)));
            Assert.Equal(a.Rivals.Select(r => r.Name), b.Rivals.Select(r => r.Name));
            Assert.NotEqual(a.Roster.Select(w => w.Id), c.Roster.Select(w => w.Id));
        }

        [Fact]
        public void ATopUpNeverOutshinesTheDatabasesStars()
        {
            var database = DataLoaders.LoadEmbeddedWrestlers().Select(w => w.Overness).OrderBy(o => o).ToList();
            var topQuarter = database[database.Count * 3 / 4];
            var career = Made(WorldSource.Database, WorldSize.Medium);

            Assert.All(career.Roster.Where(w => w.IsGenerated), w => Assert.True(w.Overness <= topQuarter));
        }

        [Fact]
        public void BelowTelevisionAPromotionOpensWithItsOwnRegion()
        {
            var career = Made(WorldSource.Generated);

            foreach (var rival in career.Rivals.Where(r => !r.Promotion.HasTelevision))
                Assert.All(rival.Contracts, c => Assert.True(rival.Promotion.CanBook(career.FindWrestler(c.WrestlerId)!)));
            Assert.All(career.Contracts, c => Assert.Equal(Region.Midwest, career.FindWrestler(c.WrestlerId)!.Region));
        }

        [Fact]
        public void BelowTelevisionAPromotionKeepsBookingItsOwnRegion()
        {
            var career = Made(WorldSource.Generated);
            foreach (var _ in World.PlanOpening(career)) { }
            for (int d = 0; d < 35; d++) career.AdvanceOneDay();

            foreach (var rival in career.Rivals.Where(r => r.IsActive && !r.Promotion.HasTelevision))
                Assert.All(rival.Contracts, c => Assert.True(rival.Promotion.CanBook(career.FindWrestler(c.WrestlerId)!)));
        }

        [Fact]
        public void ThePlayerBooksTheirOwnRegionLikeEveryoneElse()
        {
            var career = Made(WorldSource.Generated);
            var far = career.Roster.First(w => w.Region != Region.Midwest && career.ContractOf(w) is null && career.SignedTo(w) is null);
            var near = career.Roster.First(w => w.Region == Region.Midwest && career.ContractOf(w) is null && career.SignedTo(w) is null);

            career.MakeRegular(far);
            career.MakeRegular(near);

            Assert.Null(career.ContractOf(far));
            Assert.NotNull(career.ContractOf(near));
            Assert.Contains("outside your region", career.Unavailable(far));
        }

        [Fact]
        public void TelevisionBooksFromAnywhere()
        {
            var tv = new Promotion { Tier = PromotionTier.Established, Region = Region.Northeast };
            var indie = new Promotion { Tier = PromotionTier.Independent, Region = Region.Northeast };
            var w = DataLoaders.LoadEmbeddedWrestlers()[0];
            w.Region = Region.Pacific;

            Assert.True(tv.CanBook(w));
            Assert.False(indie.CanBook(w));
            w.Region = null;   // the database's people work anywhere
            Assert.True(indie.CanBook(w));
        }

        [Fact]
        public void AGeneratedWorldComesBackFromASaveWhole()
        {
            var career = Made(WorldSource.Generated);
            foreach (var _ in World.PlanOpening(career)) { }
            for (int d = 0; d < 10; d++) career.AdvanceOneDay();

            var json = SaveSerializer.ToJson(career);
            var back = SaveSerializer.FromJson(json, DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(career.Roster.Count, back.Roster.Count);
            Assert.All(back.Roster, w => Assert.True(w.IsGenerated));
            Assert.Equal(career.WorldChoice, back.WorldChoice);
            Assert.Equal(career.Promotion.Region, back.Promotion.Region);
            Assert.Equal(career.Rivals.Select(r => r.Promotion.Region), back.Rivals.Select(r => r.Promotion.Region));
            Assert.Equal(career.Contracts.Select(c => c.WrestlerId), back.Contracts.Select(c => c.WrestlerId));

            var before = career.Roster[17];
            var after = back.FindWrestler(before.Id)!;
            Assert.Equal((before.RingName, before.Region, before.Overness, before.RingSkills.GetOverallSkill(), before.Signature.Count),
                         (after.RingName, after.Region, after.Overness, after.RingSkills.GetOverallSkill(), after.Signature.Count));
        }

        [Fact]
        public void ADatabaseWorldComesBackWithBothKindsOfPeople()
        {
            var career = Made(WorldSource.Database);
            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(career.Roster.Count, back.Roster.Count);
            Assert.Equal(career.Roster.Count(w => w.IsGenerated), back.Roster.Count(w => w.IsGenerated));
        }

        [Fact]
        public void EveryLevelIsPeopledFromTheBottomOfItsBand()
        {
            // Generated people at a level are mostly not its stars: the median sits in the
            // lower half of the level's band.
            var gen = new WrestlerGenerator(DataLoaders.LoadEmbeddedWrestlers(), [], StableSeed.From("band"), 2026);
            foreach (var level in new[] { PromotionTier.Global, PromotionTier.Independent })
            {
                var (lo, hi) = WrestlerGenerator.OvernessBand(level);
                var over = Enumerable.Range(0, 400).Select(_ => gen.Make(Division.Mens, level, Region.South).Overness).OrderBy(o => o).ToList();
                Assert.All(over, o => Assert.InRange(o, lo, hi));
                Assert.True(over[over.Count / 2] < (lo + hi) / 2);
            }
        }

        [Fact]
        public void ThePlayersRosterIsTheirOwnPeople_NotTheWorld_AndADraftOnlyDealsWithThem()
        {
            var career = Made(WorldSource.Database);
            var mine = career.PlayerRoster;

            Assert.NotEmpty(mine);
            Assert.True(mine.Count < career.Roster.Count / 4);
            Assert.All(mine, w => Assert.NotNull(career.ContractOf(w)));

            career.BeginSplit([new Brand { Name = "Red" }, new Brand { Name = "Blue" }]);
            var board = DraftBoard.Create(career, seed: 1);
            Assert.All(board.Available, w => Assert.Contains(w, mine));
        }
    }
}
