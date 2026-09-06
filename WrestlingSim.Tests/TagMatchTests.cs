using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Phase 2 of the tag-match build (docs/tag-matches-plan.md).
    ///
    /// The claim under test is the one that decides whether this was worth building: a tag
    /// match is not a singles match with four names on it. The hot tag has to be *earned*
    /// by the isolation that came before it, the same way a finish has to be earned by the
    /// momentum that came before it — and booking it cold has to be visibly worse.
    /// </summary>
    public class TagMatchTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string name, double overness = 75, double charisma = 3.5,
                                  double skill = 3.5) =>
            TestRoster.Make(name, overness: overness, charisma: charisma, skill: skill);

        private static MatchSide Team(string a, string b) => MatchSide.Of(W(a), W(b));

        private static MatchBeat B(BeatType type, BeatControl control,
                                   BeatIntensity intensity = BeatIntensity.Medium) =>
            new() { Type = type, Control = control, Intensity = intensity };

        /// <summary>Mean over many seeds, so one lucky roll cannot carry a test.</summary>
        private static (double stars, double peak, double story) Mean(
            Func<MatchPlanModel> build, int runs = 120)
        {
            double s = 0, p = 0, y = 0;
            for (int i = 0; i < runs; i++)
            {
                var r = new MatchEngine(i * 7919).Execute(build());
                s += r.StarRating; p += r.CrowdPeakEnergy; y += r.StorytellingScore;
            }
            return (s / runs, p / runs, y / runs);
        }

        // ── The hot tag has to be bought ─────────────────────────────────────

        [Fact]
        public void AHotTagBookedCold_LandsFarBelowOneThatWasEarned()
        {
            // The whole thesis of phase 2. Same beats, same count, same people, same
            // seeds — the only difference is whether anybody was ever in trouble.
            MatchPlanModel Earned() => new()
            {
                SideA = Team("Face One", "Face Two"),
                SideB = Team("Heel One", "Heel Two"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,          BeatControl.WrestlerB),
                    B(BeatType.Isolation,       BeatControl.WrestlerB),
                    B(BeatType.Isolation,       BeatControl.WrestlerB),
                    B(BeatType.NearTag,         BeatControl.WrestlerB),
                    B(BeatType.HotTag,          BeatControl.WrestlerA),
                    B(BeatType.FinishClean,     BeatControl.WrestlerA),
                ]
            };

            // Identical length and identical beat *types* other than the peril: the
            // isolations become shines for the same side that ends up winning, so nobody
            // is ever cut off and the tag releases nothing.
            MatchPlanModel Cold() => new()
            {
                SideA = Team("Face One", "Face Two"),
                SideB = Team("Heel One", "Heel Two"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,          BeatControl.WrestlerB),
                    B(BeatType.Shine,           BeatControl.WrestlerA),
                    B(BeatType.Shine,           BeatControl.WrestlerA),
                    B(BeatType.DoubleTeam,      BeatControl.WrestlerA),
                    B(BeatType.HotTag,          BeatControl.WrestlerA),
                    B(BeatType.FinishClean,     BeatControl.WrestlerA),
                ]
            };

            var earned = Mean(Earned);
            var cold   = Mean(Cold);

            output.WriteLine($"  earned {earned.stars:F3}★  peak {earned.peak:F1}  story {earned.story:F1}");
            output.WriteLine($"  cold   {cold.stars:F3}★  peak {cold.peak:F1}  story {cold.story:F1}");

            Assert.True(earned.stars > cold.stars + 0.20,
                $"An earned hot tag should clearly beat a cold one — got {earned.stars:F3} vs {cold.stars:F3}.");
        }

        /// <summary>
        /// One charged tag match, parameterised by how much was spent buying the payoff.
        /// Durations match the library templates, so the fade cost of a longer heat is
        /// real rather than an artefact of the test.
        /// </summary>
        private (double pop, double stars, double story) Charged(int isolations, int nearTags, int runs = 150)
        {
            double pop = 0, stars = 0, story = 0;
            for (int i = 0; i < runs; i++)
            {
                var beats = new List<MatchBeat> { B(BeatType.StandardOpening, BeatControl.Even) };
                if (isolations > 0)
                    beats.Add(new MatchBeat { Type = BeatType.Cutoff, Control = BeatControl.WrestlerB, Duration = BeatDuration.Short });
                for (int k = 0; k < isolations; k++)
                    beats.Add(new MatchBeat { Type = BeatType.Isolation, Control = BeatControl.WrestlerB, Duration = BeatDuration.Medium });
                for (int k = 0; k < nearTags; k++)
                    beats.Add(new MatchBeat { Type = BeatType.NearTag, Control = BeatControl.WrestlerB, Intensity = BeatIntensity.High, Duration = BeatDuration.Brief });
                beats.Add(new MatchBeat { Type = BeatType.HotTag, Control = BeatControl.WrestlerA, Intensity = BeatIntensity.High, Duration = BeatDuration.Short });
                beats.Add(B(BeatType.FinishClean, BeatControl.WrestlerA));

                var r = new MatchEngine(i * 7919).Execute(new MatchPlanModel
                {
                    SideA = Team("Face One", "Face Two"),
                    SideB = Team("Heel One", "Heel Two"),
                    Beats = beats
                });
                pop   += r.BeatResults.First(b => b.BeatType == BeatType.HotTag).CrowdEnergyDelta;
                stars += r.StarRating;
                story += r.StorytellingScore;
            }
            return (pop / runs, stars / runs, story / runs);
        }

        [Fact]
        public void TheChargeCurve_IsMonotonic_InBothThePopAndTheRating()
        {
            // Measured on the hot tag's own crowd delta and on the final rating — not on
            // CrowdPeakEnergy, which saturates against the ceiling at around 78 whatever
            // you spend and so cannot see the charge at all.
            var none  = Charged(0, 0);
            var one   = Charged(1, 0);
            var two   = Charged(2, 0);
            var three = Charged(3, 0);
            var full  = Charged(3, 2);

            foreach (var (label, c) in new[]
                     { ("0 iso", none), ("1 iso", one), ("2 iso", two), ("3 iso", three), ("3 iso + 2 near", full) })
                output.WriteLine($"  {label,-16} pop {c.pop,6:F2}   story {c.story,6:F2}   {c.stars:F3}★");

            Assert.True(one.pop > none.pop * 1.8,
                "An isolation should roughly double what the tag is worth — that is the unearned penalty lifting.");
            Assert.True(two.pop > one.pop && three.pop > two.pop,
                "Each isolation up to three should buy more.");
            Assert.True(full.pop > three.pop,
                "Denied tags should add on top of the isolation.");

            Assert.True(one.stars > none.stars && three.stars > one.stars && full.stars > three.stars,
                "And the charge has to show up in the rating, not just the pop.");
        }

        [Fact]
        public void ADeniedTag_BuysMoreThanAnotherIsolationWould()
        {
            // If it did not, no booker would ever take one — it costs the room real energy
            // in the moment, so it has to pay back more than the beat it replaced.
            var isolationHeavy = Charged(3, 0);
            var deniedTag      = Charged(2, 1);

            output.WriteLine($"  3 isolations       pop {isolationHeavy.pop:F2}  {isolationHeavy.stars:F3}★");
            output.WriteLine($"  2 isolations + 1 denied tag  pop {deniedTag.pop:F2}  {deniedTag.stars:F3}★");

            Assert.True(deniedTag.pop > isolationHeavy.pop * 0.98,
                "Swapping the third isolation for a denied tag should not cost the payoff.");
            Assert.True(deniedTag.story > isolationHeavy.story,
                "And it should tell more story than another beatdown segment does.");
        }

        /// <summary>
        /// A plan of a fixed length, so extra isolations displace filler rather than
        /// lengthening the match. Without this any "more isolations" test measures fade.
        /// </summary>
        private (double stars, double pop, double crowd) AtFixedLength(string pattern, int totalBeats = 12)
        {
            double stars = 0, pop = 0, crowd = 0;
            const int runs = 150;

            for (int i = 0; i < runs; i++)
            {
                var beats = new List<MatchBeat> { B(BeatType.StandardOpening, BeatControl.Even) };
                beats.Add(new MatchBeat { Type = BeatType.Cutoff, Control = BeatControl.WrestlerB, Duration = BeatDuration.Short });

                foreach (char c in pattern)
                    beats.Add(c switch
                    {
                        'I' => new MatchBeat { Type = BeatType.Isolation, Control = BeatControl.WrestlerB, Duration = BeatDuration.Medium },
                        'N' => new MatchBeat { Type = BeatType.NearTag, Control = BeatControl.WrestlerB, Intensity = BeatIntensity.High, Duration = BeatDuration.Brief },
                        _   => new MatchBeat { Type = BeatType.RestHold, Control = BeatControl.WrestlerB, Duration = BeatDuration.Short }
                    });

                // Pad to a constant length so every variant is the same match length.
                while (beats.Count < totalBeats - 2)
                    beats.Add(new MatchBeat { Type = BeatType.RestHold, Control = BeatControl.WrestlerB, Duration = BeatDuration.Short });

                beats.Add(new MatchBeat { Type = BeatType.HotTag, Control = BeatControl.WrestlerA, Intensity = BeatIntensity.High, Duration = BeatDuration.Short });
                beats.Add(B(BeatType.FinishClean, BeatControl.WrestlerA));

                var r = new MatchEngine(i * 7919).Execute(new MatchPlanModel
                {
                    SideA = Team("Face One", "Face Two"),
                    SideB = Team("Heel One", "Heel Two"),
                    Beats = beats
                });
                stars += r.StarRating;
                pop   += r.BeatResults.First(b => b.BeatType == BeatType.HotTag).CrowdEnergyDelta;
                crowd += r.CrowdAverageEnergy;
            }
            return (stars / runs, pop / runs, crowd / runs);
        }

        [Fact]
        public void OverworkingTheHeat_IsPunished_AtConstantMatchLength()
        {
            // REWRITTEN after adjudication. The previous version asserted the hot-tag pop
            // and compared plans of different lengths, so it measured fade, not saturation
            // — and the rating in fact climbed monotonically to eight isolations, which the
            // build log had claimed was punished. It was not.
            //
            // The rule is not about the count. Doc 18 §2.3 says a long heat is *good* and
            // that the hope spots are what make it bearable, and §3.1 says a big match
            // adds a second heat cycle. So what costs the room is a RUN of isolations with
            // nothing to hold on to, and the near tag is what resets it.
            var three = AtFixedLength("III");
            var eight = AtFixedLength("IIIIIIII");

            output.WriteLine($"  3 isolations {three.stars:F3}★, 8 in a row {eight.stars:F3}★");

            // Before the fix this was 3.099 against 2.486 — five extra beats of beating
            // made the match *better*, and the optimum was "as many isolations as the beat
            // budget allows". It is now the other way round.
            //
            // The margin is deliberately modest rather than dramatic: TechnicalContribution
            // is untouched by the patience rule, because an overlong heat segment is badly
            // *paced*, not badly *wrestled*. Five more isolations really are five more
            // pieces of competent ring work; what they cost is the room, and the room is
            // roughly a third of the score. The ordering is the assertion.
            Assert.True(eight.stars < three.stars,
                $"Eight isolations in a row rated {eight.stars:F3} against {three.stars:F3} for three. " +
                "A crowd asked to wait that long with no hope spot should be lost, not more engaged.");

            // And on the axis the punishment actually lands on. Not the hot-tag pop —
            // that is identical by design, because the charge saturates at three and the
            // beat sits at the same position in a fixed-length plan. What an overworked
            // heat costs is the room it was worked in front of.
            output.WriteLine($"  crowd average {three.crowd:F2} → {eight.crowd:F2}");
            Assert.True(eight.crowd < three.crowd - 2.0,
                $"Eight isolations in a row left the crowd at {eight.crowd:F2} against " +
                $"{three.crowd:F2} for three. Before this rule existed, a longer beating made " +
                "the building louder.");
        }

        [Fact]
        public void ALongHeatPunctuatedByHopeSpots_IsNotPunished()
        {
            // The other half of the same rule, and the reason it is keyed on the run rather
            // than the count: two full heat cycles is what a big match looks like, and must
            // not be graded as padding.
            var punctuated = AtFixedLength("IIINIIIN");
            var runOfEight = AtFixedLength("IIIIIIII");
            var canonical  = AtFixedLength("ININ");

            output.WriteLine($"  I I I N I I I N {punctuated.stars:F3}★");
            output.WriteLine($"  I N I N          {canonical.stars:F3}★");
            output.WriteLine($"  I I I I I I I I {runOfEight.stars:F3}★");

            Assert.True(punctuated.stars > runOfEight.stars + 0.2,
                "A long heat broken up by denied tags must beat the same length of unbroken beating.");
        }

        [Fact]
        public void ADeniedTag_ResetsTheRoomsPatience()
        {
            // Directly: the same number of isolations, differing only in whether a hope
            // spot sits in the middle of them.
            var unbroken = AtFixedLength("IIII");
            var broken   = AtFixedLength("IINII");

            output.WriteLine($"  I I I I {unbroken.stars:F3}★   vs   I I N I I {broken.stars:F3}★");

            Assert.True(broken.stars > unbroken.stars,
                "A denied tag has to buy the room back, or a long heat can never be booked well.");
        }

        // ── Legality ─────────────────────────────────────────────────────────

        [Fact]
        public void ASideOfOne_CannotBeBookedATagBeat()
        {
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alone")),
                SideB = MatchSide.Of(W("Also Alone")),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.HotTag,          BeatControl.WrestlerA),
                    B(BeatType.FinishClean,     BeatControl.WrestlerA),
                ]
            };

            Assert.Contains(plan.Validate(), e => e.Contains("nobody on the apron"));
        }

        [Fact]
        public void UnevenSides_AreRejectedUntilTheEngineModelsNumbers()
        {
            // Not a rule about taste. The engine has no numbers term at all — a 1v2 grades
            // identically to a 1v1 — so a handicap match would be graded as something it
            // is not. The exit condition is the mechanic, not a policy change.
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alone")),
                SideB = Team("Two One", "Two Two"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.FinishClean,     BeatControl.WrestlerB),
                ]
            };

            Assert.Contains(plan.Validate(), e => e.Contains("Sides are uneven"));
        }

        [Fact]
        public void ATagBeatWithNoSide_IsRejected()
        {
            var plan = new MatchPlanModel
            {
                SideA = Team("A1", "A2"),
                SideB = Team("B1", "B2"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.HotTag,          BeatControl.Even),
                    B(BeatType.FinishClean,     BeatControl.WrestlerA),
                ]
            };

            Assert.Contains(plan.Validate(), e => e.Contains("booked for one side"));
        }

        [Fact]
        public void ATagThatBringsInTheManAlreadyInTheRing_IsRejected()
        {
            // Was a silent substitution: the engine fell through to "next man round" and
            // brought in somebody the booker had not asked for. Invisible on a two-man
            // side, a different match on a trio.
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("A1"), W("A2"), W("A3")),
                SideB = MatchSide.Of(W("B1"), W("B2"), W("B3")),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    new MatchBeat { Type = BeatType.Tag, Control = BeatControl.WrestlerA, IncomingIndex = 0 },
                    B(BeatType.FinishClean, BeatControl.WrestlerA),
                ]
            };

            Assert.Contains(plan.Validate(), e => e.Contains("already") && e.Contains("legal man"));
        }

        [Fact]
        public void ANearTagAgainstAManWithNoCorner_IsRejected()
        {
            // The rule checked the wrong side. Control on an isolation or a near tag is the
            // side *doing* the isolating; the man who needs a partner is their opponent.
            // With the uneven-sides block lifted this would have run, and the commentary
            // leaked its fallback string: "…the corner is beside himself on the apron!"
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alone"), W("Also Alone")),
                SideB = MatchSide.Of(W("B1"), W("B2")),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.NearTag, BeatControl.WrestlerB),
                    B(BeatType.FinishClean, BeatControl.WrestlerA),
                ]
            };

            // Both sides are teams here, so this one is legal — the guard is directional.
            Assert.Empty(plan.Validate());

            // And the directionality itself, proven on the beat legality rule rather than
            // on the uneven-sides block that currently masks it.
            var isolation = new MatchBeat { Type = BeatType.Isolation, Control = BeatControl.WrestlerB };
            var lopsided = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Alone")),
                SideB = MatchSide.Of(W("B1"), W("B2")),
                Beats = [B(BeatType.StandardOpening, BeatControl.Even), isolation,
                         B(BeatType.FinishClean, BeatControl.WrestlerB)]
            };

            // Side B controls the isolation, and side B is a team — but the man being
            // isolated is on side A, who has no corner. That is what must be caught.
            Assert.Contains(lopsided.Validate(),
                e => e.Contains("needs a partner on Alone's side"));
        }

        [Fact]
        public void ADeniedTagWithNoIsolation_StillBuysSomething()
        {
            // It used to buy exactly nothing: the unearned branch returned 0.55 before ever
            // reading nearTags, so a denied tag with no peril behind it was a pure cost —
            // the room lost energy and nothing gave it back. That contradicted the reason
            // the beat exists.
            var none    = Charged(0, 0);
            var twoNear = Charged(0, 2);

            output.WriteLine($"  no isolation, no near tags {none.pop:F2}");
            output.WriteLine($"  no isolation, two near tags {twoNear.pop:F2}");

            Assert.True(twoNear.pop > none.pop,
                "Denied tags have to be worth something even with no isolation behind them.");

            // But still well short of a properly built one.
            Assert.True(twoNear.pop < Charged(3, 2).pop * 0.8,
                "A near-tag-only heat is still a thin one.");
        }

        // ── Legal-performer tracking ─────────────────────────────────────────

        [Fact]
        public void AHotTag_BringsTheFreshPartnerIn_AndHeIsTheOneWhoScoresTheFall()
        {
            // The property that makes ControlSign's side lookup finally testable: after a
            // hot tag the person working for side A is Members[1], not Members[0].
            var a1 = W("Starts");
            var a2 = W("Comes In Fresh");

            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(a1, a2),
                SideB = Team("B1", "B2"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,          BeatControl.WrestlerB),
                    B(BeatType.Isolation,       BeatControl.WrestlerB),
                    B(BeatType.HotTag,          BeatControl.WrestlerA),
                    B(BeatType.FinishClean,     BeatControl.WrestlerA),
                ]
            };

            var result = new MatchEngine(Seed).Execute(plan);

            Assert.Same(a2, result.Pinner);
            Assert.Contains(a1, result.WinningSide);
        }

        [Fact]
        public void AfterAHotTag_ABeatBookedForThatSide_StillSwingsAdvantageToIt()
        {
            // This is the test phase 1 could not write. The fresh man is Members[1], so
            // the old `ReferenceEquals(control, Plan.WrestlerA)` sign test would now
            // return -1 for a side-A beat and push advantage the wrong way.
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Starts"), W("Fresh")),
                SideB = Team("B1", "B2"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,          BeatControl.WrestlerB),
                    B(BeatType.Isolation,       BeatControl.WrestlerB),
                    B(BeatType.HotTag,          BeatControl.WrestlerA),
                    B(BeatType.DoubleTeam,      BeatControl.WrestlerA, BeatIntensity.High),
                    B(BeatType.FinishClean,     BeatControl.WrestlerA),
                ]
            };

            var result = new MatchEngine(Seed).Execute(plan);
            var afterDoubleTeam = result.BeatResults[4];

            Assert.True(afterDoubleTeam.AdvantageDelta > 0,
                "A double team booked for side A, worked by the man who just tagged in, " +
                $"must swing advantage to side A — got {afterDoubleTeam.AdvantageDelta:F2}.");
        }

        [Fact]
        public void AQuickTag_ClearsTheChargeSoTheHotTagNoLongerPays()
        {
            // Tagging out mid-heat is a booking mistake and should read as one: the man
            // who comes back in is not the man the crowd has been waiting for.
            List<MatchBeat> WithTagBeforeHot(bool insertTag)
            {
                var beats = new List<MatchBeat>
                {
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,    BeatControl.WrestlerB),
                    B(BeatType.Isolation, BeatControl.WrestlerB),
                    B(BeatType.Isolation, BeatControl.WrestlerB),
                    B(BeatType.NearTag,   BeatControl.WrestlerB),
                };
                if (insertTag) beats.Add(B(BeatType.Tag, BeatControl.WrestlerA));
                beats.Add(B(BeatType.HotTag, BeatControl.WrestlerA));
                beats.Add(B(BeatType.FinishClean, BeatControl.WrestlerA));
                return beats;
            }

            double Pop(bool insertTag)
            {
                double total = 0;
                for (int i = 0; i < 100; i++)
                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = Team("Face One", "Face Two"),
                        SideB = Team("Heel One", "Heel Two"),
                        Beats = WithTagBeforeHot(insertTag)
                    }).BeatResults.First(b => b.BeatType == BeatType.HotTag).CrowdEnergyDelta;
                return total / 100;
            }

            double kept = Pop(false), spent = Pop(true);
            output.WriteLine($"  hot tag with charge intact {kept:F2}, after a routine tag {spent:F2}");

            Assert.True(kept > spent * 1.5,
                "A routine tag spends the charge, so the hot tag after it should be much smaller.");
        }

        // ── The near tag ─────────────────────────────────────────────────────

        [Fact]
        public void ANearTag_TakesEnergyOutOfTheRoom_AndGivesItBackLarger()
        {
            var plan = new MatchPlanModel
            {
                SideA = Team("Face One", "Face Two"),
                SideB = Team("Heel One", "Heel Two"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,    BeatControl.WrestlerB),
                    B(BeatType.Isolation, BeatControl.WrestlerB),
                    B(BeatType.NearTag,   BeatControl.WrestlerB),
                    B(BeatType.HotTag,    BeatControl.WrestlerA),
                    B(BeatType.FinishClean, BeatControl.WrestlerA),
                ]
            };

            var result = new MatchEngine(Seed).Execute(plan);
            var nearTag = result.BeatResults.First(b => b.BeatType == BeatType.NearTag);
            var hotTag  = result.BeatResults.First(b => b.BeatType == BeatType.HotTag);

            Assert.True(nearTag.CrowdEnergyDelta < 0,
                "The denied tag should quieten the building, not excite it.");
            Assert.True(hotTag.CrowdEnergyDelta > -nearTag.CrowdEnergyDelta,
                "And the payoff should return more than the near tag took.");
        }

        // ── Structures ───────────────────────────────────────────────────────

        [Fact]
        public void EveryTagStructure_IsBookableForATagMatchAndNotForASinglesOne()
        {
            foreach (var st in MatchStructureLibrary.ForSideSize(2))
            {
                var tag = new MatchPlanModel
                {
                    SideA = Team("A1", "A2"),
                    SideB = Team("B1", "B2"),
                    Beats = st.Beats.Select(b => b.Clone()).ToList()
                };
                Assert.Empty(tag.Validate());

                var result = new MatchEngine(Seed).Execute(tag);
                output.WriteLine($"  {st.Name,-16} {result.StarDisplay}");
                Assert.InRange(result.StarRating, 0.0, 5.0);

                var singles = new MatchPlanModel
                {
                    WrestlerA = W("Solo A"),
                    WrestlerB = W("Solo B"),
                    Beats = st.Beats.Select(b => b.Clone()).ToList()
                };
                Assert.NotEmpty(singles.Validate());
            }
        }

        [Fact]
        public void TheSouthernTag_OutRatesTheSprint_BecauseItPaysSomethingOff()
        {
            double Rate(string structure)
            {
                var st = MatchStructureLibrary.Find(structure)!;
                double total = 0;
                for (int i = 0; i < 120; i++)
                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = Team("Face One", "Face Two"),
                        SideB = Team("Heel One", "Heel Two"),
                        Beats = st.Beats.Select(b => b.Clone()).ToList()
                    }).StarRating;
                return total / 120;
            }

            double southern = Rate("Southern Tag"), sprint = Rate("Tag Sprint");
            output.WriteLine($"  Southern Tag {southern:F3}★   Tag Sprint {sprint:F3}★");

            Assert.True(southern > sprint,
                "The full formula should beat the opener that deliberately skips the peril.");
        }

        // ── End to end ───────────────────────────────────────────────────────

        [Fact]
        public void ATagMatchRunsOnAShowCard_AndReportsTheTeamsInItsNotes()
        {
            // Phase 3's actual claim: a tag match is not just constructible in code, it
            // goes on a card, runs through the show simulator, and reports sensibly.
            var roster = new List<Wrestler>
            {
                W("Ricky"), W("Robert"), W("Bobby"), W("Dennis")
            };

            var career = new WrestlingSim.Models.World.Career
            {
                Promotion   = new WrestlingSim.Models.World.Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
                StartDate   = new DateOnly(2025, 1, 6),
                CurrentDate = new DateOnly(2025, 1, 6),
                Roster      = roster
            };

            var show = career.Schedule("Saturday Night", career.CurrentDate, ShowType.HouseShow);
            var structure = MatchStructureLibrary.Find("Southern Tag")!;

            show.Card.Add(new WrestlingSim.Models.BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(roster[0], roster[1]),
                    SideB = MatchSide.Of(roster[2], roster[3]),
                    Beats = structure.Beats.Select(b => b.Clone()).ToList()
                },
                StructureName = structure.Name
            });

            var result = new ShowSimulator(career.FeudBook).Simulate(show.ToShow());

            Assert.Single(result.Items);
            Assert.True(result.OverallRating > 0);

            var item = result.Items[0];
            output.WriteLine($"  {item.Label}");
            foreach (var note in item.Notes) output.WriteLine($"    {note}");

            // The label carries its card position, so this checks the name inside it.
            Assert.Contains("Ricky & Robert vs Bobby & Dennis", item.Label);

            // The fall goes to whoever was legal at the finish. Southern Tag books a hot
            // tag, so that is the partner who started on the apron — not the man who took
            // the beating, and not "the team".
            Assert.Same(item.MatchResult!.WinningSide[1], item.MatchResult.Pinner);

            // The fall is credited to the man who was legal at the finish, not to the team.
            Assert.Contains(item.Notes, n => n.Contains("def."));
        }

        // ── Association ──────────────────────────────────────────────────────

        [Fact]
        public void AStarCarriesAWeakPartner_RatherThanBeingAveragedDownToHim()
        {
            // Ruled on in adjudication after phase 1. A flat side mean made a star-and-
            // jobber team grade as exactly the average of the star's match and the
            // jobber's match — the star losing precisely what the jobber gained. That is a
            // conservation law, and it contradicts doc 12 §3.2 and doc 17 §2.8, which say
            // association transfers heat *to* the weaker man.
            //
            // REWRITTEN TWICE after review. The first version compared a tag structure
            // against a singles structure; the second used a singles baseline. Both let a
            // structure difference dominate, so neither could see the aggregation at all —
            // the first passed even with the side read as its *worst* member.
            //
            // This varies nothing but who is on side A. Same plan, same opponents, same
            // seeds. Three compositions, and the mixed one has to land above the midpoint
            // of the other two: that is what "top-weighted" means and what a flat mean
            // would not produce.
            (double stars, double crowd) Rating(Wrestler one, Wrestler two)
            {
                double st = 0, cr = 0;
                for (int i = 0; i < 150; i++)
                {
                    var r = new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = MatchSide.Of(one, two),
                        SideB = Team("B1", "B2"),
                        Beats = MatchStructureLibrary.Find("Formula Tag")!.Beats
                                    .Select(b => b.Clone()).ToList()
                    });
                    st += r.StarRating; cr += r.CrowdAverageEnergy;
                }
                return (st / 150, cr / 150);
            }

            Wrestler Star()   => W("Star",   overness: 95, charisma: 5.0, skill: 4.6);
            Wrestler Jobber() => W("Jobber", overness: 20, charisma: 1.0, skill: 2.0);

            var twoStars   = Rating(Star(), Star());
            var twoJobbers = Rating(Jobber(), Jobber());

            // Both member orders, averaged. CORRECTED after adjudication: the first version
            // of this measured only `Star(), Jobber()`, which in Formula Tag books the
            // *jobber* to take the hot tag, work the double team and score the fall — the
            // single worst booking of that side. Holding that fixed made the aggregation
            // look like a conservation law when it is not. Averaging over who starts covers
            // the booking space instead of one bad corner of it.
            var mixedJobberIn = Rating(Star(), Jobber());
            var mixedStarIn   = Rating(Jobber(), Star());
            var mixed = ((mixedJobberIn.stars + mixedStarIn.stars) / 2.0,
                         (mixedJobberIn.crowd + mixedStarIn.crowd) / 2.0);

            double starMidpoint  = (twoStars.stars + twoJobbers.stars) / 2.0;
            double crowdMidpoint = (twoStars.crowd + twoJobbers.crowd) / 2.0;

            output.WriteLine($"  two stars      {twoStars.stars:F3}★  crowd {twoStars.crowd:F2}");
            output.WriteLine($"  mixed (jobber in) {mixedJobberIn.stars:F3}★");
            output.WriteLine($"  mixed (star in)   {mixedStarIn.stars:F3}★");
            output.WriteLine($"  mixed, averaged {mixed.Item1:F3}★  crowd {mixed.Item2:F2}");
            output.WriteLine($"  two jobbers    {twoJobbers.stars:F3}★  crowd {twoJobbers.crowd:F2}");
            output.WriteLine($"  midpoints: {starMidpoint:F3}★, crowd {crowdMidpoint:F2}");

            // The rating, over the booking space. This is the assertion the test was
            // missing: it FAILS at DragWeight = 1.0 (a flat mean lands exactly on the
            // midpoint, which is the conservation law Ruling B was made to remove) and
            // passes with real headroom at 0.5.
            Assert.True(mixed.Item1 > starMidpoint + 0.10,
                $"A star with a jobber averaged {mixed.Item1:F3} against a {starMidpoint:F3} " +
                "midpoint. A flat mean lands exactly on it — the side is being averaged, " +
                "not carried.");

            // And on the crowd axis, where the side read lives most directly.
            Assert.True(mixed.Item2 > crowdMidpoint,
                "The room should read the team as closer to the man it came to see.");

            Assert.True(mixed.Item1 < twoStars.stars,
                "But a weak partner still has to cost something, or there is no reason to " +
                "care who you put with your star.");

            // The lever the earlier version of this test obscured entirely: same two men,
            // same plan, same seeds — only who comes in on the hot tag. The pop belongs to
            // the man coming in, so this is a real booking decision rather than a stat.
            Assert.True(mixedStarIn.stars > mixedJobberIn.stars + 0.30,
                $"Putting the star in on the hot tag ({mixedStarIn.stars:F3}) should clearly " +
                $"beat putting the jobber in ({mixedJobberIn.stars:F3}).");
        }

        [Fact]
        public void ReadingASideAsItsWorstMember_WouldFailTheCarryTest()
        {
            // A guard on the guard. The previous version of the carry test above passed
            // with the aggregation inverted, so this pins the direction directly on the
            // quantity the ruling was about: a side of a star and a jobber must read
            // closer to the star than to the midpoint between them.
            var star   = W("Star",   overness: 95, charisma: 5.0, skill: 4.6);
            var jobber = W("Jobber", overness: 20, charisma: 1.0, skill: 2.0);

            // EffectiveOverness is the input the drag term is applied to in HeatEconomy,
            // and SideStanding uses exactly the same shape as the engine's SideAvg.
            double side     = HeatEconomy.SideStanding([star, jobber]);
            double midpoint = (star.EffectiveOverness + jobber.EffectiveOverness) / 2.0;

            output.WriteLine($"  star {star.EffectiveOverness:F1}, jobber {jobber.EffectiveOverness:F1}, " +
                             $"side reads {side:F1}, midpoint {midpoint:F1}");

            Assert.True(side > midpoint,
                "A side must read above the midpoint of its members — toward the man the " +
                "crowd came to see.");
            Assert.True(side < star.EffectiveOverness,
                "But a weak partner still has to cost something, or there is no reason to " +
                "care who you put with your star.");
        }
    }
}
