using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **Judged on moments, not on work.**
    ///
    /// Doc 18 §2.5: "The battle royal / Rumble. Over-the-top elimination. Everyone starts
    /// together in a battle royal; timed continuous entry is the Rumble variant, and it is
    /// the entries that make the Rumble a story rather than a scramble. Barely a match: a
    /// vehicle for a spectacle, a surprise return, and one story told in eliminations.
    /// Judged on moments, not on work."
    ///
    /// This was twice written down as not built, on the grounds that it is a design problem
    /// before a code problem. That held up on inspection: `BeatControl` names four sides and
    /// `MatchPlan.Validate` caps a plan at four, so a thirty-person field cannot be booked
    /// beat by beat at all — and the match engine's entire scoring model measures how well a
    /// match was *worked*, which doc 18 says explicitly is not how this one is judged.
    ///
    /// So it has its own plan, its own engine and its own scoring, and the tests below are
    /// mostly about whether doc 18's sentences came out as rules or as decoration.
    /// </summary>
    public class RumbleTests(ITestOutputHelper output)
    {
        private static readonly int Seed = StableSeed.From("rumble");

        private static Wrestler W(string n, int over = 70) =>
            TestRoster.Make(n, overness: over, charisma: 3.0, skill: 3.0);

        /// <summary>A Rumble of <paramref name="n"/>, entering in order, W1 winning.</summary>
        private static RumblePlan Rumble(int n, int winnerNumber = 1, int interval = 90)
        {
            var field = Enumerable.Range(1, n)
                .Select(i => new RumbleEntrant { Wrestler = W($"W{i}"), Number = i })
                .ToList();

            return new RumblePlan
            {
                Field = field,
                EntryIntervalSeconds = interval,
                Winner = field.Single(e => e.Number == winnerNumber).Wrestler
            };
        }

        // ── The scoring model ────────────────────────────────────────────────

        /// <summary>
        /// **Repeating a moment wears it out, and the cheap ones wear out fastest.**
        ///
        /// The rule that stops the format being gamed by booking filler: a second surprise
        /// return is still a surprise, a fourth near-elimination is the crowd waiting for it
        /// to end. If the decay went the other way a booker could pad a Rumble to five stars
        /// with hanging-on spots, which is the exact opposite of what the format is for.
        /// </summary>
        [Fact]
        public void RepeatingAMomentWearsItOut_AndTheCheapOnesFastest()
        {
            double oneSurprise  = RumbleScoring.MomentScore([RumbleMomentKind.SurpriseReturn]);
            double twoSurprises = RumbleScoring.MomentScore(
                [RumbleMomentKind.SurpriseReturn, RumbleMomentKind.SurpriseReturn]);

            double oneNearFall  = RumbleScoring.MomentScore([RumbleMomentKind.NearElimination]);
            double fourNearFalls = RumbleScoring.MomentScore(
                Enumerable.Repeat(RumbleMomentKind.NearElimination, 4));

            output.WriteLine($"  surprise x1 {oneSurprise:F3} -> x2 {twoSurprises:F3}");
            output.WriteLine($"  near-out x1 {oneNearFall:F3} -> x4 {fourNearFalls:F3}");

            // A second of anything is worth something, just less.
            Assert.True(twoSurprises > oneSurprise);
            Assert.True(twoSurprises < oneSurprise * 2);

            // Four cheap spots are worth less than two good ones.
            Assert.True(fourNearFalls < twoSurprises,
                $"four near-eliminations ({fourNearFalls:F3}) out-scored two surprise " +
                $"returns ({twoSurprises:F3}) — the decay is the wrong way round");
        }

        /// <summary>
        /// **A varied card of moments beats the same moment over and over**, at equal count.
        /// </summary>
        [Fact]
        public void VarietyBeatsRepetition()
        {
            double varied = RumbleScoring.MomentScore(
            [
                RumbleMomentKind.SurpriseReturn, RumbleMomentKind.Showdown,
                RumbleMomentKind.Betrayal,       RumbleMomentKind.MassElimination
            ]);

            double same = RumbleScoring.MomentScore(
                Enumerable.Repeat(RumbleMomentKind.Showdown, 4));

            output.WriteLine($"  four different {varied:F3} vs four showdowns {same:F3}");
            Assert.True(varied > same * 1.3);
        }

        /// <summary>
        /// **The entry number is the story.** Going the distance from two is the format's
        /// signature achievement; winning from twenty-nine is a coronation.
        /// </summary>
        [Theory]
        [InlineData(1,  30, 1.00)]
        [InlineData(30, 30, 0.00)]
        [InlineData(15, 30, 0.52)]
        public void WinningFromEarlyIsABiggerStory(int number, int field, double expected) =>
            Assert.Equal(expected, RumbleScoring.EntryStory(number, field, battleRoyal: false), 1);

        /// <summary>
        /// **And a battle royal has no entry story at all** — not as a penalty, but because
        /// everybody started together and there is no number to have a story about. This is
        /// doc 18's "it is the entries that make the Rumble a story rather than a scramble"
        /// as a number rather than a sentence.
        /// </summary>
        [Fact]
        public void ABattleRoyalHasNoEntryStory() =>
            Assert.Equal(0.0, RumbleScoring.EntryStory(1, 30, battleRoyal: true));

        /// <summary>
        /// **The same field scores lower as a battle royal than as a Rumble**, which is the
        /// claim above made end to end rather than in one function. If this fails, doc 18's
        /// sentence about entries is decoration in this codebase.
        /// </summary>
        [Fact]
        public void TheSameFieldScoresLowerAsABattleRoyal()
        {
            var rumble = Rumble(20);
            var royal  = Rumble(20, interval: 0);

            var a = new RumbleEngine(Seed).Execute(rumble);
            var b = new RumbleEngine(Seed).Execute(royal);

            output.WriteLine($"  rumble {a.FinalScore:F1} ({a.StarRating:F2}★) " +
                             $"vs battle royal {b.FinalScore:F1} ({b.StarRating:F2}★)");

            Assert.True(royal.IsBattleRoyal);
            Assert.True(a.FinalScore > b.FinalScore,
                "the entries bought nothing — a battle royal scored the same as a Rumble");
        }

        /// <summary>
        /// **No technical component, and that is the point.** The match engine grades how
        /// well a match was worked; doc 18 says this format is not judged that way, and a
        /// score with a work term in it would be answering a different question and printing
        /// the number anyway.
        /// </summary>
        [Fact]
        public void TheScoreIsMomentsFieldAndStory_WithNoWorkTermAtAll()
        {
            var r = new RumbleEngine(Seed).Execute(Rumble(20));

            foreach (var (label, points) in r.Ordered)
                output.WriteLine($"  {label,-12} {points:F1}");

            var labels = r.Ordered.Select(x => x.Label).ToList();
            Assert.DoesNotContain("Technical", labels);

            // And the parts add up to the whole — no hidden fifth term.
            Assert.Equal(r.FinalScore, r.Ordered.Sum(x => x.Points), 3);
        }

        // ── The match it runs ────────────────────────────────────────────────

        /// <summary>
        /// **Everybody goes out except the winner, once each, and the ring empties.**
        /// </summary>
        [Theory]
        [InlineData(6)]
        [InlineData(20)]
        [InlineData(30)]
        public void EverybodyGoesOutExceptTheWinner(int size)
        {
            var plan = Rumble(size, winnerNumber: 3);
            var r = new RumbleEngine(Seed).Execute(plan);

            output.WriteLine($"  {size}: winner {r.Winner.RingName}, " +
                             $"{r.Eliminations.Count} out, iron man {r.IronMan?.RingName} " +
                             $"({r.IronManOutlasted} outlasted), {r.StarRating:F2}★");

            Assert.Equal(size - 1, r.Eliminations.Count);
            Assert.Equal(size - 1, r.Eliminations.Select(e => e.Wrestler).Distinct().Count());
            Assert.DoesNotContain(r.Eliminations, e => e.Wrestler == r.Winner);

            // The count runs down to one, and nobody eliminates themselves.
            Assert.Equal(1, r.Eliminations.Last().Remaining);
            Assert.All(r.Eliminations, e => Assert.NotEqual(e.Wrestler, e.By));
        }

        /// <summary>
        /// **The crowd's favourites are less likely to be the next one out.**
        ///
        /// Asserted on the rule itself, because the end-to-end version of this could not
        /// tell the rule from the running order: it passed with the weighting replaced by a
        /// bare random roll, since entry position decides who is even in the ring to be
        /// picked. Fourth time in a day, and the same fix each time — extract the mechanism
        /// rather than infer it.
        /// </summary>
        [Fact]
        public void TheCrowdsFavouritesAreLessLikelyToGoOut()
        {
            double star   = RumbleScoring.EliminationRisk(0.95);
            double middle = RumbleScoring.EliminationRisk(0.55);
            double nobody = RumbleScoring.EliminationRisk(0.15);

            output.WriteLine($"  risk — star {star:F3}, midcard {middle:F3}, nobody {nobody:F3}");

            Assert.True(nobody > middle, "a nobody is no likelier to go out than a midcarder");
            Assert.True(middle > star,   "a midcarder is no likelier to go out than a star");

            // And the gap is worth something — a rule that orders correctly by a thousandth
            // would be swamped by the roll the engine adds on top.
            Assert.True(nobody - star > 0.3,
                $"the spread from {star:F3} to {nobody:F3} is too small to survive the roll");
        }

        /// <summary>
        /// And it shows up in the match, swept across seeds rather than trusted on one. This
        /// is the weaker claim on purpose: it cannot separate the weighting from the entry
        /// order, so it is a sanity check on top of the rule above rather than the test of it.
        /// </summary>
        [Fact]
        public void TheBiggestNamesLastLongest()
        {
            var stars = Enumerable.Range(1, 5).Select(i => W($"Star{i}", over: 95)).ToList();
            var jobbers = Enumerable.Range(1, 15).Select(i => W($"Jobber{i}", over: 25)).ToList();

            // Interleaved, not stars-then-jobbers. Entry order genuinely decides who is even
            // available to be eliminated, so clustering the stars at the front would measure
            // that rather than connection — which is how the first version of this test came
            // to report the opposite of the truth.
            var field = jobbers.Take(3)
                .Concat(stars.Take(2)).Concat(jobbers.Skip(3).Take(6))
                .Concat(stars.Skip(2)).Concat(jobbers.Skip(9))
                .Select((w, i) => new RumbleEntrant { Wrestler = w, Number = i + 1 })
                .ToList();

            var plan = new RumblePlan { Field = field, Winner = stars[0] };

            double starsAt = 0, jobbersAt = 0;
            const int seeds = 30;
            for (int s = 0; s < seeds; s++)
            {
                var r = new RumbleEngine(s).Execute(plan);
                starsAt += r.Eliminations.Where(e => e.Wrestler.RingName.StartsWith("Star"))
                                         .Average(e => (double)e.Order);
                jobbersAt += r.Eliminations.Where(e => e.Wrestler.RingName.StartsWith("Jobber"))
                                           .Average(e => (double)e.Order);
            }

            output.WriteLine($"  over {seeds} seeds: stars go out around " +
                             $"#{starsAt / seeds:F1}, jobbers around #{jobbersAt / seeds:F1}");
            Assert.True(starsAt > jobbersAt,
                "the people the crowd came for went out first");
        }

        /// <summary>Booked moments land, and are reported as highlights.</summary>
        [Fact]
        public void BookedMomentsLandAndAreReported()
        {
            var plan = Rumble(12);
            var cast = plan.Field.Select(e => e.Wrestler).ToList();

            plan.Moments =
            [
                new() { Kind = RumbleMomentKind.SurpriseReturn, Cast = [cast[7]],          At = 0.3 },
                new() { Kind = RumbleMomentKind.Showdown,       Cast = [cast[0], cast[1]], At = 0.6 },
                new() { Kind = RumbleMomentKind.Betrayal,       Cast = [cast[2], cast[3]], At = 0.8 }
            ];

            var r = new RumbleEngine(Seed).Execute(plan);
            foreach (var h in r.Highlights) output.WriteLine($"  {h}");

            Assert.Equal(3, r.Highlights.Count);
            Assert.Contains(r.Highlights, h => h.Contains("BACK"));
            Assert.Contains(r.Highlights, h => h.Contains("everything else in this ring has stopped"));
            Assert.Contains(r.Highlights, h => h.Contains("Their own partner"));
        }

        /// <summary>
        /// And booking moments is worth something — a Rumble with three of them beats the
        /// same field with none, or the vocabulary is decoration.
        /// </summary>
        [Fact]
        public void BookingMomentsIsWorthSomething()
        {
            var bare = Rumble(20);
            var withMoments = Rumble(20);
            var cast = withMoments.Field.Select(e => e.Wrestler).ToList();
            withMoments.Moments =
            [
                new() { Kind = RumbleMomentKind.SurpriseReturn,  Cast = [cast[9]],          At = 0.3 },
                new() { Kind = RumbleMomentKind.Showdown,        Cast = [cast[0], cast[1]], At = 0.6 },
                new() { Kind = RumbleMomentKind.MassElimination, Cast = [cast[2]],          At = 0.8 }
            ];

            var a = new RumbleEngine(Seed).Execute(bare);
            var b = new RumbleEngine(Seed).Execute(withMoments);

            output.WriteLine($"  bare {a.StarRating:F2}★ vs three moments {b.StarRating:F2}★");
            Assert.True(b.FinalScore > a.FinalScore + 5);
        }

        // ── Validation ───────────────────────────────────────────────────────

        [Fact]
        public void AFieldTooSmallToBeABattleRoyal_IsRefused()
        {
            var plan = Rumble(3);
            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("needs a field"));
        }

        [Fact]
        public void AWinnerWhoIsNotInIt_IsRefused()
        {
            var plan = Rumble(10);
            plan.Winner = W("Nobody");
            Assert.Contains(plan.Validate(), e => e.Contains("without being in it"));
        }

        [Fact]
        public void DuplicateEntryNumbers_AreRefused()
        {
            var plan = Rumble(10);
            plan.Field[3] = new RumbleEntrant { Wrestler = plan.Field[3].Wrestler, Number = 1 };
            Assert.Contains(plan.Validate(), e => e.Contains("same entry number"));
        }

        [Fact]
        public void AMomentInvolvingSomebodyNotInIt_IsRefused()
        {
            var plan = Rumble(10);
            plan.Moments = [new() { Kind = RumbleMomentKind.SurpriseReturn, Cast = [W("Ghost")] }];
            Assert.Contains(plan.Validate(), e => e.Contains("who is not in the match"));
        }

        /// <summary>Runtime scales with the field, because that is what the format costs a card.</summary>
        [Fact]
        public void ARumbleCostsACardRealTime()
        {
            output.WriteLine($"  30-strong rumble: {Rumble(30).DurationMinutes} min");
            output.WriteLine($"  30-strong royal : {Rumble(30, interval: 0).DurationMinutes} min");

            Assert.True(Rumble(30).DurationMinutes > Rumble(10).DurationMinutes);
            Assert.True(Rumble(30).DurationMinutes > Rumble(30, interval: 0).DurationMinutes,
                "a battle royal should be shorter than a rumble of the same size — there is " +
                "nothing to wait for");
        }
    }
}
