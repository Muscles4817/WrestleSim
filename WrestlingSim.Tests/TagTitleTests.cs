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
    /// Tag titles as a finished feature rather than a model that happens to hold two names.
    ///
    /// Phase 5 introduced joint reigns and taught <see cref="TitleEconomy.ResolveTitleMatch"/>
    /// about sides. What it did not do was revisit every other place the title economy reads
    /// "the champion" — the daily drift, the non-title-loss rule, the reign lookup and four
    /// display sites all still meant <c>Champions[0]</c>. On a singles belt that is the same
    /// thing; on a tag belt it is the wrong man half the time.
    /// </summary>
    public class TagTitleTests(ITestOutputHelper output)
    {
        private static readonly DateOnly Day = new(2025, 6, 1);

        private static Wrestler W(string name, double overness = 70) =>
            TestRoster.Make(name, overness: overness);

        private static Title TagBelt(string name, DateOnly established, double standing = 50) => new()
        {
            Name        = name,
            Tier        = TitleTier.Secondary,
            SideSize    = 2,
            Established = established,
            Standing    = standing
        };

        private static Career NewCareer(List<Wrestler> roster) => new()
        {
            Promotion   = new Promotion { Name = "Mid-South", Tier = PromotionTier.Established },
            StartDate   = Day,
            CurrentDate = Day,
            Roster      = roster
        };

        // ── The non-title loss ───────────────────────────────────────────────

        [Fact]
        public void ANonTitleLoss_NamesAndPricesTheHolderWhoActuallyLost()
        {
            // It named Champions[0] whoever had really been beaten, so a tag result read
            // "Ricky lost to X" when Robert took the fall — and priced the penalty against
            // the wrong man's standing.
            var ricky  = W("Ricky", 80);
            var robert = W("Robert", 40);
            var title  = TagBelt("World Tag Team Championship", Day);
            title.Lineage.Add(new TitleReign { Champions = [ricky, robert], ReignNumber = 1, Won = Day });

            var update = TitleEconomy.ApplyNonTitleLoss(title, robert, W("Nobody", 30), FinishWeight.Decisive);

            output.WriteLine($"  {update.Reason}");

            Assert.Contains("Robert", update.Reason);
            Assert.DoesNotContain("Ricky", update.Reason);
            Assert.Equal(robert, update.Champion);
        }

        [Fact]
        public void TheNonTitleLossPenaltyIsPricedAgainstTheManWhoLost()
        {
            // Robert at 40 losing to a 30 is barely a story; Ricky at 80 losing to the same
            // man is the belt looking like a technicality. The two must not cost the same.
            var ricky  = W("Ricky", 80);
            var robert = W("Robert", 40);
            var jobber = W("Nobody", 30);

            double Cost(Wrestler beaten)
            {
                var title = TagBelt("Tag Titles", Day);
                title.Lineage.Add(new TitleReign { Champions = [ricky, robert], ReignNumber = 1, Won = Day });
                double before = title.Standing;
                TitleEconomy.ApplyNonTitleLoss(title, beaten, jobber, FinishWeight.Decisive);
                return before - title.Standing;
            }

            double rickyCost = Cost(ricky), robertCost = Cost(robert);
            output.WriteLine($"  Ricky (80) loses: -{rickyCost:F2}   Robert (40) loses: -{robertCost:F2}");

            Assert.True(rickyCost > robertCost,
                "The bigger man losing to a nobody should cost the belt more.");
        }

        [Fact]
        public void AShowChargesTheNonTitleLossToWhoeverTookTheFall()
        {
            // End to end, through ShowSimulator: the belt is not on the line, and the man
            // who eats the pin is the second-listed champion.
            var ricky  = W("Ricky", 80);
            var robert = W("Robert", 80);
            var career = NewCareer([ricky, robert, W("Bobby", 75), W("Dennis", 75)]);

            var title = TagBelt("World Tag Team Championship", Day);
            career.Titles.Add(title);
            title.Lineage.Add(new TitleReign { Champions = [ricky, robert], ReignNumber = 1, Won = Day.AddDays(-100) });

            var show = career.Schedule("Saturday", Day, ShowType.HouseShow);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    // Robert starts, so he is the one legal at a finish with no tags in it.
                    SideA = new MatchSide { Members = { ricky, robert }, StartingIndex = 1 },
                    SideB = MatchSide.Of(career.Roster[2], career.Roster[3]),
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerB }
                    ]
                }
            });

            var result = new ShowSimulator(career.FeudBook, titles: career.Titles).Simulate(show.ToShow());
            var update = Assert.Single(result.TitleUpdates.Where(u => u.Event == TitleEvent.NonTitleLoss));

            output.WriteLine($"  {update.Reason}");
            Assert.Equal(robert, update.Champion);
        }

        // ── Daily drift ──────────────────────────────────────────────────────

        [Fact]
        public void TheBeltIsPulledByTheTeam_NotByWhoeverIsListedFirst()
        {
            // ApplyDailyDrift read reign.Champion.EffectiveOverness, so swapping the billing
            // order of the same two champions changed what the belt was worth over time.
            double AfterAYear(Wrestler first, Wrestler second)
            {
                var title = TagBelt("Tag Titles", Day, standing: 40);
                title.Lineage.Add(new TitleReign
                {
                    Champions = [first, second], ReignNumber = 1,
                    Won = Day, LastDefended = Day
                });

                for (int i = 1; i <= 30; i++)
                    TitleEconomy.ApplyDailyDrift(title, Day.AddDays(i));

                return title.Standing;
            }

            var star   = W("Star", 92);
            var rookie = W("Rookie", 30);

            double starFirst   = AfterAYear(star, rookie);
            double rookieFirst = AfterAYear(rookie, star);

            output.WriteLine($"  star billed first {starFirst:F3}, rookie billed first {rookieFirst:F3}");

            Assert.Equal(starFirst, rookieFirst, 6);
        }

        [Fact]
        public void AStrongTeamPullsTheBeltUp_AWeakOnePullsItDown()
        {
            double AfterAMonth(double a, double b)
            {
                var title = TagBelt("Tag Titles", Day, standing: 50);
                title.Lineage.Add(new TitleReign
                {
                    Champions = [W("A", a), W("B", b)], ReignNumber = 1,
                    Won = Day, LastDefended = Day
                });
                for (int i = 1; i <= 30; i++) TitleEconomy.ApplyDailyDrift(title, Day.AddDays(i));
                return title.Standing;
            }

            double strong = AfterAMonth(92, 88), weak = AfterAMonth(25, 20);
            output.WriteLine($"  strong champions {strong:F2}, weak champions {weak:F2}");

            Assert.True(strong > weak,
                "Who is carrying the belt has to matter to what it is worth.");
        }

        // ── Lineage ──────────────────────────────────────────────────────────

        [Fact]
        public void BothHalvesOfATeamGetCreditForTheReign()
        {
            // ReignsFor matched on Champion == w, so the second-listed man's own title
            // history did not exist as far as the game was concerned.
            var ricky  = W("Ricky");
            var robert = W("Robert");
            var title  = TagBelt("Tag Titles", Day);
            title.Lineage.Add(new TitleReign { Champions = [ricky, robert], ReignNumber = 1, Won = Day });

            Assert.Single(title.ReignsOf(ricky));
            Assert.Single(title.ReignsOf(robert));
            Assert.Empty(title.ReignsOf(W("Somebody Else")));
        }

        [Fact]
        public void ARegistryFindsABeltHeldByEitherHalfOfATeam()
        {
            var ricky  = W("Ricky");
            var robert = W("Robert");
            var registry = new TitleRegistry();
            var title = registry.Add(TagBelt("Tag Titles", Day));
            title.Lineage.Add(new TitleReign { Champions = [ricky, robert], ReignNumber = 1, Won = Day });

            Assert.Single(registry.HeldBy(ricky));
            Assert.Single(registry.HeldBy(robert));
            Assert.True(registry.IsChampion(robert));
        }

        [Fact]
        public void VacatingATagBelt_NamesBothChampions()
        {
            var title = TagBelt("Tag Titles", Day);
            title.Lineage.Add(new TitleReign
            {
                Champions = [W("Ricky"), W("Robert")], ReignNumber = 1, Won = Day.AddDays(-50)
            });

            var update = TitleEconomy.Vacate(title, Day, "Robert is injured");

            output.WriteLine($"  {update.Reason}");
            Assert.Contains("Ricky & Robert", update.Reason);
            Assert.True(title.IsVacant);
        }

        // ── End to end ───────────────────────────────────────────────────────

        [Fact]
        public void ATagTitleChangesHandsOnAShowCard_AndBothNewChampionsAreCredited()
        {
            var c1 = W("Champ A", 78); var c2 = W("Champ B", 78);
            var n1 = W("New A", 72);   var n2 = W("New B", 72);
            var career = NewCareer([c1, c2, n1, n2]);

            var title = TagBelt("World Tag Team Championship", Day, standing: 55);
            career.Titles.Add(title);
            title.Lineage.Add(new TitleReign
            {
                Champions = [c1, c2], ReignNumber = 1, Won = Day.AddDays(-120), LastDefended = Day.AddDays(-20)
            });

            var show = career.Schedule("Saturday", Day, ShowType.HouseShow);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    SideA = MatchSide.Of(n1, n2),
                    SideB = MatchSide.Of(c1, c2),
                    TitleAtStake = title,
                    Beats = MatchStructureLibrary.Find("Formula Tag")!.Beats
                                .Select(b => b.Clone()).ToList()
                },
                StructureName = "Formula Tag"
            });

            var result = new ShowSimulator(career.FeudBook, titles: career.Titles).Simulate(show.ToShow());
            var update = Assert.Single(result.TitleUpdates);

            output.WriteLine($"  {update.Reason}");
            foreach (var change in result.StatusChanges.Where(c => c.Reason.Contains("Tag") || c.Reason.Contains("champion")))
                output.WriteLine($"    {change.Wrestler.RingName}: {change.Reason}");

            Assert.Equal(TitleEvent.Changed, update.Event);
            Assert.True(title.IsHeldBy(n1));
            Assert.True(title.IsHeldBy(n2));
            Assert.Equal("New A & New B", title.CurrentReign!.ChampionName);

            // Both new champions are paid, not just the one who scored the fall.
            Assert.NotNull(update.StatusBonus);
            Assert.Single(update.PartnerBonuses);
        }

        [Fact]
        public void ATagTitleSurvivesASaveWithBothHoldersAndItsSideSize()
        {
            var c1 = W("Champ A"); var c2 = W("Champ B");
            var career = NewCareer([c1, c2]);

            var title = career.Titles.Create("World Tag Team Championship", TitleTier.Secondary,
                                             Division.Mens, Day, sideSize: 2);
            title.Lineage.Add(new TitleReign { Champions = [c1, c2], ReignNumber = 1, Won = Day });

            var loaded = SaveSerializer.FromJson(
                SaveSerializer.ToJson(career),
                new List<Wrestler> { W("Champ A"), W("Champ B") });

            var reloaded = loaded.Titles.Active.Single(t => t.IsTagTitle);
            Assert.Equal(2, reloaded.SideSize);
            Assert.Equal("Champ A & Champ B", reloaded.CurrentReign!.ChampionName);
            Assert.Same(loaded.Roster.Single(w => w.RingName == "Champ B"), reloaded.Champions[1]);
        }

        [Fact]
        public void ATagBeltIsIntroducedThroughTheSameRegistryAsAnyOther_AndDilutesTheRest()
        {
            // A tag belt is not a special kind of object. It claims the same finite
            // attention as every other title (doc 21 §2.1), which is exactly why it is not
            // seeded by default — introducing one has to be the player's decision and has
            // to cost them.
            var registry = new TitleRegistry();
            registry.SeedDefaults("Test", Day);

            double before = registry.Active.Sum(t => t.Prestige);
            var singlesPrestigeBefore = registry.Active.ToDictionary(t => t.Id, t => t.Prestige);

            var tag = registry.Create("World Tag Team Championship", TitleTier.Secondary,
                                      Division.Mens, Day, sideSize: 2);

            output.WriteLine($"  before {before:F1} across {singlesPrestigeBefore.Count} belts, " +
                             $"after {registry.Active.Sum(t => t.Prestige):F1} across {registry.Active.Count}");

            Assert.True(tag.IsTagTitle);
            Assert.All(singlesPrestigeBefore, kv =>
                Assert.True(registry.Active.Single(t => t.Id == kv.Key).Prestige < kv.Value,
                    "Adding a tag belt has to cost every other belt something."));
        }
    }
}
