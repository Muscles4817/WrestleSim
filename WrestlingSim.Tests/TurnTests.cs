using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Factories;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.Segment;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Characters, step 4 of docs/gimmick-plan.md: turns. Booked by the player, priced on the
    /// crowd, the asymmetry, the moment, how often, the build and comedy (doc 19 §5).
    /// </summary>
    public class TurnTests
    {
        private readonly ITestOutputHelper output;
        public TurnTests(ITestOutputHelper output) => this.output = output;

        private static readonly DateOnly Day = new(2026, 6, 1);

        private static Wrestler Booked(string name, Alignment a, double overness = 60, double appeal = 0.5, double charisma = 3.0)
        {
            var w = TestRoster.Make(name, overness: overness, charisma: charisma);
            w.Gimmick.NaturalAlignment = a;
            w.Gimmick.AppealRatings.ForEach(r => r.AppealScore = appeal);
            return w;
        }

        private static double Payoff(Wrestler w, Alignment to, ShowType type = ShowType.Television) =>
            Turns.Forecast(w, to, type, Day).Payoff;

        // ── What a turn is worth ─────────────────────────────────────────────

        [Fact]
        public void AHeelTheCrowdCheers_TurnsFaceToAFarBiggerReaction()
        {
            var adored = Booked("Cool Heel", Alignment.Heel, overness: 95, appeal: 0.95, charisma: 5);
            var hated  = Booked("Real Heel", Alignment.Heel, overness: 40, appeal: 0.2,  charisma: 2);
            Assert.Equal(CrowdReading.Reaction.Cheering, CrowdReading.Of(adored));
            Assert.Equal(CrowdReading.Reaction.Booing, CrowdReading.Of(hated));
            Assert.True(Payoff(adored, Alignment.Face) > 2.5 * Payoff(hated, Alignment.Face));
        }

        [Fact]
        public void HeelTurnsComeEasierThanFaceTurns()
        {
            var a = Booked("A", Alignment.Tweener);
            Assert.True(Payoff(a, Alignment.Heel) > Payoff(a, Alignment.Face));
        }

        [Fact]
        public void TheBiggerTheNight_TheBiggerTheTurn()
        {
            var a = Booked("A", Alignment.Face);
            Assert.True(Payoff(a, Alignment.Heel, ShowType.PremiumEvent) > Payoff(a, Alignment.Heel, ShowType.Television));
            Assert.True(Payoff(a, Alignment.Heel, ShowType.Television) > Payoff(a, Alignment.Heel, ShowType.HouseShow));
        }

        [Fact]
        public void TurningEveryYear_StopsMeaningAnything()
        {
            var fresh = Booked("Fresh", Alignment.Face);
            var flipper = Booked("Flipper", Alignment.Face);
            flipper.LastTurned = Day.AddMonths(-6);
            Assert.True(Payoff(flipper, Alignment.Heel) < 0.5 * Payoff(fresh, Alignment.Heel));
        }

        [Fact]
        public void ABuiltTurnLandsHarderThanACold_One_AndATeaseTheOtherWayStartsAgain()
        {
            var cold = Booked("Cold", Alignment.Face);
            var built = Booked("Built", Alignment.Face);
            for (int i = 0; i < 3; i++) Turns.Tease(built, Alignment.Heel, Day);
            Assert.True(Payoff(built, Alignment.Heel) > 1.4 * Payoff(cold, Alignment.Heel));

            Turns.Tease(built, Alignment.Face, Day);   // already face: nothing
            Assert.Equal(Alignment.Heel, built.TeasedToward);
            var other = Booked("Other", Alignment.Tweener);
            Turns.Tease(other, Alignment.Heel, Day); Turns.Tease(other, Alignment.Heel, Day);
            Turns.Tease(other, Alignment.Face, Day);
            Assert.Equal(Turns.TeaseStep, other.TurnAnticipation, 10);
        }

        [Fact]
        public void AComedyActsHeelTurnReadsAsAJoke()
        {
            var straight = Booked("Straight", Alignment.Face);
            var comic = Booked("Comic", Alignment.Face);
            comic.Gimmick.Type = GimmickType.Comedy;
            Assert.Equal(0.5 * Payoff(straight, Alignment.Heel), Payoff(comic, Alignment.Heel), 6);
        }

        [Fact]
        public void TurningToWhereTheyAlreadyStandIsNotATurn()
        {
            Assert.Equal(0, Payoff(Booked("Face", Alignment.Face), Alignment.Face));
        }

        // ── What a turn does ─────────────────────────────────────────────────

        [Fact]
        public void ATurnFlipsTheAlignment_RenewsTheCharacter_AndClearsTheBuild()
        {
            var w = Booked("Turner", Alignment.Face);
            w.Gimmick.Freshness = 0.3;
            Turns.Tease(w, Alignment.Heel, Day);
            var (change, line, payoff) = Turns.Execute(w, Alignment.Heel, ShowType.PremiumEvent, Day);

            Assert.Equal(Alignment.Heel, w.Gimmick.NaturalAlignment);
            Assert.True(w.Gimmick.Freshness > 0.5);
            Assert.Equal(0, w.TurnAnticipation);
            Assert.Equal(Day, w.LastTurned);
            Assert.True(change.MomentumDelta > 0);
            Assert.Contains("turned heel", line);
            output.WriteLine($"{line} (payoff {payoff:F2})");
        }

        [Fact]
        public void TeasingMovesTheCrowdsReadingBeforeTheTurnComes()
        {
            var w = Booked("Drifting", Alignment.Face, overness: 70, appeal: 0.6);
            double before = new PerformerProfile(w).Favour;
            for (int i = 0; i < 4; i++) Turns.Tease(w, Alignment.Heel, Day);
            Assert.True(new PerformerProfile(w).Favour < before - 0.1);
        }

        [Fact]
        public void TheBuildFades_AndFadesFastOnceTheCrowdStopsBelieving()
        {
            var w = Booked("Teased", Alignment.Face);
            for (int i = 0; i < 2; i++) Turns.Tease(w, Alignment.Heel, Day);
            Turns.Fade([w], Day.AddDays(45), 45);
            Assert.Equal(0.25, w.TurnAnticipation, 6);

            var stale = Booked("Stale", Alignment.Face);
            for (int i = 0; i < 4; i++) Turns.Tease(stale, Alignment.Heel, Day);
            Turns.Fade([stale], Day.AddDays(Turns.BelieveForDays + 15), 15);
            Assert.Equal(0.5, stale.TurnAnticipation, 6);
        }

        // ── In the show ──────────────────────────────────────────────────────

        [Fact]
        public void ABetrayalIsAHeelTurn_AndTheReportSaysSo()
        {
            var face = Booked("Partner", Alignment.Face);
            var victim = Booked("Victim", Alignment.Face);
            var segment = SegmentFactory.CreateBetrayal(face, victim);
            var show = new Show { Card = [segment], Date = Day.ToDateTime(TimeOnly.MinValue), Type = ShowType.PremiumEvent };

            var result = new ShowSimulator(new FeudBook(), seed: 3).Simulate(show);

            Assert.Equal(Alignment.Heel, face.Gimmick.NaturalAlignment);
            Assert.Contains(result.Turns, t => t.StartsWith("Partner has turned heel"));
            Assert.Contains(result.StatusChanges, c => c.Wrestler == face && c.Reason.StartsWith("Turned heel"));
        }

        [Fact]
        public void ABabyfaceWhoNeededSomebodyElseToWin_IsATease()
        {
            var face = Booked("Hero", Alignment.Face);
            var heel = Booked("Villain", Alignment.Heel);
            var sides = new List<MatchSide> { new() { Members = [face] }, new() { Members = [heel] } };
            var brief = new MatchBrief { WinningSide = 0, Finish = FinishKind.Interference };
            var match = new BookedMatch { Plan = new MatchPlan { WrestlerA = face, WrestlerB = heel, Brief = brief, Beats = BriefDirector.Write(brief, sides).Beats.ToList() } };

            new ShowSimulator(new FeudBook(), seed: 5).Simulate(new Show { Card = [match], Date = Day.ToDateTime(TimeOnly.MinValue) });

            Assert.Equal(Alignment.Heel, face.TeasedToward);
            Assert.True(face.TurnAnticipation > 0);
        }

        [Fact]
        public void TheSaveCarriesTurnsAndTheBuildTowardThem()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var career = new Career { Promotion = new Promotion { Name = "T", Tier = PromotionTier.Established }, StartDate = Day, CurrentDate = Day, Roster = roster };
            career.OpenBooks(Day);
            var turned = roster.First(w => w.Gimmick.NaturalAlignment == Alignment.Face);
            Turns.Execute(turned, Alignment.Heel, ShowType.Television, Day);
            var teased = roster.First(w => w.Gimmick.NaturalAlignment == Alignment.Heel && w != turned);
            Turns.Tease(teased, Alignment.Face, Day);

            var show = career.Schedule("Weekly", Day.AddDays(3), ShowType.Television);
            var seg = SegmentFactory.CreateBetrayal(roster.First(w => w.Gimmick.NaturalAlignment == Alignment.Face), teased);
            show.Card.Add(seg);

            var back = SaveSerializer.FromJson(SaveSerializer.ToJson(career), DataLoaders.LoadEmbeddedWrestlers());
            var t = back.FindWrestler(turned.Id)!;
            Assert.Equal(Alignment.Heel, t.Gimmick.NaturalAlignment);
            Assert.Equal(Day, t.LastTurned);
            Assert.Equal(1, t.Turns);
            var s = back.FindWrestler(teased.Id)!;
            Assert.Equal(Alignment.Face, s.TeasedToward);
            Assert.Equal(Turns.TeaseStep, s.TurnAnticipation, 10);
            var savedSeg = Assert.IsType<Segment>(back.Upcoming.Single().Card.Single());
            Assert.Equal(Alignment.Heel, savedSeg.Turn!.To);
        }

        // ── Rivals ───────────────────────────────────────────────────────────

        [Fact]
        public void ARivalTurnsAHeelItsCrowdKeepsCheering()
        {
            var career = RivalTests.NewWorld(PromotionTier.Local);
            var rival = career.Rivals.First();
            var heel = Booked("Cool Heel", Alignment.Heel, overness: 95, appeal: 0.95, charisma: 5);
            career.Roster.Add(heel);
            rival.Contracts.Add(new Contract { WrestlerId = heel.Id, Kind = ContractKind.Regular, Start = career.CurrentDate });

            for (int month = 0; month < 36 && heel.Gimmick.NaturalAlignment == Alignment.Heel; month++)
            {
                career.CurrentDate = career.CurrentDate.AddMonths(1);
                Turns.RivalMonth(career);
            }

            Assert.Equal(Alignment.Face, heel.Gimmick.NaturalAlignment);
            Assert.Contains(career.News, n => n.Text.Contains("Cool Heel has turned face"));
        }
    }
}
