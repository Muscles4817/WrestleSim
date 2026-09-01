using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    public class Feud
    {
        public required Wrestler WrestlerA { get; init; }
        public required Wrestler WrestlerB { get; init; }
        public FeudIntensity Intensity { get; set; }
        public List<FeudHistoryTag> History { get; set; } = new();

        // How many times they've wrestled in this feud — feeds into fatigue decay
        public int MatchCount { get; set; }

        /// <summary>
        /// Accumulated heat from booked segments and matches. Intensity is derived from
        /// this, so a feud is something you build by booking rather than something you declare.
        /// </summary>
        public double Heat { get; private set; }

        // Heat required to reach each intensity tier.
        public const double ColdThreshold     = 5;
        public const double BuildingThreshold = 15;
        public const double HotThreshold      = 30;
        public const double NuclearThreshold  = 50;

        public double IntensityMultiplier => Intensity switch
        {
            FeudIntensity.None     => 1.00,
            FeudIntensity.Cold     => 1.05,
            FeudIntensity.Building => 1.15,
            FeudIntensity.Hot      => 1.30,
            FeudIntensity.Nuclear  => 1.50,
            _                      => 1.00
        };

        // Crowd energy bonus at match start from feud heat
        public double StartingEnergyBonus => Intensity switch
        {
            FeudIntensity.Cold     => 3,
            FeudIntensity.Building => 7,
            FeudIntensity.Hot      => 12,
            FeudIntensity.Nuclear  => 18,
            _                      => 0
        };

        public bool HasTag(FeudHistoryTag tag) => History.Contains(tag);

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Adds heat and re-derives Intensity. Returns true if the feud moved up a tier,
        /// so callers can report the escalation to the player.
        /// </summary>
        public bool AddHeat(double amount)
        {
            if (amount <= 0) return false;

            var before = Intensity;
            Heat += amount;
            Intensity = IntensityFor(Heat);
            return Intensity > before;
        }

        /// <summary>
        /// Sets Heat directly and re-derives Intensity. For loading a save only — normal
        /// play must go through AddHeat so a feud is something you book, not something
        /// you assign.
        /// </summary>
        public void RestoreHeat(double heat)
        {
            Heat = Math.Max(0, heat);
            Intensity = IntensityFor(Heat);
        }

        /// <summary>Stamps a history tag onto the feud. Duplicates are ignored.</summary>
        public bool AddTag(FeudHistoryTag tag)
        {
            if (History.Contains(tag)) return false;
            History.Add(tag);
            return true;
        }

        /// <summary>
        /// Forces the feud to at least the given intensity, topping up Heat to match.
        /// Used when the player sets a feud up by hand rather than booking it.
        /// </summary>
        public void SetMinimumIntensity(FeudIntensity intensity)
        {
            double required = HeatFor(intensity);
            if (Heat < required) Heat = required;
            if (Intensity < intensity) Intensity = intensity;
        }

        public static FeudIntensity IntensityFor(double heat) => heat switch
        {
            >= NuclearThreshold  => FeudIntensity.Nuclear,
            >= HotThreshold      => FeudIntensity.Hot,
            >= BuildingThreshold => FeudIntensity.Building,
            >= ColdThreshold     => FeudIntensity.Cold,
            _                    => FeudIntensity.None
        };

        public static double HeatFor(FeudIntensity intensity) => intensity switch
        {
            FeudIntensity.Nuclear  => NuclearThreshold,
            FeudIntensity.Hot      => HotThreshold,
            FeudIntensity.Building => BuildingThreshold,
            FeudIntensity.Cold     => ColdThreshold,
            _                      => 0
        };

        /// <summary>Heat still needed before the next tier unlocks; null at Nuclear.</summary>
        public double? HeatToNextTier => Intensity switch
        {
            FeudIntensity.None     => ColdThreshold - Heat,
            FeudIntensity.Cold     => BuildingThreshold - Heat,
            FeudIntensity.Building => HotThreshold - Heat,
            FeudIntensity.Hot      => NuclearThreshold - Heat,
            _                      => null
        };

        public bool Involves(Wrestler w) => w == WrestlerA || w == WrestlerB;

        public override string ToString() =>
            $"{WrestlerA.RingName} vs {WrestlerB.RingName} — {Intensity} ({Heat:F0} heat)";
    }
}
