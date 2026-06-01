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

        // ── Accumulators ─────────────────────────────────────────────────────

        public double TechnicalScore    { get; set; }
        public double StorytellingScore { get; set; }

        /// <summary>Highest crowd energy reached at any point.</summary>
        public double CrowdPeakEnergy { get; set; }

        /// <summary>Running readings for average crowd calculation.</summary>
        public List<double> CrowdEnergyReadings { get; set; } = new();

        // ── Near-fall tracking ───────────────────────────────────────────────

        /// <summary>Total near falls executed so far; drives diminishing returns.</summary>
        public int NearFallCount { get; set; }

        // ── Finish ───────────────────────────────────────────────────────────

        public double FinishQuality { get; set; }

        // ── In-match heat deltas per wrestler (ring name → delta) ─────────────

        public Dictionary<string, double> InMatchHeat { get; set; } = new();

        // ── Helpers ──────────────────────────────────────────────────────────

        public double CrowdAverage =>
            CrowdEnergyReadings.Count == 0 ? CrowdEnergy
            : CrowdEnergyReadings.Average();

        public void RecordEnergy() => CrowdEnergyReadings.Add(CrowdEnergy);

        public void ApplyEnergy(double delta)
        {
            CrowdEnergy = Math.Clamp(CrowdEnergy + delta, 0, 100);
            if (CrowdEnergy > CrowdPeakEnergy) CrowdPeakEnergy = CrowdEnergy;
            RecordEnergy();
        }

        public void ApplyMomentum(double delta) =>
            Momentum = Math.Clamp(Momentum + delta, -100, 100);

        /// <summary>
        /// Natural crowd decay between beats (crowd cannot sustain maximum tension indefinitely).
        /// </summary>
        public void ApplyDecay(double decayRate = 0.03) =>
            CrowdEnergy = Math.Max(0, CrowdEnergy * (1 - decayRate));
    }
}
