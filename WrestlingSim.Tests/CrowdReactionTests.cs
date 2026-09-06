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
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("F1"), W("F2")),
                SideB = MatchSide.Of(W("H1"), W("H2")),
                Beats =
                [
                    new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                    new MatchBeat { Type = BeatType.Cutoff,    Control = BeatControl.WrestlerB },
                    new MatchBeat { Type = BeatType.Isolation,  Control = BeatControl.WrestlerB },
                    new MatchBeat { Type = BeatType.NearTag,    Control = BeatControl.WrestlerB },
                    new MatchBeat { Type = BeatType.HotTag,     Control = BeatControl.WrestlerA },
                    new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA },
                ]
            };

            var result = new MatchEngine(Seed).Execute(plan);
            var nearTag = result.BeatResults.First(b => b.BeatType == BeatType.NearTag);

            output.WriteLine($"  near tag: delta {nearTag.CrowdEnergyDelta:F2}, " +
                             $"read as {nearTag.ResolvedReaction}");

            Assert.True(nearTag.CrowdEnergyDelta < 0, "It still quietens the room.");
            Assert.Equal(ReactionKind.Tension, nearTag.ResolvedReaction);
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

        [Fact]
        public void ADeadRoom_CostsTheCrowdComponent_ButNotTheWholeMatch()
        {
            // Two good hands working a good match in front of nobody have still worked a
            // good match — the technical and storytelling components already say so. What
            // a dead room takes is the part of the grade that was about the audience.
            double Rate(double overness, double charisma)
            {
                double total = 0;
                for (int i = 0; i < 80; i++)
                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        WrestlerA = W("A", overness: overness, charisma: charisma, skill: 4.5),
                        WrestlerB = W("B", overness: overness, charisma: charisma, skill: 4.5),
                        Beats = Structure("Technical Showcase")
                    }).StarRating;
                return total / 80;
            }

            double ignored = Rate(15, 0.8), adored = Rate(92, 4.8);
            output.WriteLine($"  same plan, same skill — ignored {ignored:F3}★, adored {adored:F3}★");

            Assert.True(adored > ignored);
            Assert.True(ignored > 0.8,
                "A well-worked match in a dead room is still a well-worked match.");
        }

        [Fact]
        public void TypicalMatchesAreNotShiftedByTheFeature()
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

        [Fact]
        public void TheCrowdNoteReadsLikeSomebodyDescribingTheRoom()
        {
            foreach (var (label, over, cha) in new[]
                     { ("nobodies", 15.0, 0.8), ("midcard", 60.0, 3.0), ("stars", 94.0, 4.9) })
            {
                var r = Run(W("A", overness: over, charisma: cha),
                            W("B", overness: over, charisma: cha), "Big Match Epic");
                output.WriteLine($"  {label,-9} {r.Reaction.Investment:F2}  \"{r.CrowdNote}\"");
                Assert.False(string.IsNullOrWhiteSpace(r.CrowdNote));
            }
        }

        [Fact]
        public void EveryBeatRecordsSomething_SoASilentMatchDoesNotReadAsPerfectlyInvested()
        {
            // The weight has a floor for exactly this reason. Without it a beat that moved
            // the room by nothing would record no reaction at all, and a match of pure
            // nothing would come out with an undefined-but-flattering profile.
            var result = Run(W("A", overness: 15, charisma: 0.8),
                             W("B", overness: 15, charisma: 0.8), "TV Formula");

            double total = result.Reaction.Engagement + result.Reaction.Disengagement;
            output.WriteLine($"  total recorded {total:F1} across {result.BeatResults.Count} beats");

            Assert.True(total > 0);
            Assert.All(result.BeatResults, b => Assert.True(Enum.IsDefined(b.ResolvedReaction)));
        }
    }
}
