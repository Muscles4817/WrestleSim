using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Runs a number drawing.
    ///
    /// **The drawing draws.** It does not read numbers somebody already set — it calls
    /// <see cref="RumblePlan.DrawNumbers"/> on the night, which is what a drawing is. An
    /// order the booker arranged by hand in the match builder is overwritten, deliberately:
    /// once you have put a drum on television, the only way to keep a number is to hand it
    /// over on camera and wear what the crowd thinks of that.
    ///
    /// A sibling of <see cref="SegmentSimulator"/> rather than a subclass or a template.
    /// The two share the shape — say what happened, score the crowd, deposit heat — and
    /// nothing underneath it: a drawing has no actions, no botch, no injury and no
    /// location axis, and the thing being graded is a set of numbers rather than a set of
    /// performances. Bending the segment simulator around that would have made every rule
    /// in it read "unless it is a drawing".
    /// </summary>
    public class RumbleDrawEngine(int seed)
    {
        /// <summary>What the person running it is worth. A drawing is mostly a promo.</summary>
        public static double HostFactor(Wrestler? host) =>
            host is null ? 1.0 : Math.Clamp(0.85 + host.Charisma * 0.06, 0.85, 1.15);

        public RumbleDrawResult Execute(RumbleDraw draw)
        {
            var errors = draw.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    "Invalid drawing:\n" + string.Join("\n", errors.Select(e => "  • " + e)));

            var plan = draw.Rumble!;

            // The draw itself. Rigged names keep the number the booker gave them; everybody
            // else finds out tonight along with the building.
            plan.DrawNumbers(seed, draw.Rigged);

            var profiles = plan.Field.ToDictionary(
                e => e.Wrestler, e => new PerformerProfile(e.Wrestler));

            var commentary = new List<string>();
            var pulls      = new List<RumblePull>();
            int fieldSize  = plan.Field.Count;

            commentary.Add(draw.Host is { } host
                ? $"{host.RingName} is out here with the drum, and the numbers for " +
                  $"{draw.RumbleLabel} are getting pulled tonight."
                : $"The drum is in the ring. Numbers for {draw.RumbleLabel}, drawn right here.");

            foreach (var w in draw.Cast)
            {
                var entrant = plan.Field.First(e => e.Wrestler == w);
                bool rigged = draw.Rigged.Contains(w);

                // Announcing is the point of the exercise and the cost of it: the match reads
                // this back as anticipation, and a number said out loud cannot surprise
                // anybody later.
                entrant.NumberAnnounced = true;

                var alignment = w.Gimmick?.NaturalAlignment ?? Alignment.Face;
                double reaction = RumbleScoring.PullReaction(
                    entrant.Number, fieldSize, alignment, profiles[w].Connection);

                pulls.Add(new RumblePull
                {
                    Wrestler = w,
                    Number   = entrant.Number,
                    Rigged   = rigged,
                    Reaction = reaction
                });

                commentary.Add(Say(w, entrant.Number, fieldSize, alignment, rigged, draw.Host));
            }

            double reactionScore = RumbleScoring.DrawCrowd(pulls.Select(p => p.Reaction).ToList());
            double credibility   = RumbleScoring.DrawCredibility(draw.Rigged.Count, draw.Cast.Count);
            double hostFactor    = HostFactor(draw.Host);

            double crowd = Math.Clamp(reactionScore * credibility * hostFactor, 0, 1);

            if (draw.Rigged.Count > 0)
                commentary.Add(credibility <= 0.55
                    ? "Nobody in this building believes a number in that drum was drawn."
                    : "That did not look like luck, and the crowd is telling them so.");

            // ── Heat ─────────────────────────────────────────────────────────
            // An honest drawing builds the match; a fixed one builds a grievance, and the
            // grievance is with whoever reached into the drum.
            bool fixedIt = draw.Rigged.Count > 0 && draw.Host is not null;

            var heatWith = fixedIt
                ? new[] { draw.Host! }.Concat(draw.Rigged).Distinct().ToList()
                : draw.Cast.ToList();

            double heat = crowd * 2.0 + draw.Rigged.Count * 1.5;

            // ── Momentum ─────────────────────────────────────────────────────
            var momentum = pulls
                .Select(p => new OvernessChange
                {
                    Wrestler      = p.Wrestler,
                    Delta         = 0,
                    MomentumDelta = p.Reaction * 6.0
                })
                .Where(c => Math.Abs(c.MomentumDelta) >= 0.5)
                .ToList();

            foreach (var change in momentum)
                change.Wrestler.Momentum =
                    Math.Clamp(change.Wrestler.Momentum + change.MomentumDelta, -100, 100);

            return new RumbleDrawResult
            {
                RumbleLabel      = draw.RumbleLabel,
                Host             = draw.Host,
                Pulls            = pulls,
                Reaction         = reactionScore,
                Credibility      = credibility,
                HostFactor       = hostFactor,
                FinalScore       = crowd * 100.0,
                HeatGenerated    = heat,
                HeatParticipants = heatWith,
                HistoryTags      = fixedIt
                    ? new List<FeudHistoryTag> { FeudHistoryTag.PersonalInsult }
                    : new List<FeudHistoryTag>(),
                MomentumChanges  = momentum,
                Commentary       = commentary
            };
        }

        /// <summary>
        /// Says one pull out loud. The line is about what the number means to the person who
        /// drew it, which is the same asymmetry <see cref="RumbleScoring.PullReaction"/>
        /// scores — a face at number two is a sentence, a heel at number thirty is a robbery.
        /// </summary>
        private static string Say(Wrestler w, int number, int fieldSize, Alignment alignment,
                                  bool rigged, Wrestler? host)
        {
            string name = w.RingName;
            bool early = number * 2 < fieldSize + 1;
            bool extreme = RumbleScoring.NumberDrama(number, fieldSize) > 0.75;

            if (rigged)
                return host is null
                    ? $"Number {number} for {name}, and nobody watched that one come out of the drum."
                    : $"{host.RingName} hands {name} number {number}. No drum, no draw, no pretence.";

            if (!extreme)
                return $"{name} draws number {number}. Somewhere in the middle of it.";

            return (alignment, early) switch
            {
                (Alignment.Heel, false) =>
                    $"Number {number} for {name} — last in, and they are loving it. " +
                    "Listen to this building.",
                (Alignment.Heel, true) =>
                    $"Number {number} — {name}, and the place has just come apart laughing.",
                (Alignment.Face, true) =>
                    $"Number {number}. {name} has to go the whole way, and they know it.",
                (Alignment.Face, false) =>
                    $"Number {number} for {name}. A comfortable night, and nothing much to say about it.",
                (_, true) =>
                    $"Number {number} for {name} — a long night in front of them.",
                _ =>
                    $"Number {number} for {name}, and they will take that."
            };
        }
    }
}
