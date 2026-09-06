using WrestlingSim.Enums;

namespace WrestlingSim.Engine
{
    internal class MatchEngineState
    {
        // ── Live match state ─────────────────────────────────────────────────

        /// <summary>0–100. How engaged the crowd is right now.</summary>
        public double CrowdEnergy { get; set; }

        /// <summary>
        /// –100 to +100. Positive = WrestlerA has the advantage, negative = WrestlerB.
        ///
        /// This is *in-match* advantage — who is on top right now — and it resets every
        /// match. Not to be confused with <see cref="Models.Wrestler.Momentum"/>, which is
        /// a career-level hot/cold trend that persists between shows.
        /// </summary>
        public double Advantage { get; set; }

        /// <summary>
        /// Uncapped running total of all momentum deltas. Unlike Advantage, this is never
        /// clamped, so it accumulates across multiple heat segments and correctly measures
        /// how deep a hole a wrestler has dug — used to scale the comeback earned bonus.
        /// </summary>
        public double RawAdvantage { get; private set; }

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

        // ── Legal performers ─────────────────────────────────────────────────

        // Who is currently in the ring for each side, as an index into that side's
        // Members. Lives here rather than on MatchSide because a MatchPlan is a booking
        // and can be executed more than once — the test suite re-runs the same plan to
        // compare bookings, and a plan that remembered who was legal last time would not
        // survive that.
        private int _legalA;
        private int _legalB;

        /// <summary>Index of side A's legal performer.</summary>
        public int LegalA => _legalA;

        /// <summary>Index of side B's legal performer.</summary>
        public int LegalB => _legalB;

        public int LegalIndexFor(bool sideA) => sideA ? _legalA : _legalB;

        /// <summary>Sets who begins the match for each side.</summary>
        public void InitialiseLegal(int sideA, int sideB)
        {
            _legalA = sideA;
            _legalB = sideB;
        }

        /// <summary>
        /// Brings a fresh performer in for one side and returns who is now legal.
        ///
        /// With an explicit index, that member comes in. Without one the tag goes to the
        /// next member round, which is the only sensible default for a two-man team and a
        /// reasonable one for a trio.
        /// </summary>
        public int Tag(bool sideA, int memberCount, int? incoming = null)
        {
            int current = sideA ? _legalA : _legalB;

            // An explicit index naming the man who is already legal used to fall through
            // to "next man round", silently bringing in somebody the booker did not ask
            // for. On a two-man side that is invisible; on a trio it is a different match.
            // MatchPlan.Validate rejects it now, so this only has to be defensive.
            int next = incoming is { } i && i >= 0 && i < memberCount
                ? i
                : (current + 1) % Math.Max(1, memberCount);

            if (sideA) _legalA = next; else _legalB = next;

            // Coming in fresh is the whole point of a tag, so the charge the isolation
            // built is spent and starts again from nothing.
            ClearTagCharge(sideA);
            return next;
        }

        // ── Hot-tag charge ───────────────────────────────────────────────────

        // How much stored energy each side's corner has built while its man has been cut
        // off. Indexed 0 = side A, 1 = side B, and always credited to the side being
        // worked over rather than the side doing the working — it is their tag to make.
        private readonly int[] _isolations = new int[2];
        private readonly int[] _nearTags   = new int[2];

        // How many isolation beats in a row this side has taken with no hope spot in
        // between. Deliberately separate from the charge above, because the two measure
        // different things: the charge is what was spent buying the payoff and resets on a
        // tag; the run is how long the room has been asked to wait and resets on a *near
        // tag* as well. docs/wrestling-reference/18-match-craft.md §2.3 is explicit that a
        // long heat is good and that the hope spots are what make it bearable — so what
        // costs the crowd is a run of isolations, never the count of them.
        private readonly int[] _isolationRun = new int[2];

        /// <summary>Isolation beats this side has suffered since its last tag.</summary>
        public int IsolationsSuffered(bool sideA) => _isolations[sideA ? 0 : 1];

        /// <summary>Tags this side has been denied since its last successful one.</summary>
        public int NearTagsDenied(bool sideA) => _nearTags[sideA ? 0 : 1];

        /// <summary>Consecutive isolations this side has taken without a hope spot.</summary>
        public int IsolationRun(bool sideA) => _isolationRun[sideA ? 0 : 1];

        public void RecordIsolation(bool isolatedSideA)
        {
            _isolations[isolatedSideA ? 0 : 1]++;
            _isolationRun[isolatedSideA ? 0 : 1]++;
        }

        /// <summary>
        /// A denied tag does two jobs: it charges the payoff, and it buys the room back.
        /// Reaching for the corner and being dragged away is the hope spot — it is what
        /// stops a long heat becoming a crowd that has given up.
        /// </summary>
        public void RecordNearTag(bool reachingSideA)
        {
            _nearTags[reachingSideA ? 0 : 1]++;
            _isolationRun[reachingSideA ? 0 : 1] = 0;
        }

        private void ClearTagCharge(bool sideA)
        {
            _isolations[sideA ? 0 : 1]   = 0;
            _nearTags[sideA ? 0 : 1]     = 0;
            _isolationRun[sideA ? 0 : 1] = 0;
        }

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

        public void ApplyAdvantage(double delta)
        {
            RawAdvantage += delta;
            Advantage = Math.Clamp(Advantage + delta, -100, 100);
        }

        /// <summary>
        /// Natural crowd decay between beats (crowd cannot sustain maximum tension indefinitely).
        /// </summary>
        public void ApplyDecay(double decayRate = 0.03) =>
            CrowdEnergy = Math.Max(0, CrowdEnergy * (1 - decayRate));
    }
}
