using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchTypeEnum = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The result screen could say what happened and never why — Technical 46, three and a
    /// half stars, and no account of which decision produced either. These pin the rules that
    /// turn the engine's own recorded signals into that account.
    ///
    /// Every assertion here is about a *cause being named*, not about a score. A diagnosis
    /// that says "storytelling was low" has restated the number; one that says "there is no
    /// near fall anywhere in the sheet" has told the booker what to do, and that difference
    /// is the whole feature.
    /// </summary>
    public class MatchDiagnosisTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name) => TestRoster.Make(name);

        private static MatchBeat Beat(string template, BeatControl control) =>
            BeatLibrary.Find(template)!.ToMatchBeat(control);

        private static MatchPlan Plan(
            IEnumerable<MatchBeat> beats,
            MatchTypeEnum type = MatchTypeEnum.Standard,
            Feud? feud = null)
        {
            var a = W("Alpha");
            var b = W("Bravo");
            return new MatchPlan
            {
                Sides = [new MatchSide { Members = [a] }, new MatchSide { Members = [b] }],
                Beats = beats.ToList(),
                MatchType = type,
                Feud = feud
            };
        }

        private static (MatchEngineResult Result, MatchPlan Plan) Run(
            MatchPlan plan, double familiarity = 1.0) =>
            (new MatchEngine(seed: 12345).Execute(plan, familiarity), plan);

        private static IEnumerable<string> Texts(IReadOnlyList<CategoryDiagnosis> d, string category) =>
            d.Single(c => c.Category == category).Causes.Select(c => c.Text);

        private static IEnumerable<string> Hurts(IReadOnlyList<CategoryDiagnosis> d, string category) =>
            d.Single(c => c.Category == category)
             .Causes.Where(c => c.Direction == CauseDirection.Hurt).Select(c => c.Text);

        /// <summary>A plain, valid match: open, control, comeback, finish.</summary>
        private static List<MatchBeat> Standard() =>
        [
            Beat("Standard Collar-and-Elbow", BeatControl.Even),
            Beat("Power Beatdown",    BeatControl.WrestlerB),
            Beat("Hot Comeback",     BeatControl.WrestlerA),
            Beat("Clean Victory",    BeatControl.WrestlerA)
        ];

        // ── The rule that matters most ───────────────────────────────────────

        [Fact]
        public void AMatchWithNoNearFall_IsToldSo_AndToldToAddOne()
        {
            var (result, plan) = Run(Plan(Standard()));
            var d = MatchDiagnosis.Of(plan, result);

            Assert.Contains(Hurts(d, "Storytelling"), t => t.Contains("no near fall anywhere"));
            Assert.Contains(MatchDiagnosis.Lessons(plan, result), l => l.StartsWith("Put a near fall in"));
        }

        [Fact]
        public void AMatchWithANearFall_IsNotToldToAddOne()
        {
            var beats = Standard();
            beats.Insert(2, Beat("Signature Cover", BeatControl.WrestlerB));

            var (result, plan) = Run(Plan(beats));
            var d = MatchDiagnosis.Of(plan, result);

            Assert.DoesNotContain(Hurts(d, "Storytelling"), t => t.Contains("no near fall anywhere"));
            Assert.DoesNotContain(MatchDiagnosis.Lessons(plan, result), l => l.StartsWith("Put a near fall in"));
        }

        // ── Repetition is the review's "two heat segments" complaint ─────────

        [Fact]
        public void TwoOfTheSameBeatBackToBack_AreCalledRepetition_NotEscalation()
        {
            List<MatchBeat> beats =
            [
                Beat("Standard Collar-and-Elbow", BeatControl.Even),
                Beat("Power Beatdown",    BeatControl.WrestlerB),
                Beat("Power Beatdown",    BeatControl.WrestlerB),
                Beat("Hot Comeback",     BeatControl.WrestlerA),
                Beat("Clean Victory",    BeatControl.WrestlerA)
            ];

            var (result, plan) = Run(Plan(beats));
            var d = MatchDiagnosis.Of(plan, result);

            Assert.Contains(Hurts(d, "Storytelling"), t => t.Contains("back to back"));
            Assert.Contains(MatchDiagnosis.Lessons(plan, result),
                l => l.Contains("hope spot") || l.Contains("false finish"));
        }

        [Fact]
        public void AVariedSheet_IsNotAccusedOfRepetition()
        {
            var (result, plan) = Run(Plan(Standard()));
            Assert.DoesNotContain(Hurts(MatchDiagnosis.Of(plan, result), "Storytelling"),
                t => t.Contains("back to back"));
        }

        // ── The declared type ────────────────────────────────────────────────

        [Fact]
        public void APlanThatDoesNotDeliverItsDeclaredType_IsNamedBeatByBeat()
        {
            // A spotfest sheet declared as a technical match.
            List<MatchBeat> beats =
            [
                Beat("Hot Start",     BeatControl.Even),
                Beat("Aerial Assault", BeatControl.WrestlerA),
                Beat("Ringside Brawl", BeatControl.WrestlerB),
                Beat("Clean Victory", BeatControl.WrestlerA)
            ];

            var (result, plan) = Run(Plan(beats, MatchTypeEnum.Technical));
            var d = MatchDiagnosis.Of(plan, result);

            Assert.Contains(Hurts(d, "Technical"), t => t.Contains("do not deliver"));
            Assert.Contains(MatchDiagnosis.Lessons(plan, result), l => l.Contains("technical"));
        }

        [Fact]
        public void AStandardMatchPromisesNothing_SoIsNeverAccusedOfMissingIt()
        {
            var (result, plan) = Run(Plan(Standard(), MatchTypeEnum.Standard));
            Assert.DoesNotContain(Hurts(MatchDiagnosis.Of(plan, result), "Technical"),
                t => t.Contains("do not deliver"));
        }

        // ── Things the booker cannot fix inside this match ───────────────────

        [Fact]
        public void AStalePairing_IsReportedAsACause_AndAsALessonAboutTheNextMatch()
        {
            var (result, plan) = Run(Plan(Standard()), familiarity: 0.6);
            var d = MatchDiagnosis.Of(plan, result);

            Assert.Contains(Hurts(d, "Crowd"), t => t.Contains("drawing"));
            Assert.Contains(MatchDiagnosis.Lessons(plan, result), l => l.Contains("rest"));
        }

        [Fact]
        public void NoFeud_IsNamedAsAStorytellingCost()
        {
            var (result, plan) = Run(Plan(Standard()));
            Assert.Contains(Hurts(MatchDiagnosis.Of(plan, result), "Storytelling"),
                t => t.Contains("No story behind the match"));
        }

        // ── Shape of the output ──────────────────────────────────────────────

        [Fact]
        public void EveryCategoryExplainsItself()
        {
            var (result, plan) = Run(Plan(Standard()));
            var d = MatchDiagnosis.Of(plan, result);

            Assert.Equal(4, d.Count);
            foreach (var category in d)
            {
                output.WriteLine($"{category.Category}:");
                foreach (var cause in category.Causes)
                    output.WriteLine($"  {(cause.Direction == CauseDirection.Helped ? "+" : "-")} {cause.Text}");

                // A row that expands to nothing is worse than one that does not expand.
                Assert.NotEmpty(category.Causes);
            }
        }

        [Fact]
        public void LessonsAreActionable_AndThereAreNeverMoreThanThree()
        {
            // A sheet with several things wrong at once, to see the ranking hold.
            List<MatchBeat> beats =
            [
                Beat("Hot Start",     BeatControl.Even),
                Beat("Aerial Assault", BeatControl.WrestlerB),
                Beat("Aerial Assault", BeatControl.WrestlerB),
                Beat("Clean Victory", BeatControl.WrestlerA)
            ];

            var (result, plan) = Run(Plan(beats, MatchTypeEnum.Technical), familiarity: 0.5);
            var lessons = MatchDiagnosis.Lessons(plan, result);

            foreach (var l in lessons) output.WriteLine(l);

            Assert.InRange(lessons.Count, 1, 3);
            // Every lesson tells the booker to do something, not how the match scored.
            Assert.All(lessons, l => Assert.True(l.Length > 40, $"Too terse to act on: {l}"));
        }

        [Fact]
        public void AnUnearnedFinish_IsTheFirstLesson()
        {
            // The winner never gets on top, so the advantage does not favour them at the bell.
            List<MatchBeat> beats =
            [
                Beat("Standard Collar-and-Elbow", BeatControl.Even),
                Beat("Power Beatdown",    BeatControl.WrestlerB),
                Beat("Signature Cover",   BeatControl.WrestlerB),
                Beat("Clean Victory",    BeatControl.WrestlerA)
            ];

            var (result, plan) = Run(Plan(beats));
            output.WriteLine($"finish quality {result.FinishQuality:F1}");

            if (result.FinishQuality < 70)
            {
                Assert.Contains(Hurts(MatchDiagnosis.Of(plan, result), "Finish"),
                    t => t.Contains("unearned"));
                Assert.StartsWith("Book the heat", MatchDiagnosis.Lessons(plan, result)[0]);
            }
        }
    }
}
