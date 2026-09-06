using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// What a room is doing, as a profile rather than a number.
    ///
    /// The engine carried crowd reaction as a single 0–100 scalar. That number cannot
    /// answer the question a booker actually needs answered — *is anyone invested?* — and
    /// it conflates the two quiet rooms that mean opposite things: an audience holding its
    /// breath and an audience that has stopped caring
    /// (docs/wrestling-reference/16-crowd-psychology.md §2).
    ///
    /// Each component accumulates over a match. They are not shares of a whole and do not
    /// sum to anything in particular; a match can be loud and disengaged at once, which is
    /// exactly what a hijacked crowd is.
    /// </summary>
    public sealed class CrowdReaction
    {
        /// <summary>Cheers for something the audience wanted.</summary>
        public double Pop { get; private set; }

        /// <summary>Boos aimed at somebody they want beaten. Engagement, and useful.</summary>
        public double Heat { get; private set; }

        /// <summary>Invested quiet — the held breath before a near-fall or a denied tag.</summary>
        public double Tension { get; private set; }

        /// <summary>Nothing at all. The failure state.</summary>
        public double Silence { get; private set; }

        /// <summary>Boos with disengagement. Louder than silence and worse than it.</summary>
        public double GoAwayHeat { get; private set; }

        // ── Readings ─────────────────────────────────────────────────────────

        /// <summary>
        /// How much the room cared, whichever way it cared. Everything except the two
        /// kinds of not-caring.
        /// </summary>
        public double Engagement => Pop + Heat + Tension;

        /// <summary>How much the room has actively checked out.</summary>
        public double Disengagement => Silence + GoAwayHeat;

        /// <summary>
        /// 0–1. The share of the night the audience was actually present for. This is the
        /// number the rating reads, and the reason a loud disengaged match now grades below
        /// a quiet invested one.
        /// </summary>
        public double Investment
        {
            get
            {
                double total = Engagement + Disengagement;
                return total <= 0 ? 0 : Engagement / total;
            }
        }

        /// <summary>Nothing has been recorded — this reaction describes no match.</summary>
        public bool IsEmpty => Total <= 0;

        /// <summary>Everything recorded, across all five components.</summary>
        public double Total => Pop + Heat + Tension + Silence + GoAwayHeat;

        /// <summary>
        /// Whichever reaction the match produced most of.
        ///
        /// An unpopulated reaction reads as <see cref="ReactionKind.Silence"/> rather than
        /// as <see cref="ReactionKind.Pop"/>. It used to be Pop, because the search began at
        /// `pairs[0]` with a strict `&gt;` — so an all-zero profile claimed the loudest
        /// possible reading, and `MatchEngineResult.Reaction` defaults to `new()`, meaning
        /// any result not produced by the engine said so. Silence is the honest default:
        /// nothing was recorded, so nothing happened.
        ///
        /// **Ties resolve the same way, which they did not until a test asked.** The
        /// `IsEmpty` guard fixed the all-zero case and left every other tie resolving to
        /// whichever component happened to be listed first — which was still Pop. A room
        /// recorded as forty parts cheering and forty parts silence was reported as a pop.
        /// So the list is ordered from the most pessimistic reading to the most flattering
        /// and searched with a strict `&gt;`: a tie goes to the quieter of the two, all the
        /// way up. Exact ties are vanishingly rare in a real match, so this is about the
        /// property being true rather than about any match's rating — but "Silence is the
        /// honest default" is either the rule or it is not.
        /// </summary>
        public ReactionKind Dominant
        {
            get
            {
                if (IsEmpty) return ReactionKind.Silence;

                // Ordered quietest-and-worst first, so a strict `>` sends every tie to the
                // less flattering reading: nothing < they left < held breath < booing <
                // cheering.
                var pairs = new (ReactionKind Kind, double Value)[]
                {
                    (ReactionKind.Silence, Silence),
                    (ReactionKind.GoAwayHeat, GoAwayHeat),
                    (ReactionKind.Tension, Tension),
                    (ReactionKind.Heat, Heat),
                    (ReactionKind.Pop, Pop)
                };

                var best = pairs[0];
                foreach (var p in pairs) if (p.Value > best.Value) best = p;
                return best.Kind;
            }
        }

        /// <summary>A plain-English reading, in the register the rest of the game uses.</summary>
        public string Label => IsEmpty ? "Nothing recorded" : Investment switch
        {
            // `>=` deliberately: with nothing to separate them, "the room never turned up"
            // is the quieter and more honest reading. The strict `>` sent a tie to the
            // loudest failure mode in the vocabulary.
            < 0.35 => Silence >= GoAwayHeat
                ? "The room never turned up"
                : "They stopped watching and started entertaining themselves",
            < 0.55 => "Patchy — the crowd came and went",
            < 0.75 => Dominant switch
            {
                ReactionKind.Heat    => "A hostile room, and hostile is engaged",
                ReactionKind.Tension => "Quiet, but the good kind of quiet",
                _                    => "A warm room"
            },
            _ => Dominant switch
            {
                ReactionKind.Heat => "They wanted somebody beaten, badly",
                _                 => "The building was theirs all night"
            }
        };

        // ── Accumulation ─────────────────────────────────────────────────────

        public void Add(ReactionKind kind, double amount)
        {
            if (amount <= 0) return;

            switch (kind)
            {
                case ReactionKind.Pop:        Pop        += amount; break;
                case ReactionKind.Heat:       Heat       += amount; break;
                case ReactionKind.Tension:    Tension    += amount; break;
                case ReactionKind.Silence:    Silence    += amount; break;
                case ReactionKind.GoAwayHeat: GoAwayHeat += amount; break;
            }
        }

        public override string ToString() =>
            $"pop {Pop:F1} · heat {Heat:F1} · tension {Tension:F1} · " +
            $"silence {Silence:F1} · go-away {GoAwayHeat:F1} ({Investment:P0} invested)";
    }
}
