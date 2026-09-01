using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Models;

public static class DataLoaders
{
    private const string JsonFolder = "JSON";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<Wrestler> LoadWrestlers(string fileName) =>
        Load<Wrestler>(fileName);

    public static List<Move> LoadMoves(string fileName) =>
        Load<Move>(fileName);

    /// <summary>
    /// Deserialises a list of <typeparamref name="T"/> from a JSON data file.
    /// Accepts a bare file name ("Wrestlers.json"), a path relative to the
    /// working directory, or an absolute path.
    /// </summary>
    public static List<T> Load<T>(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A file name is required.", nameof(fileName));

        string path = ResolvePath(fileName);
        string json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
    }

    /// <summary>
    /// Finds a data file without depending on where the process was launched from.
    /// Probes, in order: the path as given, the JSON folder beside the built
    /// assembly, and the JSON folder in the current working directory.
    /// </summary>
    public static string ResolvePath(string fileName)
    {
        if (Path.IsPathRooted(fileName) && File.Exists(fileName))
            return fileName;

        string assemblyDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;

        var candidates = new[]
        {
            fileName,
            Path.Combine(assemblyDir, fileName),
            Path.Combine(assemblyDir, JsonFolder, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), fileName),
            Path.Combine(Directory.GetCurrentDirectory(), JsonFolder, fileName)
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // Linux and macOS are case-sensitive where Windows is not, so a caller asking for
        // "wrestlers.json" must still find "Wrestlers.json" on every platform.
        foreach (string candidate in candidates)
        {
            string? match = FindIgnoringCase(candidate);
            if (match != null)
                return match;
        }

        throw new FileNotFoundException(
            $"Could not find data file '{fileName}'. Looked in: {string.Join("; ", candidates.Distinct())}",
            fileName);
    }

    private static string? FindIgnoringCase(string candidate)
    {
        string? directory = Path.GetDirectoryName(candidate);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        string target = Path.GetFileName(candidate);

        return Directory
            .EnumerateFiles(directory)
            .FirstOrDefault(f => string.Equals(
                Path.GetFileName(f), target, StringComparison.OrdinalIgnoreCase));
    }
}
