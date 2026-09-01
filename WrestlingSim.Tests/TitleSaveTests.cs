using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Championships across a save. Same hazard as everywhere else in the save format: a
    /// champion serialised by value would come back as a copy, and every prestige and
    /// overness change after that would land on an object nothing else can see.
    /// </summary>
    public class TitleSaveTests
    {
        private static readonly DateOnly Start = new(2025, 1, 6);

        private static List<Wrestler> Roster() =>
        [
            TestRoster.Make("Alpha One", overness: 80),
            TestRoster.Make("Beta Two", overness: 60),
            TestRoster.Make("Gamma Three", overness: 40)
        ];

        private static Career NewCareer(List<Wrestler> roster) => new()
        {
            Promotion   = new Promotion { Name = "Gold Standard Wrestling", Tier = PromotionTier.National },
            StartDate   = Start,
            CurrentDate = Start,
            Roster      = roster
        };

        private static Career RoundTrip(Career career) =>
            SaveSerializer.FromJson(SaveSerializer.ToJson(career), Roster());

        private static Title WithLineage(Career career, List<Wrestler> roster)
        {
            var title = career.Titles.Create("World Championship", TitleTier.World, Division.Mens, Start);
            title.Standing = 71.5;

            title.Lineage.Add(new TitleReign
            {
                Champion     = roster[2],
                ReignNumber  = 1,
                Won          = Start,
                Lost         = Start.AddDays(120),
                WonAt        = "Founding Show",
                LostAt       = "Spring Classic",
                Defences     = 3,
                LastDefended = Start.AddDays(90)
            });

            title.Lineage.Add(new TitleReign
            {
                Champion     = roster[0],
                ReignNumber  = 2,
                Won          = Start.AddDays(120),
                WonAt        = "Spring Classic",
                Defences     = 5,
                LastDefended = Start.AddDays(300)
            });

            return title;
        }

        [Fact]
        public void RoundTripKeepsTheWholeSlate()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            career.Titles.SeedDefaults("GSW", Start);

            var loaded = RoundTrip(career);

            Assert.Equal(3, loaded.Titles.Active.Count);
            Assert.Equal(career.Titles.Demand, loaded.Titles.Demand, 3);
            Assert.Equal(career.Titles.Dilution, loaded.Titles.Dilution, 3);
        }

        [Fact]
        public void RoundTripKeepsPrestigeAndLineage()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            WithLineage(career, roster);

            var loaded = RoundTrip(career);
            var title = loaded.Titles.Active.Single();

            Assert.Equal(71.5, title.Standing, 3);
            Assert.Equal(2, title.ReignCount);
            Assert.Equal("Founding Show", title.Lineage[0].WonAt);
            Assert.Equal("Spring Classic", title.Lineage[0].LostAt);
            Assert.Equal(3, title.Lineage[0].Defences);
            Assert.Equal(120, title.Lineage[0].DaysHeld(Start.AddDays(999)));
            Assert.Equal(5, title.CurrentReign!.Defences);
            Assert.Equal(Start.AddDays(300), title.CurrentReign.LastDefended);
            Assert.Equal(Start.AddDays(120), title.CurrentReign.Won);
        }

        [Fact]
        public void LoadedChampionsAreTheSameInstancesTheRosterHolds()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            WithLineage(career, roster);

            var loaded = RoundTrip(career);
            var title = loaded.Titles.Active.Single();

            // The whole point: a reign's champion must BE the roster's wrestler, or a
            // prestige-driven status bonus applies to an object nothing else can see.
            foreach (var reign in title.Lineage)
                Assert.Same(loaded.FindWrestler(reign.Champion.Id), reign.Champion);

            Assert.Same(loaded.FindWrestler("alpha-one"), title.Champion);
        }

        [Fact]
        public void RoundTripKeepsABeltOnTheLineInABookedMatch()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            var title = WithLineage(career, roster);

            var show = career.Schedule("Weekly", Start.AddDays(7), ShowType.Television);
            show.Card.Add(new BookedMatch
            {
                Plan = new MatchPlanModel
                {
                    WrestlerA    = roster[0],
                    WrestlerB    = roster[1],
                    TitleAtStake = title,
                    Beats =
                    [
                        new MatchBeat { Type = BeatType.StandardOpening, Control = BeatControl.Even },
                        new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                    ]
                }
            });

            var loaded = RoundTrip(career);
            var match = (BookedMatch)loaded.Shows.Single().Card.Single();

            Assert.NotNull(match.Plan.TitleAtStake);

            // Rebound to the registry's live instance, not a second copy of the belt —
            // otherwise running the show would move a title nothing else can see.
            Assert.Same(loaded.Titles.Find(title.Id), match.Plan.TitleAtStake);
        }

        [Fact]
        public void RoundTripKeepsARetiredBeltAndItsHistory()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            var title = WithLineage(career, roster);
            career.Titles.Retire(title, Start.AddDays(400));

            var loaded = RoundTrip(career);
            var restored = loaded.Titles.All.Single();

            Assert.True(restored.Retired);
            Assert.Equal(Start.AddDays(400), restored.RetiredOn);
            Assert.Equal(2, restored.ReignCount);
            Assert.Empty(loaded.Titles.Active);
        }

        [Fact]
        public void AReignWhoseChampionLeftTheRosterIsDroppedNotResurrected()
        {
            var roster = Roster();
            var career = NewCareer(roster);
            WithLineage(career, roster);

            string json = SaveSerializer.ToJson(career);
            var trimmed = Roster().Where(w => w.Id != "gamma-three").ToList();

            var loaded = SaveSerializer.FromJson(json, trimmed);
            var title = loaded.Titles.Active.Single();

            Assert.Single(title.Lineage);

            // ReignNumber is stored rather than recomputed, so what is left still reads as
            // the second champion — the history is thinner, not renumbered.
            Assert.Equal(2, title.CurrentReign!.ReignNumber);
        }

        [Fact]
        public void ASaveFromBeforeChampionshipsGetsTheStandardSlate()
        {
            // A v1 save, written before titles existed. The alternative to seeding is a
            // promotion with no belts at all, which the game does not otherwise allow.
            var roster = Roster();
            string json = SaveSerializer.ToJson(NewCareer(roster))
                .Replace($"\"Version\":{SaveGame.CurrentVersion}", "\"Version\":1");

            var loaded = SaveSerializer.FromJson(json, Roster());

            Assert.Equal(3, loaded.Titles.Active.Count);
            Assert.Equal(1.0, loaded.Titles.Dilution, 3);
        }

        [Fact]
        public void ACurrentSaveWithNoTitlesStaysWithoutThem()
        {
            // Deliberately retiring and deleting every belt is a legal state, and a reload
            // must not helpfully put them back.
            var roster = Roster();
            var career = NewCareer(roster);

            var loaded = RoundTrip(career);

            Assert.Empty(loaded.Titles.All);
        }
    }
}
