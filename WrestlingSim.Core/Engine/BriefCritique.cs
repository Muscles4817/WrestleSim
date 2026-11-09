using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>How much a note matters, which decides how loudly the review shows it.</summary>
    public enum NoteWeight
    {
        /// <summary>Worth knowing. The booking is fine.</summary>
        Note,

        /// <summary>The booking is working against itself. Fixable, and worth fixing.</summary>
        Warning,

        /// <summary>
        /// This has a price attached and the price is large. Still not a refusal — a booker
        /// is allowed to run a card long and pay for it, and the show screen has said so
        /// since before any of this existed.
        /// </summary>
        Costly
    }

    /// <summary>One thing worth saying about a booking.</summary>
    public readonly record struct BookingNote(NoteWeight Weight, string Text);

    /// <summary>
    /// What is wrong with a booking, in the booker's own terms.
    ///
    /// **This is what the brief buys that a beat list could not.** The old builder could
    /// tell you a plan was invalid and nothing else, because the beats were the only record
    /// of what was wanted — there was no way to notice that a sheet had drifted from its own
    /// intention, since it had no intention to drift from. With a brief attached the game can
    /// say "you booked this wrestler protected and gave them nothing", which is a sentence
    /// about the booking rather than about the data.
    ///
    /// **Everything here is advice and nothing here refuses.** A booker who knows what they
    /// are doing is allowed to do it, and doc 04 §5 on the shortcut whose cost is invisible
    /// per use applies to the game lecturing you as much as to the booking itself.
    ///
    /// That includes the runtime. An over-long match is the loudest note in here because the
    /// card charges up to 35% of its score for overrunning, but it is a price rather than a
    /// rule — the show screen has offered that trade since long before the brief existed,
    /// and quietly withdrawing it here would be taking away a decision rather than informing
    /// one. The weight is called Costly and not Blocking for exactly that reason.
    /// </summary>
    public static class BriefCritique
    {
        /// <summary>
        /// Everything worth saying, worst first.
        /// </summary>
        /// <param name="brief">What was asked for.</param>
        /// <param name="written">What came out, after any hand-editing.</param>
        /// <param name="sides">Who is in it.</param>
        /// <param name="promised">What the match promised, from <see cref="MatchExpectation"/>.</param>
        /// <param name="minutesLeft">Runtime left in the slot, or null outside a card.</param>
        public static IReadOnlyList<BookingNote> Of(
            MatchBrief brief,
            IReadOnlyList<MatchBeat> written,
            IReadOnlyList<MatchSide> sides,
            Expectation promised,
            int? minutesLeft = null)
        {
            var notes = new List<BookingNote>();

            Time(brief, written, minutesLeft, notes);
            Drift(brief, written, notes);
            Promise(brief, sides, promised, notes);
            Protection(brief, written, sides, notes);
            Earned(written, notes);

            return notes.OrderByDescending(n => n.Weight).ToList();
        }

        /// <summary>
        /// What this booking is trying to do, in one sentence, read straight off the brief.
        ///
        /// The critique only ever said what was wrong. A panel that opens with its own
        /// problems and never states its goal reads as a list of complaints, and a booking
        /// with nothing wrong with it showed nothing at all — so a sound plan and an
        /// unexamined one looked identical.
        /// </summary>
        public static string Intent(MatchBrief brief, IReadOnlyList<MatchSide> sides)
        {
            string winner = sides.Count > brief.WinningSide
                ? string.Join(" & ", sides[brief.WinningSide].Members.Select(w => w.RingName))
                : "the winner";

            return $"{Name(brief.Length).Capitalise()} built as {Name(brief.Story)}, " +
                   $"with {winner} going over {Name(brief.Finish)}.";
        }

        /// <summary>
        /// What the plan has going for it.
        ///
        /// Deliberately not graded and deliberately sparse: these are the things that would
        /// be worth saying to a booker who asked "is this any good?", and padding the list
        /// with everything that is merely not wrong would make the real ones worthless.
        /// Each one names the decision it is about, exactly as the notes do.
        /// </summary>
        public static IReadOnlyList<string> Strengths(
            MatchBrief brief,
            IReadOnlyList<MatchBeat> written,
            IReadOnlyList<MatchSide> sides,
            Expectation promised)
        {
            var strengths = new List<string>();
            if (written.Count == 0) return strengths;

            // The sheet delivers what the room is there for.
            if (promised.Strength >= MatchExpectation.VagueBelow
                && MatchExpectation.Distance(promised.Wants, brief.Story) <= 0.35)
                strengths.Add($"This is what they came for — {Name(promised.Wants)}, and that is " +
                              "what the sheet gives them.");

            // The comeback was paid for. The inverse of Earned's complaint.
            bool heat     = written.Any(b => b.Type == BeatType.HeatSegment);
            bool comeback = written.Any(b => b.Type == BeatType.Comeback);
            bool teased   = written.Any(b => b.Type is BeatType.HopeSpot or BeatType.NearTag);
            if (heat && comeback && teased)
                strengths.Add("The comeback is paid for: the heat borrows against it and the " +
                              "hope spots keep the debt visible.");

            // Somebody believed it might end early.
            if (written.Any(b => b.Type == BeatType.NearFall))
                strengths.Add("There is a near fall before the finish, so the crowd is asked " +
                              "at least once whether it is already over.");

            // Two people who can carry a demanding story.
            if (MatchExpectation.CanCarryIt(sides) >= 0.8)
                strengths.Add("Both of them can carry a story at this length without the " +
                              "booking having to do it for them.");

            return strengths;
        }

        /// <summary>
        /// The parts a story casts people in, where it casts them in any.
        ///
        /// Face-in-peril is the clear case and the one a review caught: the sheet gives the
        /// winning side the shine and the comeback and the other side both heat segments,
        /// which is the game casting one as the sympathetic face and the other as the heel
        /// without ever saying so. David-and-Goliath does the same with size instead of
        /// alignment. An even contest casts nobody, and saying it did would be inventing a
        /// story the booking does not have.
        ///
        /// Returned as a side index and the part, so the caller can name the wrestler.
        /// </summary>
        public static IReadOnlyList<(int Side, Alignment Part)> Roles(MatchBrief brief, int sideCount)
        {
            if (sideCount != 2) return [];

            int winner = brief.WinningSide;
            int other  = winner == 0 ? 1 : 0;

            return brief.Story switch
            {
                MatchStory.FaceInPeril     => [(winner, Alignment.Face), (other, Alignment.Heel)],
                MatchStory.DavidAndGoliath => [(winner, Alignment.Face), (other, Alignment.Heel)],
                MatchStory.Grudge          => [],
                _                          => []
            };
        }

        /// <summary>
        /// Where the part a story casts somebody in is not the part they play.
        ///
        /// A warning and not a refusal, like everything else here: booking against alignment
        /// is a real call and sometimes the point of the match. What it cannot be is silent,
        /// which is what it was — the sheet simply handed the heat to whoever was in corner B
        /// and said nothing about having cast them.
        /// </summary>
        public static IReadOnlyList<BookingNote> Casting(
            MatchBrief brief, IReadOnlyList<MatchSide> sides)
        {
            var notes = new List<BookingNote>();

            foreach (var (side, part) in Roles(brief, sides.Count))
            {
                if (side >= sides.Count) continue;

                var starter = sides[side].Members.FirstOrDefault();
                if (starter?.Gimmick?.NaturalAlignment is not { } actual) continue;
                if (actual == part || actual == Alignment.Tweener) continue;

                notes.Add(new BookingNote(NoteWeight.Note,
                    $"{starter.RingName} is working {part.ToString().ToLowerInvariant()} in this one — " +
                    $"{Name(brief.Story)} needs somebody to {(part == Alignment.Face ? "take the beating" : "give it")} — " +
                    $"and they are booked {actual.ToString().ToLowerInvariant()}. Fine to do; worth doing on purpose."));
            }

            return notes;
        }

        /// <summary>First letter up, for a sentence that starts with one of the Name() phrases.</summary>
        private static string Capitalise(this string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

        /// <summary>
        /// The loudest note. A card has a runtime budget and going past it is charged for,
        /// so this is the one place the critique quotes a number and names the fix.
        /// </summary>
        private static void Time(
            MatchBrief brief, IReadOnlyList<MatchBeat> written, int? minutesLeft, List<BookingNote> notes)
        {
            if (minutesLeft is not { } left) return;

            // What the *card* will be charged, not what the beats add up to. Those differ
            // by the entrances, which is enough to tell a booker a match fits and then
            // overrun the show by exactly that much.
            int minutes = Models.BookedMatch.RuntimeOf(written);
            if (minutes <= left) return;

            notes.Add(new BookingNote(NoteWeight.Costly,
                $"This runs {minutes} minutes and there are {left} left in the slot. " +
                (brief.Length == MatchScale.Opener
                    ? "There is nothing shorter to drop to."
                    : $"Booking it as {ShorterThan(brief.Length)} would fit.")));
        }

        private static string ShorterThan(MatchScale scale) => scale switch
        {
            MatchScale.Epic       => "a big match",
            MatchScale.BigMatch   => "a workhorse match",
            MatchScale.Workhorse  => "a television match",
            MatchScale.Television => "an opener",
            _                     => "shorter"
        };

        /// <summary>
        /// **Has the sheet drifted from what it was booked as?**
        ///
        /// Only reachable by hand-editing, which is exactly why it is worth saying: the
        /// generator delivers the story it was asked for, and then a booker changes six beats
        /// and the match stops being the one the brief describes. The engine still grades it
        /// against the brief, so silence here would be the game keeping a secret.
        /// </summary>
        private static void Drift(MatchBrief brief, IReadOnlyList<MatchBeat> written, List<BookingNote> notes)
        {
            if (written.Count == 0) return;

            var within = BriefDirector.TypesWithin(brief.Story);
            int offStory = written.Count(b => !within.Contains(b.Type));
            double share = offStory / (double)written.Count;

            // A quarter of the sheet. Three beats in eleven is what separates a grudge from
            // a mat classic once they are both written out — the opening, the cut-off and
            // the tease — so the threshold has to sit below that rather than above it.
            if (share > 0.25)
                notes.Add(new BookingNote(NoteWeight.Warning,
                    $"Booked as {Name(brief.Story)}, but {offStory} of {written.Count} beats are not. " +
                    "The match is graded against what you said it was."));

            // The finish is the one beat the brief names outright, so it drifting is not a
            // matter of degree.
            var finish = written.LastOrDefault(b => b.IsFinish);
            if (finish is not null && finish.Type != BriefDirector.FinishType(brief.Finish))
                notes.Add(new BookingNote(NoteWeight.Note,
                    $"You asked for it to end {Name(brief.Finish)} and it now ends another way."));
        }

        /// <summary>
        /// Serving the room, or knowingly not. Doc 18 §3.2 — the second case is a real play
        /// and the note says so rather than treating it as a mistake.
        /// </summary>
        private static void Promise(
            MatchBrief brief, IReadOnlyList<MatchSide> sides, Expectation promised, List<BookingNote> notes)
        {
            if (promised.Strength < MatchExpectation.VagueBelow) return;

            double distance = MatchExpectation.Distance(promised.Wants, brief.Story);
            if (distance <= 0.35) return;

            double canCarryIt = MatchExpectation.CanCarryIt(sides);

            notes.Add(canCarryIt >= 0.8
                ? new BookingNote(NoteWeight.Note,
                    $"{promised.Reading} You are giving them {Name(brief.Story)} instead. " +
                    "These two are good enough to make that work.")
                : new BookingNote(NoteWeight.Warning,
                    $"{promised.Reading} You are giving them {Name(brief.Story)} instead, " +
                    "and this pair cannot talk a crowd round."));
        }

        /// <summary>
        /// **Booked to look strong and given nothing.**
        ///
        /// The commonest way a booking quietly fails to do what it was for. Doc 20 §4 has a
        /// challenger coming out of a loss bigger than they went in, and that only works if
        /// they are *given* something — a comeback, a near fall, the crowd believing for a
        /// moment. Elevating somebody in the brief and then booking them a squash is a plan
        /// that contradicts itself, and nothing before this could notice.
        /// </summary>
        private static void Protection(
            MatchBrief brief, IReadOnlyList<MatchBeat> written,
            IReadOnlyList<MatchSide> sides, List<BookingNote> notes)
        {
            for (int side = 0; side < sides.Count; side++)
            {
                var booking = brief.BookingOf(side);
                if (booking is not (Booking.Elevated or Booking.Protected)) continue;
                if (side == brief.WinningSide) continue;

                var control = side == 0 ? BeatControl.WrestlerA
                            : side == 1 ? BeatControl.WrestlerB
                            : BeatControl.SideC;

                bool comeback = written.Any(b => b.Control == control && b.Type == BeatType.Comeback);
                bool nearFall = written.Any(b => b.Control == control && b.Type == BeatType.NearFall);

                if (comeback || nearFall) continue;

                string who = Name(sides[side]);
                notes.Add(new BookingNote(NoteWeight.Warning,
                    $"{who} is booked {booking.ToString().ToLowerInvariant()} and loses without a " +
                    "comeback or a near fall. Nothing in the match says they were ever in it."));
            }
        }

        /// <summary>
        /// A comeback nobody paid for. Doc 18 §2.3 has the hope spot as the thing the
        /// comeback is borrowing against, and a heat section with no teases in it produces a
        /// crowd that has stopped waiting rather than one that erupts.
        /// </summary>
        private static void Earned(IReadOnlyList<MatchBeat> written, List<BookingNote> notes)
        {
            bool heat     = written.Any(b => b.Type == BeatType.HeatSegment);
            bool comeback = written.Any(b => b.Type == BeatType.Comeback);
            bool teased   = written.Any(b => b.Type is BeatType.HopeSpot or BeatType.NearTag);

            if (heat && comeback && !teased && written.Count > 8)
                notes.Add(new BookingNote(NoteWeight.Note,
                    "A long heat section with no hope spots in it. The comeback arrives " +
                    "without anything having been borrowed against it."));
        }

        // ── Words ────────────────────────────────────────────────────────────

        private static string Name(MatchSide side) =>
            string.Join(" & ", side.Members.Select(w => w.RingName));

        public static string Name(MatchStory story) => story switch
        {
            MatchStory.EvenContest         => "an even contest",
            MatchStory.FaceInPeril         => "a face-in-peril match",
            MatchStory.Grudge              => "a grudge",
            MatchStory.TechnicalExhibition => "a technical exhibition",
            MatchStory.DavidAndGoliath     => "David and Goliath",
            MatchStory.Spectacle           => "a spectacle",
            MatchStory.Showcase            => "a showcase",
            _                              => story.ToString()
        };

        public static string Name(FinishKind finish) => finish switch
        {
            FinishKind.Clean            => "clean",
            FinishKind.Dominant         => "with the finisher",
            FinishKind.Submission       => "by submission",
            FinishKind.Stolen           => "on a roll-up",
            FinishKind.Interference     => "on interference",
            FinishKind.Disqualification => "on a disqualification",
            FinishKind.CountOut         => "on a count-out",
            _                           => finish.ToString()
        };

        public static string Name(MatchScale scale) => scale switch
        {
            MatchScale.Opener     => "an opener",
            MatchScale.Television => "a television match",
            MatchScale.Workhorse  => "a workhorse match",
            MatchScale.BigMatch   => "a big match",
            MatchScale.Epic       => "an epic",
            _                     => scale.ToString()
        };

        /// <summary>
        /// Roughly how long a scale runs, from doc 18 §3.1 — the same figures the enum's own
        /// comments carry. Written down here because the player was being asked to choose a
        /// length whose only statement of what it meant was a comment in the source.
        /// </summary>
        public static string Minutes(MatchScale scale) => scale switch
        {
            MatchScale.Opener     => "5–8 min",
            MatchScale.Television => "8–12 min",
            MatchScale.Workhorse  => "10–15 min",
            MatchScale.BigMatch   => "15–25 min",
            MatchScale.Epic       => "25–40 min",
            _                     => ""
        };

        public static string Name(Booking booking) => booking switch
        {
            Booking.Elevated   => "elevated",
            Booking.Protected  => "protected",
            Booking.Even       => "as they are",
            Booking.Diminished => "taken down a peg",
            _                  => booking.ToString()
        };
    }
}
