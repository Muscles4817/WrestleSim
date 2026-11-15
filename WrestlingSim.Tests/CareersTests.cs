using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Part 3: arriving and leaving. Each test compares two people, or follows one change
    /// through, rather than asserting who retires when.
    /// </summary>
    public class CareersTests
    {
        private static readonly DateOnly Day = new(2026, 1, 5);

        private static Career Classic()
        {
            var career = new Career
            {
                Promotion = new Promotion { Name = "Test", Tier = PromotionTier.National },
                StartDate = Day, CurrentDate = Day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            Growth.Begin(career);
            career.OpenBooks(Day);
            career.Titles.SeedDefaults(career.Promotion.Name, Day);
            World.Open(career, Day);
            return career;
        }

        private static Wrestler Named(Career c, string ring) => c.Roster.First(w => w.RingName == ring);

        [Fact]
        public void OlderIsLikelierToRetire_OtherThingsEqual()
        {
            var c = Classic();
            var w = Named(c, "Randy Orton");
            double now = Careers.RetirementChance(c, w).Chance;
            w.BornIn -= 6;
            Assert.True(Careers.RetirementChance(c, w).Chance > now);
        }

        [Fact]
        public void BeingWantedAtTheTopKeepsPeopleGoing()
        {
            var c = Classic();
            var w = Named(c, "Randy Orton");
            Assert.True(c.IsGuaranteed(w) || c.SignedTo(w) is not null);
            double wanted = Careers.RetirementChance(c, w).Chance;
            w.Overness = 40;
            Assert.True(Careers.RetirementChance(c, w).Chance > wanted);
        }

        [Fact]
        public void InjuriesAndNowhereToWorkPushTowardTheDoor()
        {
            var c = Classic();
            var w = Named(c, "Kofi Kingston");
            double before = Careers.RetirementChance(c, w).Chance;

            w.InjuryHistory.Add(new Injury { Part = BodyPart.Concussion, Sustained = Day.AddYears(-2), ClearedOn = Day.AddYears(-1), WeeksOut = 30 });
            double hurt = Careers.RetirementChance(c, w).Chance;
            c.CurrentDate = Day.AddMonths(14);
            double idle = Careers.RetirementChance(c, w).Chance;

            Assert.True(hurt > before);
            Assert.True(idle > hurt);
            Assert.Contains(Careers.RetirementChance(c, w).Why, r => r.Contains("no match"));
        }

        [Fact]
        public void ARetirementEndsTheDealVacatesTheBeltAndKeepsTheName()
        {
            var c = Classic();
            var mine = c.Contracts.Select(k => c.FindWrestler(k.WrestlerId)!).First();
            var title = c.Titles.Active.First(t => t.SideSize == 1 && t.Division == mine.Division);
            title.Lineage.Add(new TitleReign { Champions = [mine], ReignNumber = title.Lineage.Count + 1, Won = Day, WonAt = "Test" });

            Careers.Retire(c, mine);

            Assert.Null(c.ContractOf(mine));
            Assert.Null(title.Champion);
            Assert.Contains(mine, c.Roster);
            Assert.DoesNotContain(mine, c.PlayerCanBook());
            Assert.Contains("retired", c.Unavailable(mine));
            Assert.Contains(c.News, n => n.Text.Contains(mine.RingName) && n.Text.Contains("retired"));
        }

        [Fact]
        public void ARivalLosesItsRetiredWrestlerToo()
        {
            var c = Classic();
            var rival = c.Rivals.First(r => r.Contracts.Any(k => k.IsGuaranteed));
            var theirs = c.FindWrestler(rival.Contracts.First(k => k.IsGuaranteed).WrestlerId)!;

            Careers.Retire(c, theirs);

            Assert.Null(rival.ContractOf(theirs));
            Assert.Null(c.SignedTo(theirs));
            Assert.DoesNotContain(theirs.Id, rival.Favourites);
        }

        [Fact]
        public void APartTimerWorksBigShowsOnly_ForThePlayerAsForAnybody()
        {
            var c = Classic();
            var w = c.Contracts.Select(k => c.FindWrestler(k.WrestlerId)!).First();
            Careers.GoPartTime(c, w);
            var other = c.Contracts.Select(k => c.FindWrestler(k.WrestlerId)!).First(x => x != w && x.Division == w.Division);

            ScheduledShow Show(ShowType type)
            {
                var show = new ScheduledShow { Date = Day.AddDays(3), Type = type, Name = "Test" };
                show.Card.Add(Factories.SegmentFactory.CreateConfrontation(w, other, "", ""));
                return show;
            }

            Assert.Contains(c.UnavailableOnCard(Show(ShowType.Television)), x => x.Wrestler == w);
            Assert.DoesNotContain(c.UnavailableOnCard(Show(ShowType.PremiumEvent)), x => x.Wrestler == w);
        }

        [Fact]
        public void TheClassRefillsTheWorldToItsSize()
        {
            var c = Classic();
            int size = c.Roster.Count;
            c.PopulationTarget = size;
            foreach (var w in c.Roster.Take(7).ToList()) Careers.Retire(c, w);
            c.CurrentDate = new DateOnly(2027, 1, 1);

            var made = Careers.Intake(c);

            Assert.Equal(7, made.Count);
            Assert.Equal(size, c.Roster.Count(w => !w.IsRetired));
            Assert.All(made, w =>
            {
                Assert.True(w.IsGenerated);
                Assert.Equal(2027, w.DebutedIn);
                Assert.InRange(w.AgeOn(c.CurrentDate)!.Value, 19, 25);
                Assert.True(w.Overness < 15, "Nobody has heard of a rookie.");
                Assert.NotNull(w.Development);
            });
        }

        [Fact]
        public void TheSameWorldMakesTheSameClass()
        {
            Wrestler[] Make()
            {
                var c = Classic();
                c.PopulationTarget = c.Roster.Count + 5;
                c.CurrentDate = new DateOnly(2027, 1, 1);
                return Careers.Intake(c).ToArray();
            }
            Assert.Equal(Make().Select(w => (w.Id, w.Overness)), Make().Select(w => (w.Id, w.Overness)));
        }

        [Fact]
        public void RetirementsAndRookiesComeBackFromASave()
        {
            var c = Classic();
            var gone = c.Roster.First(w => c.ContractOf(w) is null && c.SignedTo(w) is null);
            Careers.Retire(c, gone);
            var part = c.Roster.First(w => !w.IsRetired);
            Careers.GoPartTime(c, part);
            c.PopulationTarget = c.Roster.Count + 3;
            c.CurrentDate = new DateOnly(2027, 1, 1);
            var made = Careers.Intake(c);

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(c), DataLoaders.LoadEmbeddedWrestlers());

            Assert.Equal(gone.RetiredOn, back.FindWrestler(gone.Id)!.RetiredOn);
            Assert.True(back.FindWrestler(part.Id)!.IsPartTime);
            Assert.All(made, w => Assert.NotNull(back.FindWrestler(w.Id)));
            Assert.Equal(c.PopulationTarget, back.PopulationTarget);
        }

        [Fact]
        public void NobodyRetiredIsSignedBookedOrDeveloped()
        {
            var c = Classic();
            var w = c.Roster.First(x => c.ContractOf(x) is null && c.SignedTo(x) is null);
            Careers.Retire(c, w);
            int marks = w.Development!.History.Count;

            c.CurrentDate = new DateOnly(2026, 2, 1);
            Growth.Month(c);

            Assert.Equal(marks, w.Development.History.Count);
            Assert.DoesNotContain(w, World.FreeAgents(c));
        }
    }
}
