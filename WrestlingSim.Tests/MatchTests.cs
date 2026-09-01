using WrestlingSim.Enums;
using WrestlingSim.Models;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests;

public class MatchTests
{
    private const int Repetitions = 500;

    [Theory]
    [InlineData(MatchType.Standard)]
    [InlineData(MatchType.Technical)]
    public void CalculateMatchRating_StaysOnTheZeroToFiveScaleAcrossTheCharismaRange(MatchType type)
    {
        // Charisma applies a random modifier, so the clamp is only meaningful over many runs.
        foreach (double charisma in new[] { 0.0, 1.5, 3.0, 4.5, 5.0 })
        {
            var match = new Match(
                TestData.Wrestler(charisma: charisma, skills: TestData.Skills(5, 5, 5, 5, 5, 5)),
                TestData.Wrestler(charisma: charisma, skills: TestData.Skills(0, 0, 0, 0, 0, 0)),
                type);

            for (int i = 0; i < Repetitions; i++)
                Assert.InRange(match.CalculateMatchRating(), 0.0, 5.0);
        }
    }

    [Fact]
    public void CalculateWinner_AlwaysReturnsOneOfTheTwoCompetitors()
    {
        var a = TestData.Wrestler(gimmick: new Gimmick("Alpha"), popularity: 70);
        var b = TestData.Wrestler(gimmick: new Gimmick("Beta"), popularity: 30);
        var match = new Match(a, b);

        for (int i = 0; i < Repetitions; i++)
        {
            (string winner, string loser) = match.CalculateWinner();

            Assert.Contains(winner, new[] { "Alpha", "Beta" });
            Assert.Contains(loser, new[] { "Alpha", "Beta" });
            Assert.NotEqual(winner, loser);
        }
    }

    [Fact]
    public void CalculateWinner_FavoursThePopularWrestlerWithoutMakingTheOutcomeCertain()
    {
        var a = TestData.Wrestler(gimmick: new Gimmick("Alpha"), popularity: 90);
        var b = TestData.Wrestler(gimmick: new Gimmick("Beta"), popularity: 10);
        var match = new Match(a, b);

        int alphaWins = 0;
        for (int i = 0; i < Repetitions; i++)
        {
            if (match.CalculateWinner().Item1 == "Alpha")
                alphaWins++;
        }

        Assert.InRange(alphaWins, Repetitions / 2, Repetitions - 1);
    }

    [Fact]
    public void CalculateMatchRating_DefaultsToTheStandardMatchType()
    {
        var match = new Match(TestData.Wrestler(), TestData.Wrestler());

        Assert.Equal(MatchType.Standard, match.Type);
    }
}
