using WrestlingSim.Models;

namespace WrestlingSim.Tests;

public class GimmickTests
{
    [Fact]
    public void ChangeName_SetsTheNewNameAsCurrent()
    {
        var gimmick = new Gimmick("Rocky Maivia");

        gimmick.ChangeName("The Rock");

        Assert.Equal("The Rock", gimmick.Name);
    }

    [Fact]
    public void ChangeName_ArchivesTheOutgoingName()
    {
        var gimmick = new Gimmick("Rocky Maivia");

        gimmick.ChangeName("The Rock");

        Assert.Equal(new[] { "Rocky Maivia" }, gimmick.PreviousNames);
    }

    [Fact]
    public void ChangeName_ArchivesEveryNameInOrderAcrossRepeatedRenames()
    {
        var gimmick = new Gimmick("Terra Ryzing");

        gimmick.ChangeName("Hunter Hearst Helmsley");
        gimmick.ChangeName("Triple H");

        Assert.Equal(new[] { "Terra Ryzing", "Hunter Hearst Helmsley" }, gimmick.PreviousNames);
        Assert.Equal("Triple H", gimmick.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ChangeName_IgnoresBlankInput(string? blank)
    {
        var gimmick = new Gimmick("Mankind");

        gimmick.ChangeName(blank!);

        Assert.Equal("Mankind", gimmick.Name);
        Assert.Empty(gimmick.PreviousNames);
    }

    [Fact]
    public void ChangeName_IgnoresARenameToTheSameName()
    {
        var gimmick = new Gimmick("Kane");

        gimmick.ChangeName("Kane");

        Assert.Equal("Kane", gimmick.Name);
        Assert.Empty(gimmick.PreviousNames);
    }

    [Fact]
    public void ParameterlessConstructor_LeavesNoNullCollections()
    {
        var gimmick = new Gimmick();

        Assert.NotNull(gimmick.PreviousNames);
        Assert.NotNull(gimmick.GimmickTraits);
        Assert.NotNull(gimmick.AppealRatings);
    }

    [Fact]
    public void ChangeName_DoesNotThrowForAGimmickBuiltByTheParameterlessConstructor()
    {
        var gimmick = new Gimmick { Name = "Husky Harris" };

        gimmick.ChangeName("Bray Wyatt");

        Assert.Equal("Bray Wyatt", gimmick.Name);
        Assert.Equal(new[] { "Husky Harris" }, gimmick.PreviousNames);
    }
}
