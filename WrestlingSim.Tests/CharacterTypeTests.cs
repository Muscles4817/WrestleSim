using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using Xunit;
using Xunit.Abstractions;
using static WrestlingSim.Engine.CharacterTypes;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Characters, step 2 of docs/gimmick-plan.md: different characters want different results.
    /// The same match, the same numbers, a different gimmick type — a different price.
    /// </summary>
    public class CharacterTypeTests
    {
        private readonly ITestOutputHelper output;
        public CharacterTypeTests(ITestOutputHelper output) => this.output = output;

        private static Wrestler Typed(string name, GimmickType type, params string[] traits)
        {
            var w = TestRoster.Make(name, overness: 70);
            w.Gimmick.Type = type;
            w.Gimmick.GimmickTraits = [.. traits];
            return w;
        }

        /// <summary>
        /// One match written from a brief and worked with a fixed seed: the engine sees nothing
        /// of the gimmick type, so two runs differ only in how the characters read the result.
        /// </summary>
        private static (ShowResult Result, BookedMatch Match) Run(Wrestler winner, Wrestler loser, MatchStory story,
                                                                  FinishKind finish = FinishKind.Clean)
        {
            var sides = new List<MatchSide> { new() { Members = [winner] }, new() { Members = [loser] } };
            var brief = new MatchBrief { Story = story, Finish = finish, WinningSide = 0 };
            var match = new BookedMatch
            {
                Plan = new MatchPlan { WrestlerA = winner, WrestlerB = loser, Beats = BriefDirector.Write(brief, sides).Beats.ToList(), Brief = brief }
            };
            var show = new Show { Card = [match], TotalDurationMinutes = 120 };
            return (new ShowSimulator(new FeudBook(), seed: 11).Simulate(show), match);
        }

        private static double OvernessMove(ShowResult r, Wrestler w) =>
            r.StatusChanges.Where(c => c.Wrestler == w).Sum(c => c.OvernessDelta);

        // ── The table ────────────────────────────────────────────────────────

        [Fact]
        public void EveryMultiplierIsPositive_AndApplyingOneNeverFlipsASign()
        {
            foreach (var type in Enum.GetValues<GimmickType>().Cast<GimmickType?>().Append(null))
                foreach (var kind in Enum.GetValues<ResultKind>())
                {
                    double m = For(type).Of(kind);
                    Assert.True(m > 0, $"{type} {kind}");

                    var win  = new StatusChange(TestRoster.Make("W"), 1.0, 5.0, "");
                    var loss = new StatusChange(TestRoster.Make("L"), -1.0, -5.0, "");
                    var applied = Apply(IsWin(kind) ? win : loss, new Effect(kind, m, null));
                    Assert.Equal(Math.Sign(IsWin(kind) ? 1 : -1), Math.Sign(applied.OvernessDelta));
                }
        }

        [Fact]
        public void AWinNeverTouchesALoss_AndALossNeverTouchesAGain()
        {
            var showcase = new StatusChange(TestRoster.Make("L"), -1.0, +3.0, "lost a great match");
            var applied = Apply(showcase, new Effect(ResultKind.CloseLoss, 2.0, null));
            Assert.Equal(-2.0, applied.OvernessDelta, 10);
            Assert.Equal(+3.0, applied.MomentumDelta, 10);   // a great match's showcase is not a cost
        }

        // ── In the ring ──────────────────────────────────────────────────────

        [Fact]
        public void AMonsterBeatenClean_PaysFarMoreThanSomebodyWithNoPrice()
        {
            var plain   = Run(Typed("Winner", GimmickType.Showman), Typed("Plain", GimmickType.Loner), MatchStory.EvenContest);
            var monster = Run(Typed("Winner", GimmickType.Showman), Typed("Plain", GimmickType.Monster), MatchStory.EvenContest);

            double paid = OvernessMove(plain.Result, plain.Match.Plan.WrestlerB);
            double paidByMonster = OvernessMove(monster.Result, monster.Match.Plan.WrestlerB);
            output.WriteLine($"plain loser {paid:F3}, monster loser {paidByMonster:F3}");

            Assert.True(paid < 0);
            Assert.True(paidByMonster < paid * 1.5, "a monster beaten clean should pay at least half as much again");
            Assert.Contains(monster.Result.StatusChanges, c => c.Reason.Contains("monsters are not beaten clean"));
        }

        [Fact]
        public void AnUnderdogLosingAClose_OneCostsLittle()
        {
            var plain    = Run(Typed("Winner", GimmickType.Showman), Typed("Plain", GimmickType.Loner), MatchStory.EvenContest);
            var underdog = Run(Typed("Winner", GimmickType.Showman), Typed("Plain", GimmickType.Underdog), MatchStory.EvenContest);

            double paid = OvernessMove(plain.Result, plain.Match.Plan.WrestlerB);
            double paidByUnderdog = OvernessMove(underdog.Result, underdog.Match.Plan.WrestlerB);
            output.WriteLine($"plain loser {paid:F3}, underdog loser {paidByUnderdog:F3}");
            Assert.True(paidByUnderdog > paid * 0.6, "an underdog's close loss should cost well under the usual");
        }

        [Fact]
        public void AHeelStealingOne_IsTheCharacterWorking()
        {
            var plain   = Run(Typed("Plain", GimmickType.Loner), Typed("Loser", GimmickType.Showman), MatchStory.EvenContest, FinishKind.Stolen);
            var egotist = Run(Typed("Plain", GimmickType.Egotist), Typed("Loser", GimmickType.Showman), MatchStory.EvenContest, FinishKind.Stolen);
            Assert.True(OvernessMove(egotist.Result, egotist.Match.Plan.WrestlerA) >= OvernessMove(plain.Result, plain.Match.Plan.WrestlerA));
            Assert.Contains(egotist.Result.StatusChanges, c => c.Reason.Contains("stole it"));
        }

        [Fact]
        public void BeatingAnEgotistClean_IsThePayoff()
        {
            var e = Of(Typed("Hero", GimmickType.Loner), ResultKind.HardWin, 3.0, onATeam: false,
                       beaten: [Typed("Heel", GimmickType.Egotist)]);
            Assert.Equal(PayoffBonus, e.Multiplier, 10);
            Assert.True(e.Payoff);
            Assert.NotNull(e.Why);
        }

        /// <summary>
        /// The builder and the show read the same thing — the brief — so a warning before the
        /// bell is the consequence after it. A showcase is a one-sided win in both.
        /// </summary>
        [Fact]
        public void TheShowReadsTheBookingTheBuilderWarnedAbout()
        {
            var squash = Run(Typed("The Beast", GimmickType.Monster), Typed("Jobber", GimmickType.Loner), MatchStory.Showcase);
            Assert.Contains(squash.Result.StatusChanges, c => c.Wrestler.RingName == "The Beast" && c.Reason.Contains("ran through somebody"));

            var brief = new MatchBrief { Story = MatchStory.Showcase, WinningSide = 0 };
            Assert.Equal(ResultKind.DominantWin, BriefCritique.Expected(brief, won: true, side: 0));
            Assert.Equal(ResultKind.BeatenLoss, BriefCritique.Expected(brief, won: false, side: 1));
            Assert.Equal(ResultKind.CloseLoss, BriefCritique.Expected(new MatchBrief { WinningSide = 0 }, won: false, side: 1));

            var protectedLoser = new MatchBrief { WinningSide = 0, Bookings = { [1] = Booking.Diminished } };
            Assert.Equal(ResultKind.BeatenLoss, BriefCritique.Expected(protectedLoser, won: false, side: 1));
            Assert.Equal(ResultKind.CheapWin, BriefCritique.Expected(new MatchBrief { WinningSide = 0, Finish = FinishKind.Stolen }, won: true, side: 0));
        }

        /// <summary>A match nobody wrote a brief for reads as a fought one, either way.</summary>
        [Fact]
        public void AMatchWithNoBriefReadsAsAFoughtOne()
        {
            Assert.Equal(ResultKind.HardWin, Read(true, FinishWeight.Decisive, ShareFor(null, true, 0)));
            Assert.Equal(ResultKind.CloseLoss, Read(false, FinishWeight.Decisive, ShareFor(null, false, 1)));
        }

        // ── Before the bell ──────────────────────────────────────────────────

        [Fact]
        public void TheBuilderWarnsBeforeAMonsterIsBeatenClean_AndSaysNothingAboutAPlainResult()
        {
            var sides = new List<MatchSide>
            {
                new() { Members = [Typed("Hero", GimmickType.Loner)] },
                new() { Members = [Typed("The Beast", GimmickType.Monster)] }
            };
            var notes = BriefCritique.Character(new MatchBrief { WinningSide = 0 }, sides);
            var note = Assert.Single(notes);
            Assert.Equal(NoteWeight.Warning, note.Weight);
            Assert.Contains("The Beast", note.Text);
            output.WriteLine(note.Text);

            var plain = new List<MatchSide>
            {
                new() { Members = [Typed("Hero", GimmickType.Loner)] },
                new() { Members = [Typed("Other", GimmickType.Loner)] }
            };
            Assert.Empty(BriefCritique.Character(new MatchBrief { WinningSide = 0 }, plain));
        }

        [Fact]
        public void TheBuilderSaysAnUnderdogSquashingSomebodyIsWasted()
        {
            var sides = new List<MatchSide>
            {
                new() { Members = [Typed("Little Guy", GimmickType.Underdog)] },
                new() { Members = [Typed("Jobber", GimmickType.Loner)] }
            };
            var notes = BriefCritique.Character(new MatchBrief { Story = MatchStory.Showcase, WinningSide = 0 }, sides);
            Assert.Contains(notes, n => n.Text.Contains("Little Guy") && n.Text.Contains("one-sided"));
        }

        // ── Traits ───────────────────────────────────────────────────────────

        [Fact]
        public void TheRostersCommonTraitsMeanSomething()
        {
            var plain  = Of(Typed("A", GimmickType.Loner), ResultKind.BeatenLoss, 3.0, false);
            var feared = Of(Typed("B", GimmickType.Loner, "Feared"), ResultKind.BeatenLoss, 3.0, false);
            var vet    = Of(Typed("C", GimmickType.Loner, "veteran"), ResultKind.BeatenLoss, 3.0, false);
            Assert.True(feared.Multiplier > plain.Multiplier);
            Assert.True(vet.Multiplier < plain.Multiplier, "trait names match whatever their case");

            var shipped = DataLoaders.LoadEmbeddedWrestlers();
            int read = shipped.Count(w => EffectsOf(w).Count > 0);
            output.WriteLine($"{read} of {shipped.Count} shipped wrestlers have a trait that does something");
            Assert.True(read >= shipped.Count / 3);
        }

        // ── The stage ────────────────────────────────────────────────────────

        [Fact]
        public void ShowmenAreBiggerOnTheBigNight_AndTelevisionIsEverybodysStage()
        {
            Assert.True(Stage(Typed("S", GimmickType.Showman), ShowType.PremiumEvent) > 1);
            Assert.True(Stage(Typed("C", GimmickType.Comedy), ShowType.HouseShow) > 1);
            Assert.True(Stage(Typed("C", GimmickType.Comedy), ShowType.PremiumEvent) < 1);
            foreach (var type in Enum.GetValues<GimmickType>())
                Assert.Equal(1.0, Stage(Typed("T", type), ShowType.Television));
        }

        /// <summary>
        /// The stage makes characters differ; it must not make the whole roster draw more on a
        /// big night than it did. Averaged over the shipped roster, within two percent.
        /// </summary>
        [Fact]
        public void TheStageIsCentredOnTheShippedRoster()
        {
            var shipped = DataLoaders.LoadEmbeddedWrestlers();
            foreach (var type in new[] { ShowType.PremiumEvent, ShowType.HouseShow })
            {
                double mean = shipped.Average(w => Stage(w, type));
                output.WriteLine($"{type}: mean stage {mean:F4}");
                Assert.InRange(mean, 0.98, 1.02);
            }
        }

        [Fact]
        public void TheProfileSaysWhatACharacterIsFedBy()
        {
            var s = Summary(Typed("The Beast", GimmickType.Monster, "Feared"));
            Assert.StartsWith("A monster: fed by dominant wins", s);
            Assert.Contains("Feared", s);
        }
    }
}
