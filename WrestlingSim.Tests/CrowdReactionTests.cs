using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A5 from docs/wrestling-reference/31-sim-mapping.md — crowd reaction as a vector
    /// rather than a scalar.
    ///
    /// The scalar could not answer the question a booker actually needs answered: *is
    /// anybody invested?* It also conflated the two quiet rooms that mean opposite things —
    /// an audience holding its breath and an audience that has stopped caring. Everything
    /// here follows docs/wrestling-reference/16-crowd-psychology.md §2, and §2.1 in
    /// particular: **promotions consistently over-fear boos and under-fear silence.**
    /// </summary>
    public class CrowdReactionTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string name, double overness = 70, double charisma = 3.0,
                                  double skill = 3.5) =>
            TestRoster.Make(name, overness: overness, charisma: charisma, skill: skill);

        private static List<MatchBeat> Structure(string name) =>
            MatchStructureLibrary.Find(name)!.Beats.Select(b => b.Clone()).ToList();

        private static MatchEngineResult Run(Wrestler a, Wrestler b, string structure, int seed = Seed) =>
            new MatchEngine(seed).Execute(new MatchPlanModel
            {
                WrestlerA = a, WrestlerB = b, Beats = Structure(structure)
            });

        // ── The profile itself ───────────────────────────────────────────────

        [Fact]
        public void Investment_SeparatesARoomThatCaresFromOneThatDoesNot()
        {
            var nobodies = Run(W("Nobody A", overness: 20, charisma: 1.0),
                               W("Nobody B", overness: 20, charisma: 1.0), "TV Formula");
            var midcard  = Run(W("Mid A", overness: 60, charisma: 3.0),
                               W("Mid B", overness: 60, charisma: 3.0), "TV Formula");
            var stars    = Run(W("Star A", overness: 92, charisma: 4.8),
                               W("Star B", overness: 92, charisma: 4.8), "TV Formula");

            output.WriteLine($"  nobodies {nobodies.Reaction.Investment:F3} — {nobodies.CrowdNote}");
            output.WriteLine($"  midcard  {midcard.Reaction.Investment:F3} — {midcard.CrowdNote}");
            output.WriteLine($"  stars    {stars.Reaction.Investment:F3} — {stars.CrowdNote}");

            Assert.True(midcard.Reaction.Investment > nobodies.Reaction.Investment);
            Assert.True(stars.Reaction.Investment > midcard.Reaction.Investment);

            // A midcard match has a crowd. An early version put the window in the wrong
            // place and read a midcarder as completely uninvested, which is plainly wrong.
            Assert.True(midcard.Reaction.Investment > 0.25,
                "A midcard match still has a crowd in it.");
        }

        [Fact]
        public void ARoomNobodyTurnedUpFor_IsMostlySilence()
        {
            var result = Run(W("Nobody A", overness: 15, charisma: 0.8),
                             W("Nobody B", overness: 15, charisma: 0.8), "Technical Showcase");

            output.WriteLine($"  {result.Reaction}");
            output.WriteLine($"  {result.CrowdNote}");

            Assert.Equal(ReactionKind.Silence, result.Reaction.Dominant);
            Assert.True(result.Reaction.Silence > result.Reaction.Engagement);
        }

        [Fact]
        public void HeatCountsAsEngagement_NotAsAProblem()
        {
            // The rule that matters most. A crowd booing somebody it wants beaten is a
            // crowd that is *present* — it is the fuel the whole face-in-peril structure
            // runs on — and the engine must not treat it as a failure.
            var reaction = new CrowdReaction();
            reaction.Add(ReactionKind.Heat, 100);

            Assert.Equal(1.0, reaction.Investment, 6);
            Assert.Equal(ReactionKind.Heat, reaction.Dominant);
        }

        [Fact]
        public void SilenceAndGoAwayHeatAreTheOnlyThingsThatCountAsNotCaring()
        {
            var invested = new CrowdReaction();
            invested.Add(ReactionKind.Pop, 30);
            invested.Add(ReactionKind.Heat, 30);
            invested.Add(ReactionKind.Tension, 40);

            Assert.Equal(1.0, invested.Investment, 6);
            Assert.Equal(0, invested.Disengagement);

            var gone = new CrowdReaction();
            gone.Add(ReactionKind.Silence, 50);
            gone.Add(ReactionKind.GoAwayHeat, 50);

            Assert.Equal(0.0, gone.Investment, 6);
        }

        // ── The distinction the scalar could never make ──────────────────────

        [Fact]
        public void ADeniedTag_ReadsAsTension_NotAsTheCrowdLeaving()
        {
            // The beat that motivated the whole feature. A near tag takes energy *out* of
            // the building, and the scalar could only read that as things getting worse.
            // Phase 2's adjudication had to describe it in a comment as "stored energy"
            // precisely because the engine had no way to say it.
            // Deliberately booked between people nobody is invested in. Review found the
            // first version of this test used the default TestRoster values (overness 70,
            // charisma 3.0), which land at investment 0.545 — so the *ordinary* negative
            // branch already returns Tension and the test passed with the override deleted,
            // by a margin of 0.045. Down here the ordinary branch would say GoAwayHeat, so
            // only the override can produce Tension and the test has somewhere to fail.
            MatchEngineResult Run(double overness, double charisma) =>
                new MatchEngine(Seed).Execute(new MatchPlanModel
                {
                    SideA = MatchSide.Of(W("F1", overness, charisma), W("F2", overness, charisma)),
                    SideB = MatchSide.Of(W("H1", overness, charisma), W("H2", overness, charisma)),
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.Cutoff,     Control = BeatControl.WrestlerB },
                        new MatchBeat { Type = BeatType.Isolation,  Control = BeatControl.WrestlerB },
                        new MatchBeat { Type = BeatType.NearTag,    Control = BeatControl.WrestlerB },
                        new MatchBeat { Type = BeatType.HotTag,     Control = BeatControl.WrestlerA },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA },
                    ]
                });

            foreach (var (label, overness, charisma) in new[]
                     { ("nobodies", 18.0, 0.9), ("midcard", 70.0, 3.0), ("stars", 92.0, 4.8) })
            {
                var result = Run(overness, charisma);
                var nearTag = result.BeatResults.First(b => b.BeatType == BeatType.NearTag);
                var others  = result.BeatResults.Where(b => b.CrowdEnergyDelta < -0.5 &&
                                                            b.BeatType != BeatType.NearTag).ToList();

                output.WriteLine($"  {label,-9} near tag: delta {nearTag.CrowdEnergyDelta:F2}, " +
                                 $"read as {nearTag.ResolvedReaction}   " +
                                 $"(other quiet beats: {string.Join(", ", others.Select(x => x.ResolvedReaction))})");

                Assert.True(nearTag.CrowdEnergyDelta < 0, "It still quietens the room.");
                Assert.Equal(ReactionKind.Tension, nearTag.ResolvedReaction);
            }
        }

        /// <summary>
        /// Heat means the crowd wants somebody beaten — doc 16 §2. That is a statement about
        /// **alignment**, and the first version read it off popularity, so the engine's Heat
        /// really meant *unpopular*: a huge heel recorded Pop and an unloved babyface
        /// recorded Heat. §2 flags the two readings that break a match — cheers for a heel,
        /// boos for a babyface — as the ones that matter most, and neither was expressible.
        /// </summary>
        [Fact]
        public void HeatIsAboutAlignment_NotAboutBeingUnpopular()
        {
            Wrestler Aligned(string name, Alignment alignment, double overness, double charisma)
            {
                var w = W(name, overness, charisma);
                w.Gimmick!.NaturalAlignment = alignment;
                return w;
            }

            // Matched on overness and charisma, differing only in what they are booked as.
            var face = Aligned("Face", Alignment.Face, 78, 3.8);
            var heel = Aligned("Heel", Alignment.Heel, 78, 3.8);

            var facePop = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                WrestlerA = face, WrestlerB = Aligned("Opp A", Alignment.Heel, 70, 3.2),
                Beats = Structure("Face-in-Peril")
            }).Reaction;

            var heelPop = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                WrestlerA = heel, WrestlerB = Aligned("Opp B", Alignment.Face, 70, 3.2),
                Beats = Structure("Face-in-Peril")
            }).Reaction;

            output.WriteLine($"  babyface on top: {facePop}");
            output.WriteLine($"  heel on top:     {heelPop}");

            Assert.True(facePop.Pop > facePop.Heat,
                "A babyface going over should be cheered, not booed.");
            Assert.True(heelPop.Heat > heelPop.Pop,
                "A heel going over should draw heat — and 'unpopular' is not what that means: " +
                $"these two have identical overness and charisma. Got pop {heelPop.Pop:F1} " +
                $"vs heat {heelPop.Heat:F1}.");

            // And the crossover doc 16 §2 calls the important case: a heel the crowd adores
            // gets cheered anyway.
            var adored = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                WrestlerA = Aligned("Cool Heel", Alignment.Heel, 97, 5.0),
                WrestlerB = Aligned("Opp C", Alignment.Face, 60, 2.5),
                Beats = Structure("Face-in-Peril")
            }).Reaction;

            output.WriteLine($"  adored heel:     {adored}");
            Assert.True(adored.Pop > adored.Heat,
                "A heel the audience loves is cheered — that is the reading §2 says matters most.");
        }

        /// <summary>
        /// An unpopulated reaction described the loudest failure in the vocabulary. `Dominant`
        /// began its search at `pairs[0]` with a strict `&gt;`, so an all-zero profile returned
        /// `Pop`; the `Label` tie-break then fell through to the go-away branch. And
        /// `MatchEngineResult.Reaction` defaults to `new()`, so any result not produced by the
        /// engine said "they stopped watching and started entertaining themselves".
        /// </summary>
        [Fact]
        public void AReactionWithNothingRecorded_SaysSoRatherThanReportingTheWorst()
        {
            var empty = new CrowdReaction();

            output.WriteLine($"  empty: dominant {empty.Dominant}, label \"{empty.Label}\", " +
                             $"investment {empty.Investment:F2}");

            Assert.True(empty.IsEmpty);
            Assert.Equal(ReactionKind.Silence, empty.Dominant);
            Assert.DoesNotContain("entertaining themselves", empty.Label);

            // Same for a result the engine never filled in.
            Assert.Equal(ReactionKind.Silence, new MatchEngineResult
            {
                Winner = W("A"), Loser = W("B")
            }.Reaction.Dominant);

            // And a genuine tie between silence and go-away heat reads as the quieter of
            // the two, not the louder.
            var tied = new CrowdReaction();
            tied.Add(ReactionKind.Silence, 10);
            tied.Add(ReactionKind.GoAwayHeat, 10);
            output.WriteLine($"  tied:  \"{tied.Label}\"");
            Assert.DoesNotContain("entertaining themselves", tied.Label);
        }

        [Fact]
        public void AnOverworkedHeat_ReadsAsTheCrowdEntertainingItself()
        {
            // The other side of the same coin, and the reason the two cannot be one number:
            // both take energy out of the room and they are opposite outcomes.
            var beats = new List<MatchBeat>
            {
                new() { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                new() { Type = BeatType.Cutoff, Control = BeatControl.WrestlerB }
            };
            for (int i = 0; i < 6; i++)
                beats.Add(new MatchBeat { Type = BeatType.Isolation, Control = BeatControl.WrestlerB });
            beats.Add(new MatchBeat { Type = BeatType.HotTag, Control = BeatControl.WrestlerA });
            beats.Add(new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA });

            var result = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                SideA = MatchSide.Of(W("F1"), W("F2")),
                SideB = MatchSide.Of(W("H1"), W("H2")),
                Beats = beats
            });

            var late = result.BeatResults
                .Where(b => b.BeatType == BeatType.Isolation)
                .Skip(3)
                .ToList();

            output.WriteLine($"  {result.Reaction}");
            foreach (var b in late) output.WriteLine($"    late isolation → {b.ResolvedReaction}");

            Assert.All(late, b => Assert.Equal(ReactionKind.GoAwayHeat, b.ResolvedReaction));
            Assert.True(result.Reaction.GoAwayHeat > 0);
        }

        [Fact]
        public void RepeatingTheSameBeatUntilNobodyIsWatching_ReadsAsGoAwayHeat()
        {
            // Booking, not casting. A crowd asked to watch the same thing over and over
            // starts entertaining itself — doc 16 §2's sarcastic chants and counting along,
            // which it calls a red alert.
            var beats = new List<MatchBeat>
            {
                new() { Type = BeatType.StandardOpening, Control = BeatControl.Even }
            };
            for (int i = 0; i < 6; i++)
                beats.Add(new MatchBeat { Type = BeatType.RestHold, Control = BeatControl.WrestlerB });
            beats.Add(new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA });

            var result = new MatchEngine(Seed).Execute(new MatchPlanModel
            {
                WrestlerA = W("A", overness: 90, charisma: 4.5),
                WrestlerB = W("B", overness: 90, charisma: 4.5),
                Beats = beats
            });

            output.WriteLine($"  {result.Reaction}");

            // Even with two genuine draws in the ring, flogging one beat loses the room.
            Assert.True(result.Reaction.GoAwayHeat > 0,
                "Six rest holds in a row should lose the crowd however over the wrestlers are.");
        }

        // ── What it does to the rating ───────────────────────────────────────

        /// <summary>
        /// The shape of the curve, asserted directly.
        ///
        /// This replaces a test that could not fail. The old
        /// `ADeadRoom_CostsTheCrowdComponent_ButNotTheWholeMatch` compared a 15-overness
        /// pairing against a 92-overness one and asserted the second rated higher — which
        /// is true with A5 entirely absent, and review confirmed it held on the parent
        /// commit to four decimal places. Worse, the dead pairing it chose has a raw crowd
        /// reading below `CrowdFloor`, so its crowd component is *already zero* and the
        /// multiplier is mathematically incapable of touching it. It was the only test
        /// aimed at the rating effect and it did not test it.
        /// </summary>
        [Fact]
        public void TheInvestmentCurve_IsCentredOnTypicalAndAsymmetric()
        {
            double dead    = MatchEngine.InvestmentFactor(0.0);
            double typical = MatchEngine.InvestmentFactor(MatchEngine.TypicalInvestment);
            double full    = MatchEngine.InvestmentFactor(1.0);

            output.WriteLine($"  dead {dead:F4}   typical {typical:F4}   full {full:F4}");

            Assert.Equal(1.0, typical, 3);
            Assert.True(dead < 0.75, $"A room that never turned up should cost real value, got {dead:F3}");
            Assert.True(dead > 0.55, "…but never the whole crowd component.");
            Assert.True(full > 1.0 && full < 1.10,
                $"Being present all night is the baseline, not a bonanza, got {full:F3}");

            // Doc 16 §2.1: promotions over-fear boos and under-fear silence. So the
            // downside has to be materially steeper than the upside, and this is where
            // that is actually checked rather than asserted in a comment.
            Assert.True(1.0 - dead > (full - 1.0) * 3,
                "Silence should cost far more than engagement pays.");

            // Monotone, with no flat region — the old version clamped, and review measured
            // 64.6% of all shipped-roster matches sitting against the upper bound and
            // receiving an identical multiplier.
            double prev = -1;
            for (double x = 0; x <= 1.0001; x += 0.05)
            {
                double f = MatchEngine.InvestmentFactor(x);
                Assert.True(f > prev, $"Not monotone at {x:F2}");
                prev = f;
            }
        }

        /// <summary>
        /// And that the curve is actually *wired in* — the mutation the old test could not
        /// catch. Review deleted the whole investment→rating multiplier and all 429 tests
        /// still passed.
        ///
        /// Asserted against the reported breakdown rather than against the star rating,
        /// because the star rating is the sum of six terms and so proves nothing about any
        /// one of them. That is exactly how a deleted term went unnoticed.
        /// </summary>
        [Fact]
        public void TheCrowdComponent_IsActuallyScaledByInvestment()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            int checkedMatches = 0, moved = 0;
            double biggest = 0;

            foreach (var st in MatchStructureLibrary.ForSideSize(1).Where(x => !x.RequiresFeud))
            foreach (var a in roster)
            foreach (var b in roster)
            {
                if (ReferenceEquals(a, b)) continue;

                var r = new MatchEngine(HashCode.Combine(st.Name, a.Id, b.Id) & 0x7FFFFFFF)
                    .Execute(new MatchPlanModel
                    {
                        WrestlerA = a, WrestlerB = b,
                        Beats = st.Beats.Select(x => x.Clone()).ToList()
                    });

                var bd = r.Breakdown;

                // The identity: the crowd term IS the pre-investment term times the curve.
                Assert.Equal(bd.CrowdBeforeInvestment * MatchEngine.InvestmentFactor(r.Reaction.Investment),
                             bd.Crowd, 9);
                Assert.Equal(MatchEngine.InvestmentFactor(r.Reaction.Investment),
                             bd.InvestmentFactor, 9);

                // And — the part that was missing, and it was the whole point — that the
                // crowd term the breakdown reports is the one the *rating* actually used.
                //
                // Review mutated the final sum to use `crowdBeforeInvestment` while leaving
                // `Breakdown` untouched: that removes the entire investment→rating effect
                // from every rating the game produces, and **all 479 tests passed**, because
                // everything above only checks the reporting object against itself. That is
                // round 1's blocker — "delete the multiplier, 429 tests pass" — wearing a
                // different costume, in the commit whose stated purpose was to make the
                // composite testable.
                //
                // Nothing else in the suite asserts the six terms sum to the score. Now this
                // does. (The 0–100 clamp never binds on this corpus; if it ever does, this
                // is the right place to find out.)
                Assert.Equal(bd.Technical + bd.Storytelling + bd.Crowd
                             + bd.FinishNudge + bd.VarietyNudge + bd.CoherenceNudge,
                             r.FinalScore, 9);

                checkedMatches++;
                if (bd.CrowdBeforeInvestment > 0.01 && Math.Abs(bd.InvestmentPoints) > 0.01) moved++;
                biggest = Math.Max(biggest, Math.Abs(bd.InvestmentPoints));
            }

            output.WriteLine($"  {checkedMatches:N0} matches; investment moved the score in " +
                             $"{moved * 100.0 / checkedMatches:F1}% of them, biggest {biggest:F2} points " +
                             $"({biggest / 20:F3}★)");

            // And it has to actually be doing something in most of the corpus, not just be
            // arithmetically present.
            Assert.True(moved > checkedMatches * 0.5,
                $"Investment changed the score in only {moved * 100.0 / checkedMatches:F1}% of matches.");
            Assert.True(biggest > 2.0,
                $"Investment never moved a match by more than {biggest:F2} points — it is decorative.");
        }

        /// <summary>
        /// The other half of the same claim: that what moves is the *tails*, not the whole
        /// scale. This is the assertion whose earlier version was documented as measured and
        /// was wrong by 0.26 — `TypicalInvestment` shipped at 0.50 while the shipped roster's
        /// median was 0.7635, so 64.6% of matches saturated the upper clamp and received an
        /// identical flat uplift. Pinned to the corpus rather than to the constant, so it
        /// fails if either drifts away from the other.
        /// </summary>
        [Fact]
        public void ATypicalMatchIsUnmoved_AndThatIsMeasuredNotAsserted()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var factors = new List<double>();

            foreach (var st in MatchStructureLibrary.ForSideSize(1).Where(x => !x.RequiresFeud))
            foreach (var a in roster)
            foreach (var b in roster)
            {
                if (ReferenceEquals(a, b)) continue;
                factors.Add(new MatchEngine(HashCode.Combine(st.Name, a.Id, b.Id) & 0x7FFFFFFF)
                    .Execute(new MatchPlanModel
                    {
                        WrestlerA = a, WrestlerB = b,
                        Beats = st.Beats.Select(x => x.Clone()).ToList()
                    }).Breakdown.InvestmentFactor);
            }

            factors.Sort();
            double median = factors[factors.Count / 2];
            double p05 = factors[(int)(factors.Count * 0.05)];
            double p95 = factors[(int)(factors.Count * 0.95)];

            output.WriteLine($"  n={factors.Count:N0}  p05 {p05:F4}  median {median:F4}  p95 {p95:F4}");

            Assert.True(Math.Abs(median - 1.0) < 0.03,
                $"The median match should come out at 1.0, not {median:F4} — TypicalInvestment " +
                "has drifted away from the roster it is supposed to describe.");

            // No pile-up at either end: the old asymmetric clamp put two-thirds of the
            // corpus on one value.
            double ceiling = factors.Max();
            int atCeiling = factors.Count(x => x >= ceiling - 1e-9);
            output.WriteLine($"  {atCeiling * 100.0 / factors.Count:F2}% sit at the maximum factor");
            Assert.True(atCeiling < factors.Count * 0.05,
                $"{atCeiling * 100.0 / factors.Count:F1}% of matches get an identical multiplier — " +
                "that is a constant, not a tail effect.");

            Assert.True(p05 < 0.93, $"The bottom of the distribution barely moves ({p05:F3}).");
        }

        /// <summary>
        /// A declared override takes the beat's *whole* weight, and the weight is the beat's
        /// crowd swing.
        ///
        /// Review mutated `RecordReaction(declared, weight)` to `RecordReaction(declared,
        /// 1.0)` and all 479 tests passed: only the *kind* of the two override beats was
        /// tested, never their magnitude — and those two beats, the denied tag and the
        /// overworked isolation, are the ones A5 exists to represent. It also breaks the
        /// weight-conservation the rest of the feature relies on.
        /// </summary>
        [Fact]
        public void ADeclaredOverride_CarriesTheBeatsFullWeight()
        {
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("F1", 88, 4.4), W("F2", 86, 4.2)),
                SideB = MatchSide.Of(W("H1", 87, 4.3), W("H2", 85, 4.1)),
                Beats =
                [
                    new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                    new MatchBeat { Type = BeatType.Cutoff,    Control = BeatControl.WrestlerB },
                    new MatchBeat { Type = BeatType.Isolation,  Control = BeatControl.WrestlerB },
                    new MatchBeat { Type = BeatType.NearTag,    Control = BeatControl.WrestlerB,
                                    Intensity = BeatIntensity.Extreme },
                    new MatchBeat { Type = BeatType.HotTag,     Control = BeatControl.WrestlerA },
                    new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA },
                ]
            };

            // Two runs differing only in how hard the near tag hits. The override's share of
            // tension has to move by exactly the change in that beat's weight — a flat 1.0
            // would not move at all. (Asserting the near tag's weight *equals* total tension
            // would be wrong: the neutral branch records tension too, which is the point of
            // separating "holding its breath" from "gone".)
            MatchEngineResult Run(BeatIntensity intensity)
            {
                var beats = plan.Beats.Select(x => x.Clone()).ToList();
                beats.First(x => x.Type == BeatType.NearTag).Intensity = intensity;
                return new MatchEngine(Seed).Execute(new MatchPlanModel
                {
                    SideA = plan.SideA, SideB = plan.SideB, Beats = beats
                });
            }

            var soft = Run(BeatIntensity.Low);
            var hard = Run(BeatIntensity.Extreme);

            double SoftWeight(MatchEngineResult r) =>
                Math.Max(1.5, Math.Abs(r.BeatResults.First(b => b.BeatType == BeatType.NearTag)
                                        .CrowdEnergyDelta));

            double weightGap  = SoftWeight(hard) - SoftWeight(soft);
            double tensionGap = hard.Reaction.Tension - soft.Reaction.Tension;

            output.WriteLine($"  near tag at Low swung {SoftWeight(soft):F2}, at Extreme {SoftWeight(hard):F2}");
            output.WriteLine($"  tension {soft.Reaction.Tension:F2} → {hard.Reaction.Tension:F2}");

            Assert.True(weightGap > 2.0,
                $"setup: the two intensities should differ by more than a flat unit, got {weightGap:F2}");
            Assert.Equal(weightGap, tensionGap, 6);

            // And the whole match still conserves: every beat contributes exactly
            // max(1.5, |delta|), overrides included.
            foreach (var r in new[] { soft, hard })
                Assert.Equal(r.BeatResults.Sum(b => Math.Max(1.5, Math.Abs(b.CrowdEnergyDelta))),
                             r.Reaction.Total, 6);
        }

        /// <summary>
        /// The half of A5 that is about booking rather than casting — and the half that
        /// shipped inert.
        ///
        /// It first went in as `if (r.RepetitionFactor &lt; 0.5) → GoAwayHeat`. Review
        /// measured that firing on **0 of 33,060 beats** across every shipped structure and
        /// every roster pairing, because `RepetitionFactor` bottoms out at 0.680 on shipped
        /// content: no preset repeats a beat type often enough. A threshold nothing reaches
        /// is not a mechanism. Repetition is a continuous input to investment now, so it
        /// applies to a main-eventer's fourth near-fall as much as to a jobber's first.
        /// </summary>
        [Fact]
        public void RepeatingABeat_CostsInvestment_EvenForPeopleTheCrowdLoves()
        {
            var a = W("Star A", overness: 92, charisma: 4.8, skill: 4.2);
            var b = W("Star B", overness: 90, charisma: 4.6, skill: 4.2);

            MatchEngineResult Book(int heatSegments)
            {
                var beats = new List<MatchBeat>
                {
                    new() { Type = BeatType.HotOpening, Control = BeatControl.Even }
                };
                for (int i = 0; i < heatSegments; i++)
                    beats.Add(new MatchBeat { Type = BeatType.HeatSegment, Control = BeatControl.WrestlerB });
                beats.Add(new MatchBeat { Type = BeatType.Comeback,    Control = BeatControl.WrestlerA });
                beats.Add(new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA });

                return new MatchEngine(Seed).Execute(new MatchPlanModel
                {
                    WrestlerA = a, WrestlerB = b, Beats = beats
                });
            }

            var once = Book(1);
            var spam = Book(5);

            output.WriteLine($"  one heat segment  investment {once.Reaction.Investment:F3} — {once.CrowdNote}");
            output.WriteLine($"  five in a row     investment {spam.Reaction.Investment:F3} — {spam.CrowdNote}");
            output.WriteLine($"  go-away heat: {once.Reaction.GoAwayHeat:F1} → {spam.Reaction.GoAwayHeat:F1}");

            Assert.True(spam.Reaction.Investment < once.Reaction.Investment - 0.02,
                "Showing the crowd the same thing five times should cost their attention, " +
                $"got {spam.Reaction.Investment:F3} vs {once.Reaction.Investment:F3}.");
            Assert.True(spam.Reaction.GoAwayHeat > once.Reaction.GoAwayHeat,
                "A crowd worn out by repetition entertains itself — that is go-away heat, " +
                "not silence.");
        }

        /// <summary>
        /// The per-beat label has to be the component that was actually recorded.
        ///
        /// Review found the positive branch ended `return liked &gt;= 0.5 ? Pop : Heat`, so it
        /// could never say Silence however absent the room. Two nobodies in a Spotfest
        /// produced seven beats all labelled `Heat` — documented as engagement and a *good*
        /// outcome — in a match whose own aggregate recorded 45.1 silence and zero heat.
        /// </summary>
        [Fact]
        public void EveryBeatsLabel_MatchesWhatThatBeatActuallyRecorded()
        {
            foreach (var (label, a, b) in new[]
            {
                ("nobodies", W("Nobody A", overness: 15, charisma: 0.8),
                             W("Nobody B", overness: 15, charisma: 0.8)),
                ("stars",    W("Star A", overness: 92, charisma: 4.8),
                             W("Star B", overness: 90, charisma: 4.6)),
            })
            {
                var r = Run(a, b, "Spotfest");
                var counts = r.BeatResults.GroupBy(x => x.ResolvedReaction)
                              .ToDictionary(g => g.Key, g => g.Count());

                output.WriteLine($"  {label,-9} aggregate {r.Reaction}  →  " +
                                 string.Join(", ", counts.Select(k => $"{k.Key} ×{k.Value}")));

                // The aggregate is the sum of the per-beat splits, so the component the
                // whole match recorded most of has to appear among the per-beat labels.
                Assert.Contains(r.Reaction.Dominant, counts.Keys);
            }
        }

        /// <summary>
        /// Renamed, because the old name — `TypicalMatchesAreNotShiftedByTheFeature` — is a
        /// claim its own data denies: review measured this pairing's crowd term moving by
        /// −1.44 points (−0.072★). What the test actually asserts, and all it ever
        /// asserted, is that a midcard pairing reads as a middling room. The claim about
        /// ratings being unshifted belongs to
        /// <see cref="ATypicalMatchIsUnmoved_AndThatIsMeasuredNotAsserted"/>, which measures
        /// the whole corpus rather than one pair.
        /// </summary>
        [Fact]
        public void AMidcardPairing_ReadsAsAMiddlingRoom()
        {
            // The multiplier is centred rather than applied as a penalty. An earlier version
            // scaled the crowd component straight down by investment, which charged low
            // connection twice — CrowdCeiling already does it — and compressed every
            // difference living in the crowd component. Six unrelated tests failed at once,
            // which is what double-counting looks like.
            //
            // This pins the shape: a normal match comes out where it always did.
            var midcard = Run(W("Mid A", overness: 62, charisma: 3.1),
                              W("Mid B", overness: 58, charisma: 2.9), "TV Formula");

            output.WriteLine($"  investment {midcard.Reaction.Investment:F3}, {midcard.StarDisplay}");

            Assert.InRange(midcard.Reaction.Investment, 0.35, 0.65);
        }

        /// <summary>
        /// The note has to *describe* the room, which means different rooms get different
        /// notes. This used to assert only `!IsNullOrWhiteSpace`, which `Label` cannot
        /// return — so it read three matches and checked nothing about any of them.
        /// </summary>
        [Fact]
        public void TheCrowdNoteReadsLikeSomebodyDescribingTheRoom()
        {
            var notes = new List<string>();
            foreach (var (label, over, cha) in new[]
                     { ("nobodies", 15.0, 0.8), ("midcard", 60.0, 3.0), ("stars", 94.0, 4.9) })
            {
                var r = Run(W("A", overness: over, charisma: cha),
                            W("B", overness: over, charisma: cha), "Big Match Epic");
                output.WriteLine($"  {label,-9} {r.Reaction.Investment:F2}  \"{r.CrowdNote}\"");
                notes.Add(r.CrowdNote);
            }

            Assert.Equal(3, notes.Distinct().Count());
            Assert.All(notes, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        }

        /// <summary>
        /// The weight floor, tested rather than asserted in a comment.
        ///
        /// The old test for this said "the weight has a floor for exactly this reason" and
        /// review confirmed that removing the floor changed nothing it measured. So this
        /// asserts the floor's arithmetic signature directly: every beat contributes
        /// `max(1.5, |crowd delta|)`, so a match containing any near-inert beat must record
        /// strictly *more* total reaction than the sum of its raw deltas.
        ///
        /// It matters because a beat that moved the room by 0.0003 is not evidence that
        /// anybody was watching — but without a floor it is not evidence of anything at
        /// all, and a long stretch of nothing would leave the room's presence read off the
        /// two beats that did land. 5.1% of beats across the shipped roster fall below it.
        /// </summary>
        [Fact]
        public void ABeatThatMovesNobody_StillCountsAsAMomentTheRoomSatThrough()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            int withInertBeats = 0, floored = 0;

            foreach (var st in MatchStructureLibrary.ForSideSize(1).Where(x => !x.RequiresFeud))
            foreach (var a in roster.Take(12))
            foreach (var b in roster.Take(12))
            {
                if (ReferenceEquals(a, b)) continue;

                var r = new MatchEngine(HashCode.Combine(st.Name, a.Id, b.Id) & 0x7FFFFFFF)
                    .Execute(new MatchPlanModel
                    {
                        WrestlerA = a, WrestlerB = b,
                        Beats = st.Beats.Select(x => x.Clone()).ToList()
                    });

                double raw = r.BeatResults.Sum(x => Math.Abs(x.CrowdEnergyDelta));
                bool hasInert = r.BeatResults.Any(x => Math.Abs(x.CrowdEnergyDelta) < 1.5);
                if (!hasInert) continue;

                withInertBeats++;
                if (r.Reaction.Total > raw + 1e-6) floored++;
            }

            output.WriteLine($"  {withInertBeats} matches contained a beat below the floor; " +
                             $"{floored} of them recorded more than their raw deltas");

            Assert.True(withInertBeats > 20,
                "Not enough near-inert beats in the corpus to test the floor.");
            Assert.Equal(withInertBeats, floored);
        }

        // EveryBeatRecordsSomething_SoASilentMatchDoesNotReadAsPerfectlyInvested is gone.
        // The build log claimed it had been "replaced rather than patched"; review found it
        // byte-identical to round 1 and still asserting `total > 0` and
        // `Enum.IsDefined(ResolvedReaction)` — both unfalsifiable by construction. A new
        // test had been added *alongside* it. `ABeatThatMovesNobody…` above is the real
        // guard for the same property, and it dies when the floor is removed.
    }
}
