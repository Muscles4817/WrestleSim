using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;

namespace WrestlingSim.Web.Services;

/// <summary>
/// Reads just enough of a save to list it on the landing page.
///
/// Deliberately does not go through SaveSerializer: listing must not need the roster,
/// must not rebuild the object graph, and must survive a save whose body is unreadable.
/// </summary>
internal static class SaveHeader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SaveSlot? Read(string json)
    {
        var dto = JsonSerializer.Deserialize<SaveGame>(json, Options);
        if (dto == null || string.IsNullOrWhiteSpace(dto.CareerId)) return null;

        var tierLabel = new Promotion { Tier = dto.Tier }.TierLabel;

        var date = DateOnly.TryParse(
            dto.CurrentDate,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
                ? parsed
                : DateOnly.FromDateTime(DateTime.Today);

        return new SaveSlot(
            Key: "",                       // filled in by the caller, which knows the key
            CareerId: dto.CareerId,
            PromotionName: string.IsNullOrWhiteSpace(dto.PromotionName) ? "Untitled" : dto.PromotionName,
            TierLabel: tierLabel,
            CurrentDate: date,
            LastPlayedUtc: dto.LastPlayedUtc,
            ShowsRun: dto.Shows.Count(s => s.Result != null));
    }
}
