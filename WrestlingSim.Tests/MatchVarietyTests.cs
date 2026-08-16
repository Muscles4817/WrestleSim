using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Guards the properties that stop the rating from collapsing into a beat counter.
    ///
    /// The engine used to score a match by adding up its beats with no saturation and no
    /// repetition penalty, which made "book more beats" a strictly dominant strategy: a
    /// twelve-beat plan of one repeated move returned a deterministic 5.00★. These tests
    /// exist so that can never quietly come back.
    /// </summary>
    public class MatchVarietyTests(ITestOutputHelper output)
    {
        private static Wrestler A => TestRoster.Make("Alpha", popularity: 85, charisma: 4.0, skill: 3.6);
        private static Wrestler B => TestRoster.Make("Bravo", popularity: 82, charisma: 3.8, skill: 3.4);

        private static MatchBeat T(string template, BeatControl control) =>
            BeatLibrary.Find(template)!.ToMatchBeat(control);

        private static double MeanStars(IEnumerable<MatchBeat> beats, int runs = 200,
            MatchType type = MatchType.Standard)
        {
            var list = beats.ToList();
            double sum = 0;
            for (int i = 0; i < runs; i++)
                sum += new MatchEngine(i * 6151).Execute(new MatchPlan
                {
                    WrestlerA = A, WrestlerB = B, MatchType = type,
                    Beats = list.Select(b => b.Clone()).ToList()
                }).StarRating;
            return sum / runs;
        }

        /// <summary>Opening + N copies of one template + finish.</summary>
        private static List<MatchBeat> SpamPlan(string template, int copies, BeatControl control)
        {
            var beats = new List<MatchBeat> { T("Hot Start", BeatControl.Even) };
            for (int i = 0; i < copies; i++) beats.Add(T(template, control));
            beats.Add(T("Clean Victory", BeatControl.WrestlerA));
            return beats;
        }

        // ── The exploit that broke the game ──────────────────────────────────

        [Theory]
        [InlineData("Explosive Flurry", BeatControl.WrestlerB)]
        [InlineData("Shock Kickout",    BeatControl.WrestlerA)]
        [InlineData("Trash Talk",       BeatControl.WrestlerA)]
        [InlineData("Aerial Assault",   BeatControl.WrestlerA)]
        [InlineData("Ringside Brawl",   BeatControl.Even)]
        public void RepeatingOneBeat_StopsPaying(string template, BeatControl control)
        {
            double four     = MeanStars(SpamPlan(template, 4, control));
            double twelve   = MeanStars(SpamPlan(template, 12, control));
            double thirty   = MeanStars(SpamPlan(template, 30, control));

            output.WriteLine($"  {template,-18}  4x {four:F3}   12x {twelve:F3}   30x {thirty:F3}");

            // Tripling the length must not keep buying rating.
            Assert.True(thirty < 4.5,
                $"30 copies of {template} reached {thirty:F2}★ — repetition is still payable.");
            Assert.True(thirty - twelve < 0.15,
                $"Going from 12 to 30 copies of {template} still gained {thirty - twelve:F3}★; " +
                "diminishing returns are not biting.");
        }

        [Fact]
        public void NoSpamPlan_CanReachAPerfectRating()
        {
            // The specific old failure: 12 beats of alternating heat/comeback returned
            // 5.00 as both the minimum and the maximum over 200 seeds.
            var beats = new List<MatchBeat> { T("Hot Start", BeatControl.Even) };
            for (int i = 0; i < 12; i++)
            {
                beats.Add(T("Explosive Flurry", BeatControl.WrestlerB));
                beats.Add(T("Fighting Spirit",  BeatControl.WrestlerA));
            }
            beats.Add(T("Dominant Statement", BeatControl.WrestlerA));

            double worst = double.MaxValue, best = double.MinValue;
            for (int i = 0; i < 200; i++)
            {
                double s = new MatchEngine(i * 3571).Execute(new MatchPlan
                {
                    WrestlerA = A, WrestlerB = B,
                    Beats = beats.Select(b => b.Clone()).ToList()
                }).StarRating;
                worst = Math.Min(worst, s); best = Math.Max(best, s);
            }

            output.WriteLine($"  26-beat alternating spam: {worst:F3} – {best:F3}");

            Assert.True(best < 4.9,
                $"A 26-beat two-move plan reached {best:F2}★. Length is still a substitute for quality.");
        }

        [Fact]
        public void VariedBooking_BeatsRepetitiveBooking_AtEqualLength()
        {
            // Same beat count, same wrestlers, same finish — only the variety differs.
            var repetitive = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Hot Comeback", BeatControl.WrestlerA),
                T("Shock Kickout", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerA),
            };
            // Same length, comparable beat strength, but three distinct beat types where
            // the repetitive plan has one repeated three times.
            var varied = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Ringside Brawl", BeatControl.Even),
                T("Trash Talk", BeatControl.WrestlerB),
                T("Hot Comeback", BeatControl.WrestlerA),
                T("Shock Kickout", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerA),
            };

            double rep = MeanStars(repetitive);
            double var = MeanStars(varied);

            output.WriteLine($"  repetitive (3x beatdown): {rep:F3}");
            output.WriteLine($"  varied (beatdown/hold/taunt): {var:F3}");

            Assert.True(var > rep,
                $"Seven varied beats should beat seven repetitive ones: {var:F3} vs {rep:F3}");
        }

        [Fact]
        public void LongMatches_DoNotAutomaticallyOutrankShortOnes()
        {
            // A tight, well-built four-beat TV match should be able to beat a bloated
            // fifteen-beat one. Under the old engine this was impossible by construction.
            var tight = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(b => b.Clone()).ToList();

            var bloated = new List<MatchBeat> { T("Feeling-Out Process", BeatControl.Even) };
            for (int i = 0; i < 13; i++)
                bloated.Add(T("Wear-Down Hold", i % 2 == 0 ? BeatControl.WrestlerB : BeatControl.WrestlerA));
            bloated.Add(T("Count-Out", BeatControl.WrestlerA));

            double tightScore   = MeanStars(tight);
            double bloatedScore = MeanStars(bloated);

            output.WriteLine($"  4-beat TV Formula: {tightScore:F3}   15-beat rest-hold marathon: {bloatedScore:F3}");

            Assert.True(tightScore > bloatedScore,
                $"A tight 4-beat match must be able to beat a bloated 15-beat one: " +
                $"{tightScore:F3} vs {bloatedScore:F3}");
        }

        // ── Booking decisions have to cost something ─────────────────────────

        [Fact]
        public void UnearnedFinish_IsPenalised()
        {
            // Book the win to the wrestler who has spent the whole match being beaten up.
            var earned = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Hot Comeback", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerA),
            };
            var unearned = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Power Beatdown", BeatControl.WrestlerA),
                T("Hot Comeback", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerB),   // B wins with no momentum
            };

            double e = MeanStars(earned), u = MeanStars(unearned);
            output.WriteLine($"  earned finish {e:F3}   unearned finish {u:F3}");

            Assert.True(e - u > 0.25,
                $"An unearned finish should cost real rating: {e:F3} vs {u:F3}");
        }

        [Fact]
        public void ClassicStructures_CanActuallyEarnTheirFinish()
        {
            // Regression test for a specific bug: Face-in-Peril, Technical Showcase and
            // Grudge Brawl took the unearned-finish penalty on essentially every run,
            // because two heat segments swung more momentum than one comeback could
            // recover. The game's most classic structures could not book a clean win.
            foreach (var name in new[] { "Face-in-Peril", "Technical Showcase", "Grudge Brawl", "TV Formula" })
            {
                var st = MatchStructureLibrary.Find(name)!;
                int earned = 0;
                const int runs = 200;

                for (int i = 0; i < runs; i++)
                {
                    var r = new MatchEngine(i * 15485863).Execute(new MatchPlan
                    {
                        WrestlerA = A, WrestlerB = B,
                        Beats = st.Beats.Select(b => b.Clone()).ToList()
                    });
                    // FinishQuality = earnedMultiplier*80 + crowd*0.2, so the earned band
                    // starts at 80 and the unearned band tops out at 64.
                    if (r.FinishQuality >= 72) earned++;
                }

                double rate = 100.0 * earned / runs;
                output.WriteLine($"  {name,-20} earned finish in {rate:F1}% of runs");

                Assert.True(rate >= 80.0,
                    $"{name} is booked so the face wins after a comeback; it should read as an " +
                    $"earned finish nearly always, got {rate:F1}%");
            }
        }

        [Fact]
        public void RestHolds_CoolTheCrowd_AndAreNotFreeFiller()
        {
            var withoutHold = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Hot Comeback", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerA),
            };
            var withHolds = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Power Beatdown", BeatControl.WrestlerB),
                T("Wear-Down Hold", BeatControl.WrestlerB),
                T("Wear-Down Hold", BeatControl.WrestlerB),
                T("Wear-Down Hold", BeatControl.WrestlerB),
                T("Hot Comeback", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerA),
            };

            double clean = MeanStars(withoutHold), padded = MeanStars(withHolds);
            output.WriteLine($"  4 beats {clean:F3}   same + 3 rest holds {padded:F3}");

            Assert.True(padded < clean,
                $"Padding a match with rest holds must make it worse, not longer-and-better: " +
                $"{padded:F3} vs {clean:F3}");
        }

        // ── Match type is a real choice ──────────────────────────────────────

        [Fact]
        public void DeclaringAMatchType_ChangesTheResult()
        {
            // The old engine ignored MatchType entirely: all four produced bit-identical
            // ratings on 57,600 paired seeds.
            var beats = MatchStructureLibrary.Find("Technical Showcase")!.Beats.Select(b => b.Clone()).ToList();

            var byType = Enum.GetValues<MatchType>()
                .ToDictionary(t => t, t => MeanStars(beats, type: t));

            foreach (var kv in byType) output.WriteLine($"  {kv.Key,-14} {kv.Value:F3}");

            Assert.True(byType.Values.Max() - byType.Values.Min() > 0.10,
                "Match type must change the rating; got a spread of " +
                $"{byType.Values.Max() - byType.Values.Min():F3} stars.");
        }

        [Fact]
        public void DeclaringTheWrongMatchType_IsPunished()
        {
            // A mat-based plan called a technical match should beat the same plan called
            // a spotfest — you are graded against what you advertised.
            var matBased = MatchStructureLibrary.Find("Technical Showcase")!.Beats.Select(b => b.Clone()).ToList();
            var aerial   = MatchStructureLibrary.Find("Spotfest")!.Beats.Select(b => b.Clone()).ToList();

            double matAsTechnical = MeanStars(matBased, type: MatchType.Technical);
            double matAsSpotfest  = MeanStars(matBased, type: MatchType.Spotfest);
            double airAsSpotfest  = MeanStars(aerial,   type: MatchType.Spotfest);
            double airAsTechnical = MeanStars(aerial,   type: MatchType.Technical);

            output.WriteLine($"  mat plan   : Technical {matAsTechnical:F3}  Spotfest {matAsSpotfest:F3}");
            output.WriteLine($"  aerial plan: Spotfest  {airAsSpotfest:F3}  Technical {airAsTechnical:F3}");

            Assert.True(matAsTechnical > matAsSpotfest,
                "A mat-based plan should score better declared Technical than declared Spotfest.");
            Assert.True(airAsSpotfest > airAsTechnical,
                "An aerial plan should score better declared Spotfest than declared Technical.");
        }

        // ── Feuds have to pay off ────────────────────────────────────────────

        [Fact]
        public void FeudIntensity_PaysOffThroughTheMultiplier_NotJustStartingEnergy()
        {
            // Feud.IntensityMultiplier used to be unreachable: it only applied via an
            // explicit FeudalResonance, which no structure or booking flow ever set, so it
            // was dead code on every path a player could take.
            var a = A; var b = B;
            var beats = MatchStructureLibrary.Find("Grudge Brawl")!.Beats.Select(x => x.Clone()).ToList();
            // Add a storytelling beat so the feud has something to amplify.
            beats.Insert(beats.Count - 1, T("Trash Talk", BeatControl.WrestlerB));

            double Run(FeudIntensity? intensity)
            {
                Feud? feud = null;
                if (intensity is { } i)
                {
                    feud = new Feud { WrestlerA = a, WrestlerB = b };
                    feud.SetMinimumIntensity(i);
                }
                double sum = 0;
                const int runs = 200;
                for (int k = 0; k < runs; k++)
                    sum += new MatchEngine(k * 401).Execute(new MatchPlan
                    {
                        WrestlerA = a, WrestlerB = b, Feud = feud,
                        Beats = beats.Select(x => x.Clone()).ToList()
                    }).StarRating;
                return sum / runs;
            }

            double none    = Run(null);
            double hot     = Run(FeudIntensity.Hot);
            double nuclear = Run(FeudIntensity.Nuclear);

            output.WriteLine($"  none {none:F3}   hot {hot:F3}   nuclear {nuclear:F3}");

            Assert.True(nuclear > none + 0.20,
                $"A nuclear feud should be worth real rating over no feud: {nuclear:F3} vs {none:F3}");
            Assert.True(nuclear > hot,
                $"Nuclear should outscore Hot: {nuclear:F3} vs {hot:F3}");
        }

        [Fact]
        public void FeudMultiplier_ReachesFeudBeats_WithoutAnExplicitResonance()
        {
            var a = A; var b = B;
            var feud = new Feud { WrestlerA = a, WrestlerB = b };
            feud.SetMinimumIntensity(FeudIntensity.Nuclear);

            // A plain storytelling beat with no FeudalResonance object attached at all.
            var beats = new List<MatchBeat>
            {
                T("Hot Start", BeatControl.Even),
                T("Trash Talk", BeatControl.WrestlerB),
                T("Revenge Spot", BeatControl.WrestlerA),
                T("Clean Victory", BeatControl.WrestlerA),
            };

            double WithFeud(Feud? f)
            {
                double sum = 0;
                for (int i = 0; i < 200; i++)
                    sum += new MatchEngine(i * 977).Execute(new MatchPlan
                    {
                        WrestlerA = a, WrestlerB = b, Feud = f,
                        Beats = beats.Select(x => x.Clone()).ToList()
                    }).StorytellingScore;
                return sum / 200;
            }

            double bare = WithFeud(null), hot = WithFeud(feud);
            output.WriteLine($"  storytelling: no feud {bare:F1}   nuclear feud {hot:F1}");

            Assert.True(hot > bare * 1.10,
                $"Feud beats should draw on feud intensity without a hand-authored resonance: " +
                $"{hot:F1} vs {bare:F1}");
        }
    }
}
