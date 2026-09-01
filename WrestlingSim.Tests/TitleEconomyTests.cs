using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The prestige economy from docs/wrestling-reference/21-championships.md §3, §4 and §5.
    ///
    /// A title is worth exactly what the audience believes about it, so every one of these
    /// asserts a stated rule rather than an implementation detail: long credible reigns
    /// build, churn and non-title losses destroy, and a protected finish cannot move a belt.
    /// </summary>
    public class TitleEconomyTests
    {
        private static readonly DateOnly Day0 = new(2025, 1, 6);

        private static Title Belt(double standing = 60, TitleTier tier = TitleTier.World) => new()
        {
            Name        = "World Championship",
            Tier        = tier,
            Established = Day0,
            Standing    = standing
        };

        private static Title BeltHeldBy(Wrestler champion, double standing = 60, DateOnly? since = null)
        {
            var title = Belt(standing);
            title.Lineage.Add(new TitleReign
            {
                Champion    = champion,
                ReignNumber = 1,
                Won         = since ?? Day0
            });
            return title;
        }

        // ── The decisive-finish rule ─────────────────────────────────────────

        [Theory]
        [InlineData(BeatType.FinishClean, true)]
        [InlineData(BeatType.FinishSubmission, true)]
        [InlineData(BeatType.FinishSuperFinisher, true)]
        [InlineData(BeatType.FinishRollup, true)]
        [InlineData(BeatType.FinishDQ, false)]
        [InlineData(BeatType.FinishCountout, false)]
        [InlineData(BeatType.FinishInterference, false)]
        public void TitlesDoNotChangeHandsOnAProtectedFinish(BeatType finish, bool expected) =>
            Assert.Equal(expected, TitleEconomy.ChangesHands(HeatEconomy.WeightOf(finish)));

        [Fact]
        public void ADisqualificationLeavesTheBeltWithTheChampion()
        {
            var champion  = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 70);
            var title = BeltHeldBy(champion);

            var update = TitleEconomy.ResolveTitleMatch(
                title, winner: challenger, loser: champion,
                FinishWeight.Protected, starRating: 4.0, Day0.AddDays(60));

            Assert.Equal(TitleEvent.RetainedOnATechnicality, update.Event);
            Assert.Same(champion, title.Champion);
            Assert.Single(title.Lineage);
        }

        [Fact]
        public void ACleanWinTakesTheBelt()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 75);
            var title = BeltHeldBy(champion);

            var update = TitleEconomy.ResolveTitleMatch(
                title, winner: challenger, loser: champion,
                FinishWeight.Decisive, starRating: 4.5, Day0.AddDays(200), "Big Show");

            Assert.Equal(TitleEvent.Changed, update.Event);
            Assert.Same(challenger, title.Champion);
            Assert.Equal(2, title.Lineage.Count);
            Assert.Equal(2, title.CurrentReign!.ReignNumber);
            Assert.Equal(Day0.AddDays(200), title.Lineage[0].Lost);
            Assert.Equal("Big Show", title.CurrentReign.WonAt);
        }

        // ── Prestige rises (§3) ──────────────────────────────────────────────

        [Fact]
        public void ACredibleDefenceRaisesPrestige()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 78);
            var title = BeltHeldBy(champion, standing: 60);

            var update = TitleEconomy.ResolveTitleMatch(
                title, winner: champion, loser: challenger,
                FinishWeight.Decisive, starRating: 4.5, Day0.AddDays(30));

            Assert.Equal(TitleEvent.Retained, update.Event);
            Assert.True(update.PrestigeDelta > 0, $"expected a rise, got {update.PrestigeDelta:F2}");
            Assert.Equal(1, title.CurrentDefences);
        }

        [Fact]
        public void ADefenceAgainstANobodyIsWorthFarLessThanOneAgainstAStar()
        {
            var champion = TestRoster.Make("Champ", overness: 85);

            double vsStar   = TitleEconomy.DefenceGain(4.0, TestRoster.Make("Star", overness: 85), FinishWeight.Decisive);
            double vsNobody = TitleEconomy.DefenceGain(4.0, TestRoster.Make("Nobody", overness: 10), FinishWeight.Decisive);

            Assert.True(vsStar > vsNobody * 2,
                $"a credible challenger should be worth much more: {vsStar:F2} vs {vsNobody:F2}");
        }

        [Fact]
        public void RetainingByCountOutIsWorthLessThanRetainingClean()
        {
            var challenger = TestRoster.Make("Challenger", overness: 75);

            double clean     = TitleEconomy.DefenceGain(4.0, challenger, FinishWeight.Decisive);
            double protectedFinish = TitleEconomy.DefenceGain(4.0, challenger, FinishWeight.Protected);

            Assert.True(protectedFinish < clean * 0.6,
                $"the travelling-champion finish is not a free defence: {protectedFinish:F2} vs {clean:F2}");
        }

        [Fact]
        public void ALongReignDefendedRegularlyBuildsTheTitleUp()
        {
            var champion = TestRoster.Make("Ace", overness: 88);
            var title = BeltHeldBy(champion, standing: 60);
            double before = title.Standing;

            // A year of the doc's minimum: a defence roughly every month, against people
            // the audience rates, and the clock running in between.
            var date = Day0;
            for (int month = 0; month < 12; month++)
            {
                for (int day = 0; day < 30; day++)
                {
                    date = date.AddDays(1);
                    TitleEconomy.ApplyDailyDrift(title, date);
                }

                TitleEconomy.ResolveTitleMatch(
                    title, winner: champion, loser: TestRoster.Make($"Challenger {month}", overness: 72),
                    FinishWeight.Decisive, starRating: 4.0, date);
            }

            Assert.True(title.Standing > before + 10,
                $"a year of credible defences should build the belt: {before:F1} → {title.Standing:F1}");
            Assert.Equal(12, title.CurrentDefences);
            Assert.Equal(360, title.CurrentReign!.DaysHeld(date));
        }

        // ── Prestige falls (§4) ──────────────────────────────────────────────

        [Fact]
        public void HotPotatoingTheBeltDestroysIt()
        {
            var title = Belt(standing: 70);
            var holders = Enumerable.Range(0, 6)
                .Select(i => TestRoster.Make($"Holder {i}", overness: 60))
                .ToList();

            // Somebody has to have it first.
            title.Lineage.Add(new TitleReign { Champion = holders[0], ReignNumber = 1, Won = Day0 });

            double before = title.Standing;
            var date = Day0;

            for (int i = 1; i < holders.Count; i++)
            {
                date = date.AddDays(21); // a change every three weeks
                TitleEconomy.ResolveTitleMatch(
                    title, winner: holders[i], loser: holders[i - 1],
                    FinishWeight.Decisive, starRating: 3.0, date);
            }

            Assert.True(title.Standing < before - 20,
                $"five changes in four months should gut the belt: {before:F1} → {title.Standing:F1}");
        }

        [Fact]
        public void AChampionTheAudienceRejectsCostsTheTitle()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var accepted = TestRoster.Make("Accepted", overness: 78);
            var rejected = TestRoster.Make("Rejected", overness: 20);

            double toAccepted = TitleEconomy.ChangeDelta(70, 300, accepted, 4.0, FinishWeight.Decisive);
            double toRejected = TitleEconomy.ChangeDelta(70, 300, rejected, 4.0, FinishWeight.Decisive);

            Assert.True(toRejected < toAccepted - 5,
                $"the audience's disbelief transfers to the belt: {toRejected:F2} vs {toAccepted:F2}");
            Assert.True(toRejected < 0);
        }

        [Fact]
        public void ATitleWonOnARollUpIsWorthLessThanOneWonClean()
        {
            var newChampion = TestRoster.Make("Winner", overness: 75);

            double clean = TitleEconomy.ChangeDelta(65, 250, newChampion, 4.0, FinishWeight.Decisive);
            double fluke = TitleEconomy.ChangeDelta(65, 250, newChampion, 4.0, FinishWeight.Fluke);

            Assert.True(fluke < clean, $"{fluke:F2} should be below {clean:F2}");
        }

        [Fact]
        public void ALongChasePaidOffCleanLeavesTheTitleStronger()
        {
            var champion   = TestRoster.Make("Champ", overness: 85);
            var challenger = TestRoster.Make("Challenger", overness: 82);
            var title = BeltHeldBy(champion, standing: 70);

            var update = TitleEconomy.ResolveTitleMatch(
                title, winner: challenger, loser: champion,
                FinishWeight.Decisive, starRating: 5.0, Day0.AddDays(300), "The Big One");

            Assert.True(update.PrestigeDelta > 0,
                $"a built challenger winning big should not damage the belt, got {update.PrestigeDelta:F2}");
        }

        // ── The non-title loss (§4.1) ────────────────────────────────────────

        [Fact]
        public void ANonTitleLossCostsTheChampionsBeltSomething()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var title = BeltHeldBy(champion, standing: 60);

            var update = TitleEconomy.ApplyNonTitleLoss(
                title, TestRoster.Make("Opponent", overness: 70), FinishWeight.Decisive);

            Assert.Equal(TitleEvent.NonTitleLoss, update.Event);
            Assert.True(update.PrestigeDelta < 0);
            Assert.Same(champion, title.Champion);   // the belt does not move
        }

        [Fact]
        public void NonTitleLossesAccumulate()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var title = BeltHeldBy(champion, standing: 60);

            for (int i = 0; i < 8; i++)
                TitleEconomy.ApplyNonTitleLoss(
                    title, TestRoster.Make($"Opponent {i}", overness: 70), FinishWeight.Decisive);

            Assert.True(title.Standing < 50,
                $"eight non-title losses should hollow the belt out, got {title.Standing:F1}");
        }

        [Fact]
        public void LosingNonTitleToSomeoneWellBelowYouCostsMore()
        {
            var champion = TestRoster.Make("Champ", overness: 85);

            double toPeer   = TitleEconomy.NonTitleLossPenalty(champion, TestRoster.Make("Peer", overness: 80), FinishWeight.Decisive);
            double toNobody = TitleEconomy.NonTitleLossPenalty(champion, TestRoster.Make("Nobody", overness: 30), FinishWeight.Decisive);

            Assert.True(toNobody > toPeer, $"{toNobody:F2} should exceed {toPeer:F2}");
        }

        [Fact]
        public void ALossByDisqualificationInANonTitleMatchCostsLeast()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var opponent = TestRoster.Make("Opponent", overness: 78);

            Assert.True(
                TitleEconomy.NonTitleLossPenalty(champion, opponent, FinishWeight.Protected)
                < TitleEconomy.NonTitleLossPenalty(champion, opponent, FinishWeight.Decisive));
        }

        // ── Absence and vacancy (§4) ─────────────────────────────────────────

        [Fact]
        public void ATitleNobodyDefendsBleedsValue()
        {
            var champion = TestRoster.Make("Absent Champ", overness: 85);
            var title = BeltHeldBy(champion, standing: 60);
            double before = title.Standing;

            var date = Day0;
            for (int i = 0; i < 180; i++)
            {
                date = date.AddDays(1);
                TitleEconomy.ApplyDailyDrift(title, date);
            }

            Assert.True(title.Standing < before - 10,
                $"half a year with no defence should hurt: {before:F1} → {title.Standing:F1}");
        }

        [Fact]
        public void ADefendedBeltBorrowsItsChampionsStanding()
        {
            var star = TestRoster.Make("Star", overness: 95);
            var cold = TestRoster.Make("Cold", overness: 25);

            var withStar = BeltHeldBy(star, standing: 60);
            var withCold = BeltHeldBy(cold, standing: 60);

            // Both defend on the same schedule, so the only difference is who is carrying it.
            var date = Day0;
            for (int i = 0; i < 120; i++)
            {
                date = date.AddDays(1);
                withStar.CurrentReign!.LastDefended = date;
                withCold.CurrentReign!.LastDefended = date;
                TitleEconomy.ApplyDailyDrift(withStar, date);
                TitleEconomy.ApplyDailyDrift(withCold, date);
            }

            Assert.True(withStar.Standing > 60, $"a champion the audience believes in lifts the belt, got {withStar.Standing:F1}");
            Assert.True(withCold.Standing < 60, $"a champion they do not drags it down, got {withCold.Standing:F1}");
        }

        [Fact]
        public void EveryVacancyWeakensTheLineage()
        {
            var champion = TestRoster.Make("Champ", overness: 80);
            var title = BeltHeldBy(champion, standing: 60);

            var update = TitleEconomy.Vacate(title, Day0.AddDays(100), "Injury");

            Assert.Equal(TitleEvent.Vacated, update.Event);
            Assert.True(title.IsVacant);
            Assert.Equal(60 - TitleEconomy.VacancyCost, title.Standing, 3);
            Assert.True(title.Lineage[0].Vacated);
            Assert.Equal(100, title.Lineage[0].DaysHeld(Day0.AddDays(100)));
        }

        [Fact]
        public void AVacantTitleCanBeWonAndTheLineageResumes()
        {
            var title = Belt(standing: 55);
            var a = TestRoster.Make("A", overness: 70);
            var b = TestRoster.Make("B", overness: 68);

            var update = TitleEconomy.ResolveTitleMatch(
                title, winner: a, loser: b, FinishWeight.Decisive, 4.0, Day0.AddDays(10), "Tournament Final");

            Assert.Equal(TitleEvent.Filled, update.Event);
            Assert.Same(a, title.Champion);
            Assert.Equal(1, title.CurrentReign!.ReignNumber);
        }

        // ── Reign length is a real number (§5) ───────────────────────────────

        [Fact]
        public void ReignLengthIsMeasuredInDaysAndSurvivesTheReignEnding()
        {
            var champion   = TestRoster.Make("Champ", overness: 80);
            var challenger = TestRoster.Make("Challenger", overness: 78);
            var title = BeltHeldBy(champion, since: Day0);

            var end = Day0.AddDays(412);
            var update = TitleEconomy.ResolveTitleMatch(
                title, winner: challenger, loser: champion, FinishWeight.Decisive, 4.0, end);

            Assert.Equal(412, update.OutgoingReignDays);
            Assert.Equal(412, title.Lineage[0].DaysHeld(end.AddDays(999)));
            Assert.Equal("412 days", title.Lineage[0].LengthLabel(end));
        }

        [Fact]
        public void TheDropWhenALongReignEndsIsBiggerThanForAShortOne()
        {
            var newChampion = TestRoster.Make("Winner", overness: 60);

            // Same challenger, same match, same belt — only the reign that ended differs.
            // Doc 21's sim note: the drop should scale with what the outgoing champion
            // had accumulated, which is what makes the end of a long reign an event.
            double afterLong  = TitleEconomy.ChangeDelta(70, 700, newChampion, 4.0, FinishWeight.Decisive);
            double afterMedium = TitleEconomy.ChangeDelta(70, 150, newChampion, 4.0, FinishWeight.Decisive);

            Assert.True(afterLong < afterMedium,
                $"handing back a big reign's aura should cost more: {afterLong:F2} vs {afterMedium:F2}");
        }

        // ── Status transfer ──────────────────────────────────────────────────

        [Fact]
        public void WinningAPrestigiousBeltIsWorthMoreThanWinningADevaluedOne()
        {
            var winner = TestRoster.Make("Winner", overness: 60);

            var big   = TitleEconomy.WinBonus(Belt(standing: 90), winner);
            var small = TitleEconomy.WinBonus(Belt(standing: 10), winner);

            Assert.True(big.OvernessDelta > small.OvernessDelta * 3);
            Assert.True(big.MomentumDelta > small.MomentumDelta * 3);
        }

        [Fact]
        public void PrestigeNeverLeavesTheZeroToHundredBand()
        {
            var champion = TestRoster.Make("Champ", overness: 5);
            var title = BeltHeldBy(champion, standing: 2);

            for (int i = 0; i < 20; i++)
                TitleEconomy.ApplyNonTitleLoss(title, TestRoster.Make($"X{i}", overness: 90), FinishWeight.Decisive);

            Assert.InRange(title.Standing, 0, 100);
            Assert.InRange(title.Prestige, 0, 100);
        }
    }
}
