using WrestlingSim.Enums;

namespace WrestlingSim.Engine
{
    internal class MatchEngineState
    {
        // ── Live match state ─────────────────────────────────────────────────

        /// <summary>0–100. How engaged the crowd is right now.</summary>
        public double CrowdEnergy { get; set; }

        /// <summary>
        /// –100 to +100.  Positive = WrestlerA has the advantage.
        /// Negative = WrestlerB has the advantage.
        /// </summary>
        public double Momentum { get; set; }

        /// <summary>
        /// Uncapped running total of all momentum deltas. Unlike Momentum, this is never
        /// clamped, so it accumulates across multiple heat segments and correctly measures
        /// how deep a hole a wrestler has dug — used to scale the comeback earned bonus.
        /// </summary>
        public double RawMomentum { get; private set; }

        // ── Accumulators ─────────────────────────────────────────────────────

        public double TechnicalScore    { get; set; }
        public double StorytellingScore { get; set; }

        /// <summary>Highest crowd energy reached at any point.</summary>
        public double CrowdPeakEnergy { get; set; }

        /// <summary>
        /// The loudest this particular pairing can ever get this building, 0–100.
        ///
        /// Set from both wrestlers' Connection at the opening bell. Two people the
        /// audience has no investment in cannot reach a WrestleMania-main-event
        /// reaction no matter how many near-falls are booked — without this the crowd
        /// component pinned at 100 for every match and stopped distinguishing anything.
        /// </summary>
        public double CrowdCeiling { get; set; } = 100;

        /// <summary>Running readings for average crowd calculation.</summary>
        public List<double> CrowdEnergyReadings { get; set; } = new();

        // ── Repetition tracking ──────────────────────────────────────────────

        /// <summary>
        /// How many times each beat type has been executed. Drives diminishing returns:
        /// a crowd that has already seen four beatdowns does not react to the fifth.
        /// Generalises what used to be a near-fall-only rule.
        /// </summary>
        private readonly Dictionary<BeatType, int> _typeCounts = new();

        /// <summary>Records this beat type and returns how many times it has now been used (1-based).</summary>
        public int RegisterBeat(BeatType type)
        {
            _typeCounts.TryGetValue(type, out int seen);
            _typeCounts[type] = seen + 1;
            BeatIndex++;
            return seen + 1;
        }

        public int TimesUsed(BeatType type) => _typeCounts.TryGetValue(type, out int n) ? n : 0;

        /// <summary>How many distinct beat types the match has used. Rewards varied booking.</summary>
        public int DistinctBeatTypes => _typeCounts.Count;

        /// <summary>0-based position of the beat currently resolving.</summary>
        public int BeatIndex { get; private set; } = -1;

        /// <summary>Total near falls executed so far; drives near-fall specific commentary.</summary>
        public int NearFallCount => TimesUsed(BeatType.NearFall);

        // ── Finish ───────────────────────────────────────────────────────────

        public double FinishQuality { get; set; }

        // ── Helpers ──────────────────────────────────────────────────────────

        public double CrowdAverage =>
            CrowdEnergyReadings.Count == 0 ? CrowdEnergy
            : CrowdEnergyReadings.Average();

        public void RecordEnergy() => CrowdEnergyReadings.Add(CrowdEnergy);

        /// <summary>
        /// Applies a crowd-energy delta.
        ///
        /// Positive deltas are compressed as the crowd approaches its ceiling — the last
        /// 20 points of a reaction are far harder to buy than the first 20. Without this,
        /// every match of every quality pinned the peak at exactly 100 and the crowd
        /// component stopped distinguishing anything.
        /// </summary>
        public void ApplyEnergy(double delta)
        {
            if (delta > 0)
            {
                double headroom = Math.Max(0, (CrowdCeiling - CrowdEnergy) / Math.Max(1, CrowdCeiling));
                delta *= 0.20 + 0.80 * Math.Sqrt(headroom);
            }

            CrowdEnergy = Math.Clamp(CrowdEnergy + delta, 0, CrowdCeiling);
            if (CrowdEnergy > CrowdPeakEnergy) CrowdPeakEnergy = CrowdEnergy;
            RecordEnergy();
        }

        public void ApplyMomentum(double delta)
        {
            RawMomentum += delta;
            Momentum = Math.Clamp(Momentum + delta, -100, 100);
        }

        /// <summary>
        /// Natural crowd decay between beats (crowd cannot sustain maximum tension indefinitely).
        /// </summary>
        public void ApplyDecay(double decayRate = 0.03) =>
            CrowdEnergy = Math.Max(0, CrowdEnergy * (1 - decayRate));
    }
}
