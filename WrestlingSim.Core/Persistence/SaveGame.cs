using WrestlingSim.Enums;
using WrestlingSim.Models;

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
        /// <summary>Bumped when the shape below changes incompatibly.</summary>
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public string CareerId { get; set; } = "";
        public string PromotionName { get; set; } = "";
        public PromotionTier Tier { get; set; }

        /// <summary>World dates, as ISO yyyy-MM-dd.</summary>
        public string CurrentDate { get; set; } = "";
        public string StartDate { get; set; } = "";

        public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;

        public List<WrestlerStateDto> Wrestlers { get; set; } = new();
        public List<FeudDto> Feuds { get; set; } = new();
        public List<ShowDto> Shows { get; set; } = new();
    }

    /// <summary>Per-wrestler mutable state. Structure comes from the embedded roster.</summary>
    public class WrestlerStateDto
    {
        public string Id { get; set; } = "";
        public int Popularity { get; set; }
    }

    public class FeudDto
    {
        public string WrestlerA { get; set; } = "";
        public string WrestlerB { get; set; } = "";
        public double Heat { get; set; }
        public int MatchCount { get; set; }
        public List<FeudHistoryTag> History { get; set; } = new();
    }

    public class ShowDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Date { get; set; } = "";
        public ShowType Type { get; set; }
        public string Venue { get; set; } = "";
        public int RuntimeMinutes { get; set; }
        public int Attendance { get; set; }

        public List<CardItemDto> Card { get; set; } = new();

        /// <summary>
        /// Result summary. Full BeatResult play-by-play is deliberately not persisted —
        /// it is large, and a completed show only needs to report what it did.
        /// </summary>
        public ShowResultDto? Result { get; set; }
    }

    public class CardItemDto
    {
        public CardItemKind Kind { get; set; }

        // ── Match ────────────────────────────────────────────────────────────
        public string? WrestlerA { get; set; }
        public string? WrestlerB { get; set; }
        public MatchType MatchType { get; set; }
        public string StructureName { get; set; } = "Custom";
        public List<BeatDto> Beats { get; set; } = new();

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
}
