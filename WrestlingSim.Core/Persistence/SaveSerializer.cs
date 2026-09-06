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
                Active         = d.Active,
                BrandId        = d.BrandId
            }).ToList(),

            Brands = ToDto(career.Brands),

            Teams = career.Teams.Select(t => new TagTeamDto
            {
                Id              = t.Id,
                Name            = t.Name,
                Members         = t.Members.Select(w => w.Id).ToList(),
                Formed          = Iso(t.Formed),
                Disbanded       = t.Disbanded is { } d ? Iso(d) : null,
                MatchesTogether = t.MatchesTogether,
                LastTeamed      = t.LastTeamed is { } l ? Iso(l) : null,
                DecayedTo       = t.DecayedTo is { } dt ? Iso(dt) : null,
                Chemistry       = Math.Round(t.Chemistry, 4)
            }).ToList(),

            Feuds = career.FeudBook.AllIncludingDormant
                .Select(f => new FeudDto
                {
                    SideA              = f.SideA.Select(w => w.Id).ToList(),
                    SideB              = f.SideB.Select(w => w.Id).ToList(),
                    Heat               = f.Heat,
                    MatchCount         = f.MatchCount,
                    RememberedMeetings = f.RememberedMeetings,
                    LastMatchDate      = f.LastMatchDate is { } met ? Iso(met) : null,
                    LastAdvanced       = f.LastAdvanced is { } adv ? Iso(adv) : null,
                    DecayedTo          = f.DecayedTo is { } dec ? Iso(dec) : null,
                    Concluded          = f.Concluded,
                    ConcludedOn        = f.ConcludedOn is { } con ? Iso(con) : null,
                    MatchesSinceHot    = f.MatchesSinceHot,
                    ChaptersSettled    = f.ChaptersSettled,
                    Distrust           = Math.Round(f.Distrust, 4),
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
                SideSize    = t.SideSize,
                Standing    = Math.Round(t.Standing, 3),
                Retired     = t.Retired,
                RetiredOn   = t.RetiredOn is { } retired ? Iso(retired) : null,
                Lineage = t.Lineage.Select(r => new TitleReignDto
                {
                    // By id, never by value — champions are live roster instances.
                    Champions    = r.Champions.Select(c => c.Id).ToList(),
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

        /// <summary>
        /// The split, or null when the promotion has never divided — so a save from a
        /// promotion with no brands looks exactly as it did before this existed.
        /// </summary>
        private static BrandSplitDto? ToDto(BrandSplit split)
        {
            if (!split.Active && split.Brands.Count == 0 && split.CrossoverCount == 0) return null;

            return new BrandSplitDto
            {
                Active           = split.Active,
                Integrity        = Math.Round(split.Integrity, 3),
                PermanentErosion = Math.Round(split.PermanentErosion, 3),
                CrossoverCount   = split.CrossoverCount,
                StartedOn        = split.StartedOn is { } s ? Iso(s) : null,
                LastDraftOn      = split.LastDraftOn is { } d ? Iso(d) : null,

                Brands = split.Brands.Select(b => new BrandDto
                {
                    Id            = b.Id,
                    Name          = b.Name,
                    Identity      = b.Identity,
                    Colour        = b.Colour,
                    // By id, never by value — the roster instances belong to the career.
                    RosterIds     = b.RosterIds.ToList(),
                    RecentRatings = b.RecentRatings.Select(r => Math.Round(r, 2)).ToList()
                }).ToList(),

                Crossovers = split.Crossovers.Select(c => new CrossoverDto
                {
                    WrestlerId    = c.WrestlerId,
                    WrestlerName  = c.WrestlerName,
                    HomeBrandName = c.HomeBrandName,
                    ShowBrandName = c.ShowBrandName,
                    ShowName      = c.ShowName,
                    Date          = Iso(c.Date),
                    Cost          = Math.Round(c.Cost, 3)
                }).ToList()
            };
        }

        private static ShowDto ToDto(ScheduledShow show) => new()
        {
            Id             = show.Id,
            DefinitionId   = show.DefinitionId,
            Name           = show.Name,
            Date           = Iso(show.Date),
            Type           = show.Type,
            Venue          = show.Venue,
            BrandId        = show.BrandId,
            RuntimeMinutes = show.RuntimeMinutes,
            Attendance     = show.Attendance,
            Card           = show.Card.Select(ToDto).Where(c => c != null).Select(c => c!).ToList(),
            Result         = show.Result == null ? null : ToDto(show.Result)
        };

        private static CardItemDto? ToDto(ICardItem item) => item switch
        {
            // Sides, not two wrestlers. WrestlerA/WrestlerB are deliberately not written
            // any more — a v2 reader could not have made sense of a tag match anyway, and
            // writing a half-truth would mean a downgrade silently loses the partners.
            BookedMatch m => new CardItemDto
            {
                Kind           = CardItemKind.Match,
                SideA          = m.Plan.SideA.Members.Select(w => w.Id).ToList(),
                SideB          = m.Plan.SideB.Members.Select(w => w.Id).ToList(),
                TeamAId        = m.Plan.SideA.Team?.Id,
                TeamBId        = m.Plan.SideB.Team?.Id,
                StartingIndexA = m.Plan.SideA.StartingIndex,
                StartingIndexB = m.Plan.SideB.StartingIndex,
                MatchType      = m.Plan.MatchType,
                StructureName  = m.StructureName,
                TitleId        = m.Plan.TitleAtStake?.Id,
                IsBlowOff      = m.Plan.IsBlowOff,
                Beats = m.Plan.Beats.Select(b => new BeatDto
                {
                    Type          = b.Type,
                    Control       = b.Control,
                    Intensity     = b.Intensity,
                    Duration      = b.Duration,
                    StyleHint     = b.StyleHint,
                    IncomingIndex = b.IncomingIndex
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
                Active         = d.Active,
                BrandId        = d.BrandId
            }));

            // Teams before cards, because a card item refers to a team by id.
            foreach (var t in dto.Teams)
            {
                var members = Bind(t.Members, byId);
                // A team one of whose members has left the roster is not a team any more.
                if (members is null || members.Count < 2) continue;

                career.Teams.Add(new TagTeam
                {
                    Id              = t.Id,
                    Name            = t.Name,
                    Members         = members,
                    Formed          = ParseDate(t.Formed),
                    Disbanded       = ParseOptionalDate(t.Disbanded),
                    MatchesTogether = t.MatchesTogether,
                    LastTeamed      = ParseOptionalDate(t.LastTeamed),
                    DecayedTo       = ParseOptionalDate(t.DecayedTo),
                    Chemistry       = t.Chemistry
                });
            }

            if (dto.Brands is { } brands) career.Brands = FromDto(brands, byId);

            foreach (var f in dto.Feuds)
            {
                // v3 writes sides; a v2 feud names one wrestler per side.
                var sideAIds = f.SideA ?? (string.IsNullOrEmpty(f.WrestlerA) ? null : [f.WrestlerA]);
                var sideBIds = f.SideB ?? (string.IsNullOrEmpty(f.WrestlerB) ? null : [f.WrestlerB]);
                if (sideAIds is null || sideBIds is null) continue;

                var a = Bind(sideAIds, byId);
                var b = Bind(sideBIds, byId);
                if (a is null || b is null) continue;

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

                // A pre-A3 save has no LastAdvanced. Falling back to LastMatchDate is the
                // conservative reading — the alternative is null, which means "never
                // advanced" and therefore *never decays*, quietly exempting every feud in
                // an old save from the rule this release adds.
                feud.RestoreDecay(
                    ParseOptionalDate(f.LastAdvanced) ?? ParseOptionalDate(f.LastMatchDate),
                    ParseOptionalDate(f.DecayedTo));
                feud.RestoreResolution(
                    f.Concluded, ParseOptionalDate(f.ConcludedOn),
                    f.MatchesSinceHot, f.Distrust, f.ChaptersSettled);

                foreach (var tag in f.History) feud.AddTag(tag);
            }

            RestoreTitles(dto, career, byId);

            foreach (var s in dto.Shows)
                career.Shows.Add(FromDto(s, byId, career.FeudBook, career.Titles, career.Teams));

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
                    // v2 saves have no SideSize; 0 means "not recorded" and every belt in
                    // one of those is a singles belt by definition.
                    SideSize    = t.SideSize <= 0 ? 1 : t.SideSize,
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
                    // v3 writes Champions; a v2 reign names a single holder.
                    var championIds = r.Champions
                        ?? (string.IsNullOrEmpty(r.Champion) ? null : [r.Champion]);
                    if (championIds is null) continue;

                    var champions = Bind(championIds, byId);
                    if (champions is null) continue;

                    title.Lineage.Add(new TitleReign
                    {
                        Champions    = champions,
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

        /// Rebuilds the split. Roster ids that are not in the supplied roster are dropped,
        /// for the same reason a feud with a missing participant is: the save must never
        /// leave a brand holding a person the game cannot produce.
        /// </summary>
        private static BrandSplit FromDto(BrandSplitDto dto, Dictionary<string, Wrestler> byId)
        {
            var split = new BrandSplit
            {
                Active           = dto.Active,
                Integrity        = Math.Clamp(dto.Integrity, 0, 100),
                PermanentErosion = Math.Clamp(dto.PermanentErosion, 0, 100),
                CrossoverCount   = Math.Max(0, dto.CrossoverCount),
                StartedOn        = ParseOptionalDate(dto.StartedOn),
                LastDraftOn      = ParseOptionalDate(dto.LastDraftOn)
            };

            foreach (var b in dto.Brands)
            {
                var brand = new Brand
                {
                    Id       = string.IsNullOrWhiteSpace(b.Id) ? Guid.NewGuid().ToString("N") : b.Id,
                    Name     = b.Name,
                    Identity = b.Identity,
                    Colour   = string.IsNullOrWhiteSpace(b.Colour) ? "#d4af37" : b.Colour
                };

                foreach (var id in b.RosterIds)
                    if (byId.ContainsKey(id)) brand.RosterIds.Add(id);

                brand.RecentRatings.AddRange(b.RecentRatings.TakeLast(Brand.FormWindow));
                split.Brands.Add(brand);
            }

            // Someone who has since left the roster still crossed over at the time, so the
            // ledger keeps them: the erosion happened whether or not they are still here.
            split.Crossovers.AddRange(dto.Crossovers.Select(c => new CrossoverRecord
            {
                WrestlerId    = c.WrestlerId,
                WrestlerName  = c.WrestlerName,
                HomeBrandName = c.HomeBrandName,
                ShowBrandName = c.ShowBrandName,
                ShowName      = c.ShowName,
                Date          = ParseDate(c.Date),
                Cost          = c.Cost
            }));

            // Older saves predate the running total; the ledger is the best count available.
            if (split.CrossoverCount < split.Crossovers.Count)
                split.CrossoverCount = split.Crossovers.Count;

            split.Integrity = Math.Min(split.Integrity, split.Ceiling);
            return split;
        }

        private static ScheduledShow FromDto(
            ShowDto dto, Dictionary<string, Wrestler> byId, FeudBook feudBook,
            TitleRegistry titles, List<TagTeam> teams)
        {
            var show = new ScheduledShow
            {
                Id             = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
                DefinitionId   = dto.DefinitionId,
                Name           = dto.Name,
                Date           = ParseDate(dto.Date),
                Type           = dto.Type,
                Venue          = dto.Venue,
                BrandId        = dto.BrandId,
                RuntimeMinutes = dto.RuntimeMinutes > 0 ? dto.RuntimeMinutes : 120,
                Attendance     = dto.Attendance
            };

            foreach (var item in dto.Card)
            {
                var built = FromDto(item, byId, feudBook, titles, teams);
                if (built != null) show.Card.Add(built);
            }

            if (dto.Result != null) show.Result = FromDto(dto.Result);

            return show;
        }

        /// <summary>
        /// Resolves a saved side against the live roster, or null if anybody is missing.
        /// </summary>
        private static List<Wrestler>? Bind(List<string> ids, Dictionary<string, Wrestler> byId)
        {
            var members = new List<Wrestler>(ids.Count);
            foreach (var id in ids)
            {
                if (!byId.TryGetValue(id, out var w)) return null;
                members.Add(w);
            }
            return members.Count == 0 ? null : members;
        }

        private static ICardItem? FromDto(
            CardItemDto dto, Dictionary<string, Wrestler> byId, FeudBook feudBook,
            TitleRegistry titles, List<TagTeam> teams)
        {
            if (dto.Kind == CardItemKind.Match)
            {
                // v3 writes SideA/SideB; a v2 save has one wrestler per side instead.
                var sideAIds = dto.SideA ?? (dto.WrestlerA is null ? null : [dto.WrestlerA]);
                var sideBIds = dto.SideB ?? (dto.WrestlerB is null ? null : [dto.WrestlerB]);
                if (sideAIds is null || sideBIds is null) return null;

                var sideA = Bind(sideAIds, byId);
                var sideB = Bind(sideBIds, byId);

                // A card item naming somebody the roster no longer has is dropped whole
                // rather than rebuilt a man short — a three-quarters tag match is not a
                // match, and silently running one would be worse than losing the booking.
                if (sideA is null || sideB is null) return null;

                var a = sideA[Math.Clamp(dto.StartingIndexA, 0, sideA.Count - 1)];
                var b = sideB[Math.Clamp(dto.StartingIndexB, 0, sideB.Count - 1)];

                var plan = new MatchPlanModel
                {
                    SideA = new MatchSide
                    {
                        Members       = sideA,
                        StartingIndex = Math.Clamp(dto.StartingIndexA, 0, sideA.Count - 1),
                        Team          = teams.FirstOrDefault(t => t.Id == dto.TeamAId)
                    },
                    SideB = new MatchSide
                    {
                        Members       = sideB,
                        StartingIndex = Math.Clamp(dto.StartingIndexB, 0, sideB.Count - 1),
                        Team          = teams.FirstOrDefault(t => t.Id == dto.TeamBId)
                    },
                    MatchType = dto.MatchType,
                    // Re-bind to the live feud so a reloaded card reads current heat.
                    //
                    // Keyed on the two SIDES. Looking it up by the two starters found
                    // nothing for a tag match — the feud lives under the side key — so a
                    // reloaded tag card silently lost its feud, and a card carrying a
                    // feud-gated beat came back unrunnable.
                    Feud      = feudBook.Find(sideA, sideB),
                    // Likewise the belt: the same Title instance the registry holds, so a
                    // reloaded card can still put it on the line.
                    TitleAtStake = dto.TitleId is null ? null : titles.Find(dto.TitleId),
                    IsBlowOff    = dto.IsBlowOff,
                    Beats = dto.Beats.Select(x => new MatchBeat
                    {
                        Type      = x.Type,
                        Control   = x.Control,
                        Intensity = x.Intensity,
                        Duration  = x.Duration,
                        StyleHint = x.StyleHint,
                        IncomingIndex = x.IncomingIndex
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

        /// <summary>A date that is allowed to be absent, unlike the clock's.</summary>
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
