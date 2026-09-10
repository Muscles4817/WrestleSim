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

        // ── How good it was ─────────────────────────────────────────────────

        /// <summary>
        /// **The score and the stars never disagree.**
        ///
        /// `MatchEngine` defines the star rating as the final score over twenty, and the two
        /// are shown side by side on the show report. Giving the 0–100 view its own thresholds
        /// would let a score painted "very good" sit next to stars painted for a good one, on
        /// the one screen whose job is saying how the show went. The whole range is walked
        /// rather than sampled, because a disagreement at one value is the failure.
        /// </summary>
        [Fact]
        public void TheScoreAndTheStarsAlwaysLandInTheSameBand()
        {
            for (int score = 0; score <= 100; score++)
                Assert.Equal(Grades.OfStars(score / 20.0), Grades.OfScore(score));
        }

        /// <summary>
        /// The bands climb. A better match is never painted as a worse one, which is the only
        /// thing a booker actually reads off the colour.
        /// </summary>
        [Fact]
        public void ABetterMatchIsNeverGradedLower()
        {
            var previous = Grades.OfStars(0);

            for (double stars = 0; stars <= 5.0001; stars += 0.05)
            {
                var grade = Grades.OfStars(stars);
                Assert.True(grade >= previous, $"{stars:F2} stars graded below the rating under it");
                previous = grade;
            }

            output.WriteLine($"0.0 → {Grades.OfStars(0).Label()}, 5.0 → {Grades.OfStars(5).Label()}");
            Assert.Equal(Grade.Poor,    Grades.OfStars(0));
            Assert.Equal(Grade.Classic, Grades.OfStars(5));
        }

        /// <summary>Every band is reached by some rating, or it is a band that does not exist.</summary>
        [Fact]
        public void EveryGradeIsReachable()
        {
            var seen = new HashSet<Grade>();
            for (double stars = 0; stars <= 5.0001; stars += 0.05) seen.Add(Grades.OfStars(stars));

            Assert.Equal(Enum.GetValues<Grade>().Length, seen.Count);
        }

        /// <summary>
        /// **The bottom band is the warning colour, not a faded one.**
        ///
        /// This is where the grade ramp and the overness ramp part company, and the difference
        /// is the point of having two. A wrestler with a low reading is an enhancement talent
        /// doing the job they are on the card to do; a match with a low reading is a mistake
        /// the booker made, and the screen should say so.
        /// </summary>
        [Fact]
        public void APoorMatchIsNotJustAQuietOne()
        {
            Assert.Equal("grade--poor", Grade.Poor.Tone());
            Assert.NotEqual(CardPosition.Enhancement.Tone(), Grade.Poor.Tone());

            foreach (Grade g in Enum.GetValues<Grade>())
            {
                Assert.StartsWith("grade--", g.Tone());
                Assert.False(string.IsNullOrWhiteSpace(g.Label()));
            }
        }

        /// <summary>Every grade has its own colour, or two of them are one band.</summary>
        [Fact]
        public void NoTwoGradesShareATone()
        {
            var tones = Enum.GetValues<Grade>().Select(g => g.Tone()).ToList();

            Assert.Equal(tones.Count, tones.Distinct().Count());
        }

        // ── The rest of the palette ─────────────────────────────────────────

        /// <summary>
        /// Beat intensity climbs through four distinct colours. A beat sheet is a shape, and
        /// the shape is what the intensities do down the page.
        /// </summary>
        [Fact]
        public void EveryBeatIntensityIsItsOwnColour()
        {
            var tones = Enum.GetValues<BeatIntensity>().Select(i => i.Tone()).ToList();

            Assert.Equal(tones.Count, tones.Distinct().Count());
            Assert.All(tones, t => Assert.StartsWith("heat--", t));
        }

        /// <summary>
        /// **Only three suggestion bands are painted.**
        ///
        /// A story is the reason to book somebody, a worn-out pairing is the reason not to,
        /// and a name already in the match is not a candidate. The rest are the ordinary case,
        /// and this app has already learned what happens when the ordinary case gets a colour:
        /// twenty-six amber notices doing four different jobs, and amber meaning nothing.
        /// </summary>
        [Fact]
        public void OnlyTheBandsWorthActingOnArePainted()
        {
            var painted = Enum.GetValues<BookingSuggestions.SuggestionBand>()
                              .Where(b => BookingSuggestions.Tone(b).Length > 0)
                              .ToList();

            output.WriteLine(string.Join(", ", painted));

            Assert.Equal(3, painted.Count);
            Assert.Contains(BookingSuggestions.SuggestionBand.Story,   painted);
            Assert.Contains(BookingSuggestions.SuggestionBand.WornOut, painted);
            Assert.Contains(BookingSuggestions.SuggestionBand.Booked,  painted);
        }

        /// <summary>
        /// A feud at nuclear is painted as a warning rather than as a prize. Doc 20 §6 has it
        /// as the state with a clock on it — something to be paid off soon, not sat in.
        /// </summary>
        [Fact]
        public void NuclearHeatReadsAsAWarning()
        {
            Assert.Equal("badge--heel", FeudIntensity.Nuclear.Badge());
            Assert.Equal("badge--gold", FeudIntensity.Hot.Badge());
            Assert.NotEqual(FeudIntensity.Hot.Badge(), FeudIntensity.Building.Badge());
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
