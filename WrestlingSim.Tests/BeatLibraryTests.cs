using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using Xunit.Abstractions;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Verifies the BeatLibrary catalogue and proves that match plans built
    /// entirely from library templates produce valid, runnable matches.
    /// </summary>
    public class BeatLibraryTests(ITestOutputHelper output)
    {
        // ── Catalogue structure ───────────────────────────────────────────────

        [Fact]
        public void AllTemplates_HaveUniqueNames()
        {
            var duplicates = BeatLibrary.All
                .GroupBy(t => t.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0,
                $"Duplicate template names: {string.Join(", ", duplicates)}");
        }

        [Fact]
        public void AllBeatTypes_HaveAtLeastOneTemplate()
        {
            var coveredTypes = BeatLibrary.All.Select(t => t.Type).ToHashSet();

            var missing = Enum.GetValues<BeatType>()
                .Where(bt => !coveredTypes.Contains(bt))
                .ToList();

            Assert.True(missing.Count == 0,
                $"BeatTypes with no template: {string.Join(", ", missing)}");
        }

        [Fact]
        public void Find_ReturnsTemplate_CaseInsensitive()
        {
            Assert.NotNull(BeatLibrary.Find("hot start"));
            Assert.NotNull(BeatLibrary.Find("HOT START"));
            Assert.NotNull(BeatLibrary.Find("Clean Victory"));
            Assert.Null(BeatLibrary.Find("does not exist"));
        }

        [Fact]
        public void ByCategory_ReturnsCorrectSubset()
        {
            var openings = BeatLibrary.ByCategory(BeatLibrary.CatOpening).ToList();
            Assert.True(openings.Count >= 3);
            Assert.All(openings, t => Assert.Equal(BeatLibrary.CatOpening, t.Category));
        }

        [Fact]
        public void Available_FiltersFeudGatedTemplates()
        {
            // No feud: FeudalEscalation and OutsideParty should be unavailable
            var noFeud = BeatLibrary.Available(null).ToList();
            Assert.DoesNotContain(noFeud, t => t.Type == BeatType.FeudalEscalation);
            Assert.DoesNotContain(noFeud, t => t.Type == BeatType.ThirdPartyPullIn);

            // Building feud unlocks them
            var feud = new Feud
            {
                WrestlerA = MakeDummy("A"),
                WrestlerB = MakeDummy("B"),
                Intensity = FeudIntensity.Building
            };
            var withFeud = BeatLibrary.Available(feud).ToList();
            Assert.Contains(withFeud, t => t.Type == BeatType.FeudalEscalation);
        }

        [Fact]
        public void ToMatchBeat_UsesTemplateDefaults_WhenNoOverride()
        {
            var template = BeatLibrary.Find("Power Beatdown")!;
            var beat = template.ToMatchBeat(BeatControl.WrestlerA);

            Assert.Equal(BeatType.HeatSegment,      beat.Type);
            Assert.Equal(BeatControl.WrestlerA,     beat.Control);
            Assert.Equal(BeatIntensity.High,         beat.Intensity);
            Assert.Equal(BeatDuration.Medium,        beat.Duration);
            Assert.Null(beat.FeudalResonance);
        }

        [Fact]
        public void ToMatchBeat_RespectsOverrides()
        {
            var template = BeatLibrary.Find("Power Beatdown")!;
            var beat = template.ToMatchBeat(
                BeatControl.WrestlerB,
                intensity: BeatIntensity.Extreme,
                duration:  BeatDuration.Long);

            Assert.Equal(BeatControl.WrestlerB, beat.Control);
            Assert.Equal(BeatIntensity.Extreme,  beat.Intensity);
            Assert.Equal(BeatDuration.Long,      beat.Duration);
        }

        // ── Library-built match round-trip ────────────────────────────────────

        [Fact]
        public void LibraryBuiltMatchPlan_PassesValidation_AndProducesResult()
        {
            var wrestlerA = MakeDummy("Alpha");
            var wrestlerB = MakeDummy("Bravo");

            // Build a complete, valid plan using only library templates
            var plan = new MatchPlan
            {
                WrestlerA = wrestlerA,
                WrestlerB = wrestlerB,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    BeatLibrary.Find("Hot Start")!
                        .ToMatchBeat(BeatControl.Even),

                    BeatLibrary.Find("Power Beatdown")!
                        .ToMatchBeat(BeatControl.WrestlerB),

                    BeatLibrary.Find("Hot Comeback")!
                        .ToMatchBeat(BeatControl.WrestlerA),

                    BeatLibrary.Find("Shock Kickout")!
                        .ToMatchBeat(BeatControl.WrestlerA),

                    BeatLibrary.Find("Clean Victory")!
                        .ToMatchBeat(BeatControl.WrestlerA)
                ]
            };

            var errors = plan.Validate();
            Assert.Empty(errors);

            var result = new MatchEngine(seed: 1).Execute(plan);

            output.WriteLine($"  Winner:      {result.Winner.RingName}");
            output.WriteLine($"  Star Rating: {result.StarDisplay}");
            output.WriteLine($"  Final Score: {result.FinalScore:F1}");

            Assert.Equal(wrestlerA.RingName, result.Winner.RingName);
            Assert.True(result.StarRating > 0);
        }

        [Fact]
        public void LibraryBuiltMatchPlan_WithFeudalEscalation_RequiresValidFeud()
        {
            var wrestlerA = MakeDummy("Alpha");
            var wrestlerB = MakeDummy("Bravo");

            // Plan that includes FeudalEscalation but has no feud
            var plan = new MatchPlan
            {
                WrestlerA = wrestlerA,
                WrestlerB = wrestlerB,
                MatchType = MatchType.Standard,
                Beats     =
                [
                    BeatLibrary.Find("Hot Start")!.ToMatchBeat(BeatControl.Even),
                    BeatLibrary.Find("Feud Erupts")!.ToMatchBeat(BeatControl.Even),
                    BeatLibrary.Find("Clean Victory")!.ToMatchBeat(BeatControl.WrestlerA)
                ]
            };

            var errors = plan.Validate();
            Assert.Contains(errors, e => e.Contains("FeudalEscalation"));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Wrestler MakeDummy(string name) => new(
            realName : name,
            gimmick  : new Models.Gimmick(name),
            popularity: 70,
            ringSkills: new Models.RingSkills(2, 3, 3, 3, 3, 2),
            charisma  : 3.5,
            style     : WrestlingStyle.Brawler
        )
        {
            Mental = new Models.Person.MentalAttributes
            {
                Psychology = 65,
                Selling    = 65,
                RingIQ     = 65,
                Toughness  = 70
            }
        };
    }
}
