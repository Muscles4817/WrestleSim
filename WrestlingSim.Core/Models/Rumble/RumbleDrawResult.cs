using WrestlingSim.Enums;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Models.Rumble
{
    /// <summary>What a number drawing did, in the terms a drawing is actually about.</summary>
    public class RumbleDrawResult
    {
        public required string RumbleLabel { get; init; }

        /// <summary>Every number pulled, in the order the building found out.</summary>
        public List<RumblePull> Pulls { get; init; } = new();

        public Wrestler? Host { get; init; }

        // ── Scoring ──────────────────────────────────────────────────────────

        /// <summary>What the numbers themselves were worth, 0–1, before the fix and the host.</summary>
        public double Reaction { get; init; }

        /// <summary>How much of the drum the crowd still believed, 0–1.</summary>
        public double Credibility { get; init; }

        /// <summary>What the person running it added or took off, around 1.00.</summary>
        public double HostFactor { get; init; }

        /// <summary>Out of 100, on the same scale the show layer scores everything else.</summary>
        public double FinalScore { get; init; }

        // ── Consequences ─────────────────────────────────────────────────────

        /// <summary>Feud heat this deposited, before the show splits it across pairings.</summary>
        public double HeatGenerated { get; init; }

        /// <summary>
        /// Who the heat is between. The cast on an honest night; the host and whoever they
        /// handed a number to when it was fixed, because that is who has the grievance.
        /// </summary>
        public List<Wrestler> HeatParticipants { get; init; } = new();

        public List<FeudHistoryTag> HistoryTags { get; init; } = new();

        /// <summary>
        /// Momentum only, and no overness. A drawing is not a rub — nobody's standing moves
        /// because of a number. What moves is whether they are the story of the week, and
        /// that is exactly what momentum is for.
        /// </summary>
        public List<OvernessChange> MomentumChanges { get; init; } = new();

        public List<string> Commentary { get; init; } = new();

        /// <summary>Largest-first, for display.</summary>
        public IEnumerable<(string Label, double Value)> Ordered =>
            new[]
            {
                ("The numbers",  Reaction),
                ("Believed",     Credibility),
                ("On the mic",   HostFactor)
            }
            .OrderByDescending(x => x.Item2);
    }

    /// <summary>One wrestler, one number, and what the building did about it.</summary>
    public class RumblePull
    {
        public required Wrestler Wrestler { get; init; }
        public required int Number { get; init; }

        /// <summary>Handed to them rather than drawn.</summary>
        public bool Rigged { get; init; }

        /// <summary>What this pull alone was worth to the crowd, 0–1.</summary>
        public double Reaction { get; init; }
    }
}
