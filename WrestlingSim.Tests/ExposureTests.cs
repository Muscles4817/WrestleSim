using WrestlingSim.Engine;
using WrestlingSim.Enums;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// What makes people more and less known (owner's ruling, 15 Nov 2026; docs 12 and 17):
    /// being shown builds the stock toward what the promotion's audience can make you, the
    /// crowd's reaction sets the pace and the heat, and a loss with structure costs little.
    /// </summary>
    public class ExposureTests
    {
        [Fact]
        public void BeingShownMakesYouBetterKnown_UpToWhatThePromotionCanMakeYou()
        {
            var w = TestRoster.Make("Midcarder", overness: 40, charisma: 4.0);
            var big = HeatEconomy.ForExposure(w, HeatEconomy.Reach(PromotionTier.Global), 12, 60, talking: false);
            var small = HeatEconomy.ForExposure(w, HeatEconomy.Reach(PromotionTier.Independent), 12, 60, talking: false);

            Assert.True(big.OvernessDelta > 0);
            Assert.True(big.OvernessDelta > small.OvernessDelta, "Global television reaches more people than a hall.");

            var known = TestRoster.Make("Known", overness: 50, charisma: 4.0);
            Assert.Equal(0, HeatEconomy.ForExposure(known, HeatEconomy.Reach(PromotionTier.Independent), 12, 60, false).OvernessDelta);
        }

        [Fact]
        public void TheCrowdsReactionSetsThePace_LovedOrHatedAlike()
        {
            var w = TestRoster.Make("A", overness: 40, charisma: 3.5);
            double reach = HeatEconomy.Reach(PromotionTier.National);
            var hot = HeatEconomy.ForExposure(w, reach, 12, 85, talking: false);
            var flat = HeatEconomy.ForExposure(w, reach, 12, 25, talking: false);

            Assert.True(hot.OvernessDelta > flat.OvernessDelta);
            Assert.True(hot.MomentumDelta > 0, "A strong reaction warms them up.");
            Assert.True(flat.MomentumDelta < 0, "Indifference cools them down.");
        }

        [Fact]
        public void TalkingIsWorthMoreToSomebodyWhoCanTalk()
        {
            double reach = HeatEconomy.Reach(PromotionTier.National);
            var talker = TestRoster.Make("Talker", overness: 40, charisma: 4.8);
            var mute = TestRoster.Make("Mute", overness: 40, charisma: 1.2);

            Assert.True(HeatEconomy.ForExposure(talker, reach, 8, 60, talking: true).OvernessDelta
                      > HeatEconomy.ForExposure(talker, reach, 8, 60, talking: false).OvernessDelta);
            Assert.True(HeatEconomy.ForExposure(mute, reach, 8, 60, talking: true).OvernessDelta
                      < HeatEconomy.ForExposure(mute, reach, 8, 60, talking: false).OvernessDelta);
        }

        [Fact]
        public void AnActSeenTooOftenGetsLessFromBeingSeen()
        {
            var w = TestRoster.Make("A", overness: 40, charisma: 3.5);
            double reach = HeatEconomy.Reach(PromotionTier.National);
            Assert.True(HeatEconomy.ForExposure(w, reach, 12, 60, false, freshness: 1.0).OvernessDelta
                      > HeatEconomy.ForExposure(w, reach, 12, 60, false, freshness: 0.6).OvernessDelta);
        }

        [Fact]
        public void NobodyBecomesMoreOverThanTheyHaveItInThemToBe()
        {
            var limited = TestRoster.Make("Limited", overness: 60, charisma: 1.5);
            Assert.True(HeatEconomy.Appeal(limited) < 60);
            Assert.Equal(0, HeatEconomy.ForExposure(limited, 92, 15, 90, false).OvernessDelta);
            Assert.Equal(0, HeatEconomy.DampenGain(limited, 3.0));
        }

        [Fact]
        public void LosingAGreatMatchDoesNotHurt()
        {
            var great = HeatEconomy.ForMatch(TestRoster.Make("W", 60), TestRoster.Make("L", 70), 4.75, FinishWeight.Decisive);
            var poor = HeatEconomy.ForMatch(TestRoster.Make("W", 60), TestRoster.Make("L", 70), 2.0, FinishWeight.Decisive);

            Assert.Equal(0, great.Loser.OvernessDelta, 6);
            Assert.True(poor.Loser.OvernessDelta < 0);
        }

        [Fact]
        public void LosingInAFeudThatIsGoingSomewhereCostsLittle()
        {
            double Cost(FeudIntensity f) => -HeatEconomy.ForMatch(TestRoster.Make("W", 60), TestRoster.Make("L", 70), 2.5,
                FinishWeight.Decisive, story: HeatEconomy.StoryOf(f)).Loser.OvernessDelta;

            Assert.True(Cost(FeudIntensity.Hot) < Cost(FeudIntensity.Building));
            Assert.True(Cost(FeudIntensity.Building) < Cost(FeudIntensity.None));
        }

        [Fact]
        public void TheWinnerOfAGreatMatchStillGains_TheLoserIsSimplyNotHurt()
        {
            var great = HeatEconomy.ForMatch(TestRoster.Make("W", 60, charisma: 4.5), TestRoster.Make("L", 70), 4.75, FinishWeight.Decisive);
            Assert.True(great.Winner.OvernessDelta > 0);
        }
    }
}
