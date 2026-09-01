using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using Xunit.Abstractions;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    public class MatchStructureLibraryTests(ITestOutputHelper output)
    {
        // ── Catalogue structure ───────────────────────────────────────────────

        [Fact]
        public void AllStructures_HaveUniqueNames()
        {
            var duplicates = MatchStructureLibrary.All
                .GroupBy(s => s.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0,
                $"Duplicate structure names: {string.Join(", ", duplicates)}");
        }

        [Fact]
        public void AllStructures_HaveExactlyOneOpening()
        {
            foreach (var structure in MatchStructureLibrary.All)
            {
                var openings = structure.Beats.Count(b => b.IsOpening);
                Assert.True(openings == 1,
                    $"'{structure.Name}' has {openings} opening beats (expected 1).");
            }
        }

        [Fact]
        public void AllStructures_HaveExactlyOneFinish_AsLastBeat()
        {
            foreach (var structure in MatchStructureLibrary.All)
            {
                var finishes = structure.Beats.Count(b => b.IsFinish);
                Assert.True(finishes == 1,
                    $"'{structure.Name}' has {finishes} finish beats (expected 1).");
                Assert.True(structure.Beats.Last().IsFinish,
                    $"'{structure.Name}' finish beat is not last.");
            }
        }

        [Fact]
        public void Find_ReturnsStructure_CaseInsensitive()
        {
            Assert.NotNull(MatchStructureLibrary.Find("TV Formula"));
            Assert.NotNull(MatchStructureLibrary.Find("tv formula"));
            Assert.NotNull(MatchStructureLibrary.Find("Big Match Epic"));
            Assert.Null(MatchStructureLibrary.Find("does not exist"));
        }

        [Fact]
        public void WithTag_ReturnsCorrectSubset()
        {
            var feudMatches = MatchStructureLibrary.WithTag("Feud").ToList();
            Assert.True(feudMatches.Count >= 1);
            Assert.All(feudMatches, s => Assert.Contains("Feud", s.Tags));
        }

        // ── Round-trip: structure → MatchPlan → MatchEngine ──────────────────

        [Theory]
        [InlineData("TV Formula")]
        [InlineData("Face-in-Peril")]
        [InlineData("Technical Showcase")]
        [InlineData("Spotfest")]
        [InlineData("Grudge Brawl")]
        [InlineData("Big Match Epic")]
        public void NonFeudStructure_ProducesValidResult(string structureName)
        {
            var wrestlerA = MakeDummy("Alpha");
            var wrestlerB = MakeDummy("Bravo");

            var structure = MatchStructureLibrary.Find(structureName)!;

            var plan = new MatchPlan
            {
                WrestlerA = wrestlerA,
                WrestlerB = wrestlerB,
                MatchType = MatchType.Standard,
                Beats     = structure.Beats.ToList()
            };

            var errors = plan.Validate();
            Assert.True(errors.Count == 0,
                $"'{structureName}' failed validation: {string.Join(", ", errors)}");

            var result = new MatchEngine(seed: 42).Execute(plan);

            output.WriteLine($"[{structureName}]");
            output.WriteLine($"  Winner:      {result.Winner.RingName}");
            output.WriteLine($"  Star Rating: {result.StarDisplay}");
            output.WriteLine($"  Final Score: {result.FinalScore:F1}");

            Assert.True(result.StarRating > 0);
            Assert.Equal("Alpha", result.Winner.RingName);
        }

        [Fact]
        public void FeudBlowoff_FailsValidation_WithoutFeud()
        {
            var plan = new MatchPlan
            {
                WrestlerA = MakeDummy("Alpha"),
                WrestlerB = MakeDummy("Bravo"),
                MatchType = MatchType.Standard,
                Beats     = MatchStructureLibrary.Find("Feud Blowoff")!.Beats.ToList()
            };

            var errors = plan.Validate();
            Assert.Contains(errors, e => e.Contains("FeudalEscalation"));
        }

        [Fact]
        public void FeudBlowoff_ProducesValidResult_WithActiveFeud()
        {
            var wrestlerA = MakeDummy("Alpha");
            var wrestlerB = MakeDummy("Bravo");

            var feud = new Feud
            {
                WrestlerA = wrestlerA,
                WrestlerB = wrestlerB,
                Intensity = FeudIntensity.Hot,
                History   = [FeudHistoryTag.FamilyInvolved]
            };

            var plan = new MatchPlan
            {
                WrestlerA = wrestlerA,
                WrestlerB = wrestlerB,
                MatchType = MatchType.Standard,
                Feud      = feud,
                Beats     = MatchStructureLibrary.Find("Feud Blowoff")!.Beats.ToList()
            };

            var errors = plan.Validate();
            Assert.Empty(errors);

            var result = new MatchEngine(seed: 42).Execute(plan);

            output.WriteLine($"[Feud Blowoff]");
            output.WriteLine($"  Winner:      {result.Winner.RingName}");
            output.WriteLine($"  Star Rating: {result.StarDisplay}");

            Assert.True(result.StarRating > 0);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Wrestler MakeDummy(string name) => new(
            realName  : name,
            gimmick   : new Models.Gimmick(name),
            overness: 75,
            ringSkills: new Models.RingSkills(3, 3, 3, 3, 3, 3),
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
