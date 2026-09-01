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
                .Select(w => new WrestlerStateDto
                {
                    Id             = w.Id,
                    Overness       = Math.Round(w.Overness, 3),
                    Momentum       = Math.Round(w.Momentum, 3),
                    LastAppearance = w.LastAppearance is { } seen ? Iso(seen) : null
                })
                .ToList(),

            ShowDefinitions = career.ShowDefinitions.Select(d => new ShowDefinitionDto
            {
                Id             = d.Id,
                Name           = d.Name,
                Type           = d.Type,
                Recurrence     = d.Recurrence,
                Day            = d.Day,
                Ordinal        = d.Ordinal,
                Venue          = d.Venue,
                RuntimeMinutes = d.RuntimeMinutes,
                Active         = d.Active
            }).ToList(),

            Feuds = career.FeudBook.AllIncludingDormant
                .Select(f => new FeudDto
                {
                    WrestlerA          = f.WrestlerA.Id,
                    WrestlerB          = f.WrestlerB.Id,
                    Heat               = f.Heat,
                    MatchCount         = f.MatchCount,
                    RememberedMeetings = f.RememberedMeetings,
                    LastMatchDate      = f.LastMatchDate is { } met ? Iso(met) : null,
                    History            = new List<FeudHistoryTag>(f.History)
                })
                .ToList(),

            Shows = career.Shows.Select(ToDto).ToList(),

            Titles = career.Titles.All.Select(t => new TitleDto
            {
                Id          = t.Id,
                Name        = t.Name,
                Tier        = t.Tier,
                Division    = t.Division,
                Established = Iso(t.Established),
                Standing    = Math.Round(t.Standing, 3),
                Retired     = t.Retired,
                RetiredOn   = t.RetiredOn is { } retired ? Iso(retired) : null,
                Lineage = t.Lineage.Select(r => new TitleReignDto
                {
                    // By id, never by value — the champion is a live roster instance.
                    Champion     = r.Champion.Id,
                    ReignNumber  = r.ReignNumber,
                    Won          = Iso(r.Won),
                    Lost         = r.Lost is { } lost ? Iso(lost) : null,
                    LastDefended = r.LastDefended is { } defended ? Iso(defended) : null,
                    WonAt        = r.WonAt,
                    LostAt       = r.LostAt,
                    Defences     = r.Defences,
                    Vacated      = r.Vacated
                }).ToList()
            }).ToList()
        };

        private static ShowDto ToDto(ScheduledShow show) => new()
        {
            Id             = show.Id,
            DefinitionId   = show.DefinitionId,
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
                TitleId       = m.Plan.TitleAtStake?.Id,
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
            {
                if (!byId.TryGetValue(state.Id, out var w)) continue;

                // ResolvedOverness falls back to the pre-split "Popularity" field, so a
                // save written before the stock/flow split still opens with its roster
                // standings intact rather than silently resetting everyone to zero.
                w.Overness = Math.Clamp(state.ResolvedOverness, 0, 100);
                w.Momentum = Math.Clamp(state.Momentum, -100, 100);
                w.LastAppearance = DateOnly.TryParse(
                    state.LastAppearance, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var seen)
                        ? seen
                        : null;
            }

            var career = new Career
            {
                Id          = string.IsNullOrWhiteSpace(dto.CareerId) ? Guid.NewGuid().ToString("N") : dto.CareerId,
                Promotion   = new Promotion { Name = dto.PromotionName, Tier = dto.Tier },
                CurrentDate = ParseDate(dto.CurrentDate),
                StartDate   = ParseDate(dto.StartDate),
                Roster      = roster.ToList(),
                LastPlayedUtc = dto.LastPlayedUtc
            };

            career.ShowDefinitions.AddRange(dto.ShowDefinitions.Select(d => new ShowDefinition
            {
                Id             = string.IsNullOrWhiteSpace(d.Id) ? Guid.NewGuid().ToString("N") : d.Id,
                Name           = d.Name,
                Type           = d.Type,
                Recurrence     = d.Recurrence,
                Day            = d.Day,
                Ordinal        = d.Ordinal,
                Venue          = d.Venue,
                RuntimeMinutes = d.RuntimeMinutes,
                Active         = d.Active
            }));

            foreach (var f in dto.Feuds)
            {
                if (!byId.TryGetValue(f.WrestlerA, out var a)) continue;
                if (!byId.TryGetValue(f.WrestlerB, out var b)) continue;

                var feud = career.FeudBook.GetOrCreate(a, b);
                feud.RestoreHeat(f.Heat);
                feud.MatchCount = f.MatchCount;

                // A save from before match-count decay has no freshness state. Seeding it
                // from MatchCount treats those meetings as still remembered, which is the
                // conservative reading — the alternative would hand every old save a free
                // reset on every pairing it has already run into the ground.
                feud.RestoreMeetings(
                    f.RememberedMeetings > 0 ? f.RememberedMeetings : f.MatchCount,
                    ParseOptionalDate(f.LastMatchDate));
                foreach (var tag in f.History) feud.AddTag(tag);
            }

            RestoreTitles(dto, career, byId);

            foreach (var s in dto.Shows)
                career.Shows.Add(FromDto(s, byId, career.FeudBook, career.Titles));

            return career;
        }

        /// <summary>
        /// Rebuilds the promotion's belts and rebinds every reign to the live roster
        /// instance rather than a copy.
        ///
        /// A save from before championships existed gets the standard slate seeded at the
        /// career's start date — the alternative is a promotion with no titles at all,
        /// which is not a state the game otherwise lets you reach.
        /// </summary>
        private static void RestoreTitles(SaveGame dto, Career career, Dictionary<string, Wrestler> byId)
        {
            if (dto.Titles.Count == 0)
            {
                if (dto.Version < 2)
                    career.Titles.SeedDefaults(career.Promotion.Name, career.StartDate);
                return;
            }

            foreach (var t in dto.Titles)
            {
                var title = new Title
                {
                    Id          = string.IsNullOrWhiteSpace(t.Id) ? Guid.NewGuid().ToString("N") : t.Id,
                    Name        = t.Name,
                    Tier        = t.Tier,
                    Division    = t.Division,
                    Established = ParseDate(t.Established),
                    Standing    = Math.Clamp(t.Standing, 0, 100),
                    Retired     = t.Retired,
                    RetiredOn   = DateOnly.TryParse(
                        t.RetiredOn, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var retired) ? retired : null
                };

                foreach (var r in t.Lineage)
                {
                    // A reign whose champion is not on this roster is dropped, the same
                    // rule the rest of the save follows: never resurrect a half-built
                    // person. ReignNumber is stored, so the numbering left behind still
                    // reads correctly.
                    if (!byId.TryGetValue(r.Champion, out var champion)) continue;

                    title.Lineage.Add(new TitleReign
                    {
                        Champion     = champion,
                        ReignNumber  = r.ReignNumber,
                        Won          = ParseDate(r.Won),
                        Lost         = ParseOptionalDate(r.Lost),
                        LastDefended = ParseOptionalDate(r.LastDefended),
                        WonAt        = r.WonAt,
                        LostAt       = r.LostAt,
                        Defences     = r.Defences,
                        Vacated      = r.Vacated
                    });
                }

                career.Titles.Add(title);
            }

            career.Titles.Rebalance();
        }

        private static ScheduledShow FromDto(
            ShowDto dto, Dictionary<string, Wrestler> byId, FeudBook feudBook, TitleRegistry titles)
        {
            var show = new ScheduledShow
            {
                Id             = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
                DefinitionId   = dto.DefinitionId,
                Name           = dto.Name,
                Date           = ParseDate(dto.Date),
                Type           = dto.Type,
                Venue          = dto.Venue,
                RuntimeMinutes = dto.RuntimeMinutes > 0 ? dto.RuntimeMinutes : 120,
                Attendance     = dto.Attendance
            };

            foreach (var item in dto.Card)
            {
                var built = FromDto(item, byId, feudBook, titles);
                if (built != null) show.Card.Add(built);
            }

            if (dto.Result != null) show.Result = FromDto(dto.Result);

            return show;
        }

        private static ICardItem? FromDto(
            CardItemDto dto, Dictionary<string, Wrestler> byId, FeudBook feudBook, TitleRegistry titles)
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
                    // Likewise the belt: the same Title instance the registry holds, so a
                    // reloaded card can still put it on the line.
                    TitleAtStake = dto.TitleId is null ? null : titles.Find(dto.TitleId),
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
                StarRating      = i.StarRating,
                Notes           = new List<string>(i.Notes)
            }).ToList()
        };

        // ── Dates ────────────────────────────────────────────────────────────
        // Stored as plain ISO strings so a save stays readable and stays stable
        // regardless of the host's culture settings.

        private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

        private static DateOnly? ParseOptionalDate(string? value) =>
            DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                              System.Globalization.DateTimeStyles.None, out var d)
                ? d
                : null;

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
