using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    /// <summary>
    /// Everything a simulated segment produced. Returned rather than printed so the
    /// UI owns presentation and the show layer can consume the numbers.
    /// </summary>
    public class SegmentResult
    {
        public required string SegmentName { get; init; }
        public required SegmentType Type { get; init; }
        public required SegmentLocation Location { get; init; }

        /// <summary>Crowd reaction, 0–10.</summary>
        public double AudienceImpact { get; set; }

        /// <summary>Feud heat this segment deposited, before it is split across pairings.</summary>
        public double HeatGenerated { get; set; }

        /// <summary>Tags to stamp on the feuds between the participants.</summary>
        public List<FeudHistoryTag> HistoryTags { get; set; } = new();

        /// <summary>Popularity changes actually applied, keyed by wrestler.</summary>
        public List<OvernessChange> OvernessChanges { get; set; } = new();

        public bool Botched { get; set; }
        public Wrestler? Injured { get; set; }

        /// <summary>Play-by-play lines for the UI to render.</summary>
        public List<string> Commentary { get; set; } = new();

        /// <summary>Audience impact expressed on the show layer's 0–100 scale.</summary>
        public double Score => AudienceImpact * 10;
    }

    public class OvernessChange
    {
        public required Wrestler Wrestler { get; init; }
        public int Delta { get; init; }
    }
}
