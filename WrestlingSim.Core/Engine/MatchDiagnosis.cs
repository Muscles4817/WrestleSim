using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;
using MatchTypeEnum = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Engine
{
    public enum CauseDirection { Helped, Hurt }

    /// <summary>One thing that moved a category, in the booker's terms.</summary>
    public readonly record struct Cause(CauseDirection Direction, string Text);

    /// <summary>A scorecard row, with what put it where it is.</summary>
    public sealed record CategoryDiagnosis(string Category, IReadOnlyList<Cause> Causes);

    /// <summary>
    /// Why the match scored what it scored.
    ///
    /// The result screen could say what happened and never why. It reported Technical 46,
    /// Storytelling 47, three and a half stars, and left the player to guess which of their
    /// decisions had produced any of it — which made the outcome feel calculated rather than
    /// legible, and gave them nothing to do differently next time.
    ///
    /// Everything here reads a signal the engine already recorded. Nothing is invented and
    /// nothing is re-simulated: the repetition factor, the type coherence, the investment
    /// factor, the familiarity discount and the per-beat contributions were all being
    /// computed and thrown away. This is the same information, in sentences.
    ///
    /// Two rules hold it honest. A cause names the beat or the decision it comes from, so it
    /// can be acted on — "no near fall before the finish" is a booking note, "storytelling
    /// was low" is a restatement of the number. And a lesson is only offered where the plan
    /// could actually have been different: a cold feud is a lesson about the next three
    /// weeks, not about this match, and it says so.
    /// </summary>
    public static class MatchDiagnosis
    {
        /// <summary>Below this a beat type has been used often enough to be landing flat.</summary>
        private const double RepetitionFloor = 0.85;

        /// <summary>Below this the declared match type is not being delivered.</summary>
        private const double CoherenceFloor = 0.7;

        /// <summary>A finish the advantage did not favour. Mirrors the engine's own threshold.</summary>
        private const double EarnedFinish = 70.0;

        public static IReadOnlyList<CategoryDiagnosis> Of(MatchPlan plan, MatchEngineResult result) =>
        [
            new("Technical",    Technical(plan, result)),
            new("Storytelling", Storytelling(plan, result)),
            new("Crowd",        Crowd(result)),
            new("Finish",       Finish(result))
        ];

        // ── Technical ────────────────────────────────────────────────────────

        private static List<Cause> Technical(MatchPlan plan, MatchEngineResult result)
        {
            var causes = new List<Cause>();

            if (Best(result, b => b.TechnicalContribution) is { } best)
                causes.Add(new(CauseDirection.Helped,
                    $"{Name(best)} was the best work in the match."));

            if (plan.MatchType != MatchTypeEnum.Standard)
            {
                var off = result.BeatResults
                    .Where(b => !MatchEngine.IsOnType(b.BeatType, plan.MatchType))
                    .ToList();

                if (result.MatchTypeCoherence < CoherenceFloor && off.Count > 0)
                    causes.Add(new(CauseDirection.Hurt,
                        $"{off.Count} of {result.BeatResults.Count} beats do not deliver what a " +
                        $"{plan.MatchType.ToString().ToLowerInvariant()} match promises — " +
                        $"{Join(off.Select(Name).Distinct())}."));
                else if (result.MatchTypeCoherence >= 0.85)
                    causes.Add(new(CauseDirection.Helped,
                        $"The sheet delivered what a {plan.MatchType.ToString().ToLowerInvariant()} " +
                        "match promises."));
            }

            foreach (var worn in Repeated(result))
                causes.Add(new(CauseDirection.Hurt,
                    $"{Name(worn)} had been seen already and landed flat."));

            return causes;
        }

        // ── Storytelling ─────────────────────────────────────────────────────

        private static List<Cause> Storytelling(MatchPlan plan, MatchEngineResult result)
        {
            var causes = new List<Cause>();
            var beats = result.BeatResults;

            if (ComebackAfterPeril(result) is { } rally)
                causes.Add(new(CauseDirection.Helped,
                    $"{Name(rally)} paid off a long stretch with {result.Winner.RingName} in trouble."));

            int resonant = beats.Count(b => b.FeudalResonanceActivated);
            if (resonant > 0)
                causes.Add(new(CauseDirection.Helped,
                    $"{resonant} beat{(resonant == 1 ? "" : "s")} drew on the feud behind the match."));

            bool nearFall = beats.Any(b => b.BeatType is BeatType.NearFall);
            if (!nearFall)
                causes.Add(new(CauseDirection.Hurt,
                    "Nothing in the match made the crowd believe it might end early — " +
                    "there is no near fall anywhere in the sheet."));

            if (plan.Feud is { } feud && feud.Intensity <= FeudIntensity.Cold)
                causes.Add(new(CauseDirection.Hurt,
                    $"The feud is {feud.Intensity.ToString().ToLowerInvariant()}, so there was little " +
                    "for the escalation to be about."));
            else if (plan.Feud is null)
                // Deliberately says nothing about near falls: this fires whether or not the
                // sheet has any, and next to the "there is no near fall anywhere" cause it
                // read as the two lines contradicting each other.
                causes.Add(new(CauseDirection.Hurt,
                    "No story behind the match, so nothing in it mattered beyond the result."));

            foreach (var pair in AdjacentRepeats(result))
                causes.Add(new(CauseDirection.Hurt,
                    $"Two {Name(pair)} beats back to back read as repetition rather than escalation."));

            return causes;
        }

        // ── Crowd ────────────────────────────────────────────────────────────

        private static List<Cause> Crowd(MatchEngineResult result)
        {
            var causes = new List<Cause>();

            if (result.CrowdPeakEnergy - result.CrowdAverageEnergy >= 12)
                causes.Add(new(CauseDirection.Helped,
                    $"The room peaked at {result.CrowdPeakEnergy:F0}, well above where it sat."));

            double investment = result.Breakdown.InvestmentFactor;
            if (investment < 0.98)
                causes.Add(new(CauseDirection.Hurt,
                    $"The crowd was present but not invested, which cost " +
                    $"{(1 - investment) * 100:F0}% of the crowd score. Volume is not the same as caring."));
            else if (investment > 1.02)
                causes.Add(new(CauseDirection.Helped,
                    "The room was genuinely invested, not just loud."));

            if (result.Familiarity < 0.95)
                causes.Add(new(CauseDirection.Hurt,
                    $"This pairing is drawing {result.Familiarity * 100:F0}% of what it first did. " +
                    "The work was graded as booked; the room was not there for it."));

            if (result.BeatResults.Any(b => b.NearFallJeopardy < 1))
                causes.Add(new(CauseDirection.Hurt,
                    "A near fall landed while somebody was free to break it, so the count carried " +
                    "no jeopardy."));

            return causes;
        }

        // ── Finish ───────────────────────────────────────────────────────────

        private static List<Cause> Finish(MatchEngineResult result) =>
            result.FinishQuality >= EarnedFinish
                ? [new(CauseDirection.Helped,
                    $"The advantage favoured {result.Winner.RingName} when the finish landed, so the " +
                    "win read as earned.")]
                : [new(CauseDirection.Hurt,
                    $"{result.Winner.RingName} was not ahead when the finish landed, so the win read " +
                    "as unearned and finish quality took the hit.")];

        // ── Lessons ──────────────────────────────────────────────────────────

        /// <summary>
        /// What to do differently, most valuable first.
        ///
        /// Only things the booker could change in the booking. The familiarity discount and a
        /// cold feud are real and are reported as causes, but neither is fixable inside this
        /// match, so the lesson for them is about what to book before the rematch rather than
        /// about the sheet.
        /// </summary>
        public static IReadOnlyList<string> Lessons(MatchPlan plan, MatchEngineResult result)
        {
            var lessons = new List<string>();
            var beats = result.BeatResults;

            if (result.FinishQuality < EarnedFinish)
                lessons.Add($"Book the heat and the comeback so {result.Winner.RingName} is ahead when " +
                            "the finish lands. An unearned finish is the most expensive thing on the card.");

            if (!beats.Any(b => b.BeatType is BeatType.NearFall))
                lessons.Add("Put a near fall in before the finish. Without one the crowd is never asked " +
                            "whether the match might already be over.");

            foreach (var pair in AdjacentRepeats(result).Take(1))
                lessons.Add($"Replace the second {Name(pair)} with a hope spot or a false finish. Two of " +
                            "the same beat in a row is repetition where the plan wanted escalation.");

            if (plan.MatchType != MatchTypeEnum.Standard && result.MatchTypeCoherence < CoherenceFloor)
                lessons.Add($"Book the beats a {plan.MatchType.ToString().ToLowerInvariant()} match is " +
                            "graded on, or declare a different one. Promising one match and working " +
                            "another is paid for twice.");

            if (plan.Feud is null || plan.Feud.Intensity <= FeudIntensity.Cold)
                lessons.Add("Build the feud with segments before running this pairing again. The booking " +
                            "was sound; there was nothing behind it.");

            if (result.Familiarity < 0.95)
                lessons.Add("Give this pairing a rest. The crowd has seen it recently enough that the " +
                            "same match would be graded the same and drawn less.");

            return lessons.Take(3).ToList();
        }

        // ── Reading the beats ────────────────────────────────────────────────

        private static BeatResult? Best(MatchEngineResult result, Func<BeatResult, double> by)
        {
            var top = result.BeatResults.OrderByDescending(by).FirstOrDefault();
            return top is not null && by(top) > 0 ? top : null;
        }

        /// <summary>
        /// A comeback that followed a real stretch of trouble, rather than one booked into a
        /// match the winner was already running. Two consecutive beats worked by somebody
        /// else is the shortest run that reads as peril.
        /// </summary>
        private static BeatResult? ComebackAfterPeril(MatchEngineResult result)
        {
            var beats = result.BeatResults;
            for (int i = 2; i < beats.Count; i++)
            {
                if (beats[i].BeatType is not (BeatType.Comeback or BeatType.HotTag)) continue;
                if (beats[i].Worker != result.Winner) continue;

                bool peril = beats[i - 1].Worker is { } a && a != result.Winner
                          && beats[i - 2].Worker is { } b && b != result.Winner;

                if (peril) return beats[i];
            }
            return null;
        }

        /// <summary>Beat types the crowd had already tired of by the time they were used again.</summary>
        private static IEnumerable<BeatResult> Repeated(MatchEngineResult result) =>
            result.BeatResults
                  .Where(b => b.RepetitionFactor < RepetitionFloor)
                  .GroupBy(b => b.BeatType)
                  .Select(g => g.First());

        /// <summary>The same beat type twice in a row, which is the shape of a flat match.</summary>
        private static IEnumerable<BeatResult> AdjacentRepeats(MatchEngineResult result)
        {
            var beats = result.BeatResults;
            var seen = new HashSet<BeatType>();
            for (int i = 1; i < beats.Count; i++)
                if (beats[i].BeatType == beats[i - 1].BeatType && seen.Add(beats[i].BeatType))
                    yield return beats[i];
        }

        private static string Name(BeatResult b) =>
            BeatLibrary.All.FirstOrDefault(t => t.Type == b.BeatType)?.Name
            ?? b.BeatLabel.ToLowerInvariant();

        private static string Join(IEnumerable<string> parts)
        {
            var list = parts.ToList();
            return list.Count <= 1 ? list.FirstOrDefault() ?? ""
                 : string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1];
        }
    }
}
