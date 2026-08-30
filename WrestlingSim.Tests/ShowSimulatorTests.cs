using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Tests
{
    public class ShowSimulatorTests
    {
        private static BookedMatch Match(Wrestler a, Wrestler b, string structure = "TV Formula") =>
            new BookedMatch
            {
                Plan = new MatchPlan
                {
                    WrestlerA = a,
                    WrestlerB = b,
                    Beats     = MatchStructureLibrary.Find(structure)!.Beats.Select(x => x.Clone()).ToList()
                },
                StructureName = structure
            };

        private static Segment Promo(Wrestler speaker)
        {
            var segment = new Segment($"{speaker.RingName} Promo", SegmentType.Promo, SegmentLocation.Ring, true);
            segment.AddParticipant(speaker);
            segment.AddAction(SegmentActionLibrary.Find("Cut a Promo")!.ToAction(speaker));
            return segment;
        }

        private static Show ShowOf(params ICardItem[] items) =>
            new Show { Name = "Test Show", Card = items.ToList(), TotalDurationMinutes = 180 };

        // ── Fatigue ──────────────────────────────────────────────────────────

        [Fact]
        public void SecondItemOfTheSameKind_TakesTheFatiguePenalty()
        {
            // Regression: the rule was `i > 1`, so the second item on the card always
            // escaped it however identical it was to the first.
            var show = ShowOf(Promo(TestRoster.Babyface), Promo(TestRoster.Heel));

            var result = new ShowSimulator(new FeudBook(), seed: 1).Simulate(show);

            Assert.False(result.Items[0].FatiguePenaltyApplied);
            Assert.True(result.Items[1].FatiguePenaltyApplied);
        }

        [Fact]
        public void AlternatingCard_TakesNoFatiguePenalty()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var show = ShowOf(Promo(a), Match(a, b), Promo(b), Match(b, a));
            var result = new ShowSimulator(new FeudBook(), seed: 1).Simulate(show);

            Assert.All(result.Items, i => Assert.False(i.FatiguePenaltyApplied));
        }

        // ── Runtime budget ───────────────────────────────────────────────────

        [Fact]
        public void CardWithinBudget_TakesNoOverrunPenalty()
        {
            var show = ShowOf(Match(TestRoster.Babyface, TestRoster.Heel));
            show.TotalDurationMinutes = 180;

            var result = new ShowSimulator(new FeudBook(), seed: 2).Simulate(show);

            Assert.False(show.IsOverrunning);
            Assert.Equal(0, result.OverrunPenalty);
        }

        [Fact]
        public void OverrunningCard_IsPenalised()
        {
            // TotalDurationMinutes was previously set and never read anywhere.
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var show = ShowOf(Match(a, b), Promo(a), Match(b, a));
            show.TotalDurationMinutes = 10; // deliberately far too short

            var result = new ShowSimulator(new FeudBook(), seed: 2).Simulate(show);

            Assert.True(show.IsOverrunning);
            Assert.True(result.OverrunPenalty > 0);
            Assert.True(result.OverrunPenalty <= 0.35);
        }

        [Fact]
        public void BookedMinutes_IsTheSumOfTheCard()
        {
            var show = ShowOf(Match(TestRoster.Babyface, TestRoster.Heel), Promo(TestRoster.Heel));
            Assert.Equal(show.Card.Sum(i => i.DurationMinutes), show.BookedMinutes);
            Assert.True(show.BookedMinutes > 0);
        }

        // ── Position ─────────────────────────────────────────────────────────

        [Fact]
        public void OpenerAndMainEvent_AreWeighted()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var show = ShowOf(Match(a, b), Promo(a), Match(b, a));
            var result = new ShowSimulator(new FeudBook(), seed: 3).Simulate(show);

            Assert.Equal(1.2, result.Items[0].PositionWeight);
            Assert.Equal(1.0, result.Items[1].PositionWeight);
            Assert.Equal(1.5, result.Items[2].PositionWeight);
        }

        // ── Feud plumbing ────────────────────────────────────────────────────

        [Fact]
        public void MatchesAndSegments_BothDepositHeat()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;
            var book = new FeudBook();

            var beatdown = SegmentTemplateLibrary.Find("Post-Match Beatdown")!.Create(new[] { a, b });
            var show = ShowOf(Match(a, b), beatdown);

            var result = new ShowSimulator(book, seed: 4).Simulate(show);

            Assert.NotEmpty(result.FeudUpdates);

            var feud = book.Find(a, b);
            Assert.NotNull(feud);
            Assert.True(feud!.Heat > 0);
            Assert.True(feud.HasTag(FeudHistoryTag.PriorMatch));
            Assert.Equal(1, feud.MatchCount);
        }

        [Fact]
        public void ShowResult_CarriesFullEngineOutput()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var result = new ShowSimulator(new FeudBook(), seed: 5)
                .Simulate(ShowOf(Match(a, b), Promo(a)));

            Assert.NotNull(result.Items[0].MatchResult);
            Assert.NotNull(result.Items[0].StarRating);
            Assert.NotNull(result.Items[1].SegmentResult);
            Assert.InRange(result.OverallRating, 0, 100);
            Assert.InRange(result.FinalCrowdMood, 0, 10);
        }

        [Fact]
        public void EmptyCard_ScoresZeroRatherThanThrowing()
        {
            var result = new ShowSimulator(new FeudBook(), seed: 6).Simulate(ShowOf());
            Assert.Equal(0, result.OverallRating);
        }

        // ── End-to-end loop ──────────────────────────────────────────────────

        [Fact]
        public void BookedSegments_UnlockFeudGatedBeats()
        {
            // The whole point of the branch: you should be able to *earn* a feud by
            // booking segments rather than declaring one in a menu.
            var a = TestRoster.Make("Babyface Bill", popularity: 80, charisma: 4.0);
            var b = TestRoster.Make("Heel Harry",    popularity: 75, charisma: 3.5);
            var book = new FeudBook();

            List<MatchBeat> FeudMatch() =>
            [
                BeatLibrary.Find("Hot Start")!.ToMatchBeat(BeatControl.Even),
                BeatLibrary.Find("Feud Erupts")!.ToMatchBeat(BeatControl.Even),
                BeatLibrary.Find("Clean Victory")!.ToMatchBeat(BeatControl.WrestlerA),
            ];

            // Before any booking, the feud-gated beat is illegal.
            var cold = new MatchPlan { WrestlerA = a, WrestlerB = b, Beats = FeudMatch(), Feud = book.Find(a, b) };
            Assert.Contains(cold.Validate(), e => e.Contains("FeudalEscalation"));
            Assert.DoesNotContain(BeatLibrary.Available(book.Find(a, b)), t => t.Name == "Feud Erupts");

            // Book three angles between them.
            foreach (var name in new[] { "Betrayal", "Post-Match Beatdown", "Face-to-Face Confrontation" })
            {
                var segment = SegmentTemplateLibrary.Find(name)!.Create(new[] { b, a });
                var segResult = new SegmentSimulator(21).Simulate(segment);
                book.RecordSegment(segment.Participants, segResult.HeatGenerated, segResult.HistoryTags);
            }

            var feud = book.Find(a, b);
            Assert.NotNull(feud);
            Assert.True(feud!.Intensity >= FeudIntensity.Building,
                $"Three angles should reach Building, got {feud.Intensity} on {feud.Heat:F1} heat.");
            Assert.True(feud.HasTag(FeudHistoryTag.Betrayal));

            // Now the same plan is legal and the beat is offered in the editor.
            var hot = new MatchPlan { WrestlerA = a, WrestlerB = b, Beats = FeudMatch(), Feud = feud };
            Assert.Empty(hot.Validate());
            Assert.Contains(BeatLibrary.Available(feud), t => t.Name == "Feud Erupts");

            // And the earned feud raises the crowd's starting energy.
            Assert.True(feud.StartingEnergyBonus > 0);
        }

        [Fact]
        public void EarnedFeud_ImprovesTheMatchItPaysOff()
        {
            var a = TestRoster.Make("Babyface Bill", popularity: 80, charisma: 4.0);
            var b = TestRoster.Make("Heel Harry",    popularity: 75, charisma: 3.5);

            var book = new FeudBook();
            for (int i = 0; i < 4; i++)
            {
                var segment = SegmentTemplateLibrary.Find("Betrayal")!.Create(new[] { b, a });
                var segResult = new SegmentSimulator(30 + i).Simulate(segment);
                book.RecordSegment(segment.Participants, segResult.HeatGenerated, segResult.HistoryTags);
            }

            var feud = book.Find(a, b)!;

            double WithFeud(Feud? f) => Enumerable.Range(0, 80).Average(seed =>
                new MatchEngine(seed).Execute(new MatchPlan
                {
                    WrestlerA = a,
                    WrestlerB = b,
                    Feud      = f,
                    Beats     = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList()
                }).StarRating);

            Assert.True(WithFeud(feud) > WithFeud(null),
                "A feud built through segments should make the blowoff match rate higher.");
        }
    }
}
