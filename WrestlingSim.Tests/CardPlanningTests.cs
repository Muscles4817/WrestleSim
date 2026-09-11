using Xunit;
using Xunit.Abstractions;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **A card can hold a match nobody has cast yet.**
    ///
    /// The shape of a night is the decision — five matches and four segments, in an order —
    /// and the details come after. Before this a match had to be built to completion the
    /// moment it was added, so a booker could not discover they wanted five of them until they
    /// had finished the first, and could never see the shape of a show that was not done.
    ///
    /// What makes that safe is one rule: the show refuses to run while anything on it is
    /// unfinished. An unfinished card is a card being worked on, not a card that falls over at
    /// the bell.
    /// </summary>
    public class CardPlanningTests(ITestOutputHelper output)
    {
        private static readonly DateOnly Day = new(2026, 1, 5);

        private static BookedMatch Slot() => new()
        {
            Plan = new MatchPlanModel
            {
                Sides = [new MatchSide { Intended = 1 }, new MatchSide { Intended = 1 }]
            }
        };

        private static Career NewCareer(List<Wrestler> roster) => new()
        {
            Promotion   = new Promotion { Name = "Planning", Tier = PromotionTier.Established },
            StartDate   = Day,
            CurrentDate = Day,
            Roster      = roster
        };

        // ── An intended side ─────────────────────────────────────────────────

        /// <summary>
        /// A side with no intended size is whatever is in it, which is every side the engine,
        /// the tests and the old builder ever made. Defaulting the intent to one instead of to
        /// null would have declared every tag team on every existing card to be half-empty.
        /// </summary>
        [Fact]
        public void ASideWithNoIntentIsWhoeverIsOnIt()
        {
            var tag = MatchSide.Of(TestRoster.Make("A"), TestRoster.Make("B"));

            Assert.Null(tag.Intended);
            Assert.True(tag.IsComplete);
            Assert.Equal(0, tag.Vacancies);
        }

        [Theory]
        [InlineData(1, 0, false, 1)]
        [InlineData(1, 1, true,  0)]
        [InlineData(3, 1, false, 2)]
        [InlineData(3, 3, true,  0)]
        public void AnIntendedSideWaitsForTheBodiesItWasPromised(
            int intended, int cast, bool complete, int vacancies)
        {
            var side = new MatchSide { Intended = intended };
            for (int i = 0; i < cast; i++) side.Members.Add(TestRoster.Make($"W{i}"));

            Assert.Equal(complete, side.IsComplete);
            Assert.Equal(vacancies, side.Vacancies);
        }

        // ── What finishes a match ────────────────────────────────────────────

        /// <summary>
        /// Cast *and* written. A match with two people in it and no beats is a pairing, not a
        /// match, and running one would take the engine a plan with nothing to work.
        /// </summary>
        [Fact]
        public void AMatchIsNotFinishedUntilItIsCastAndWritten()
        {
            var slot = Slot();
            Assert.False(slot.IsComplete);

            slot.Plan.Sides[0].Members.Add(TestRoster.Make("Face"));
            slot.Plan.Sides[1].Members.Add(TestRoster.Make("Heel"));
            Assert.False(slot.IsComplete);

            slot.Plan.Beats.AddRange(WrestlingSim.Engine.BriefDirector
                .Write(new MatchBrief(), slot.Plan.Sides).Beats);

            output.WriteLine($"  cast and written: {slot.Plan.Beats.Count} beats");
            Assert.True(slot.IsComplete);
        }

        /// <summary>
        /// An uncast slot is charged a plausible runtime rather than the two minutes of its
        /// own entrances. Otherwise a booker planning seven matches is told they have spent
        /// fourteen minutes of a ninety-minute show, and the meter is useless during the only
        /// part of the job it exists to help with.
        /// </summary>
        [Fact]
        public void AnUncastSlotStillCostsTimeOnTheCard()
        {
            var slot = Slot();

            output.WriteLine($"  planned slot: {slot.DurationMinutes} min");
            Assert.Equal(BookedMatch.PlannedMinutes, slot.DurationMinutes);
            Assert.True(slot.DurationMinutes > 5, "an empty slot that costs nothing makes the meter lie");
        }

        // ── What stops the show ──────────────────────────────────────────────

        /// <summary>
        /// **The safety net.** Anything unfinished stops the night, which is what lets the
        /// card be half-built in the first place.
        /// </summary>
        [Fact]
        public void AShowWillNotRunWithSomethingUnfinishedOnIt()
        {
            var show = new ScheduledShow { Type = ShowType.Television, Date = Day };
            Assert.False(show.IsRunnable);

            show.Card.Add(Slot());
            Assert.True(show.IsBooked, "it is booked — there is something on it");
            Assert.False(show.IsRunnable, "and it is not runnable, which are different questions");
            Assert.Single(show.Unfinished);

            var match = (BookedMatch)show.Card[0];
            match.Plan.Sides[0].Members.Add(TestRoster.Make("Face"));
            match.Plan.Sides[1].Members.Add(TestRoster.Make("Heel"));
            match.Plan.Beats.AddRange(WrestlingSim.Engine.BriefDirector
                .Write(new MatchBrief(), match.Plan.Sides).Beats);

            Assert.True(show.IsRunnable);
            Assert.Empty(show.Unfinished);
        }

        /// <summary>One finished match does not carry an unfinished one.</summary>
        [Fact]
        public void OneFinishedMatchDoesNotCarryTheRest()
        {
            var show = new ScheduledShow { Type = ShowType.Television, Date = Day };

            var done = Slot();
            done.Plan.Sides[0].Members.Add(TestRoster.Make("Face"));
            done.Plan.Sides[1].Members.Add(TestRoster.Make("Heel"));
            done.Plan.Beats.AddRange(WrestlingSim.Engine.BriefDirector
                .Write(new MatchBrief(), done.Plan.Sides).Beats);

            show.Card.Add(done);
            show.Card.Add(Slot());

            Assert.False(show.IsRunnable);
            Assert.Single(show.Unfinished);
        }

        // ── It survives a reload ─────────────────────────────────────────────

        /// <summary>
        /// **A half-built card survives a save.** A booker plans a month ahead, and the plan is
        /// the thing they came back for.
        ///
        /// The subtle half is that `Bind` returns null both for "this side is empty" and for
        /// "this side names a wrestler the roster no longer has", and the reader drops an item
        /// whose side comes back null. Without separating those two, every planned slot would
        /// be silently deleted on reload as though it named a stranger.
        /// </summary>
        [Fact]
        public void APlannedSlotSurvivesASaveAndReload()
        {
            var roster = new List<Wrestler>
            {
                TestRoster.Make("Alpha One", overness: 80),
                TestRoster.Make("Beta Two",  overness: 60)
            };
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", Day, ShowType.Television);

            show.Card.Add(Slot());

            // And a half-cast one, which is the state a booker leaves a card in most often.
            var half = Slot();
            half.Plan.Sides[0].Members.Add(roster[0]);
            show.Card.Add(half);

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career),
                [TestRoster.Make("Alpha One", overness: 80), TestRoster.Make("Beta Two", overness: 60)]);
            var back = loaded.Shows.Single(s => s.Id == show.Id);

            output.WriteLine($"  {back.Card.Count} items back, {back.Unfinished.Count()} unfinished");

            Assert.Equal(2, back.Card.Count);
            Assert.False(back.IsRunnable);

            var empty = (BookedMatch)back.Card[0];
            Assert.Equal(1, empty.Plan.Sides[0].Intended);
            Assert.Equal(1, empty.Plan.Sides[0].Vacancies);
            Assert.Empty(empty.Plan.Sides[0].Members);

            var partial = (BookedMatch)back.Card[1];
            Assert.Single(partial.Plan.Sides[0].Members);
            Assert.Equal(1, partial.Plan.Sides[1].Vacancies);

            // And the cast half is bound to the loaded roster, not to a copy.
            Assert.Same(loaded.Roster.Single(w => w.Id == roster[0].Id),
                        partial.Plan.Sides[0].Members[0]);
        }

        /// <summary>
        /// A finished match saved before any of this existed comes back with no intent, which
        /// reads as "whoever is in it" — so an old card is complete on load exactly as it was.
        /// </summary>
        [Fact]
        public void AnOlderSaveComesBackFinished()
        {
            var roster = new List<Wrestler>
            {
                TestRoster.Make("Alpha One", overness: 80),
                TestRoster.Make("Beta Two",  overness: 60)
            };
            var career = NewCareer(roster);
            var show = career.Schedule("Weekly", Day, ShowType.Television);

            var plan = new MatchPlanModel
            {
                SideA = MatchSide.Of(roster[0]),
                SideB = MatchSide.Of(roster[1])
            };
            plan.Beats.AddRange(WrestlingSim.Engine.BriefDirector.Write(new MatchBrief(), plan.Sides).Beats);
            show.Card.Add(new BookedMatch { Plan = plan });

            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career),
                [TestRoster.Make("Alpha One", overness: 80), TestRoster.Make("Beta Two", overness: 60)]);
            var back = loaded.Shows.Single(s => s.Id == show.Id);

            Assert.True(back.IsRunnable);
            Assert.Null(((BookedMatch)back.Card[0]).Plan.Sides[0].Intended);
        }
    }
}
