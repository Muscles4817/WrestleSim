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
    /// **The gimmick match, defined as what it takes away.**
    ///
    /// The spec is not doc 18. Every format built so far — multi-man, elimination,
    /// handicap, the battle royal — comes out of doc 18 §2.5 and changes who is in the
    /// match or how a fall works. A stipulation is in **doc 20 §6**, inside "The blow-off",
    /// and doc 04 §5 lists it in the booker's toolkit beside the turn and the title change.
    /// It is a *feud* mechanic: doc 20 is explicit that "the stipulation must match the
    /// escalation."
    ///
    /// So the claims under test are the three that make it a decision rather than a bonus:
    ///
    ///   • **It removes the cheap loss.** Doc 04 prices the screwjob finish as "protects
    ///     both, sells the rematch"; every rung of the ladder is the removal of that.
    ///   • **It has to be earned.** A cage for a feud that never got past words is worse
    ///     than no cage, and worse in a way scarcity cannot launder.
    ///   • **It has to be rare.** Doc 17 §4.1: one to two per year, per promotion.
    ///
    /// Remove any one and the feature is a free rating multiplier with no counter, which is
    /// the shape this codebase has now closed twice on the Rumble.
    /// </summary>
    public class StipulationTests(ITestOutputHelper output)
    {
        private static readonly int Seed = StableSeed.From("stipulations");

        private static Wrestler W(string n, int over = 75) =>
            TestRoster.Make(n, overness: over, charisma: 3.0, skill: 3.5);

        private static readonly BeatType[] Finishes =
        [
            BeatType.FinishClean, BeatType.FinishRollup, BeatType.FinishSubmission,
            BeatType.FinishSuperFinisher, BeatType.FinishDQ, BeatType.FinishCountout,
            BeatType.FinishInterference
        ];

        private static readonly Stipulation[] Gimmicks =
        [
            Stipulation.NoDisqualification, Stipulation.SteelCage,
            Stipulation.LastManStanding, Stipulation.IQuit
        ];

        // ── What it takes away ───────────────────────────────────────────────

        /// <summary>
        /// **Every stipulation forbids the disqualification and the count-out.**
        ///
        /// The one property that holds across the whole ladder, and the sentence the
        /// feature exists to make true: those are the two finishes that let somebody lose
        /// without being beaten, so removing them is what a gimmick match *is*. Asserted
        /// across the whole enum rather than rung by rung, so a rung added later cannot
        /// quietly opt out of the only rule they all share.
        /// </summary>
        [Fact]
        public void EveryStipulationRemovesTheCheapLoss()
        {
            foreach (var stip in Gimmicks)
            {
                Assert.False(StipulationRules.Allows(stip, BeatType.FinishDQ),
                    $"{stip} allowed a disqualification finish");
                Assert.False(StipulationRules.Allows(stip, BeatType.FinishCountout),
                    $"{stip} allowed a count-out finish");
            }

            // And under the rules, everything is still available — the ladder is what is
            // removed *from* a normal match, not a separate set of rules.
            foreach (var finish in Finishes)
                Assert.True(StipulationRules.Allows(Stipulation.None, finish));
        }

        /// <summary>
        /// **A stipulation has no opinion about a beat that is not a finish.**
        ///
        /// Added after review found the builder unusable for two of the four rungs.
        /// <see cref="StipulationRules.Allows"/> was written as a question about finishes —
        /// "can this end a match here" — and `Last Man Standing` answers it with
        /// `is FinishClean or FinishSuperFinisher`, which is `false` for a hot opening, a
        /// heat segment and everything else. The builder's beat-library gate called it for
        /// *every* template, so choosing Last Man Standing or I Quit disabled the entire
        /// library and the match could not be booked at all.
        ///
        /// The fix is here rather than at the call site because there were already three
        /// callers and the trap was invisible from all of them. A stipulation constrains
        /// how a match can *end*; it has nothing to say about how one is worked.
        ///
        /// Steel Cage and No DQ happened to return true for non-finishes, which is why a
        /// browser pass that only booked a cage found nothing.
        /// </summary>
        [Fact]
        public void AStipulationHasNoOpinionAboutBeatsThatAreNotFinishes()
        {
            BeatType[] notFinishes =
            [
                BeatType.HotOpening, BeatType.StandardOpening, BeatType.HeatSegment,
                BeatType.Comeback, BeatType.NearFall, BeatType.HighSpot, BeatType.RestHold,
                BeatType.Shine, BeatType.HotTag, BeatType.DisposalSpot, BeatType.Elimination
            ];

            foreach (var stip in Gimmicks)
                foreach (var beat in notFinishes)
                    Assert.True(StipulationRules.Allows(stip, beat),
                        $"{stip} refused {beat}, which is not a finish");

            output.WriteLine($"  {notFinishes.Length} non-finish beats allowed under all " +
                             $"{Gimmicks.Length} rungs");
        }

        /// <summary>
        /// **Each rung has its own character, and the cage's is who it keeps out.**
        ///
        /// Doc 20 §6.2's "Says" column, as rules. The cage is the one that bans the
        /// interference finish outright rather than merely stripping its protection —
        /// "nobody escapes, nobody interferes" is the entire reason the structure exists.
        /// </summary>
        [Fact]
        public void EachRungForbidsWhatItSaysItForbids()
        {
            foreach (var stip in Gimmicks)
                output.WriteLine($"  {StipulationRules.Label(stip),-20} " +
                    string.Join(", ", Finishes.Where(f => StipulationRules.Allows(stip, f))));

            // No DQ: the run-in is legal. That is the point — see the promotion test below.
            Assert.True(StipulationRules.Allows(Stipulation.NoDisqualification,
                                                BeatType.FinishInterference));

            // The cage keeps it out entirely.
            Assert.False(StipulationRules.Allows(Stipulation.SteelCage,
                                                 BeatType.FinishInterference));

            // "Only unconsciousness ends it" — nothing that relies on somebody being
            // briefly beaten, so no roll-up and no submission.
            Assert.False(StipulationRules.Allows(Stipulation.LastManStanding, BeatType.FinishRollup));
            Assert.False(StipulationRules.Allows(Stipulation.LastManStanding, BeatType.FinishSubmission));
            Assert.True(StipulationRules.Allows(Stipulation.LastManStanding, BeatType.FinishClean));

            // I Quit names one way to win rather than removing several.
            Assert.Equal(
                [BeatType.FinishSubmission],
                Finishes.Where(f => StipulationRules.Allows(Stipulation.IQuit, f)).ToArray());
        }

        /// <summary>
        /// **A run-in in a No-DQ match is a clean loss — and the belt moves.**
        ///
        /// The sharpest consequence in the feature, and it costs one line: interference
        /// reads as <see cref="FinishWeight.Protected"/> because it is against the rules,
        /// and the audience forgives a loss it can blame on a rule being broken. Announce
        /// that there are no rules and there is nothing left to blame.
        ///
        /// <see cref="TitleEconomy.ChangesHands"/> then follows with no change of its own,
        /// which is why a booker who needs a belt off somebody who will not lose clean
        /// reaches for a stipulation.
        /// </summary>
        [Fact]
        public void ARunInIsAnExcuseUnderTheRulesAndNotWithoutThem()
        {
            var underRules = StipulationRules.Weigh(Stipulation.None, BeatType.FinishInterference);
            var noDq       = StipulationRules.Weigh(Stipulation.NoDisqualification,
                                                    BeatType.FinishInterference);

            output.WriteLine($"  interference: rules {underRules}, no-DQ {noDq}");
            output.WriteLine($"  belt moves:   rules {TitleEconomy.ChangesHands(underRules)}, " +
                             $"no-DQ {TitleEconomy.ChangesHands(noDq)}");

            Assert.Equal(FinishWeight.Protected, underRules);
            Assert.Equal(FinishWeight.Decisive, noDq);

            Assert.False(TitleEconomy.ChangesHands(underRules));
            Assert.True(TitleEconomy.ChangesHands(noDq));

            // A roll-up is a legal pinfall either way. The stipulation has no opinion about
            // whether it convinced anybody, which is charged for separately.
            Assert.Equal(FinishWeight.Fluke,
                StipulationRules.Weigh(Stipulation.NoDisqualification, BeatType.FinishRollup));
            Assert.Equal(FinishWeight.Decisive,
                StipulationRules.Weigh(Stipulation.IQuit, BeatType.FinishSubmission));
        }

        /// <summary>
        /// **A run-in settles a feud only when there were no rules to break.**
        ///
        /// Doc 20 §6.1: a blow-off has to resolve. This is doc 20's argument for reaching
        /// for a stipulation at the end of a feud, as a rule rather than a sentence — the
        /// stipulation removes the escape route that would leave the story open, so the
        /// same booked finish either settles it or does not depending on what was
        /// announced.
        /// </summary>
        [Fact]
        public void ARunInSettlesAFeudOnlyWithNoRulesToBreak()
        {
            foreach (var stip in new[] { Stipulation.None, Stipulation.NoDisqualification })
                output.WriteLine($"  {StipulationRules.Label(stip),-20} " +
                    $"run-in settles it: {StipulationRules.SettlesAFeud(stip, BeatType.FinishInterference)}");

            Assert.False(StipulationRules.SettlesAFeud(Stipulation.None, BeatType.FinishInterference));
            Assert.True(StipulationRules.SettlesAFeud(Stipulation.NoDisqualification,
                                                      BeatType.FinishInterference));

            // A count-out never settles anything, and every stipulation has outlawed it
            // anyway — so the only way to book one is to book no stipulation at all.
            Assert.False(StipulationRules.SettlesAFeud(Stipulation.None, BeatType.FinishCountout));

            // And a clean finish settles it under any rules, which is the baseline the
            // whole ladder is measured against.
            foreach (var stip in Gimmicks.Append(Stipulation.None))
                if (StipulationRules.Allows(stip, BeatType.FinishClean))
                    Assert.True(StipulationRules.SettlesAFeud(stip, BeatType.FinishClean));
        }

        // ── What it demands ──────────────────────────────────────────────────

        /// <summary>
        /// **A cage for a feud that never got past words is worse than no cage.**
        ///
        /// Doc 20 §6: the stipulation must be proportional to what was built. Negative
        /// rather than merely zero, because announcing stakes the room can see were not
        /// earned is an active statement that the promotion has run out of ideas — which is
        /// worse than announcing nothing.
        /// </summary>
        [Fact]
        public void AStipulationNobodyEarnedIsWorseThanNone()
        {
            foreach (var reached in Enum.GetValues<FeudIntensity>())
                output.WriteLine($"  cage at {reached,-9} " +
                    $"{StipulationRules.StakesBonus(Stipulation.SteelCage, reached, null),6:F1}");

            double cold  = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.None, null);
            double hot   = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.Hot, null);
            double none  = StipulationRules.StakesBonus(Stipulation.None, FeudIntensity.Hot, null);

            Assert.True(cold < 0, $"a cage for a feud at None should cost, got {cold:F1}");
            Assert.True(hot > 0);
            Assert.Equal(0.0, none, 5);
            Assert.True(cold < none, "an unearned stipulation should be worse than booking none");

            // The ladder demands more as it climbs, so I Quit between strangers is the
            // worst booking available here.
            Assert.True(
                StipulationRules.StakesBonus(Stipulation.IQuit, FeudIntensity.None, null) < cold);
        }

        /// <summary>
        /// Reaching the rung is the whole of the credit. A feud hotter than the stipulation
        /// demands is not paid twice — the heat it built is already paying out through
        /// <see cref="Feud.StartingEnergyBonus"/>, and crediting it here as well would make
        /// a nuclear feud's cage worth more than the nuclear feud.
        /// </summary>
        [Fact]
        public void BeingHotterThanTheRungDemandsIsNotPaidTwice()
        {
            double atHot     = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.Hot, null);
            double atNuclear = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.Nuclear, null);

            output.WriteLine($"  cage at Hot {atHot:F1}, at Nuclear {atNuclear:F1}");
            Assert.Equal(atHot, atNuclear, 5);
        }

        // ── How rare it has to be ────────────────────────────────────────────

        /// <summary>
        /// **A cage match is special once a year.** Doc 17 §4.1, and doc 20 §9 asks for it
        /// as a promotion-level counter — so running one this month devalues the next one
        /// whoever is in it.
        /// </summary>
        [Fact]
        public void RunningOneRecentlyDevaluesTheNext()
        {
            double never     = StipulationRules.Scarcity(null);
            double yesterday = StipulationRules.Scarcity(1);
            double aMonth    = StipulationRules.Scarcity(30);
            double halfAYear = StipulationRules.Scarcity(180);

            output.WriteLine($"  never {never:F2}  1d {yesterday:F2}  30d {aMonth:F2}  180d {halfAYear:F2}");

            Assert.Equal(1.0, never, 3);
            Assert.Equal(1.0, halfAYear, 3);
            Assert.True(yesterday < aMonth && aMonth < halfAYear);
            Assert.True(yesterday >= 0.15, "a crowd that saw a cage last night still knows what one is");
        }

        /// <summary>
        /// **Scarcity multiplies the reward and not the penalty.**
        ///
        /// The load-bearing asymmetry. If a long gap softened an unearned stipulation, the
        /// way to book an unearned cage would be to wait — which is not a lesson about
        /// wrestling. Being overdue can make a good idea better; it cannot make a bad one
        /// good.
        /// </summary>
        [Fact]
        public void BeingOverdueCannotExcuseBeingUnearned()
        {
            double earnedFresh = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.Hot, null);
            double earnedStale = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.Hot, 7);

            double unearnedFresh = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.None, null);
            double unearnedStale = StipulationRules.StakesBonus(Stipulation.SteelCage, FeudIntensity.None, 7);

            output.WriteLine($"  earned:   never-run {earnedFresh:F1}  a week ago {earnedStale:F1}");
            output.WriteLine($"  unearned: never-run {unearnedFresh:F1}  a week ago {unearnedStale:F1}");

            Assert.True(earnedStale < earnedFresh, "overuse should cut what a good booking earns");
            Assert.Equal(unearnedFresh, unearnedStale, 5);
        }

        /// <summary>
        /// The promotion-level counter itself: a cage on one feud makes a cage on a
        /// different feud a fortnight later worth less, because the audience's sense that a
        /// cage is special is a fact about the show and not about who is in it.
        /// </summary>
        [Fact]
        public void TheCooldownIsPromotionWideAndNotPerFeud()
        {
            var book = new StipulationBook();
            var day0 = new DateOnly(2026, 1, 5);

            Assert.Null(book.DaysSince(Stipulation.SteelCage, day0));

            book.Record(Stipulation.SteelCage, day0);

            output.WriteLine($"  14 days later: {book.FreshnessOf(Stipulation.SteelCage, day0.AddDays(14)):F2}");
            output.WriteLine($"  a year later:  {book.FreshnessOf(Stipulation.SteelCage, day0.AddDays(365)):F2}");

            Assert.Equal(14, book.DaysSince(Stipulation.SteelCage, day0.AddDays(14)));
            Assert.True(book.FreshnessOf(Stipulation.SteelCage, day0.AddDays(14)) < 0.5);
            Assert.Equal(1.0, book.FreshnessOf(Stipulation.SteelCage, day0.AddDays(365)), 3);

            // Each rung wears out on its own. Running a cage does not make a No-DQ stale.
            Assert.Null(book.DaysSince(Stipulation.NoDisqualification, day0.AddDays(14)));
        }

        // ── Booking it ───────────────────────────────────────────────────────

        /// <summary>
        /// **The builder refuses a finish the stipulation outlawed.**
        ///
        /// Without this the whole feature is a label: a cage match you can still end by
        /// disqualification has removed nothing.
        /// </summary>
        [Fact]
        public void APlanCannotEndAWayItsStipulationForbids()
        {
            var plan = Singles(W("A"), W("B"), BeatType.FinishDQ);
            plan.Stipulation = Stipulation.SteelCage;

            var errors = plan.Validate();
            foreach (var e in errors) output.WriteLine($"  • {e}");
            Assert.Contains(errors, e => e.Contains("Steel Cage"));

            // The same plan under the rules is a perfectly ordinary booking.
            plan.Stipulation = Stipulation.None;
            Assert.Empty(plan.Validate());
        }

        // ── In a match ───────────────────────────────────────────────────────

        /// <summary>
        /// **The same match is worth more with an earned stipulation and less with an
        /// unearned one.**
        ///
        /// Asserted against <see cref="MatchEngineResult.StipulationStakes"/> as well as
        /// the rating, because a claim that can only be seen through a whole match's score
        /// is a claim no test can hold — the lesson this codebase has now learned four
        /// separate times.
        /// </summary>
        [Fact]
        public void AnEarnedStipulationLiftsTheRoomAndAnUnearnedOneFlattensIt()
        {
            MatchEngineResult Run(Stipulation stip, FeudIntensity intensity)
            {
                var a = W("Face"); var b = W("Heel");
                var plan = Singles(a, b, BeatType.FinishClean);
                plan.Stipulation = stip;

                var feud = new Feud { Camps = [[a], [b]] };
                feud.AddHeat(intensity switch
                {
                    FeudIntensity.Nuclear  => 60,
                    FeudIntensity.Hot      => 35,
                    FeudIntensity.Building => 18,
                    FeudIntensity.Cold     => 7,
                    _                      => 0
                });
                plan.Feud = feud;

                return new MatchEngine(Seed).Execute(plan);
            }

            var plainHot   = Run(Stipulation.None, FeudIntensity.Hot);
            var cageHot    = Run(Stipulation.SteelCage, FeudIntensity.Hot);
            var cageCold   = Run(Stipulation.SteelCage, FeudIntensity.None);

            output.WriteLine($"  no stip, hot feud   stakes {plainHot.StipulationStakes,6:F1}  {plainHot.FinalScore:F1}");
            output.WriteLine($"  cage,    hot feud   stakes {cageHot.StipulationStakes,6:F1}  {cageHot.FinalScore:F1}");
            output.WriteLine($"  cage,    no feud    stakes {cageCold.StipulationStakes,6:F1}  {cageCold.FinalScore:F1}");

            Assert.Equal(0.0, plainHot.StipulationStakes, 5);
            Assert.True(cageHot.StipulationStakes > 0);
            Assert.True(cageCold.StipulationStakes < 0);

            Assert.True(cageHot.FinalScore > plainHot.FinalScore,
                "an earned cage should be worth something on the night");
            Assert.True(cageCold.FinalScore < plainHot.FinalScore,
                "a cage nobody earned should cost, not merely fail to pay");

            Assert.Equal(Stipulation.SteelCage, cageHot.Stipulation);
        }

        /// <summary>
        /// **In a No-DQ match, the run-in moves the belt — through the show layer.**
        ///
        /// Added because a mutation survived: <see cref="ShowSimulator"/> weighing the
        /// finish with <see cref="HeatEconomy.WeightOf"/> instead of
        /// <see cref="StipulationRules.Weigh"/> passed every test in this file. The rule
        /// was asserted and the *wiring* was not, so the headline consequence of the
        /// feature — the reason a booker reaches for a stipulation to get a belt off
        /// somebody who will not lose clean — was resting on nothing.
        ///
        /// The pair is the assertion: the same booking, the same seed, the same finish,
        /// and the only difference is whether the match was announced as No DQ.
        /// </summary>
        [Fact]
        public void ARunInMovesTheBeltOnlyWhenThereAreNoRules()
        {
            bool BeltMoved(Stipulation stip)
            {
                var champion   = W("Champ", 80);
                var challenger = W("Challenger", 76);

                var registry = new TitleRegistry();
                var title = registry.Create("World Championship", TitleTier.World,
                                            Division.Mens, new DateOnly(2025, 1, 1));
                title.Standing = 60;
                title.Lineage.Add(new TitleReign
                {
                    Champion = champion, ReignNumber = 1, Won = new DateOnly(2025, 1, 1)
                });

                var plan = Singles(challenger, champion, BeatType.FinishInterference);
                plan.TitleAtStake = title;
                plan.Stipulation  = stip;
                plan.Feud         = HotFeud(challenger, champion);

                var show = new Show
                {
                    Name = "Title Night",
                    Date = new DateTime(2026, 1, 5),
                    Card = [new BookedMatch { Plan = plan }],
                    TotalDurationMinutes = 180
                };

                new ShowSimulator(new FeudBook(), seed: Seed, titles: registry).Simulate(show);
                return title.Champion == challenger;
            }

            bool underRules = BeltMoved(Stipulation.None);
            bool noDq       = BeltMoved(Stipulation.NoDisqualification);

            output.WriteLine($"  run-in finish — belt moved under the rules: {underRules}");
            output.WriteLine($"  run-in finish — belt moved with no rules:   {noDq}");

            Assert.False(underRules, "a title should not change hands on a run-in under the rules");
            Assert.True(noDq, "announce there are no rules and the run-in is a clean win");
        }

        /// <summary>
        /// **On a card, the stipulation is recorded and the second one is the stale one.**
        ///
        /// Read before the match is recorded against the counter, so a card running two
        /// cages cannot let the first launder the second.
        /// </summary>
        [Fact]
        public void RunningOneOnAShowSpendsIt()
        {
            var roster = new[] { W("A"), W("B"), W("C"), W("D") };
            var career = NewCareer(roster.ToList());
            var day    = career.CurrentDate;

            // Both earned, which the first version of this test forgot: with no feud on
            // either, both land on the unearned floor — which scarcity deliberately does
            // not scale — and the card could not show the thing it was written to show.
            // The rule was right and the setup was wrong.
            var first  = Singles(roster[0], roster[1], BeatType.FinishClean);
            var second = Singles(roster[2], roster[3], BeatType.FinishClean);
            first.Stipulation = second.Stipulation = Stipulation.SteelCage;
            first.Feud  = HotFeud(roster[0], roster[1]);
            second.Feud = HotFeud(roster[2], roster[3]);

            var show = career.Schedule("Cage Night", day, ShowType.PremiumEvent);
            show.Card.Add(new BookedMatch { Plan = first });
            show.Card.Add(new BookedMatch { Plan = second });

            var result = new ShowSimulator(career.FeudBook, seed: Seed,
                                           stipulations: career.Stipulations)
                .Simulate(show.ToShow());

            foreach (var item in result.Items)
                foreach (var note in item.Notes) output.WriteLine($"  {note}");

            Assert.Equal(0, career.Stipulations.DaysSince(Stipulation.SteelCage, day));

            // The first cage of the night was fresh; the second was not.
            Assert.True(result.Items[0].MatchResult!.StipulationStakes
                        > result.Items[1].MatchResult!.StipulationStakes,
                "the second cage on the same card should be worth less than the first");

            Assert.Contains(result.Items[1].Notes, n => n.Contains("Steel Cage"));
        }

        /// <summary>
        /// The counter survives a save, because a cooldown that resets when the player
        /// closes the tab is not a cooldown.
        /// </summary>
        [Fact]
        public void TheCooldownSurvivesASaveAndReload()
        {
            var roster = new List<Wrestler> { W("A"), W("B") };
            var career = NewCareer(roster);
            career.Stipulations.Record(Stipulation.LastManStanding, career.CurrentDate);

            var reloaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster);
            int? since = reloaded.Stipulations.DaysSince(
                Stipulation.LastManStanding, career.CurrentDate.AddDays(20));

            output.WriteLine($"  after reload, 20 days on: {since} days since");
            Assert.Equal(20, since);
            Assert.Null(reloaded.Stipulations.DaysSince(Stipulation.SteelCage, career.CurrentDate));
        }

        /// <summary>And a booked plan's own stipulation round-trips with the card.</summary>
        [Fact]
        public void ABookedStipulationRoundTrips()
        {
            var roster = new List<Wrestler> { W("A"), W("B") };
            var career = NewCareer(roster);

            var plan = Singles(roster[0], roster[1], BeatType.FinishSubmission);
            plan.Stipulation = Stipulation.IQuit;
            career.Schedule("Show", career.CurrentDate, ShowType.PremiumEvent)
                  .Card.Add(new BookedMatch { Plan = plan });

            var back = Assert.IsType<BookedMatch>(
                SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster)
                              .Shows.Single().Card.Single());

            output.WriteLine($"  {StipulationRules.Label(back.Plan.Stipulation)}");
            Assert.Equal(Stipulation.IQuit, back.Plan.Stipulation);
            Assert.Empty(back.Plan.Validate());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static MatchPlan Singles(Wrestler a, Wrestler b, BeatType finish) => new()
        {
            Sides =
            [
                new MatchSide { Members = [a] },
                new MatchSide { Members = [b] }
            ],
            Beats =
            [
                new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                new MatchBeat { Type = BeatType.HeatSegment,     Control = BeatControl.WrestlerB },
                new MatchBeat { Type = BeatType.Comeback,        Control = BeatControl.WrestlerA },
                new MatchBeat { Type = BeatType.NearFall,        Control = BeatControl.WrestlerA },
                new MatchBeat { Type = finish,                   Control = BeatControl.WrestlerA }
            ]
        };

        private static Feud HotFeud(Wrestler a, Wrestler b)
        {
            var feud = new Feud { Camps = [[a], [b]] };
            feud.AddHeat(35);
            return feud;
        }

        private static Career NewCareer(List<Wrestler> roster)
        {
            var start = new DateOnly(2026, 1, 5);
            return new Career
            {
                Promotion   = new Promotion { Name = "Cage Wrestling" },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster
            };
        }
    }
}
