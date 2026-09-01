using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using Xunit;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Title stakes and match-count decay were written independently and meet in one
    /// expression in MatchEngine.InitialiseState. These pin how they compose, because a
    /// hand-resolved merge is exactly where that sort of rule quietly stops holding.
    /// </summary>
    public class StakesAndStalenessTests
    {
        private static MatchPlanModel Plan(Title? title = null, Feud? feud = null) =>
            new()
            {
                WrestlerA = TestRoster.Make("Star A", overness: 85, charisma: 4.0),
                WrestlerB = TestRoster.Make("Star B", overness: 82, charisma: 4.0),
                Feud      = feud,
                TitleAtStake = title,
                Beats =
                [
                    new MatchBeat { Type = BeatType.HotOpening,  Control = BeatControl.Even },
                    new MatchBeat { Type = BeatType.HeatSegment, Control = BeatControl.WrestlerB },
                    new MatchBeat { Type = BeatType.Comeback,    Control = BeatControl.WrestlerA },
                    new MatchBeat { Type = BeatType.NearFall,    Control = BeatControl.WrestlerA },
                    new MatchBeat { Type = BeatType.FinishClean, Control = BeatControl.WrestlerA }
                ]
            };

        private static double Run(MatchPlanModel plan, double familiarity) =>
            new MatchEngine(seed: 4242).Execute(plan, familiarity).CrowdPeakEnergy;

        [Fact]
        public void StakesLiftTheRoom()
        {
            var title = new Title { Name = "World", Tier = TitleTier.World };
            title.Standing = 80;

            double plain = Run(Plan(), familiarity: 1.0);
            double forTheBelt = Run(Plan(title), familiarity: 1.0);

            Assert.True(forTheBelt > plain,
                $"a title match should draw a bigger room: {forTheBelt:F1} vs {plain:F1}");
        }

        [Fact]
        public void StalenessFlattensTheRoom()
        {
            double fresh = Run(Plan(), familiarity: 1.0);
            double stale = Run(Plan(), familiarity: 0.57);

            Assert.True(stale < fresh,
                $"a fifth meeting should be flatter: {stale:F1} vs {fresh:F1}");
        }

        [Fact]
        public void ABeltCannotBuyARoomOutOfBeingSickOfAPairing()
        {
            var title = new Title { Name = "World", Tier = TitleTier.World };
            title.Standing = 80;

            double freshPlain = Run(Plan(), familiarity: 1.0);
            double staleTitle = Run(Plan(title), familiarity: 0.57);

            // Familiarity is applied to the total, stakes included, so putting the belt on
            // a fifth meeting makes the flat version slightly less flat — it does not
            // restore it. This is the whole point of the ordering in InitialiseState.
            Assert.True(staleTitle < freshPlain,
                $"stakes must not undo staleness: {staleTitle:F1} vs {freshPlain:F1}");
        }

        [Fact]
        public void StakesStillHelpAStaleMatchALittle()
        {
            var title = new Title { Name = "World", Tier = TitleTier.World };
            title.Standing = 80;

            double stalePlain = Run(Plan(), familiarity: 0.57);
            double staleTitle = Run(Plan(title), familiarity: 0.57);

            Assert.True(staleTitle > stalePlain,
                $"the belt should still be worth something: {staleTitle:F1} vs {stalePlain:F1}");
        }
    }
}
