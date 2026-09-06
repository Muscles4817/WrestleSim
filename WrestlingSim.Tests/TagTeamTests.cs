using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Phase 4 of the tag-match build (docs/tag-matches-plan.md).
    ///
    /// A standing team is not the same act as two singles wrestlers on the same side, and
    /// the difference is not in either man's stats. Chemistry does two jobs: it makes
    /// tandem offence work, and it makes the pair read to a crowd as one act, which is
    /// what lets a strong partner carry a weak one.
    /// </summary>
    public class TagTeamTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name, double overness = 75, double charisma = 3.5,
                                  double skill = 3.5) =>
            TestRoster.Make(name, overness: overness, charisma: charisma, skill: skill);

        private static readonly DateOnly Day = new(2025, 6, 1);

        private static TagTeam TeamOf(double chemistry, params Wrestler[] members) => new()
        {
            Name      = string.Join(" & ", members.Select(m => m.RingName)),
            Members   = members.ToList(),
            Formed    = Day,
            Chemistry = chemistry
        };

        // ── Chemistry as a value ─────────────────────────────────────────────

        [Fact]
        public void ChemistryBuildsWithMatchesTogether_AndSaturates()
        {
            var team = TeamOf(0, W("A"), W("B"));

            double after1 = Run(1), after10 = Run(10), after25 = Run(25), after100 = Run(100);

            double Run(int matches)
            {
                var t = TeamOf(0, W("A"), W("B"));
                for (int i = 0; i < matches; i++) t.RecordMatch(Day);
                return t.Chemistry;
            }

            output.WriteLine($"  1:{after1:F3}  10:{after10:F3}  25:{after25:F3}  100:{after100:F3}");

            Assert.True(after10 > after1);
            Assert.True(after25 > after10);
            Assert.True(after100 <= 1.0);
            // The hundredth match is worth far less than the tenth.
            Assert.True(after100 - after25 < after10 - after1);
        }

        [Fact]
        public void ChemistryDecaysWhenATeamStopsTeaming()
        {
            var team = TeamOf(0, W("A"), W("B"));
            for (int i = 0; i < 40; i++) team.RecordMatch(Day);
            double established = team.Chemistry;

            // Inside the grace period nothing moves.
            team.Decay(Day.AddDays(TagTeam.GraceDays));
            Assert.Equal(established, team.Chemistry, 6);

            // Two years apart and they are not the same act any more.
            team.Decay(Day.AddDays(730));
            output.WriteLine($"  established {established:F3} → after two years {team.Chemistry:F3}");

            Assert.True(team.Chemistry < established * 0.8,
                "A team that stops teaming should stop being a team.");
        }

        [Fact]
        public void ASideOfOne_IsTriviallyInSyncWithItself()
        {
            Assert.Equal(1.0, MatchSide.Of(W("Solo")).Chemistry);
        }

        [Fact]
        public void APairWithNoTeam_HasNoChemistry()
        {
            // Two singles wrestlers booked together are exactly that.
            Assert.Equal(0.0, MatchSide.Of(W("A"), W("B")).Chemistry);
        }

        // ── Chemistry in the ring ────────────────────────────────────────────

        [Fact]
        public void ADrilledTeam_HitsABetterDoubleTeamThanStrangers()
        {
            double Technical(double chemistry)
            {
                double total = 0;
                for (int i = 0; i < 150; i++)
                {
                    var a1 = W("A1"); var a2 = W("A2");
                    var side = MatchSide.Of(a1, a2);
                    side.Team = chemistry > 0 ? TeamOf(chemistry, a1, a2) : null;

                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = side,
                        SideB = MatchSide.Of(W("B1"), W("B2")),
                        Beats =
                        [
                            new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                            new MatchBeat { Type = BeatType.DoubleTeam, Control = BeatControl.WrestlerA, Intensity = BeatIntensity.High },
                            new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                        ]
                    }).BeatResults[1].TechnicalContribution;
                }
                return total / 150;
            }

            double strangers = Technical(0.0), drilled = Technical(1.0);
            output.WriteLine($"  double team — strangers {strangers:F2}, drilled {drilled:F2}");

            Assert.True(drilled > strangers * 1.15,
                "An established team should hit tandem offence meaningfully better.");
        }

        [Fact]
        public void AnEstablishedTeam_LetsAStarCarryAWeakPartnerFurther()
        {
            // The mechanic that makes the tag division an elevation tool
            // (docs/wrestling-reference/12-pushes-and-positioning.md §2.2.1). Chemistry
            // lowers the drag, so the side reads closer to its best member.
            double Rating(double chemistry)
            {
                double total = 0;
                for (int i = 0; i < 120; i++)
                {
                    var star   = W("Star",   overness: 95, charisma: 5.0, skill: 4.6);
                    var rookie = W("Rookie", overness: 25, charisma: 1.5, skill: 2.2);
                    var side = MatchSide.Of(star, rookie);
                    side.Team = chemistry > 0 ? TeamOf(chemistry, star, rookie) : null;

                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = side,
                        SideB = MatchSide.Of(W("B1"), W("B2")),
                        Beats = MatchStructureLibrary.Find("Formula Tag")!.Beats
                                    .Select(b => b.Clone()).ToList()
                    }).StarRating;
                }
                return total / 120;
            }

            double thrownTogether = Rating(0.0), realTeam = Rating(1.0);
            output.WriteLine($"  star + rookie — thrown together {thrownTogether:F3}★, real team {realTeam:F3}★");

            Assert.True(realTeam > thrownTogether + 0.1,
                "Making them an actual team should let the star carry the rookie further.");
        }

        [Fact]
        public void ADrilledTeamMiscommunicating_IsABiggerStoryThanStrangersDoingIt()
        {
            double Story(double chemistry)
            {
                double total = 0;
                for (int i = 0; i < 150; i++)
                {
                    var a1 = W("A1"); var a2 = W("A2");
                    var side = MatchSide.Of(a1, a2);
                    side.Team = chemistry > 0 ? TeamOf(chemistry, a1, a2) : null;

                    total += new MatchEngine(i * 7919).Execute(new MatchPlanModel
                    {
                        SideA = side,
                        SideB = MatchSide.Of(W("B1"), W("B2")),
                        Beats =
                        [
                            new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                            new MatchBeat { Type = BeatType.Miscommunication, Control = BeatControl.WrestlerA },
                            new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerB }
                        ]
                    }).BeatResults[1].StorytellingContribution;
                }
                return total / 150;
            }

            double strangers = Story(0.0), drilled = Story(1.0);
            output.WriteLine($"  miscommunication — strangers {strangers:F2}, drilled {drilled:F2}");

            Assert.True(drilled > strangers,
                "A team that moves as one colliding is a departure. Two strangers colliding is Tuesday.");
        }

        [Fact]
        public void SinglesMatchesAreUntouchedByAnyOfThis()
        {
            // A side of one has chemistry 1.0, and TopWeighted returns the single member
            // outright, so the drag term never runs. This pins that.
            var a = W("A", overness: 80, charisma: 4.0, skill: 4.0);
            var b = W("B", overness: 70, charisma: 3.0, skill: 3.5);

            var beats = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList();

            var plain = new MatchEngine(4242).Execute(new MatchPlanModel
            { WrestlerA = a, WrestlerB = b, Beats = beats.Select(x => x.Clone()).ToList() });

            Assert.True(plain.StarRating > 0);
            Assert.Equal(1.0, MatchSide.Of(a).Chemistry);
        }

        // ── World integration ────────────────────────────────────────────────

        [Fact]
        public void RunningAMatchOnACard_BuildsTheTeamsChemistry()
        {
            var a1 = W("Ricky"); var a2 = W("Robert");
            var team = TeamOf(0, a1, a2);

            var career = new Career
            {
                Promotion   = new Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = [a1, a2, W("Bobby"), W("Dennis")],
                Teams       = [team]
            };

            var show = career.Schedule("Saturday Night", Day, ShowType.HouseShow);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = new MatchSide { Members = { a1, a2 }, Team = team },
                    SideB = MatchSide.Of(career.Roster[2], career.Roster[3]),
                    Beats = MatchStructureLibrary.Find("Formula Tag")!.Beats.Select(b => b.Clone()).ToList()
                }
            });

            new ShowSimulator(career.FeudBook).Simulate(show.ToShow());

            Assert.Equal(1, team.MatchesTogether);
            Assert.True(team.Chemistry > 0);
            Assert.Equal(Day, team.LastTeamed);
        }

        [Fact]
        public void TheWorldClock_DecaysATeamThatIsNotBeingUsed()
        {
            var a1 = W("Ricky"); var a2 = W("Robert");
            var team = TeamOf(0, a1, a2);
            for (int i = 0; i < 40; i++) team.RecordMatch(Day);
            double before = team.Chemistry;

            var career = new Career
            {
                Promotion   = new Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = [a1, a2],
                Teams       = [team]
            };

            for (int i = 0; i < 400; i++) career.AdvanceOneDay();

            output.WriteLine($"  {before:F3} → {team.Chemistry:F3} after 400 days idle");
            Assert.True(team.Chemistry < before);
        }

        [Fact]
        public void TeamsRoundTripThroughASave_AndRebindToTheirSide()
        {
            var roster = new List<Wrestler> { W("Ricky"), W("Robert"), W("Bobby"), W("Dennis") };
            var team = TeamOf(0.6, roster[0], roster[1]);
            team.MatchesTogether = 30;
            team.LastTeamed = Day;

            var career = new Career
            {
                Promotion   = new Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = roster,
                Teams       = [team]
            };

            var show = career.Schedule("Saturday Night", Day, ShowType.HouseShow);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = new MatchSide { Members = { roster[0], roster[1] }, Team = team },
                    SideB = MatchSide.Of(roster[2], roster[3]),
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                }
            });

            string json = WrestlingSim.Persistence.SaveSerializer.ToJson(career);
            var fresh = new List<Wrestler> { W("Ricky"), W("Robert"), W("Bobby"), W("Dennis") };
            var loaded = WrestlingSim.Persistence.SaveSerializer.FromJson(json, fresh);

            var loadedTeam = Assert.Single(loaded.Teams);
            Assert.Equal("Ricky & Robert", loadedTeam.Name);
            Assert.Equal(30, loadedTeam.MatchesTogether);
            Assert.Equal(0.6, loadedTeam.Chemistry, 4);

            // And the card's side points at the same instance, not a copy — otherwise
            // running the reloaded show would build chemistry on a team nobody can see.
            var match = Assert.IsType<BookedMatch>(loaded.Shows.Single().Card.Single());
            Assert.Same(loadedTeam, match.Plan.SideA.Team);
        }

        [Fact]
        public void ATeamWhoseMemberHasLeftTheRoster_IsNotLoaded()
        {
            var roster = new List<Wrestler> { W("Stays"), W("Leaves") };
            var team = TeamOf(0.5, roster[0], roster[1]);

            var career = new Career
            {
                Promotion   = new Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = roster,
                Teams       = [team]
            };

            string json = WrestlingSim.Persistence.SaveSerializer.ToJson(career);
            var loaded = WrestlingSim.Persistence.SaveSerializer.FromJson(
                json, new List<Wrestler> { W("Stays") });

            Assert.Empty(loaded.Teams);
        }

        [Fact]
        public void CareerFindsAStandingTeamRegardlessOfOrder()
        {
            var a = W("A"); var b = W("B");
            var team = TeamOf(0.5, a, b);
            var career = new Career
            {
                Promotion   = new Promotion { Name = "P", Tier = PromotionTier.Established },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = [a, b],
                Teams       = [team]
            };

            Assert.Same(team, career.TeamFor(new[] { a, b }));
            Assert.Same(team, career.TeamFor(new[] { b, a }));
            Assert.Null(career.TeamFor(new[] { a }));
            Assert.Null(career.TeamFor(new[] { a, b, W("C") }));
        }
    }
}
