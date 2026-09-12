using Xunit;
using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **The reps the sharpness meter was always asking for.**
    ///
    /// `RingCondition` says most of the roster needs more than one match a week to hold a
    /// professional reading, and until the loop existed the only way to get a match was a
    /// televised one on a card booked beat by beat. A booker who wanted somebody back to match
    /// fitness had nothing to do it with except put them on television before they were ready,
    /// which is the exact mistake the meter exists to describe.
    ///
    /// What a loop is *not* is the other half. Doc 06 defines a house show as the night the
    /// television audience was not at, so nothing consequential happens on it: no overness, no
    /// momentum, no feud heat, no titles, and no appearance against the absence clock.
    /// </summary>
    public class HouseShowLoopTests(ITestOutputHelper output)
    {
        private static readonly DateOnly Day = new(2026, 3, 2);

        private static Wrestler Worker(string name, double sharpness = 40, double fatigue = 0)
        {
            var w = TestRoster.Make(name);
            w.Sharpness = sharpness;
            w.Fatigue   = fatigue;
            return w;
        }

        private static HouseShowLoop Run(int towns, params Wrestler[] cast) => new()
        {
            Cast = cast.ToList(), Towns = towns, MinutesPerNight = 12, Pace = BeatIntensity.Medium
        };

        // ── What it is for ───────────────────────────────────────────────────

        /// <summary>
        /// **A run of towns is how a rusty wrestler gets sharp.** Three nights move somebody
        /// further than the one weekly television match they would otherwise have had, which
        /// is the whole reason the mechanic exists.
        /// </summary>
        [Fact]
        public void ARunOfTownsGetsSomebodySharp()
        {
            var road = Worker("On the road");
            var tv   = Worker("Television only");

            new LoopSimulator(seed: 1).Run(Run(3, road), Day);

            double upkeep = RingCondition.SelfMaintenance(
                tv.Mental?.Psychology ?? 70, tv.Mental?.RingIQ ?? 70);
            tv.Sharpness += RingCondition.SharpnessGain(15, 1.0, upkeep);

            output.WriteLine($"  three towns  {road.Sharpness:F1}   one television match  {tv.Sharpness:F1}");

            Assert.True(road.Sharpness > tv.Sharpness,
                "three nights has to be worth more than one, or the road buys nothing");
            Assert.True(road.Sharpness > 40, "and it has to move at all");
        }

        /// <summary>And it costs them. Both meters move, in opposite directions, as always.</summary>
        [Fact]
        public void AndItTakesSomethingOutOfThem()
        {
            var w = Worker("Working");
            var result = new LoopSimulator(seed: 2).Run(Run(4, w), Day);
            var row = result.Workers.Single();

            output.WriteLine($"  +{row.SharpnessGained:F1} sharpness, +{row.FatigueAdded:F1} fatigue over {row.NightsWorked} nights");

            Assert.True(row.SharpnessGained > 0);
            Assert.True(row.FatigueAdded > 0);
            Assert.Equal(4, row.NightsWorked);
        }

        /// <summary>
        /// More towns is more of both. The dial that turns a top-up into a grind has to
        /// actually turn.
        /// </summary>
        [Fact]
        public void MoreTownsIsMoreOfBoth()
        {
            var light = Worker("One night");
            var heavy = Worker("Six nights");

            new LoopSimulator(seed: 3).Run(Run(1, light), Day);
            new LoopSimulator(seed: 3).Run(Run(6, heavy), Day);

            output.WriteLine($"  1 town: {light.Sharpness:F1} sharp, {light.Fatigue:F1} tired");
            output.WriteLine($"  6 towns: {heavy.Sharpness:F1} sharp, {heavy.Fatigue:F1} tired");

            Assert.True(heavy.Sharpness > light.Sharpness);
            Assert.True(heavy.Fatigue   > light.Fatigue);
        }

        /// <summary>
        /// **A grind can cook somebody, which a weekly television schedule never could.**
        ///
        /// Fatigue was calibrated for a workload the calendar did not offer: one match a week
        /// costs 7.6 and a week off repays about 14, so it returned to zero before every bell
        /// and the cooked threshold of 70 was unreachable in normal play. The road is where
        /// that meter finally gets fed.
        /// </summary>
        [Fact]
        public void ALongRunCanCookSomebody()
        {
            var w = Worker("Grinding", fatigue: 35);

            new LoopSimulator(seed: 4).Run(new HouseShowLoop
            {
                Cast = [w], Towns = 6, MinutesPerNight = 20, Pace = BeatIntensity.High
            }, Day);

            output.WriteLine($"  six hard nights from 35: {w.Fatigue:F1} (cooked at {RingCondition.CookedThreshold})");
            Assert.True(w.Fatigue >= RingCondition.CookedThreshold,
                $"six hard nights should cook somebody, not leave them at {w.Fatigue:F0}");
        }

        // ── What it is not ───────────────────────────────────────────────────

        /// <summary>
        /// **Nothing consequential happens on it.**
        ///
        /// Being in a ring and being on television are different currencies, and the loop only
        /// pays in the first one. This is the test that stops the road quietly becoming a
        /// second, easier way to get somebody over — which would make booking television
        /// pointless and is the exact thing doc 06's definition rules out.
        /// </summary>
        [Fact]
        public void NobodyGetsOverOnAHouseShow()
        {
            var w = Worker("Touring");
            w.Overness = 55;
            w.Momentum = 12;

            new LoopSimulator(seed: 5).Run(Run(6, w), Day);

            Assert.Equal(55, w.Overness, 3);
            Assert.Equal(12, w.Momentum, 3);
        }

        /// <summary>
        /// **And the absence clock keeps running.** You can work six towns a week and still be
        /// forgotten, because the people who forget you were watching television.
        /// </summary>
        [Fact]
        public void WorkingTheRoadIsNotAnAppearance()
        {
            var w = Worker("Touring");
            var before = w.LastAppearance;

            new LoopSimulator(seed: 6).Run(Run(6, w), Day);

            Assert.Equal(before, w.LastAppearance);
        }

        // ── Injuries ─────────────────────────────────────────────────────────

        /// <summary>
        /// The road hurts people, and somebody hurt in town two does not work towns three and
        /// four. Run at a punishing pace over many nights so the rate is not the thing under
        /// test — what is under test is that the run *stops*.
        /// </summary>
        [Fact]
        public void SomebodyHurtOnTheRoadGoesHome()
        {
            // Enough runs that an injury is certain somewhere; the claim is about what
            // happens after one, not about how often one happens.
            for (int seed = 0; seed < 400; seed++)
            {
                var w = Worker("Unlucky", fatigue: 60);
                var result = new LoopSimulator(seed: seed).Run(new HouseShowLoop
                {
                    Cast = [w], Towns = 6, MinutesPerNight = 20, Pace = BeatIntensity.Extreme
                }, Day);

                var row = result.Workers.Single();
                if (row.HurtOnNight is not { } night) continue;

                output.WriteLine($"  seed {seed}: hurt on night {night}, worked {row.NightsWorked}");
                Assert.Equal(night, row.NightsWorked);
                Assert.True(night <= 6);
                Assert.Single(result.Injuries);
                Assert.NotNull(w.Injury);
                return;
            }

            Assert.Fail("400 punishing runs and nobody got hurt — the road cannot be this safe");
        }

        /// <summary>Somebody already hurt does not go out at all.</summary>
        [Fact]
        public void AnInjuredWrestlerDoesNotTravel()
        {
            var w = Worker("Already hurt");
            w.Injury = new Models.Person.Injury
            {
                Part = BodyPart.Knee, Sustained = Day.AddDays(-7),
                ClearedOn = Day.AddDays(70), WeeksOut = 11
            };

            var result = new LoopSimulator(seed: 7).Run(Run(4, w), Day);
            var row = result.Workers.Single();

            Assert.Equal(0, row.NightsWorked);
            Assert.Equal(0, row.SharpnessGained, 3);
            Assert.Equal(0, row.FatigueAdded, 3);
        }

        /// <summary>
        /// **How dangerous the road is, measured rather than assumed.**
        ///
        /// Doc 15 §1 puts 35–55% of a roster missing time in a year, and `MatchEngine` was
        /// calibrated against that for televised matches. A loop is the other half of the
        /// year's workload, so its rate has to be sized against the same figure or the road
        /// quietly becomes either the safest place in wrestling or a meat grinder.
        ///
        /// The dial is the beats of risk a night carries. Nothing else in these tests reads
        /// it, so it could be set to one — six times safer — with every other test green.
        /// </summary>
        [Theory]
        [InlineData(BeatIntensity.Low,    0.03, 0.14)]
        [InlineData(BeatIntensity.Medium, 0.09, 0.28)]
        [InlineData(BeatIntensity.High,   0.18, 0.55)]
        public void TheRoadHurtsPeopleAtARateDocFifteenWouldRecognise(
            BeatIntensity pace, double lowPercent, double highPercent)
        {
            int hurt = 0, nights = 0;

            for (int seed = 0; seed < 600; seed++)
            {
                var roster = DataLoaders.LoadEmbeddedWrestlers();
                var result = new LoopSimulator(seed: seed).Run(new HouseShowLoop
                {
                    Cast            = roster.Skip(seed % 60).Take(6).ToList(),
                    Towns           = 3,
                    MinutesPerNight = 12,
                    Pace            = pace
                }, Day);

                hurt   += result.Injuries.Count;
                nights += result.Workers.Sum(w => w.NightsWorked);
            }

            double perNight = hurt * 100.0 / nights;

            // What that comes to over a season of three towns a week, which is the workload a
            // booker who uses this feature is actually setting.
            double aYear = (1 - Math.Pow(1 - perNight / 100.0, 156)) * 100;

            output.WriteLine($"  {pace}: {perNight:F2}% a night · {aYear:F0}% across 156 nights a year");

            Assert.InRange(perNight, lowPercent, highPercent);
        }

        // ── The booking ──────────────────────────────────────────────────────

        /// <summary>A run needs two people and a town, the same way a match needs two corners.</summary>
        [Fact]
        public void ARunNeedsACastAndATown()
        {
            Assert.False(new HouseShowLoop { Cast = [], Towns = 3 }.IsBookable);
            Assert.False(new HouseShowLoop { Cast = [Worker("A")], Towns = 3 }.IsBookable);
            Assert.False(new HouseShowLoop { Cast = [Worker("A"), Worker("B")], Towns = 0 }.IsBookable);
            Assert.True(new HouseShowLoop { Cast = [Worker("A"), Worker("B")], Towns = 1 }.IsBookable);
        }

        /// <summary>
        /// **A card and a run of towns happen on the same date.**
        ///
        /// They are not alternatives. A real untelevised night is a couple of matches that
        /// mean something plus everybody else getting work, and the first version made the
        /// booker choose — worse, it chose for them: adding one match to a date with a booked
        /// run silently dropped the run, with no warning and nowhere left on the screen to
        /// see it. This is that behaviour, inverted.
        /// </summary>
        [Fact]
        public void ACardAndARunOfTownsBothHappen()
        {
            var show = new ScheduledShow { Type = ShowType.HouseShow, Date = Day };
            Assert.False(show.IsBooked);
            Assert.False(show.HasRoadRun);

            show.Loop = new HouseShowLoop { Cast = [Worker("Road A"), Worker("Road B")], Towns = 2 };
            Assert.True(show.HasRoadRun);
            Assert.True(show.IsLoopOnly);
            Assert.True(show.IsBooked);
            Assert.True(show.IsRunnable);

            var match = new BookedMatch
            {
                Plan = new Models.MatchPlan.MatchPlan
                {
                    Sides = [Models.MatchPlan.MatchSide.Of(Worker("Card A")),
                             Models.MatchPlan.MatchSide.Of(Worker("Card B"))]
                }
            };
            match.Plan.Beats.AddRange(WrestlingSim.Engine.BriefDirector
                .Write(new Models.MatchPlan.MatchBrief(), match.Plan.Sides).Beats);
            show.Card.Add(match);

            // The run is still there, and the night is still runnable.
            Assert.True(show.HasRoadRun);
            Assert.False(show.IsLoopOnly, "it is no longer *only* a run of towns");
            Assert.True(show.IsRunnable);
            Assert.Equal(2, show.Loop!.Cast.Count);
        }

        /// <summary>
        /// Nobody is in two places. Somebody on the card and on the road for the same date is
        /// a booking mistake, and the screen says so rather than one of the two quietly
        /// winning — which is what the old "a card beats a run" rule did to a whole cast.
        /// </summary>
        [Fact]
        public void SomebodyOnTheCardAndOnTheRoadIsFlagged()
        {
            var both = Worker("In two places");
            var show = new ScheduledShow { Type = ShowType.HouseShow, Date = Day };

            show.Loop = new HouseShowLoop { Cast = [both, Worker("Road")], Towns = 2 };
            show.Card.Add(new BookedMatch
            {
                Plan = new Models.MatchPlan.MatchPlan
                {
                    Sides = [Models.MatchPlan.MatchSide.Of(both),
                             Models.MatchPlan.MatchSide.Of(Worker("Opponent"))]
                }
            });

            output.WriteLine($"  double booked: {string.Join(", ", show.DoubleBooked.Select(w => w.RingName))}");
            Assert.Single(show.DoubleBooked);
            Assert.Same(both, show.DoubleBooked.Single());

            // And the run itself is untouched by it — the card wins for that wrestler when
            // the night is run, not when it is booked, so removing the match puts them back
            // on the road without the booker having to remember to.
            Assert.Equal(2, show.Loop!.Cast.Count);
        }

        /// <summary>
        /// **Both halves of a mixed night actually happen.**
        ///
        /// The card is graded and moves standing; the road moves condition and nothing else.
        /// Run on the same date they must not interfere: the people on the card are not
        /// sharpened by a tour they were not on, and the people on the road do not appear in
        /// the card's rating.
        /// </summary>
        [Fact]
        public void ACardAndARoadRunOnOneNightEachDoTheirOwnJob()
        {
            var onTheRoad = Worker("Road", sharpness: 50);
            var onTheCard = Worker("Card", sharpness: 50);

            double roadBefore = onTheRoad.Sharpness;
            double cardBefore = onTheCard.Sharpness;

            var loop = new HouseShowLoop { Cast = [onTheRoad, Worker("Road two")], Towns = 3 };
            var result = new LoopSimulator(seed: 11).Run(loop, Day);

            output.WriteLine($"  road {roadBefore:F1} → {onTheRoad.Sharpness:F1} · " +
                             $"card {cardBefore:F1} → {onTheCard.Sharpness:F1}");

            Assert.True(onTheRoad.Sharpness > roadBefore, "the road sharpens whoever is on it");
            Assert.Equal(cardBefore, onTheCard.Sharpness, 3);
            Assert.DoesNotContain(result.Workers, w => w.Wrestler == onTheCard);
        }

        /// <summary>
        /// **A televised match costs one town, not the whole run.**
        ///
        /// The first mixed night billed anybody on the card for the card *and* every town, so
        /// one body worked four nights out of one date. The fix for that took them off the
        /// run altogether, which is wrong in the other direction and worse: television and
        /// the road become mutually exclusive, and the one route back to match fitness the
        /// sharpness meter describes — protected television plus live local reps — stops
        /// existing. Somebody wrestles Monday television and is in a gym on the Friday.
        /// </summary>
        [Fact]
        public void ACardWrestlerWorksTheRunOneTownShort()
        {
            var alsoOnTv = Worker("Television", sharpness: 50);
            var roadOnly = Worker("Road only",  sharpness: 50);

            var loop = Run(3, alsoOnTv, roadOnly);
            var result = new LoopSimulator(seed: 7).Run(loop, Day, new HashSet<Wrestler> { alsoOnTv });

            var tv   = result.Workers.Single(w => w.Wrestler == alsoOnTv);
            var road = result.Workers.Single(w => w.Wrestler == roadOnly);

            output.WriteLine($"  television {tv.NightsWorked} of {loop.Towns} towns, " +
                             $"+{tv.SharpnessGained:F1} sharp");
            output.WriteLine($"  road only  {road.NightsWorked} of {loop.Towns} towns, " +
                             $"+{road.SharpnessGained:F1} sharp");

            Assert.Equal(2, tv.NightsWorked);
            Assert.Equal(3, road.NightsWorked);

            // The point of the whole thing: being on television does not cost them the reps.
            Assert.True(tv.SharpnessGained > 0, "television plus the towns is still reps");
            Assert.True(tv.OnTelevision);
            Assert.False(road.OnTelevision);
        }

        /// <summary>
        /// Unless there is nothing left of the run. One town, and they were on the card that
        /// night, means they were not on the road at all — and a row reading "worked 0 towns"
        /// is the card being reported a second time in different words.
        /// </summary>
        [Fact]
        public void AOneTownRunLeavesNothingForSomebodyOnTheCard()
        {
            var alsoOnTv = Worker("Television", sharpness: 50);
            double before = alsoOnTv.Sharpness;

            var loop = Run(1, alsoOnTv, Worker("Road only"));
            var result = new LoopSimulator(seed: 7).Run(loop, Day, new HashSet<Wrestler> { alsoOnTv });

            Assert.DoesNotContain(result.Workers, w => w.Wrestler == alsoOnTv);
            Assert.Equal(before, alsoOnTv.Sharpness, 3);
            Assert.Single(result.Workers);
        }

        /// <summary>
        /// The night count alone cannot explain itself. A short run means either "they were
        /// on television" or "they went home hurt", and those read completely differently to
        /// a booker — so the result says which rather than leaving the report to guess.
        /// </summary>
        [Fact]
        public void AShortRunSaysWhetherItWasTelevisionOrAnInjury()
        {
            var alsoOnTv = Worker("Television");
            var loop = Run(4, alsoOnTv, Worker("Road only"));
            var result = new LoopSimulator(seed: 3).Run(loop, Day, new HashSet<Wrestler> { alsoOnTv });

            var tv = result.Workers.Single(w => w.Wrestler == alsoOnTv);
            Assert.True(tv.OnTelevision);
            Assert.Null(tv.HurtOnNight);
        }

        // ── The protected rep ────────────────────────────────────────────────

        /// <summary>
        /// **A tag run keeps more of the benefit than of the cost, and that is the whole
        /// reason to book one.**
        ///
        /// `RingCondition.SharpnessGain` scales its *demand* term by the work share and
        /// leaves its rep term alone: turning up and working a match is the rep whatever else
        /// happens, and the apron only discounts the bill. So a tag night gives less than a
        /// singles night — but it gives up proportionally less than it saves, which is what
        /// "protected" means and what a smaller-is-worse reading of the numbers would miss.
        /// </summary>
        [Fact]
        public void ATagRunKeepsMoreOfTheRepThanOfTheBill()
        {
            var singles = Worker("Singles", sharpness: 40);
            var tags    = Worker("Tags",    sharpness: 40);

            new LoopSimulator(seed: 5).Run(Run(3, singles, Worker("S2")), Day);

            var tagRun = Run(3, tags, Worker("T2"), Worker("T3"), Worker("T4"));
            tagRun.SideSize = 2;
            var result = new LoopSimulator(seed: 5).Run(tagRun, Day);

            // The report reads this to say the run was tag matches, and a booker looking at
            // smaller numbers needs to know whether that is the format or the wrestler.
            Assert.Equal(2, result.SideSize);

            double sharpSingles = singles.Sharpness - 40, sharpTags = tags.Sharpness - 40;
            double tiredSingles = singles.Fatigue,        tiredTags = tags.Fatigue;

            output.WriteLine($"  singles  +{sharpSingles:F2} sharp  +{tiredSingles:F2} tired");
            output.WriteLine($"  tags     +{sharpTags:F2} sharp  +{tiredTags:F2} tired");
            output.WriteLine($"  kept     {sharpTags / sharpSingles:P0} of the rep, " +
                             $"{tiredTags / tiredSingles:P0} of the bill");

            Assert.True(sharpTags > 0, "a tag is still a rep");
            Assert.True(sharpTags < sharpSingles, "and still less of one than a singles match");
            Assert.True(tiredTags < tiredSingles, "it costs less");

            // The claim that makes it worth booking: the trade is in the wrestler's favour.
            Assert.True(sharpTags / sharpSingles > tiredTags / tiredSingles,
                        "a tag keeps more of the rep than of the bill");
        }

        /// <summary>
        /// **And it is fewer rolls of the dice, not merely cheaper ones.**
        ///
        /// You cannot get hurt taking a bump you were on the apron for, so the nightly injury
        /// roll walks fewer beats. Measured across a corpus rather than asserted, because one
        /// seed of a probabilistic model is one sample: the figures below are what the engine
        /// actually does, printed so a change to them is visible.
        /// </summary>
        [Fact]
        public void ATagRunIsFewerRollsOfTheDice()
        {
            int Hurt(int sideSize)
            {
                int hurt = 0;
                for (int run = 0; run < 3000; run++)
                {
                    var cast = Enumerable.Range(0, 4)
                        .Select(i => Worker($"Body {i}")).ToArray();

                    var loop = new HouseShowLoop
                    {
                        Cast = cast.ToList(), Towns = 6,
                        MinutesPerNight = 16, Pace = BeatIntensity.High,
                        SideSize = sideSize
                    };

                    hurt += new LoopSimulator(seed: StableSeed.From(sideSize, run))
                        .Run(loop, Day).Injuries.Count;
                }
                return hurt;
            }

            int singles = Hurt(1), tags = Hurt(2);
            output.WriteLine($"  3000 runs of 4 bodies, 6 towns, full tilt:");
            output.WriteLine($"    singles {singles} hurt · tags {tags} hurt " +
                             $"({(double)tags / singles:P0} of the singles rate)");

            // Not just "fewer": a tag walks four beats a night where singles walks six, so
            // the gap should be a clear fraction and not a coin landing the right way up. The
            // measured ratio is printed rather than pinned — the margin is here to rule out
            // noise, and the number above is the one to read when it moves.
            Assert.True(tags < singles * 0.90,
                        $"a tag run should hurt clearly fewer people: {tags} against {singles}");
        }

        /// <summary>
        /// A tag run needs two full sides. Three people booked into one is not a tag run with
        /// somebody sitting out, it is a card that has not been finished — and the format chip
        /// can make an already-booked run unbookable without anybody touching the cast.
        /// </summary>
        [Fact]
        public void ATagRunNeedsTwoFullSides()
        {
            var loop = Run(3, Worker("A"), Worker("B"));
            Assert.True(loop.IsBookable, "two is a singles match");

            loop.SideSize = 2;
            Assert.False(loop.IsBookable, "two is not a tag match");

            loop.Cast.Add(Worker("C"));
            Assert.False(loop.IsBookable, "nor is three");

            loop.Cast.Add(Worker("D"));
            Assert.True(loop.IsBookable);
        }

        /// <summary>
        /// **A mixed run: some of them protected, the rest working singles, on the same
        /// night.**
        ///
        /// One format for the whole run was the wrong shape. A booker sending eight people
        /// out is usually protecting two of them, not eight, and a run that can only be all
        /// of one thing cannot do the job the tag format was added for.
        /// </summary>
        [Fact]
        public void SomeOfThemAreProtectedAndTheRestAreNot()
        {
            var looked_after = Worker("Coming back", sharpness: 40);
            var ordinary     = Worker("Fine",        sharpness: 40);

            var loop = Run(3, looked_after, ordinary, Worker("Three"), Worker("Four"));
            loop.InTags.Add(looked_after);

            Assert.Equal(2, loop.SideSizeFor(looked_after));
            Assert.Equal(1, loop.SideSizeFor(ordinary));

            var result = new LoopSimulator(seed: 9).Run(loop, Day);

            var kept = result.Workers.Single(w => w.Wrestler == looked_after);
            var full = result.Workers.Single(w => w.Wrestler == ordinary);

            output.WriteLine($"  protected  +{kept.SharpnessGained:F2} sharp  +{kept.FatigueAdded:F2} tired");
            output.WriteLine($"  singles    +{full.SharpnessGained:F2} sharp  +{full.FatigueAdded:F2} tired");

            Assert.Equal(2, kept.SideSize);
            Assert.Equal(1, full.SideSize);

            // Two people on the same run, the same towns, the same pace, doing different jobs.
            Assert.True(kept.FatigueAdded < full.FatigueAdded);
            Assert.True(kept.SharpnessGained < full.SharpnessGained);
            Assert.True(kept.SharpnessGained > 0);

            // And the report can pick them out without being told which run this was.
            Assert.Same(looked_after, result.Protected.Single().Wrestler);
        }

        /// <summary>
        /// Protecting one person on an otherwise singles run still means somebody out there
        /// is working a tag match, and a tag match takes four bodies however few of them are
        /// the one being looked after.
        /// </summary>
        [Fact]
        public void ProtectingOnePersonMakesTheWholeRunNeedFour()
        {
            var kept = Worker("Coming back");
            var loop = Run(3, kept, Worker("Two"));

            Assert.True(loop.IsBookable, "two is a singles run");

            loop.InTags.Add(kept);
            Assert.Equal(2, loop.LargestSide);
            Assert.False(loop.IsBookable, "somebody out there is working a tag match now");

            loop.Cast.Add(Worker("Three"));
            loop.Cast.Add(Worker("Four"));
            Assert.True(loop.IsBookable);
        }

        /// <summary>
        /// A mark on somebody who is not on the run decides nothing. Otherwise a name taken
        /// off the cast keeps demanding room for a match nobody is in.
        /// </summary>
        [Fact]
        public void AMarkOnSomebodyWhoIsNotGoingCountsForNothing()
        {
            var dropped = Worker("Taken off");
            var loop = Run(3, Worker("A"), Worker("B"));
            loop.InTags.Add(dropped);

            Assert.Equal(1, loop.LargestSide);
            Assert.True(loop.IsBookable);

            var result = new LoopSimulator(seed: 4).Run(loop, Day);
            Assert.DoesNotContain(result.Workers, w => w.Wrestler == dropped);
            Assert.Empty(result.Protected);
        }

        // ── Who is on the road ───────────────────────────────────────────────

        /// <summary>
        /// A touring assignment is a standing instruction, not a booking. It says who the
        /// loop builder opens on, and it stops meaning anything the day it runs out.
        /// </summary>
        [Fact]
        public void ATouringAssignmentCoversAStretchAndThenStops()
        {
            var w = Worker("On the road");
            Assert.False(w.IsTouring(Day));

            w.TouringUntil = Day.AddDays(28);

            Assert.True(w.IsTouring(Day));
            Assert.True(w.IsTouring(Day.AddDays(28)), "the last day is still on the road");
            Assert.False(w.IsTouring(Day.AddDays(29)));
        }
    }
}
