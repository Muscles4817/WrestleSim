using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Doc 15 §4 — the aging curve.
    ///
    /// The first group asserts the curve shapes against the reference. The second asserts
    /// the thing that actually keeps this change safe: that every drift is 1.000 at the
    /// roster baseline, so a save starting in the baseline year behaves exactly as it did
    /// before any of this existed.
    /// </summary>
    public class AgeCurveTests
    {
        private static Wrestler Aged(int bornIn, int debutedIn, WrestlingStyle style = WrestlingStyle.Technical)
        {
            var w = TestRoster.Make("Test Hand");
            w.BornIn    = bornIn;
            w.DebutedIn = debutedIn;
            w.Style     = style;
            return w;
        }

        // ── The curves ───────────────────────────────────────────────────────

        [Fact]
        public void AthleticismPeaksAcrossTheMidTwentiesAndFallsAfter()
        {
            // Doc 15 §4: athletic peak 24-28, declining from ~28.
            Assert.Equal(1.00, AgeCurve.Athleticism(24), 3);
            Assert.Equal(1.00, AgeCurve.Athleticism(28), 3);

            Assert.True(AgeCurve.Athleticism(20) < AgeCurve.Athleticism(24),
                "A twenty-year-old has not peaked yet.");

            foreach (int age in new[] { 32, 36, 40, 44, 48 })
                Assert.True(AgeCurve.Athleticism(age) < AgeCurve.Athleticism(age - 4),
                    $"Athleticism should still be falling at {age}.");
        }

        [Fact]
        public void RingPsychologyRisesToAboutFortyAndThenHolds()
        {
            // Doc 15 §4: psychology rising to ~40 while athleticism declines.
            Assert.True(AgeCurve.RingPsychology(22) < AgeCurve.RingPsychology(30));
            Assert.True(AgeCurve.RingPsychology(30) < AgeCurve.RingPsychology(40));
            Assert.Equal(1.00, AgeCurve.RingPsychology(40), 3);

            // It holds rather than collapsing: what a veteran loses is the body.
            Assert.True(AgeCurve.RingPsychology(50) > 0.95,
                "A fifty-year-old has not forgotten how to work.");
        }

        [Fact]
        public void TheTwoCurvesCrossWhichIsTheWholePointOfTheChapter()
        {
            // The young wrestler is the better athlete and the worse worker; the veteran is
            // the reverse. Doc 15 §4's diagram, asserted.
            Assert.True(AgeCurve.Athleticism(25)   > AgeCurve.Athleticism(42));
            Assert.True(AgeCurve.RingPsychology(25) < AgeCurve.RingPsychology(42));
        }

        [Fact]
        public void ConnectionIsTenureAndTakesAboutADecade()
        {
            // Doc 15 §4.1: drawing power is accumulated audience relationship.
            Assert.True(AgeCurve.Connection(1) < AgeCurve.Connection(5));
            Assert.True(AgeCurve.Connection(5) < AgeCurve.Connection(12));
            Assert.Equal(1.00, AgeCurve.Connection(12), 3);

            // And it cannot be hurried past that.
            Assert.Equal(AgeCurve.Connection(12), AgeCurve.Connection(25), 3);
        }

        [Fact]
        public void StyleAdjustsHowSteeplyTheBodyFallsOff()
        {
            // Doc 15 §4.2: technical ages extremely well, high-flying ages badly.
            double techAt42 = AgeCurve.Athleticism(42, WrestlingStyle.Technical);
            double flyAt42  = AgeCurve.Athleticism(42, WrestlingStyle.HighFlyer);
            double powAt42  = AgeCurve.Athleticism(42, WrestlingStyle.Powerhouse);

            Assert.True(techAt42 > powAt42, "Technical should age better than powerhouse.");
            Assert.True(powAt42  > flyAt42, "Powerhouse should age better than high-flying.");
        }

        [Fact]
        public void StyleDoesNotTouchThePeakOrTheYoungEnd()
        {
            // Being twenty-five is not a style problem, and no style is better than peak.
            foreach (var style in new[] { WrestlingStyle.Technical, WrestlingStyle.HighFlyer })
            {
                Assert.Equal(AgeCurve.Athleticism(22), AgeCurve.Athleticism(22, style), 6);
                Assert.Equal(1.00, AgeCurve.Athleticism(26, style), 3);
            }
        }

        [Fact]
        public void InjuryRiskIsAUNotARamp()
        {
            // Doc 15 §3.1 gives over-35 a 1.5-2x multiplier; §4's band table gives the
            // 18-23s the highest rate of all from inexperience. Both ends, not one.
            double young = AgeCurve.InjuryMultiplier(19);
            double prime = AgeCurve.InjuryMultiplier(27);
            double old   = AgeCurve.InjuryMultiplier(44);

            Assert.True(young > prime, "The raw rookie should be more fragile than the prime.");
            Assert.True(old   > prime, "The veteran should be more fragile than the prime.");
            Assert.InRange(AgeCurve.InjuryMultiplier(40), 1.5, 2.0);
        }

        [Fact]
        public void RecoverySlowsWithAgeAndNeverSpeedsUp()
        {
            foreach (int age in new[] { 28, 34, 40, 46, 52 })
                Assert.True(AgeCurve.RecoveryMultiplier(age) < AgeCurve.RecoveryMultiplier(age - 6),
                    $"Recovery should be slower at {age} than six years earlier.");
        }

        // ── The safety property ──────────────────────────────────────────────

        [Fact]
        public void EveryDriftIsExactlyOneAtTheRosterBaseline()
        {
            // The property this whole change rests on. The shipped roster's attributes were
            // authored with doc 15 §4 already applied by hand, so a save starting in the
            // baseline year must read them unmodified or the curve prices age twice.
            int year = AgeCurve.RosterBaselineYear;

            foreach (var w in DataLoaders.LoadEmbeddedWrestlers())
            {
                Assert.Equal(1.0, AgeCurve.AthleticismDrift(w.BornIn, year, w.Style), 9);
                Assert.Equal(1.0, AgeCurve.RingPsychologyDrift(w.BornIn, year), 9);
                Assert.Equal(1.0, AgeCurve.ConnectionDrift(w.DebutedIn, year), 9);
            }
        }

        [Fact]
        public void AWrestlerWithNoBirthYearNeverAges()
        {
            // Missing data degrades to the old behaviour, not to a guess.
            var unknown = TestRoster.Make("No Papers");
            Assert.Null(unknown.BornIn);

            foreach (int year in new[] { 2026, 2036, 2056 })
            {
                Assert.Equal(1.0, AgeCurve.AthleticismDrift(unknown.BornIn, year, unknown.Style), 9);
                Assert.Equal(1.0, AgeCurve.RingPsychologyDrift(unknown.BornIn, year), 9);
                Assert.Equal(1.0, AgeCurve.ConnectionDrift(unknown.DebutedIn, year), 9);
            }
        }

        [Fact]
        public void TenYearsOnTheBodyHasGoneAndTheHeadHasNot()
        {
            var w = Aged(bornIn: 1990, debutedIn: 2010);   // 36 at baseline, 46 ten years on
            int later = AgeCurve.RosterBaselineYear + 10;

            Assert.True(AgeCurve.AthleticismDrift(w.BornIn, later, w.Style) < 1.0,
                "A 46-year-old should be less athletic than they were at 36.");
            Assert.True(AgeCurve.RingPsychologyDrift(w.BornIn, later) >= 1.0,
                "Psychology should not have gone backwards between 36 and 46.");
        }

        [Fact]
        public void AYoungWrestlerGetsBetterNotWorse()
        {
            var w = Aged(bornIn: 2004, debutedIn: 2022, style: WrestlingStyle.HighFlyer);
            int later = AgeCurve.RosterBaselineYear + 4;   // 22 -> 26

            Assert.True(AgeCurve.AthleticismDrift(w.BornIn, later, w.Style) > 1.0,
                "Twenty-two to twenty-six is the body improving.");
            Assert.True(AgeCurve.RingPsychologyDrift(w.BornIn, later) > 1.0,
                "And the head improving faster.");
            Assert.True(AgeCurve.ConnectionDrift(w.DebutedIn, later) > 1.0,
                "And the audience getting to know them.");
        }

        // ── Through the engine ───────────────────────────────────────────────

        [Fact]
        public void ThePerformerProfileIsUnmovedWithoutADate()
        {
            // Every existing caller passes no date, so every existing reading must be
            // untouched. Guards the thousand tests that predate this.
            var w = Aged(bornIn: 1978, debutedIn: 1998);

            var undated = new PerformerProfile(w);
            var baseline = new PerformerProfile(w, new DateOnly(AgeCurve.RosterBaselineYear, 6, 1));

            Assert.Equal(undated.Athleticism, baseline.Athleticism, 9);
            Assert.Equal(undated.RingPsych,   baseline.RingPsych,   9);
            Assert.Equal(undated.Connection,  baseline.Connection,  9);
        }

        [Fact]
        public void ThePerformerProfileAgesWhenTheClockRuns()
        {
            var w = Aged(bornIn: 1986, debutedIn: 2006, style: WrestlingStyle.HighFlyer);

            var now   = new PerformerProfile(w, new DateOnly(AgeCurve.RosterBaselineYear, 6, 1));
            var later = new PerformerProfile(w, new DateOnly(AgeCurve.RosterBaselineYear + 12, 6, 1));

            Assert.True(later.Athleticism < now.Athleticism,
                "A high-flyer from 40 to 52 should have lost a great deal.");

            // Psychology does drift down past its peak — doc 15 §4's chart turns both
            // non-athletic lines down at the far right — but it is nearly flat next to the
            // body. That ratio is the claim of the chapter, not that psychology is immortal.
            double athleticLoss = 1.0 - later.Athleticism / now.Athleticism;
            double psychLoss    = 1.0 - later.RingPsych   / now.RingPsych;

            Assert.True(psychLoss < athleticLoss / 4.0,
                $"Psychology lost {psychLoss:P1} against the body's {athleticLoss:P1}; " +
                 "the head is supposed to outlast the body by a wide margin.");
        }

        [Fact]
        public void AgeChargesTheInjuryRollAndOnlyWithADate()
        {
            var veteran = Aged(bornIn: 1982, debutedIn: 2002);   // 44 at baseline
            var prime   = Aged(bornIn: 1998, debutedIn: 2018);   // 28 at baseline
            var date    = new DateOnly(AgeCurve.RosterBaselineYear, 6, 1);

            Assert.True(InjuryRisk.AgeRisk(veteran, date) > InjuryRisk.AgeRisk(prime, date),
                "Doc 15 §3.1: over-35 carries 1.5-2x the risk.");

            // And nothing at all without a date, so untouched callers are untouched.
            Assert.Equal(1.0, InjuryRisk.AgeRisk(veteran, null), 9);
        }

        [Fact]
        public void AnOlderBodyPaysFatigueBackSlower()
        {
            double young = RingCondition.RecoveryPerDay(60, 1.0, 26);
            double old   = RingCondition.RecoveryPerDay(60, 1.0, 46);
            double noAge = RingCondition.RecoveryPerDay(60, 1.0, null);

            Assert.True(old < young, "A 46-year-old should recover slower than a 26-year-old.");
            Assert.Equal(RingCondition.RecoveryPerDay(60, 1.0), noAge, 9);
        }

        // ── The roster data ──────────────────────────────────────────────────

        [Fact]
        public void TheRosterAgesAreInternallyPlausible()
        {
            int year = AgeCurve.RosterBaselineYear;

            foreach (var w in DataLoaders.LoadEmbeddedWrestlers())
            {
                if (w.BornIn is null)
                {
                    // A debut with no birth year would age the connection and not the
                    // body, which is a half-aged wrestler and worse than an unaged one.
                    Assert.Null(w.DebutedIn);
                    continue;
                }

                int age = w.AgeOn(new DateOnly(year, 6, 1))!.Value;
                Assert.InRange(age, 18, 60);

                // A birth year without a debut year is allowed, and is the honest state
                // for somebody whose age is documented and whose first match is not.
                if (w.DebutedIn is null) continue;

                int debutAge = w.DebutedIn.Value - w.BornIn.Value;
                Assert.InRange(debutAge, 14, 40);
                Assert.True(w.DebutedIn <= year, $"{w.RealName} cannot debut in the future.");
            }
        }

        [Fact]
        public void MostOfTheRosterHasAnAge()
        {
            // Not all of it. Two entries are deliberately blank: one performer has no
            // published birth year at all, and one has two reports a month apart giving
            // different ages. Guessing either would put a false claim about a real person
            // into the repo to move a curve by a fraction of a percent.
            //
            // This pins the coverage so a roster edit that drops ages wholesale is noticed.
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            int withAge = roster.Count(w => w.BornIn is not null);

            Assert.True(withAge >= 74,
                $"Only {withAge} of {roster.Count} wrestlers have a birth year.");
        }

        [Fact]
        public void TheRosterSpansTheBandsDocFifteenDescribes()
        {
            // A curve nobody is positioned on is decoration. There has to be somebody in
            // the prime, somebody past it, and somebody still climbing.
            var date = new DateOnly(AgeCurve.RosterBaselineYear, 6, 1);
            var ages = DataLoaders.LoadEmbeddedWrestlers()
                .Select(w => w.AgeOn(date))
                .Where(a => a is not null)
                .Select(a => a!.Value)
                .ToList();

            Assert.Contains(ages, a => a < 29);              // still climbing
            Assert.Contains(ages, a => a is >= 29 and < 36);  // the prime
            Assert.Contains(ages, a => a >= 40);              // past it
        }
    }
}
