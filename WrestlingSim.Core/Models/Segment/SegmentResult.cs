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

        /// <summary>How each speaker's promo came out, 0–100 (Engine.Promos).</summary>
        public Dictionary<Wrestler, double> Promos { get; } = new();

        /// <summary>What decided a promo, when it is worth saying — for the show report.</summary>
        public List<string> PromoLines { get; } = new();

        /// <summary>Audience impact expressed on the show layer's 0–100 scale.</summary>
        public double Score => AudienceImpact * 10;
    }

    public class OvernessChange
    {
        public required Wrestler Wrestler { get; init; }

        /// <summary>Change in overness — the stock. Usually a fraction of a point.</summary>
        public double Delta { get; init; }

        /// <summary>
        /// Change in momentum — the flow. Swings much harder than overness: a good angle
        /// makes you hot this week without changing how much the audience fundamentally
        /// cares about you.
        /// </summary>
        public double MomentumDelta { get; set; }
    }
}
