using WrestlingSim.Enums;
using WrestlingSim.Models.World;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Models.MatchPlan
{
    public class MatchPlan
    {
        /// <summary>
        /// The two sides. A singles match is a match between two sides of one, which is
        /// why there is no separate singles plan type.
        ///
        /// <see cref="WrestlerA"/> and <see cref="WrestlerB"/> below are compatibility
        /// shims over these, so an object initialiser written for the singles-only engine
        /// still reads and behaves the same. Set either the shims or the sides for a given
        /// side, not both — the shim appends to whatever <see cref="SideA"/> currently is,
        /// so doing both would leave you with three people on one side.
        /// </summary>
        public MatchSide SideA { get; init; } = new();

        public MatchSide SideB { get; init; } = new();

        /// <summary>The wrestler who starts for side A. Shim; see <see cref="SideA"/>.</summary>
        public Wrestler WrestlerA
        {
            get => SideA.Members.Count > 0
                ? SideA.Starter
                : throw new InvalidOperationException("Side A has no members.");
            init => SideA.Members.Add(value);
        }

        /// <summary>The wrestler who starts for side B. Shim; see <see cref="SideB"/>.</summary>
        public Wrestler WrestlerB
        {
            get => SideB.Members.Count > 0
                ? SideB.Starter
                : throw new InvalidOperationException("Side B has no members.");
            init => SideB.Members.Add(value);
        }

        /// <summary>Everyone in the match, side A first.</summary>
        public IEnumerable<Wrestler> AllParticipants => SideA.Members.Concat(SideB.Members);

        /// <summary>True when either side has a partner on the apron.</summary>
        public bool IsTagMatch => SideA.IsTag || SideB.IsTag;

        public List<MatchBeat> Beats { get; set; } = new();

        // Active feud between the two sides, if any.
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
        /// True when the belt is on the line and the champion is one of the people in it.
        /// A vacant title is contested by both, which is also a title match.
        /// </summary>
        public bool IsTitleMatch => TitleAtStake != null;

        // ── Derived / validation ─────────────────────────────────────────────

        /// <summary>The side the finish beat is booked for, or null if there is no finish.</summary>
        public MatchSide? BookedWinningSide
        {
            get
            {
                var finish = Beats.LastOrDefault(b => b.IsFinish);
                if (finish == null) return null;
                return finish.Control == BeatControl.WrestlerA ? SideA : SideB;
            }
        }

        public MatchSide? BookedLosingSide
        {
            get
            {
                var winning = BookedWinningSide;
                if (winning == null) return null;
                return winning == SideA ? SideB : SideA;
            }
        }

        /// <summary>
        /// Winner inferred from the finish beat's Control.
        ///
        /// For a tag match this reports the side's *starter*, because a plan on its own
        /// does not know who will be legal when the finish lands — that depends on the
        /// tags booked in between, which only the engine resolves. The engine reports the
        /// person who actually scored the fall as
        /// <see cref="MatchEngineResult.Pinner"/>.
        /// </summary>
        public Wrestler? BookedWinner => BookedWinningSide?.Starter;

        public Wrestler? BookedLoser => BookedLosingSide?.Starter;

        /// <summary>
        /// Returns validation errors. Empty list = plan is valid to execute.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // ── Sides ────────────────────────────────────────────────────────
            // Checked first and returned on, because everything below reads people out
            // of the sides and would throw rather than report.
            if (SideA.Members.Count == 0) errors.Add("Side A has nobody in it.");
            if (SideB.Members.Count == 0) errors.Add("Side B has nobody in it.");
            if (errors.Count > 0) return errors;

            // A wrestler on both sides breaks every "which side is this person on" lookup
            // in the engine, and is not a booking anybody meant to make.
            foreach (var w in SideA.Members.Where(SideB.Contains))
                errors.Add($"{w.RingName} is booked on both sides of the match.");

            foreach (var side in new[] { SideA, SideB })
                if (side.Members.Count != side.Members.Distinct().Count())
                    errors.Add("A wrestler is booked twice on the same side.");

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
                else if (title.Champion is { } champion && !AllParticipants.Contains(champion))
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

        /// <summary>The side a beat's control refers to, or null for Even / Contested.</summary>
        public MatchSide? ControlSide(MatchBeat beat) => beat.Control switch
        {
            BeatControl.WrestlerA => SideA,
            BeatControl.WrestlerB => SideB,
            _                     => null
        };

        /// <summary>Which side this wrestler is on, or null if they are not in the match.</summary>
        public MatchSide? SideOf(Wrestler w) =>
            SideA.Contains(w) ? SideA : SideB.Contains(w) ? SideB : null;

        /// <summary>The side opposing the one given.</summary>
        public MatchSide Opposing(MatchSide side) => side == SideA ? SideB : SideA;

        /// <summary>
        /// Resolves which wrestler is "Control" for a given beat.
        ///
        /// Reports the controlling side's starter. During execution the engine resolves
        /// this against whoever is currently legal instead — see
        /// <see cref="Engine.MatchEngineState.LegalIndexFor"/>.
        /// </summary>
        public Wrestler? ControlWrestler(MatchBeat beat) => ControlSide(beat)?.Starter;

        public Wrestler OtherWrestler(Wrestler w) =>
            SideA.Contains(w) ? SideB.Starter : SideA.Starter;
    }
}
