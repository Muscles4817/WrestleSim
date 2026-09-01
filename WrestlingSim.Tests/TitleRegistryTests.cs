using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The fixed prestige pool — docs/wrestling-reference/21-championships.md §2.1.
    ///
    /// This is the mechanic the whole feature turns on: the audience's attention is
    /// finite, so every belt you add takes value off the ones you already have. If these
    /// stop holding, "should we add a title?" stops being a decision.
    /// </summary>
    public class TitleRegistryTests
    {
        private static readonly DateOnly Day0 = new(2025, 1, 6);

        private static TitleRegistry Standard()
        {
            var registry = new TitleRegistry();
            registry.SeedDefaults("Test", Day0);
            return registry;
        }

        [Fact]
        public void TheShippedSlateIsAWorldTitleASecondaryAndAWomensWorldTitle()
        {
            var registry = Standard();

            Assert.Equal(3, registry.Active.Count);
            Assert.Equal(2, registry.Active.Count(t => t.Tier == TitleTier.World));
            Assert.Single(registry.Active.Where(t => t.Tier == TitleTier.Secondary));
            Assert.Single(registry.Active.Where(t => t.Division == Division.Womens));
        }

        [Fact]
        public void TheShippedSlateExactlyFitsTheAudiencesAttention()
        {
            var registry = Standard();

            Assert.Equal(TitleRegistry.AttentionCapacity, registry.Demand, 3);
            Assert.Equal(1.0, registry.Dilution, 3);

            // Undiluted, so every belt is worth exactly what it has earned.
            foreach (var title in registry.Active)
                Assert.Equal(title.Standing, title.Prestige, 3);
        }

        [Fact]
        public void AFourthBeltMeasurablyWeakensTheOtherThree()
        {
            var registry = Standard();
            var before = registry.Active.ToDictionary(t => t.Id, t => t.Prestige);

            registry.Create("Hardcore Championship", TitleTier.Tertiary, Division.Mens, Day0);

            foreach (var (id, was) in before)
            {
                var now = registry.Find(id)!.Prestige;
                Assert.True(now < was,
                    $"{registry.Find(id)!.Name} should be worth less: {was:F2} → {now:F2}");
            }
        }

        [Fact]
        public void ASecondWorldTitleCostsFarMoreThanASpecialtyBelt()
        {
            double specialty = Standard().DilutionCostOfAdding(TitleTier.Tertiary);
            double secondWorld = Standard().DilutionCostOfAdding(TitleTier.World);

            Assert.True(secondWorld > specialty * 2,
                $"two world titles is the expensive mistake: {secondWorld:P0} vs {specialty:P0}");
        }

        [Fact]
        public void TheAdvertisedCostOfAddingABeltIsWhatItActuallyCosts()
        {
            var registry = Standard();
            var world = registry.Active.First(t => t.Tier == TitleTier.World && t.Division == Division.Mens);

            double predicted = registry.DilutionCostOfAdding(TitleTier.Secondary);
            double before = world.Prestige;

            registry.Create("Second Television Title", TitleTier.Secondary, Division.Mens, Day0);

            double actual = (before - world.Prestige) / before;
            Assert.Equal(predicted, actual, 4);
        }

        [Fact]
        public void RetiringABeltGivesItsAttentionBackToTheOthers()
        {
            var registry = Standard();
            registry.Create("Hardcore Championship", TitleTier.Tertiary, Division.Mens, Day0);

            var world = registry.Active.First(t => t.Tier == TitleTier.World && t.Division == Division.Mens);
            double diluted = world.Prestige;

            registry.Retire(registry.Active.First(t => t.Tier == TitleTier.Tertiary), Day0.AddDays(30));

            Assert.True(world.Prestige > diluted,
                $"retiring the fourth belt should restore the others: {diluted:F2} → {world.Prestige:F2}");
            Assert.Equal(world.Standing, world.Prestige, 3);
        }

        [Fact]
        public void ALeanSlateIsNeverDilutedButCannotManufacturePrestige()
        {
            var registry = new TitleRegistry();
            var only = registry.Create("Sole Championship", TitleTier.World, Division.Mens, Day0);
            only.Standing = 50;

            Assert.Equal(1.0, registry.Dilution, 3);
            Assert.Equal(50, only.Prestige, 3);
        }

        [Fact]
        public void ANewBeltStartsWithNoLineageAndAStandingToMatch()
        {
            var registry = Standard();
            var created = registry.Create("Brand New Title", TitleTier.World, Division.Mens, Day0);

            Assert.Empty(created.Lineage);
            Assert.True(created.IsVacant);
            Assert.Equal(Title.NewTitleStanding, created.Standing, 3);

            // Even declared at world level it is worth a fraction of the belt with history.
            var established = registry.Active.First(t => t.Name.Contains("World Heavyweight"));
            Assert.True(created.Prestige < established.Prestige / 2);
        }

        [Fact]
        public void RetiringABeltKeepsItsLineage()
        {
            var registry = Standard();
            var title = registry.Active.First();
            title.Lineage.Add(new TitleReign
            {
                Champion = TestRoster.Make("Old Champ"), ReignNumber = 1, Won = Day0
            });

            registry.Retire(title, Day0.AddDays(50));

            Assert.True(title.Retired);
            Assert.Single(title.Lineage);
            Assert.True(title.Lineage[0].Vacated);
            Assert.Equal(0, title.AttentionClaim);
            Assert.Equal(Day0.AddDays(50), title.RetiredOn);
        }

        [Fact]
        public void RevivingABeltBringsItsHistoryBack()
        {
            var registry = Standard();
            var title = registry.Active.First();
            registry.Retire(title, Day0.AddDays(50));

            registry.Revive(title);

            Assert.False(title.Retired);
            Assert.Contains(title, registry.Active);
            Assert.Equal(1.0, title.AttentionClaim, 3);
        }

        [Fact]
        public void HeldByFindsEveryBeltAPersonIsCarrying()
        {
            var registry = Standard();
            var champion = TestRoster.Make("Double Champ");

            foreach (var title in registry.Active.Take(2))
                title.Lineage.Add(new TitleReign { Champion = champion, ReignNumber = 1, Won = Day0 });

            Assert.Equal(2, registry.HeldBy(champion).Count);
            Assert.True(registry.IsChampion(champion));
        }
    }
}
