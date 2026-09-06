using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The play-by-play of a three-way is about the three people in it.
    ///
    /// The engine was written for two sides and its commentary said so: `Opponent(w)` meant
    /// "the one you are not", which has exactly one answer with two sides and no answer at
    /// all with three. So a beat aimed at side C narrated side B — the result was right and
    /// the play-by-play was about somebody else.
    ///
    /// The booking already answered the question. `MatchBeat.Against` names who a beat is
    /// aimed at, and the finish names who takes the fall; the commentary just had to read it.
    /// </summary>
    public class ThreeWayCommentaryTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string n) => TestRoster.Make(n, overness: 80, charisma: 4.0, skill: 4.0);

        private static MatchEngineResult Run(IEnumerable<MatchBeat> beats)
        {
            var (a, b, c) = (W("Alpha"), W("Bravo"), W("Charlie"));
            return new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                Sides = [MatchSide.Of(a), MatchSide.Of(b), MatchSide.Of(c)],
                Beats = beats.ToList()
            });
        }

        private static MatchBeat Beat(BeatType t, BeatControl c, BeatControl? against = null) =>
            new() { Type = t, Control = c, Against = against };

        private static MatchBeat Finish() =>
            new() { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA,
                    Against = BeatControl.SideC };

        /// <summary>
        /// **A beat aimed at the third side is about the third side.** This is the bug: with
        /// `Opponent(w)` returning whichever side was listed second, a disposal booked on
        /// Charlie described Bravo going through the table and never mentioned Charlie.
        /// </summary>
        [Fact]
        public void ABeatAimedAtTheThirdSide_NamesTheThirdSide()
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, BeatControl.WrestlerA, BeatControl.SideC),
                Finish()
            ]);

            var disposal = r.BeatResults.Single(x => x.BeatType == BeatType.DisposalSpot);
            string line = disposal.Commentary.First();
            output.WriteLine($"  {line}");

            Assert.Contains("Charlie", line);
            Assert.DoesNotContain("Bravo", line);
        }

        /// <summary>And the same beat aimed at the other side names that one instead.</summary>
        [Theory]
        [InlineData(BeatControl.WrestlerB, "Bravo", "Charlie")]
        [InlineData(BeatControl.SideC,     "Charlie", "Bravo")]
        public void TheTargetFollowsTheBooking(BeatControl against, string named, string notNamed)
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, BeatControl.WrestlerA, against),
                Finish()
            ]);

            string line = r.BeatResults.Single(x => x.BeatType == BeatType.DisposalSpot)
                           .Commentary.First();
            output.WriteLine($"  aimed at {against}: {line}");

            Assert.Contains(named, line);
            Assert.DoesNotContain(notNamed, line);
        }

        /// <summary>
        /// An opening in a three-way is three people going at each other. Saying "Alpha and
        /// Bravo" leaves somebody out of their own match.
        /// </summary>
        [Fact]
        public void AnOpeningNamesEverybodyInTheMatch()
        {
            var r = Run([Beat(BeatType.HotOpening, BeatControl.Even), Finish()]);

            string line = r.BeatResults.First().Commentary.First();
            output.WriteLine($"  {line}");

            foreach (var name in new[] { "Alpha", "Bravo", "Charlie" })
                Assert.Contains(name, line);
        }

        /// <summary>
        /// Nobody in the match should be absent from the whole play-by-play. A participant
        /// who is never mentioned is one the audience did not see.
        /// </summary>
        [Fact]
        public void NobodyGoesUnmentionedForTheWholeMatch()
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.NearFall, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.PinBreak, BeatControl.SideC, BeatControl.WrestlerA),
                Finish()
            ]);

            string all = string.Join(" ", r.BeatResults.SelectMany(b => b.Commentary));
            foreach (var line in r.BeatResults.SelectMany(b => b.Commentary))
                output.WriteLine($"  {line}");

            foreach (var name in new[] { "Alpha", "Bravo", "Charlie" })
                Assert.Contains(name, all);
        }

        /// <summary>
        /// Billing reads the way a person says it, and — critically — is unchanged for two
        /// sides. Every singles and tag commentary line goes through this, so a different
        /// answer at two names would rewrite the whole existing play-by-play.
        /// </summary>
        [Fact]
        public void BillingReadsLikeSpeech_AndIsUnchangedForTwo()
        {
            Assert.Equal("Alpha and Bravo", MatchEngine.Billing(["Alpha", "Bravo"]));
            Assert.Equal("Alpha, Bravo and Charlie",
                         MatchEngine.Billing(["Alpha", "Bravo", "Charlie"]));
            Assert.Equal("Alpha, Bravo, Charlie and Delta",
                         MatchEngine.Billing(["Alpha", "Bravo", "Charlie", "Delta"]));
            Assert.Equal("Alpha", MatchEngine.Billing(["Alpha"]));
        }
        /// <summary>
        /// **A beat booked to the third side is worked by the third side.**
        ///
        /// `ControlLegal` was a third copy of the BeatControl-to-side mapping that knew only
        /// A and B, so a beat booked to side C resolved to null and every handler's
        /// `control ??= ctx.LegalA` credited it to side A. A pin break booked for Charlie
        /// read "Alpha covers — and Alpha is there to break it up", which is one wrestler
        /// saving a cover from themselves.
        /// </summary>
        [Fact]
        public void ABeatBookedToTheThirdSide_IsWorkedByTheThirdSide()
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.PinBreak, BeatControl.SideC, BeatControl.WrestlerA),
                Finish()
            ]);

            string line = r.BeatResults.Single(x => x.BeatType == BeatType.PinBreak)
                           .Commentary.First();
            output.WriteLine($"  {line}");

            Assert.Contains("Charlie", line);

            // And nobody is doing something to themselves: the two names in the line differ.
            Assert.DoesNotContain("Alpha covers — and Alpha", line);
            Assert.DoesNotContain("Charlie covers — and Charlie", line);
        }

        /// <summary>
        /// The same, measured rather than read: control resolves to each side in turn. A
        /// commentary assertion could pass on a line that happens not to name anybody.
        /// </summary>
        [Theory]
        [InlineData(BeatControl.WrestlerA, "Alpha")]
        [InlineData(BeatControl.WrestlerB, "Bravo")]
        [InlineData(BeatControl.SideC,     "Charlie")]
        public void ControlResolvesToTheSideItNames(BeatControl control, string expected)
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, control,
                     control == BeatControl.WrestlerA ? BeatControl.WrestlerB : BeatControl.WrestlerA),
                Finish()
            ]);

            string line = r.BeatResults.Single(x => x.BeatType == BeatType.DisposalSpot)
                           .Commentary.First();
            string target = control == BeatControl.WrestlerA ? "Bravo" : "Alpha";
            output.WriteLine($"  {control}: {line}");

            // Both the booked disposer and the booked victim are named, and they are
            // different people. Asserting the disposer comes *first* was over-fitting to one
            // of the beat's three phrasings — two of them name the victim first, so the
            // assertion failed on correct behaviour.
            Assert.Contains(expected, line);
            Assert.Contains(target, line);
            Assert.NotEqual(expected, target);
        }

        /// <summary>
        /// **The ordinary beats follow the booking too**, not just the multi-man ones.
        ///
        /// The multi-man handlers resolve their own target, so a test using a disposal spot
        /// or a pin break cannot tell whether the *general* path — the `other` every handler
        /// receives — respects `Against`. It did not: reverting it to `Opponent(control)`
        /// passed every test in this class, because none of them exercised it.
        ///
        /// A near fall is the plainest beat that names its opponent, so it is the one to ask.
        /// </summary>
        [Theory]
        [InlineData(BeatControl.WrestlerB, "Bravo", "Charlie")]
        [InlineData(BeatControl.SideC,     "Charlie", "Bravo")]
        public void AnOrdinaryBeatKicksOutTheSideItWasAimedAt(
            BeatControl against, string named, string notNamed)
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.NearFall, BeatControl.WrestlerA, against),
                Finish()
            ]);

            string line = string.Join(" ", r.BeatResults
                .Single(x => x.BeatType == BeatType.NearFall).Commentary);
            output.WriteLine($"  aimed at {against}: {line}");

            Assert.Contains(named, line);
            Assert.DoesNotContain(notNamed, line);
        }

        /// <summary>
        /// **A beat that does not say who it is against is between the people still upright.**
        ///
        /// The booking need not aim every beat, and when it does not the engine has to choose.
        /// Choosing somebody who was just put through a table and is still down produces the
        /// format's characteristic failure in reverse: not "where did they go?" but "how are
        /// they in this?"
        /// </summary>
        [Fact]
        public void AnUndirectedBeat_DoesNotTargetSomebodyLyingOnTheFloor()
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.DisposalSpot, BeatControl.WrestlerA, BeatControl.SideC),

                // No Against on either: the engine picks, and Charlie is on the floor.
                Beat(BeatType.NearFall, BeatControl.WrestlerA),
                Beat(BeatType.NearFall, BeatControl.WrestlerA),
                Finish()
            ]);

            var duringWindow = r.BeatResults
                .Where(x => x.BeatType == BeatType.NearFall)
                .SelectMany(x => x.Commentary)
                .ToList();

            foreach (var line in duringWindow) output.WriteLine($"  {line}");

            Assert.All(duringWindow, line =>
                Assert.DoesNotContain("Charlie", line));
            Assert.Contains(duringWindow, line => line.Contains("Bravo"));
        }

        /// <summary>
        /// **Commentary that counts the field counts it right.** An opening that says "these
        /// two" or "Both wrestlers" with three in the ring is the same error as naming only
        /// two of them, one level of abstraction up — and it survived the fix for the naming,
        /// because no name is wrong in it.
        ///
        /// Pair-specific lines are left alone deliberately: a feud erupting *between two
        /// rivals* should say "these two", because it is about those two rather than about
        /// the match.
        /// </summary>
        [Fact]
        public void CommentaryThatCountsTheField_CountsItRight()
        {
            var r = Run(
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.SlowOpening, BeatControl.Even),
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Finish()
            ]);

            var openings = r.BeatResults.Where(x => x.BeatType is BeatType.HotOpening
                                                    or BeatType.SlowOpening
                                                    or BeatType.StandardOpening)
                            .SelectMany(x => x.Commentary).ToList();

            foreach (var line in openings) output.WriteLine($"  {line}");

            Assert.All(openings, line =>
            {
                Assert.DoesNotContain("these two", line);
                Assert.DoesNotContain("Both wrestlers", line);
                Assert.DoesNotContain("both wrestlers", line);
            });

            // Absence is not a pass — this codebase has been caught by that twice. Sweep
            // seeds until the collective phrasing actually appears, so the test knows the
            // positive form exists and not merely that the wrong one is gone.
            bool sawCollective = false;
            for (int seed = 0; seed < 40 && !sawCollective; seed++)
            {
                var swept = new MatchEngine(seed).Execute(new MatchPlanModel
                {
                    Sides = [MatchSide.Of(W("Alpha")), MatchSide.Of(W("Bravo")), MatchSide.Of(W("Charlie"))],
                    Beats = [Beat(BeatType.HotOpening, BeatControl.Even),
                             Beat(BeatType.SlowOpening, BeatControl.Even),
                             Beat(BeatType.StandardOpening, BeatControl.Even),
                             Finish()]
                });

                sawCollective = swept.BeatResults.SelectMany(x => x.Commentary)
                    .Any(l => l.Contains("all three") || l.Contains("All three"));
            }

            Assert.True(sawCollective,
                "No opening across forty seeds said 'all three' — the collective phrasing " +
                "may be unreachable rather than correct.");
        }


    }
}
