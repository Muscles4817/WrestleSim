using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **The rule that produced the structure library, rather than the library.**
    ///
    /// A <see cref="MatchStructure"/> was a frozen list of beats. Booking `Face-in-Peril`
    /// twice gave the identical eleven beats twice, and a beat nobody hand-wrote into a
    /// structure was a beat no booker ever saw — which is how `Hope Spot` shipped reachable
    /// from three singles structures and none of the multi-man ones.
    ///
    /// The claim this file exists to check is that <see cref="PhaseGrammar"/> is the *same
    /// rule*, not a new one that happens to look similar. If the shipped structures do not
    /// come back out of it, the grammar is wrong and everything built on top inherits that.
    /// </summary>
    public class PhaseGrammarTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name, WrestlingStyle style)
        {
            var w = TestRoster.Make(name);
            w.Style = style;
            return w;
        }

        private static List<MatchSide> Two(WrestlingStyle a = WrestlingStyle.Technical,
                                           WrestlingStyle b = WrestlingStyle.Powerhouse) =>
        [
            new MatchSide { Members = [W("Alpha", a)] },
            new MatchSide { Members = [W("Bravo", b)] }
        ];

        // ── The regeneration proof ───────────────────────────────────────────

        /// <summary>
        /// **The shape of every shipped singles structure comes back out of a brief, on
        /// every draft.**
        ///
        /// Controller, duration and total running time, beat by beat, whatever the generator
        /// rolls, plus intensity from the second beat on. This is the deterministic half:
        /// what the booker asked for decides the skeleton, and re-rolling cannot move it.
        ///
        /// The opening's *intensity* is the one thing excluded, and deliberately. It comes
        /// from the beat rather than the grammar, because a feeling-out process is low by
        /// virtue of being one and having the grammar restate that is a second place for it
        /// to be wrong. So a face-in-peril television match opens either with a
        /// collar-and-elbow or with a hot start, which is a real variation a booker would
        /// recognise rather than a gap in the rule.
        ///
        /// It found three real faults while it was being written, all of which would have
        /// shipped. The grammar restating an opening's intensity when the beat template
        /// already knew it. A technical match not getting the extra time its mat work takes.
        /// And beat choice letting a wrestler's style overrule the booker's story.
        /// </summary>
        [Theory]
        [InlineData("TV Formula",         MatchStory.FaceInPeril,         MatchScale.Television, FinishKind.Clean)]
        [InlineData("Face-in-Peril",      MatchStory.FaceInPeril,         MatchScale.Workhorse,  FinishKind.Clean)]
        [InlineData("Technical Showcase", MatchStory.TechnicalExhibition, MatchScale.Workhorse,  FinishKind.Submission)]
        [InlineData("Big Match",          MatchStory.FaceInPeril,         MatchScale.BigMatch,   FinishKind.Dominant)]
        public void TheShapeOfEveryShippedStructureComesBackOutOfABrief(
            string structure, MatchStory story, MatchScale scale, FinishKind finish)
        {
            var want = MatchStructureLibrary.Find(structure)!;

            for (int draft = 0; draft < 25; draft++)
            {
                var brief = new MatchBrief
                {
                    Story = story, Length = scale, Finish = finish, WinningSide = 0, Draft = draft
                };
                var got = BriefDirector.Write(brief, Two());

                Assert.Equal(want.Beats.Count, got.Beats.Count);
                Assert.Equal(want.Beats.Sum(b => b.DurationMinutes), got.Minutes);

                for (int i = 0; i < want.Beats.Count; i++)
                {
                    Assert.Equal(want.Beats[i].Control,  got.Beats[i].Control);
                    Assert.Equal(want.Beats[i].Duration, got.Beats[i].Duration);

                    // See the note above: the opening's intensity is the opening beat's.
                    if (i > 0) Assert.Equal(want.Beats[i].Intensity, got.Beats[i].Intensity);
                }
            }

            output.WriteLine($"  {structure}: {want.Beats.Count} beats / " +
                             $"{want.Beats.Sum(b => b.DurationMinutes)} min, held across 25 drafts");
        }

        /// <summary>
        /// **And the exact beat list is somewhere in the space the generator produces.**
        ///
        /// The shape is fixed and the fill is not, so a given draft is not obliged to
        /// reproduce a hand-written structure exactly — an opening that can legitimately be
        /// a collar-and-elbow or a hot start will be one of them. What would be damning is
        /// if the shipped structure were *unreachable*, because then the grammar is not the
        /// rule those lists were written from.
        ///
        /// The first version of this test asserted exactness on draft zero and passed, which
        /// was luck rather than a property. It broke the moment the test cast changed.
        /// </summary>
        [Theory]
        [InlineData("TV Formula",         MatchStory.FaceInPeril,         MatchScale.Television, FinishKind.Clean)]
        [InlineData("Face-in-Peril",      MatchStory.FaceInPeril,         MatchScale.Workhorse,  FinishKind.Clean)]
        [InlineData("Technical Showcase", MatchStory.TechnicalExhibition, MatchScale.Workhorse,  FinishKind.Submission)]
        [InlineData("Big Match",          MatchStory.FaceInPeril,         MatchScale.BigMatch,   FinishKind.Dominant)]
        public void TheExactShippedBeatListIsReachable(
            string structure, MatchStory story, MatchScale scale, FinishKind finish)
        {
            var want = MatchStructureLibrary.Find(structure)!;

            for (int draft = 0; draft < 400; draft++)
            {
                var brief = new MatchBrief
                {
                    Story = story, Length = scale, Finish = finish, WinningSide = 0, Draft = draft
                };
                var got = BriefDirector.Write(brief, Two());

                if (!want.Beats.Select(b => b.Type).SequenceEqual(got.Beats.Select(b => b.Type)))
                    continue;

                output.WriteLine($"  {structure} reproduced exactly on draft {draft}");
                foreach (var phase in got.Phases)
                    output.WriteLine($"    {phase.Phase,-12} {phase.Name}");
                return;
            }

            Assert.Fail($"{structure} is not reachable from its brief in 400 drafts");
        }

        /// <summary>
        /// **The Epic disagrees by one beat, and the grammar is right.**
        ///
        /// The hand-written structure drops its third hope spot back to Medium after the
        /// second was High. Doc 18 §3.3's pyramid rule says everything escalates and the
        /// biggest thing is the last thing, so a later tease cannot be smaller than an
        /// earlier one — the crowd has already seen the bigger version and stopped believing
        /// the smaller.
        ///
        /// Pinned rather than glossed over, because "close enough" on a regeneration test is
        /// how a grammar quietly stops being the rule it claims to be. If this ever starts
        /// matching exactly, somebody has changed one of the two and should say which.
        /// </summary>
        [Fact]
        public void TheEpicDisagreesOnOneHopeSpotAndTheRuleWins()
        {
            var want = MatchStructureLibrary.Find("Epic")!;
            var brief = new MatchBrief
            {
                Story = MatchStory.FaceInPeril, Length = MatchScale.Epic, Finish = FinishKind.Dominant
            };

            var got = BriefDirector.Write(brief, Two());

            Assert.Equal(want.Beats.Count, got.Beats.Count);
            Assert.Equal(want.Beats.Sum(b => b.DurationMinutes), got.Minutes);

            var differ = Enumerable.Range(0, want.Beats.Count)
                .Where(i => want.Beats[i].Type      != got.Beats[i].Type
                         || want.Beats[i].Control   != got.Beats[i].Control
                         || want.Beats[i].Intensity != got.Beats[i].Intensity
                         || want.Beats[i].Duration  != got.Beats[i].Duration)
                .ToList();

            foreach (int i in differ)
                output.WriteLine($"  beat {i + 1}: library {want.Beats[i].Type} " +
                                 $"{want.Beats[i].Intensity} vs generated {got.Beats[i].Intensity}");

            int only = Assert.Single(differ);
            Assert.Equal(BeatType.HopeSpot, want.Beats[only].Type);
            Assert.Equal(BeatIntensity.Medium, want.Beats[only].Intensity);
            Assert.Equal(BeatIntensity.High,   got.Beats[only].Intensity);
        }

        /// <summary>
        /// Hope spots escalate across the whole match, which is the rule the Epic breaks.
        /// Doc 18 §3.3: the pyramid is about the match, not about the section.
        /// </summary>
        [Fact]
        public void HopeSpotsNeverGetSmaller()
        {
            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            {
                var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = scale };
                var hopes = PhaseGrammar.Shape(brief)
                    .Where(s => s.Phase == MatchPhase.HopeSpot)
                    .Select(s => s.Intensity)
                    .ToList();

                output.WriteLine($"  {scale,-12} {string.Join(" → ", hopes)}");

                for (int i = 1; i < hopes.Count; i++)
                    Assert.True(hopes[i] >= hopes[i - 1],
                                $"{scale}: hope spot {i + 1} is smaller than the one before it");
            }
        }

        // ── The shape follows the brief ──────────────────────────────────────

        /// <summary>
        /// **Length is a cycle count, not a slider.**
        ///
        /// Doc 18 §3.1's table is a table of contents rather than of minutes: "15–25 min
        /// adds a second heat/comeback cycle and a real finishing stretch." So a longer
        /// match is not the same match with bigger numbers, it has more of the thing that
        /// makes a match, and the minutes follow from that rather than the other way round.
        /// </summary>
        [Fact]
        public void LongerMeansMoreCyclesRatherThanLongerBeats()
        {
            int previousBeats = 0, previousMinutes = 0;

            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            {
                var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = scale };
                var shape = PhaseGrammar.Shape(brief);
                var got   = BriefDirector.Write(brief, Two());

                int cycles = shape.Count(s => s.Phase == MatchPhase.Heat);

                output.WriteLine($"  {scale,-12} {got.Beats.Count,2} beats  {got.Minutes,3} min  " +
                                 $"{cycles} cycle(s)  {PhaseGrammar.NearFalls(scale)} near falls");

                Assert.Equal(PhaseGrammar.Cycles(scale), cycles);
                Assert.True(got.Beats.Count > previousBeats, $"{scale} is not longer than the one below it");
                Assert.True(got.Minutes    > previousMinutes);

                previousBeats   = got.Beats.Count;
                previousMinutes = got.Minutes;
            }
        }

        /// <summary>
        /// An even contest is the only shape where control genuinely changes hands, and that
        /// is the whole of what makes it even. Everywhere else the antagonist keeps the heat.
        /// </summary>
        [Fact]
        public void OnlyAnEvenContestGivesBothOfThemAHeatSection()
        {
            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            {
                var brief = new MatchBrief { Story = story, Length = MatchScale.BigMatch };
                var heats = PhaseGrammar.Shape(brief)
                    .Where(s => s.Phase == MatchPhase.Heat)
                    .Select(s => s.Control)
                    .ToList();

                bool shared = heats.Distinct().Count() > 1;
                output.WriteLine($"  {story,-20} heat worked by {string.Join(", ", heats)}");

                Assert.Equal(story == MatchStory.EvenContest, shared);
            }
        }

        /// <summary>
        /// The two shapes that are *about* not getting going do not get a shine, and a match
        /// too short to afford one does not get it either.
        /// </summary>
        [Fact]
        public void TheShapesAboutNotGettingGoingHaveNoShine()
        {
            Assert.False(PhaseGrammar.HasShine(MatchStory.DavidAndGoliath, MatchScale.Epic));
            Assert.False(PhaseGrammar.HasShine(MatchStory.Showcase,        MatchScale.Epic));
            Assert.False(PhaseGrammar.HasShine(MatchStory.FaceInPeril,     MatchScale.Opener));
            Assert.True(PhaseGrammar.HasShine(MatchStory.FaceInPeril,      MatchScale.Television));

            var goliath = PhaseGrammar.Shape(
                new MatchBrief { Story = MatchStory.DavidAndGoliath, Length = MatchScale.BigMatch });

            output.WriteLine("  " + string.Join(", ", goliath.Select(s => s.Phase)));
            Assert.DoesNotContain(goliath, s => s.Phase == MatchPhase.Shine);
        }

        // ── Booking, which is not the same as winning ────────────────────────

        /// <summary>
        /// **A challenger booked to be elevated is who the match is about, even losing.**
        ///
        /// Doc 20 §4 calls this the most useful thing a booker can do with a title match
        /// they do not intend to change: the challenger takes the champion to the limit and
        /// comes out bigger than they went in. It only works if the *shape* follows them —
        /// they shine, they take the hope spots, they make the comeback — and then lose
        /// anyway.
        ///
        /// This is the thing a structure list could not express at all. Every singles
        /// structure in the library had WrestlerA shine, come back and win.
        /// </summary>
        [Fact]
        public void TheValiantLoserGetsTheShapeAndTheWinnerGetsTheFall()
        {
            var brief = new MatchBrief
            {
                Story       = MatchStory.FaceInPeril,
                Length      = MatchScale.BigMatch,
                WinningSide = 0,
                Bookings    = { [1] = Booking.Elevated }
            };

            var shape = PhaseGrammar.Shape(brief);

            var shine    = shape.Single(s => s.Phase == MatchPhase.Shine);
            var comeback = shape.Last(s => s.Phase == MatchPhase.Comeback);
            var finish   = shape.Single(s => s.Phase == MatchPhase.Finish);

            output.WriteLine($"  shine    {shine.Control}  ({shine.Why})");
            output.WriteLine($"  comeback {comeback.Control}");
            output.WriteLine($"  finish   {finish.Control}");

            // The loser is the protagonist: they shine and they come back.
            Assert.Equal(BeatControl.WrestlerB, shine.Control);
            Assert.Equal(BeatControl.WrestlerB, comeback.Control);

            // And they still lose.
            Assert.Equal(BeatControl.WrestlerA, finish.Control);
        }

        /// <summary>
        /// With nobody singled out the match is about whoever wins it, which is the ordinary
        /// case and has to stay the default.
        /// </summary>
        [Fact]
        public void OtherwiseTheMatchIsAboutWhoeverWinsIt()
        {
            foreach (int winner in new[] { 0, 1 })
            {
                var brief = new MatchBrief { Story = MatchStory.FaceInPeril, WinningSide = winner };
                Assert.Equal(winner, brief.Protagonist(2));

                var shape = PhaseGrammar.Shape(brief);
                var expected = winner == 0 ? BeatControl.WrestlerA : BeatControl.WrestlerB;

                Assert.Equal(expected, shape.Single(s => s.Phase == MatchPhase.Shine).Control);
                Assert.Equal(expected, shape.Single(s => s.Phase == MatchPhase.Finish).Control);
            }
        }

        /// <summary>
        /// Elevating the winner does not make the loser the protagonist. Only an asymmetry
        /// does, and this is the case that would flip it if the rule were "anybody elevated".
        /// </summary>
        [Fact]
        public void ElevatingBothOfThemChangesNothing()
        {
            var brief = new MatchBrief
            {
                WinningSide = 0,
                Bookings    = { [0] = Booking.Elevated, [1] = Booking.Elevated }
            };

            Assert.Equal(0, brief.Protagonist(2));
        }

        // ── The finish is the booker's, not the generator's ──────────────────

        /// <summary>
        /// Every finish the brief can ask for is the finish that comes out. A generator that
        /// quietly substitutes one is deciding the result of the match.
        /// </summary>
        [Fact]
        public void TheFinishIsAlwaysTheOneThatWasAskedFor()
        {
            foreach (FinishKind kind in Enum.GetValues<FinishKind>())
            {
                var brief = new MatchBrief { Finish = kind, Length = MatchScale.Workhorse };
                var got   = BriefDirector.Write(brief, Two());
                var last  = got.Beats[^1];

                output.WriteLine($"  {kind,-18} → {last.Type}");

                Assert.True(last.IsFinish);
                Assert.Equal(BriefDirector.FinishType(kind), last.Type);
            }
        }

        /// <summary>Whoever the brief says goes over is the one who takes the fall.</summary>
        [Fact]
        public void TheWinnerControlsTheFinish()
        {
            foreach (int winner in new[] { 0, 1 })
            {
                var brief = new MatchBrief { WinningSide = winner, Length = MatchScale.Workhorse };
                var got   = BriefDirector.Write(brief, Two());

                Assert.Equal(winner == 0 ? BeatControl.WrestlerA : BeatControl.WrestlerB,
                             got.Beats[^1].Control);
            }
        }

        // ── Variety ──────────────────────────────────────────────────────────

        /// <summary>
        /// **The same brief twice is the same match and a different beat sheet.**
        ///
        /// The shape is deterministic because it is what the booker asked for. What fills it
        /// is not, and that is the difference between this and the structure library: the
        /// old one gave the identical eleven beats however many times you booked it.
        /// </summary>
        [Fact]
        public void TheSameBriefGivesTheSameShapeAndDifferentBeats()
        {
            var sides = Two();
            var sheets = new List<string>();

            for (int draft = 0; draft < 8; draft++)
            {
                var brief = new MatchBrief
                {
                    Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse, Draft = draft
                };
                var got = BriefDirector.Write(brief, sides);

                // The shape does not move.
                Assert.Equal(11, got.Beats.Count);
                Assert.Equal(15, got.Minutes);

                sheets.Add(string.Join(",", got.Phases.Select(p => p.Name)));
            }

            int distinct = sheets.Distinct().Count();
            output.WriteLine($"  eight drafts produced {distinct} distinct beat sheets");
            foreach (var sheet in sheets.Distinct()) output.WriteLine($"    {sheet}");

            Assert.True(distinct >= 5, "re-rolling has to actually re-roll");
        }

        /// <summary>
        /// And the same brief on the same cast is reproducible, which is what lets a saved
        /// plan regenerate. <c>StableSeed</c> rather than <c>HashCode.Combine</c> is the
        /// whole of why this holds across processes as well as within one.
        /// </summary>
        [Fact]
        public void TheSameBriefAndCastRegeneratesExactly()
        {
            var brief = new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.BigMatch };

            var first  = BriefDirector.Write(brief, Two());
            var second = BriefDirector.Write(brief.Clone(), Two());

            Assert.Equal(first.Phases.Select(p => p.Name), second.Phases.Select(p => p.Name));
        }

        /// <summary>
        /// **Who is in the match changes what is in the match.**
        ///
        /// Over a run of drafts rather than on one sheet. A single pair of sheets can
        /// coincide by chance — the first version of this test asserted two casts differed
        /// on draft zero and failed, correctly, because the style weighting at the time was
        /// a flat 0.85 for every mismatch and the two casts had identical weights on every
        /// candidate. A style that never changes an outcome is decoration, and that is what
        /// this catches.
        /// </summary>
        [Fact]
        public void WhoIsInItChangesWhatIsInIt()
        {
            var brief = new MatchBrief { Story = MatchStory.EvenContest, Length = MatchScale.Workhorse };

            var mat   = Spread(brief, WrestlingStyle.Technical,  WrestlingStyle.Technical);
            var force = Spread(brief, WrestlingStyle.Powerhouse, WrestlingStyle.Powerhouse);
            var air   = Spread(brief, WrestlingStyle.HighFlyer,  WrestlingStyle.HighFlyer);

            foreach (var (label, spread) in new[] { ("mat", mat), ("force", force), ("air", air) })
                output.WriteLine($"  {label,-6} {string.Join(", ", spread.OrderByDescending(x => x.Value)
                                                                        .Take(4)
                                                                        .Select(x => $"{x.Key} {x.Value}"))}");

            Assert.NotEqual(mat, force);
            Assert.NotEqual(mat, air);
            Assert.NotEqual(force, air);
        }

        /// <summary>Which beats a cast draws over fifty drafts, as a count per template.</summary>
        private static Dictionary<string, int> Spread(MatchBrief brief, WrestlingStyle a, WrestlingStyle b)
        {
            var counts = new Dictionary<string, int>();

            for (int draft = 0; draft < 50; draft++)
            {
                var rolled = brief.Clone();
                rolled.Draft = draft;

                foreach (var phase in BriefDirector.Write(rolled, Two(a, b)).Phases)
                    counts[phase.Name] = counts.GetValueOrDefault(phase.Name) + 1;
            }

            return counts;
        }

        /// <summary>
        /// **The story wins the beat, not the cast.**
        ///
        /// This one is a regression test for a real fault. The first weighting had a
        /// matching style hint worth ×2.5 and a matching story worth ×3.0 against a
        /// mismatched hint's ×0.4, so a technical exhibition worked by a powerhouse drew
        /// Power Beatdown for its heat more often than Technical Dissection. That is the
        /// generator overruling the booker, and the place to charge for a cast that cannot
        /// deliver the booking is the score, not a substituted beat nobody chose.
        /// </summary>
        [Fact]
        public void AskingForTechnicalGetsTechnicalEvenFromTheWrongBody()
        {
            var counts = new Dictionary<BeatType, int>();

            for (int draft = 0; draft < 200; draft++)
            {
                var brief = new MatchBrief
                {
                    Story = MatchStory.TechnicalExhibition, Length = MatchScale.Workhorse, Draft = draft
                };

                // The powerhouse works the heat, which is the awkward case.
                var got = BriefDirector.Write(brief, Two(WrestlingStyle.Technical, WrestlingStyle.Powerhouse));

                foreach (var phase in got.Phases.Where(p => p.Phase == MatchPhase.Heat))
                    counts[phase.Beat.Type] = counts.GetValueOrDefault(phase.Beat.Type) + 1;
            }

            foreach (var (type, n) in counts.OrderByDescending(x => x.Value))
                output.WriteLine($"  {type,-14} {n * 100.0 / counts.Values.Sum(),5:F1}%");

            // Everything in the heat pool is a HeatSegment here, so the interesting question
            // is which template — asserted through the story-fit weighting below.
            var technical = Share(MatchStory.TechnicalExhibition, WrestlingStyle.Powerhouse, "Technical Dissection");
            var beatdown  = Share(MatchStory.TechnicalExhibition, WrestlingStyle.Powerhouse, "Power Beatdown");

            output.WriteLine($"  Technical Dissection {technical:P0} vs Power Beatdown {beatdown:P0}");
            Assert.True(technical > beatdown,
                        "asking for a technical match has to beat the cast's own preference");
        }

        /// <summary>How often one named template fills the heat, over enough drafts to mean it.</summary>
        private static double Share(MatchStory story, WrestlingStyle heater, string template)
        {
            int hits = 0, total = 0;

            for (int draft = 0; draft < 300; draft++)
            {
                var brief = new MatchBrief { Story = story, Length = MatchScale.Workhorse, Draft = draft };
                var got = BriefDirector.Write(brief, Two(WrestlingStyle.Technical, heater));

                foreach (var phase in got.Phases.Where(p => p.Phase == MatchPhase.Heat))
                {
                    total++;
                    if (phase.Name == template) hits++;
                }
            }

            return total == 0 ? 0 : hits / (double)total;
        }

        /// <summary>
        /// **The wrong body is rarer than a merely different one.**
        ///
        /// A power beatdown belongs to a powerhouse. A brawler is close enough to it that
        /// he gets one often; a high flyer is not, and should almost never be handed it.
        /// A flat penalty for every mismatch makes those last two the same wrestler as far
        /// as the generator is concerned, which is the thing this checks.
        ///
        /// Written because a mutation flattening style affinity to a single number survived
        /// everything else here. The cast tests above only prove that *some* difference
        /// exists between casts, which a flat penalty still produces through the matching
        /// case alone. This one is about the shape of the penalty.
        /// </summary>
        [Fact]
        public void APowerBeatIsRarerOnAFlyerThanOnABrawler()
        {
            double own     = Share(MatchStory.EvenContest, WrestlingStyle.Powerhouse, "Power Beatdown");
            double kindred = Share(MatchStory.EvenContest, WrestlingStyle.Brawler,    "Power Beatdown");
            double alien   = Share(MatchStory.EvenContest, WrestlingStyle.HighFlyer,  "Power Beatdown");

            output.WriteLine($"  Power Beatdown worked by a powerhouse {own:P0}, " +
                             $"a brawler {kindred:P0}, a high flyer {alien:P0}");

            Assert.True(own > kindred,   "a powerhouse should get it more than anybody");
            Assert.True(kindred > alien, "a brawler is closer to it than a high flyer is");

            // The ordering is the claim, and it is what a flat penalty cannot produce: with
            // one number for every mismatch a brawler and a high flyer swap places, because
            // the only thing left separating them is which *other* beat happens to match.
            Assert.True(own - alien > 0.06, "the penalty has to be visible, not a rounding difference");
        }

        /// <summary>
        /// A grudge gets brawling and a technical exhibition does not. The pools overlap —
        /// both fill their heat from the same category — so this is the weighting doing the
        /// work rather than the phase table.
        /// </summary>
        [Fact]
        public void AGrudgeBrawlsAndATechnicalMatchDoesNot()
        {
            double grudgeBrawl = Share(MatchStory.Grudge, WrestlingStyle.Brawler, "Explosive Flurry");
            double techBrawl   = Share(MatchStory.TechnicalExhibition, WrestlingStyle.Brawler, "Explosive Flurry");

            output.WriteLine($"  Explosive Flurry in a grudge {grudgeBrawl:P0}, in a technical match {techBrawl:P0}");

            Assert.True(grudgeBrawl > 0.25, "a grudge should brawl");
            Assert.True(techBrawl < 0.10,   "a technical exhibition should not");
        }

        // ── Every brief has to produce a legal plan ──────────────────────────

        /// <summary>
        /// **Every combination the UI can offer produces a plan the engine accepts.**
        ///
        /// A generator that can emit an invalid plan is worse than one that emits a dull
        /// one: the booker gets an error on a sheet they did not write and cannot fix. Seven
        /// stories by five lengths by seven finishes is the whole input space, so it is
        /// cheaper to check all of it than to argue about which corners matter.
        /// </summary>
        [Fact]
        public void EveryBriefTheUiCanOfferProducesALegalPlan()
        {
            int checkedPlans = 0;

            foreach (MatchStory story in Enum.GetValues<MatchStory>())
            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            foreach (FinishKind finish in Enum.GetValues<FinishKind>())
            {
                var sides = Two();
                var brief = new MatchBrief { Story = story, Length = scale, Finish = finish };
                var got   = BriefDirector.Write(brief, sides);

                var plan = new MatchPlan
                {
                    Sides = sides,
                    Beats = got.Beats.ToList()
                };

                var errors = plan.Validate();
                if (errors.Count > 0)
                    output.WriteLine($"  {story}/{scale}/{finish}: {string.Join("; ", errors)}");

                Assert.Empty(errors);
                checkedPlans++;
            }

            output.WriteLine($"  {checkedPlans} briefs, every one of them a legal plan");
        }

        /// <summary>
        /// Interference adds a beat and does not replace one. A manager at ringside who
        /// never appears is a loaded gun that never goes off.
        /// </summary>
        [Fact]
        public void OutsideInterferencePutsSomebodyInTheMatch()
        {
            var plain = new MatchBrief { Length = MatchScale.Workhorse };
            var loaded = new MatchBrief { Length = MatchScale.Workhorse, Outside = OutsideFactor.Manager };

            var without = BriefDirector.Write(plain, Two());
            var with    = BriefDirector.Write(loaded, Two());

            output.WriteLine($"  {without.Beats.Count} beats without, {with.Beats.Count} with");

            Assert.Equal(without.Beats.Count + 1, with.Beats.Count);
            Assert.Contains(with.Beats, b => b.Type == BeatType.ThirdPartyPullIn);
            Assert.DoesNotContain(without.Beats, b => b.Type == BeatType.ThirdPartyPullIn);

            // And it lands before the finish, because the finish has to be the last thing.
            Assert.True(with.Beats[^1].IsFinish);
        }

        /// <summary>
        /// Every phase carries a reason. The review screen's whole purpose is telling a
        /// booker why the sheet looks like this, and a blank one is a phase that cannot
        /// account for itself.
        /// </summary>
        [Fact]
        public void EveryPhaseCanSayWhyItIsThere()
        {
            var brief = new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.Epic };
            var got   = BriefDirector.Write(brief, Two());

            foreach (var phase in got.Phases)
            {
                output.WriteLine($"  {phase.Phase,-12} {phase.Name,-26} {phase.Why}");
                Assert.False(string.IsNullOrWhiteSpace(phase.Why));
            }
        }
    }
}
