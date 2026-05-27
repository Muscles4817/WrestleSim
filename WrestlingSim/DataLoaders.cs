using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Models;

public static class DataLoaders
{
    public static List<Wrestler> LoadWrestlers(string filePath)
    {
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JSON", filePath);
        string json = File.ReadAllText(fullPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        List<Wrestler> wrestlers = JsonSerializer.Deserialize<List<Wrestler>>(json, options);

        // List<Move> moves = LoadMoves("MoveList.json");

        return wrestlers;
    }

    public static List<Move> LoadMoves(string filePath)
    {
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JSON", filePath);
        string json = File.ReadAllText(fullPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Deserialize<List<Move>>(json, options);
    }
}



