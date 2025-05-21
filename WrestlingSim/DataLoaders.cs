using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Models;

public static class DataLoaders
{
    public static List<Wrestler> LoadWrestlers(string filePath)
    {
        //string json = File.ReadAllText(@"C:\Users\mjmak\source\repos\WrestlingSim\WrestlingSim\JSON\Wrestlers.json");
        string json = File.ReadAllText(@"C:\Users\Callum\Source\Repos\WrestleSim\WrestlingSim\JSON\Wrestlers.json");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<Wrestler>>(json, options);
    }

    public static List<Move> LoadMoves(string filePath)
    {
        //string json = File.ReadAllText(@"C:\Users\mjmak\source\repos\WrestlingSim\WrestlingSim\JSON\MoveList.json");
        string json = File.ReadAllText(@"C:\Users\Callum\Source\Repos\WrestleSim\WrestlingSim\JSON\MoveList.json");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<Move>>(json, options);
    }
}



