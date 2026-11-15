using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Parts 4 and 5: one set of consequences for every show, and a rival booker who chooses
    /// results. Mechanisms and comparisons, not fates.
    /// </summary>
    public class BookerTests
    {
        private static readonly DateOnly Day = new(2026, 1, 5);

        private static Career World4()
        {
            var career = new Career
            {
                Promotion = new Promotion { Name = "Test", Tier = PromotionTier.Local },
                StartDate = Day, CurrentDate = Day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            Growth.Begin(career);
            career.OpenBooks(Day);
            career.Titles.SeedDefaults(career.Promotion.Name, Day);
            World.Open(career, Day);
            return career;
        }

        private static BookedMatch Singles(Wrestler a, Wrestler b, Title? title = null) => new()
        {
            Plan = new MatchPlan { Sides = [MatchSide.Of(a), MatchSide.Of(b)], TitleAtStake = title }
        };

        [Fact]
        public void ARivalNightHasTheSameConsequencesAsThePlayers()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            var before = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!)
                .ToDictionary(w => w.Id, w => (w.Overness, w.Fatigue));

            for (int d = 0; d < 10 && rival.Nights.Count == 0; d++) career.AdvanceOneDay();
            Assert.NotEmpty(rival.Nights);

            var worked = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!)
                .Where(w => w.LastAppearance == rival.Nights[^1].Date).ToList();
            Assert.NotEmpty(worked);
            Assert.Contains(worked, w => w.Overness != before[w.Id].Overness);
            Assert.Contains(worked, w => w.Fatigue > before[w.Id].Fatigue);
            Assert.Contains(worked, w => w.Development!.Matches > 0);
            Assert.True(rival.Feuds.AllIncludingDormant.Any(), "Every match-up is remembered in the rival's own book.");
        }

        [Fact]
        public void TheSideWorthMoreWins_AndAPushIsWorthMore()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            rival.Competence = 1.0;
            var people = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).OrderByDescending(w => w.Overness).ToList();
            var star = people[0];
            var lesser = people.First(w => star.Overness - w.Overness is > 5 and < 15);

            var m = Singles(lesser, star);
            Assert.Equal(1, World.Winner(rival, m, ShowType.Television, new Random(1)));

            rival.Direction = new BookingDirection { Pushes = [lesser.Id] };
            Assert.Equal(0, World.Winner(rival, m, ShowType.Television, new Random(1)));
        }

        [Fact]
        public void APoorBookerMisjudgesNearEquals_NotAStarAgainstANobody()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            rival.Competence = 0.4;
            var star = TestRoster.Make("Star", overness: 90);
            var nobody = TestRoster.Make("Nobody", overness: 20);
            var peer = TestRoster.Make("Peer", overness: 88);

            var rng = new Random(5);
            Assert.All(Enumerable.Range(0, 200), _ => Assert.Equal(0, World.Winner(rival, Singles(star, nobody), ShowType.Television, rng)));
            int upsets = Enumerable.Range(0, 200).Count(_ => World.Winner(rival, Singles(star, peer), ShowType.Television, rng) == 1);
            Assert.InRange(upsets, 10, 190);
        }

        [Fact]
        public void TheBeltGoesWhereTheDirectionSays_OnABigNight()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            var title = rival.Titles.Active.First(t => t.SideSize == 1);
            var people = rival.Contracts.Select(c => career.FindWrestler(c.WrestlerId)!).Where(w => w.Division == title.Division).ToList();
            var champ = people[0];
            var next = people[1];
            title.Lineage.Add(new TitleReign { Champions = [champ], ReignNumber = 1, Won = Day.AddDays(-200), WonAt = "Test" });

            World.Direct(career, rival);
            Assert.True(rival.Direction!.NextChampion.ContainsKey(title.Name), "A reign of two hundred days is due to end.");
            rival.Direction.NextChampion[title.Name] = next.Id;

            var m = Singles(champ, next, title);
            Assert.Equal(1, World.Winner(rival, m, ShowType.PremiumEvent, new Random(1)));
            Assert.Equal(0, World.Winner(rival, m, ShowType.Television, new Random(1)));
        }

        [Fact]
        public void ADirectionBuildsPeopleWhoAreNotAlreadyMade()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            var d = World.Direct(career, rival);

            Assert.NotEmpty(d.Pushes);
            Assert.All(d.Pushes.Select(id => career.FindWrestler(id)!), w =>
            {
                Assert.True(w.Overness < 75);
                Assert.False(w.IsPartTime);
            });
            var kept = d.Pushes.ToList();
            Assert.Equal(kept, World.Direct(career, rival).Pushes);
        }

        [Fact]
        public void WinningEverythingInFrontOfACrowdThatHasNotTakenToYouTurnsThemAgainstYou()
        {
            var w = TestRoster.Make("Forced", overness: 40, charisma: 2.0);
            for (int i = 0; i < 10; i++) HeatEconomy.Track(w, won: true, reaction: -10);
            Assert.True(HeatEconomy.Rejecting(w));

            var seen = HeatEconomy.ForExposure(w, 80, 12, 50, talking: false);
            Assert.True(seen.MomentumDelta < 0, "Being shown cools somebody the crowd is refusing.");

            var liked = TestRoster.Make("Liked", overness: 40, charisma: 2.0);
            for (int i = 0; i < 10; i++) HeatEconomy.Track(liked, won: true, reaction: +8);
            Assert.False(HeatEconomy.Rejecting(liked), "Winning a lot is fine if the crowd is with them.");
        }

        [Fact]
        public void ARivalDropsAPushTheCrowdRefused()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            var d = World.Direct(career, rival);
            var refused = career.FindWrestler(d.Pushes[0])!;
            refused.RejectedSince = Day;

            World.Direct(career, rival);

            Assert.DoesNotContain(refused.Id, rival.Direction!.Pushes);
            Assert.Contains(refused.Id, rival.Direction.Rejected.Keys);
            Assert.Contains(career.News, n => n.Text.Contains(refused.RingName));
        }

        [Fact]
        public void TheBookersPlansAndTheCrowdsMoodComeBackFromASave()
        {
            var career = World4();
            var rival = career.Rivals.First(r => r.Promotion.HasTelevision);
            World.Direct(career, rival);
            var w = career.Roster[3];
            w.Favoured = 0.9; w.Warmth = -4; w.RejectedSince = Day;

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            var r2 = back.Rivals.First(r => r.Id == rival.Id);

            Assert.Equal(rival.Direction!.Pushes, r2.Direction!.Pushes);
            Assert.Equal(rival.Competence, r2.Competence);
            var w2 = back.FindWrestler(w.Id)!;
            Assert.Equal((0.9, -4.0, (DateOnly?)Day), (w2.Favoured, w2.Warmth, w2.RejectedSince));
        }

        [Fact]
        public void ASuggestedCardIsWrittenInFull_FromPeopleThePlayerCanBook_AndChangesNothingUntilKept()
        {
            var career = World4();
            var show = career.Schedule("Tuesday", Day.AddDays(3), ShowType.HouseShow);
            string before = SaveSerializer.ToJson(career);

            var card = World.SuggestCard(career, show);

            Assert.NotEmpty(card);
            Assert.All(card.OfType<BookedMatch>(), m => Assert.True(m.IsComplete, "A suggested match has its beats written."));
            var bookable = career.PlayerCanBook().ToHashSet();
            Assert.All(card.SelectMany(i => i.Wrestlers), w => Assert.Contains(w, bookable));
            Assert.Equal(before, SaveSerializer.ToJson(career));

            show.Card.AddRange(card);
            Assert.Empty(career.UnavailableOnCard(show));
        }
    }
}
