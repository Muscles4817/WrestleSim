using WrestlingSim.Enums;
using WrestlingSim.Models;

namespace WrestlingSim.Tests;

public class DataLoaderTests
{
    [Fact]
    public void LoadWrestlers_ResolvesABareFileNameWithoutAnAbsolutePath()
    {
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers("Wrestlers.json");

        Assert.True(wrestlers.Count >= 2, "The roster needs at least two wrestlers to book a match.");
    }

    [Fact]
    public void LoadWrestlers_GivesEveryWrestlerAGimmickAndARingName()
    {
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers("Wrestlers.json");

        Assert.All(wrestlers, w =>
        {
            Assert.NotNull(w.Gimmick);
            Assert.False(string.IsNullOrWhiteSpace(w.RingName));
            Assert.False(string.IsNullOrWhiteSpace(w.RealName));
        });
    }

    [Fact]
    public void LoadWrestlers_ParsesFanGroupAppealIntoTheEnumRatherThanLeavingItUntyped()
    {
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers("Wrestlers.json");

        List<FanGroupAppeal> appeal = wrestlers
            .Where(w => w.Gimmick?.AppealRatings is { Count: > 0 })
            .SelectMany(w => w.Gimmick!.AppealRatings)
            .ToList();

        Assert.NotEmpty(appeal);
        Assert.All(appeal, a =>
        {
            Assert.True(Enum.IsDefined(a.Group), $"'{a.Group}' is not a known FanGroup.");
            Assert.InRange(a.AppealScore, 0.0, 1.0);
        });
    }

    [Fact]
    public void LoadWrestlers_ParsesTheRemainingGimmickEnums()
    {
        List<Wrestler> wrestlers = DataLoaders.LoadWrestlers("Wrestlers.json");

        Assert.All(wrestlers, w =>
        {
            Assert.True(Enum.IsDefined(w.Gimmick!.Type));
            Assert.True(Enum.IsDefined(w.Gimmick.Tone));
            Assert.True(Enum.IsDefined(w.Gimmick.Durability));
            Assert.True(Enum.IsDefined(w.Gimmick.NaturalAlignment));
            Assert.True(Enum.IsDefined(w.Style));
        });
    }

    [Fact]
    public void LoadMoves_ReadsTheMoveListRatherThanTheRoster()
    {
        List<Move> moves = DataLoaders.LoadMoves("MoveList.json");

        Assert.NotEmpty(moves);
        Assert.All(moves, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Name));
            Assert.NotNull(m.Types);
            Assert.NotEmpty(m.Types);
        });
        Assert.Contains(moves, m => m.Name == "Superkick");
    }

    [Fact]
    public void ResolvePath_FindsADataFileFromAnyWorkingDirectory()
    {
        string original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.GetTempPath());

            string resolved = DataLoaders.ResolvePath("Wrestlers.json");

            Assert.True(File.Exists(resolved));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Theory]
    [InlineData("wrestlers.json")]
    [InlineData("WRESTLERS.JSON")]
    public void ResolvePath_MatchesFileNamesRegardlessOfCaseOnCaseSensitiveFileSystems(string fileName)
    {
        string resolved = DataLoaders.ResolvePath(fileName);

        Assert.True(File.Exists(resolved));
        Assert.Equal("Wrestlers.json", Path.GetFileName(resolved));
    }

    [Fact]
    public void ResolvePath_ReportsEveryLocationItSearchedWhenAFileIsMissing()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => DataLoaders.ResolvePath("NoSuchFile.json"));

        Assert.Contains("NoSuchFile.json", ex.Message);
        Assert.Contains("Looked in", ex.Message);
    }

    [Fact]
    public void Load_RejectsABlankFileName()
    {
        Assert.Throws<ArgumentException>(() => DataLoaders.LoadWrestlers("  "));
    }
}
