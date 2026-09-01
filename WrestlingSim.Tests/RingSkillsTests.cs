using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Tests;

public class RingSkillsTests
{
    [Fact]
    public void GetTechnicalScore_WeightsTechnicalAndGrapplingAndPenalisesBrawling()
    {
        // 4*0.5 + 3*0.3 + 2*0.1 - 1*0.1 = 2.0 + 0.9 + 0.2 - 0.1
        var skills = new RingSkills(
            highFlyer: 0, grappler: 3, powerHouse: 0,
            technical: 4, brawler: 1, striker: 2);

        Assert.Equal(3.0, skills.GetTechnicalScore(), precision: 2);
    }

    [Fact]
    public void GetTechnicalScore_NeverGoesBelowZeroForAPureBrawler()
    {
        var skills = new RingSkills(
            highFlyer: 0, grappler: 0, powerHouse: 0,
            technical: 0, brawler: 5, striker: 0);

        Assert.Equal(0.0, skills.GetTechnicalScore());
    }

    [Fact]
    public void GetStandardScore_BlendsTheTopSkillsWithTheChosenSpeciality()
    {
        // base = (4 + 3 + 2 + 3) / 4 = 3.0 ; speciality = Powerhouse = 4
        // 3.0 * 0.7 + 4 * 0.3 = 2.1 + 1.2
        var skills = new RingSkills(
            highFlyer: 2, grappler: 3, powerHouse: 4,
            technical: 3, brawler: 2, striker: 1);

        Assert.Equal(3.3, skills.GetStandardScore(WrestlingStyle.Powerhouse), precision: 2);
    }

    [Fact]
    public void GetStandardScore_RewardsMatchingTheSpecialityToTheWrestlersBestSkill()
    {
        var skills = new RingSkills(
            highFlyer: 5, grappler: 2, powerHouse: 2,
            technical: 2, brawler: 2, striker: 2);

        double onSpeciality = skills.GetStandardScore(WrestlingStyle.HighFlyer);
        double offSpeciality = skills.GetStandardScore(WrestlingStyle.Grappler);

        Assert.True(onSpeciality > offSpeciality,
            $"Playing to the speciality should score higher ({onSpeciality} vs {offSpeciality}).");
    }

    [Fact]
    public void GetStyleProficiency_FallsBackToTheOverallSkillForAnUnsetStyle()
    {
        var skills = new RingSkills(1, 2, 3, 4, 5, 6);

        Assert.Equal(skills.GetOverallSkill(), skills.GetStyleProficiency(WrestlingStyle.Null));
    }
}
