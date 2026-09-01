using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using Xunit;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The brand split and its erosion.
    ///
    /// The mechanic under test is docs/wrestling-reference/22-brand-splits.md §4.1: each
    /// crossover is individually cheap and locally correct, and the aggregate of them
    /// destroys the structure. Most of these tests are about that asymmetry — a cost that
    /// never comes back against a benefit that fades.
    /// </summary>
    public class BrandSplitTests
    {
        private static Career NewCareer(params Wrestler[] roster)
        {
            var start = new DateOnly(2025, 1, 6);
            return new Career
            {
                Promotion   = new Promotion { Name = "Split Wrestling", Tier = PromotionTier.National },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster.ToList()
            };
        }

        private static (Career, Brand, Brand) Split(params Wrestler[] roster)
        {
            var career = NewCareer(roster);
            var red  = new Brand { Name = "Red" };
            var blue = new Brand { Name = "Blue" };

            career.BeginSplit([red, blue]);
            return (career, red, blue);
        }

        private static BookedMatch Match(Wrestler a, Wrestler b) => new()
        {
            Plan = new MatchPlanModel
            {
                WrestlerA = a,
                WrestlerB = b,
                Beats     = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList()
            },
            StructureName = "TV Formula"
        };

        private static Show ShowOf(string name, params ICardItem[] items) => new()
        {
            Name                 = name,
            Date                 = new DateTime(2025, 1, 6),
            Card                 = items.ToList(),
            TotalDurationMinutes = 180
        };

        // ── Assignment ───────────────────────────────────────────────────────

        [Fact]
        public void AssigningToABrand_TakesThemOffTheOther()
        {
            var star = TestRoster.Make("Star One");
            var (career, red, blue) = Split(star);

            career.Brands.Assign(star, red);
            career.Brands.Assign(star, blue);

            Assert.False(red.Contains(star.Id));
            Assert.True(blue.Contains(star.Id));
            Assert.Equal(blue, career.Brands.BrandOf(star));
        }

        [Fact]
        public void AssigningToNull_MakesThemAFreeAgent()
        {
            var star = TestRoster.Make("Star One");
            var (career, red, _) = Split(star);

            career.Brands.Assign(star, red);
            career.Brands.Assign(star, null);

            Assert.Null(career.Brands.BrandOf(star));
            Assert.Contains(star, career.Brands.Unassigned(career.Roster));
        }

        [Fact]
        public void BrandRosterResolvesIdsBackToTheLiveInstances()
        {
            var a = TestRoster.Make("Alpha One", overness: 80);
            var b = TestRoster.Make("Beta Two", overness: 60);
            var (career, red, _) = Split(a, b);

            career.Brands.Assign(a, red);
            career.Brands.Assign(b, red);

            var roster = career.RosterOf(red).ToList();

            Assert.Equal(2, roster.Count);
            Assert.Same(a, roster[0]);
            Assert.Same(b, roster[1]);
        }

        // ── Crossover detection ──────────────────────────────────────────────

        [Fact]
        public void SomeoneOnTheirOwnBrandsShow_IsNotACrossover()
        {
            var star = TestRoster.Make("Star One");
            var (career, red, _) = Split(star);
            career.Brands.Assign(star, red);

            Assert.False(career.Brands.IsCrossover(star, red));
        }

        [Fact]
        public void SomeoneOnTheOtherBrandsShow_IsACrossover()
        {
            var star = TestRoster.Make("Star One");
            var (career, red, blue) = Split(star);
            career.Brands.Assign(star, red);

            Assert.True(career.Brands.IsCrossover(star, blue));
        }

        [Fact]
        public void CompanyWideShow_IsNeverACrossover()
        {
            // The inter-brand supercard of doc 22 §3.4 — the legitimate release valve.
            var star = TestRoster.Make("Star One");
            var (career, red, _) = Split(star);
            career.Brands.Assign(star, red);

            Assert.False(career.Brands.IsCrossover(star, showBrand: null));
        }

        [Fact]
        public void UnassignedPerformer_IsNeverACrossover()
        {
            var free = TestRoster.Make("Free Agent");
            var (career, _, blue) = Split(free);

            Assert.False(career.Brands.IsCrossover(free, blue));
        }

        [Fact]
        public void DetectListsEachCrossoverOnce_HoweverManyItemsTheyWork()
        {
            var visitor = TestRoster.Make("Visiting Star", overness: 90);
            var host    = TestRoster.Make("Home Hand", overness: 60);
            var other   = TestRoster.Make("Other Hand", overness: 55);

            var (career, red, blue) = Split(visitor, host, other);
            career.Brands.Assign(visitor, red);
            career.Brands.Assign(host, blue);
            career.Brands.Assign(other, blue);

            var card = new ICardItem[] { Match(visitor, host), Match(visitor, other) };
            var found = BrandIntegrity.Detect(card, career.Brands, blue);

            Assert.Single(found);
            Assert.Same(visitor, found[0].Wrestler);
            Assert.Equal(red, found[0].Home);
        }

        [Fact]
        public void CrossingOverABiggerStarCostsMore()
        {
            var big   = TestRoster.Make("Big Name", overness: 95);
            var small = TestRoster.Make("Small Name", overness: 20);

            Assert.True(BrandIntegrity.CostOf(big) > BrandIntegrity.CostOf(small));
            Assert.InRange(BrandIntegrity.CostOf(small), BrandIntegrity.CostFloor, BrandIntegrity.CostFloor + 1);
        }

        // ── Erosion ──────────────────────────────────────────────────────────

        [Fact]
        public void RunningAShowWithACrossover_ErodesIntegrityAndTheCeiling()
        {
            var visitor = TestRoster.Make("Visiting Star", overness: 90);
            var host    = TestRoster.Make("Home Hand", overness: 60);

            var (career, red, blue) = Split(visitor, host);
            career.Brands.Assign(visitor, red);
            career.Brands.Assign(host, blue);

            double cost = BrandIntegrity.CostOf(visitor);

            var result = new ShowSimulator(career.FeudBook, seed: 7, brands: new BrandContext(career.Brands, blue))
                .Simulate(ShowOf("Blue Night", Match(visitor, host)));

            Assert.NotNull(result.Brand);
            Assert.Equal(100 - cost, career.Brands.Integrity, 3);
            Assert.Equal(100 - cost * BrandIntegrity.PermanentShare, career.Brands.Ceiling, 3);
            Assert.Equal(1, career.Brands.CrossoverCount);
            Assert.Equal("Visiting Star", career.Brands.Crossovers[0].WrestlerName);
        }

        [Fact]
        public void AnExclusiveShow_ErodesNothing()
        {
            var a = TestRoster.Make("Home One", overness: 70);
            var b = TestRoster.Make("Home Two", overness: 65);

            var (career, _, blue) = Split(a, b);
            career.Brands.Assign(a, blue);
            career.Brands.Assign(b, blue);

            var result = new ShowSimulator(career.FeudBook, seed: 7, brands: new BrandContext(career.Brands, blue))
                .Simulate(ShowOf("Blue Night", Match(a, b)));

            Assert.Equal(100, career.Brands.Integrity);
            Assert.True(result.Brand!.WasExclusive);
            Assert.True(result.Brand.ExclusivityBonus > 0);
        }

        [Fact]
        public void ACompanyWideShow_ChargesNothingHoweverMixedTheCardIs()
        {
            var a = TestRoster.Make("Red Star", overness: 90);
            var b = TestRoster.Make("Blue Star", overness: 88);

            var (career, red, blue) = Split(a, b);
            career.Brands.Assign(a, red);
            career.Brands.Assign(b, blue);

            var result = new ShowSimulator(career.FeudBook, seed: 7, brands: new BrandContext(career.Brands, null))
                .Simulate(ShowOf("The Supercard", Match(a, b)));

            Assert.Equal(100, career.Brands.Integrity);
            Assert.Null(result.Brand);
        }

        [Fact]
        public void ARunOfDefensibleCrossovers_RuinsTheSplit()
        {
            // The whole point. Nobody ever books "destroy the split"; they book the biggest
            // available name on the show that needs it, twenty times. Doc 22 §4.1.
            var visitor = TestRoster.Make("Visiting Star", overness: 90);
            var host    = TestRoster.Make("Home Hand", overness: 60);

            var (career, red, blue) = Split(visitor, host);
            career.Brands.Assign(visitor, red);
            career.Brands.Assign(host, blue);

            var context = new BrandContext(career.Brands, blue);

            for (int week = 0; week < 25; week++)
                new ShowSimulator(career.FeudBook, seed: week, brands: context)
                    .Simulate(ShowOf($"Blue Night {week}", Match(visitor, host)));

            Assert.True(career.Brands.Integrity < 20,
                $"25 crossovers should have gutted the split, integrity was {career.Brands.Integrity:F1}");
            Assert.Equal("The split exists in the graphics", BrandIntegrity.Phase(career.Brands.Integrity));

            // And no draft can undo it: the ceiling has come down with it.
            Assert.True(career.Brands.Ceiling < 70);
        }

        // ── The payoff, and its decay ────────────────────────────────────────

        [Fact]
        public void TheCrossoverBonusFadesWithIntegrity()
        {
            var star = TestRoster.Make("Big Name", overness: 90);

            double whole  = BrandIntegrity.AttractionBonus(star, 100);
            double halved = BrandIntegrity.AttractionBonus(star, 50);
            double gone   = BrandIntegrity.AttractionBonus(star, 0);

            Assert.True(whole > 0.1);
            Assert.Equal(whole / 2, halved, 6);
            Assert.Equal(0, gone);
        }

        [Fact]
        public void ACrossoverLiftsTheItemItAppearsOn()
        {
            var visitor = TestRoster.Make("Visiting Star", overness: 90);
            var host    = TestRoster.Make("Home Hand", overness: 60);

            // Same seed, same card, same people — the only difference is whose show it is.
            var (crossCareer, red, blue) = Split(visitor, host);
            crossCareer.Brands.Assign(visitor, red);
            crossCareer.Brands.Assign(host, blue);

            var crossed = new ShowSimulator(
                    crossCareer.FeudBook, seed: 11, brands: new BrandContext(crossCareer.Brands, blue))
                .Simulate(ShowOf("Blue Night", Match(visitor, host)));

            var (homeCareer, _, homeBlue) = Split(visitor, host);
            homeCareer.Brands.Assign(visitor, homeBlue);
            homeCareer.Brands.Assign(host, homeBlue);

            var athome = new ShowSimulator(
                    homeCareer.FeudBook, seed: 11, brands: new BrandContext(homeCareer.Brands, homeBlue))
                .Simulate(ShowOf("Blue Night", Match(visitor, host)));

            Assert.True(crossed.Items[0].RawScore > athome.Items[0].RawScore);
        }

        // ── What integrity governs ───────────────────────────────────────────

        [Fact]
        public void StarMakingFallsWithIntegrityButNeverToNothing()
        {
            Assert.Equal(1.0, BrandIntegrity.StarMakingFactor(100), 6);
            Assert.Equal(0.75, BrandIntegrity.StarMakingFactor(50), 6);
            Assert.Equal(0.50, BrandIntegrity.StarMakingFactor(0), 6);
        }

        [Fact]
        public void OnARuinedSplit_WinnersKeepLessOfWhatTheyEarn()
        {
            var winner = TestRoster.Make("Rising Hand", overness: 55);
            var loser  = TestRoster.Make("Established Name", overness: 85);

            double Gain(double integrity)
            {
                var a = TestRoster.Make("Rising Hand", overness: 55);
                var b = TestRoster.Make("Established Name", overness: 85);

                var (career, _, blue) = Split(a, b);
                career.Brands.Assign(a, blue);
                career.Brands.Assign(b, blue);
                career.Brands.Integrity = integrity;

                new ShowSimulator(career.FeudBook, seed: 3, brands: new BrandContext(career.Brands, blue))
                    .Simulate(ShowOf("Blue Night", Match(a, b)));

                return a.Overness - 55;
            }

            Assert.True(Gain(100) > Gain(0));
            Assert.True(Gain(0) > 0);

            // Referenced only so the fixture reads as a pairing rather than two strays.
            Assert.NotEqual(winner.Id, loser.Id);
        }

        [Fact]
        public void ExclusivityIsWorthNothingOnceIntegrityHasGone()
        {
            Assert.True(BrandIntegrity.ExclusivityBonus(100) > 0);
            Assert.Equal(0, BrandIntegrity.ExclusivityBonus(0));
        }

        // ── The B-show spiral ────────────────────────────────────────────────

        [Fact]
        public void BrandsAreNotComparedUntilBothHaveRunEnoughShows()
        {
            var (career, red, blue) = Split(TestRoster.Make("One"));

            red.RecordShow(80);
            red.RecordShow(80);
            red.RecordShow(80);

            Assert.Equal(1.0, BrandIntegrity.StandingFactor(red, career.Brands), 6);
            Assert.False(BrandIntegrity.IsSecondary(blue, career.Brands));
        }

        [Fact]
        public void ABrandRatingBelowTheOther_BecomesTheBShow()
        {
            var (career, red, blue) = Split(TestRoster.Make("One"));

            for (int i = 0; i < 4; i++) { red.RecordShow(80); blue.RecordShow(50); }

            Assert.True(BrandIntegrity.IsSecondary(blue, career.Brands));
            Assert.False(BrandIntegrity.IsSecondary(red, career.Brands));

            Assert.Equal(1.0, BrandIntegrity.StandingFactor(red, career.Brands), 6);
            Assert.True(BrandIntegrity.StandingFactor(blue, career.Brands) < 0.95);
        }

        [Fact]
        public void OnlyTheFormWindowIsKept()
        {
            var brand = new Brand { Name = "Red" };
            for (int i = 0; i < Brand.FormWindow + 5; i++) brand.RecordShow(i);

            Assert.Equal(Brand.FormWindow, brand.RecentRatings.Count);
        }

        // ── Depth ────────────────────────────────────────────────────────────

        [Fact]
        public void TheShippedRosterSizeIsTooThinForASplit_ButNothingBlocksIt()
        {
            var roster = Enumerable.Range(0, 30)
                .Select(i => TestRoster.Make($"Wrestler {i:00}", overness: 40))
                .ToList();

            var report = BrandIntegrity.Depth(roster, 2);

            Assert.True(report.IsThin);
            Assert.Equal(15, report.PerBrand);
            Assert.Contains(report.Warnings, w => w.Contains("60–80"));
            Assert.Contains(report.Warnings, w => w.Contains("25–40"));

            // And the split still goes ahead — warn, never block. Doc 22 §3.6 / §4.4.
            var career = NewCareer(roster.ToArray());
            career.BeginSplit([new Brand { Name = "Red" }, new Brand { Name = "Blue" }]);

            Assert.True(career.Brands.Active);
        }

        [Fact]
        public void ADeepRosterRaisesNoDepthWarnings()
        {
            var roster = Enumerable.Range(0, 80)
                .Select(i => TestRoster.Make($"Wrestler {i:00}", overness: 78))
                .Select((w, i) => { w.Division = i % 2 == 0 ? Division.Mens : Division.Womens; return w; })
                .ToList();

            var report = BrandIntegrity.Depth(roster, 2);

            Assert.False(report.IsThin);
            Assert.Equal(40, report.PerBrand);
        }

        [Fact]
        public void ThinTopOfTheCardIsCalledOutSeparately()
        {
            // Sixty bodies and nobody the audience cares about is still not a split.
            var roster = Enumerable.Range(0, 60)
                .Select(i => TestRoster.Make($"Wrestler {i:00}", overness: 30))
                .Select((w, i) => { w.Division = i % 2 == 0 ? Division.Mens : Division.Womens; return w; })
                .ToList();

            var report = BrandIntegrity.Depth(roster, 2);

            Assert.Contains(report.Warnings, w => w.Contains("upper card"));
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        [Fact]
        public void EndingTheSplit_ClearsBrandsButKeepsTheErosion()
        {
            var visitor = TestRoster.Make("Visiting Star", overness: 90);
            var host    = TestRoster.Make("Home Hand", overness: 60);

            var (career, red, blue) = Split(visitor, host);
            career.Brands.Assign(visitor, red);
            career.Brands.Assign(host, blue);

            var definition = new ShowDefinition { Name = "Blue Night", BrandId = blue.Id };
            career.ShowDefinitions.Add(definition);
            career.MaterialiseSchedule();

            new ShowSimulator(career.FeudBook, seed: 7, brands: new BrandContext(career.Brands, blue))
                .Simulate(ShowOf("Blue Night", Match(visitor, host)));

            double ceiling = career.Brands.Ceiling;
            career.EndSplit();

            Assert.False(career.Brands.Active);
            Assert.Empty(career.Brands.Brands);
            Assert.Null(definition.BrandId);
            Assert.All(career.Shows.Where(s => !s.HasRun), s => Assert.Null(s.BrandId));
            Assert.Equal(ceiling, career.Brands.Ceiling, 6);
        }

        [Fact]
        public void SplittingAgainStartsFromTheCeiling_NotFromWhole()
        {
            var visitor = TestRoster.Make("Visiting Star", overness: 90);
            var host    = TestRoster.Make("Home Hand", overness: 60);

            var (career, red, blue) = Split(visitor, host);
            career.Brands.Assign(visitor, red);
            career.Brands.Assign(host, blue);

            new ShowSimulator(career.FeudBook, seed: 7, brands: new BrandContext(career.Brands, blue))
                .Simulate(ShowOf("Blue Night", Match(visitor, host)));

            double ceiling = career.Brands.Ceiling;
            career.EndSplit();
            career.BeginSplit([new Brand { Name = "Gold" }, new Brand { Name = "Silver" }]);

            Assert.Equal(ceiling, career.Brands.Integrity, 6);
            Assert.True(career.Brands.Integrity < 100);
        }

        [Fact]
        public void MaterialisedDatesInheritTheirDefinitionsBrand()
        {
            var (career, _, blue) = Split(TestRoster.Make("One"));

            career.ShowDefinitions.Add(new ShowDefinition
            {
                Name       = "Blue Night",
                Recurrence = RecurrenceKind.Weekly,
                Day        = DayOfWeek.Tuesday,
                BrandId    = blue.Id
            });

            var added = career.MaterialiseSchedule();

            Assert.NotEmpty(added);
            Assert.All(added, s => Assert.Equal(blue.Id, s.BrandId));
            Assert.Equal(blue, career.BrandOfShow(added[0]));
        }

        [Fact]
        public void EligibleForABrandShow_IsThatBrandPlusTheFreeAgents()
        {
            var mine = TestRoster.Make("Mine");
            var them = TestRoster.Make("Theirs");
            var free = TestRoster.Make("Free");

            var (career, red, blue) = Split(mine, them, free);
            career.Brands.Assign(mine, blue);
            career.Brands.Assign(them, red);

            var show = new ScheduledShow { Name = "Blue Night", BrandId = blue.Id };
            var eligible = career.EligibleFor(show).ToList();

            Assert.Contains(mine, eligible);
            Assert.Contains(free, eligible);
            Assert.DoesNotContain(them, eligible);
        }
    }
}
