using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Tests
{
    public class SegmentSimulatorTests
    {
        private static Segment Promo(Wrestler speaker, bool scripted = true)
        {
            var segment = new Segment("Test Promo", SegmentType.Promo, SegmentLocation.Ring, scripted);
            segment.AddParticipant(speaker);
            segment.AddAction(SegmentActionLibrary.Find("Cut a Promo")!.ToAction(speaker));
            return segment;
        }

        private static Segment Attack(Wrestler attacker, Wrestler victim, string action = "Blindside Attack")
        {
            var segment = new Segment("Test Attack", SegmentType.Brawl, SegmentLocation.Ring, isScripted: true);
            segment.AddParticipant(attacker);
            segment.AddParticipant(victim);
            segment.AddAction(SegmentActionLibrary.Find(action)!.ToAction(attacker, victim));
            return segment;
        }

        // ── Validation ───────────────────────────────────────────────────────

        [Fact]
        public void EmptySegment_IsRejected()
        {
            var segment = new Segment("Nothing", SegmentType.Promo, SegmentLocation.Ring, true);
            segment.AddParticipant(TestRoster.Babyface);

            Assert.Contains(segment.Validate(), e => e.Contains("no actions"));
            Assert.Throws<InvalidOperationException>(() => new SegmentSimulator(1).Simulate(segment));
        }

        [Fact]
        public void PhysicalAction_WithoutTarget_IsRejected()
        {
            var a = TestRoster.Babyface;
            var segment = new Segment("Swing at air", SegmentType.Brawl, SegmentLocation.Ring, true);
            segment.AddParticipant(a);
            segment.AddAction(SegmentActionLibrary.Find("Blindside Attack")!.ToAction(a));

            Assert.Contains(segment.Validate(), e => e.Contains("needs a target"));
        }

        [Fact]
        public void PerformerMustBeAParticipant()
        {
            var a = TestRoster.Babyface;
            var outsider = TestRoster.Make("Nobody");

            var segment = new Segment("Test", SegmentType.Promo, SegmentLocation.Ring, true);
            segment.AddParticipant(a);
            segment.AddAction(SegmentActionLibrary.Find("Cut a Promo")!.ToAction(outsider));

            Assert.Contains(segment.Validate(), e => e.Contains("not a participant"));
        }

        // ── Heat ─────────────────────────────────────────────────────────────

        [Fact]
        public void Segment_GeneratesHeat()
        {
            var result = new SegmentSimulator(7).Simulate(Attack(TestRoster.Heel, TestRoster.Babyface));
            Assert.True(result.HeatGenerated > 0, "A physical segment must deposit feud heat.");
        }

        [Fact]
        public void Betrayal_GeneratesMoreHeatThanAPromo()
        {
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var promoHeat    = new SegmentSimulator(3).Simulate(Promo(a)).HeatGenerated;
            var betrayalHeat = new SegmentSimulator(3).Simulate(Attack(b, a, "Turn on a Partner")).HeatGenerated;

            Assert.True(betrayalHeat > promoHeat,
                $"Betrayal ({betrayalHeat:F1}) should out-heat a promo ({promoHeat:F1}).");
        }

        [Fact]
        public void TemplateHistoryTags_ReachTheResult()
        {
            var segment = SegmentTemplateLibrary.Find("Betrayal")!
                .Create(new[] { TestRoster.Heel, TestRoster.Babyface });

            var result = new SegmentSimulator(11).Simulate(segment);

            Assert.Contains(FeudHistoryTag.Betrayal, result.HistoryTags);
        }

        // ── Botch ────────────────────────────────────────────────────────────

        [Fact]
        public void ScriptedSegment_NeverBotches()
        {
            var speaker = TestRoster.Make("Green Rookie", psychology: 20);

            for (int seed = 0; seed < 400; seed++)
                Assert.False(new SegmentSimulator(seed).Simulate(Promo(speaker, scripted: true)).Botched);
        }

        [Fact]
        public void UnscriptedSingleActionPromo_CanBotch()
        {
            // Regression: the old gate was `improvRisk > 15`, and a one-action promo tops
            // out at 10, so a solo unscripted promo could never botch no matter who cut it.
            var speaker = TestRoster.Make("Green Rookie", psychology: 20);

            int botches = Enumerable.Range(0, 600)
                .Count(seed => new SegmentSimulator(seed).Simulate(Promo(speaker, scripted: false)).Botched);

            Assert.True(botches > 0, "A one-action unscripted promo must be able to botch.");
        }

        [Fact]
        public void HighPsychology_BotchesLessThanLow()
        {
            var green   = TestRoster.Make("Green",   psychology: 20);
            var veteran = TestRoster.Make("Veteran", psychology: 95);

            int greenBotches   = Enumerable.Range(0, 800).Count(s => new SegmentSimulator(s).Simulate(Promo(green,   false)).Botched);
            int veteranBotches = Enumerable.Range(0, 800).Count(s => new SegmentSimulator(s).Simulate(Promo(veteran, false)).Botched);

            Assert.True(greenBotches > veteranBotches,
                $"Psychology should protect an unscripted segment (green {greenBotches}, veteran {veteranBotches}).");
        }

        // ── Overness ─────────────────────────────────────────────────────────

        [Fact]
        public void GoodSegment_RaisesOverness()
        {
            var speaker = TestRoster.Make("Talker", overness: 50, charisma: 5.0);
            double before = speaker.Overness;

            var result = new SegmentSimulator(5).Simulate(Promo(speaker));

            Assert.True(speaker.Overness > before);
            Assert.Contains(result.OvernessChanges, c => c.Wrestler == speaker && c.Delta > 0);
        }

        [Fact]
        public void BotchedSegment_CostsOverness()
        {
            // Regression: overness was clamped to 0.5..3.0 and only ever added, so a promo
            // was free overness however badly it went.
            var speaker = TestRoster.Make("Green Rookie", overness: 50, charisma: 4.0, psychology: 20);

            for (int seed = 0; seed < 600; seed++)
            {
                var subject = TestRoster.Make("Green Rookie", overness: 50, charisma: 4.0, psychology: 20);
                var result  = new SegmentSimulator(seed).Simulate(Promo(subject, scripted: false));

                if (!result.Botched) continue;

                Assert.True(subject.Overness < 50,
                    $"A botched segment should cost overness, got {subject.Overness}.");
                return;
            }

            Assert.Fail("No botch occurred across 600 seeds — cannot verify the overness penalty.");
        }

        [Fact]
        public void Popularity_IsClampedToOneHundred()
        {
            var speaker = TestRoster.Make("Megastar", overness: 99, charisma: 5.0);

            for (int seed = 0; seed < 20; seed++)
                new SegmentSimulator(seed).Simulate(Promo(speaker));

            Assert.True(speaker.Overness <= 100, $"Popularity ran away to {speaker.Overness}.");
        }

        // ── Injury ───────────────────────────────────────────────────────────

        [Fact]
        public void Toughness_ResistsInjury()
        {
            var attacker = TestRoster.Heel;

            int fragileInjuries = Enumerable.Range(0, 1500).Count(s =>
                new SegmentSimulator(s).Simulate(
                    Attack(attacker, TestRoster.Make("Glass", toughness: 0), "Weapon Shot")).Injured != null);

            int toughInjuries = Enumerable.Range(0, 1500).Count(s =>
                new SegmentSimulator(s).Simulate(
                    Attack(attacker, TestRoster.Make("Granite", toughness: 100), "Weapon Shot")).Injured != null);

            Assert.True(fragileInjuries > toughInjuries,
                $"Toughness should reduce injuries (glass {fragileInjuries}, granite {toughInjuries}).");
        }

        [Fact]
        public void Injury_StampsAnInjuryAngle()
        {
            var attacker = TestRoster.Heel;

            for (int seed = 0; seed < 1500; seed++)
            {
                var result = new SegmentSimulator(seed).Simulate(
                    Attack(attacker, TestRoster.Make("Glass", toughness: 0), "Weapon Shot"));

                if (result.Injured == null) continue;

                Assert.Contains(FeudHistoryTag.InjuryAngle, result.HistoryTags);
                return;
            }

            Assert.Fail("No injury occurred across 1500 seeds.");
        }

        // ── Location ─────────────────────────────────────────────────────────

        [Fact]
        public void Location_ChangesAudienceImpact()
        {
            // Previously SegmentLocation was stored, printed, and never scored.
            var speaker = TestRoster.Make("Talker", charisma: 4.0);

            double ImpactAt(SegmentLocation location)
            {
                var segment = new Segment("Test", SegmentType.Promo, location, isScripted: true);
                segment.AddParticipant(speaker);
                segment.AddAction(SegmentActionLibrary.Find("Cut a Promo")!.ToAction(speaker));
                return new SegmentSimulator(1).Simulate(segment).AudienceImpact;
            }

            Assert.True(ImpactAt(SegmentLocation.Crowd) > ImpactAt(SegmentLocation.Ring));
            Assert.True(ImpactAt(SegmentLocation.Ring)  > ImpactAt(SegmentLocation.Backstage));
            Assert.True(ImpactAt(SegmentLocation.Backstage) > ImpactAt(SegmentLocation.GMOffice));
        }

        [Fact]
        public void Charisma_DrivesTalkingSegments()
        {
            double dull    = new SegmentSimulator(2).Simulate(Promo(TestRoster.Make("Dull",    charisma: 0.5))).AudienceImpact;
            double magnetic = new SegmentSimulator(2).Simulate(Promo(TestRoster.Make("Magnetic", charisma: 5.0))).AudienceImpact;

            Assert.True(magnetic > dull);
        }

        [Fact]
        public void Simulate_WritesResultsBackOntoTheSegment()
        {
            var segment = Attack(TestRoster.Heel, TestRoster.Babyface);
            var result  = new SegmentSimulator(9).Simulate(segment);

            Assert.Equal(result.AudienceImpact, segment.AudienceImpact, precision: 6);
            Assert.Equal(result.HeatGenerated,  segment.HeatImpact,     precision: 6);
        }

        [Fact]
        public void Commentary_IsProducedForEveryAction()
        {
            var segment = SegmentTemplateLibrary.Find("Backstage Interview")!
                .Create(new[] { TestRoster.Babyface, TestRoster.Heel });

            var result = new SegmentSimulator(4).Simulate(segment);

            Assert.True(result.Commentary.Count >= segment.Actions.Count);
        }
    }
}
