using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A3 — feud decay and the terminal blow-off.
    ///
    /// Before this, heat was a ratchet. Every segment ever booked was still paying off
    /// months later, nothing ever cooled, and there was no cost whatever to starting five
    /// programmes and finishing none — which is precisely the booking
    /// docs/wrestling-reference/20-storylines-and-feuds.md §9 spends a section warning
    /// about. Three rules go in:
    ///
    ///   • **Neglect costs.** A feud nobody is telling bleeds heat (§9: "left off TV for
    ///     three weeks loses its heat").
    ///   • **Not paying off costs.** Past the third match with nothing settled the crowd
    ///     stops believing the story is going anywhere, and that belief is durable — §9's
    ///     interference loop, where every match ends in a run-in and nothing resolves.
    ///   • **Paying off pays.** A blow-off is worth what was built (§6, §5) — and a
    ///     blow-off declared on a story nobody was told mattered is worth *less* than not
    ///     declaring one, the same shape as the unearned finish and the unearned hot tag.
    /// </summary>
    public class FeudDecayTests(ITestOutputHelper output)
    {
        private static readonly DateOnly Day0 = new(2026, 1, 1);

        private static Feud Hot(double heat = 60)
        {
            var f = new Feud
            {
                SideA = [TestRoster.Make("A")],
                SideB = [TestRoster.Make("B")],
                Intensity = FeudIntensity.None
            };
            f.AddHeat(heat);
            f.Advance(Day0);
            return f;
        }

        // ── Decay ────────────────────────────────────────────────────────────

        [Fact]
        public void AFeudLeftAlone_LosesHeat_AndAFeudBeingToldDoesNot()
        {
            var neglected = Hot();
            var told      = Hot();

            for (int day = 1; day <= 60; day++)
            {
                var date = Day0.AddDays(day);
                neglected.ApplyDailyDecay(date);

                // Told about every fortnight — which is what "on television" means.
                if (day % 14 == 0) told.Advance(date);
                told.ApplyDailyDecay(date);
            }

            output.WriteLine($"  after 60 days: neglected {neglected.Heat:F1} " +
                             $"({neglected.Intensity}), on TV {told.Heat:F1} ({told.Intensity})");

            Assert.True(neglected.Heat < 30,
                $"A feud ignored for two months still has {neglected.Heat:F1} heat.");
            Assert.Equal(60, told.Heat, 6);
        }

        [Fact]
        public void TheGracePeriod_IsRealAndIsNotCharged()
        {
            var feud = Hot();

            // Ticked every day *inside* the grace, which is what the world clock actually
            // does. The first version only called on the last day of the grace, where
            // `today == from` and stamping the marker is harmless — so it passed with the
            // marker stamped mid-grace and passed with the marker removed entirely, which
            // is precisely the bug its own comment claimed it existed to catch. Review
            // applied both mutations and watched it stay green.
            for (int day = 1; day <= Feud.HeatGraceDays; day++)
                feud.ApplyDailyDecay(Day0.AddDays(day));

            Assert.Equal(60, feud.Heat, 6);

            // One day past the grace charges exactly one day — not fifteen. Stamping the
            // decay marker while still inside the grace would make the next call measure
            // from today rather than from the end of the grace, billing the grace itself.
            // That is the bug TagTeam.Decay shipped with and it is the same shape here.
            feud.ApplyDailyDecay(Day0.AddDays(Feud.HeatGraceDays + 1));
            double oneDay = 60 * Feud.HeatDailyRetention;
            output.WriteLine($"  one day past grace: {feud.Heat:F4} (expected {oneDay:F4})");
            Assert.Equal(oneDay, feud.Heat, 6);
        }

        [Fact]
        public void DecayIsIdempotentPerDay_SoTheClockCannotDoubleBill()
        {
            var once  = Hot();
            var often = Hot();

            for (int day = 1; day <= 90; day++)
            {
                var date = Day0.AddDays(day);
                once.ApplyDailyDecay(date);
                // The world clock is not the only thing that can tick a day.
                often.ApplyDailyDecay(date);
                often.ApplyDailyDecay(date);
                often.ApplyDailyDecay(date);
            }

            output.WriteLine($"  charged once {once.Heat:F4}, charged three times {often.Heat:F4}");
            Assert.Equal(once.Heat, often.Heat, 9);

            // And the rate is geometric in days, not in days-squared — the quadratic
            // compounding TagTeam shipped with gave a thirty-day half-life on a curve
            // written for two years.
            double expected = 60 * Math.Pow(Feud.HeatDailyRetention, 90 - Feud.HeatGraceDays);
            Assert.Equal(expected, once.Heat, 6);
        }

        [Fact]
        public void AFeudThatHasNeverBeenAdvanced_DoesNotDecay()
        {
            // Nothing has happened, so there is no "since when" to measure from. This is
            // the pre-A3 save case: LastAdvanced falls back to LastMatchDate on load, and
            // a pairing with neither has no heat worth decaying anyway.
            var feud = new Feud { SideA = [TestRoster.Make("A")], SideB = [TestRoster.Make("B")] };
            feud.AddHeat(40);

            feud.ApplyDailyDecay(Day0.AddDays(400));
            Assert.Equal(40, feud.Heat, 6);
        }

        // ── Distrust ─────────────────────────────────────────────────────────

        [Fact]
        public void ThreeMatchesAreFree_AndTheFourthStartsCostingCredibility()
        {
            var feud = Hot(70);
            Assert.True(feud.Intensity >= FeudIntensity.Hot, "setup: needs to be Hot");

            for (int i = 1; i <= 3; i++)
            {
                feud.RecordUnresolved();
                output.WriteLine($"  match {i}: distrust {feud.Distrust:F2}, " +
                                 $"credibility {feud.Credibility:F3}");
                Assert.Equal(0, feud.Distrust);
            }

            feud.RecordUnresolved();
            output.WriteLine($"  match 4: distrust {feud.Distrust:F2}, " +
                             $"credibility {feud.Credibility:F3}");
            Assert.True(feud.Distrust > 0, "The fourth unresolved match cost nothing.");
            Assert.True(feud.Credibility < 1.0);
        }

        [Fact]
        public void DistrustSuppressesWhatTheFeudPutsInTheRoom()
        {
            var clean   = Hot(70);
            var abused  = Hot(70);

            for (int i = 0; i < 8; i++) abused.RecordUnresolved();

            output.WriteLine($"  clean  {clean.Intensity}: {clean.StartingEnergyBonus:F2} " +
                             $"(credibility {clean.Credibility:F2})");
            output.WriteLine($"  abused {abused.Intensity}: {abused.StartingEnergyBonus:F2} " +
                             $"(credibility {abused.Credibility:F2})");

            Assert.Equal(clean.Intensity, abused.Intensity);
            Assert.True(abused.StartingEnergyBonus < clean.StartingEnergyBonus * 0.9,
                "Eight matches that settled nothing cost the pairing almost nothing.");
        }

        [Fact]
        public void AFeudThatIsNotHot_CannotAccrueDistrust()
        {
            // The clock measures refusing to pay off a story the crowd is invested in.
            // A cold pairing having matches is not that.
            var cold = new Feud { SideA = [TestRoster.Make("A")], SideB = [TestRoster.Make("B")] };
            cold.AddHeat(5);
            for (int i = 0; i < 10; i++) cold.RecordUnresolved();

            Assert.Equal(0, cold.Distrust);
            Assert.Equal(0, cold.MatchesSinceHot);
        }

        [Fact]
        public void AFeudThatCoolsOff_ResetsThePatienceClock()
        {
            var feud = Hot(70);
            feud.RecordUnresolved();
            feud.RecordUnresolved();
            Assert.Equal(2, feud.MatchesSinceHot);

            // Left alone until it drops out of Hot.
            for (int day = 1; day <= 200; day++) feud.ApplyDailyDecay(Day0.AddDays(day));

            output.WriteLine($"  after 200 days: {feud.Heat:F1} heat, {feud.Intensity}, " +
                             $"matchesSinceHot {feud.MatchesSinceHot}");
            Assert.True(feud.Intensity < FeudIntensity.Hot, "setup: should have cooled");
            Assert.Equal(0, feud.MatchesSinceHot);
        }

        // ── The blow-off ─────────────────────────────────────────────────────

        [Fact]
        public void ABlowOff_SettlesTheStoryAndSpendsTheHeat()
        {
            var feud = Hot(80);
            feud.RecordUnresolved();
            feud.RecordUnresolved();
            feud.RecordUnresolved();
            feud.RecordUnresolved();
            double distrustBefore = feud.Distrust;

            feud.BlowOff(Day0.AddDays(30));

            Assert.True(feud.Concluded);
            Assert.Equal(0, feud.Heat);
            Assert.Equal(FeudIntensity.None, feud.Intensity);
            Assert.True(feud.Distrust < distrustBefore,
                "Actually finishing a story should earn some belief back.");
        }

        [Fact]
        public void ASettledFeud_CanBeStartedAgain_ButCarriesWhatTheCrowdLearned()
        {
            var feud = Hot(80);
            for (int i = 0; i < 6; i++) feud.RecordUnresolved();
            double taught = feud.Distrust;
            Assert.True(taught > 0, "setup: needs distrust");

            feud.BlowOff(Day0.AddDays(30));
            Assert.True(feud.Concluded);

            // A year later, they start again. GetOrCreate hands back the same object
            // forever, so without this a pairing that ever finished a programme could
            // never have another one.
            feud.AddHeat(50);

            output.WriteLine($"  reopened at {feud.Heat:F0} heat, {feud.Intensity}, " +
                             $"chapter {feud.ChaptersSettled + 1}, distrust {feud.Distrust:F2}");

            Assert.False(feud.Concluded);
            Assert.Equal(1, feud.ChaptersSettled);
            Assert.Equal(0, feud.MatchesSinceHot);
            Assert.True(feud.Distrust > 0,
                "What the booker taught the crowd should outlive the story it was taught by.");
        }

        /// <summary>
        /// Doc 31's A3 brief asks for this in as many words — *"continuing past the blow-off
        /// should be penalised"* — and the first version made it free, which is worse than
        /// having no blow-off at all: it let a booker take the payoff and keep the programme.
        ///
        /// Time-sensitive, because the two cases are genuinely different. Restarting a
        /// fortnight after the cage match tells the audience the ending they were sold did
        /// not count (doc 20 §6.2's scarcity argument). Reviving the same rivalry two years
        /// later is one of the oldest and best things in wrestling.
        /// </summary>
        [Fact]
        public void RestartingRightAfterTheBlowOff_Costs_AndAGenuineRevivalDoesNot()
        {
            Feud Settled(DateOnly on)
            {
                var f = Hot(80);
                f.BlowOff(on);
                return f;
            }

            var tooSoon = Settled(Day0);
            tooSoon.Advance(Day0.AddDays(21));
            tooSoon.AddHeat(50);

            var revival = Settled(Day0);
            revival.Advance(Day0.AddDays(Feud.RespectTheEndingDays + 30));
            revival.AddHeat(50);

            output.WriteLine($"  restarted after 21 days:  distrust {tooSoon.Distrust:F2}, " +
                             $"credibility {tooSoon.Credibility:F2}");
            output.WriteLine($"  revived after 7 months:   distrust {revival.Distrust:F2}, " +
                             $"credibility {revival.Credibility:F2}");

            Assert.True(tooSoon.Distrust > revival.Distrust,
                "Carrying on a fortnight after the blow-off should cost what a revival does not.");
            Assert.Equal(0, revival.Distrust);
            Assert.False(tooSoon.Concluded);
            Assert.False(revival.Concluded);
            Assert.Equal(1, tooSoon.ChaptersSettled);
        }

        /// <summary>
        /// The same rule through `FeudBook.Record`, which is how the game actually books.
        ///
        /// Round 2 caught this as the round-1 pattern in miniature: the test above drives
        /// `Advance` and `AddHeat` on the model in the right order, so reverting the
        /// *reorder* in `FeudBook.Record` — which is what makes the rule read today's date
        /// rather than the blow-off's — left all 443 green. With `AddHeat` first, a
        /// two-year revival is charged as though it were a fortnight.
        /// </summary>
        [Fact]
        public void ARevivalBookedThroughTheFeudBook_IsNotChargedAsAContinuation()
        {
            var book = new FeudBook();
            var a = TestRoster.Make("Face");
            var b = TestRoster.Make("Heel");

            var feud = book.GetOrCreate(a, b);
            feud.SetMinimumIntensity(FeudIntensity.Nuclear);
            feud.BlowOff(Day0);

            // Two years later, somebody books a segment between them.
            book.Record(a, b, heat: 30, date: Day0.AddDays(730));

            output.WriteLine($"  revived after two years: distrust {feud.Distrust:F2}, " +
                             $"chapter {feud.ChaptersSettled + 1}");

            Assert.False(feud.Concluded);
            Assert.Equal(0, feud.Distrust);

            // And the same book, restarting a settled programme a fortnight later, does pay.
            var soon = book.GetOrCreate(TestRoster.Make("F2"), TestRoster.Make("H2"));
            soon.SetMinimumIntensity(FeudIntensity.Nuclear);
            soon.BlowOff(Day0);
            book.Record(soon.SideA, soon.SideB, heat: 30, date: Day0.AddDays(14));

            output.WriteLine($"  restarted after a fortnight: distrust {soon.Distrust:F2}");
            Assert.True(soon.Distrust > 0);
        }

        [Fact]
        public void ABlowOffIsWorthWhatWasBuilt_AndAnUnearnedOneIsWorthLessThanNothing()
        {
            foreach (var i in new[] { FeudIntensity.Cold, FeudIntensity.Building,
                                      FeudIntensity.Hot, FeudIntensity.Nuclear })
                output.WriteLine($"  {i,-9} ×{Feud.PayoffFor(i):F2}");

            Assert.True(Feud.PayoffFor(FeudIntensity.Nuclear) > Feud.PayoffFor(FeudIntensity.Hot));
            Assert.True(Feud.PayoffFor(FeudIntensity.Hot) > Feud.PayoffFor(FeudIntensity.Building));
            Assert.True(Feud.PayoffFor(FeudIntensity.Cold) < 1.0,
                "A blow-off on a story nobody was told mattered should be a penalty, not a bonus.");
        }

        // ── Through the engine, not just the model ───────────────────────────

        [Fact]
        public void DeclaringABlowOff_MovesTheMatch_AndOnlyTheFinish()
        {
            var (plain, blown) = (RunBlowOff(false), RunBlowOff(true));

            output.WriteLine($"  chapter  {plain.Stars:F3}  finish crowd {plain.FinishCrowd:F2}");
            output.WriteLine($"  blow-off {blown.Stars:F3}  finish crowd {blown.FinishCrowd:F2}");

            Assert.True(blown.Stars > plain.Stars,
                "A Nuclear feud settled should out-rate the same match as another chapter.");
            Assert.True(blown.FinishCrowd > plain.FinishCrowd);

            // Every beat before the finish should be untouched — the payoff is the payoff,
            // not a blanket bonus on the whole match.
            Assert.Equal(plain.BeforeFinish, blown.BeforeFinish, 9);
        }

        [Fact]
        public void AnUnearnedBlowOff_RatesBelowNotDeclaringOne()
        {
            var chapter  = RunBlowOff(false, FeudIntensity.Cold);
            var unearned = RunBlowOff(true,  FeudIntensity.Cold);

            output.WriteLine($"  cold chapter  {chapter.Stars:F3}");
            output.WriteLine($"  cold blow-off {unearned.Stars:F3}");

            Assert.True(unearned.Stars < chapter.Stars,
                "Settling a story the audience was never told mattered should cost something.");
        }

        [Fact]
        public void ABlowOffNeedsAFeud_AndAStoryCannotEndTwice()
        {
            var plan = Bout(null);
            plan.IsBlowOff = true;
            Assert.Contains(plan.Validate(), e => e.Contains("no feud"));

            var settled = Hot(80);
            settled.BlowOff(Day0);
            var second = Bout(settled);
            second.IsBlowOff = true;
            Assert.Contains(second.Validate(), e => e.Contains("already been blown off"));
        }

        [Fact]
        public void ABlowOffThatDoesNotResolve_SettlesNothingAndCostsDouble()
        {
            var kept   = Hot(80);
            var broken = Hot(80);

            kept.BlowOff(Day0);
            broken.RecordBrokenPromise();

            output.WriteLine($"  kept:   concluded {kept.Concluded}, distrust {kept.Distrust:F2}");
            output.WriteLine($"  broken: concluded {broken.Concluded}, distrust {broken.Distrust:F2}");

            Assert.True(kept.Concluded);
            Assert.False(broken.Concluded);
            Assert.True(broken.Heat > 0, "A broken promise leaves the story open, heat and all.");

            // And it costs more than an ordinary unfinished match, which is what makes
            // declaring a blow-off a decision rather than a free bonus.
            var ordinary = Hot(80);
            for (int i = 0; i <= Feud.PatienceMatches; i++) ordinary.RecordUnresolved();

            output.WriteLine($"  one broken promise {broken.Distrust:F2} vs four ordinary " +
                             $"unresolved {ordinary.Distrust:F2}");
            Assert.True(broken.Distrust > ordinary.Distrust);
        }

        /// <summary>
        /// End to end, through the show simulator rather than the model — because the
        /// blow-off rule is only real if the thing that actually runs shows applies it.
        /// </summary>
        [Fact]
        public void AShowAppliesTheBlowOffRule_BothWays()
        {
            foreach (var (finishBeat, shouldSettle) in new[]
                     { ("Clean Victory", true), ("DQ Finish", false) })
            {
                var book = new FeudBook();
                var a = TestRoster.Make("Face", overness: 80);
                var b = TestRoster.Make("Heel", overness: 78);

                var feud = book.GetOrCreate(a, b);
                feud.SetMinimumIntensity(FeudIntensity.Nuclear);

                var beats = MatchStructureLibrary.Find("TV Formula")!
                                .Beats.Select(x => x.Clone()).ToList();
                beats[^1] = BeatLibrary.Find(finishBeat)!.ToMatchBeat(BeatControl.WrestlerA);

                var show = new Show
                {
                    Name = "Blow-off Night",
                    Date = new DateTime(2026, 1, 1),
                    TotalDurationMinutes = 180,
                    Card =
                    [
                        new BookedMatch
                        {
                            StructureName = "TV Formula",
                            Plan = new MatchPlanModel
                            {
                                WrestlerA = a, WrestlerB = b,
                                Feud = feud, IsBlowOff = true, Beats = beats
                            }
                        }
                    ]
                };

                new ShowSimulator(book, seed: 7).Simulate(show);

                output.WriteLine($"  {finishBeat,-24} concluded={feud.Concluded} " +
                                 $"heat={feud.Heat:F0} distrust={feud.Distrust:F2}");
                Assert.Equal(shouldSettle, feud.Concluded);
                if (!shouldSettle)
                    Assert.True(feud.Distrust > 0,
                        "A blow-off that settled nothing should have cost something.");
            }
        }

        // ── Wired in, not just implemented ───────────────────────────────────
        //
        // Review mutation-tested this branch and found four survivors, every one of them a
        // *hookup* rather than a rule: the decay call in the world clock, the
        // RecordUnresolved call in the show simulator, Advance() clearing the decay marker,
        // and the pre-A3 save fallback. Each could be deleted with 436 tests still green,
        // because every test above drives the model directly. A mechanism nothing calls is
        // not a mechanism, and these four are what make the difference between a feature
        // and a class.

        /// <summary>
        /// The world clock actually cools feuds. `TitleShowTests` already runs 150 days of
        /// `AdvanceOneDay` to prove title drift is wired in; this is the same guard for the
        /// mechanism A3 is named after, and it was missing.
        /// </summary>
        [Fact]
        public void TheWorldClock_CoolsAFeudNobodyIsTelling()
        {
            var career = CareerWithFeud(out var feud);
            feud.AddHeat(60);
            feud.Advance(career.CurrentDate);

            double before = feud.Heat;
            for (int i = 0; i < 60; i++) career.AdvanceOneDay();

            output.WriteLine($"  {before:F1} heat → {feud.Heat:F1} after 60 days of the clock " +
                             $"({feud.Intensity})");

            Assert.True(feud.Heat < before * 0.2,
                $"Sixty days of the world clock left the feud at {feud.Heat:F1} of {before:F1}. " +
                "ApplyDailyDecay is not being called.");
        }

        /// <summary>
        /// And the clock respects the grace, so a feud advanced every fortnight never cools —
        /// which is the half of the rule that stops it being a flat decay.
        /// </summary>
        [Fact]
        public void TheWorldClock_LeavesAFeudAloneWhileItIsBeingTold()
        {
            var career = CareerWithFeud(out var feud);
            feud.AddHeat(60);

            for (int i = 0; i < 60; i++)
            {
                career.AdvanceOneDay();
                if (i % 10 == 0) feud.Advance(career.CurrentDate);
            }

            output.WriteLine($"  told every ten days: {feud.Heat:F1} heat after 60 ({feud.Intensity})");
            Assert.Equal(60, feud.Heat, 6);
        }

        /// <summary>
        /// A show that settles nothing charges the pairing for it. This is the only place
        /// distrust accrues in career play, and it could be deleted with a green suite.
        /// </summary>
        [Fact]
        public void AShowThatSettlesNothing_ChargesThePairingForIt()
        {
            var book = new FeudBook();
            var a = TestRoster.Make("Face", overness: 80);
            var b = TestRoster.Make("Heel", overness: 78);

            var feud = book.GetOrCreate(a, b);
            feud.SetMinimumIntensity(FeudIntensity.Nuclear);

            for (int night = 1; night <= 6; night++)
            {
                var show = new Show
                {
                    Name = $"Night {night}",
                    Date = new DateTime(2026, 1, 1).AddDays(night * 7),
                    TotalDurationMinutes = 180,
                    Card =
                    [
                        new BookedMatch
                        {
                            StructureName = "TV Formula",
                            Plan = new MatchPlanModel
                            {
                                WrestlerA = a, WrestlerB = b, Feud = feud,
                                Beats = MatchStructureLibrary.Find("TV Formula")!
                                            .Beats.Select(x => x.Clone()).ToList()
                            }
                        }
                    ]
                };
                new ShowSimulator(book, seed: night).Simulate(show);
                output.WriteLine($"  after night {night}: matchesSinceHot {feud.MatchesSinceHot}, " +
                                 $"distrust {feud.Distrust:F2}");
            }

            Assert.True(feud.MatchesSinceHot >= 6, "The show simulator is not counting matches.");
            Assert.True(feud.Distrust > 0,
                "Six shows and nothing settled cost the pairing nothing — RecordUnresolved " +
                "is not being called.");
        }

        /// <summary>
        /// Telling the story again restarts the decay clock rather than resuming where it
        /// left off. Without this a feud ignored for a month and then re-booked keeps its
        /// old marker: review measured it losing a further 47.5% of what remained, and the
        /// grace period silently skipped for good.
        /// </summary>
        [Fact]
        public void AdvancingAFeud_RestartsTheClockRatherThanResumingIt()
        {
            var resumed = Hot(60);
            var restarted = Hot(60);

            // Both ignored for forty days.
            for (int d = 1; d <= 40; d++)
            {
                resumed.ApplyDailyDecay(Day0.AddDays(d));
                restarted.ApplyDailyDecay(Day0.AddDays(d));
            }
            Assert.Equal(resumed.Heat, restarted.Heat, 9);

            // Then one is told again. Its grace should start over from that night.
            restarted.Advance(Day0.AddDays(40));

            for (int d = 41; d <= 54; d++)
            {
                resumed.ApplyDailyDecay(Day0.AddDays(d));
                restarted.ApplyDailyDecay(Day0.AddDays(d));
            }

            output.WriteLine($"  a fortnight later: never re-told {resumed.Heat:F2}, " +
                             $"re-told on day 40 {restarted.Heat:F2}");

            Assert.True(restarted.Heat > resumed.Heat * 1.2,
                $"Re-booking a cold feud bought it nothing: {restarted.Heat:F2} vs " +
                $"{resumed.Heat:F2}. Advance() is not clearing the decay marker.");
        }

        private static Career CareerWithFeud(out Feud feud)
        {
            var a = TestRoster.Make("Face", overness: 80);
            var b = TestRoster.Make("Heel", overness: 78);
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Decay Wrestling", Tier = PromotionTier.National },
                StartDate   = Day0,
                CurrentDate = Day0,
                Roster      = [a, b]
            };
            feud = career.FeudBook.GetOrCreate(a, b);
            return career;
        }

        private sealed record Run(double Stars, double FinishCrowd, double BeforeFinish);

        private static MatchPlanModel Bout(Feud? feud) => new()
        {
            WrestlerA = TestRoster.Make("Face", overness: 80, charisma: 4.0, skill: 3.8),
            WrestlerB = TestRoster.Make("Heel", overness: 78, charisma: 4.0, skill: 3.8),
            MatchType = MatchType.Standard,
            Feud      = feud,
            Beats     = MatchStructureLibrary.Find("Big Match Epic")!
                            .Beats.Select(b => b.Clone()).ToList()
        };

        private static Run RunBlowOff(bool declared, FeudIntensity at = FeudIntensity.Nuclear)
        {
            var feud = new Feud
            {
                SideA = [TestRoster.Make("A")], SideB = [TestRoster.Make("B")],
                Intensity = FeudIntensity.None
            };
            feud.SetMinimumIntensity(at);

            var plan = Bout(feud);
            plan.IsBlowOff = declared;

            var r = new MatchEngine(20260906).Execute(plan);
            var finish = r.BeatResults.Last();

            return new Run(
                r.StarRating,
                finish.CrowdEnergyDelta,
                r.BeatResults.Take(r.BeatResults.Count - 1).Sum(b => b.CrowdEnergyDelta));
        }
    }
}
