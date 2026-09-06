using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

public static class DataLoaders
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ── Parsing ──────────────────────────────────────────────────────────────
    // Split from the loaders so every host shares one deserialisation setup.

    public static List<Wrestler> ParseWrestlers(string json) =>
        JsonSerializer.Deserialize<List<Wrestler>>(json, Options) ?? new List<Wrestler>();

    public static List<Move> ParseMoves(string json) =>
        JsonSerializer.Deserialize<List<Move>>(json, Options) ?? new List<Move>();

    public static List<TagTeamSeed> ParseTagTeams(string json) =>
        JsonSerializer.Deserialize<List<TagTeamSeed>>(json, Options) ?? new List<TagTeamSeed>();

    // ── File loading ─────────────────────────────────────────────────────────
    // For hosts with a filesystem. Reads the copy next to the executable, so the
    // data can be edited without a rebuild.

    public static List<Wrestler> LoadWrestlers(string filePath) =>
        ParseWrestlers(ReadDataFile(filePath));

    public static List<Move> LoadMoves(string filePath) =>
        ParseMoves(ReadDataFile(filePath));

    public static List<TagTeamSeed> LoadTagTeams(string filePath) =>
        ParseTagTeams(ReadDataFile(filePath));

    private static string ReadDataFile(string filePath) =>
        File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JSON", filePath));

    // ── Embedded loading ─────────────────────────────────────────────────────
    // For hosts without a filesystem, such as the WebAssembly build. Same physical
    // JSON file, compiled into the assembly — no fetch and no base-path concerns.

    public static List<Wrestler> LoadEmbeddedWrestlers() =>
        ParseWrestlers(ReadEmbedded("Wrestlers.json"));

    public static List<Move> LoadEmbeddedMoves() =>
        ParseMoves(ReadEmbedded("MoveList.json"));

    /// <summary>
    /// The standing tag teams a new career starts with. Bound to roster instances by
    /// <see cref="TagTeamSeed.SeedDefaults"/> — on their own these are just names.
    /// </summary>
    public static List<TagTeamSeed> LoadEmbeddedTagTeams() =>
        ParseTagTeams(ReadEmbedded("TagTeams.json"));

    private static string ReadEmbedded(string fileName)
    {
        var assembly = typeof(DataLoaders).Assembly;
        string suffix = "JSON." + fileName;

        string resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException(
                $"Embedded data file '{fileName}' not found. Known resources: " +
                string.Join(", ", assembly.GetManifestResourceNames()));

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
