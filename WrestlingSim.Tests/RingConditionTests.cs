using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **Two meters that pull against each other.**
    ///
    /// Before this the engine had fatigue inside a match — <c>FadeFactor</c>, the stamina
    /// penalty, the extra wear of being outnumbered — and nothing whatever outside one. A
    /// wrestler left a thirty-four-minute epic in exactly the state they entered it, and
    /// eight months off cost them overness and not one point of ring sharpness. Work every
    /// night for a month: no wear. Sit out a year: come back as sharp as you left.
    ///
    /// The design is that a day off pays back fatigue and costs sharpness, so resting
    /// somebody is a trade rather than a repair and there is no correct amount of it. The
    /// tests below are the claims that make that true, and the two that would be easiest to
    /// get wrong:
    ///
    ///   • **Danger is not fatigue.** Nothing in <see cref="RingCondition"/> reads match
    ///     type or stipulation. A sprint can cost more than a match twice its length.
    ///   • **A style has a shape, not a number.** Pace and length are separate axes and the
    ///     styles do not rank the same way on them.
    /// </summary>
    public class RingConditionTests(ITestOutputHelper output)
    {
        private static readonly int Seed = StableSeed.From("ring-condition");

        private static Wrestler W(string n, WrestlingStyle style = WrestlingStyle.Brawler,
                                  int psych = 78, int stamina = 80)
        {
            var w = TestRoster.Make(n, overness: 75, charisma: 3.2, psychology: psych, skill: 3.5);
            w.Style = style;
            w.Mental!.RingIQ = psych;
            w.Physical!.Stamina = stamina;
            return w;
        }

        private static MatchBeat B(BeatType type, BeatIntensity intensity, BeatDuration duration,
                                  BeatControl control = BeatControl.WrestlerA) =>
            new() { Type = type, Intensity = intensity, Duration = duration, Control = control };

        // ── Danger is not fatigue ────────────────────────────────────────────

        /// <summary>
        /// **A twelve-minute sprint can cost more than a twenty-six-minute match.**
        ///
        /// The claim the whole cost function exists to make true, and the one the first
        /// implementation got wrong: multiplying both halves of the exertion by the running
        /// time made length dominate everything, so a sprint was always the cheap option.
        /// Pace is squared and volume is not, which is the physiology — going flat out is
        /// anaerobic and the bill grows far faster than the clock does.
        ///
        /// True for the pace-loaded styles rather than universally, which is the honest
        /// version: a striker pays more for the sprint, a powerhouse very much less.
        /// </summary>
        [Fact]
        public void ASprintCanCostMoreThanAMatchTwiceItsLength()
        {
            foreach (var style in new[] { WrestlingStyle.Striker, WrestlingStyle.HighFlyer,
                                          WrestlingStyle.Powerhouse, WrestlingStyle.Technical })
            {
                double sprint  = RingCondition.MatchCost(12, pace: 2.00, style);
                double classic = RingCondition.MatchCost(26, pace: 1.05, style);
                output.WriteLine($"  {style,-11} 12m flat out {sprint,5:F1}   26m steady {classic,5:F1}");
            }

            double strikerSprint  = RingCondition.MatchCost(12, 2.00, WrestlingStyle.Striker);
            double strikerClassic = RingCondition.MatchCost(26, 1.05, WrestlingStyle.Striker);
            Assert.True(strikerSprint > strikerClassic,
                $"a striker's sprint should outcost the long one: {strikerSprint:F1} vs {strikerClassic:F1}");

            // And the reverse for the style that is punished by the clock instead.
            double powerSprint  = RingCondition.MatchCost(12, 2.00, WrestlingStyle.Powerhouse);
            double powerClassic = RingCondition.MatchCost(26, 1.05, WrestlingStyle.Powerhouse);
            Assert.True(powerClassic > powerSprint * 1.3,
                $"a powerhouse should be punished by the clock: {powerClassic:F1} vs {powerSprint:F1}");
        }

        /// <summary>
        /// **A style is a shape over two axes, not a single number.**
        ///
        /// If pace load and volume load ranked the styles the same way, one of them would
        /// be redundant and the whole interaction would collapse into "some styles are
        /// tiring". The orders have to genuinely differ.
        /// </summary>
        [Fact]
        public void PaceAndLengthDoNotRankTheStylesTheSameWay()
        {
            var styles = Enum.GetValues<WrestlingStyle>().Where(s => s != WrestlingStyle.Null).ToList();

            var byPace   = styles.OrderByDescending(RingCondition.PaceLoad).ToList();
            var byVolume = styles.OrderByDescending(RingCondition.VolumeLoad).ToList();

            output.WriteLine($"  by pace:   {string.Join(" > ", byPace)}");
            output.WriteLine($"  by length: {string.Join(" > ", byVolume)}");

            Assert.NotEqual(byPace, byVolume);

            // The specific inversion the design rests on.
            Assert.True(RingCondition.PaceLoad(WrestlingStyle.HighFlyer)
                        > RingCondition.PaceLoad(WrestlingStyle.Powerhouse));
            Assert.True(RingCondition.VolumeLoad(WrestlingStyle.Powerhouse)
                        > RingCondition.VolumeLoad(WrestlingStyle.HighFlyer));

            // And technical is the economical one on both, which is why technical wrestlers
            // could always work every night of the week.
            Assert.Equal(WrestlingStyle.Technical, byPace.Last());
            Assert.Equal(WrestlingStyle.Technical, byVolume.Last());
        }

        /// <summary>
        /// Pace is read off the beats and is minute-weighted, so eight brief extreme spots
        /// do not read like eight minutes of them.
        /// </summary>
        [Fact]
        public void PaceIsMinuteWeightedNotBeatCounted()
        {
            var briefSpots = Enumerable.Range(0, 8)
                .Select(_ => B(BeatType.HighSpot, BeatIntensity.Extreme, BeatDuration.Brief))
                .Append(B(BeatType.HeatSegment, BeatIntensity.Low, BeatDuration.Extended))
                .ToList();

            var sustained = new List<MatchBeat>
            {
                B(BeatType.HighSpot,    BeatIntensity.Extreme, BeatDuration.Long),
                B(BeatType.HeatSegment, BeatIntensity.Extreme, BeatDuration.Long)
            };

            double spotty = RingCondition.Pace(briefSpots);
            double flat   = RingCondition.Pace(sustained);

            output.WriteLine($"  8 brief extreme spots + a long rest: {spotty:F2}");
            output.WriteLine($"  14 minutes of extreme:               {flat:F2}");

            Assert.True(flat > spotty * 1.4);
            Assert.Equal(RingCondition.IntensityWeight(BeatIntensity.Extreme), flat, 3);
        }

        // ── The two meters pull against each other ───────────────────────────

        /// <summary>
        /// **The same day off pays back fatigue and costs sharpness.**
        ///
        /// The whole design in one assertion. If a rest day only helped, resting everybody
        /// permanently would be the dominant strategy and the meters would be decoration.
        /// </summary>
        [Fact]
        public void ADayOffMendsOneMeterAndSpendsTheOther()
        {
            var w = W("Rested");
            w.Fatigue = 60; w.Sharpness = 90;

            RingCondition.ApplyDayOff(w);

            output.WriteLine($"  fatigue 60 → {w.Fatigue:F1}, sharpness 90 → {w.Sharpness:F1}");

            Assert.True(w.Fatigue < 60, "a day off should pay back fatigue");
            Assert.True(w.Sharpness < 90, "and cost sharpness");
        }

        /// <summary>
        /// **Nobody decays to nothing, and where they stop is who they are.**
        ///
        /// A wrestler who has been at it twenty years and keeps himself ready stops falling
        /// at "fine" — able to go out and have a good match. What he cannot reach from home
        /// is his own best. Somebody whose game is speed and timing settles a long way
        /// below that, which is the young wrestler needing reps week in and week out.
        /// </summary>
        [Fact]
        public void SomeWrestlersStayReadyAtHomeAndSomeDoNot()
        {
            foreach (var (label, psych) in new[] { ("veteran", 95), ("regular", 78), ("rookie", 58) })
            {
                double upkeep = RingCondition.SelfMaintenance(psych, psych);
                output.WriteLine($"  {label,-8} psych {psych}  upkeep {upkeep:F2}  " +
                                 $"settles at {RingCondition.RestingFloor(upkeep):F0}");
            }

            double vet    = RingCondition.RestingFloor(RingCondition.SelfMaintenance(95, 95));
            double rookie = RingCondition.RestingFloor(RingCondition.SelfMaintenance(58, 58));

            Assert.True(vet > rookie + 12,
                $"the veteran should hold far more of it at home: {vet:F0} vs {rookie:F0}");
            Assert.True(vet < 90, "and still not be at his best without live matches");
            Assert.True(rookie > 30, "and nobody decays to useless");

            // Rust stops at the floor rather than continuing through it.
            Assert.Equal(0.0, RingCondition.RustPerDay(vet, RingCondition.SelfMaintenance(95, 95)), 3);
        }

        /// <summary>
        /// **A rep does more for the young one, and he still needs more of them.**
        ///
        /// Two things that sound contradictory and are not, which is why this is asserted
        /// rather than described. Per match the youngster's body answers harder; across a
        /// calendar he is losing it faster than he is buying it back, so holding a high
        /// reading takes more matches a week than it does for the veteran.
        /// </summary>
        [Fact]
        public void AYoungWrestlerGainsMorePerMatchAndStillNeedsMoreMatches()
        {
            double vetUpkeep    = RingCondition.SelfMaintenance(95, 95);
            double rookieUpkeep = RingCondition.SelfMaintenance(58, 58);

            double vetRep    = RingCondition.SharpnessGain(12, 1.2, vetUpkeep);
            double rookieRep = RingCondition.SharpnessGain(12, 1.2, rookieUpkeep);

            output.WriteLine($"  one match:  veteran +{vetRep:F2}   rookie +{rookieRep:F2}");
            Assert.True(rookieRep > vetRep, "the young one's body answers a rep harder");

            // Now across a calendar, working twice a week from the same starting point.
            double Settle(double upkeep)
            {
                double s = 40;
                for (int week = 0; week < 40; week++)
                {
                    s = Math.Min(100, s + 2 * RingCondition.SharpnessGain(12, 1.2, upkeep));
                    for (int d = 0; d < 5; d++) s -= RingCondition.RustPerDay(s, upkeep);
                }
                return s;
            }

            double vetAfter    = Settle(vetUpkeep);
            double rookieAfter = Settle(rookieUpkeep);

            output.WriteLine($"  40 weeks at 2 matches/wk from 40: veteran {vetAfter:F0}, rookie {rookieAfter:F0}");
            Assert.True(vetAfter > rookieAfter,
                "on a light schedule the veteran holds the higher reading despite the smaller rep");
        }

        /// <summary>
        /// Recovery is fast from deep fatigue and slow from nearly fresh, which is why a
        /// week off does most of the work of a month — and why resting somebody past that
        /// point is only costing them sharpness.
        /// </summary>
        [Fact]
        public void RestGivesBackMostOfItEarly()
        {
            double deep    = RingCondition.RecoveryPerDay(80, 1.0);
            double shallow = RingCondition.RecoveryPerDay(10, 1.0);

            output.WriteLine($"  from 80 fatigue: {deep:F2}/day   from 10: {shallow:F2}/day");
            Assert.True(deep > shallow * 2);

            // A better gas tank recovers faster from the same hole.
            Assert.True(RingCondition.RecoveryPerDay(50, 1.20)
                        > RingCondition.RecoveryPerDay(50, 0.80));
        }

        /// <summary>
        /// **Fatigue does not compound on itself.**
        ///
        /// Written after a mutation survived. The first wiring read a wrestler's
        /// conditioning through <see cref="PerformerProfile.Conditioning"/>, which by then
        /// already had the fatigue factor taken off it — so a tired wrestler paid *more*
        /// for the same match and recovered *slower* from it, twice over, and nobody
        /// designed either. It was fixed during the build and left nothing behind that
        /// would notice it coming back.
        ///
        /// The match is the match. What it takes out of somebody is decided by how long
        /// they were out there and how fast they went, not by how tired they already were.
        /// </summary>
        [Fact]
        public void BeingTiredDoesNotMakeTonightsMatchCostMore()
        {
            var fresh = W("Fresh");
            var tired = W("Tired"); tired.Fatigue = 70;

            double freshGas = new PerformerProfile(fresh).BaseConditioning;
            double tiredGas = new PerformerProfile(tired).BaseConditioning;

            output.WriteLine($"  base conditioning: fresh {freshGas:F3}, tired {tiredGas:F3}");
            Assert.Equal(freshGas, tiredGas, 4);

            // Which is what keeps the cost of a given match, and the rate of recovery from
            // it, the same for both of them.
            Assert.Equal(RingCondition.MatchCost(20, 1.3, WrestlingStyle.Brawler, 1.0, freshGas),
                         RingCondition.MatchCost(20, 1.3, WrestlingStyle.Brawler, 1.0, tiredGas), 4);

            // Recovery still scales with fatigue itself — deep holes empty fastest — but
            // through the fatigue argument, not through a gas tank fatigue has already
            // shrunk.
            Assert.Equal(RingCondition.RecoveryPerDay(40, freshGas),
                         RingCondition.RecoveryPerDay(40, tiredGas), 4);
        }

        // ── What the meters do ───────────────────────────────────────────────

        /// <summary>
        /// **Fatigue is gas and rust is craft, and neither is the crowd.**
        ///
        /// The split that keeps three different things apart. Being rusty and being
        /// forgotten are not the same, and the game already models forgotten — absence
        /// bleeds overness. Putting rust on connection as well would have merged them, and
        /// a returning legend has to be able to be white hot and unable to go.
        /// </summary>
        [Fact]
        public void FatigueTakesTheGasAndRustTakesTheCraftAndNeitherTakesTheCrowd()
        {
            var fresh = W("Fresh");
            var tired = W("Tired");  tired.Fatigue = 90;
            var rusty = W("Rusty");  rusty.Sharpness = 10;

            var pFresh = new PerformerProfile(fresh);
            var pTired = new PerformerProfile(tired);
            var pRusty = new PerformerProfile(rusty);

            output.WriteLine($"  fresh  cond {pFresh.Conditioning:F3}  work {pFresh.Workrate:F3}  conn {pFresh.Connection:F3}");
            output.WriteLine($"  tired  cond {pTired.Conditioning:F3}  work {pTired.Workrate:F3}  conn {pTired.Connection:F3}");
            output.WriteLine($"  rusty  cond {pRusty.Conditioning:F3}  work {pRusty.Workrate:F3}  conn {pRusty.Connection:F3}");

            Assert.True(pTired.Conditioning < pFresh.Conditioning * 0.95, "fatigue takes the gas");
            Assert.Equal(pFresh.Workrate, pTired.Workrate, 3);

            Assert.True(pRusty.Workrate  < pFresh.Workrate  * 0.95, "rust takes the craft");
            Assert.True(pRusty.RingPsych < pFresh.RingPsych * 0.95);
            Assert.Equal(pFresh.Conditioning, pRusty.Conditioning, 3);

            // Neither touches the crowd.
            Assert.Equal(pFresh.Connection, pTired.Connection, 3);
            Assert.Equal(pFresh.Connection, pRusty.Connection, 3);
        }

        /// <summary>
        /// Rust reaches the style-hinted workrate too. Without it a beat carrying a
        /// StyleHint read the raw skills and routed around the meter entirely — the one
        /// path on which being ring rusty would have cost nothing.
        /// </summary>
        [Fact]
        public void RustReachesTheStyleHintedWorkrateToo()
        {
            var fresh = W("Fresh", WrestlingStyle.Technical);
            var rusty = W("Rusty", WrestlingStyle.Technical); rusty.Sharpness = 10;

            double sharpWork = new PerformerProfile(fresh).WorkrateFor(WrestlingStyle.Grappler);
            double rustyWork = new PerformerProfile(rusty).WorkrateFor(WrestlingStyle.Grappler);

            output.WriteLine($"  WorkrateFor(Grappler): sharp {sharpWork:F3}, rusty {rustyWork:F3}");
            Assert.True(rustyWork < sharpWork * 0.95);
        }

        /// <summary>
        /// **A tired wrestler has a worse match**, end to end. The meters would be
        /// bookkeeping if the rating never moved.
        /// </summary>
        [Fact]
        public void ATiredWrestlerHasAWorseMatch()
        {
            double Run(double fatigue, double sharpness)
            {
                var a = W("Alpha"); var b = W("Bravo");
                a.Fatigue = fatigue; a.Sharpness = sharpness;

                return new MatchEngine(Seed).Execute(new MatchPlan
                {
                    Sides = [new MatchSide { Members = [a] }, new MatchSide { Members = [b] }],
                    Beats = MatchStructureLibrary.Find("Big Match")!.Beats
                                                 .Select(x => x.Clone()).ToList()
                }).StarRating;
            }

            double good   = Run(0, 100);
            double cooked = Run(95, 100);
            double rusted = Run(0, 5);

            output.WriteLine($"  fresh and sharp {good:F2}   cooked {cooked:F2}   rusty {rusted:F2}");

            Assert.True(cooked < good, "a cooked wrestler should rate lower");
            Assert.True(rusted < good, "so should a rusty one");
        }

        // ── On a card, and over a calendar ───────────────────────────────────

        /// <summary>
        /// **Working a show costs, and being booked twice costs twice.**
        ///
        /// Charged per card item rather than once per night, which is the only thing
        /// stopping a booker working their main eventer three times on the same show for
        /// free.
        /// </summary>
        [Fact]
        public void WorkingAShowCostsAndBeingBookedTwiceCostsTwice()
        {
            var roster = new List<Wrestler> { W("A"), W("B"), W("C") };
            var career = NewCareer(roster);

            var show = career.Schedule("Night", career.CurrentDate, ShowType.Television);
            show.Card.Add(Match(roster[0], roster[1]));
            show.Card.Add(Match(roster[0], roster[2]));   // A works twice

            new ShowSimulator(career.FeudBook, seed: Seed).Simulate(show.ToShow());

            output.WriteLine($"  A (two matches) fatigue {roster[0].Fatigue:F1}, sharpness {roster[0].Sharpness:F1}");
            output.WriteLine($"  B (one match)   fatigue {roster[1].Fatigue:F1}, sharpness {roster[1].Sharpness:F1}");

            Assert.True(roster[1].Fatigue > 0, "working a match should cost something");
            Assert.True(roster[0].Fatigue > roster[1].Fatigue * 1.8,
                "two matches should cost about twice one");
        }

        /// <summary>
        /// Standing on the apron is a rest, and that is why tag wrestling is survivable at
        /// volume — but well short of proportional, because you are still out there taking
        /// the double team.
        /// </summary>
        [Fact]
        public void TaggingOutIsARestButNotHalfAMatchOff()
        {
            output.WriteLine($"  singles {RingCondition.WorkShare(1):F2}  " +
                             $"tag {RingCondition.WorkShare(2):F2}  trios {RingCondition.WorkShare(3):F2}");

            Assert.Equal(1.0, RingCondition.WorkShare(1), 3);
            Assert.True(RingCondition.WorkShare(2) < 1.0);
            Assert.True(RingCondition.WorkShare(2) > 0.5, "not proportional — you are still in there");
            Assert.True(RingCondition.WorkShare(3) < RingCondition.WorkShare(2));
        }

        /// <summary>
        /// The clock moves both meters, so a career that advances without booking anybody
        /// leaves them fresh and rusty rather than unchanged.
        /// </summary>
        [Fact]
        public void AdvancingTheClockMovesBothMeters()
        {
            var roster = new List<Wrestler> { W("Idle") };
            var career = NewCareer(roster);
            roster[0].Fatigue = 50;

            for (int i = 0; i < 21; i++) career.AdvanceOneDay();

            output.WriteLine($"  three weeks idle: fatigue {roster[0].Fatigue:F1}, " +
                             $"sharpness {roster[0].Sharpness:F1}");

            Assert.True(roster[0].Fatigue < 20, "three weeks off should clear most of it");
            Assert.True(roster[0].Sharpness < 95, "and cost real sharpness");
        }

        /// <summary>Both meters survive a save, or a career reload is a full night's rest.</summary>
        [Fact]
        public void TheMetersSurviveASaveAndReload()
        {
            var roster = new List<Wrestler> { W("A"), W("B") };
            var career = NewCareer(roster);
            roster[0].Fatigue = 63.5;
            roster[0].Sharpness = 41.25;

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster)
                .Roster.Single(w => w.Id == roster[0].Id);

            output.WriteLine($"  fatigue {back.Fatigue:F2}, sharpness {back.Sharpness:F2}");
            Assert.Equal(63.5, back.Fatigue, 2);
            Assert.Equal(41.25, back.Sharpness, 2);
        }

        /// <summary>
        /// A save written before the meters existed reads back as a fresh, sharp roster —
        /// the state a new career starts in — rather than a roster of exhausted rookies.
        /// </summary>
        [Fact]
        public void AnOlderSaveLoadsAsFreshAndSharp()
        {
            var roster = new List<Wrestler> { W("A") };
            var career = NewCareer(roster);
            roster[0].Fatigue = 80; roster[0].Sharpness = 20;

            // Strip the two fields, as a save written before they existed would not have them.
            string json = SaveSerializer.ToJson(career);
            json = System.Text.RegularExpressions.Regex.Replace(json, @"\s*""Fatigue"":[^,}]*,", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, @"\s*,?\s*""Sharpness"":[^,}]*", "");

            var back = SaveSerializer.FromJson(json, roster).Roster.Single();
            output.WriteLine($"  legacy save: fatigue {back.Fatigue:F0}, sharpness {back.Sharpness:F0}");

            Assert.Equal(0, back.Fatigue, 2);
            Assert.Equal(100, back.Sharpness, 2);
        }

        // ── The warning ──────────────────────────────────────────────────────

        /// <summary>
        /// The reading a booker gets. It is a warning and not a gate: a cooked wrestler is
        /// still bookable, because sometimes you have to run them.
        /// </summary>
        [Fact]
        public void TheBookerIsToldAndNotStopped()
        {
            Assert.Null(RingCondition.Warning(10, 95));
            Assert.Contains("cooked", RingCondition.Warning(85, 95)!);
            Assert.Contains("rusty",  RingCondition.Warning(10, 30)!);

            string both = RingCondition.Warning(85, 30)!;
            output.WriteLine($"  both: {both}");
            Assert.Contains("cooked", both);
            Assert.Contains("rusty",  both);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static BookedMatch Match(Wrestler a, Wrestler b) => new()
        {
            Plan = new MatchPlan
            {
                Sides = [new MatchSide { Members = [a] }, new MatchSide { Members = [b] }],
                Beats = MatchStructureLibrary.Find("TV Formula")!.Beats
                                             .Select(x => x.Clone()).ToList()
            }
        };

        private static Career NewCareer(List<Wrestler> roster)
        {
            var start = new DateOnly(2026, 1, 5);
            return new Career
            {
                Promotion   = new Promotion { Name = "Condition Wrestling" },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster
            };
        }
    }
}
