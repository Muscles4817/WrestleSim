using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Doc 18 §2.5: *"a multi-man match is a poor place to elevate somebody"*, and the reason
    /// to book one is that it *"lets a champion lose the match without losing cleanly"* and
    /// *"lets a challenger win without beating the champion"*.
    ///
    /// Those read like two claims and are one fact: with three in the ring, pinning somebody
    /// does not establish that you can beat them. They were fighting two people, and one of
    /// them was probably on the floor at the time.
    /// </summary>
    public class MultiManElevationTests(ITestOutputHelper output)
    {
        private static Wrestler W(string n, double o) =>
            TestRoster.Make(n, overness: o, charisma: 3.5, skill: 3.5);

        [Fact]
        public void MoreWaysToWin_MeansTheResultSaysLess()
        {
            double singles = HeatEconomy.Conclusiveness(2);
            double three   = HeatEconomy.Conclusiveness(3);
            double four    = HeatEconomy.Conclusiveness(4);

            output.WriteLine($"  singles {singles:F3} · three-way {three:F3} · four-way {four:F3}");

            Assert.Equal(1.0, singles);
            Assert.True(three < singles && four < three);

            // A three-way says most of what a singles match says, not a fraction of it —
            // somebody was still beaten in front of the same crowd.
            Assert.True(three is > 0.4 and < 0.85,
                $"A three-way should discount the result without erasing it; got {three:F3}.");

            // And a side count below two is not a match; it must not amplify anything.
            Assert.Equal(1.0, HeatEconomy.Conclusiveness(1));
        }

        /// <summary>
        /// **The elevation half.** Beating a star in a three-way should be worth
        /// meaningfully less than beating the same star, in the same match quality, one on one.
        /// </summary>
        [Fact]
        public void BeatingAStarInAThreeWay_ElevatesLessThanBeatingThemOneOnOne()
        {
            var challenger = W("Challenger", 55);
            var star       = W("Star", 92);

            var oneOnOne = HeatEconomy.ForMatch(
                challenger, star, starRating: 4.0, FinishWeight.Decisive);
            var threeWay = HeatEconomy.ForMatch(
                challenger, star, starRating: 4.0, FinishWeight.Decisive, sideCount: 3);

            output.WriteLine($"  singles  : challenger +{oneOnOne.Winner.OvernessDelta:F2} overness");
            output.WriteLine($"  three-way: challenger +{threeWay.Winner.OvernessDelta:F2} overness");

            Assert.True(threeWay.Winner.OvernessDelta < oneOnOne.Winner.OvernessDelta,
                "A multi-man match is a poor place to elevate somebody.");

            // But it is not nothing — they did win, and the crowd saw it.
            Assert.True(threeWay.Winner.OvernessDelta > 0);
        }

        /// <summary>
        /// **The protection half**, and it is the same discount seen from the other side. The
        /// star losing a three-way costs them less than losing the same match cleanly.
        /// </summary>
        [Fact]
        public void LosingAThreeWayCostsAStarLess_ThanLosingOneOnOne()
        {
            var challenger = W("Challenger", 55);
            var star       = W("Star", 92);

            var oneOnOne = HeatEconomy.ForMatch(
                challenger, star, starRating: 4.0, FinishWeight.Decisive);
            var threeWay = HeatEconomy.ForMatch(
                challenger, star, starRating: 4.0, FinishWeight.Decisive, sideCount: 3);

            output.WriteLine($"  singles  : star {oneOnOne.Loser.OvernessDelta:F2} overness");
            output.WriteLine($"  three-way: star {threeWay.Loser.OvernessDelta:F2} overness");

            // Both are losses, so both are negative; the three-way one is shallower.
            Assert.True(threeWay.Loser.OvernessDelta > oneOnOne.Loser.OvernessDelta,
                "A champion can be beaten in a three-way without being beaten.");
            Assert.True(threeWay.Loser.OvernessDelta < 0, "It is still a loss.");
        }

        /// <summary>
        /// Protection is the point *and the cost*, and the engine has to charge both. A
        /// booker who could take the protection without paying for it in elevation would
        /// have a free lunch, and §2.5's whole argument is that there isn't one.
        /// </summary>
        [Fact]
        public void ProtectionAndElevationAreDiscountedTogether()
        {
            var challenger = W("Challenger", 55);
            var star       = W("Star", 92);

            var singles = HeatEconomy.ForMatch(challenger, star, 4.0, FinishWeight.Decisive);
            var three   = HeatEconomy.ForMatch(challenger, star, 4.0, FinishWeight.Decisive,
                                               sideCount: 3);

            double winRatio  = three.Winner.OvernessDelta / singles.Winner.OvernessDelta;
            double lossRatio = three.Loser.OvernessDelta  / singles.Loser.OvernessDelta;

            output.WriteLine($"  winner keeps {winRatio:P0} of the gain, " +
                             $"loser {lossRatio:P0} of the damage");

            Assert.Equal(winRatio, lossRatio, 2);
        }

        /// <summary>
        /// A two-side match is untouched, whatever its size. Trios are six people and still
        /// two sides — doc §2.5 is explicit that they are not a multi-man match, because
        /// nobody has to be disposed of.
        /// </summary>
        [Fact]
        public void ATwoSidedMatchIsUnaffected_HoweverManyPeopleAreInIt()
        {
            var a = W("A", 70);
            var b = W("B", 70);

            var implicitTwo = HeatEconomy.ForMatch(a, b, 3.5, FinishWeight.Decisive);
            var explicitTwo = HeatEconomy.ForMatch(a, b, 3.5, FinishWeight.Decisive, sideCount: 2);

            Assert.Equal(implicitTwo.Winner.OvernessDelta, explicitTwo.Winner.OvernessDelta, 6);
            Assert.Equal(implicitTwo.Loser.OvernessDelta,  explicitTwo.Loser.OvernessDelta, 6);
        }

        /// <summary>
        /// And the discount is about the number of *ways the match could go*, not about the
        /// finish being scrappy — those are separate facts and multiply separately. A clean
        /// pin in a three-way is decisive in the first sense and inconclusive in the second.
        /// </summary>
        [Fact]
        public void ConclusivenessIsSeparateFromHowTheMatchEnded()
        {
            var challenger = W("Challenger", 55);
            var star       = W("Star", 92);

            double Clean(int sides) => HeatEconomy
                .ForMatch(challenger, star, 4.0, FinishWeight.Decisive, sideCount: sides)
                .Winner.OvernessDelta;
            double Rollup(int sides) => HeatEconomy
                .ForMatch(challenger, star, 4.0, FinishWeight.Fluke, sideCount: sides)
                .Winner.OvernessDelta;

            output.WriteLine($"  clean  : singles {Clean(2):F2} · three-way {Clean(3):F2}");
            output.WriteLine($"  rollup : singles {Rollup(2):F2} · three-way {Rollup(3):F2}");

            // A clean three-way pin still beats a singles roll-up: it is a real finish.
            Assert.True(Clean(3) > Rollup(2),
                "A clean win in a three-way should say more than a roll-up in a singles match.");

            // And both axes bite independently.
            Assert.True(Rollup(3) < Rollup(2));
            Assert.True(Clean(3) < Clean(2));
        }
        /// <summary>
        /// **The hookup**, which is the half that direct calls to `ForMatch` cannot cover.
        ///
        /// A mutation making `ShowSimulator` always pass `sideCount: 2` passed every test in
        /// this class, because they all called the economy directly. That is the same gap
        /// A3's first review round found four of — every test drove the model and nothing
        /// checked that anything called it.
        ///
        /// This runs a real three-way through a real show and asserts the overness the
        /// winner actually received is the discounted figure, by computing both candidates
        /// from the star rating the match actually produced.
        /// </summary>
        [Fact]
        public void AShowAppliesTheDiscount_NotJustTheEconomyWhenAskedDirectly()
        {
            var day = new DateOnly(2026, 3, 1);
            var challenger = W("Challenger", 55);
            var star       = W("Star", 92);
            var third      = W("Third", 70);

            var career = new Models.World.Career
            {
                Promotion   = new Models.World.Promotion { Name = "Mid-South" },
                StartDate   = day,
                CurrentDate = day,
                Roster      = [challenger, star, third]
            };

            double overnessBefore = challenger.Overness;

            var show = career.Schedule("Saturday", day, ShowType.HouseShow);
            show.Card.Add(new BookedMatch
            {
                Plan = new Models.MatchPlan.MatchPlan
                {
                    Sides =
                    [
                        Models.MatchPlan.MatchSide.Of(challenger),
                        Models.MatchPlan.MatchSide.Of(star),
                        Models.MatchPlan.MatchSide.Of(third)
                    ],
                    Beats =
                    [
                        new Models.MatchPlan.MatchBeat { Type = BeatType.HotOpening,
                                                         Control = BeatControl.Even },
                        new Models.MatchPlan.MatchBeat { Type = BeatType.FinishClean,
                                                         Control = BeatControl.WrestlerA,
                                                         Against = BeatControl.WrestlerB }
                    ]
                }
            });

            var result = new ShowSimulator(career.FeudBook).Simulate(show.ToShow());
            double gained = challenger.Overness - overnessBefore;

            // What the same result would have paid at each side count, using the star rating
            // this match actually produced — and **pristine copies of the wrestlers**, because
            // the show has already moved the real ones and both the upset gap and the
            // approaching-the-ceiling damping read current overness. Recomputing against the
            // post-match objects put the answer between the two candidates and looked like the
            // discount being half-applied.
            double stars = result.Items.Single().MatchResult!.StarRating;
            double Candidate(int sides) => HeatEconomy.ForMatch(
                W("Challenger", 55), W("Star", 92), stars, FinishWeight.Decisive,
                sideCount: sides).Winner.OvernessDelta;

            double asThreeWay = Candidate(3);
            double asSingles  = Candidate(2);

            output.WriteLine($"  {stars:F2} stars — challenger gained {gained:F3}");
            output.WriteLine($"  a three-way would pay {asThreeWay:F3}, a singles {asSingles:F3}");

            Assert.True(asSingles > asThreeWay, "The two candidates have to differ, or this proves nothing.");
            Assert.Equal(asThreeWay, gained, 2);
        }

    }
}
