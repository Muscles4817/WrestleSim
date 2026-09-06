using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// One wrestler choosing a grudge over the win, and who it was done to.
    ///
    /// <paramref name="By"/> is the one who did it — broke the cover, walked past the
    /// opening. <paramref name="Against"/> is the one it was done to, and therefore the one
    /// who leaves with a grievance.
    ///
    /// The direction decides *how much* — whether the aggrieved party was pinned is read off
    /// <see cref="Against"/> — and what the show reports. It does **not** decide where the
    /// heat lands: a feud is keyed on its camps sorted, so depositing it is symmetric, and
    /// swapping these two would change nothing about the story's temperature. Worth saying
    /// plainly, because a field that looks directional and is only half directional is the
    /// kind of thing a later reader will assume more of than it does.
    /// </summary>
    public readonly record struct GrudgeMoment(Wrestler By, Wrestler Against, BeatType Beat);
}
