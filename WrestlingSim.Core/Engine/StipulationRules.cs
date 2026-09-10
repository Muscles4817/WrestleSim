using WrestlingSim.Enums;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What a gimmick match takes away, what it demands in return, and what it is worth.
    ///
    /// Doc 20 §6.2 is the ladder; doc 04 §5 names the cost of overusing it — "every match
    /// becoming a gimmick match makes the cage mean nothing" — and doc 17 §4.1 puts a number
    /// on that: a stipulation match type is fresh for **1–2 uses per year, per promotion**.
    ///
    /// Three rules, and all three have to be here or the feature is a free rating bonus:
    ///
    ///   • **It removes escape routes.** <see cref="Allows"/>. Doc 04 says the screwjob
    ///     finish "protects both, sells the rematch"; a stipulation is the removal of that.
    ///   • **It has to be earned.** <see cref="Escalation"/>. Doc 20 §6: the stipulation
    ///     must be *proportional* to what was built, and a cage for a feud that never got
    ///     past words is worse than no cage at all.
    ///   • **It has to be rare.** <see cref="Scarcity"/>. Doc 20 §9: "using a cage match
    ///     monthly should devalue all cage matches."
    ///
    /// Every one of these is a pure function, so each can be asserted as itself rather than
    /// inferred from a match that ran — which is this codebase's standing lesson.
    /// </summary>
    public static class StipulationRules
    {
        // ── What it takes away ───────────────────────────────────────────────

        /// <summary>
        /// Whether this finish can end a match under this stipulation.
        ///
        /// The one property that holds across the whole enum, and the sentence the feature
        /// exists to state: **every stipulation forbids the disqualification and the
        /// count-out**. Those are the two finishes that let somebody lose without being
        /// beaten (<see cref="FinishWeight.Protected"/>), so removing them is what a
        /// gimmick match *is*. Everything else below is one rung's particular character.
        /// </summary>
        public static bool Allows(Stipulation stipulation, BeatType finish)
        {
            if (stipulation == Stipulation.None) return true;

            // The universal rule. Nothing on the ladder lets you lose cheaply.
            if (finish is BeatType.FinishDQ or BeatType.FinishCountout) return false;

            return stipulation switch
            {
                // A run-in is legal, which is precisely why it stops being an excuse —
                // see Weigh.
                Stipulation.NoDisqualification => true,

                // What the cage is *for*. Doc 20: "nobody escapes, nobody interferes."
                Stipulation.SteelCage => finish != BeatType.FinishInterference,

                // "Only unconsciousness ends it." Nothing that relies on the other person
                // being briefly beaten — a roll-up, a hold they can escape — counts, and
                // nobody outside the match can end it for them.
                Stipulation.LastManStanding =>
                    finish is BeatType.FinishClean or BeatType.FinishSuperFinisher,

                // The only rung that names a way to win instead of removing several.
                Stipulation.IQuit => finish == BeatType.FinishSubmission,

                _ => true
            };
        }

        /// <summary>
        /// How decisive the audience found this finish, given the rules it happened under.
        ///
        /// Exactly one cell differs from <see cref="HeatEconomy.WeightOf(BeatType)"/>, and
        /// it is the sharpest thing in this file: **a run-in in a No-DQ match is a clean
        /// loss.** Interference reads as protected because it is against the rules — the
        /// audience forgives a loss it can blame on a rule being broken. Announce that
        /// there are no rules and there is nothing left to blame.
        ///
        /// It follows, through <see cref="TitleEconomy.ChangesHands"/> and without a line
        /// of code there, that a championship *does* change hands on a run-in finish once
        /// the match is No DQ. That is the classic, and it is the reason a booker who needs
        /// a belt off somebody reaches for a stipulation.
        /// </summary>
        public static FinishWeight Weigh(Stipulation stipulation, BeatType finish)
        {
            var plain = HeatEconomy.WeightOf(finish);

            if (stipulation == Stipulation.None) return plain;
            if (!Allows(stipulation, finish)) return plain;

            // A roll-up is still a fluke: it is a legal pinfall either way, and the
            // stipulation has no opinion about whether it convinced anybody.
            return plain == FinishWeight.Protected ? FinishWeight.Decisive : plain;
        }

        /// <summary>
        /// Whether a feud declared as blown off by this finish actually got resolved.
        ///
        /// Doc 20 §6.1: a blow-off has to *resolve*. A count-out settles nothing, and
        /// neither does a run-in — under the rules. Take the rules away and the run-in
        /// settles everything, which is doc 20's whole argument for reaching for a
        /// stipulation at the end of a feud: the stipulation is what removes the escape
        /// route that would otherwise leave the story open.
        ///
        /// Extracted rather than left inline because it was written twice, in the Blazor
        /// match screen and the console booking flow, and tested in neither.
        /// </summary>
        public static bool SettlesAFeud(Stipulation stipulation, BeatType finish) =>
            Weigh(stipulation, finish) != FinishWeight.Protected;

        // ── What it demands ──────────────────────────────────────────────────

        /// <summary>
        /// The least a feud has to have reached before this rung reads as earned.
        ///
        /// Doc 20 §6.2's ladder in order, mapped onto the intensity tiers the feud book
        /// already keeps. This is the closest thing available to the "escalation level"
        /// doc 20 §9 asks for and nothing yet provides — intensity is derived from heat, so
        /// it measures how *much* has happened rather than *what*. Named here rather than
        /// hidden so that the day a real escalation level exists, there is one function to
        /// move it to.
        /// </summary>
        public static FeudIntensity Demands(Stipulation stipulation) => stipulation switch
        {
            Stipulation.NoDisqualification => FeudIntensity.Building,
            Stipulation.SteelCage          => FeudIntensity.Hot,
            Stipulation.LastManStanding    => FeudIntensity.Hot,
            Stipulation.IQuit              => FeudIntensity.Nuclear,
            _                              => FeudIntensity.None
        };

        /// <summary>
        /// Whether the story earned this, from −1 (absurd) to +1 (earned).
        ///
        /// Reaching the rung is the whole of the credit — a feud hotter than the
        /// stipulation demands does not get paid twice, because the heat it built is
        /// already paying out through <see cref="Models.MatchPlan.Feud.StartingEnergyBonus"/>.
        /// What is graded here is only the gap on the wrong side of it.
        /// </summary>
        public static double Escalation(Stipulation stipulation, FeudIntensity reached)
        {
            if (stipulation == Stipulation.None) return 0.0;

            int gap = (int)reached - (int)Demands(stipulation);
            if (gap >= 0) return 1.0;

            return gap switch
            {
                -1 => -0.35,
                -2 => -0.70,
                _  => -1.00
            };
        }

        // ── How rare it has to be ────────────────────────────────────────────

        /// <summary>How long a stipulation takes to become special again. Doc 17 §4.1.</summary>
        public const int FreshAfterDays = 180;

        /// <summary>
        /// How much of this stipulation's value survives how recently the promotion last
        /// ran one, 0.15–1.
        ///
        /// Null means never used, which is worth full value. It floors above zero rather
        /// than at it because a cage match run last month is a tired idea, not a
        /// non-existent one — the crowd still knows what a cage is.
        /// </summary>
        public static double Scarcity(int? daysSinceLastUse) =>
            daysSinceLastUse is not { } days
                ? 1.0
                : Math.Clamp((double)days / FreshAfterDays, 0.15, 1.0);

        // ── What it is worth ─────────────────────────────────────────────────

        /// <summary>
        /// Most crowd energy this rung can put in the room, before proportion and scarcity.
        ///
        /// Scaled against the two stakes bonuses already in the engine — a title tops out
        /// at +14 (<see cref="Models.World.Title.StakesBonus"/>) and a nuclear feud at +18.
        /// A stipulation sits alongside those rather than above them: it is what the story
        /// escalated *into*, not a substitute for having a story.
        /// </summary>
        private static double TopBonus(Stipulation stipulation) => stipulation switch
        {
            Stipulation.NoDisqualification => 7.0,
            Stipulation.SteelCage          => 11.0,
            Stipulation.LastManStanding    => 12.0,
            Stipulation.IQuit              => 15.0,
            _                              => 0.0
        };

        /// <summary>
        /// What booking this stipulation, for this feud, this recently, puts in the room —
        /// in the same crowd-energy points a title and a feud already contribute.
        ///
        /// **Scarcity multiplies the reward and not the penalty**, and that asymmetry is
        /// the load-bearing line in this file. A cage match nobody earned is a bad booking
        /// whether or not the promotion has run one lately; letting a long gap soften it
        /// would mean the way to book an unearned cage is to wait, which is not a lesson
        /// about wrestling. Being overdue can make a good idea better. It cannot make a
        /// bad one good.
        /// </summary>
        public static double StakesBonus(Stipulation stipulation, FeudIntensity reached,
                                         int? daysSinceLastUse)
        {
            if (stipulation == Stipulation.None) return 0.0;

            double earned = Escalation(stipulation, reached);
            double top    = TopBonus(stipulation);

            return earned >= 0
                ? top * earned * Scarcity(daysSinceLastUse)
                : top * earned;
        }

        // ── Words for the player ─────────────────────────────────────────────

        public static string Label(Stipulation stipulation) => stipulation switch
        {
            Stipulation.NoDisqualification => "No Disqualification",
            Stipulation.SteelCage          => "Steel Cage",
            Stipulation.LastManStanding    => "Last Man Standing",
            Stipulation.IQuit              => "I Quit",
            _                              => "Under the rules"
        };

        /// <summary>Doc 20 §6.2's "Says" column — what booking this announces.</summary>
        public static string Says(Stipulation stipulation) => stipulation switch
        {
            Stipulation.NoDisqualification => "Rules can't contain this.",
            Stipulation.SteelCage          => "Nobody escapes, nobody interferes.",
            Stipulation.LastManStanding    => "Only unconsciousness ends it.",
            Stipulation.IQuit              => "Submission of will, not just body.",
            _                              => ""
        };
    }
}
