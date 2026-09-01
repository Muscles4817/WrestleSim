using WrestlingSim.Models;

namespace WrestlingSim.Tests;

public class WrestlerTests
{
    [Fact]
    public void RingName_FollowsTheCurrentGimmicksName()
    {
        var wrestler = TestData.Wrestler(gimmick: new Gimmick("Rocky Maivia"));

        wrestler.ChangeName("The Rock");

        Assert.Equal("The Rock", wrestler.RingName);
    }

    [Fact]
    public void ChangeGimmick_AssignsTheIncomingGimmick()
    {
        var wrestler = TestData.Wrestler(gimmick: new Gimmick("Ringmaster"));
        var austin = new Gimmick("Stone Cold Steve Austin");

        wrestler.ChangeGimmick(austin);

        Assert.Same(austin, wrestler.Gimmick);
        Assert.Equal("Stone Cold Steve Austin", wrestler.RingName);
    }

    [Fact]
    public void ChangeGimmick_ArchivesTheOutgoingGimmick()
    {
        var ringmaster = new Gimmick("Ringmaster");
        var wrestler = TestData.Wrestler(gimmick: ringmaster);

        wrestler.ChangeGimmick(new Gimmick("Stone Cold Steve Austin"));

        Assert.Same(ringmaster, Assert.Single(wrestler.PreviousGimmicks));
    }

    [Fact]
    public void ChangeGimmick_IgnoresNull()
    {
        var original = new Gimmick("Doink");
        var wrestler = TestData.Wrestler(gimmick: original);

        wrestler.ChangeGimmick(null!);

        Assert.Same(original, wrestler.Gimmick);
        Assert.Empty(wrestler.PreviousGimmicks);
    }

    [Fact]
    public void ChangeGimmick_IgnoresReassigningTheGimmickAlreadyInUse()
    {
        var original = new Gimmick("Goldust");
        var wrestler = TestData.Wrestler(gimmick: original);

        wrestler.ChangeGimmick(original);

        Assert.Same(original, wrestler.Gimmick);
        Assert.Empty(wrestler.PreviousGimmicks);
    }

    [Fact]
    public void PreviousNames_SpansBothArchivedGimmicksAndArchivedNames()
    {
        var first = new Gimmick("Rocky Maivia");
        first.ChangeName("The Rock");

        var wrestler = TestData.Wrestler(gimmick: first);
        wrestler.ChangeGimmick(new Gimmick("The Corporate Champion"));

        Assert.Equal(
            new[] { "The Rock", "Rocky Maivia", "The Corporate Champion" },
            wrestler.PreviousNames());
    }
}
