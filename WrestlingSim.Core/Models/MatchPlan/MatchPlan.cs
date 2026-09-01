using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Models.MatchPlan
{
    public class MatchPlan
    {
        public required Wrestler WrestlerA { get; init; }
        public required Wrestler WrestlerB { get; init; }

        public List<MatchBeat> Beats { get; set; } = new();

        // Active feud between the two wrestlers, if any.
        public Feud? Feud { get; set; }

        public MatchType MatchType { get; set; } = MatchType.Standard;

        /// <summary>
        /// The championship on the line, or null for a non-title match.
        ///
        /// A title creates automatic stakes for any match involving it
        /// (docs/wrestling-reference/21-championships.md §1.1), so this changes what the
        /// crowd brings to the opening bell, what the winner takes away, and — via
        /// <see cref="Engine.TitleEconomy"/> — what the belt itself is worth afterwards.
        /// </summary>
        public Title? TitleAtStake { get; set; }

        /// <summary>
        /// True when the belt is on the line and the champion is one of the two people in
        /// it. A vacant title is contested by both, which is also a title match.
        /// </summary>
        public bool IsTitleMatch => TitleAtStake != null;

        // ── Derived / validation ─────────────────────────────────────────────

        /// <summary>Winner inferred from the finish beat's Control field.</summary>
        public Wrestler? BookedWinner
        {
            get
            {
                var finish = Beats.LastOrDefault(b => b.IsFinish);
                if (finish == null) return null;
                return finish.Control == BeatControl.WrestlerA ? WrestlerA : WrestlerB;
            }
        }

        public Wrestler? BookedLoser
        {
            get
            {
                var winner = BookedWinner;
                if (winner == null) return null;
                return winner == WrestlerA ? WrestlerB : WrestlerA;
            }
        }

        /// <summary>
        /// Returns validation errors. Empty list = plan is valid to execute.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (!Beats.Any())
                errors.Add("Plan has no beats.");

            if (!Beats.Any(b => b.IsOpening))
                errors.Add("Plan has no opening beat.");

            var finishBeats = Beats.Where(b => b.IsFinish).ToList();
            if (finishBeats.Count == 0)
                errors.Add("Plan has no finish beat.");
            else if (finishBeats.Count > 1)
                errors.Add("Plan has more than one finish beat.");
            else if (!Beats.Last().IsFinish)
                errors.Add("Finish beat must be the last beat.");
            else if (finishBeats[0].Control is not (BeatControl.WrestlerA or BeatControl.WrestlerB))
                // BookedWinner reads the finish's Control, so Even/Contested silently resolved
                // to WrestlerB while the engine's commentary credited WrestlerA. A finish has
                // to say who won.
                errors.Add("Finish beat must be controlled by WrestlerA or WrestlerB — a finish decides who wins.");

            // ── Title ────────────────────────────────────────────────────────
            if (TitleAtStake is { } title)
            {
                if (title.Retired)
                    errors.Add($"{title.Name} has been retired and cannot be defended.");

                // A champion who is not in the match cannot lose the belt in it, so this
                // is a non-title match with a misleading label rather than a title match.
                else if (title.Champion is { } champion
                         && champion != WrestlerA && champion != WrestlerB)
                    errors.Add(
                        $"{title.Name} cannot be on the line here — {champion.RingName} holds it " +
                        "and is not in this match.");
            }

            // Feud-gated beats require an active feud
            foreach (var beat in Beats)
            {
                if (beat.Type == BeatType.FeudalEscalation && (Feud == null || Feud.Intensity < FeudIntensity.Building))
                    errors.Add($"FeudalEscalation requires a feud of at least Building intensity.");

                if (beat.Type == BeatType.ThirdPartyPullIn && (Feud == null || (!Feud.HasTag(FeudHistoryTag.FamilyInvolved) && !Feud.HasTag(FeudHistoryTag.ManagerConflict))))
                    errors.Add($"ThirdPartyPullIn requires feud history tag FamilyInvolved or ManagerConflict.");

                if (beat.Type == BeatType.AlliesRejected)
                {
                    var beatIndex = Beats.IndexOf(beat);
                    bool hasPriorPullIn = Beats.Take(beatIndex).Any(b => b.Type == BeatType.ThirdPartyPullIn);
                    if (!hasPriorPullIn)
                        errors.Add("AlliesRejected requires a ThirdPartyPullIn earlier in the plan.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Resolves which wrestler is "Control" for a given beat,
        /// returning the actual Wrestler object.
        /// </summary>
        public Wrestler? ControlWrestler(MatchBeat beat) => beat.Control switch
        {
            BeatControl.WrestlerA => WrestlerA,
            BeatControl.WrestlerB => WrestlerB,
            _                     => null  // Even / Contested
        };

        public Wrestler OtherWrestler(Wrestler w) => w == WrestlerA ? WrestlerB : WrestlerA;
    }
}
