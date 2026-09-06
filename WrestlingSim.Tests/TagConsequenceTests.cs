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
    /// Phase 5 of the tag-match build (docs/tag-matches-plan.md).
    ///
    /// A tag match now costs and pays something distinct from a singles match. The load-
    /// bearing claim is the asymmetry: the man who ate the fall takes it in full and his
    /// partner takes a share, so having the other guy take the pin is a real, costed way to
    /// protect somebody — which is the most common use a tag match is put to
    /// (docs/wrestling-reference/12-pushes-and-positioning.md §6.1).
    /// </summary>
    public class TagConsequenceTests(ITestOutputHelper output)
    {
        private static readonly DateOnly Day = new(2025, 6, 1);

        private static Wrestler W(string name, double overness = 70) =>
            TestRoster.Make(name, overness: overness);

        // ── The heat split ───────────────────────────────────────────────────

        [Fact]
        public void TheManWhoAteTheFall_LosesMoreThanHisPartner()
        {
            var winA = W("Win A", 70); var winB = W("Win B", 70);
            var pinned = W("Pinned", 70); var partner = W("Protected", 70);

            var outcome = HeatEconomy.ForSides(
                [winA, winB], winA, [pinned, partner], pinned,
                starRating: 3.5, finish: FinishWeight.Decisive);

            var pinnedChange   = outcome.All.Single(c => c.Wrestler == pinned);
            var partnerChange  = outcome.All.Single(c => c.Wrestler == partner);

            output.WriteLine($"  pinned    {pinnedChange.OvernessDelta:+0.000;-0.000} overness");
            output.WriteLine($"  protected {partnerChange.OvernessDelta:+0.000;-0.000} overness");

            Assert.True(pinnedChange.OvernessDelta < 0);
            Assert.True(partnerChange.OvernessDelta < 0, "Being on the losing team still costs something.");
            Assert.True(partnerChange.OvernessDelta > pinnedChange.OvernessDelta,
                "The man who was pinned has to lose more than the man who was not.");
        }

        [Fact]
        public void ProtectingSomeoneByHavingHisPartnerTakeTheFall_ActuallyProtectsHim()
        {
            // The booking lever this whole method exists for.
            var star = W("Star", 90);
            var partner = W("Partner", 60);
            var winA = W("Win A", 75); var winB = W("Win B", 75);

            var starPinned = HeatEconomy.ForSides(
                [winA, winB], winA, [star, partner], star,
                starRating: 3.5, finish: FinishWeight.Decisive);

            var partnerPinned = HeatEconomy.ForSides(
                [winA, winB], winA, [star, partner], partner,
                starRating: 3.5, finish: FinishWeight.Decisive);

            double starLossWhenPinned    = starPinned.All.Single(c => c.Wrestler == star).OvernessDelta;
            double starLossWhenProtected = partnerPinned.All.Single(c => c.Wrestler == star).OvernessDelta;

            output.WriteLine($"  star takes the fall himself: {starLossWhenPinned:+0.000;-0.000}");
            output.WriteLine($"  partner takes it instead:    {starLossWhenProtected:+0.000;-0.000}");

            Assert.True(starLossWhenProtected > starLossWhenPinned,
                "Putting the fall on the partner has to cost the star less.");
            Assert.True(starLossWhenProtected < 0,
                "But it must not be free, or it is not a decision.");
        }

        [Fact]
        public void ThePinnerGainsMoreThanHisPartner()
        {
            var pinner = W("Pinner", 70); var partner = W("Partner", 70);

            var outcome = HeatEconomy.ForSides(
                [pinner, partner], pinner, [W("L1", 85), W("L2", 85)], W("L1", 85),
                starRating: 4.0, finish: FinishWeight.Decisive);

            double pinnerGain  = outcome.All.Single(c => c.Wrestler == pinner).OvernessDelta;
            double partnerGain = outcome.Partners!.Single(c => c.Wrestler == partner).OvernessDelta;

            output.WriteLine($"  pinner {pinnerGain:+0.000}  partner {partnerGain:+0.000}");

            Assert.True(pinnerGain > partnerGain);
            Assert.True(partnerGain > 0, "Being on the winning team is worth something.");
        }

        [Fact]
        public void BeatingATeamOfMidcarders_IsNotTheSameStatementAsBeatingOneMainEventer()
        {
            var winner = W("Winner", 60);

            var overMainEventer = HeatEconomy.ForMatch(
                winner, W("Main Eventer", 95), 3.5, FinishWeight.Decisive);

            var overTwoMidcarders = HeatEconomy.ForSides(
                [winner, W("Partner", 60)], winner,
                [W("Mid A", 55), W("Mid B", 55)], W("Mid A", 55),
                3.5, FinishWeight.Decisive);

            double vsStar = overMainEventer.Winner.OvernessDelta;
            double vsTeam = overTwoMidcarders.Winner.OvernessDelta;

            output.WriteLine($"  beating a 95 main-eventer {vsStar:+0.000}");
            output.WriteLine($"  beating two 55 mid-carders {vsTeam:+0.000}");

            Assert.True(vsStar > vsTeam,
                "You can only take status from people who have it.");
        }

        [Fact]
        public void ASideReadsTowardItsBestMember_NotItsAverage()
        {
            var star   = W("Star", 95);
            var jobber = W("Jobber", 20);

            double side     = HeatEconomy.SideStanding([star, jobber]);
            double midpoint = (star.EffectiveOverness + jobber.EffectiveOverness) / 2.0;

            output.WriteLine($"  side reads {side:F1}, midpoint would be {midpoint:F1}");

            Assert.True(side > midpoint);
            Assert.True(side < star.EffectiveOverness);
        }

        // ── Side-keyed feuds ─────────────────────────────────────────────────

        [Fact]
        public void ATeamRivalry_IsItsOwnFeud_NotOneOfTheSinglesFeudsInsideIt()
        {
            var book = new FeudBook();
            var a1 = W("A1"); var a2 = W("A2"); var b1 = W("B1"); var b2 = W("B2");

            var teamFeud    = book.GetOrCreate([a1, a2], [b1, b2]);
            var singlesFeud = book.GetOrCreate(a1, b1);

            Assert.NotSame(teamFeud, singlesFeud);
            Assert.True(teamFeud.IsTeamFeud);
            Assert.False(singlesFeud.IsTeamFeud);
            Assert.Equal("A1 & A2", teamFeud.SideAName);
        }

        [Fact]
        public void ATeamFeudIsFoundRegardlessOfBillingOrHomeAdvantage()
        {
            var book = new FeudBook();
            var a1 = W("A1"); var a2 = W("A2"); var b1 = W("B1"); var b2 = W("B2");

            var first = book.GetOrCreate([a1, a2], [b1, b2]);

            Assert.Same(first, book.GetOrCreate([a2, a1], [b1, b2]));
            Assert.Same(first, book.GetOrCreate([b2, b1], [a1, a2]));
            Assert.Same(first, book.Find([a1, a2], [b2, b1]));
        }

        [Fact]
        public void ATagMatch_BuildsTheTeamFeudInFullAndTheSinglesFeudsAtAFraction()
        {
            var a1 = W("A1"); var a2 = W("A2"); var b1 = W("B1"); var b2 = W("B2");
            var career = NewCareer([a1, a2, b1, b2]);
            var show = career.Schedule("Saturday", Day, ShowType.HouseShow);

            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(a1, a2),
                    SideB = MatchSide.Of(b1, b2),
                    Beats = MatchStructureLibrary.Find("Formula Tag")!.Beats
                                .Select(b => b.Clone()).ToList()
                }
            });

            new ShowSimulator(career.FeudBook).Simulate(show.ToShow());

            var teamFeud  = career.FeudBook.Find([a1, a2], [b1, b2])!;
            var crossFeud = career.FeudBook.Find(a1, b1)!;

            output.WriteLine($"  team feud {teamFeud.Heat:F2} heat, cross-pair {crossFeud.Heat:F2}");

            Assert.True(teamFeud.Heat > 0);
            Assert.True(crossFeud.Heat > 0, "A tag programme builds the singles rivalries inside it.");
            Assert.True(crossFeud.Heat < teamFeud.Heat,
                "But the team rivalry is the one being booked.");
        }

        [Fact]
        public void TeamFamiliarityWearsOutSeparatelyFromTheSinglesPairings()
        {
            var a1 = W("A1"); var a2 = W("A2"); var b1 = W("B1"); var b2 = W("B2");
            var book = new FeudBook();

            var teamFeud = book.GetOrCreate([a1, a2], [b1, b2]);
            for (int i = 0; i < 4; i++) teamFeud.RecordMatch(Day);

            var singles = book.GetOrCreate(a1, b1);

            output.WriteLine($"  team familiarity {teamFeud.Familiarity(Day):F3}, " +
                             $"singles {singles.Familiarity(Day):F3}");

            Assert.True(teamFeud.Familiarity(Day) < 0.9,
                "Four team matches should have worn the pairing out.");
            Assert.Equal(1.0, singles.Familiarity(Day), 3);
        }

        // ── Tag titles ───────────────────────────────────────────────────────

        [Fact]
        public void ATagTitle_IsHeldAndLostJointly()
        {
            var c1 = W("Champ A", 80); var c2 = W("Champ B", 80);
            var n1 = W("New A", 75);   var n2 = W("New B", 75);

            var title = new Title
            {
                Name = "World Tag Team Championship", Tier = TitleTier.Secondary,
                SideSize = 2, Established = Day, Standing = 50
            };
            title.Lineage.Add(new TitleReign
            {
                Champions = [c1, c2], ReignNumber = 1, Won = Day.AddDays(-200)
            });

            Assert.True(title.IsHeldBy(c1));
            Assert.True(title.IsHeldBy(c2));
            Assert.Equal("Champ A & Champ B", title.CurrentReign!.ChampionName);

            var update = TitleEconomy.ResolveTitleMatch(
                title, [n1, n2], [c1, c2], n1, c2,
                FinishWeight.Decisive, 4.0, Day, "Saturday Night");

            output.WriteLine($"  {update.Reason}");

            Assert.Equal(TitleEvent.Changed, update.Event);
            Assert.True(title.IsHeldBy(n1));
            Assert.True(title.IsHeldBy(n2));
            Assert.False(title.IsHeldBy(c1));
            Assert.False(title.IsHeldBy(c2));

            // Both new champions get the status bonus — they won it together.
            Assert.NotNull(update.StatusBonus);
            Assert.Single(update.PartnerBonuses);
            Assert.Equal(n2, update.PartnerBonuses[0].Wrestler);
        }

        [Fact]
        public void ATagTitleRetains_WhenEitherChampionsSideWins()
        {
            var c1 = W("Champ A", 80); var c2 = W("Champ B", 80);
            var title = new Title { Name = "Tag Titles", SideSize = 2, Established = Day, Standing = 50 };
            title.Lineage.Add(new TitleReign { Champions = [c1, c2], ReignNumber = 1, Won = Day.AddDays(-100) });

            // The champions win, but the fall is scored by the partner who was not legal
            // at the opening bell. It is still a retention.
            var update = TitleEconomy.ResolveTitleMatch(
                title, [c1, c2], [W("X", 70), W("Y", 70)], c2, W("X", 70),
                FinishWeight.Decisive, 3.5, Day);

            Assert.Equal(TitleEvent.Retained, update.Event);
            Assert.True(title.IsHeldBy(c1));
        }

        [Fact]
        public void ATagTitle_CannotBeDefendedInASinglesMatch_AndViceVersa()
        {
            var c1 = W("Champ A"); var c2 = W("Champ B");
            var tagTitle = new Title { Name = "Tag Titles", SideSize = 2, Established = Day };
            tagTitle.Lineage.Add(new TitleReign { Champions = [c1, c2], ReignNumber = 1, Won = Day });

            var singlesPlan = new MatchPlanModel
            {
                WrestlerA = c1,
                WrestlerB = W("Challenger"),
                TitleAtStake = tagTitle,
                Beats =
                [
                    new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                    new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                ]
            };

            Assert.Contains(singlesPlan.Validate(),
                e => e.Contains("cannot be defended in a singles match")
                  || e.Contains("is not in this match"));

            var singlesTitle = new Title { Name = "World Title", SideSize = 1, Established = Day };
            var tagPlan = new MatchPlanModel
            {
                SideA = MatchSide.Of(W("A1"), W("A2")),
                SideB = MatchSide.Of(W("B1"), W("B2")),
                TitleAtStake = singlesTitle,
                Beats =
                [
                    new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                    new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                ]
            };

            Assert.Contains(tagPlan.Validate(),
                e => e.Contains("cannot be defended in a tag match"));
        }

        [Fact]
        public void TagTitlesAndFeudsRoundTripThroughASave()
        {
            var a1 = W("A1"); var a2 = W("A2"); var b1 = W("B1"); var b2 = W("B2");
            var career = NewCareer([a1, a2, b1, b2]);

            var title = career.Titles.Create("World Tag Team Championship", TitleTier.Secondary,
                                             Division.Mens, Day);
            title.SideSize = 2;
            title.Lineage.Add(new TitleReign { Champions = [a1, a2], ReignNumber = 1, Won = Day });

            career.FeudBook.Record([a1, a2], [b1, b2], 20);

            string json = SaveSerializer.ToJson(career);
            var fresh = new List<Wrestler> { W("A1"), W("A2"), W("B1"), W("B2") };
            var loaded = SaveSerializer.FromJson(json, fresh);

            var loadedTitle = loaded.Titles.Active.Single(t => t.Name.Contains("Tag"));
            Assert.Equal(2, loadedTitle.SideSize);
            Assert.Equal(2, loadedTitle.Champions.Count);
            Assert.Equal("A1 & A2", loadedTitle.CurrentReign!.ChampionName);

            var loadedFeud = loaded.FeudBook.AllIncludingDormant
                .Single(f => f.IsTeamFeud);
            Assert.Equal("A1 & A2", loadedFeud.SideAName);
            Assert.Equal(20, loadedFeud.Heat, 3);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Career NewCareer(List<Wrestler> roster) => new()
        {
            Promotion   = new Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
            StartDate   = Day,
            CurrentDate = Day,
            Roster      = roster
        };
    }
}
