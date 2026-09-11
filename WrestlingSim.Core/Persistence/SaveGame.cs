using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;

// ImplicitUsings pulls in System.IO, which also defines a MatchType.
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Persistence
{
    /// <summary>
    /// The serialisable form of a career.
    ///
    /// The object graph in memory is full of shared references — a Feud holds Wrestler
    /// references, so does every MatchPlan and Segment. Serialising that directly would
    /// write a separate copy of each wrestler per feud and break identity on load, so
    /// everything here refers to people by <see cref="Wrestler.Id"/> instead.
    ///
    /// Roster *structure* (names, skills, gimmicks) is not stored: it comes from the
    /// embedded roster on load, and the save carries only the state that changes.
    /// That keeps saves small and lets the shipped roster be corrected without
    /// invalidating them.
    /// </summary>
    public class SaveGame
    {
        /// <summary>
        /// Bumped when the shape below changes incompatibly.
        ///
        /// v2 added championships and the brand split. A v1 save has never had belts, so
        /// loading one seeds the standard slate rather than leaving the promotion with
        /// none at all (see <see cref="SaveSerializer.FromDto"/>); it simply has no
        /// brands, which needs no seeding because an undivided promotion is a valid state.
        /// </summary>
        /// <summary>
        /// v3 stores a match as two *sides* rather than two wrestlers, so a tag match can
        /// be saved. v2 saves still load: a v2 card item carries WrestlerA/WrestlerB, and
        /// each becomes a side of one.
        /// </summary>
        public const int CurrentVersion = 3;

        public int Version { get; set; } = CurrentVersion;

        public string CareerId { get; set; } = "";
        public string PromotionName { get; set; } = "";
        public PromotionTier Tier { get; set; }

        /// <summary>World dates, as ISO yyyy-MM-dd.</summary>
        public string CurrentDate { get; set; } = "";
        public string StartDate { get; set; } = "";

        public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;

        public List<WrestlerStateDto> Wrestlers { get; set; } = new();
        public List<ShowDefinitionDto> ShowDefinitions { get; set; } = new();
        public List<FeudDto> Feuds { get; set; } = new();
        public List<ShowDto> Shows { get; set; } = new();
        public List<TitleDto> Titles { get; set; } = new();

        /// <summary>The brand split, or null for a promotion that has never divided.</summary>
        public BrandSplitDto? Brands { get; set; }

        /// <summary>
        /// When the promotion last ran each gimmick match. Absent from every save written
        /// before stipulations existed, which reads back as "never run" — correct, because
        /// no save written then could have run one.
        /// </summary>
        public List<StipulationUseDto> Stipulations { get; set; } = new();

        /// <summary>Standing tag teams. Absent in v2 saves; an empty list is correct there.</summary>
        public List<TagTeamDto> Teams { get; set; } = new();
    }

    /// <summary>
    /// A championship and its whole lineage. Champions are stored by
    /// <see cref="Wrestler.Id"/> for the same reason everyone else is: the live graph
    /// shares wrestler instances, and writing one by value here would hand the loaded
    /// career a second copy of that person.
    /// </summary>
    public class TagTeamDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Members { get; set; } = new();
        public string Formed { get; set; } = "";
        public string? Disbanded { get; set; }
        public int MatchesTogether { get; set; }
        public string? LastTeamed { get; set; }

        /// <summary>How far decay has already been charged. Without it a reloaded team
        /// re-pays every idle day it had already paid for.</summary>
        public string? DecayedTo { get; set; }

        public double Chemistry { get; set; }
    }

    public class TitleDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public TitleTier Tier { get; set; }
        public Division Division { get; set; }

        /// <summary>1 for a singles belt, 2 for a tag belt. Absent in v2 saves, where 0
        /// means "not recorded" and loads as 1.</summary>
        public int SideSize { get; set; }

        public string Established { get; set; } = "";
        public double Standing { get; set; }
        public bool Retired { get; set; }
        public string? RetiredOn { get; set; }
        public List<TitleReignDto> Lineage { get; set; } = new();
    }

    public class TitleReignDto
    {
        /// <summary>The champion's <see cref="Wrestler.Id"/>, never the wrestler itself.</summary>
        /// <summary>Everyone who held the belt for this reign. Written from v3.</summary>
        public List<string>? Champions { get; set; }

        /// <summary>v2 form: a single holder. Read, never written.</summary>
        public string? Champion { get; set; }

        public int ReignNumber { get; set; }
        public string Won { get; set; } = "";
        public string? Lost { get; set; }
        public string? LastDefended { get; set; }
        public string WonAt { get; set; } = "";
        public string LostAt { get; set; } = "";
        public int Defences { get; set; }
        public bool Vacated { get; set; }
    }

    /// <summary>
    /// The brand structure and its erosion. Rosters are lists of
    /// <see cref="Models.Wrestler.Id"/> for the same reason everything else here is: the
    /// live graph shares wrestler references, and a second copy would break identity.
    /// </summary>
    public class BrandSplitDto
    {
        public bool Active { get; set; }
        public double Integrity { get; set; } = 100;
        public double PermanentErosion { get; set; }
        public int CrossoverCount { get; set; }

        /// <summary>ISO yyyy-MM-dd, or null.</summary>
        public string? StartedOn { get; set; }
        public string? LastDraftOn { get; set; }

        public List<BrandDto> Brands { get; set; } = new();
        public List<CrossoverDto> Crossovers { get; set; } = new();
    }

    public class BrandDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Identity { get; set; } = "";
        public string Colour { get; set; } = "";

        /// <summary>Roster membership, by wrestler id.</summary>
        public List<string> RosterIds { get; set; } = new();

        public List<double> RecentRatings { get; set; } = new();
    }

    public class CrossoverDto
    {
        public string WrestlerId { get; set; } = "";
        public string WrestlerName { get; set; } = "";
        public string HomeBrandName { get; set; } = "";
        public string ShowBrandName { get; set; } = "";
        public string ShowName { get; set; } = "";
        public string Date { get; set; } = "";
        public double Cost { get; set; }
    }

    /// <summary>Per-wrestler mutable state. Structure comes from the embedded roster.</summary>
    public class WrestlerStateDto
    {
        public string Id { get; set; } = "";

        /// <summary>The stock: how over they are.</summary>
        public double Overness { get; set; }

        /// <summary>The flow: which way they are trending.</summary>
        public double Momentum { get; set; }

        /// <summary>Last show they worked, ISO yyyy-MM-dd. Null if they have not yet.</summary>
        public string? LastAppearance { get; set; }

        /// <summary>
        /// The two ring-condition meters. Absent from saves written before they existed,
        /// which read back as a fresh, sharp roster — the state a new career starts in, so
        /// an old save loads as though nobody had worked yet rather than as a roster of
        /// exhausted rookies.
        /// </summary>
        public double Fatigue { get; set; }
        public double Sharpness { get; set; } = 100;

        /// <summary>
        /// What is keeping them out, and everything they have ever done to themselves.
        /// History is permanent — doc 15's sim implications call it "the most important
        /// detail" — so it is saved in full rather than as a count.
        /// </summary>
        public InjuryDto? Injury { get; set; }
        public List<InjuryDto> InjuryHistory { get; set; } = new();

        /// <summary>
        /// What Overness was called before the stock/flow split. Read only as a fallback so
        /// saves written by earlier builds still open; never written.
        /// </summary>
        public int? Popularity { get; set; }

        /// <summary>Overness, taking the legacy field when a pre-split save omits it.</summary>
        public double ResolvedOverness => Overness > 0 ? Overness : Popularity ?? 0;
    }

    public class ShowDefinitionDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ShowType Type { get; set; }
        public RecurrenceKind Recurrence { get; set; }
        public DayOfWeek Day { get; set; }
        public WeekOrdinal Ordinal { get; set; }
        public string Venue { get; set; } = "";
        public int? RuntimeMinutes { get; set; }
        public bool Active { get; set; } = true;

        /// <summary>Owning brand, or null for a company-wide show.</summary>
        public string? BrandId { get; set; }
    }

    public class FeudDto
    {
        /// <summary>
        /// v4: every camp in the story. A feud can have more than two — a triangle, or a
        /// faction war — and SideA/SideB below can only name the first two.
        ///
        /// Written alongside them rather than instead of them, so a save from this build
        /// still opens in one that predates multi-party feuds: that build reads the two
        /// sides and loses the third camp, which is wrong but survivable, where an unknown
        /// field would lose the whole feud.
        /// </summary>
        public List<List<string>>? Camps { get; set; }

        /// <summary>The first two camps. Written from v3; absent in v2 saves.</summary>
        public List<string>? SideA { get; set; }
        public List<string>? SideB { get; set; }

        /// <summary>v2 form: one wrestler per side. Read, never written.</summary>
        public string? WrestlerA { get; set; }
        public string? WrestlerB { get; set; }
        public double Heat { get; set; }
        public int MatchCount { get; set; }

        /// <summary>
        /// Freshness state for the pairing — see Feud.RememberedMeetings. Absent from
        /// saves written before match-count decay existed; those fall back to MatchCount
        /// on load, which reads an old feud as fully remembered rather than as brand new.
        /// </summary>
        public double RememberedMeetings { get; set; }

        public string? LastMatchDate { get; set; }

        // ── Decay and resolution. Absent from saves written before A3. ───────

        /// <summary>Last day anything advanced the feud. Null in a pre-A3 save.</summary>
        public string? LastAdvanced { get; set; }

        /// <summary>How far daily decay has already been charged.</summary>
        public string? DecayedTo { get; set; }

        public bool Concluded { get; set; }
        public string? ConcludedOn { get; set; }
        public int MatchesSinceHot { get; set; }
        public int ChaptersSettled { get; set; }
        public double Distrust { get; set; }

        public List<FeudHistoryTag> History { get; set; } = new();
    }

    public class ShowDto
    {
        public string Id { get; set; } = "";
        public string? DefinitionId { get; set; }
        public string Name { get; set; } = "";
        public string Date { get; set; } = "";
        public ShowType Type { get; set; }
        public string Venue { get; set; } = "";
        public string? BrandId { get; set; }
        public int RuntimeMinutes { get; set; }
        public int Attendance { get; set; }

        public List<CardItemDto> Card { get; set; } = new();

        /// <summary>A booked run of towns, on an untelevised date. Null on every other show.</summary>
        public LoopDto? Loop { get; set; }

        /// <summary>
        /// Result summary. Full BeatResult play-by-play is deliberately not persisted —
        /// it is large, and a completed show only needs to report what it did.
        /// </summary>
        public ShowResultDto? Result { get; set; }
    }

    /// <summary>
    /// A booked run of towns. The cast is written by id like every other roster reference, so
    /// a wrestler removed between saves drops off the run rather than resurrecting.
    /// </summary>
    public class LoopDto
    {
        public List<string> Cast { get; set; } = new();
        public int Towns { get; set; } = 3;
        public BeatIntensity Pace { get; set; } = BeatIntensity.Medium;
        public int MinutesPerNight { get; set; } = 12;
    }

    public class CardItemDto
    {
        public CardItemKind Kind { get; set; }

        // ── Match ────────────────────────────────────────────────────────────

        /// <summary>
        /// Everyone on each side, in booking order. Written from v3 onward.
        /// </summary>
        public List<string>? SideA { get; set; }
        public List<string>? SideB { get; set; }

        /// <summary>Standing team on each side, by <see cref="TagTeamDto.Id"/>, if any.</summary>
        public string? TeamAId { get; set; }
        public string? TeamBId { get; set; }

        /// <summary>Index of the member who takes the opening bell for each side.</summary>
        public int StartingIndexA { get; set; }
        public int StartingIndexB { get; set; }

        /// <summary>
        /// v2 form: one wrestler per side. Still read, never written — a v2 card becomes
        /// two sides of one. Kept nullable so a v3 save can omit them entirely.
        /// </summary>
        public string? WrestlerA { get; set; }
        public string? WrestlerB { get; set; }
        public MatchType MatchType { get; set; }

        /// <summary>The gimmick. Absent on older saves, which read back as None.</summary>
        public Stipulation Stipulation { get; set; }

        public string StructureName { get; set; } = "Custom";

        /// <summary>
        /// What the booker asked for, when this match was written from a brief.
        ///
        /// Saved because the engine grades a match against it: a card booked tonight and run
        /// next week would otherwise reload with no promise attached and be scored as though
        /// nobody had said what it was for. Null on a hand-built plan and on every save
        /// written before briefs existed.
        /// </summary>
        public BriefDto? Brief { get; set; }

        public List<BeatDto> Beats { get; set; } = new();

        /// <summary>The championship on the line, by <see cref="TitleDto.Id"/>. Null if none.</summary>
        public string? TitleId { get; set; }

        /// <summary>
        /// Present when this card slot is a battle royal rather than an ordinary match.
        ///
        /// A sub-object rather than a new <see cref="CardItemKind"/>, deliberately: the Kind
        /// drives the show's pacing rules — three matches in a row is a slog — and a Rumble
        /// *is* a match as far as an audience sitting through the card is concerned. It
        /// needs to be told apart when reading a save, not when pacing a show.
        ///
        /// Null on every save written before this existed, so an older file reads back as
        /// exactly what it was.
        /// </summary>
        public RumbleDto? Rumble { get; set; }

        /// <summary>
        /// Present when this card slot is a number drawing for a battle royal booked on a
        /// later show. Kind stays <see cref="CardItemKind.Segment"/>, because a drawing is
        /// a segment to a crowd sitting through it and the pacing rules read Kind.
        ///
        /// Null on every save written before this existed.
        /// </summary>
        public RumbleDrawDto? Draw { get; set; }

        /// <summary>
        /// Whether the booker declared this the blow-off. Absent from pre-A3 saves, which
        /// read as false — correct, since no save written before A3 could have declared one.
        /// </summary>
        public bool IsBlowOff { get; set; }

        // ── Segment ──────────────────────────────────────────────────────────
        public string? SegmentName { get; set; }
        public SegmentType SegmentType { get; set; }
        public SegmentLocation Location { get; set; }
        public bool IsScripted { get; set; } = true;
        public List<string> Participants { get; set; } = new();
        public List<SegmentActionDto> Actions { get; set; } = new();
        public List<FeudHistoryTag> HistoryTags { get; set; } = new();
    }

    public class BeatDto
    {
        public BeatType Type { get; set; }
        public BeatControl Control { get; set; }
        public BeatIntensity Intensity { get; set; }
        public BeatDuration Duration { get; set; }
        public WrestlingStyle? StyleHint { get; set; }

        /// <summary>Which member a tag beat brings in. Null tags to the next man round.</summary>
        public int? IncomingIndex { get; set; }
    }

    public class SegmentActionDto
    {
        public SegmentActionType ActionType { get; set; }
        public string Performer { get; set; } = "";
        public string? Target { get; set; }
        public string Dialogue { get; set; } = "";
        public double HeatImpact { get; set; }
        public double OvernessImpact { get; set; }
        public double BaseImpact { get; set; }
        public string Label { get; set; } = "";
    }

    public class ShowResultDto
    {
        public double OverallRating { get; set; }
        public double FinalCrowdMood { get; set; }
        public double OverrunPenalty { get; set; }
        public int BookedMinutes { get; set; }
        public int BudgetMinutes { get; set; }
        public List<CardItemResultDto> Items { get; set; } = new();
    }

    public class CardItemResultDto
    {
        public string Label { get; set; } = "";
        public CardItemKind Kind { get; set; }
        public int DurationMinutes { get; set; }
        public double Score { get; set; }

        /// <summary>Star rating when the item was a match; null for segments.</summary>
        public double? StarRating { get; set; }

        public List<string> Notes { get; set; } = new();
    }

    /// <summary>What a match was booked to be.</summary>
    public class BriefDto
    {
        public MatchStory Story  { get; set; }
        public MatchScale Length { get; set; }
        public FinishKind Finish { get; set; }
        public int WinningSide   { get; set; }
        public OutsideFactor Outside { get; set; }
        public int Draft { get; set; }

        /// <summary>
        /// The two sides who work together, as a two-element list, or null for a match where
        /// nobody does. A list rather than a tuple because a tuple round-trips through JSON
        /// as `Item1`/`Item2`, which is a shape nobody wants to read in a save file.
        /// </summary>
        public List<int>? Alliance { get; set; }

        public bool AllianceBreaks { get; set; } = true;

        /// <summary>How each side comes out, keyed by side index as a string for JSON.</summary>
        public Dictionary<string, Booking> Bookings { get; set; } = new();
    }

    /// <summary>One injury, on a wrestler's sheet or in their history.</summary>
    public class InjuryDto
    {
        public BodyPart Part { get; set; }
        public string Sustained { get; set; } = "";
        public string ClearedOn { get; set; } = "";
        public int WeeksOut { get; set; }
    }

    /// <summary>One gimmick match type and the last date the promotion ran it.</summary>
    public class StipulationUseDto
    {
        public Stipulation Stipulation { get; set; }
        public DateOnly LastUsed { get; set; }
    }

    /// <summary>A battle royal on a card. Ids, so a wrestler is bound once on load.</summary>
    public class RumbleDto
    {
        /// <summary>
        /// <see cref="Models.Rumble.RumblePlan.Id"/>, so a drawing on another show can find
        /// this one. Empty on saves written before drawings existed — nothing pointed at a
        /// Rumble then, so a fresh id on load loses nothing.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>The field, in entry order.</summary>
        public List<RumbleEntrantDto> Field { get; set; } = new();

        public int EntryIntervalSeconds { get; set; } = 90;
        public string? WinnerId { get; set; }
        public string? Stakes { get; set; }
        public List<RumbleMomentDto> Moments { get; set; } = new();
    }

    public class RumbleEntrantDto
    {
        public string WrestlerId { get; set; } = "";
        public int Number { get; set; } = 1;
        public bool IsSurprise { get; set; }

        /// <summary>The drawing already told the crowd this number. False on older saves.</summary>
        public bool NumberAnnounced { get; set; }
    }

    /// <summary>A number drawing on a card, pointing at the match it draws for by id.</summary>
    public class RumbleDrawDto
    {
        public string RumbleId { get; set; } = "";
        public string RumbleLabel { get; set; } = "the Rumble";
        public List<string> Cast { get; set; } = new();
        public string? HostId { get; set; }
        public List<string> Rigged { get; set; } = new();
    }

    public class RumbleMomentDto
    {
        public RumbleMomentKind Kind { get; set; }
        public List<string> Cast { get; set; } = new();
        public double At { get; set; } = 0.5;
    }

}
