using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Trios — three a side.
    ///
    /// The point of these is what they *do not* test. Three a side is still two sides, so
    /// the face-in-peril structure, the hot-tag charge, the heat split and the feud keying
    /// all apply unchanged (docs/wrestling-reference/18-match-craft.md §2.5). The sides
    /// abstraction from phase 1 bought trios without anyone asking for them — a 3v3 plan
    /// validated and executed correctly before a line of trios code was written.
    ///
    /// What did need doing was everything that had quietly assumed a side has at most two
    /// people: commentary that names "his partner", a beat called All Four In, a UI with a
    /// boolean toggle, and no structures to book.
    /// </summary>
    public class TriosTests(ITestOutputHelper output)
    {
        private const int Seed = 20260906;

        private static Wrestler W(string name, double overness = 75, double charisma = 3.5,
                                  double skill = 3.5) =>
            TestRoster.Make(name, overness: overness, charisma: charisma, skill: skill);

        private static MatchSide Trio(string a, string b, string c) =>
            MatchSide.Of(W(a), W(b), W(c));

        private static MatchBeat B(BeatType type, BeatControl control) =>
            new() { Type = type, Control = control };

        // ── It works at all ──────────────────────────────────────────────────

        [Fact]
        public void A3v3Plan_ValidatesAndRuns()
        {
            var plan = new MatchPlanModel
            {
                SideA = Trio("A1", "A2", "A3"),
                SideB = Trio("B1", "B2", "B3"),
                Beats = MatchStructureLibrary.Find("Six-Man War")!.Beats.Select(b => b.Clone()).ToList()
            };

            Assert.Empty(plan.Validate());

            var result = new MatchEngine(Seed).Execute(plan);
            output.WriteLine($"  {result.StarDisplay}, {result.Pinner.RingName} scored the fall");

            Assert.Equal(3, result.WinningSide.Count);
            Assert.Equal(3, result.LosingSide.Count);
            Assert.InRange(result.StarRating, 0.0, 5.0);
        }

        [Fact]
        public void ATagCanNameWhichOfTwoPartnersComesIn()
        {
            // The whole reason `IncomingIndex` exists. On a two-man side "the next man
            // round" is unambiguous; on a trio it is a choice, and the booker makes it.
            var a1 = W("Starts"); var a2 = W("Second"); var a3 = W("Third");

            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(a1, a2, a3),
                SideB = Trio("B1", "B2", "B3"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,    BeatControl.WrestlerB),
                    B(BeatType.Isolation, BeatControl.WrestlerB),
                    new MatchBeat { Type = BeatType.HotTag, Control = BeatControl.WrestlerA, IncomingIndex = 2 },
                    B(BeatType.FinishClean, BeatControl.WrestlerA),
                ]
            };

            var result = new MatchEngine(Seed).Execute(plan);

            Assert.Same(a3, result.Pinner);
        }

        [Fact]
        public void WithoutAnExplicitIndex_ATagGoesToTheNextManRound()
        {
            var a1 = W("First"); var a2 = W("Second"); var a3 = W("Third");

            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(a1, a2, a3),
                SideB = Trio("B1", "B2", "B3"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.Cutoff,    BeatControl.WrestlerB),
                    B(BeatType.Isolation, BeatControl.WrestlerB),
                    B(BeatType.HotTag,    BeatControl.WrestlerA),
                    B(BeatType.FinishClean, BeatControl.WrestlerA),
                ]
            };

            Assert.Same(a2, new MatchEngine(Seed).Execute(plan).Pinner);
        }

        [Fact]
        public void UnevenSidesAreStillRejected_ThreeAgainstTwoIncluded()
        {
            var plan = new MatchPlanModel
            {
                SideA = Trio("A1", "A2", "A3"),
                SideB = MatchSide.Of(W("B1"), W("B2")),
                Beats = [B(BeatType.StandardOpening, BeatControl.Even),
                         B(BeatType.FinishClean, BeatControl.WrestlerA)]
            };

            Assert.Contains(plan.Validate(), e => e.Contains("Sides are uneven"));
        }

        // ── The formula is unchanged ─────────────────────────────────────────

        [Fact]
        public void TheHotTagChargeWorksTheSameWayWithThreeASide()
        {
            // Not a new rule for trios — the same rule, reached through a bigger side.
            double Pop(int isolations)
            {
                double total = 0;
                for (int i = 0; i < 120; i++)
                {
                    var beats = new List<MatchBeat>
                    {
                        B(BeatType.StandardOpening, BeatControl.Even),
                        new MatchBeat { Type = BeatType.Cutoff, Control = BeatControl.WrestlerB, Duration = BeatDuration.Short }
                    };
                    for (int k = 0; k < isolations; k++)
                        beats.Add(new MatchBeat { Type = BeatType.Isolation, Control = BeatControl.WrestlerB, Duration = BeatDuration.Medium });
                    beats.Add(new MatchBeat { Type = BeatType.HotTag, Control = BeatControl.WrestlerA, Intensity = BeatIntensity.High, Duration = BeatDuration.Short });
                    beats.Add(B(BeatType.FinishClean, BeatControl.WrestlerA));

                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = Trio("A1", "A2", "A3"),
                        SideB = Trio("B1", "B2", "B3"),
                        Beats = beats
                    }).BeatResults.First(x => x.BeatType == BeatType.HotTag).CrowdEnergyDelta;
                }
                return total / 120;
            }

            double cold = Pop(0), earned = Pop(3);
            output.WriteLine($"  cold {cold:F2}, three isolations {earned:F2}");

            Assert.True(earned > cold * 1.8,
                "The charge has to buy the same thing in a trios match that it does in a tag.");
        }

        [Fact]
        public void ASideOfThree_ReadsTowardItsBestMember()
        {
            // Top-weighting generalises: one weak man in three dilutes less than one in two,
            // which is the right shape — the more people the crowd has to look at, the less
            // any single one of them defines the side.
            var star = W("Star", overness: 95, charisma: 5.0, skill: 4.6);
            var mid  = W("Mid",  overness: 60, charisma: 3.0, skill: 3.5);
            var weak = W("Weak", overness: 20, charisma: 1.0, skill: 2.0);

            double trio = HeatEconomy.SideStanding([star, mid, weak]);
            double mean = new[] { star, mid, weak }.Average(w => w.EffectiveOverness);

            output.WriteLine($"  trio reads {trio:F1}, flat mean would be {mean:F1}");

            Assert.True(trio > mean);
            Assert.True(trio < star.EffectiveOverness);
        }

        // ── What had to change ───────────────────────────────────────────────

        [Fact]
        public void TheBreakdownBeatCountsThePeopleActuallyInTheRing()
        {
            // It was called "All Four In" and said so out loud, which is wrong the moment
            // six are in there. The enum member keeps its name — it is what gets written
            // into save files — but nothing the player reads says four any more.
            var plan = new MatchPlanModel
            {
                SideA = Trio("A1", "A2", "A3"),
                SideB = Trio("B1", "B2", "B3"),
                Beats =
                [
                    B(BeatType.StandardOpening, BeatControl.Even),
                    B(BeatType.AllFourBrawl, BeatControl.Even),
                    B(BeatType.FinishClean, BeatControl.WrestlerA),
                ]
            };

            var commentary = string.Join(" ",
                new MatchEngine(Seed).Execute(plan).BeatResults[1].Commentary);

            output.WriteLine($"  {commentary}");
            Assert.DoesNotContain("four", commentary, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CommentaryDoesNotNameOnePartnerAndIgnoreTheOther()
        {
            // The near-tag line named `PartnersOf(x).First()`, which in a trio silently
            // picks one of two and reads as a mistake rather than as shorthand.
            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("Peril"), W("Corner One"), W("Corner Two")),
                SideB = Trio("B1", "B2", "B3"),
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

            var nearTag = new MatchEngine(Seed).Execute(plan)
                .BeatResults.First(b => b.BeatType == BeatType.NearTag);
            var line = string.Join(" ", nearTag.Commentary);

            output.WriteLine($"  {line}");

            // Either it names nobody in particular, or — if it does name someone — it must
            // not single out one of the two men on the apron.
            bool namesOne = line.Contains("Corner One") ^ line.Contains("Corner Two");
            Assert.False(namesOne,
                "Naming one of two partners and ignoring the other reads as an error.");
        }

        [Fact]
        public void EveryTriosStructure_IsBookableForThreeASideAndNotForTwo()
        {
            foreach (var st in MatchStructureLibrary.ForSideSize(3))
            {
                var trios = new MatchPlanModel
                {
                    SideA = Trio("A1", "A2", "A3"),
                    SideB = Trio("B1", "B2", "B3"),
                    Beats = st.Beats.Select(b => b.Clone()).ToList()
                };
                Assert.Empty(trios.Validate());

                var result = new MatchEngine(Seed).Execute(trios);
                output.WriteLine($"  {st.Name,-16} {result.StarDisplay}");
                Assert.InRange(result.StarRating, 0.0, 5.0);

                // A trios preset in a singles match is not bookable.
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
        public void TheSixManWar_OutRatesTheLuchaSprint_BecauseItPaysSomethingOff()
        {
            double Rate(string structure)
            {
                var st = MatchStructureLibrary.Find(structure)!;
                double total = 0;
                for (int i = 0; i < 100; i++)
                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = Trio("A1", "A2", "A3"),
                        SideB = Trio("B1", "B2", "B3"),
                        Beats = st.Beats.Select(b => b.Clone()).ToList()
                    }).StarRating;
                return total / 100;
            }

            double war = Rate("Six-Man War"), lucha = Rate("Lucha Trios");
            output.WriteLine($"  Six-Man War {war:F3}★   Lucha Trios {lucha:F3}★");

            Assert.True(war > lucha,
                "The full formula should beat the sprint that never puts anyone in peril.");
        }

        // ── The rest of the world ────────────────────────────────────────────

        [Fact]
        public void ATriosMatchRoundTripsThroughASave()
        {
            var roster = new List<Wrestler>
            {
                W("A1"), W("A2"), W("A3"), W("B1"), W("B2"), W("B3")
            };
            var career = new Career
            {
                Promotion   = new Promotion { Name = "CMLL", Tier = PromotionTier.Established },
                StartDate   = new DateOnly(2025, 6, 1),
                CurrentDate = new DateOnly(2025, 6, 1),
                Roster      = roster
            };

            var show = career.Schedule("Viernes", career.CurrentDate, ShowType.HouseShow);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(roster[0], roster[1], roster[2]),
                    SideB = MatchSide.Of(roster[3], roster[4], roster[5]),
                    Beats = MatchStructureLibrary.Find("Six-Man War")!.Beats
                                .Select(b => b.Clone()).ToList()
                },
                StructureName = "Six-Man War"
            });

            var fresh = roster.Select(w => W(w.RingName)).ToList();
            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), fresh);
            var match = Assert.IsType<BookedMatch>(loaded.Shows.Single().Card.Single());

            Assert.Equal(3, match.Plan.SideA.Size);
            Assert.Equal(3, match.Plan.SideB.Size);
            Assert.Empty(match.Plan.Validate());
        }

        [Fact]
        public void ATriosFeudIsKeyedOnAllSixPeople()
        {
            var book = new FeudBook();
            var a = new List<Wrestler> { W("A1"), W("A2"), W("A3") };
            var b = new List<Wrestler> { W("B1"), W("B2"), W("B3") };

            var trios = book.GetOrCreate(a, b);
            // Any subset is a different rivalry.
            var tag = book.GetOrCreate(a.Take(2).ToList(), b.Take(2).ToList());

            Assert.NotSame(trios, tag);
            Assert.Equal("A1 & A2 & A3", trios.SideAName);

            // And billing order still does not matter.
            Assert.Same(trios, book.Find(a.AsEnumerable().Reverse().ToList(), b));
        }

        [Fact]
        public void ASixManTitleCanBeContestedAndChangesHandsAsAUnit()
        {
            var day = new DateOnly(2025, 6, 1);
            var c = new List<Wrestler> { W("C1"), W("C2"), W("C3") };
            var n = new List<Wrestler> { W("N1"), W("N2"), W("N3") };

            var title = new Title
            {
                Name = "Trios Championship", Tier = TitleTier.Secondary,
                SideSize = 3, Established = day, Standing = 50
            };
            title.Lineage.Add(new TitleReign { Champions = c.ToList(), ReignNumber = 1, Won = day.AddDays(-90) });

            var update = TitleEconomy.ResolveTitleMatch(
                title, n, c, n[0], c[2], FinishWeight.Decisive, 3.5, day, "Viernes");

            output.WriteLine($"  {update.Reason}");

            Assert.Equal(TitleEvent.Changed, update.Event);
            Assert.All(n, w => Assert.True(title.IsHeldBy(w)));
            Assert.All(c, w => Assert.False(title.IsHeldBy(w)));

            // All three new champions are paid, not just the one who scored the fall.
            Assert.Equal(2, update.PartnerBonuses.Count);
        }

        [Fact]
        public void TheHeatSplitDistributesAcrossAllThreePartners()
        {
            var win = new List<Wrestler> { W("W1", 70), W("W2", 70), W("W3", 70) };
            var los = new List<Wrestler> { W("L1", 70), W("L2", 70), W("L3", 70) };

            var outcome = HeatEconomy.ForSides(win, win[0], los, los[0], 3.5, FinishWeight.Decisive);

            Assert.Equal(4, outcome.Partners!.Count);   // two per side
            Assert.All(outcome.All, c => Assert.NotEqual(0, c.OvernessDelta));

            double pinner  = outcome.Winner.OvernessDelta;
            double partner = outcome.Partners.First(c => c.Wrestler == win[1]).OvernessDelta;

            output.WriteLine($"  pinner {pinner:+0.000}, each partner {partner:+0.000}");
            Assert.True(pinner > partner);
        }
    }
}
