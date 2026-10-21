using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **The three-way, rebuilt from the rule instead of from a list.**
    ///
    /// The shipped `Triple Threat` was eight beats and twelve minutes, and so were
    /// `The Grudge Three-Way`, `Triple Threat Elimination` and `Fatal Four-Way`. Every
    /// format in the library was the same length whoever was in it, none of them could be
    /// booked as a main event, and none of them contained the thing doc 18 §2.5 calls "the
    /// format's single best story" — because the engine had no beat for it.
    ///
    /// Two claims are checked here, and they are the two the reference makes:
    ///
    /// - **The alliance and its betrayal exist**, and the booker chooses whether it breaks.
    /// - **Nobody is left on the floor unexplained.** Doc 18 §2.5: "the entire craft of a
    ///   multi-man match is disposing of people plausibly and then bringing them back at the
    ///   right moment. A triple threat that never explains where the third man went is the
    ///   format's characteristic failure."
    /// </summary>
    public class MultiManGrammarTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name, WrestlingStyle style = WrestlingStyle.Technical)
        {
            var w = TestRoster.Make(name);
            w.Style = style;
            return w;
        }

        private static List<MatchSide> Three() =>
        [
            new MatchSide { Members = [W("Alpha")] },
            new MatchSide { Members = [W("Bravo", WrestlingStyle.Powerhouse)] },
            new MatchSide { Members = [W("Charlie", WrestlingStyle.HighFlyer)] }
        ];

        private static List<MatchSide> Four() =>
        [
            new MatchSide { Members = [W("Alpha")] },
            new MatchSide { Members = [W("Bravo", WrestlingStyle.Powerhouse)] },
            new MatchSide { Members = [W("Charlie", WrestlingStyle.HighFlyer)] },
            new MatchSide { Members = [W("Delta", WrestlingStyle.Brawler)] }
        ];

        private WrittenMatch Write(MatchBrief brief, IReadOnlyList<MatchSide> sides)
        {
            var written = BriefDirector.Write(brief, sides);
            foreach (var phase in written.Phases)
                output.WriteLine($"  {phase.Phase,-10} {phase.Name,-24} " +
                                 $"{phase.Beat.Control,-10} vs {phase.Beat.Against?.ToString() ?? "-"}");
            return written;
        }

        // ── The story the format did not have ────────────────────────────────

        /// <summary>
        /// **Two of them work the third, and then it breaks.**
        ///
        /// Doc 18 §2.5 names this as the best thing the format has, and until now the game
        /// could not express it: every multi-man beat in the library was about *removing*
        /// somebody, and none of them could say two people were working together.
        /// </summary>
        [Fact]
        public void AnAllianceAndItsBetrayalAreBothInTheMatch()
        {
            var brief = new MatchBrief
            {
                Story = MatchStory.EvenContest, Length = MatchScale.Workhorse,
                WinningSide = 2, Alliance = (0, 1), AllianceBreaks = true
            };

            var written = Write(brief, Three());

            var alliance = Assert.Single(written.Beats, b => b.Type == BeatType.Alliance);
            var betrayal = Assert.Single(written.Beats, b => b.Type == BeatType.Betrayal);

            // Worked by the allies, against the one who is not in it.
            Assert.Equal(BeatControl.WrestlerA, alliance.Control);
            Assert.Equal(BeatControl.SideC,     alliance.Against);

            // And the betrayal is one ally on the other, not on the outsider.
            Assert.Equal(BeatControl.WrestlerB, betrayal.Control);
            Assert.Equal(BeatControl.WrestlerA, betrayal.Against);

            // In that order, and before the match settles into a pair.
            var order = written.Beats.ToList();
            Assert.True(order.IndexOf(alliance) < order.IndexOf(betrayal));
        }

        /// <summary>
        /// An alliance that holds is a legitimate booking and a quieter one. Two of them can
        /// simply beat the third, and the booker gets to choose which.
        /// </summary>
        [Fact]
        public void AnAllianceCanHold()
        {
            var brief = new MatchBrief
            {
                Story = MatchStory.Grudge, Length = MatchScale.BigMatch,
                WinningSide = 1, Alliance = (0, 1), AllianceBreaks = false
            };

            var written = Write(brief, Three());

            Assert.Contains(written.Beats, b => b.Type == BeatType.Alliance);
            Assert.DoesNotContain(written.Beats, b => b.Type == BeatType.Betrayal);
        }

        /// <summary>No alliance asked for, none booked. And no betrayal without one.</summary>
        [Fact]
        public void NoAllianceMeansNoBetrayal()
        {
            var brief = new MatchBrief
            {
                Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse, WinningSide = 0
            };

            var written = Write(brief, Three());

            Assert.DoesNotContain(written.Beats, b => b.Type is BeatType.Alliance or BeatType.Betrayal);
        }

        /// <summary>
        /// An alliance between the only two people in the match is not a thing that can
        /// happen, and asking for one in a singles match is quietly ignored rather than
        /// producing a beat that names a side which is not there.
        /// </summary>
        [Fact]
        public void ASinglesMatchCannotHaveAnAlliance()
        {
            var brief = new MatchBrief
            {
                Length = MatchScale.Workhorse, Alliance = (0, 1), AllianceBreaks = true
            };

            var sides = new List<MatchSide>
            {
                new() { Members = [W("Alpha")] },
                new() { Members = [W("Bravo")] }
            };

            var written = BriefDirector.Write(brief, sides);

            Assert.Null(brief.OutnumberedSide(2));
            Assert.DoesNotContain(written.Beats, b => b.Type is BeatType.Alliance or BeatType.Betrayal);
        }

        // ── The bracketing rule ──────────────────────────────────────────────

        /// <summary>
        /// **Nobody is on the floor unexplained.**
        ///
        /// Doc 18 §2.5's central claim, as a test: everything between the opening and the
        /// finishing stretch is a two-person passage, so in a three-way it has to be opened
        /// by putting the odd one out somewhere and closed by bringing them back.
        ///
        /// One bracket around the whole run rather than one per beat. A disposal before
        /// every cut-off would be absurd and the crowd stops believing the fourth one
        /// anyway.
        /// </summary>
        [Theory]
        [InlineData(MatchScale.Television)]
        [InlineData(MatchScale.Workhorse)]
        [InlineData(MatchScale.BigMatch)]
        [InlineData(MatchScale.Epic)]
        public void TheTwoPersonPassageIsAlwaysBracketed(MatchScale scale)
        {
            var brief = new MatchBrief { Story = MatchStory.EvenContest, Length = scale, WinningSide = 0 };
            var shape = PhaseGrammar.Shape(brief, sideCount: 3);

            var phases = shape.Select(s => s.Phase).ToList();
            output.WriteLine($"  {scale}: {string.Join(" ", phases)}");

            int opened = phases.IndexOf(MatchPhase.Disposal);
            int closed = phases.IndexOf(MatchPhase.Return);

            Assert.True(opened >= 0, "somebody has to be put somewhere");
            Assert.True(closed > opened, "and brought back afterwards");

            // Every two-person phase sits inside the bracket.
            foreach (var twoHanded in new[] { MatchPhase.Cutoff, MatchPhase.Heat, MatchPhase.Comeback })
            {
                foreach (int at in Enumerable.Range(0, phases.Count).Where(i => phases[i] == twoHanded))
                {
                    Assert.True(at > opened, $"{twoHanded} at {at} happens before anybody was disposed of");
                    Assert.True(at < closed, $"{twoHanded} at {at} happens after everybody is back");
                }
            }
        }

        /// <summary>
        /// A singles match is not bracketed, because there is nobody to bracket. The rule
        /// has to be about the third man rather than about multi-man matches in general, or
        /// it would put a disposal spot in every television match in the game.
        /// </summary>
        [Fact]
        public void ASinglesMatchIsNotBracketed()
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse };
            var phases = PhaseGrammar.Shape(brief).Select(s => s.Phase).ToList();

            output.WriteLine("  " + string.Join(" ", phases));

            Assert.DoesNotContain(MatchPhase.Disposal, phases);
            Assert.DoesNotContain(MatchPhase.Return, phases);
        }

        /// <summary>
        /// **The fall cannot be counted with all three of them standing.**
        ///
        /// Doc 18 §2.5: "the disposal is what makes the near falls mean anything — outside a
        /// disposal window every cover in a three-way is breakable and the crowd knows it."
        /// So the last thing before the finish is removing one of them, which is also how
        /// nearly every good three-way actually ends.
        /// </summary>
        [Fact]
        public void SomebodyIsRemovedImmediatelyBeforeTheFall()
        {
            var brief = new MatchBrief
            {
                Story = MatchStory.EvenContest, Length = MatchScale.Workhorse, WinningSide = 1
            };

            var shape = PhaseGrammar.Shape(brief, sideCount: 3);

            Assert.Equal(MatchPhase.Finish,   shape[^1].Phase);
            Assert.Equal(MatchPhase.Disposal, shape[^2].Phase);

            // And it is not the winner being removed from their own finish.
            Assert.NotEqual(shape[^1].Control, shape[^2].Against);
        }

        /// <summary>
        /// **The fall names who takes it, and it is not the one who was just dumped.**
        ///
        /// `MatchPlan.Validate` refuses a three-way finish that names a winner and not a
        /// loser, because in this format those are genuinely different questions — that is
        /// the whole reason to book one. The first version of the grammar left it null and
        /// produced an unrunnable plan for every multi-man brief; the validator caught it,
        /// which is what a validator is for.
        /// </summary>
        [Fact]
        public void TheFallGoesOnWhoeverIsLeftStanding()
        {
            var brief = new MatchBrief
            {
                Story = MatchStory.EvenContest, Length = MatchScale.Workhorse, WinningSide = 2
            };

            var shape  = PhaseGrammar.Shape(brief, sideCount: 3);
            var finish = shape[^1];
            var last   = shape[^2];

            output.WriteLine($"  disposal: {last.Control} removes {last.Against}");
            output.WriteLine($"  finish:   {finish.Control} pins {finish.Against}");

            Assert.NotNull(finish.Against);
            Assert.NotEqual(finish.Control, finish.Against);
            Assert.NotEqual(last.Against,   finish.Against);
        }

        // ── It has to run ────────────────────────────────────────────────────

        /// <summary>
        /// **Every multi-man brief produces a plan the engine accepts.**
        ///
        /// Seven stories by five lengths by three alliance settings, in three-way and
        /// four-way. A generator that emits an invalid plan is worse than one that emits a
        /// dull one: the booker gets an error on a sheet they did not write.
        /// </summary>
        [Fact]
        public void EveryMultiManBriefProducesALegalPlan()
        {
            int checkedPlans = 0;

            foreach (var maker in new Func<List<MatchSide>>[] { Three, Four })
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            foreach (var alliance in new (int, int)?[] { null, (0, 1), (1, 2) })
            foreach (bool breaks in new[] { true, false })
            {
                var sides = maker();
                var brief = new MatchBrief
                {
                    Story = story, Length = scale, WinningSide = sides.Count - 1,
                    Alliance = alliance, AllianceBreaks = breaks
                };

                var plan = new MatchPlan
                {
                    Sides = sides,
                    Beats = BriefDirector.Write(brief, sides).Beats.ToList()
                };

                var errors = plan.Validate();
                if (errors.Count > 0)
                    output.WriteLine($"  {sides.Count}-way {story}/{scale}/{alliance}/{breaks}: " +
                                     string.Join("; ", errors));

                Assert.Empty(errors);
                checkedPlans++;
            }

            output.WriteLine($"  {checkedPlans} multi-man briefs, every one of them a legal plan");
        }

        /// <summary>
        /// And they run through the engine without throwing, which is a different question
        /// from being valid: a beat can pass the plan validator and still name a side the
        /// handler cannot resolve.
        /// </summary>
        [Fact]
        public void EveryMultiManBriefRunsThroughTheEngine()
        {
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            foreach (var alliance in new (int, int)?[] { null, (0, 1), (1, 2), (0, 2) })
            {
                var sides = Three();
                var brief = new MatchBrief
                {
                    Story = story, Length = MatchScale.Workhorse, WinningSide = 2,
                    Alliance = alliance, AllianceBreaks = true
                };

                var plan = new MatchPlan
                {
                    Sides = sides,
                    Beats = BriefDirector.Write(brief, sides).Beats.ToList(),
                    Brief = brief
                };

                var result = new MatchEngine(StableSeed.From("multiman", story, alliance?.Item1))
                    .Execute(plan);

                Assert.True(result.FinalScore > 0);
                Assert.Equal(sides[2].Members[0], result.Winner);
            }
        }

        /// <summary>
        /// The alliance and the betrayal reach the play-by-play, which is the difference
        /// between a mechanic and a mechanic the player can see. A beat that scores and
        /// never says anything is a number moving on its own.
        /// </summary>
        [Fact]
        public void TheAllianceAndTheBetrayalAreCalled()
        {
            var sides = Three();
            var brief = new MatchBrief
            {
                Story = MatchStory.EvenContest, Length = MatchScale.Workhorse,
                WinningSide = 2, Alliance = (0, 1), AllianceBreaks = true
            };

            var plan = new MatchPlan
            {
                Sides = sides,
                Beats = BriefDirector.Write(brief, sides).Beats.ToList()
            };

            var result = new MatchEngine(StableSeed.From("called")).Execute(plan);
            var lines = result.BeatResults.SelectMany(b => b.Commentary).ToList();

            foreach (var line in lines) output.WriteLine("  " + line);

            // The alliance names both allies and the one they are working over.
            Assert.Contains(lines, l => l.Contains("Alpha") && l.Contains("Bravo") && l.Contains("Charlie"));

            // And the betrayal says somebody turned.
            Assert.Contains(lines, l => l.Contains("turns on") || l.Contains("moves first")
                                     || l.Contains("truce"));
        }

        // ── The old library, as briefs ───────────────────────────────────────

        /// <summary>
        /// **A three-way can be a main event now.**
        ///
        /// Every multi-man structure the library shipped was eight beats, and the longest
        /// was twelve minutes — doc 18 §3.1's television length. There was no way to book a
        /// three-way that ran like the semi-main it usually is.
        /// </summary>
        [Fact]
        public void AThreeWayCanRunAsLongAsTheMatchDeserves()
        {
            var old = MatchStructureLibrary.Find("Triple Threat")!;
            int oldMinutes = old.Beats.Sum(b => b.DurationMinutes);

            var epic = BriefDirector.Write(
                new MatchBrief
                {
                    Story = MatchStory.EvenContest, Length = MatchScale.Epic, WinningSide = 2,
                    Alliance = (0, 1), AllianceBreaks = true
                },
                Three());

            output.WriteLine($"  library: {old.Beats.Count} beats / {oldMinutes} min");
            output.WriteLine($"  as an epic: {epic.Beats.Count} beats / {epic.Minutes} min");

            Assert.Equal(8, old.Beats.Count);
            Assert.True(epic.Beats.Count > 16);
            Assert.True(epic.Minutes > 30);
        }

        /// <summary>
        /// **The multi-man presets only appear where they make sense.**
        ///
        /// A chip called "Triple Threat" on a singles match is a control whose every option
        /// is wrong, and the old structure picker had exactly that problem in reverse: it
        /// listed tag structures for a three-way and disabled them.
        /// </summary>
        [Fact]
        public void MultiManPresetsAreOnlyOfferedToMultiManMatches()
        {
            var singles = BriefPresets.For(2).Select(p => p.Name).ToList();
            var threeWay = BriefPresets.For(3).Select(p => p.Name).ToList();

            output.WriteLine("  singles:  " + string.Join(", ", singles));
            output.WriteLine("  three-way: " + string.Join(", ", threeWay));

            Assert.DoesNotContain("Triple Threat", singles);
            Assert.Contains("Triple Threat", threeWay);

            // And the singles ones stay available, because a three-way is still a match.
            Assert.Contains("Face-in-Peril", threeWay);
            Assert.True(threeWay.Count > singles.Count);
        }

        /// <summary>
        /// **A preset's alliance is resolved against the cast, not stored as side indices.**
        ///
        /// The preset does not know who is in the match and the same chip has to work on any
        /// three-way, so it says what kind of alliance the shape wants: the two who are not
        /// booked to win. That rule produces a different pair depending on who is going over,
        /// which is the point.
        /// </summary>
        [Theory]
        [InlineData(0, 1, 2)]
        [InlineData(1, 0, 2)]
        [InlineData(2, 0, 1)]
        public void APresetsAllianceFollowsWhoIsBookedToWin(int winner, int first, int second)
        {
            var resolved = BriefPresets.Resolve(AllianceHint.AgainstTheWinner, winner, sideCount: 3);

            output.WriteLine($"  side {winner} goes over, so {first} and {second} team up");
            Assert.Equal((first, second), resolved);
        }

        /// <summary>And it resolves to nothing where there is nobody to form one with.</summary>
        [Fact]
        public void APresetsAllianceIsNothingInASinglesMatch()
        {
            Assert.Null(BriefPresets.Resolve(AllianceHint.AgainstTheWinner, 0, sideCount: 2));
            Assert.Null(BriefPresets.Resolve(AllianceHint.None, 0, sideCount: 3));
        }

        /// <summary>
        /// Every multi-man preset produces a legal, runnable match on the shape it is offered
        /// for. These are the fast path, so one of them being broken is a broken button.
        /// </summary>
        [Fact]
        public void EveryMultiManPresetProducesAMatchWorthBooking()
        {
            foreach (var preset in BriefPresets.For(3).Where(p => p.MinimumSides >= 3))
            {
                var sides = Three();
                var brief = preset.Brief.Clone();
                brief.WinningSide = 2;
                brief.Alliance = BriefPresets.Resolve(preset.Alliance, brief.WinningSide, 3);
                brief.AllianceBreaks = preset.Alliance != AllianceHint.AgainstTheWinnerAndHolds;

                var written = BriefDirector.Write(brief, sides);
                var plan = new MatchPlan { Sides = sides, Beats = written.Beats.ToList() };

                output.WriteLine($"  {preset.Name,-22} {written.Beats.Count,2} beats {written.Minutes,3} min" +
                                 (brief.Alliance is { } a ? $"  alliance {a.First}+{a.Second}" : ""));

                Assert.Empty(plan.Validate());
                Assert.Contains(written.Beats, b => b.Type == BeatType.DisposalSpot);
            }
        }

        /// <summary>
        /// And the short one is still available, because a three-way on television is a real
        /// booking and the point was never that they should all be long.
        /// </summary>
        [Fact]
        public void AThreeWayCanStillBeShort()
        {
            var written = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.Spectacle, Length = MatchScale.Opener, WinningSide = 1 },
                Three());

            output.WriteLine($"  {written.Beats.Count} beats / {written.Minutes} min");
            Assert.True(written.Minutes <= 14);
        }
    }
}
