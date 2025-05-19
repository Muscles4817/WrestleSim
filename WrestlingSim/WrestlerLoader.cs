using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Models;

public static class WrestlerLoader
{
    public static List<Wrestler> LoadWrestlers(string filePath)
    {
        string json = File.ReadAllText(@"C:\Users\mjmak\source\repos\WrestlingSim\WrestlingSim\JSON\Wrestlers.json");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<List<Wrestler>>(json, options);
    }
}
