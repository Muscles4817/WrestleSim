using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Characters, step 1 of docs/gimmick-plan.md: a character that wears and rests, the crowd's
    /// reading beside the booking, and the gimmick fields that did nothing either living or gone.
    /// </summary>
    public class CharacterTests
    {
        private readonly ITestOutputHelper output;
        public CharacterTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2025, 1, 6);

        private static Career NewCareer(IEnumerable<Wrestler> roster)
        {
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Test Wrestling", Tier = PromotionTier.Established },
                StartDate   = Day,
                CurrentDate = Day,
                Roster      = roster.ToList()
            };
            career.OpenBooks(Day);
            return career;
        }

        private static BookedMatch Match(Wrestler a, Wrestler b) => new()
        {
            Plan = new MatchPlan
            {
                WrestlerA = a,
                WrestlerB = b,
                Beats     = MatchStructureLibrary.Find("TV Formula")!.Beats.Select(x => x.Clone()).ToList()
            },
            StructureName = "TV Formula"
        };

        private static ShowResult Week(Career career, params BookedMatch[] matches)
        {
            var show = career.Schedule("Weekly", career.CurrentDate, ShowType.Television);
            show.RuntimeMinutes = 30;
            show.Card.AddRange(matches);
            var result = career.RunShow(show)!;
            for (int d = 0; d < 7; d++) career.AdvanceOneDay();
            return result;
        }

        private static Wrestler With(string name, GimmickDurability durability, double freshness = 0.8)
        {
            var w = TestRoster.Make(name);
            w.Gimmick.Durability = durability;
            w.Gimmick.Freshness = freshness;
            return w;
        }

        // ── It costs nothing until it is worn ────────────────────────────────

        [Fact]
        public void AFreshOrEstablishedCharacterIsUnmoved_AndAWornOneFalls()
        {
            Assert.Equal(1.0, CharacterFreshness.Factor(1.0));
            Assert.Equal(1.0, CharacterFreshness.Factor(CharacterFreshness.Worn));
            Assert.Equal(CharacterFreshness.FloorFactor, CharacterFreshness.Factor(0.0), 10);

            double last = 1.0;
            for (double f = CharacterFreshness.Worn; f >= 0; f -= 0.05)
            {
                double now = CharacterFreshness.Factor(f);
                Assert.True(now <= last, $"the factor rose as the character wore, at {f:F2}");
                last = now;
            }
        }

        /// <summary>
        /// Day one of a save draws exactly what it drew before characters wore — so neither
        /// `TypicalInvestment` nor the box-office medians move — and that rests on this: nobody
        /// shipped or generated starts below the line.
        /// </summary>
        [Fact]
        public void NobodyStartsWorn()
        {
            var shipped = DataLoaders.LoadEmbeddedWrestlers();
            var worn = shipped.Where(w => w.Gimmick.Freshness < CharacterFreshness.Worn).Select(w => w.RingName).ToList();
            Assert.True(worn.Count == 0, "shipped worn: " + string.Join(", ", worn));

            var career = NewCareer(DataLoaders.LoadEmbeddedWrestlers());
            var choice = new WorldChoice(WorldSize.Small, WorldSource.Generated, StableSeed.From("characters"));
            World.Populate(career, choice, DataLoaders.LoadEmbeddedWrestlers());
            var generated = career.Roster.Where(w => w.IsGenerated).ToList();
            Assert.NotEmpty(generated);
            Assert.All(generated, w => Assert.True(w.Gimmick.Freshness >= CharacterFreshness.Worn, w.RingName));
        }

        [Fact]
        public void AWornCharacterConnectsLess_AnEstablishedOneDoesNot()
        {
            var w = TestRoster.Make("Worn Walter");
            w.Gimmick.Freshness = 0.9;
            var fresh = new PerformerProfile(w);
            w.Gimmick.Freshness = 0.5;
            var established = new PerformerProfile(w);
            w.Gimmick.Freshness = 0.1;
            var spent = new PerformerProfile(w);

            Assert.Equal(fresh.Disposition, established.Disposition, 10);
            Assert.True(spent.Disposition < established.Disposition);
        }

        // ── Wear ─────────────────────────────────────────────────────────────

        [Fact]
        public void TheSameMatchEveryWeek_WearsAShortTermCharacterFastestAndALongTermOneSlowest()
        {
            var shortA = With("Short A", GimmickDurability.ShortTerm);
            var shortB = With("Short B", GimmickDurability.ShortTerm);
            var midA   = With("Mid A",   GimmickDurability.MediumTerm);
            var midB   = With("Mid B",   GimmickDurability.MediumTerm);
            var longA  = With("Long A",  GimmickDurability.LongTerm);
            var longB  = With("Long B",  GimmickDurability.LongTerm);
            var career = NewCareer([shortA, shortB, midA, midB, longA, longB]);

            for (int week = 0; week < 20; week++)
                Week(career, Match(shortA, shortB), Match(midA, midB), Match(longA, longB));

            output.WriteLine($"after 20 weeks: short {shortA.Gimmick.Freshness:F3}, medium {midA.Gimmick.Freshness:F3}, long {longA.Gimmick.Freshness:F3}");
            Assert.True(shortA.Gimmick.Freshness < midA.Gimmick.Freshness);
            Assert.True(midA.Gimmick.Freshness < longA.Gimmick.Freshness);
            Assert.True(longA.Gimmick.Freshness < 0.8, "even a long-term character wears with nothing new");
            Assert.True(shortA.Gimmick.Freshness < CharacterFreshness.Worn,
                "five months of the same match on television should wear a short-term character out");
        }

        [Fact]
        public void AStoryThatMoves_WearsTheCharacterLess()
        {
            var a = With("Samey", GimmickDurability.MediumTerm);
            var b = With("Moving", GimmickDurability.MediumTerm);
            CharacterFreshness.Wear(a, ShowType.Television, novelty: 0);
            CharacterFreshness.Wear(b, ShowType.Television, novelty: 1);
            Assert.True(0.8 - b.Gimmick.Freshness < 0.8 - a.Gimmick.Freshness);
            Assert.True(b.Gimmick.Freshness < 0.8, "something new slows the wear; it does not stop it");
        }

        [Fact]
        public void AHouseShowWearsLessThanTelevision()
        {
            var tv = With("TV", GimmickDurability.MediumTerm);
            var road = With("Road", GimmickDurability.MediumTerm);
            CharacterFreshness.Wear(tv, ShowType.Television, 0);
            CharacterFreshness.Wear(road, ShowType.HouseShow, 0);
            Assert.True(road.Gimmick.Freshness > tv.Gimmick.Freshness);
        }

        [Fact]
        public void WearingOutIsInTheShowReport()
        {
            var a = With("Nearly Done", GimmickDurability.ShortTerm, freshness: CharacterFreshness.Worn + 0.005);
            var b = With("Opponent", GimmickDurability.ShortTerm);
            var career = NewCareer([a, b]);

            var result = Week(career, Match(a, b));
            Assert.Contains(result.Audience!.Notes, n => n.StartsWith("Nearly Done: the character is wearing thin"));
        }

        // ── Rest ─────────────────────────────────────────────────────────────

        [Fact]
        public void TimeAwayRestoresTheCharacter_ButAMissedWeekDoesNot_AndAReturnIsNotADebut()
        {
            var w = With("Rested", GimmickDurability.MediumTerm, freshness: 0.3);
            w.LastAppearance = Day;

            CharacterFreshness.Rest([w], Day.AddDays(CharacterFreshness.GraceDays), CharacterFreshness.GraceDays);
            Assert.Equal(0.3, w.Gimmick.Freshness, 10);

            CharacterFreshness.Rest([w], Day.AddDays(CharacterFreshness.GraceDays + 120), 120);
            Assert.Equal(CharacterFreshness.RestCeiling - (CharacterFreshness.RestCeiling - 0.3) * 0.5, w.Gimmick.Freshness, 6);

            CharacterFreshness.Rest([w], Day.AddDays(5000), 5000);
            Assert.True(w.Gimmick.Freshness <= CharacterFreshness.RestCeiling + 1e-9);
        }

        /// <summary>The clock jumps when a show is run early; a jump must rest exactly as the days would.</summary>
        [Fact]
        public void RestIsTheSameInOneStepOrDayByDay()
        {
            var stepped = With("Stepped", GimmickDurability.MediumTerm, freshness: 0.3);
            var daily   = With("Daily",   GimmickDurability.MediumTerm, freshness: 0.3);
            stepped.LastAppearance = daily.LastAppearance = Day;

            CharacterFreshness.Rest([stepped], Day.AddDays(100), 100);
            for (int d = 1; d <= 100; d++) CharacterFreshness.Rest([daily], Day.AddDays(d), 1);

            Assert.Equal(stepped.Gimmick.Freshness, daily.Gimmick.Freshness, 9);
        }

        [Fact]
        public void TheCareerClockRestsPeopleOffScreen()
        {
            var w = With("Away", GimmickDurability.MediumTerm, freshness: 0.3);
            w.LastAppearance = Day;
            var career = NewCareer([w]);
            for (int d = 0; d < 120; d++) Assert.True(career.AdvanceOneDay());
            Assert.True(w.Gimmick.Freshness > 0.5, $"four months away left it at {w.Gimmick.Freshness:F3}");
        }

        [Fact]
        public void TheSaveCarriesHowWornACharacterIs()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = NewCareer(roster);
            var who = career.Roster[0];
            who.Gimmick.Freshness = 0.2345;

            var reloaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            Assert.Equal(0.2345, reloaded.FindWrestler(who.Id)!.Gimmick.Freshness, 4);
        }

        // ── The crowd's reading ──────────────────────────────────────────────

        [Fact]
        public void TheCrowdsReadingIsShownBesideTheBooking_AndOnlyFacesAndHeelsCross()
        {
            var adored = TestRoster.Make("Cool Heel", overness: 97, charisma: 5.0);
            adored.Gimmick.NaturalAlignment = Alignment.Heel;
            adored.Gimmick.AppealRatings.ForEach(a => a.AppealScore = 0.95);
            var reading = CrowdReading.Of(adored);
            Assert.Equal(CrowdReading.Reaction.Cheering, reading);
            Assert.True(CrowdReading.Crossed(Alignment.Heel, reading));
            Assert.NotNull(CrowdReading.Meaning(Alignment.Heel, reading));

            Assert.False(CrowdReading.Crossed(Alignment.Tweener, CrowdReading.Reaction.Cheering));
            Assert.False(CrowdReading.Crossed(Alignment.Tweener, CrowdReading.Reaction.Booing));
            Assert.False(CrowdReading.Crossed(Alignment.Face, CrowdReading.Reaction.Cheering));
            Assert.Null(CrowdReading.Meaning(Alignment.Face, CrowdReading.Reaction.Cheering));
        }

        /// <summary>Doc 31 §2: some shipped heels read as cheered. The reading has to see them.</summary>
        [Fact]
        public void SomeShippedHeelsReadAsCheered()
        {
            var heels = DataLoaders.LoadEmbeddedWrestlers().Where(w => w.Gimmick.NaturalAlignment == Alignment.Heel).ToList();
            var cheered = heels.Where(w => CrowdReading.Of(w) == CrowdReading.Reaction.Cheering).Select(w => w.RingName).ToList();
            output.WriteLine($"{cheered.Count} of {heels.Count} heels cheered: {string.Join(", ", cheered)}");
            Assert.NotEmpty(cheered);
            Assert.True(cheered.Count < heels.Count / 2, "most heels should draw heat");
        }

        // ── A new character ──────────────────────────────────────────────────

        [Fact]
        public void ChangingGimmick_MakesTheNewOneCurrent_AndKeepsTheOldInTheHistory()
        {
            var w = TestRoster.Make("Old Name");
            var old = w.Gimmick;
            w.ChangeGimmick(new Gimmick("New Name"));

            Assert.Equal("New Name", w.RingName);
            Assert.Same(old, Assert.Single(w.PreviousGimmicks));
            Assert.Contains("Old Name", w.PreviousNames());
        }
    }
}
