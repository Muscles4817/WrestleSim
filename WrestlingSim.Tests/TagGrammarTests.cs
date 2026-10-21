using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **A tag match is the same skeleton with different furniture.**
    ///
    /// Doc 18 §2.3's stages do not change when there are two people a side. What changes is
    /// what fills them: the heat becomes an isolation, because the thing being denied is a
    /// corner rather than a comeback; the hope spot becomes a near tag; the comeback becomes
    /// a hot tag. That is the whole argument for one grammar rather than a second structure
    /// library for tags, and it is what this file checks.
    ///
    /// Two things a tag match has that a singles match has no room for: the tandem offence
    /// the isolation was building to, and the save — a near fall in a tag match is broken up
    /// by a partner rather than surviving on its own, which is why the format's near falls
    /// read differently.
    /// </summary>
    public class TagGrammarTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name, WrestlingStyle style = WrestlingStyle.Technical)
        {
            var w = TestRoster.Make(name);
            w.Style = style;
            return w;
        }

        private static List<MatchSide> Teams(int perSide)
        {
            var names = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel" };
            return
            [
                new MatchSide { Members = names.Take(perSide).Select(n => W(n)).ToList() },
                new MatchSide { Members = names.Skip(perSide).Take(perSide).Select(n => W(n)).ToList() }
            ];
        }

        private WrittenMatch Write(MatchBrief brief, int perSide)
        {
            var written = BriefDirector.Write(brief, Teams(perSide));
            foreach (var phase in written.Phases)
                output.WriteLine($"  {phase.Phase,-10} {phase.Name,-22} {phase.Beat.Control,-10} " +
                                 $"in={phase.Beat.IncomingIndex?.ToString() ?? "-"}");
            return written;
        }

        // ── The furniture ────────────────────────────────────────────────────

        /// <summary>
        /// **Heat becomes isolation, hope spot becomes near tag, comeback becomes hot tag.**
        ///
        /// Three substitutions and the shape is a tag match. The singles beats do not appear
        /// at all, because a heat segment in a tag match is a beat that has forgotten there
        /// is a corner to be kept away from.
        /// </summary>
        [Fact]
        public void TheStagesAreTheSameAndTheBeatsAreNot()
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse };

            var singles = BriefDirector.Write(brief, Teams(1));
            output.WriteLine("  singles:");
            foreach (var p in singles.Phases) output.WriteLine($"    {p.Phase,-10} {p.Name}");

            output.WriteLine("  tag:");
            var tag = Write(brief, 2);

            // Same stages, in the same order, as far as the tag payoff.
            var stages = new[] { MatchPhase.Opening, MatchPhase.Shine, MatchPhase.Cutoff,
                                 MatchPhase.Heat, MatchPhase.HopeSpot };

            foreach (var stage in stages)
                Assert.Equal(singles.Phases.Count(p => p.Phase == stage),
                             tag.Phases.Count(p => p.Phase == stage));

            // Different beats filling them.
            Assert.Contains(tag.Beats, b => b.Type == BeatType.Isolation);
            Assert.Contains(tag.Beats, b => b.Type == BeatType.NearTag);
            Assert.Contains(tag.Beats, b => b.Type == BeatType.HotTag);

            Assert.DoesNotContain(tag.Beats, b => b.Type == BeatType.HeatSegment);
            Assert.DoesNotContain(tag.Beats, b => b.Type == BeatType.HopeSpot);
            Assert.DoesNotContain(tag.Beats, b => b.Type == BeatType.Comeback);

            // And the singles version has none of the tag ones.
            Assert.DoesNotContain(singles.Beats, b => b.Type is BeatType.Isolation
                                                          or BeatType.NearTag or BeatType.HotTag);
        }

        /// <summary>
        /// **What a hot tag buys.** The comeback has a second half a singles match has no
        /// room for: the tandem offence the crowd spent the whole isolation waiting for, and
        /// then everybody in at once.
        /// </summary>
        [Fact]
        public void TheHotTagIsPaidOffWithTandemOffenceAndEverybodyIn()
        {
            var written = Write(new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse }, 2);

            var phases = written.Phases.Select(p => p.Phase).ToList();

            int hotTag = phases.IndexOf(MatchPhase.Comeback);
            int tandem = phases.IndexOf(MatchPhase.Tandem);
            int allFour = phases.IndexOf(MatchPhase.AllFour);

            Assert.True(hotTag >= 0 && tandem > hotTag, "the tandem offence follows the hot tag");
            Assert.True(allFour > tandem, "and then it breaks down entirely");

            Assert.Contains(written.Beats, b => b.Type == BeatType.DoubleTeam);
            Assert.Contains(written.Beats, b => b.Type == BeatType.AllFourBrawl);
        }

        /// <summary>
        /// Everybody in is a *loss of control*, and a six-minute tag has not established
        /// enough control to lose. The tandem offence still happens, because that is the
        /// payoff rather than the chaos.
        /// </summary>
        [Fact]
        public void TheShortestTagDoesNotBreakDown()
        {
            var written = Write(new MatchBrief { Story = MatchStory.EvenContest, Length = MatchScale.Opener }, 2);

            Assert.Contains(written.Phases, p => p.Phase == MatchPhase.Tandem);
            Assert.DoesNotContain(written.Phases, p => p.Phase == MatchPhase.AllFour);
        }

        /// <summary>
        /// **A near fall in a tag match is broken up rather than surviving on its own.**
        ///
        /// It is why the format's near falls read differently: the count is never the only
        /// question, because there is always somebody who might get there.
        /// </summary>
        [Fact]
        public void TheStretchIsBrokenUpByAPartner()
        {
            var tag     = Write(new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse }, 2);
            var singles = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse }, Teams(1));

            Assert.Contains(tag.Beats, b => b.Type == BeatType.SaveBreakup);
            Assert.DoesNotContain(singles.Beats, b => b.Type == BeatType.SaveBreakup);

            // And it lands between the near falls and the finish.
            var phases = tag.Phases.Select(p => p.Phase).ToList();
            Assert.True(phases.IndexOf(MatchPhase.Save) > phases.LastIndexOf(MatchPhase.Stretch));
            Assert.True(phases.IndexOf(MatchPhase.Save) < phases.IndexOf(MatchPhase.Finish));
        }

        /// <summary>
        /// No near falls, no save. A match with nothing to break up does not need somebody to
        /// break it, and booking one would be a beat with no beat in front of it.
        /// </summary>
        [Fact]
        public void NoStretchMeansNoSave()
        {
            var written = Write(new MatchBrief { Story = MatchStory.EvenContest, Length = MatchScale.Television }, 2);

            Assert.Equal(0, PhaseGrammar.NearFalls(MatchScale.Television));
            Assert.DoesNotContain(written.Phases, p => p.Phase == MatchPhase.Save);
        }

        // ── Trios: the deeper heat ───────────────────────────────────────────

        /// <summary>
        /// **What the third body buys is a deeper heat, not a longer one.**
        ///
        /// Doc 18 §2.5: "three fresh opponents rotating on one man, which two a side cannot
        /// book". So the valley gains a rotation, and the beating changes bodies rather than
        /// running on.
        /// </summary>
        [Fact]
        public void TriosRotateFreshBodiesThroughTheHeat()
        {
            var tag   = Write(new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.BigMatch }, 2);
            output.WriteLine("  ── trios ──");
            var trios = Write(new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.BigMatch }, 3);

            Assert.DoesNotContain(tag.Phases,   p => p.Phase == MatchPhase.Rotation);
            Assert.Contains(trios.Phases, p => p.Phase == MatchPhase.Rotation);

            // The rotation happens on the side working the heat.
            foreach (var rotation in trios.Phases.Where(p => p.Phase == MatchPhase.Rotation))
                Assert.Equal(BeatControl.WrestlerB, rotation.Beat.Control);
        }

        /// <summary>
        /// **And a different partner each time.**
        ///
        /// The first version counted the incoming partner per cycle rather than per match, so
        /// a two-cycle trios tagged in the same person twice — the second time while they
        /// were already the legal one. `MatchPlan.Validate` refuses that, which is how it was
        /// found, and it is exactly the kind of fault a beat list hides and a generator does
        /// not.
        /// </summary>
        [Fact]
        public void EachRotationBringsInSomebodyNew()
        {
            var trios = Write(new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.BigMatch }, 3);

            var incoming = trios.Phases
                .Where(p => p.Phase == MatchPhase.Rotation)
                .Select(p => p.Beat.IncomingIndex)
                .ToList();

            output.WriteLine("  incoming: " + string.Join(", ", incoming));

            Assert.True(incoming.Count >= 2, "a big match has room for two rotations");
            Assert.Equal(incoming.Count, incoming.Distinct().Count());
            Assert.DoesNotContain(0, incoming);
        }

        /// <summary>Nobody is ever tagged in who is not on the side. </summary>
        [Fact]
        public void NobodyIsTaggedInWhoIsNotThere()
        {
            // Every story, not just the one the rotation was written for. The first version
            // of this swept only Face-in-Peril, and lucha rotates on a *different* phase —
            // so the branch that actually counts bodies fastest was the one branch the
            // legality check could not see.
            foreach (int perSide in new[] { 2, 3, 4 })
            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            {
                var written = BriefDirector.Write(
                    new MatchBrief { Story = story, Length = scale }, Teams(perSide));

                foreach (var beat in written.Beats.Where(b => b.IncomingIndex is not null))
                    Assert.InRange(beat.IncomingIndex!.Value, 1, perSide - 1);
            }
        }

        /// <summary>
        /// **A tag shine is longer than a singles shine.**
        ///
        /// Doc 18 §2.3's shine is one wrestler looking good; a tag team's is two of them
        /// looking good together, with the quick tags that establish they can work as a unit,
        /// and that takes more than the singles beat's worth of time. The short match is the
        /// interesting case, because a big singles match already buys the longer shine on
        /// length alone — so the claim is only visible where the length is not paying for it.
        /// </summary>
        [Theory]
        [InlineData(MatchScale.Television)]
        [InlineData(MatchScale.Workhorse)]
        public void TheTeamGetsLongerToLookGoodTogether(MatchScale scale)
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = scale };

            var singles = PhaseGrammar.Shape(brief, 2, 1).First(p => p.Phase == MatchPhase.Shine);
            var team    = PhaseGrammar.Shape(brief, 2, 2).First(p => p.Phase == MatchPhase.Shine);

            output.WriteLine($"{scale}: singles={singles.Duration} tag={team.Duration}");

            Assert.Equal(BeatDuration.Short,  singles.Duration);
            Assert.Equal(BeatDuration.Medium, team.Duration);
        }

        // ── Lucha is a different match ───────────────────────────────────────

        /// <summary>
        /// **Lucha rotates instead of resting.**
        ///
        /// Doc 25 §3.3 is explicit that three a side is the *default* in lucha rather than a
        /// variant, and that the rapid tag rules allow constant motion. So the beat where an
        /// American six-man slows down is the beat where this one changes bodies, and there
        /// is no long isolation for anybody to be denied a corner from.
        /// </summary>
        [Fact]
        public void ALuchaTriosHasNoIsolationAndDoesNotSlowDown()
        {
            var american = Write(new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse }, 3);
            output.WriteLine("  ── lucha ──");
            var lucha    = Write(new MatchBrief { Story = MatchStory.Spectacle,   Length = MatchScale.Workhorse }, 3);

            Assert.Contains(american.Beats, b => b.Type == BeatType.Isolation);
            Assert.DoesNotContain(lucha.Beats, b => b.Type == BeatType.Isolation);

            // The American six-man rests in its valley; the lucha changes bodies there.
            Assert.Contains(american.Phases, p => p.Phase == MatchPhase.Rest);
            Assert.DoesNotContain(lucha.Phases, p => p.Phase == MatchPhase.Rest);
            Assert.Contains(lucha.Phases, p => p.Phase == MatchPhase.Rotation);
        }

        /// <summary>
        /// **And it moves more, not less.**
        ///
        /// The claim doc 25 §3.3 makes is "constant motion", which is a claim about how often
        /// the bodies change and not merely about which beat they change on. The first
        /// version of the rotation had lucha rotate *instead of* the American six-man's
        /// rotation rather than as well, and there is at most one rest beat in a valley — so
        /// a lucha trios changed bodies once where the American six-man of the same length
        /// changed them twice. It read as the format's opposite, and every test passed,
        /// because each of them asked where the rotation was rather than how many there
        /// were.
        /// </summary>
        [Theory]
        [InlineData(MatchScale.Workhorse)]
        [InlineData(MatchScale.BigMatch)]
        [InlineData(MatchScale.Epic)]
        public void LuchaChangesBodiesAtLeastAsOftenAsTheAmericanSixMan(MatchScale scale)
        {
            int Rotations(MatchStory story) =>
                PhaseGrammar.Shape(new MatchBrief { Story = story, Length = scale }, 2, 3)
                            .Count(p => p.Phase == MatchPhase.Rotation);

            int american = Rotations(MatchStory.FaceInPeril);
            int lucha    = Rotations(MatchStory.Spectacle);

            output.WriteLine($"{scale}: american={american} lucha={lucha}");

            Assert.True(american > 0, "the six-man is the baseline and has to rotate at all");
            Assert.True(lucha >= american,
                $"lucha rotated {lucha} times against the six-man's {american}");
        }

        /// <summary>
        /// A singles spotfest still rests. Lucha's constant motion is a claim about *lucha*,
        /// not about spectacle in general — a one-on-one high-spot match has breathers and
        /// always did.
        /// </summary>
        [Fact]
        public void ASinglesSpotfestStillHasABreather()
        {
            var written = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.Spectacle, Length = MatchScale.Workhorse }, Teams(1));

            Assert.Contains(written.Phases, p => p.Phase == MatchPhase.Rest);
            Assert.DoesNotContain(written.Phases, p => p.Phase == MatchPhase.Rotation);
        }

        /// <summary>
        /// **The pools with two beats in them use both.**
        ///
        /// A rotation is a tag or a blind tag, and a lucha heat is a double team or a high
        /// spot. Both pairs were written as pools rather than as single beats on purpose: a
        /// blind tag is a different moment from a called one, and a booker who re-rolls the
        /// same brief should see it. Narrowing either pool to its first member left every
        /// test green, because each of them asked what *kind* of beat filled a phase and
        /// none asked whether the second member was ever chosen.
        /// </summary>
        [Theory]
        [InlineData(MatchStory.FaceInPeril, MatchPhase.Rotation, BeatType.Tag,        BeatType.BlindTag)]
        [InlineData(MatchStory.Spectacle,   MatchPhase.Heat,     BeatType.DoubleTeam, BeatType.HighSpot)]
        public void BothHalvesOfAPoolGetPicked(
            MatchStory story, MatchPhase phase, BeatType first, BeatType second)
        {
            var sides = Teams(3);
            var seen  = new HashSet<BeatType>();

            for (int draft = 0; draft < 40; draft++)
            {
                var brief = new MatchBrief
                {
                    Story = story, Length = MatchScale.Workhorse, Draft = draft
                };

                foreach (var written in BriefDirector.Write(brief, sides).Phases
                                                     .Where(w => w.Phase == phase))
                    seen.Add(written.Beat.Type);
            }

            output.WriteLine($"{story} {phase}: {string.Join(", ", seen)}");

            Assert.Contains(first,  seen);
            Assert.Contains(second, seen);
        }

        /// <summary>
        /// **And it never changes bodies twice in a row.**
        ///
        /// Constant motion is not two tags with nothing between them. The lucha valley has
        /// one rotation after the hope spot and another where the rest would have been, and
        /// once both were switched on they landed adjacent — on the written sheet the pair
        /// read as one long tag with a dead beat in it.
        /// </summary>
        [Fact]
        public void NoTwoRotationsAreAdjacent()
        {
            foreach (int perSide in new[] { 2, 3, 4 })
            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            {
                var shape = PhaseGrammar.Shape(
                    new MatchBrief { Story = story, Length = scale }, 2, perSide);

                for (int i = 1; i < shape.Count; i++)
                    Assert.False(shape[i].Phase == MatchPhase.Rotation &&
                                 shape[i - 1].Phase == MatchPhase.Rotation,
                                 $"{perSide} a side, {scale} {story}: two rotations at {i - 1}");
            }
        }

        // ── The booker can reach it ──────────────────────────────────────────

        /// <summary>
        /// **The tag presets are offered to the matches that can book them, and to no others.**
        ///
        /// A chip called "Lucha Trios" on a one-on-one match is a control whose every option
        /// is wrong. Side *count* is the wrong gate for this: a trios is two sides of three,
        /// so gating it on sides would offer it to a three-way, which cannot book it, and
        /// hide it from the match it was written for.
        /// </summary>
        [Fact]
        public void TagPresetsAreOnlyOfferedWhereThereAreBodiesForThem()
        {
            List<string> Offered(int perSide) =>
                BriefPresets.For(2, perSide).Select(p => p.Name).ToList();

            var singles = Offered(1);
            var tag     = Offered(2);
            var trios   = Offered(3);
            var threeWay = BriefPresets.For(3).Select(p => p.Name).ToList();

            output.WriteLine($"  singles: {string.Join(", ", singles)}");
            output.WriteLine($"  tag:     {string.Join(", ", tag)}");
            output.WriteLine($"  trios:   {string.Join(", ", trios)}");

            foreach (var tagOnly in new[] { "Southern Tag", "Tag Sprint" })
            {
                Assert.DoesNotContain(tagOnly, singles);
                Assert.Contains(tagOnly, tag);
                Assert.Contains(tagOnly, trios);
            }

            foreach (var triosOnly in new[] { "Six-Man War", "Lucha Trios" })
            {
                Assert.DoesNotContain(triosOnly, tag);
                Assert.Contains(triosOnly, trios);

                // And a three-way is three sides of one, so it gets none of them.
                Assert.DoesNotContain(triosOnly, threeWay);
            }

            // The singles presets are still there. A tag Face-in-Peril is a Face-in-Peril.
            Assert.Contains("Face-in-Peril", tag);
        }

        /// <summary>
        /// **The chip that lights up is the one the booker tapped.**
        ///
        /// Several presets carry the same brief, because the shape does not change with the
        /// body count — only the furniture does. So the name a brief matches has to depend on
        /// the match as well as on the brief, or tapping "Southern Tag" lights "Face-in-Peril"
        /// and the control looks broken.
        /// </summary>
        [Theory]
        [InlineData(1, "Face-in-Peril")]
        [InlineData(2, "Southern Tag")]
        [InlineData(3, "Southern Tag")]
        public void AProvokedBriefMatchesTheMostSpecificPresetTheMatchAllows(
            int perSide, string expected)
        {
            var brief = BriefPresets.Find("Southern Tag")!.Value.Brief;

            Assert.Equal(expected, BriefPresets.Matching(brief, sideCount: 2, perSide: perSide));
        }

        /// <summary>
        /// **The suggested preset is one this match can actually book.**
        ///
        /// The star beside a chip says "this suits them". Suggesting a name that is not in
        /// the offered list does not misfire loudly — it renders no star at all, on every
        /// chip, and the feature quietly stops existing. That is the failure worth a test,
        /// rather than any particular pairing of story to name.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void WhatIsSuggestedIsSomethingTheMatchCanBook(int perSide)
        {
            var offered = BriefPresets.For(2, perSide).Select(p => p.Name).ToList();

            foreach (MatchStory wants in Enum.GetValues<MatchStory>())
            {
                string suggested = BriefPresets.SuggestedFor(
                    new Expectation(wants, 0.8, ""), perSide);

                output.WriteLine($"  {perSide} a side, wants {wants,-20} → {suggested}");
                Assert.Contains(suggested, offered);
            }
        }

        /// <summary>
        /// And where a tag preset exists for what the match promises, that is the one
        /// suggested. A six-man that promises spectacle should be pointed at the lucha
        /// preset, not at a singles spotfest booked with six people in it.
        /// </summary>
        [Theory]
        [InlineData(2, MatchStory.FaceInPeril, "Southern Tag")]
        [InlineData(3, MatchStory.FaceInPeril, "Six-Man War")]
        [InlineData(3, MatchStory.Spectacle,   "Lucha Trios")]
        [InlineData(1, MatchStory.FaceInPeril, "Face-in-Peril")]
        [InlineData(1, MatchStory.Spectacle,   "Spotfest")]
        public void TheTagSuggestionsComeFirstWhereThereAreBodiesForThem(
            int perSide, MatchStory wants, string expected)
        {
            Assert.Equal(expected, BriefPresets.SuggestedFor(new Expectation(wants, 0.8, ""), perSide));
        }

        /// <summary>
        /// Every tag preset produces a legal, runnable match on the shape it is offered for.
        /// These are the fast path, so one of them being broken is a broken button.
        /// </summary>
        [Fact]
        public void EveryTagPresetProducesAMatchWorthBooking()
        {
            foreach (var preset in BriefPresets.All.Where(p => p.MinimumPerSide > 1))
            {
                var sides = Teams(preset.MinimumPerSide);
                var plan  = new MatchPlan
                {
                    Sides = sides,
                    Beats = BriefDirector.Write(preset.Brief.Clone(), sides).Beats.ToList()
                };

                var errors = plan.Validate().ToList();
                output.WriteLine($"  {preset.Name,-14} {plan.Beats.Count} beats, {errors.Count} problems");
                Assert.Empty(errors);
            }
        }

        // ── It has to run ────────────────────────────────────────────────────

        /// <summary>
        /// Every tag and trios brief produces a plan the engine accepts. Seven stories by
        /// five lengths by four side sizes, both two-sided and three-sided.
        /// </summary>
        [Fact]
        public void EveryTagBriefProducesALegalPlan()
        {
            int checkedPlans = 0;

            foreach (int perSide in new[] { 2, 3, 4 })
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            foreach (FinishKind finish in Enum.GetValues<FinishKind>())
            {
                var sides = Teams(perSide);
                var brief = new MatchBrief { Story = story, Length = scale, Finish = finish };

                var plan = new MatchPlan
                {
                    Sides = sides,
                    Beats = BriefDirector.Write(brief, sides).Beats.ToList()
                };

                var errors = plan.Validate();
                if (errors.Count > 0)
                    output.WriteLine($"  {perSide} a side {story}/{scale}/{finish}: {string.Join("; ", errors)}");

                Assert.Empty(errors);
                checkedPlans++;
            }

            output.WriteLine($"  {checkedPlans} tag briefs, every one of them a legal plan");
        }

        /// <summary>
        /// And they run through the engine, which is a different question from being valid:
        /// a tag beat can pass the validator and still find no partner to tag.
        /// </summary>
        [Fact]
        public void EveryTagBriefRunsThroughTheEngine()
        {
            foreach (int perSide in new[] { 2, 3 })
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            {
                var sides = Teams(perSide);
                var brief = new MatchBrief { Story = story, Length = MatchScale.Workhorse };

                var plan = new MatchPlan
                {
                    Sides = sides,
                    Beats = BriefDirector.Write(brief, sides).Beats.ToList(),
                    Brief = brief
                };

                var result = new MatchEngine(StableSeed.From("tag", story, perSide)).Execute(plan);

                Assert.True(result.FinalScore > 0);
                Assert.Contains(result.Winner, sides[0].Members);
            }
        }

        /// <summary>
        /// The hot tag reaches the play-by-play. Doc 18 has it as the loudest moment in the
        /// format, and a beat that scores without ever being called is a number moving on
        /// its own.
        /// </summary>
        [Fact]
        public void TheHotTagIsCalled()
        {
            var sides = Teams(2);
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse };

            var plan = new MatchPlan
            {
                Sides = sides,
                Beats = BriefDirector.Write(brief, sides).Beats.ToList()
            };

            var result = new MatchEngine(StableSeed.From("hottag")).Execute(plan);
            var lines = result.BeatResults.SelectMany(b => b.Commentary).ToList();

            foreach (var line in lines) output.WriteLine("  " + line);

            Assert.Contains(lines, l => l.Contains("tag", StringComparison.OrdinalIgnoreCase));
        }

        // ── Length still means what it meant ─────────────────────────────────

        /// <summary>
        /// A tag match gets longer the same way a singles match does — by gaining cycles —
        /// and every length is longer than the one below it. The old library had three tag
        /// structures and a booker who wanted a twenty-five-minute tag had nothing to pick.
        /// </summary>
        [Fact]
        public void ATagMatchGrowsTheSameWayASinglesMatchDoes()
        {
            int previous = 0;

            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            {
                var written = BriefDirector.Write(
                    new MatchBrief { Story = MatchStory.FaceInPeril, Length = scale }, Teams(2));

                output.WriteLine($"  {scale,-12} {written.Beats.Count,2} beats {written.Minutes,3} min");

                Assert.True(written.Minutes > previous, $"{scale} is not longer than the one below it");
                previous = written.Minutes;
            }
        }
    }
}
