using System.Text.Json;
using System.Text.Json.Serialization;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;

// Both of these live in a namespace of the same name, so the bare name resolves to
// the namespace. Alias them so the code below can read naturally.
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using SegmentModel = WrestlingSim.Models.Segment.Segment;
using SegmentActionModel = WrestlingSim.Models.Segment.SegmentAction;

namespace WrestlingSim.Persistence
{
    /// <summary>
    /// Converts a live <see cref="Career"/> to and from its serialisable form.
    ///
    /// Loading takes the roster it should bind against, because the save stores people by
    /// id rather than by value. Anyone in the save who is not in the supplied roster is
    /// dropped, and anything referring to them is dropped with them — a save must never
    /// resurrect a half-built wrestler.
    /// </summary>
    public static class SaveSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // ── Text ─────────────────────────────────────────────────────────────

        public static string ToJson(Career career, bool indented = false) =>
            JsonSerializer.Serialize(ToDto(career), new JsonSerializerOptions(Options) { WriteIndented = indented });

        /// <summary>
        /// Rebuilds a career from JSON. Throws <see cref="SaveLoadException"/> for anything
        /// a caller could reasonably show the player, rather than a raw JSON exception.
        /// </summary>
        public static Career FromJson(string json, IReadOnlyList<Wrestler> roster)
        {
            SaveGame? dto;
            try
            {
                dto = JsonSerializer.Deserialize<SaveGame>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new SaveLoadException("That file is not a readable save.", ex);
            }

            if (dto == null)
                throw new SaveLoadException("That save file is empty.");

            if (dto.Version > SaveGame.CurrentVersion)
                throw new SaveLoadException(
                    $"That save was made by a newer version of the game (save v{dto.Version}, " +
                    $"this build reads v{SaveGame.CurrentVersion}).");

            return FromDto(dto, roster);
        }

        // ── Career → DTO ─────────────────────────────────────────────────────

        public static SaveGame ToDto(Career career) => new()
        {
            Version       = SaveGame.CurrentVersion,
            CareerId      = career.Id,
            PromotionName = career.Promotion.Name,
            Tier          = career.Promotion.Tier,
            CurrentDate   = Iso(career.CurrentDate),
            StartDate     = Iso(career.StartDate),
            LastPlayedUtc = career.LastPlayedUtc,

            // Only state that actually changes. Everything else comes from the roster.
            Wrestlers = career.Roster
                .Select(w => new WrestlerStateDto { Id = w.Id, Popularity = w.Popularity })
                .ToList(),

            Feuds = career.FeudBook.AllIncludingDormant
                .Select(f => new FeudDto
                {
                    WrestlerA  = f.WrestlerA.Id,
                    WrestlerB  = f.WrestlerB.Id,
                    Heat       = f.Heat,
                    MatchCount = f.MatchCount,
                    History    = new List<FeudHistoryTag>(f.History)
                })
                .ToList(),

            Shows = career.Shows.Select(ToDto).ToList()
        };

        private static ShowDto ToDto(ScheduledShow show) => new()
        {
            Id             = show.Id,
            Name           = show.Name,
            Date           = Iso(show.Date),
            Type           = show.Type,
            Venue          = show.Venue,
            RuntimeMinutes = show.RuntimeMinutes,
            Attendance     = show.Attendance,
            Card           = show.Card.Select(ToDto).Where(c => c != null).Select(c => c!).ToList(),
            Result         = show.Result == null ? null : ToDto(show.Result)
        };

        private static CardItemDto? ToDto(ICardItem item) => item switch
        {
            BookedMatch m => new CardItemDto
            {
                Kind          = CardItemKind.Match,
                WrestlerA     = m.Plan.WrestlerA.Id,
                WrestlerB     = m.Plan.WrestlerB.Id,
                MatchType     = m.Plan.MatchType,
                StructureName = m.StructureName,
                Beats = m.Plan.Beats.Select(b => new BeatDto
                {
                    Type      = b.Type,
                    Control   = b.Control,
                    Intensity = b.Intensity,
                    Duration  = b.Duration,
                    StyleHint = b.StyleHint
                }).ToList()
            },

            SegmentModel s => new CardItemDto
            {
                Kind         = CardItemKind.Segment,
                SegmentName  = s.Name,
                SegmentType  = s.Type,
                Location     = s.Location,
                IsScripted   = s.IsScripted,
                Participants = s.Participants.Select(p => p.Id).ToList(),
                HistoryTags  = new List<FeudHistoryTag>(s.HistoryTags),
                Actions = s.Actions.Select(a => new SegmentActionDto
                {
                    ActionType     = a.ActionType,
                    Performer      = a.Performer.Id,
                    Target         = a.Target?.Id,
                    Dialogue       = a.Dialogue,
                    HeatImpact     = a.HeatImpact,
                    OvernessImpact = a.OvernessImpact,
                    BaseImpact     = a.BaseImpact,
                    Label          = a.Label
                }).ToList()
            },

            _ => null
        };

        private static ShowResultDto ToDto(ShowResult result) => new()
        {
            OverallRating  = result.OverallRating,
            FinalCrowdMood = result.FinalCrowdMood,
            OverrunPenalty = result.OverrunPenalty,
            BookedMinutes  = result.BookedMinutes,
            BudgetMinutes  = result.BudgetMinutes,
            Items = result.Items.Select(i => new CardItemResultDto
            {
                Label           = i.Label,
                Kind            = i.Kind,
                DurationMinutes = i.DurationMinutes,
                Score           = i.Score,
                StarRating      = i.StarRating,
                Notes           = new List<string>(i.Notes)
            }).ToList()
        };

        // ── DTO → Career ─────────────────────────────────────────────────────

        public static Career FromDto(SaveGame dto, IReadOnlyList<Wrestler> roster)
        {
            var byId = roster.ToDictionary(w => w.Id, StringComparer.OrdinalIgnoreCase);

            // Apply saved state onto the freshly-loaded roster.
            foreach (var state in dto.Wrestlers)
                if (byId.TryGetValue(state.Id, out var w))
                    w.Popularity = Math.Clamp(state.Popularity, 0, 100);

            var career = new Career
            {
                Id          = string.IsNullOrWhiteSpace(dto.CareerId) ? Guid.NewGuid().ToString("N") : dto.CareerId,
                Promotion   = new Promotion { Name = dto.PromotionName, Tier = dto.Tier },
                CurrentDate = ParseDate(dto.CurrentDate),
                StartDate   = ParseDate(dto.StartDate),
                Roster      = roster.ToList(),
                LastPlayedUtc = dto.LastPlayedUtc
            };

            foreach (var f in dto.Feuds)
            {
                if (!byId.TryGetValue(f.WrestlerA, out var a)) continue;
                if (!byId.TryGetValue(f.WrestlerB, out var b)) continue;

                var feud = career.FeudBook.GetOrCreate(a, b);
                feud.RestoreHeat(f.Heat);
                feud.MatchCount = f.MatchCount;
                foreach (var tag in f.History) feud.AddTag(tag);
            }

            foreach (var s in dto.Shows)
                career.Shows.Add(FromDto(s, byId, career.FeudBook));

            return career;
        }

        private static ScheduledShow FromDto(
            ShowDto dto, Dictionary<string, Wrestler> byId, FeudBook feudBook)
        {
            var show = new ScheduledShow
            {
                Id             = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
                Name           = dto.Name,
                Date           = ParseDate(dto.Date),
                Type           = dto.Type,
                Venue          = dto.Venue,
                RuntimeMinutes = dto.RuntimeMinutes > 0 ? dto.RuntimeMinutes : 120,
                Attendance     = dto.Attendance
            };

            foreach (var item in dto.Card)
            {
                var built = FromDto(item, byId, feudBook);
                if (built != null) show.Card.Add(built);
            }

            if (dto.Result != null) show.Result = FromDto(dto.Result);

            return show;
        }

        private static ICardItem? FromDto(
            CardItemDto dto, Dictionary<string, Wrestler> byId, FeudBook feudBook)
        {
            if (dto.Kind == CardItemKind.Match)
            {
                if (dto.WrestlerA == null || dto.WrestlerB == null) return null;
                if (!byId.TryGetValue(dto.WrestlerA, out var a)) return null;
                if (!byId.TryGetValue(dto.WrestlerB, out var b)) return null;

                var plan = new MatchPlanModel
                {
                    WrestlerA = a,
                    WrestlerB = b,
                    MatchType = dto.MatchType,
                    // Re-bind to the live feud so a reloaded card reads current heat.
                    Feud      = feudBook.Find(a, b),
                    Beats = dto.Beats.Select(x => new MatchBeat
                    {
                        Type      = x.Type,
                        Control   = x.Control,
                        Intensity = x.Intensity,
                        Duration  = x.Duration,
                        StyleHint = x.StyleHint
                    }).ToList()
                };

                return new BookedMatch { Plan = plan, StructureName = dto.StructureName };
            }

            var segment = new SegmentModel(
                dto.SegmentName ?? "Segment", dto.SegmentType, dto.Location, dto.IsScripted);

            foreach (var id in dto.Participants)
                if (byId.TryGetValue(id, out var p)) segment.AddParticipant(p);

            foreach (var a in dto.Actions)
            {
                if (!byId.TryGetValue(a.Performer, out var performer)) continue;

                Wrestler? target = null;
                if (a.Target != null) byId.TryGetValue(a.Target, out target);

                segment.AddAction(new SegmentActionModel
                {
                    ActionType     = a.ActionType,
                    Performer      = performer,
                    Target         = target,
                    Dialogue       = a.Dialogue,
                    HeatImpact     = a.HeatImpact,
                    OvernessImpact = a.OvernessImpact,
                    BaseImpact     = a.BaseImpact,
                    Label          = a.Label
                });
            }

            segment.HistoryTags.AddRange(dto.HistoryTags);

            // A segment whose cast did not survive the roster is not bookable.
            return segment.Participants.Count == 0 ? null : segment;
        }

        private static ShowResult FromDto(ShowResultDto dto) => new()
        {
            OverallRating  = dto.OverallRating,
            FinalCrowdMood = dto.FinalCrowdMood,
            OverrunPenalty = dto.OverrunPenalty,
            BookedMinutes  = dto.BookedMinutes,
            BudgetMinutes  = dto.BudgetMinutes,
            Items = dto.Items.Select(i => new CardItemResult
            {
                Label           = i.Label,
                Kind            = i.Kind,
                DurationMinutes = i.DurationMinutes,
                Score           = i.Score,
                Notes           = new List<string>(i.Notes)
            }).ToList()
        };

        // ── Dates ────────────────────────────────────────────────────────────
        // Stored as plain ISO strings so a save stays readable and stays stable
        // regardless of the host's culture settings.

        private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

        private static DateOnly ParseDate(string? value) =>
            DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                              System.Globalization.DateTimeStyles.None, out var d)
                ? d
                : DateOnly.FromDateTime(DateTime.Today);
    }

    /// <summary>A save could not be read, with a message fit to show the player.</summary>
    public class SaveLoadException : Exception
    {
        public SaveLoadException(string message, Exception? inner = null) : base(message, inner) { }
    }
}
