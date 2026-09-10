using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **What the brief buys that a beat list could not.**
    ///
    /// The old builder could tell you a plan was invalid and nothing else. There was no way
    /// for it to notice that a sheet had drifted from its own intention, because the beats
    /// were the only record of what was wanted and so there was no intention to drift from.
    /// With a brief attached the game can say "you booked this wrestler protected and gave
    /// them nothing", which is a sentence about the booking rather than about the data.
    /// </summary>
    public class BriefCritiqueTests(ITestOutputHelper output)
    {
        private static Wrestler W(string name, WrestlingStyle style = WrestlingStyle.Technical,
                                  int psych = 75)
        {
            var w = TestRoster.Make(name, psychology: psych);
            w.Style = style;
            w.Mental!.Psychology = psych;
            w.Mental!.RingIQ = psych;
            return w;
        }

        private static List<MatchSide> Sides(WrestlingStyle a = WrestlingStyle.Technical,
                                             WrestlingStyle b = WrestlingStyle.Powerhouse,
                                             int psych = 75) =>
        [
            new MatchSide { Members = [W("Alpha", a, psych)] },
            new MatchSide { Members = [W("Bravo", b, psych)] }
        ];

        private static IReadOnlyList<BookingNote> Critique(
            MatchBrief brief, IReadOnlyList<MatchSide> sides,
            IReadOnlyList<MatchBeat>? beats = null, Expectation? promised = null, int? minutesLeft = null) =>
            BriefCritique.Of(
                brief,
                beats ?? BriefDirector.Write(brief, sides).Beats,
                sides,
                promised ?? Expectation.Nothing,
                minutesLeft);

        private void Show(IReadOnlyList<BookingNote> notes)
        {
            if (notes.Count == 0) output.WriteLine("  (nothing to say)");
            foreach (var note in notes) output.WriteLine($"  [{note.Weight}] {note.Text}");
        }

        /// <summary>A booking that does what it says gets no lecture.</summary>
        [Fact]
        public void AGoodBookingIsLeftAlone()
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse };
            var notes = Critique(brief, Sides());

            Show(notes);
            Assert.Empty(notes);
        }

        // ── Time, the one hard constraint ────────────────────────────────────

        /// <summary>
        /// The loudest note there is, and the only one that quotes a number and names the
        /// fix. Loud rather than refusing: the card charges for overrunning and has offered
        /// that trade since long before the brief existed.
        /// </summary>
        [Fact]
        public void AMatchTooLongForTheSlotIsTheLoudestNote()
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Epic };
            var notes = Critique(brief, Sides(), minutesLeft: 12);

            Show(notes);

            var note = Assert.Single(notes, n => n.Weight == NoteWeight.Costly);
            Assert.Contains("12 left", note.Text);
            Assert.Contains("big match", note.Text);
        }

        /// <summary>And it says nothing when it fits, or when there is no card to fit into.</summary>
        [Fact]
        public void NothingIsSaidAboutTimeWhenThereIsEnoughOfIt()
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Television };

            Assert.DoesNotContain(Critique(brief, Sides(), minutesLeft: 40),
                                  n => n.Weight == NoteWeight.Costly);

            // Exactly the entrances of headroom, which is the case the first version got
            // wrong: it summed the beats and left the two minutes off, so a match that
            // overran the card by precisely its own entrances was reported as fitting.
            var written = BriefDirector.Write(brief, Sides());
            int beatsOnly = written.Beats.Sum(b => b.DurationMinutes);

            Assert.Contains(Critique(brief, Sides(), minutesLeft: beatsOnly),
                            n => n.Weight == NoteWeight.Costly);
            Assert.DoesNotContain(Critique(brief, Sides(), minutesLeft: beatsOnly + 2),
                                  n => n.Weight == NoteWeight.Costly);
            Assert.DoesNotContain(Critique(brief, Sides(), minutesLeft: null),
                                  n => n.Weight == NoteWeight.Costly);
        }

        /// <summary>An opener that will not fit has nothing shorter to offer, and says so.</summary>
        [Fact]
        public void AnOpenerThatWillNotFitHasNowhereToGo()
        {
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Opener };
            var notes = Critique(brief, Sides(), minutesLeft: 2);

            Show(notes);
            Assert.Contains(notes, n => n.Text.Contains("nothing shorter"));
        }

        // ── Drift, which only hand-editing can cause ─────────────────────────

        /// <summary>
        /// **A sheet edited away from its own brief.**
        ///
        /// The generator delivers the story it was asked for, so this is unreachable until
        /// somebody hand-edits — which is exactly why it needs saying. The engine still
        /// grades the match against the brief, so silence here would be the game keeping a
        /// secret about how it is marking you.
        /// </summary>
        [Fact]
        public void ASheetEditedAwayFromItsBriefIsCalledOut()
        {
            var brief = new MatchBrief { Story = MatchStory.TechnicalExhibition, Length = MatchScale.Workhorse };
            var sides = Sides();

            // Booked as a mat classic, then hand-edited into a brawl.
            var brawl = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.Workhorse }, sides).Beats;

            var notes = Critique(brief, sides, brawl);
            Show(notes);

            Assert.Contains(notes, n => n.Weight == NoteWeight.Warning
                                     && n.Text.Contains("technical exhibition")
                                     && n.Text.Contains("graded against"));
        }

        /// <summary>The finish is named outright in the brief, so changing it is not a matter of degree.</summary>
        [Fact]
        public void ChangingTheFinishAwayFromTheBriefIsNoted()
        {
            var brief = new MatchBrief { Finish = FinishKind.Submission, Length = MatchScale.Workhorse };
            var sides = Sides();

            var beats = BriefDirector.Write(brief, sides).Beats.ToList();
            beats[^1] = BeatLibrary.Find("Clean Victory")!.ToMatchBeat(BeatControl.WrestlerA);

            var notes = Critique(brief, sides, beats);
            Show(notes);

            Assert.Contains(notes, n => n.Text.Contains("by submission"));
        }

        // ── The promise ──────────────────────────────────────────────────────

        /// <summary>
        /// **Defying the room is a play, and the note treats it as one.**
        ///
        /// Doc 18 §3.2: great workers adjust in real time, which is the skill of giving a
        /// crowd something it did not ask for. The same booking gets a note for a pair who
        /// can do it and a warning for a pair who cannot, which is the whole difference
        /// between advice and a rule.
        /// </summary>
        [Fact]
        public void DefyingTheRoomIsANoteForMastersAndAWarningForJourneymen()
        {
            var brief = new MatchBrief { Story = MatchStory.TechnicalExhibition, Length = MatchScale.Workhorse };
            var wantsAFight = new Expectation(MatchStory.Grudge, 0.9, "They want a fight.");

            var byMasters = Critique(brief, Sides(psych: 95), promised: wantsAFight);
            var byJourneymen = Critique(brief, Sides(psych: 55), promised: wantsAFight);

            output.WriteLine("  masters:"); Show(byMasters);
            output.WriteLine("  journeymen:"); Show(byJourneymen);

            Assert.Contains(byMasters,    n => n.Weight == NoteWeight.Note    && n.Text.Contains("make that work"));
            Assert.Contains(byJourneymen, n => n.Weight == NoteWeight.Warning && n.Text.Contains("cannot talk a crowd round"));
        }

        /// <summary>Serving the room says nothing, because there is nothing to warn about.</summary>
        [Fact]
        public void ServingTheRoomIsNotWorthMentioning()
        {
            var brief = new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.Workhorse };
            var wantsAFight = new Expectation(MatchStory.Grudge, 0.9, "They want a fight.");

            var notes = Critique(brief, Sides(), promised: wantsAFight);
            Show(notes);

            Assert.DoesNotContain(notes, n => n.Text.Contains("instead"));
        }

        /// <summary>A vague promise is not something to be held to, so it is not mentioned.</summary>
        [Fact]
        public void AVaguePromiseIsNotMentioned()
        {
            var brief = new MatchBrief { Story = MatchStory.TechnicalExhibition, Length = MatchScale.Workhorse };
            var faint = new Expectation(MatchStory.Grudge, MatchExpectation.VagueBelow - 0.01, "Faint.");

            Assert.DoesNotContain(Critique(brief, Sides(), promised: faint), n => n.Text.Contains("instead"));
        }

        // ── Protection, the commonest quiet failure ──────────────────────────

        /// <summary>
        /// **Booked to look strong and given nothing.**
        ///
        /// Doc 20 §4 has a challenger coming out of a loss bigger than they went in, and that
        /// only works if they are given something the crowd can believe in. Elevating
        /// somebody in the brief and then booking them a squash is a plan that contradicts
        /// itself, and nothing in the game could notice before the brief existed.
        /// </summary>
        [Fact]
        public void ElevatingSomebodyAndGivingThemNothingIsCalledOut()
        {
            var sides = Sides();
            var brief = new MatchBrief
            {
                Story       = MatchStory.Showcase,
                Length      = MatchScale.Opener,
                WinningSide = 0,
                Bookings    = { [1] = Booking.Elevated }
            };

            // A showcase gives the loser nothing, which is what a showcase is for.
            var squash = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.Showcase, Length = MatchScale.Opener, WinningSide = 0 },
                sides).Beats;

            var notes = Critique(brief, sides, squash);
            Show(notes);

            Assert.Contains(notes, n => n.Weight == NoteWeight.Warning
                                     && n.Text.Contains("Bravo")
                                     && n.Text.Contains("elevated"));
        }

        /// <summary>
        /// And a loser who *is* given something gets no such note, which is the whole point
        /// of the valiant loss.
        /// </summary>
        [Fact]
        public void AValiantLoserWhoIsGivenSomethingIsFine()
        {
            var sides = Sides();
            var brief = new MatchBrief
            {
                Story       = MatchStory.FaceInPeril,
                Length      = MatchScale.BigMatch,
                WinningSide = 0,
                Bookings    = { [1] = Booking.Elevated }
            };

            var notes = Critique(brief, sides);
            Show(notes);

            Assert.DoesNotContain(notes, n => n.Text.Contains("never in it"));
        }

        /// <summary>The winner is not checked for this. Winning is the thing they were given.</summary>
        [Fact]
        public void TheWinnerIsNotAskedToProveTheyWereInIt()
        {
            var sides = Sides();
            var brief = new MatchBrief
            {
                Story       = MatchStory.Showcase,
                Length      = MatchScale.Opener,
                WinningSide = 0,
                Bookings    = { [0] = Booking.Elevated }
            };

            var notes = Critique(brief, sides);
            Show(notes);

            Assert.DoesNotContain(notes, n => n.Text.Contains("never in it"));
        }

        // ── The unearned comeback ────────────────────────────────────────────

        /// <summary>
        /// Doc 18 §2.3 has the hope spot as what the comeback is borrowing against. A long
        /// heat section with none in it produces a crowd that stopped waiting rather than one
        /// that erupts.
        /// </summary>
        [Fact]
        public void AComebackNobodyPaidForIsNoted()
        {
            var sides = Sides();
            var brief = new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse };

            var stripped = BriefDirector.Write(brief, sides).Beats
                .Where(b => b.Type != BeatType.HopeSpot)
                .ToList();

            // Pad it back out so the note's length gate is met without hope spots.
            while (stripped.Count <= 9)
                stripped.Insert(4, BeatLibrary.Find("Methodical Grind")!.ToMatchBeat(BeatControl.WrestlerB));

            var notes = Critique(brief, sides, stripped);
            Show(notes);

            Assert.Contains(notes, n => n.Text.Contains("borrowed against"));
        }

        // ── Ordering ─────────────────────────────────────────────────────────

        /// <summary>
        /// Worst first. A booker reading a list on a phone reads the top of it, so the thing
        /// that stops the match running cannot be underneath a note about hope spots.
        /// </summary>
        [Fact]
        public void TheWorstThingIsSaidFirst()
        {
            var sides = Sides(psych: 50);
            var brief = new MatchBrief
            {
                Story       = MatchStory.TechnicalExhibition,
                Length      = MatchScale.Epic,
                WinningSide = 0,
                Bookings    = { [1] = Booking.Elevated }
            };

            var squash = BriefDirector.Write(
                new MatchBrief { Story = MatchStory.Showcase, Length = MatchScale.Opener }, sides).Beats;

            var notes = Critique(brief, sides, squash,
                                 new Expectation(MatchStory.Grudge, 0.9, "They want a fight."),
                                 minutesLeft: 5);
            Show(notes);

            Assert.True(notes.Count >= 3, "this booking is wrong in several ways at once");
            Assert.Equal(NoteWeight.Costly, notes[0].Weight);

            for (int i = 1; i < notes.Count; i++)
                Assert.True(notes[i].Weight <= notes[i - 1].Weight);
        }

        // ── Presets ──────────────────────────────────────────────────────────

        /// <summary>
        /// Every preset produces a legal, sensible match. These are the fast path, so one of
        /// them being broken is worse than a bad default — it is a broken button.
        /// </summary>
        [Fact]
        public void EveryPresetProducesAMatchWorthBooking()
        {
            foreach (var preset in BriefPresets.All)
            {
                var sides = Sides();
                var written = BriefDirector.Write(preset.Brief, sides);
                var plan = new MatchPlan { Sides = sides, Beats = written.Beats.ToList() };

                output.WriteLine($"  {preset.Name,-20} {written.Beats.Count,2} beats {written.Minutes,3} min");

                Assert.Empty(plan.Validate());
                Assert.True(written.Beats.Count >= 5);
            }
        }

        /// <summary>
        /// A preset stays lit while it still describes the booking, and lets go the moment it
        /// does not. A chip that stays selected after you have changed what it means is
        /// lying about the state of the form.
        /// </summary>
        [Fact]
        public void APresetLetsGoOnceTheBriefHasMovedAwayFromIt()
        {
            var preset = BriefPresets.Find("Face-in-Peril")!.Value;
            var brief = preset.Brief.Clone();

            Assert.Equal("Face-in-Peril", BriefPresets.Matching(brief));

            brief.Length = MatchScale.Epic;
            Assert.NotEqual("Face-in-Peril", BriefPresets.Matching(brief));
        }

        /// <summary>
        /// The suggestion follows what the match promises, and is offered rather than
        /// applied. A default that picks itself is a decision taken away from the booker.
        /// </summary>
        [Theory]
        [InlineData(MatchStory.Grudge,              "Grudge Brawl")]
        [InlineData(MatchStory.TechnicalExhibition, "Technical Showcase")]
        [InlineData(MatchStory.Spectacle,           "Spotfest")]
        [InlineData(MatchStory.DavidAndGoliath,     "Giant Killer")]
        public void TheSuggestedPresetFollowsThePromise(MatchStory wants, string expected)
        {
            var promised = new Expectation(wants, 0.8, "");
            Assert.Equal(expected, BriefPresets.SuggestedFor(promised));
            Assert.NotNull(BriefPresets.Find(expected));
        }
    }
}
