using WrestlingSim.Enums;

namespace WrestlingSim.Models.Rumble
{
    /// <summary>
    /// The number drawing, as a thing that happens on a show.
    ///
    /// **Why this is a card item and not a button.** The draw shipped first as
    /// <see cref="RumblePlan.DrawNumbers"/> behind a 🎲 in the builder, which made it
    /// honest and completely invisible: the numbers moved, and no crowd ever saw it
    /// happen. That is the wrong shape for the one part of this format doc 18 §2.5 says
    /// carries it — "it is the entries that make the Rumble a story rather than a
    /// scramble". A story the audience is not told is not a story.
    ///
    /// So the drawing goes where every other story beat in this game goes: on a card, in
    /// front of a building, costing a slot and a chunk of the runtime budget. It is booked
    /// on a show *before* the one holding the match, which is what a go-home show is for.
    ///
    /// **What it buys, and what it costs.**
    ///
    ///   • It buys **anticipation**. A number the crowd has had a week with is worth more
    ///     than one they find out about when the music hits —
    ///     <see cref="Engine.RumbleScoring.Anticipation"/> is that, priced.
    ///   • It costs a **card slot** and its runtime, like anything else worth booking.
    ///   • It costs the **surprise**. Somebody whose number was read out on television
    ///     cannot walk out to a shocked building, so a wrestler booked into a
    ///     <see cref="RumbleMomentKind.SurpriseReturn"/> cannot be in the cast. That is a
    ///     validation error rather than a quiet scoring cut, because it is a booking
    ///     decision and the booker should meet it while they are making it.
    ///
    /// **Rigging.** <see cref="Rigged"/> names the wrestlers whose numbers the host hands
    /// out rather than draws. This is the only way to guarantee a number once a drawing is
    /// booked, and it is deliberately not free: the crowd stops believing the drum
    /// (<see cref="Engine.RumbleScoring.DrawCredibility"/>) and the wrestler it was done to
    /// has a grievance with whoever did it. Rigging off camera is not modelled, because a
    /// fix nobody sees costs nothing and would be the dominant strategy inside a minute —
    /// which is the exact hole the entry-number work was written to close.
    /// </summary>
    public class RumbleDraw : ICardItem
    {
        /// <summary>
        /// Which match this draws for, by <see cref="RumblePlan.Id"/>. Held as an id as
        /// well as a reference because the two live on different shows, and a save file
        /// reads one card at a time — the reference is resolved in a second pass on load.
        /// </summary>
        public string RumbleId { get; set; } = "";

        /// <summary>
        /// The match itself. Null only between deserialisation and the pass that resolves
        /// it; a draw whose match cannot be found is dropped rather than left dangling.
        /// </summary>
        public RumblePlan? Rumble { get; set; }

        /// <summary>What to call the match on the card sheet when it is not to hand.</summary>
        public string RumbleLabel { get; set; } = "the Rumble";

        /// <summary>
        /// Who comes out and pulls a number. Not the whole field — thirty people filing
        /// past a drum is not a segment — and the choice is the booking: these are the
        /// numbers the audience will know going in.
        /// </summary>
        public List<Wrestler> Cast { get; set; } = new();

        /// <summary>
        /// Whoever is running it. Optional, and worth having: a drawing needs somebody to
        /// blame, and a general manager who can talk makes the whole thing land harder.
        /// </summary>
        public Wrestler? Host { get; set; }

        /// <summary>
        /// Whose numbers are handed out rather than drawn. A subset of <see cref="Cast"/>,
        /// because a fix the crowd cannot see is not a fix — see the class summary.
        /// </summary>
        public List<Wrestler> Rigged { get; set; } = new();

        // ── ICardItem ────────────────────────────────────────────────────────

        public string Name => $"The draw — {RumbleLabel}";

        /// <summary>
        /// A segment, and that matters: it takes the segment fatigue penalty when it
        /// follows one, which is right. A drawing after a promo is two people talking.
        /// </summary>
        public CardItemKind Kind => CardItemKind.Segment;

        /// <summary>
        /// A drawing has nothing to fill in. It is made complete by the builder that creates
        /// it, because there is no half-made version of pulling numbers out of a drum.
        /// </summary>
        public bool IsComplete => true;

        public int DurationMinutes => Math.Clamp(3 + Cast.Count, 4, 12);

        public IReadOnlyList<Wrestler> Wrestlers =>
            Host is null || Cast.Contains(Host) ? Cast : Cast.Append(Host).ToList();

        // ── Booking ──────────────────────────────────────────────────────────

        /// <summary>What is wrong with this booking, in words a booker can act on.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (Rumble is not { } plan)
            {
                errors.Add("This drawing is not attached to a match. Book the battle royal " +
                           "first, on a later date, and the drawing can draw for it.");
                return errors;
            }

            if (plan.IsBattleRoyal)
                errors.Add("Everybody starts together in a battle royal, so there are no " +
                           "numbers to draw. This only makes sense for timed entries.");

            if (Cast.Count == 0)
                errors.Add("Nobody comes out to pull a number.");

            if (Cast.Count != Cast.Distinct().Count())
                errors.Add("Somebody is drawing twice.");

            foreach (var w in Cast.Where(w => !plan.Field.Any(e => e.Wrestler == w)))
                errors.Add($"{w.RingName} draws a number for a match they are not in.");

            foreach (var w in Rigged.Where(w => !Cast.Contains(w)))
                errors.Add($"{w.RingName}'s number is fixed but they never come out to " +
                           "collect it. A fix nobody sees is not worth anything.");

            // The whole cost of announcing a number, stated where the booker is making the
            // decision rather than deducted quietly on the night.
            var surprises = plan.Field.Where(e => e.IsSurprise).Select(e => e.Wrestler)
                .Concat(plan.Moments
                    .Where(m => m.Kind == RumbleMomentKind.SurpriseReturn)
                    .SelectMany(m => m.Cast))
                .Distinct();

            foreach (var w in surprises.Where(Cast.Contains))
                errors.Add($"{w.RingName} is booked as a surprise. You cannot announce a " +
                           "surprise — take them out of the drawing or out of the moment.");

            return errors;
        }
    }
}
