using WrestlingSim.Models.World;

namespace WrestlingSim.Models
{
    /// <summary>What a run of towns did to the people on it.</summary>
    public class LoopResult
    {
        public int Towns { get; init; }
        public int MinutesPerNight { get; init; }

        /// <summary>One row per wrestler, in the order they were booked.</summary>
        public List<LoopWorker> Workers { get; init; } = new();

        /// <summary>Anybody the run took off the road. Same shape a show reports.</summary>
        public List<InjuryReport> Injuries { get; init; } = new();
    }

    /// <summary>
    /// One wrestler's week on the road. Both meters, before and after, because the whole
    /// point of the loop is that it moves them in opposite directions and the booker is
    /// choosing which one they care about.
    /// </summary>
    public sealed class LoopWorker
    {
        public required Wrestler Wrestler { get; init; }

        public double SharpnessBefore { get; init; }
        public double SharpnessAfter  { get; init; }
        public double FatigueBefore   { get; init; }
        public double FatigueAfter    { get; init; }

        public double SharpnessGained => SharpnessAfter - SharpnessBefore;
        public double FatigueAdded    => FatigueAfter   - FatigueBefore;

        /// <summary>Cut short by an injury, and on which night.</summary>
        public int? HurtOnNight { get; init; }

        /// <summary>How many nights they actually worked.</summary>
        public required int NightsWorked { get; init; }
    }
}
