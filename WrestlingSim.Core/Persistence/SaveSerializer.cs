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
using WrestlingSim.Models.Rumble;
using RumblePlanModel = WrestlingSim.Models.Rumble.RumblePlan;
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
                    LastAppearance = w.LastAppearance is { } seen ? Iso(seen) : null,
                    TouringUntil   = w.TouringUntil is { } road ? Iso(road) : null,
                    Fatigue        = w.Fatigue,
                    Sharpness      = w.Sharpness,
                    Injury         = ToDto(w.Injury),
                    InjuryHistory  = w.InjuryHistory.Select(ToDto).OfType<InjuryDto>().ToList()
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
            Stipulations = career.Stipulations.LastUsed
                .Select(kv => new StipulationUseDto { Stipulation = kv.Key, LastUsed = kv.Value })
                .OrderBy(u => u.Stipulation)
                .ToList(),

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
                    Camps              = f.Camps.Select(c => c.Select(w => w.Id).ToList()).ToList(),
                    // Still written so a save from this build opens in one that predates
                    // multi-party feuds. Such a build loses the third camp; it does not lose
                    // the feud.
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
            Loop           = show.Loop == null ? null : new LoopDto
            {
                Cast            = show.Loop.Cast.Select(w => w.Id).ToList(),
                Towns           = show.Loop.Towns,
                Pace            = show.Loop.Pace,
                MinutesPerNight = show.Loop.MinutesPerNight
            },
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
                IntendedA      = m.Plan.SideA.Intended,
                IntendedB      = m.Plan.SideB.Intended,
                MatchType      = m.Plan.MatchType,
                Stipulation    = m.Plan.Stipulation,
                StructureName  = m.StructureName,
                Brief          = ToDto(m.Plan.Brief),
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

            // A battle royal keeps Kind = Match, because that is what it is to a crowd
            // sitting through the card and the pacing rules read Kind. The sub-object is
            // what tells the two apart on the way back in.
            RumblePlanModel r => new CardItemDto
            {
                Kind = CardItemKind.Match,
                Rumble = new RumbleDto
                {
                    Id = r.Id,
                    Field = r.Field.Select(e => new RumbleEntrantDto
                    {
                        WrestlerId      = e.Wrestler.Id,
                        Number          = e.Number,
                        IsSurprise      = e.IsSurprise,
                        NumberAnnounced = e.NumberAnnounced
                    }).ToList(),
                    EntryIntervalSeconds = r.EntryIntervalSeconds,
                    WinnerId = r.Winner?.Id,
                    Stakes   = r.Stakes,
                    Moments  = r.Moments.Select(m => new RumbleMomentDto
                    {
                        Kind = m.Kind,
                        Cast = m.Cast.Select(w => w.Id).ToList(),
                        At   = m.At
                    }).ToList()
                }
            },

            // A drawing keeps Kind = Segment for the same reason a Rumble keeps Match: it is
            // what a crowd sits through, and the pacing rules read Kind. The match it draws
            // for lives on another show, so it is held by id and bound in a second pass.
            RumbleDraw d => new CardItemDto
            {
                Kind = CardItemKind.Segment,
                Draw = new RumbleDrawDto
                {
                    RumbleId    = d.Rumble?.Id ?? d.RumbleId,
                    RumbleLabel = d.RumbleLabel,
                    Cast        = d.Cast.Select(w => w.Id).ToList(),
                    HostId      = d.Host?.Id,
                    Rigged      = d.Rigged.Select(w => w.Id).ToList()
                }
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

                w.TouringUntil = DateOnly.TryParse(
                    state.TouringUntil, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var road)
                        ? road
                        : null;

                w.Fatigue   = Math.Clamp(state.Fatigue, 0, 100);
                w.Sharpness = Math.Clamp(state.Sharpness, 0, 100);

                w.Injury        = FromDto(state.Injury);
                w.InjuryHistory = state.InjuryHistory
                    .Select(FromDto).OfType<Models.Person.Injury>().ToList();
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

            foreach (var use in dto.Stipulations)
                career.Stipulations.Restore(use.Stipulation, use.LastUsed);

            foreach (var f in dto.Feuds)
            {
                // v4 writes every camp; v3 writes two sides; a v2 feud names one wrestler
                // per side. Read the richest form present.
                List<List<string>>? campIds = f.Camps;
                if (campIds is null)
                {
                    var sideAIds = f.SideA ?? (string.IsNullOrEmpty(f.WrestlerA) ? null : [f.WrestlerA]);
                    var sideBIds = f.SideB ?? (string.IsNullOrEmpty(f.WrestlerB) ? null : [f.WrestlerB]);
                    if (sideAIds is null || sideBIds is null) continue;
                    campIds = [sideAIds, sideBIds];
                }

                var camps = campIds.Select(ids => Bind(ids, byId)).ToList();
                if (camps.Count < 2 || camps.Any(c => c is null)) continue;

                var feud = career.FeudBook.GetOrCreate(
                    camps.Select(c => (IReadOnlyList<Wrestler>)c!).ToList());
                feud.RestoreHeat(f.Heat);
                feud.MatchCount = f.MatchCount;

                // A save from before match-count decay has no freshness state. Seeding it
                // from MatchCount treats those meetings as still remembered, which is the
                // conservative reading — the alternative would hand every old save a free
                // reset on every pairing it has already run into the ground.
                feud.RestoreMeetings(
                    f.RememberedMeetings > 0 ? f.RememberedMeetings : f.MatchCount,
                    ParseOptionalDate(f.LastMatchDate));

                // A pre-A3 save has no LastAdvanced, and null means "never advanced" and
                // therefore *never decays* — which would quietly exempt every feud in an
                // old save from the rule this release adds.
                //
                // LastMatchDate is the first fallback and the save's own clock is the
                // second. The clock matters more than it looks: a feud built entirely out
                // of segments has no LastMatchDate at all, because RecordSegment did not
                // take a date before A3 — and "build it with promos for a month, then have
                // the match" is an ordinary way to book. Review measured such a feud
                // sitting at 70 heat after 400 simulated days. Falling back to the save
                // date gives it a grace period starting from the load and then decays it
                // like everything else.
                feud.RestoreDecay(
                    ParseOptionalDate(f.LastAdvanced)
                        ?? ParseOptionalDate(f.LastMatchDate)
                        ?? career.CurrentDate,
                    ParseOptionalDate(f.DecayedTo));
                feud.RestoreResolution(
                    f.Concluded, ParseOptionalDate(f.ConcludedOn),
                    f.MatchesSinceHot, f.Distrust, f.ChaptersSettled);

                foreach (var tag in f.History) feud.AddTag(tag);
            }

            RestoreTitles(dto, career, byId);

            foreach (var s in dto.Shows)
                career.Shows.Add(FromDto(s, byId, career.FeudBook, career.Titles, career.Teams));

            ResolveDraws(career);

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

            if (dto.Loop is { } loopDto)
            {
                show.Loop = new HouseShowLoop
                {
                    // Names that are no longer on the roster drop off the run rather than
                    // coming back from the dead, which is how every other cast is read.
                    Cast            = loopDto.Cast.Select(id => byId.GetValueOrDefault(id))
                                                  .Where(w => w != null).Select(w => w!).ToList(),
                    Towns           = Math.Max(1, loopDto.Towns),
                    Pace            = loopDto.Pace,
                    MinutesPerNight = Math.Max(1, loopDto.MinutesPerNight)
                };
            }

            if (dto.Result != null) show.Result = FromDto(dto.Result);

            return show;
        }

        /// <summary>
        /// Binds every <see cref="RumbleDraw"/> to the match it draws for.
        ///
        /// A second pass because the two live on different shows and a card is read one at a
        /// time: the drawing on the December television is deserialised before the January
        /// pay-per-view exists. A drawing whose match is gone — the card was deleted, or it
        /// named a wrestler the roster has lost and was dropped whole — is dropped too,
        /// rather than left on the sheet pointing at nothing.
        /// </summary>
        private static void ResolveDraws(Career career)
        {
            var rumbles = career.Shows
                .SelectMany(s => s.Card)
                .OfType<RumblePlanModel>()
                .ToDictionary(r => r.Id, r => r);

            foreach (var show in career.Shows)
                show.Card.RemoveAll(item =>
                {
                    if (item is not RumbleDraw draw) return false;
                    draw.Rumble = rumbles.GetValueOrDefault(draw.RumbleId);
                    return draw.Rumble is null;
                });
        }

        private static BriefDto? ToDto(Models.MatchPlan.MatchBrief? brief) =>
            brief is null ? null : new BriefDto
            {
                Story       = brief.Story,
                Length      = brief.Length,
                Finish      = brief.Finish,
                WinningSide = brief.WinningSide,
                Outside     = brief.Outside,
                Draft       = brief.Draft,
                Alliance    = brief.Alliance is { } pact ? [pact.First, pact.Second] : null,
                AllianceBreaks = brief.AllianceBreaks,
                Bookings    = brief.Bookings.ToDictionary(
                                  kv => kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                  kv => kv.Value)
            };

        /// <summary>
        /// A brief whose side keys cannot be read is dropped rather than half-restored: a
        /// booking that says the wrong side goes over is worse than one that says nothing.
        /// </summary>
        private static Models.MatchPlan.MatchBrief? FromDto(BriefDto? dto)
        {
            if (dto is null) return null;

            var brief = new Models.MatchPlan.MatchBrief
            {
                Story       = dto.Story,
                Length      = dto.Length,
                Finish      = dto.Finish,
                WinningSide = dto.WinningSide,
                Outside     = dto.Outside,
                Draft       = dto.Draft,

                // Two entries or nothing. A malformed pair is dropped rather than half-read:
                // an alliance missing one of its members would generate a beat aimed at a
                // side that is not in it.
                Alliance    = dto.Alliance is { Count: 2 } pair ? (pair[0], pair[1]) : null,
                AllianceBreaks = dto.AllianceBreaks
            };

            foreach (var (key, booking) in dto.Bookings)
                if (int.TryParse(key, System.Globalization.NumberStyles.Integer,
                                 System.Globalization.CultureInfo.InvariantCulture, out int side))
                    brief.Bookings[side] = booking;

            return brief;
        }

        private static InjuryDto? ToDto(Models.Person.Injury? injury) =>
            injury is null ? null : new InjuryDto
            {
                Part      = injury.Part,
                Sustained = Iso(injury.Sustained),
                ClearedOn = Iso(injury.ClearedOn),
                WeeksOut  = injury.WeeksOut
            };

        /// <summary>
        /// An injury whose dates cannot be read is dropped rather than restored with a
        /// guessed one — a wrestler wrongly held out for six months is worse than a
        /// history entry lost.
        /// </summary>
        private static Models.Person.Injury? FromDto(InjuryDto? dto)
        {
            if (dto is null) return null;

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var style   = System.Globalization.DateTimeStyles.None;

            if (!DateOnly.TryParse(dto.Sustained, culture, style, out var sustained)) return null;
            if (!DateOnly.TryParse(dto.ClearedOn, culture, style, out var cleared))   return null;

            return new Models.Person.Injury
            {
                Part      = dto.Part,
                Sustained = sustained,
                ClearedOn = cleared,
                WeeksOut  = dto.WeeksOut
            };
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
            // Checked before the match branch, because a rumble is written with
            // Kind = Match and would otherwise be read back as a match with no sides.
            if (dto.Rumble is { } rumbleDto)
            {
                var field = rumbleDto.Field
                    .Where(e => byId.ContainsKey(e.WrestlerId))
                    .Select(e => new RumbleEntrant
                    {
                        Wrestler        = byId[e.WrestlerId],
                        Number          = e.Number,
                        IsSurprise      = e.IsSurprise,
                        NumberAnnounced = e.NumberAnnounced
                    })
                    .ToList();

                // A card naming somebody the roster has lost is dropped whole rather than
                // rebuilt a wrestler short — the same rule the match branch follows, and for
                // the same reason: a thirty-strong Rumble quietly becoming a twenty-nine is
                // a booking the player never made.
                if (field.Count != rumbleDto.Field.Count) return null;
                if (rumbleDto.WinnerId is null || !byId.TryGetValue(rumbleDto.WinnerId, out var champ))
                    return null;

                return new RumblePlanModel
                {
                    Id = string.IsNullOrEmpty(rumbleDto.Id)
                        ? Guid.NewGuid().ToString("N")
                        : rumbleDto.Id,
                    Field = field,
                    EntryIntervalSeconds = rumbleDto.EntryIntervalSeconds,
                    Winner = champ,
                    Stakes = rumbleDto.Stakes,
                    Moments = rumbleDto.Moments
                        .Where(m => m.Cast.All(byId.ContainsKey))
                        .Select(m => new RumbleMoment
                        {
                            Kind = m.Kind,
                            Cast = m.Cast.Select(id => byId[id]).ToList(),
                            At   = m.At
                        }).ToList()
                };
            }

            // Checked before the segment branch for the same reason the rumble branch is
            // checked before the match branch: a drawing is written with Kind = Segment and
            // would otherwise be read back as a promo with no actions.
            if (dto.Draw is { } drawDto)
            {
                var cast = drawDto.Cast.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

                // Same rule as everywhere else on a card: a booking naming somebody the
                // roster has lost is dropped whole rather than quietly run one short.
                if (cast.Count != drawDto.Cast.Count || cast.Count == 0) return null;
                if (drawDto.HostId is { } hostId && !byId.ContainsKey(hostId)) return null;

                // Rumble stays null here. It lives on another show, which this pass has not
                // necessarily read yet — ResolveDraws binds it once every card is built, and
                // drops the drawing if the match it draws for is gone.
                return new RumbleDraw
                {
                    RumbleId    = drawDto.RumbleId,
                    RumbleLabel = drawDto.RumbleLabel,
                    Cast        = cast,
                    Host        = drawDto.HostId is null ? null : byId[drawDto.HostId],
                    Rigged      = drawDto.Rigged.Where(byId.ContainsKey).Select(id => byId[id]).ToList()
                };
            }

            if (dto.Kind == CardItemKind.Match)
            {
                // v3 writes SideA/SideB; a v2 save has one wrestler per side instead.
                var sideAIds = dto.SideA ?? (dto.WrestlerA is null ? null : [dto.WrestlerA]);
                var sideBIds = dto.SideB ?? (dto.WrestlerB is null ? null : [dto.WrestlerB]);
                if (sideAIds is null || sideBIds is null) return null;

                // A side with nobody on it is legal now: a match somebody has planned onto the
                // card and not yet cast. `Bind` returns null for both "empty" and "names a
                // wrestler who has gone", so an empty list is separated out here before it is
                // asked — otherwise every planned slot would be dropped on reload as though
                // it named a stranger.
                var sideA = sideAIds.Count == 0 ? [] : Bind(sideAIds, byId);
                var sideB = sideBIds.Count == 0 ? [] : Bind(sideBIds, byId);

                // A card item naming somebody the roster no longer has is dropped whole
                // rather than rebuilt a man short — a three-quarters tag match is not a
                // match, and silently running one would be worse than losing the booking.
                if (sideA is null || sideB is null) return null;

                // Clamped against an empty side gives -1, which is not an index. A planned
                // side has no starter to remember yet.
                int startA = sideA.Count == 0 ? 0 : Math.Clamp(dto.StartingIndexA, 0, sideA.Count - 1);
                int startB = sideB.Count == 0 ? 0 : Math.Clamp(dto.StartingIndexB, 0, sideB.Count - 1);

                var plan = new MatchPlanModel
                {
                    SideA = new MatchSide
                    {
                        Members       = sideA,
                        StartingIndex = startA,
                        Intended      = dto.IntendedA,
                        Team          = teams.FirstOrDefault(t => t.Id == dto.TeamAId)
                    },
                    SideB = new MatchSide
                    {
                        Members       = sideB,
                        StartingIndex = startB,
                        Intended      = dto.IntendedB,
                        Team          = teams.FirstOrDefault(t => t.Id == dto.TeamBId)
                    },
                    MatchType = dto.MatchType,
                    Brief     = FromDto(dto.Brief),
                    Stipulation = dto.Stipulation,
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
