using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>One stage of a match, before any beat has been chosen to fill it.</summary>
    public enum MatchPhase
    {
        /// <summary>The bell. Establishes the tone and nothing else.</summary>
        Opening,

        /// <summary>The protagonist looks good. Doc 18 §2.3's second stage.</summary>
        Shine,

        /// <summary>The turn. One moment, and the match changes hands.</summary>
        Cutoff,

        /// <summary>The control section. Where a match is actually built.</summary>
        Heat,

        /// <summary>A tease inside the heat. Bought back, not given.</summary>
        HopeSpot,

        /// <summary>The valley. A long match needs one or nobody can work it.</summary>
        Rest,

        /// <summary>Character work in the middle of a beating.</summary>
        Escalation,

        /// <summary>The payoff. What every hope spot was borrowing against.</summary>
        Comeback,

        /// <summary>Near falls. The finishing stretch.</summary>
        Stretch,

        /// <summary>Somebody outside the match gets involved.</summary>
        Outside,

        /// <summary>The end.</summary>
        Finish
    }

    /// <summary>
    /// A stage of the match with everything decided except which beat fills it.
    ///
    /// <paramref name="Why"/> is not decoration. It is what lets the review screen say
    /// "Charlotte controls this because you booked a face-in-peril" instead of showing a
    /// list of beats with no account of itself, which is what the structure picker did.
    /// </summary>
    /// <param name="Intensity">
    /// Null where the beat template's own default is the right answer. An opening is the
    /// case that matters: a feeling-out process is Low because it is a feeling-out process,
    /// and having the grammar restate that is a second place for it to be wrong.
    /// </param>
    public readonly record struct PhaseSlot(
        MatchPhase     Phase,
        BeatControl    Control,
        BeatIntensity? Intensity,
        BeatDuration?  Duration,
        string         Why);

    /// <summary>
    /// The shape of a match, derived from what the booker asked for.
    ///
    /// **This is the piece that replaces the structure library.** A structure was a frozen
    /// list of beats; this is the rule that produced those lists, which means it can produce
    /// the ones nobody wrote down. The proof that it is the same rule is in the tests: the
    /// four singles structures the library shipped come back out of it beat for beat.
    ///
    /// Two inputs and they do different jobs. <see cref="MatchStory"/> decides *who controls
    /// what* and which pool the beats come from. <see cref="MatchScale"/> decides *how many
    /// of each*, straight off doc 18 §3.1 — "15–25 min adds a second heat/comeback cycle and
    /// a real finishing stretch" is a sentence about counts, and this is that sentence.
    ///
    /// Nothing here picks a beat. That is <see cref="BriefDirector"/>, and the split is the
    /// point: the shape is deterministic and the filling is not, so a booker gets the same
    /// match every time in structure and never in detail.
    /// </summary>
    public static class PhaseGrammar
    {
        /// <summary>
        /// What sits between the cut-off and the comeback, per length and per cycle.
        ///
        /// The valley is the part that separates a fifteen-minute match from a six-minute
        /// one. A short match cuts off, heats and comes back; there is no room for anything
        /// else and putting it in makes the comeback arrive too late for the time left.
        /// A long one needs the hope spots, and above twenty-five minutes it needs the rest
        /// as well, because the performers do.
        /// </summary>
        private static IReadOnlyList<MatchPhase> Valley(MatchScale length, int cycle) =>
            (length, cycle) switch
            {
                (MatchScale.Opener,     _) => [],
                (MatchScale.Television, _) => [],
                (MatchScale.Workhorse,  _) => [MatchPhase.HopeSpot, MatchPhase.Rest, MatchPhase.HopeSpot],
                (MatchScale.BigMatch,   _) => [MatchPhase.HopeSpot],
                (MatchScale.Epic,       0) => [MatchPhase.HopeSpot, MatchPhase.Rest, MatchPhase.HopeSpot],
                (MatchScale.Epic,       _) => [MatchPhase.HopeSpot, MatchPhase.Escalation],
                _                           => [MatchPhase.HopeSpot]
            };

        /// <summary>How many heat/comeback cycles. Doc 18 §3.1's table, as a number.</summary>
        public static int Cycles(MatchScale length) => length switch
        {
            MatchScale.BigMatch => 2,
            MatchScale.Epic     => 2,
            _                    => 1
        };

        /// <summary>
        /// How many near falls before the finish.
        ///
        /// Zero on the short lengths on purpose. A near fall is a promise the finish has to
        /// beat, and a six-minute match has nothing left to beat it with — which is why the
        /// TV formula goes comeback straight to finish and why that reads as clean rather
        /// than abrupt.
        /// </summary>
        public static int NearFalls(MatchScale length) => length switch
        {
            MatchScale.Opener     => 0,
            MatchScale.Television => 0,
            MatchScale.Workhorse  => 2,
            MatchScale.BigMatch   => 2,
            MatchScale.Epic       => 3,
            _                      => 1
        };

        /// <summary>
        /// Whether the protagonist gets to look good before the match turns.
        ///
        /// Everything except the shortest match and the two shapes that are *about* not
        /// having one: a showcase has no protagonist to shine, and the whole point of David
        /// and Goliath is that he never gets going in the first place.
        /// </summary>
        public static bool HasShine(MatchStory story, MatchScale length) =>
            length != MatchScale.Opener &&
            story is not (MatchStory.Showcase or MatchStory.DavidAndGoliath);

        // ── The shape ────────────────────────────────────────────────────────

        /// <summary>
        /// The match, stage by stage, with nothing chosen but the running order.
        /// </summary>
        /// <param name="brief">What the booker asked for.</param>
        /// <param name="sideCount">How many sides are in it. Two, for now.</param>
        public static IReadOnlyList<PhaseSlot> Shape(MatchBrief brief, int sideCount = 2)
        {
            var slots = new List<PhaseSlot>();

            int protagonist = brief.Protagonist(sideCount);
            int antagonist  = protagonist == 0 ? 1 : 0;

            var p = Control(protagonist);
            var q = Control(antagonist);

            bool valiant = protagonist != brief.WinningSide;

            // ── Opening ──────────────────────────────────────────────────────
            slots.Add(new PhaseSlot(
                MatchPhase.Opening, BeatControl.Even,
                null,
                OpeningDuration(brief.Story, brief.Length),
                OpeningWhy(brief.Story)));

            // ── Shine ────────────────────────────────────────────────────────
            if (HasShine(brief.Story, brief.Length))
            {
                slots.Add(new PhaseSlot(
                    MatchPhase.Shine, p, BeatIntensity.Medium,
                    brief.Length == MatchScale.Epic ? BeatDuration.Medium : BeatDuration.Short,
                    valiant
                        ? "They look good early, because you booked them to come out of this bigger."
                        : "The protagonist looks good before the match turns."));
            }

            // ── Cycles ───────────────────────────────────────────────────────
            int cycles = Cycles(brief.Length);
            int hopes  = 0;

            for (int cycle = 0; cycle < cycles; cycle++)
            {
                bool last = cycle == cycles - 1;

                // An even contest is the one shape where control genuinely changes hands,
                // so the second cycle is worked the other way round. Everywhere else the
                // antagonist keeps the heat, which is what makes those shapes what they are.
                bool swap = brief.Story == MatchStory.EvenContest && cycle % 2 == 1;
                var heater  = swap ? p : q;
                var reliever = swap ? q : p;

                slots.Add(new PhaseSlot(
                    MatchPhase.Cutoff, heater, BeatIntensity.Medium,
                    CutoffDuration(brief.Story, brief.Length),
                    CutoffWhy(brief.Story, cycle)));

                slots.Add(new PhaseSlot(
                    MatchPhase.Heat, heater,
                    cycle == 0 ? BeatIntensity.High : BeatIntensity.Medium,
                    HeatDuration(brief.Story, brief.Length, cycle),
                    HeatWhy(brief.Story)));

                var valley = Valley(brief.Length, cycle);
                foreach (var phase in valley)
                {
                    slots.Add(phase switch
                    {
                        MatchPhase.Rest => new PhaseSlot(
                            MatchPhase.Rest, heater, BeatIntensity.Low,
                            RestDuration(brief.Story, brief.Length),
                            "A long match needs a valley, or there is nowhere left to escalate to."),

                        MatchPhase.Escalation => new PhaseSlot(
                            MatchPhase.Escalation, heater, BeatIntensity.Medium, BeatDuration.Brief,
                            "Character work in the middle of a beating."),

                        // Hope spots escalate across the whole match rather than within a
                        // cycle, because doc 18 §3.3's pyramid is about the match and not
                        // about the section: the fourth tease has to be bigger than the
                        // first or the crowd has stopped believing any of them.
                        _ => new PhaseSlot(
                            MatchPhase.HopeSpot, reliever,
                            hopes++ == 0 ? BeatIntensity.Medium : BeatIntensity.High,
                            BeatDuration.Brief,
                            "A tease. The comeback is worth what these cost.")
                    });
                }

                slots.Add(new PhaseSlot(
                    MatchPhase.Comeback, reliever,
                    last ? ComebackPeak(brief.Length) : BeatIntensity.High,
                    BeatDuration.Short,
                    last ? "The payoff." : "The first comeback, which the next cut-off takes back."));
            }

            // ── Outside interference ─────────────────────────────────────────
            //
            // Placed after the last comeback and before the near falls, which is where it
            // does its job: late enough to matter, early enough that the finish is still
            // the last thing that happens.
            if (brief.Outside != OutsideFactor.None)
            {
                slots.Add(new PhaseSlot(
                    MatchPhase.Outside, Control(brief.WinningSide),
                    BeatIntensity.High, BeatDuration.Brief,
                    OutsideWhy(brief.Outside)));
            }

            // ── Finishing stretch ────────────────────────────────────────────
            int nearFalls = NearFalls(brief.Length);
            for (int i = 0; i < nearFalls; i++)
            {
                slots.Add(new PhaseSlot(
                    MatchPhase.Stretch, Control(brief.WinningSide),
                    i == 0 ? BeatIntensity.High : BeatIntensity.Extreme,
                    BeatDuration.Brief,
                    i == 0 ? "The stretch begins." : "Escalating, because the finish has to beat this."));
            }

            // ── Finish ───────────────────────────────────────────────────────
            slots.Add(new PhaseSlot(
                MatchPhase.Finish, Control(brief.WinningSide),
                FinishIntensity(brief.Finish), BeatDuration.Brief,
                FinishWhy(brief.Finish)));

            return slots;
        }

        // ── The small decisions, each with its reason ────────────────────────

        private static BeatControl Control(int side) =>
            side == 0 ? BeatControl.WrestlerA
          : side == 1 ? BeatControl.WrestlerB
          : BeatControl.SideC;

        /// <summary>
        /// A short match cannot afford the time a slow opening takes, so it is cut to the
        /// bone. Everywhere else the opening beat's own default is right, and saying so
        /// again here is only a second place to be wrong.
        /// </summary>
        private static BeatDuration? OpeningDuration(MatchStory story, MatchScale length)
        {
            if (story is MatchStory.Grudge or MatchStory.Spectacle or MatchStory.TechnicalExhibition)
                return null;

            return length is MatchScale.BigMatch or MatchScale.Epic
                ? null
                : BeatDuration.Brief;
        }

        /// <summary>
        /// The turn is a moment, and a technical match makes it less of one still — the
        /// point there is what happens on the mat afterwards, not the transition into it.
        /// </summary>
        private static BeatDuration CutoffDuration(MatchStory story, MatchScale length) =>
            story == MatchStory.TechnicalExhibition || length == MatchScale.Television
                ? BeatDuration.Brief
                : BeatDuration.Short;

        /// <summary>
        /// How long the control section runs.
        ///
        /// A technical match holds it longer at every length. Limb work is the content
        /// rather than the wait for the content, which is doc 18 §4's distinction between a
        /// heat section that is a rest for the audience and one that is the match.
        /// </summary>
        private static BeatDuration HeatDuration(MatchStory story, MatchScale length, int cycle)
        {
            if (story == MatchStory.TechnicalExhibition) return BeatDuration.Medium;

            return length switch
            {
                MatchScale.Opener     => BeatDuration.Short,
                MatchScale.Television => BeatDuration.Short,
                MatchScale.Workhorse  => BeatDuration.Short,
                MatchScale.BigMatch   => cycle == 0 ? BeatDuration.Medium : BeatDuration.Short,
                MatchScale.Epic       => BeatDuration.Medium,
                _                     => BeatDuration.Short
            };
        }

        /// <summary>
        /// The valley. Longest in a technical match, where the rest hold is doing work
        /// rather than buying time.
        /// </summary>
        private static BeatDuration RestDuration(MatchStory story, MatchScale length) =>
            story == MatchStory.TechnicalExhibition ? BeatDuration.Medium
          : length == MatchScale.Epic               ? BeatDuration.Short
          : BeatDuration.Brief;

        /// <summary>
        /// The last comeback in a big match goes to Extreme, because doc 18 §3.3's pyramid
        /// rule says the biggest thing is the last thing and a short match has already spent
        /// what it has.
        /// </summary>
        private static BeatIntensity ComebackPeak(MatchScale length) =>
            length is MatchScale.BigMatch or MatchScale.Epic
                ? BeatIntensity.Extreme
                : BeatIntensity.High;

        private static BeatIntensity FinishIntensity(FinishKind finish) => finish switch
        {
            FinishKind.Dominant         => BeatIntensity.Extreme,
            FinishKind.Stolen           => BeatIntensity.Medium,
            FinishKind.Disqualification => BeatIntensity.Medium,
            FinishKind.CountOut         => BeatIntensity.Low,
            _                           => BeatIntensity.High
        };

        private static string OpeningWhy(MatchStory story) => story switch
        {
            MatchStory.Grudge              => "They do not wait for the bell to mean anything.",
            MatchStory.Spectacle           => "Straight into it. Nothing is being established.",
            MatchStory.TechnicalExhibition => "A feeling-out process, because the mat work needs the room.",
            _                              => "Establishing, and nothing more."
        };

        private static string CutoffWhy(MatchStory story, int cycle) => story switch
        {
            MatchStory.DavidAndGoliath => "Cut off early, because he was never going to get going.",
            MatchStory.EvenContest when cycle > 0 => "It changes hands, which is what makes it even.",
            _ when cycle > 0 => "Taken back. The first comeback was not the last word.",
            _ => "The turn."
        };

        private static string HeatWhy(MatchStory story) => story switch
        {
            MatchStory.Grudge              => "A beating rather than a control section.",
            MatchStory.TechnicalExhibition => "Limb work, and the submission it is building to.",
            MatchStory.DavidAndGoliath     => "The long beating this match is about surviving.",
            MatchStory.Showcase            => "One-sided, on purpose.",
            _                              => "The control section."
        };

        private static string OutsideWhy(OutsideFactor outside) =>
            outside.HasFlag(OutsideFactor.RefereeBump) ? "The referee is down and the rules went with him."
          : outside.HasFlag(OutsideFactor.RunIn)       ? "Somebody who was not booked in this arrives."
          : "The second at ringside finally matters.";

        private static string FinishWhy(FinishKind finish) => finish switch
        {
            FinishKind.Clean            => "Beaten in the middle. Nothing to argue with.",
            FinishKind.Dominant         => "Emphatic, and it is meant to be remembered.",
            FinishKind.Submission       => "They gave up, which is the only finish they chose.",
            FinishKind.Stolen           => "Out of nowhere. They were not beaten, exactly.",
            FinishKind.Interference     => "Somebody else decided it.",
            FinishKind.Disqualification => "Nobody is beaten and the story keeps running.",
            FinishKind.CountOut         => "The cheapest ending there is.",
            _                           => "The end."
        };
    }
}
