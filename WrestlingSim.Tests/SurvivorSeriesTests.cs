using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **Falls take out people, and a side is out when it has nobody left.**
    ///
    /// The elimination work took out whole *sides*, which is right for a triple threat where
    /// a side is one wrestler. A Survivor Series is the same rule with bigger sides: teams,
    /// tag rules, and a fall sends you to the back rather than ending the match.
    ///
    /// The reason it earns its own name is what happens in between. Four against three is a
    /// handicap match — arrived at rather than booked — and the wrestlers carrying it should
    /// feel exactly what a booked handicap does. That is not a new mechanism here; it is the
    /// handicap term reading live survivor counts instead of the booked side sizes, which is
    /// the same correction elimination has now needed in four places.
    /// </summary>
    public class SurvivorSeriesTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string n) =>
            TestRoster.Make(n, overness: 70, charisma: 3.0, skill: 3.5);

        private static MatchBeat Beat(BeatType t, BeatControl c, BeatControl? against = null) =>
            new() { Type = t, Control = c, Against = against };

        /// <summary>Two teams of <paramref name="size"/>: A1..An versus B1..Bn.</summary>
        private static MatchPlanModel Teams(int size, params MatchBeat[] beats) => new()
        {
            SideA = MatchSide.Of(Enumerable.Range(1, size).Select(i => W($"A{i}")).ToArray()),
            SideB = MatchSide.Of(Enumerable.Range(1, size).Select(i => W($"B{i}")).ToArray()),
            Beats = beats.ToList()
        };

        // ── Falls take out people ────────────────────────────────────────────

        /// <summary>
        /// **A fall takes out the wrestler who was in the ring, and the team carries on.**
        ///
        /// The whole difference from side elimination, and it needed no new field on the
        /// beat: an elimination is aimed at a *side*, and who goes out is whoever is legal —
        /// which is who you can pin. With one wrestler a side that is the side, so the
        /// triple-threat behaviour is unchanged by construction.
        /// </summary>
        [Fact]
        public void AFallTakesOutTheWrestlerInTheRing_NotTheTeam()
        {
            var r = new MatchEngine(Seed).Execute(Teams(3,
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            foreach (var g in r.Eliminations)
                output.WriteLine($"  {g.Order}. {g.Wrestler.RingName} by {g.By.RingName}");
            output.WriteLine($"  survivors: {string.Join(", ", r.Survivors.Select(w => w.RingName))}");

            // Three of side B out, one at a time — two booked eliminations and the finish,
            // which is the last fall.
            Assert.Equal(3, r.Eliminations.Count);
            Assert.Equal(3, r.Eliminations.Select(e => e.Wrestler).Distinct().Count());
            Assert.All(r.Eliminations, e => Assert.StartsWith("B", e.Wrestler.RingName));

            // And side A is intact, which is what "survivors" means.
            Assert.Equal(3, r.Survivors.Count);

            // The count beside each elimination is *people* left, not sides — six of one
            // and none of the other in a two-team match, where both sides are in until the
            // last fall. A browser check caught this reading "2 left" six times running
            // while every test here passed, because none of them looked at the number.
            Assert.Equal([5, 4, 3], r.Eliminations.Select(e => e.Remaining));
        }

        /// <summary>
        /// **Nobody who has been pinned comes back.** The tag rotation walks the team round,
        /// and in this format the next one round is often somebody who left ten minutes ago
        /// — which would be the same class of mistake as naming an eliminated wrestler in
        /// commentary, except this one would have them win the match.
        /// </summary>
        [Fact]
        public void AnEliminatedWrestlerIsNeverLegalAgain()
        {
            var r = new MatchEngine(Seed).Execute(Teams(3,
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerB, BeatControl.WrestlerA),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Tag,         BeatControl.WrestlerA),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerA),
                Beat(BeatType.Tag,         BeatControl.WrestlerA),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerA),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            var goneFirst = r.Eliminations[0].Wrestler;
            int at = r.BeatResults.FindIndex(x => x.BeatType == BeatType.Elimination);

            var after = r.BeatResults.Skip(at + 1).ToList();
            foreach (var b in after)
                output.WriteLine($"  {b.BeatType,-16} {b.Worker?.RingName} -> {b.Target?.RingName}");

            output.WriteLine($"  first out: {goneFirst.RingName}");
            Assert.All(after, b => Assert.NotEqual(goneFirst, b.Worker));
            Assert.All(after, b => Assert.NotEqual(goneFirst, b.Target));
        }

        // ── The handicap arrives on its own ──────────────────────────────────

        /// <summary>
        /// **Once the numbers go lopsided, the short-handed side feels it.**
        ///
        /// This is the claim that makes a Survivor Series more than a long tag match, and it
        /// is deliberately not a new mechanism: `NumbersFatigue` reads live survivor counts,
        /// so a team that is a wrestler down is in a handicap match by the same rule as one
        /// that was booked into one.
        ///
        /// Asserted on the recorded factor rather than a score, so what is checked is the
        /// rule and not the twenty other things a score moves.
        /// </summary>
        [Fact]
        public void ATeamAWrestlerDown_IsInAHandicapMatch()
        {
            var r = new MatchEngine(Seed).Execute(Teams(3,
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerA),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),

                // Level to here. Side B loses one, and works the rest short-handed.
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.HighSpot,    BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            foreach (var b in r.BeatResults)
                output.WriteLine($"  {b.BeatType,-16} {b.Worker?.RingName,-4} " +
                                 $"numbers {b.NumbersFatigue:F3}");

            int firstFall = r.BeatResults.FindIndex(x => x.BeatType == BeatType.Elimination);

            // While it is even, nobody carries anything.
            Assert.All(r.BeatResults.Take(firstFall),
                       b => Assert.Equal(1.0, b.NumbersFatigue));

            // Afterwards, side B's work is discounted and side A's is not.
            var shortHanded = r.BeatResults.Skip(firstFall + 1)
                               .Where(b => b.Worker?.RingName.StartsWith("B") == true).ToList();
            var atFullStrength = r.BeatResults.Skip(firstFall + 1)
                               .Where(b => b.Worker?.RingName.StartsWith("A") == true).ToList();

            Assert.NotEmpty(shortHanded);
            Assert.Contains(shortHanded, b => b.NumbersFatigue < 1.0);
            Assert.All(atFullStrength, b => Assert.Equal(1.0, b.NumbersFatigue));
        }

        // ── The payoff ───────────────────────────────────────────────────────

        /// <summary>
        /// **The result names who was left standing.**
        ///
        /// The reason this match has a name of its own: the story is not that a team won, it
        /// is *who was left*. A sole survivor is a made wrestler; four survivors is a squash
        /// of the other team, and the result screen cannot tell them apart without this.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TheResultNamesTheSurvivors(int survivors)
        {
            const int size = 3;
            var beats = new List<MatchBeat> { Beat(BeatType.StandardOpening, BeatControl.Even) };

            // Side A loses (size - survivors); side B loses all of them.
            for (int i = 0; i < size - survivors; i++)
                beats.Add(Beat(BeatType.Elimination, BeatControl.WrestlerB, BeatControl.WrestlerA));
            for (int i = 0; i < size - 1; i++)
                beats.Add(Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB));
            beats.Add(Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var plan = Teams(size, beats.ToArray());
            Assert.Empty(plan.Validate());

            var r = new MatchEngine(Seed).Execute(plan);
            output.WriteLine($"  {survivors} expected: " +
                             $"{string.Join(", ", r.Survivors.Select(w => w.RingName))}");

            Assert.Equal(survivors, r.Survivors.Count);
            Assert.All(r.Survivors, w => Assert.StartsWith("A", w.RingName));
            Assert.DoesNotContain(r.Survivors, w => r.Eliminations.Any(e => e.Wrestler == w));
        }

        /// <summary>And the play-by-play says the count, because eight falls is too many to keep in your head.</summary>
        [Fact]
        public void ThePlayByPlayKeepsTheScore()
        {
            var r = new MatchEngine(Seed).Execute(Teams(3,
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.HeatSegment, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB)));

            var lines = r.BeatResults.SelectMany(b => b.Commentary).ToList();
            foreach (var l in lines) output.WriteLine($"  {l}");

            Assert.Contains(lines, l => l.Contains("down to"));
            Assert.Contains(lines, l => l.Contains("sole survivor") || l.Contains("still standing"));
        }

        // ── Validation ───────────────────────────────────────────────────────

        /// <summary>
        /// **A plan cannot take more falls from a side than it has wrestlers.** The
        /// side-level version of this rule could not express the mistake at all.
        /// </summary>
        [Fact]
        public void TakingMoreFallsThanASideHasWrestlers_IsRefused()
        {
            var plan = Teams(2,
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("takes 3 falls from them"));
        }

        /// <summary>
        /// And it still has to empty a side. A Survivor Series that eliminates two of four
        /// and stops is a tag match with some falls in it.
        /// </summary>
        [Fact]
        public void APlanThatEmptiesNobody_IsRefused()
        {
            var plan = Teams(3,
                Beat(BeatType.StandardOpening, BeatControl.Even),
                Beat(BeatType.Elimination, BeatControl.WrestlerA, BeatControl.WrestlerB),
                Beat(BeatType.FinishClean, BeatControl.WrestlerA, BeatControl.WrestlerB));

            var errors = plan.Validate();
            output.WriteLine($"  {string.Join(" | ", errors)}");
            Assert.Contains(errors, e => e.Contains("runs until one side is left"));
        }

        /// <summary>The shipped preset validates, runs, and leaves somebody standing.</summary>
        [Fact]
        public void TheShippedPresetRuns()
        {
            var structure = MatchStructureLibrary.All.Single(s => s.Name == "Survivor Series");
            var plan = Teams(4);
            plan.Beats = structure.Beats.Select(b => b.Clone()).ToList();

            Assert.Empty(plan.Validate());

            var r = new MatchEngine(Seed).Execute(plan);
            foreach (var g in r.Eliminations)
                output.WriteLine($"  {g.Order}. {g.Wrestler.RingName} by {g.By.RingName}");
            output.WriteLine($"  survivors: {string.Join(", ", r.Survivors.Select(w => w.RingName))}" +
                             $" — {r.StarRating:F2} stars");

            // Five booked eliminations plus the finish, which is the sixth fall — the
            // engine records it as one because in this format it is one.
            Assert.Equal(6, r.Eliminations.Count);
            Assert.NotEmpty(r.Survivors);
            Assert.All(r.Survivors, w => Assert.DoesNotContain(w, r.Eliminations.Select(e => e.Wrestler)));
        }
    }
}
