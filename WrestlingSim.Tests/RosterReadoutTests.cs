using Xunit;
using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **What the roster list says in colour.**
    ///
    /// Every overness reading in the picker was the same grey, every alignment invisible and
    /// every division indistinguishable, so a seventy-name list asked the booker to compare
    /// digits. The rules that decide what gets painted live here rather than in the razor,
    /// for the same reason `BookingSuggestions` does: a component cannot be tested and these
    /// are decisions rather than layout.
    /// </summary>
    public class RosterReadoutTests(ITestOutputHelper output)
    {
        private static Wrestler W(Division division = Division.Womens, double overness = 50)
        {
            var w = TestRoster.Make($"Pick {division} {overness}");
            w.Division = division;
            w.Overness = overness;
            return w;
        }

        // ── The division a picker opens on ───────────────────────────────────

        /// <summary>
        /// Nobody booked means the whole roster. There is nothing to match yet, and guessing
        /// would hide half the roster from the very first choice of the match.
        /// </summary>
        [Fact]
        public void AnEmptyLineupOpensOnEverybody()
        {
            Assert.Null(RosterFilters.DivisionFor([]));
        }

        /// <summary>
        /// One name booked, and the next picker opens on their division. This is the case the
        /// whole thing exists for: a booker who has picked one woman is picking the second
        /// from the women's division roughly always, and making them say so once per slot is
        /// asking a question whose answer is already on the screen.
        /// </summary>
        [Theory]
        [InlineData(Division.Womens)]
        [InlineData(Division.Mens)]
        public void OneNameBookedSetsTheFilter(Division division)
        {
            Assert.Equal(division, RosterFilters.DivisionFor([W(division)]));
        }

        /// <summary>And it stays set while everybody booked agrees.</summary>
        [Fact]
        public void AFullSideOfOneDivisionKeepsTheFilter()
        {
            var booked = new[] { W(Division.Mens), W(Division.Mens), W(Division.Mens) };

            Assert.Equal(Division.Mens, RosterFilters.DivisionFor(booked));
        }

        /// <summary>
        /// **And an intergender match already on the board opens on everybody.**
        ///
        /// The rule that makes this a default rather than a decision. Once a booker has put a
        /// man and a woman in the same match, taking the first name's division and filtering
        /// to it would hide half of what they have visibly already chosen — the picker would
        /// be arguing with a booking that has been made.
        /// </summary>
        [Fact]
        public void AMixedLineupOpensOnEverybody()
        {
            var booked = new[] { W(Division.Womens), W(Division.Mens) };

            Assert.Null(RosterFilters.DivisionFor(booked));
            // And in either order: it is a disagreement, not a position in the list.
            Assert.Null(RosterFilters.DivisionFor(booked.Reverse()));
        }

        /// <summary>
        /// The disagreement is found however deep in the lineup it sits. A trios against a
        /// trios is six names, and the odd one out can be the last of them.
        /// </summary>
        [Fact]
        public void TheOddOneOutIsFoundAnywhereInTheLineup()
        {
            var booked = new List<Wrestler>();
            for (int i = 0; i < 5; i++) booked.Add(W(Division.Womens, 40 + i));
            booked.Add(W(Division.Mens));

            Assert.Null(RosterFilters.DivisionFor(booked));
        }

        // ── The colour of a reading ──────────────────────────────────────────

        /// <summary>
        /// **The overness colour follows the card position, and does not have its own
        /// thresholds.**
        ///
        /// A second set of cut-offs would drift from the first, and then the group heading
        /// would say "Midcard" over a number painted main-event gold. The test walks the
        /// whole 0–100 range rather than sampling it, because a disagreement at one value is
        /// the entire failure.
        /// </summary>
        [Fact]
        public void TheColourAgreesWithTheHeadingAtEveryOverness()
        {
            var tones = new Dictionary<string, CardPosition>();

            for (int overness = 0; overness <= 100; overness++)
            {
                var w = W(overness: overness);
                string tone = w.CardPosition.Tone();

                // The same tone never appears against two different card positions.
                if (tones.TryGetValue(tone, out var already))
                    Assert.Equal(already, w.CardPosition);
                else
                    tones[tone] = w.CardPosition;
            }

            output.WriteLine(string.Join("\n",
                tones.Select(t => $"  {t.Value.Label(),-12} {t.Key}")));

            // Five bands, five tones. A shared tone would collapse two of them.
            Assert.Equal(Enum.GetValues<CardPosition>().Length, tones.Count);
        }

        /// <summary>Every card position has a tone, including any added later.</summary>
        [Fact]
        public void EveryCardPositionIsPainted()
        {
            foreach (CardPosition position in Enum.GetValues<CardPosition>())
                Assert.StartsWith("pop--", position.Tone());
        }

        /// <summary>
        /// Face and heel take the two colours the palette carries for them; a tweener takes
        /// neither. Painting the undecided one would say there are three kinds of alignment
        /// to weigh, and there are two and an undecided.
        /// </summary>
        [Fact]
        public void FaceAndHeelAreTheOnlyAlignmentsWithAColour()
        {
            Assert.Equal("badge--face", Alignment.Face.Badge());
            Assert.Equal("badge--heel", Alignment.Heel.Badge());
            Assert.Equal("badge--muted", Alignment.Tweener.Badge());
        }

        /// <summary>
        /// **Colour is never the only channel.** Every badge carries a word, so the list still
        /// works for a reader who cannot tell the red from the green — which is the pair of
        /// hues most likely to be one hue.
        /// </summary>
        [Fact]
        public void EveryBadgeSaysItInWordsAsWellAsInColour()
        {
            foreach (Alignment a in Enum.GetValues<Alignment>())
                Assert.False(string.IsNullOrWhiteSpace(a.Label()));

            foreach (Division d in Enum.GetValues<Division>())
                Assert.False(string.IsNullOrWhiteSpace(d.Label()));
        }

        /// <summary>And the two divisions are told apart, which is the whole ask.</summary>
        [Fact]
        public void TheDivisionsDoNotShareALabelOrAColour()
        {
            Assert.NotEqual(Division.Womens.Label(), Division.Mens.Label());
            Assert.NotEqual(Division.Womens.Badge(), Division.Mens.Badge());
        }
    }
}
