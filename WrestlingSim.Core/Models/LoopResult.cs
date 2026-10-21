using WrestlingSim.Models.World;

namespace WrestlingSim.Models
{
    /// <summary>What a run of towns did to the people on it.</summary>
    public class LoopResult
    {
        public int Towns { get; init; }
        public int MinutesPerNight { get; init; }

        /// <summary>
        /// The run's default format: 1 for singles, 2 for a tag run. Individuals can be
        /// working something else — see <see cref="LoopWorker.SideSize"/>.
        /// </summary>
        public int SideSize { get; init; } = 1;

        /// <summary>Anybody working a bigger side than the run's default: the protected ones.</summary>
        public IEnumerable<LoopWorker> Protected =>
            Workers.Where(w => w.SideSize > SideSize);

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

        /// <summary>
        /// They were on the televised card the same night, so the run was a town shorter for
        /// them. Reported rather than inferred from the night count, because a short count
        /// also means somebody went home hurt and the two read completely differently.
        /// </summary>
        public bool OnTelevision { get; init; }

        /// <summary>
        /// How many a side this one worked: 1 for singles, 2 for tags. Per wrestler rather
        /// than per run, because a mixed loop is the real shape of a week — and a booker
        /// reading a smaller pair of numbers needs to know whether that is the format or the
        /// wrestler.
        /// </summary>
        public int SideSize { get; init; } = 1;
    }
}
