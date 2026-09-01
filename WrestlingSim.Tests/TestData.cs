using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Tests;

/// <summary>Builders for wrestlers and gimmicks, so tests state only what they care about.</summary>
internal static class TestData
{
    public static Gimmick Gimmick(string name = "The Prototype") => new(name);

    public static RingSkills Skills(
        double highFlyer = 3, double grappler = 3, double powerhouse = 3,
        double technical = 3, double brawler = 3, double striker = 3) =>
        new(highFlyer, grappler, powerhouse, technical, brawler, striker);

    public static Wrestler Wrestler(
        string realName = "Test Worker",
        Gimmick? gimmick = null,
        int popularity = 50,
        RingSkills? skills = null,
        double charisma = 3.0,
        WrestlingStyle style = WrestlingStyle.Technical) =>
        new(realName, gimmick ?? Gimmick(), popularity, skills ?? Skills(), charisma, style);
}
