using WrestlingSim.Engine;
using WrestlingSim.Enums;

namespace WrestlingSim.Tests
{
    public class FeudBookTests
    {
        [Fact]
        public void NewPair_HasNoFeud()
        {
            var book = new FeudBook();
            Assert.Null(book.Find(TestRoster.Babyface, TestRoster.Heel));
            Assert.Empty(book.All);
        }

        [Fact]
        public void Feud_IsFoundRegardlessOfArgumentOrder()
        {
            var book = new FeudBook();
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            book.Record(a, b, heat: 10);

            Assert.NotNull(book.Find(a, b));
            Assert.Same(book.Find(a, b), book.Find(b, a));
        }

        [Theory]
        [InlineData(2,  FeudIntensity.None)]
        [InlineData(5,  FeudIntensity.Cold)]
        [InlineData(15, FeudIntensity.Building)]
        [InlineData(30, FeudIntensity.Hot)]
        [InlineData(50, FeudIntensity.Nuclear)]
        [InlineData(99, FeudIntensity.Nuclear)]
        public void Heat_DerivesIntensity(double heat, FeudIntensity expected)
        {
            var book = new FeudBook();
            var update = book.Record(TestRoster.Babyface, TestRoster.Heel, heat);

            Assert.Equal(expected, update.Feud.Intensity);
        }

        [Fact]
        public void Heat_AccumulatesAcrossSegments()
        {
            var book = new FeudBook();
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            book.Record(a, b, heat: 6);
            book.Record(a, b, heat: 6);
            var third = book.Record(a, b, heat: 6);

            Assert.Equal(18, third.Feud.Heat, precision: 3);
            Assert.Equal(FeudIntensity.Building, third.Feud.Intensity);
        }

        [Fact]
        public void CrossingATier_ReportsEscalation()
        {
            var book = new FeudBook();
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var first  = book.Record(a, b, heat: 6);   // None -> Cold
            var second = book.Record(a, b, heat: 1);   // still Cold
            var third  = book.Record(a, b, heat: 10);  // Cold -> Building

            Assert.True(first.Escalated);
            Assert.False(second.Escalated);
            Assert.True(third.Escalated);
            Assert.Equal(FeudIntensity.Cold, third.PreviousLevel);
        }

        [Fact]
        public void Tags_AreStampedOnceAndReportedOnce()
        {
            var book = new FeudBook();
            var a = TestRoster.Babyface;
            var b = TestRoster.Heel;

            var first  = book.Record(a, b, 5, new[] { FeudHistoryTag.Betrayal });
            var second = book.Record(a, b, 5, new[] { FeudHistoryTag.Betrayal });

            Assert.Contains(FeudHistoryTag.Betrayal, first.NewTags);
            Assert.Empty(second.NewTags);
            Assert.Single(second.Feud.History);
            Assert.True(second.Feud.HasTag(FeudHistoryTag.Betrayal));
        }

        [Fact]
        public void RecordSegment_SplitsHeatAcrossEveryPairing()
        {
            var book = new FeudBook();
            var a = TestRoster.Make("A");
            var b = TestRoster.Make("B");
            var c = TestRoster.Make("C");

            var updates = book.RecordSegment(new[] { a, b, c }, heat: 12);

            // Three pairings, so a six-man does not generate triple the heat of a two-hander.
            Assert.Equal(3, updates.Count);
            Assert.All(updates, u => Assert.Equal(4, u.HeatAdded, precision: 3));

            double total = new[] { (a, b), (a, c), (b, c) }
                .Sum(pair => book.Find(pair.Item1, pair.Item2)!.Heat);
            Assert.Equal(12, total, precision: 3);
        }

        [Fact]
        public void RecordSegment_NeedsTwoParticipants()
        {
            var book = new FeudBook();
            Assert.Empty(book.RecordSegment(new[] { TestRoster.Babyface }, heat: 10));
        }

        [Fact]
        public void DormantFeuds_AreNotListedAsActive()
        {
            var book = new FeudBook();
            book.GetOrCreate(TestRoster.Babyface, TestRoster.Heel);

            Assert.Empty(book.All);
        }

        [Fact]
        public void HandDeclaredFeud_TopsUpHeatToMatchIntensity()
        {
            var book = new FeudBook();
            var feud = book.GetOrCreate(TestRoster.Babyface, TestRoster.Heel);

            feud.SetMinimumIntensity(FeudIntensity.Hot);

            Assert.Equal(FeudIntensity.Hot, feud.Intensity);
            Assert.True(feud.Heat >= 30);

            // Further booking builds on top rather than starting from zero.
            feud.AddHeat(25);
            Assert.Equal(FeudIntensity.Nuclear, feud.Intensity);
        }
    }
}
