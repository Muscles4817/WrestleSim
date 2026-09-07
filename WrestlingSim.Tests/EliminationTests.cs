using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **Falls remove people; last one standing wins.**
    ///
    /// Doc 18 §2.5: elimination "solves the third-man problem by construction, which is why
    /// it scales where a four-way does not. The drama moves from the fall to the *order* of
    /// eliminations."
    ///
    /// That sentence is the spec. A first-fall-wins three-way lives on the possibility that
    /// any cover ends it; an elimination match gives that up and buys a sequence instead —
    /// who went first, who outlasted whom, and whether the winner had to go through the
    /// field or inherited a ring somebody else emptied. So what these tests care about is
    /// the *order*, and the thing that ruins it: falls landing on top of each other.
    /// </summary>
    public class EliminationTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string n) =>
            TestRoster.Make(n, overness: 80, charisma: 4.0, skill: 4.0);

        private static MatchBeat Beat(BeatType t, BeatControl c, BeatControl? against = null) =>
            new() { Type = t, Control = c, Against = against };

        /// <summary>A three-way of singles, cast Alpha / Bravo / Charlie (/ Delta).</summary>
        private static MatchPlanModel Plan(int sides, params MatchBeat[] beats) => new()
        {
            Sides = new[] { "Alpha", "Bravo", "Charlie", "Delta" }
                        .Take(sides).Select(n => MatchSide.Of(W(n))).ToList(),
            Beats = beats.ToList()
        };

        // ── The pacing measure ───────────────────────────────────────────────
        //
        // Tested as a pure function rather than through a match, because this repo keeps
        // measuring the neighbourhood of a mechanism instead of the mechanism. The score
        // it feeds is checked separately, below.

        /// <summary>
        /// **Evenly spaced falls score 1.0; falls on top of each other score 0.**
        ///
        /// The finish counts as a fall — it is the last one — so a ten-beat match with two
        /// eliminations is looking for three roughly-even gaps.
        /// </summary>
        [Theory]
        // Nine beats, two eliminations, finish last: three falls, ideal gap exactly 3.
        [InlineData(new[] { 2, 5 }, 9, 1.0)]    // dead even — 1.0 is reachable when it divides
        [InlineData(new[] { 3, 6 }, 10, 0.9)]   // as even as ten beats and three falls allow
        [InlineData(new[] { 5, 6 }, 10, 0.3)]   // second fall one beat after the first
        [InlineData(new[] { 1, 2 }, 10, 0.3)]   // both early, then nothing for seven beats
        [InlineData(new[] { 7, 8 }, 10, 0.3)]   // both late — a scramble at the end
        public void PacingRewardsSpacingAndPunishesScrambles(int[] falls, int total, double expected)
        {
            double pacing = MatchEngine.EliminationPacing(falls, total);
            output.WriteLine($"  falls at [{string.Join(", ", falls)}] of {total}: {pacing:F3}");
            Assert.Equal(expected, pacing, 1);
        }

        /// <summary>
        /// The minimum gap, not the mean — deliberately. Averaging lets a long stretch of
        /// work pay for two falls back to back, and it does not: one bunched pair spoils the
        /// run whatever else the match did. These two have the same mean gap and should not
        /// score the same.
        /// </summary>
        [Fact]
        public void OneBunchedPairIsNotPaidForByALongStretchOfWork()
        {
            double spread  = MatchEngine.EliminationPacing([4, 8], 12);
            double bunched = MatchEngine.EliminationPacing([7, 8], 12);

            output.WriteLine($"  spread {spread:F3} vs bunched {bunched:F3}");
            Assert.True(spread > bunched + 0.3,
                $"a bunched pair scored {bunched:F3} against {spread:F3} for the same match " +
                "length and fall count — the measure is averaging when it should not");
        }

        /// <summary>A match with no eliminations has no pacing to get wrong.</summary>
        [Fact]
        public void NoEliminations_IsNotPenalised() =>
            Assert.Equal(1.0, MatchEngine.EliminationPacing([], 8));

        // ── The order ────────────────────────────────────────────────────────

        /// <summary>
        /// **The result reports who went out, in order, and who did it.**
        ///
        /// The set would answer who won and lose the whole story. This is the assertion that
        /// the format's own drama survives into something a player can read.
        /// </summary>
        [Fact]
        public void TheResultCarriesTheOrderOfEliminations()
        {
            var r = new MatchEngine(Seed).Execute(Plan(4,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideD),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerB, BeatControl.SideC),
                Beat(BeatType.Comeback, BeatControl.WrestlerA),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            foreach (var g in r.Eliminations)
                output.WriteLine($"  {g.Order}. {g.Wrestler.RingName} by {g.By.RingName} " +
                                 $"({g.Remaining} left)");

            Assert.Equal(2, r.Eliminations.Count);

            Assert.Equal("Delta",   r.Eliminations[0].Wrestler.RingName);
            Assert.Equal("Alpha",   r.Eliminations[0].By.RingName);
            Assert.Equal(1,         r.Eliminations[0].Order);
            Assert.Equal(3,         r.Eliminations[0].Remaining);

            Assert.Equal("Charlie", r.Eliminations[1].Wrestler.RingName);
            Assert.Equal("Bravo",   r.Eliminations[1].By.RingName);
            Assert.Equal(2,         r.Eliminations[1].Order);
            Assert.Equal(2,         r.Eliminations[1].Remaining);

            Assert.Equal("Alpha", r.Winner.RingName);
        }

        /// <summary>
        /// **Somebody eliminated is gone, not merely quiet.** The distinction from a disposal
        /// window is the whole difference between the two mechanisms: a disposed man is
        /// expected back and the commentary should be wondering where he is; an eliminated
        /// man left through the curtain and naming him is a mistake.
        /// </summary>
        [Fact]
        public void AnEliminatedWrestlerIsNeverMentionedAgain()
        {
            var r = new MatchEngine(Seed).Execute(Plan(3,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerA),
                Beat(BeatType.NearFall, BeatControl.WrestlerB),
                Beat(BeatType.CrowdBrawl, BeatControl.WrestlerA),
                Beat(BeatType.Comeback, BeatControl.WrestlerA),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            int gone = r.BeatResults.FindIndex(x => x.BeatType == BeatType.Elimination);
            var after = r.BeatResults.Skip(gone + 1).SelectMany(x => x.Commentary).ToList();

            foreach (var line in after) output.WriteLine($"  {line}");

            Assert.All(after, line => Assert.DoesNotContain("Charlie", line));

            // Absence is not a pass: the beats after the elimination have to be narrating
            // somebody, and it has to be the two who are left.
            Assert.Contains(after, line => line.Contains("Alpha"));
            Assert.Contains(after, line => line.Contains("Bravo"));
        }

        /// <summary>
        /// And the room-wide lines count the survivors. A crowd brawl after one of three has
        /// gone is between two people, and "all three" would be naming a wrestler who is in
        /// the back.
        /// </summary>
        [Fact]
        public void RoomWideLinesCountTheSurvivors()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var lines = new MatchEngine(seed).Execute(Plan(3,
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                    Beat(BeatType.CrowdBrawl, BeatControl.WrestlerA),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)))
                    .BeatResults.Single(x => x.BeatType == BeatType.CrowdBrawl).Commentary;

                if (seed == 0) foreach (var l in lines) output.WriteLine($"  {l}");

                Assert.All(lines, l => Assert.DoesNotContain("all three", l));
                Assert.All(lines, l => Assert.DoesNotContain("All three", l));
                Assert.All(lines, l => Assert.DoesNotContain("Charlie", l));
            }
        }

        // ── Scoring ──────────────────────────────────────────────────────────

        /// <summary>
        /// **The spacing is worth something, or the measure is decoration.**
        ///
        /// Two plans with the same beats and the same eliminations, differing only in where
        /// the falls land. If the well-spaced one does not score better, doc 18's claim about
        /// the order is a comment rather than a rule.
        /// </summary>
        [Fact]
        public void AScrambleScoresWorseThanASpreadOutRun()
        {
            MatchBeat[] Spread() =>
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerB, BeatControl.SideD),
                Beat(BeatType.Comeback, BeatControl.WrestlerA),
                Beat(BeatType.NearFall, BeatControl.WrestlerA),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.HighSpot, BeatControl.WrestlerA),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)
            ];

            MatchBeat[] Scramble() =>
            [
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Comeback, BeatControl.WrestlerA),
                Beat(BeatType.NearFall, BeatControl.WrestlerA),
                Beat(BeatType.HighSpot, BeatControl.WrestlerA),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideD),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)
            ];

            // Swept, because one seed's difference could be noise in the beat rolls rather
            // than the rule under test.
            int spreadWins = 0;
            double totalGap = 0;
            for (int seed = 0; seed < 25; seed++)
            {
                var spread   = new MatchEngine(seed).Execute(Plan(4, Spread()));
                var scramble = new MatchEngine(seed).Execute(Plan(4, Scramble()));

                totalGap += spread.FinalScore - scramble.FinalScore;
                if (spread.FinalScore > scramble.FinalScore) spreadWins++;
            }

            output.WriteLine($"  spread scored higher in {spreadWins}/25 seeds, " +
                             $"mean gap {totalGap / 25:F2} points");

            Assert.True(spreadWins >= 22,
                $"the spread-out run only beat the scramble in {spreadWins} of 25 seeds");
            Assert.True(totalGap / 25 > 1.0,
                $"mean gap of {totalGap / 25:F2} points is too small to be the rule rather " +
                "than the roll");
        }

        /// <summary>
        /// And the nudge is zero in a match with no eliminations — this must not have quietly
        /// re-graded every other match in the game.
        /// </summary>
        [Fact]
        public void AMatchWithoutEliminations_IsNotTouchedByThePacingRule()
        {
            var r = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alpha")),
                SideB = MatchSide.Of(W("Bravo")),
                Beats =
                [
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                    Beat(BeatType.Comeback, BeatControl.WrestlerA),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerA)
                ]
            });

            Assert.Equal(0.0, r.Breakdown.PacingNudge);
            Assert.Empty(r.Eliminations);
        }

        // ── What the ring is, versus what the plan says ──────────────────────
        //
        // Two rules ask "is the room split" and "can this cover be broken". Both were keyed
        // off `Plan.IsMultiMan`, which is a fact about the booking and stays true for the
        // whole of an elimination match — so both kept applying to a three-way that had
        // become a one-on-one. Round one of this feature's own review found them.

        /// <summary>
        /// **Attention is measured against whoever is still in.**
        ///
        /// `TopConnection` was the most connected performer in the *match*, so eliminating
        /// the biggest name first left the two who remained working the rest of the match at
        /// a permanent discount — measured against somebody who had gone to the back, in a
        /// match that was now entirely theirs.
        ///
        /// Isolating this is harder than it looks, and the first attempt got it wrong in the
        /// way this codebase always gets it wrong. A three-way comparison — eliminate the
        /// star, versus eliminate a mid-carder — changes two things at once: who is still in
        /// (attention, the mechanism) *and* who the remaining beats are worked on (the
        /// victim's connection, which also scales the crowd term). It reported the opposite
        /// of the truth, confidently.
        ///
        /// Four sides fixes it. Star plus three mid-carders; the tail is controlled by MidOne
        /// and aimed at MidTwo in both runs, three sides remain in both runs, and the only
        /// difference is whether the Star or MidThree was the one eliminated.
        /// </summary>
        [Fact]
        public void AttentionIsMeasuredAgainstWhoIsStillIn()
        {
            var star = TestRoster.Make("Star",     overness: 99, charisma: 5.0, skill: 4.0);
            var mid1 = TestRoster.Make("MidOne",   overness: 55, charisma: 2.5, skill: 4.0);
            var mid2 = TestRoster.Make("MidTwo",   overness: 55, charisma: 2.5, skill: 4.0);
            var mid3 = TestRoster.Make("MidThree", overness: 55, charisma: 2.5, skill: 4.0);

            // Sides: A = MidOne, B = MidTwo, C = MidThree, D = Star.
            MatchPlanModel Build(BeatControl goesOutFirst, BeatControl takesTheFall) => new()
            {
                Sides = [MatchSide.Of(mid1), MatchSide.Of(mid2),
                         MatchSide.Of(mid3), MatchSide.Of(star)],
                Beats =
                [
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    Beat(BeatType.Elimination, BeatControl.WrestlerA, goesOutFirst),

                    // Identical in both runs: MidOne works MidTwo.
                    Beat(BeatType.HeatSegment, BeatControl.WrestlerA, BeatControl.WrestlerB),
                    Beat(BeatType.HighSpot,    BeatControl.WrestlerA, BeatControl.WrestlerB),

                    Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerA, takesTheFall)
                ]
            };

            // Measured on the crowd *reaction* vector, not CrowdEnergyDelta. Attention feeds
            // `RecordReaction` — how much of a beat's noise the room actually gives it —
            // and the energy delta is computed before it. My first pass at this test read
            // the energy delta, found a 6.4% gap, and reported it as attention; the gap was
            // the near-fall rule below. Two mechanisms, one measurement, wrong attribution.
            double starGone = 0, starStillIn = 0;
            for (int seed = 0; seed < 25; seed++)
            {
                // Star out first — MidOne is now the biggest name in the ring.
                starGone += new MatchEngine(seed)
                    .Execute(Build(BeatControl.SideD, BeatControl.SideC)).Reaction.Investment;

                // MidThree out first — the star is still in and still owns the room.
                starStillIn += new MatchEngine(seed)
                    .Execute(Build(BeatControl.SideC, BeatControl.SideD)).Reaction.Investment;
            }

            output.WriteLine($"  MidOne works MidTwo, same two beats, three sides in, either way:");
            output.WriteLine($"    star eliminated : {starGone / 25:F4} investment");
            output.WriteLine($"    star still in   : {starStillIn / 25:F4} investment");

            Assert.True(starGone > starStillIn,
                $"the room was {starGone / 25:F4} invested with the star gone against " +
                $"{starStillIn / 25:F4} with the star in — attention is still being measured " +
                "against somebody who left through the curtain");
        }

        /// <summary>
        /// **Down to two, a cover carries jeopardy again.**
        ///
        /// The near-fall discount exists because in a multi-man everybody knows the count can
        /// be broken. Once elimination has taken the field to two, nobody is left to break
        /// anything — so the crowd starts believing counts again, which is the entire
        /// narrative payoff of the format and was being withheld, because the rule was keyed
        /// off the plan's side count rather than off who was still in.
        ///
        /// Asserted on the recorded factor rather than on crowd energy, and that is the
        /// point of the factor being recorded. The first version of this test compared a
        /// near fall before an elimination against one after it and reported a 2.1× swing —
        /// which was the crowd level the elimination had built, not the rule. It passed
        /// against a mutation that reverted the mechanism completely. Two mechanisms, one
        /// measurement, wrong attribution; the third time in a day, which is why the
        /// mechanism is now observable instead of inferred.
        /// </summary>
        [Fact]
        public void ANearFallDownToTwo_CarriesFullJeopardy()
        {
            // Four sides. The first elimination leaves three — still a field, still
            // breakable. The second leaves two, and nobody can reach the cover.
            var r = new MatchEngine(Seed).Execute(Plan(4,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideD),
                Beat(BeatType.NearFall, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.NearFall, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            var nearFalls = r.BeatResults.Where(x => x.BeatType == BeatType.NearFall).ToList();
            output.WriteLine($"  three in : jeopardy {nearFalls[0].NearFallJeopardy:F2}");
            output.WriteLine($"  two in   : jeopardy {nearFalls[1].NearFallJeopardy:F2}");

            Assert.Equal(MatchEngine.CrowdedOutNearFall, nearFalls[0].NearFallJeopardy);
            Assert.Equal(1.0, nearFalls[1].NearFallJeopardy);
        }

        /// <summary>
        /// And a singles near fall is untouched — this rule has never applied to two sides
        /// and must not start now that it counts survivors instead of the plan.
        /// </summary>
        [Fact]
        public void ASinglesNearFall_AlwaysCarriesFullJeopardy()
        {
            var r = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alpha")),
                SideB = MatchSide.Of(W("Bravo")),
                Beats =
                [
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    Beat(BeatType.NearFall, BeatControl.WrestlerA),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerA)
                ]
            });

            Assert.Equal(1.0, r.BeatResults.Single(x => x.BeatType == BeatType.NearFall)
                                           .NearFallJeopardy);
        }

        // ── Validation ───────────────────────────────────────────────────────

        /// <summary>
        /// **An elimination match runs until one side is left.**
        ///
        /// The format's one structural promise. A plan that eliminates one of four and then
        /// books a finish has not had an elimination match — it has had a four-way with a
        /// spare beat in it, and the two are graded differently, so the difference has to be
        /// real rather than a matter of what the booker called it.
        /// </summary>
        [Fact]
        public void APlanThatDoesNotThinTheFieldToOne_IsRefused()
        {
            var plan = Plan(4,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideD),
                Beat(BeatType.Comeback, BeatControl.WrestlerA),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("runs until one side is left"));
        }

        /// <summary>A side cannot work a beat after it has been eliminated.</summary>
        [Fact]
        public void ABeatBookedForAnEliminatedSide_IsRefused()
        {
            var plan = Plan(3,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.HeatSegment, BeatControl.SideC),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("already been eliminated"));
        }

        /// <summary>And cannot be the target of one either.</summary>
        [Fact]
        public void ABeatAimedAtAnEliminatedSide_IsRefused()
        {
            var plan = Plan(3,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.NearFall, BeatControl.WrestlerA, BeatControl.SideC),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("aimed at a side that has already"));
        }

        /// <summary>An elimination has to say who goes out.</summary>
        [Fact]
        public void AnEliminationWithNoTarget_IsRefused()
        {
            var plan = Plan(3,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("has to say who"));
        }

        /// <summary>Nobody eliminates themselves.</summary>
        [Fact]
        public void ASideEliminatingItself_IsRefused()
        {
            var plan = Plan(3,
                Beat(BeatType.HotOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerA),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("eliminating itself"));
        }

        /// <summary>
        /// And an elimination needs a third party, like the rest of its category — a
        /// two-sided elimination match is a match that ends when somebody is eliminated,
        /// which is a normal finish with a longer name.
        /// </summary>
        [Fact]
        public void AnEliminationInASinglesMatch_IsRefused()
        {
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alpha")),
                SideB = MatchSide.Of(W("Bravo")),
                Beats =
                [
                    Beat(BeatType.HotOpening, BeatControl.Even),
                    Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                    Beat(BeatType.FinishClean, BeatControl.WrestlerA)
                ]
            };

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("needs a third party"));
        }

        // ── Bookable ─────────────────────────────────────────────────────────

        /// <summary>
        /// **The player can book one.** Both shipped elimination presets validate, run, and
        /// produce the field thinning to one — the rule in CLAUDE.md, applied to the feature
        /// this file is about rather than assumed.
        /// </summary>
        [Theory]
        [InlineData("Triple Threat Elimination", 3, 1)]
        [InlineData("Four-Way Elimination",      4, 2)]
        public void TheShippedPresetsRun(string name, int sides, int expectedEliminations)
        {
            var structure = MatchStructureLibrary.All.Single(s => s.Name == name);
            var plan = Plan(sides);
            plan.Beats = structure.Beats.Select(b => b.Clone()).ToList();

            Assert.Empty(plan.Validate());

            var r = new MatchEngine(Seed).Execute(plan);
            foreach (var g in r.Eliminations)
                output.WriteLine($"  {g.Order}. {g.Wrestler.RingName} by {g.By.RingName}");
            output.WriteLine($"  winner {r.Winner.RingName}, {r.StarRating:F2} stars, " +
                             $"pacing {r.EliminationPacing:F2}");

            Assert.Equal(expectedEliminations, r.Eliminations.Count);
            Assert.DoesNotContain(r.Eliminations, g => g.Wrestler == r.Winner);

            // Everybody but the winner is out, which is what the format promises.
            Assert.Equal(sides - 1, r.Eliminations.Count + 1);
        }
    }
}
