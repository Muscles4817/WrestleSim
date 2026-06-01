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
    }
}
