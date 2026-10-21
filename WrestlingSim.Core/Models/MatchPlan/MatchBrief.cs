using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// What the booker wants out of a match, before a single beat is chosen.
    ///
    /// **This is the thing the player fills in, and the beat sheet is generated from it.**
    /// The old flow asked for a match type and then a named structure, which meant picking
    /// from a fixed list of beat sheets — so every Face-in-Peril in a career was the same
    /// eleven beats in the same order, and a triple threat was eight beats whoever was in
    /// it. A brief says what the match is *for* and the grammar writes it, differently every
    /// time.
    ///
    /// Sides are held as **indices into <see cref="MatchPlan.Sides"/>** rather than as
    /// wrestlers, which is what lets the same brief describe a singles match, a tag match
    /// and a three-way. The generator resolves them.
    /// </summary>
    public class MatchBrief
    {
        /// <summary>What the match is about. Picks the shape and the beat pool.</summary>
        public MatchStory Story { get; set; } = MatchStory.EvenContest;

        /// <summary>How long, in doc 18 §3.1's terms. Picks the counts.</summary>
        public MatchScale Length { get; set; } = MatchScale.Television;

        /// <summary>How it ends.</summary>
        public FinishKind Finish { get; set; } = FinishKind.Clean;

        /// <summary>Which side goes over, as an index into the plan's sides.</summary>
        public int WinningSide { get; set; }

        /// <summary>
        /// How each side should come out, by side index. Absent means
        /// <see cref="Booking.Even"/>.
        ///
        /// Deliberately separate from <see cref="WinningSide"/>: doc 20's whole feud
        /// economy rests on the difference between losing and being diminished.
        /// </summary>
        public Dictionary<int, Booking> Bookings { get; set; } = new();

        /// <summary>Anybody outside the match who gets involved.</summary>
        public OutsideFactor Outside { get; set; } = OutsideFactor.None;

        /// <summary>
        /// The two sides who work together, by index, or null for a match where nobody does.
        ///
        /// **Doc 18 §2.5 calls this "the format's single best story."** Two working against
        /// the third is what a three-way opens with, and it is also the honest answer to the
        /// third-man problem: nobody is standing on the floor being unaccounted for, because
        /// all three are busy.
        ///
        /// Meaningless in a two-sided match and ignored there, which is why it is nullable
        /// rather than defaulted — an alliance between the only two people in the match is
        /// not a thing that can happen.
        /// </summary>
        public (int First, int Second)? Alliance { get; set; }

        /// <summary>
        /// Whether the alliance falls apart on camera.
        ///
        /// Doc 18 §2.5: "the moment it breaks is the peak." An alliance that never breaks is
        /// a legitimate booking — two of them can simply beat the third — but it is the
        /// quieter one, and the game should let a booker choose which.
        /// </summary>
        public bool AllianceBreaks { get; set; } = true;

        /// <summary>Whoever is on the wrong end of the alliance, or null when there is none.</summary>
        public int? OutnumberedSide(int sideCount)
        {
            if (Alliance is not { } pair) return null;

            // No explicit "fewer than three sides" guard. The search *is* that rule: in a
            // two-sided match every side is one of the two allies, so there is nobody left
            // to be outnumbered and this returns null on its own. A second statement of the
            // same rule above it is one that can drift out of step with this one.
            for (int side = 0; side < sideCount; side++)
                if (side != pair.First && side != pair.Second) return side;

            return null;
        }

        /// <summary>
        /// Bumped by the re-roll button. Part of the seed, so the same brief re-rolled gives
        /// a genuinely different sheet rather than the same one again.
        /// </summary>
        public int Draft { get; set; }

        public Booking BookingOf(int side) =>
            Bookings.TryGetValue(side, out var b) ? b : Booking.Even;

        /// <summary>
        /// The side the story is *about* — who shines, who takes the hope spots, who makes
        /// the comeback.
        ///
        /// Normally the winner, because normally the match is about the person who wins it.
        /// The exception is the valiant loss: a challenger booked <see cref="Booking.Elevated"/>
        /// against a winner who is not is the one the crowd is watching, and the shape
        /// should follow them rather than the result. That is how a losing challenger comes
        /// out bigger than they went in, which doc 20 §4 calls the most useful thing a
        /// booker can do with a title match they do not intend to change.
        /// </summary>
        public int Protagonist(int sideCount)
        {
            if (sideCount <= 1) return 0;

            for (int side = 0; side < sideCount; side++)
            {
                if (side == WinningSide) continue;
                if (BookingOf(side) == Booking.Elevated &&
                    BookingOf(WinningSide) != Booking.Elevated)
                    return side;
            }

            return WinningSide;
        }

        public MatchBrief Clone() => new()
        {
            Story       = Story,
            Length      = Length,
            Finish      = Finish,
            WinningSide = WinningSide,
            Bookings       = new Dictionary<int, Booking>(Bookings),
            Outside        = Outside,
            Alliance       = Alliance,
            AllianceBreaks = AllianceBreaks,
            Draft          = Draft
        };
    }
}
