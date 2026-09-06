using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Feuds with more than two camps.
    ///
    /// A three-way programme is **one story with three camps**, not three rivalries that
    /// happen to overlap — nobody followed "Rock vs Foley" on its own while the triangle was
    /// running. Faction warfare is the same shape with more bodies, and a betrayal is a camp
    /// splitting rather than a new feud starting.
    ///
    /// The distinction that does the work is *camp* versus *participant*: two members of the
    /// same faction are both in the feud and have no grievance with each other.
    /// </summary>
    public class MultiPartyFeudTests(ITestOutputHelper output)
    {
        private static Wrestler W(string n) => TestRoster.Make(n);

        [Fact]
        public void AThreeWayProgrammeIsOneStory_NotThreeRivalries()
        {
            var (rock, hhh, foley) = (W("Rock"), W("HHH"), W("Foley"));
            var book = new FeudBook();

            var triangle = book.GetOrCreate([[rock], [hhh], [foley]]);
            triangle.SetMinimumIntensity(FeudIntensity.Hot);

            Assert.True(triangle.IsMultiParty);
            Assert.Equal(3, triangle.Camps.Count);
            output.WriteLine($"  {triangle}");

            // One story, not three: the book holds a single feud.
            Assert.Single(book.All);

            // And every pairing inside it finds that one story.
            Assert.Same(triangle, book.Find(rock, hhh));
            Assert.Same(triangle, book.Find(hhh, foley));
            Assert.Same(triangle, book.Find(rock, foley));
        }

        /// <summary>
        /// Sharing a camp is not a grievance. This is the distinction a betrayal depends on:
        /// before it the two share a camp, after it they do not.
        /// </summary>
        [Fact]
        public void TwoPeopleInTheSameCamp_AreInTheFeudAndNotOpposed()
        {
            var (roman, jey, cody) = (W("Roman"), W("Jey"), W("Cody"));
            var faction = new FeudBook().GetOrCreate([[roman, jey], [cody]]);

            Assert.True(faction.Involves(roman));
            Assert.True(faction.Involves(jey));

            Assert.False(faction.Opposes(roman, jey));      // stablemates
            Assert.True(faction.Opposes(roman, cody));
            Assert.True(faction.Opposes(jey, cody));

            Assert.Equal(faction.CampOf(roman), faction.CampOf(jey));
            Assert.NotEqual(faction.CampOf(roman), faction.CampOf(cody));
        }

        [Fact]
        public void SomebodyOutsideTheStory_IsNotInIt()
        {
            var (a, b, c) = (W("A"), W("B"), W("C"));
            var feud = new FeudBook().GetOrCreate([[a], [b]]);

            Assert.Null(feud.CampOf(c));
            Assert.False(feud.Involves(c));
            Assert.False(feud.Opposes(a, c));
        }

        /// <summary>
        /// A direct rivalry beats being incidentally in the same larger story. If two people
        /// have their own feud *and* are both in a faction war, asking about the two of them
        /// should return the one that is actually about them.
        /// </summary>
        [Fact]
        public void ADedicatedRivalry_OutranksTheLargerStoryThatContainsIt()
        {
            var (a, b, c) = (W("A"), W("B"), W("C"));
            var book = new FeudBook();

            var war = book.GetOrCreate([[a], [b], [c]]);
            war.SetMinimumIntensity(FeudIntensity.Nuclear);

            var personal = book.GetOrCreate(a, b);
            personal.SetMinimumIntensity(FeudIntensity.Building);

            output.WriteLine($"  war      {war}");
            output.WriteLine($"  personal {personal}");

            // Even though the triangle is hotter, the pairing has its own story.
            Assert.Same(personal, book.Find(a, b));
            // And the pairings without one still find the triangle.
            Assert.Same(war, book.Find(a, c));
        }

        /// <summary>
        /// What a multi-man match actually needs: every live story among the people in it,
        /// including one between two wrestlers who are both about to lose to a third.
        /// </summary>
        [Fact]
        public void AMatchCanAskForEveryStoryAmongItsParticipants()
        {
            var (a, b, c, outsider) = (W("A"), W("B"), W("C"), W("Outsider"));
            var book = new FeudBook();

            var hot = book.GetOrCreate(a, b);
            hot.SetMinimumIntensity(FeudIntensity.Hot);
            var cold = book.GetOrCreate(a, c);
            cold.SetMinimumIntensity(FeudIntensity.Building);
            var elsewhere = book.GetOrCreate(outsider, W("Nobody"));
            elsewhere.SetMinimumIntensity(FeudIntensity.Hot);

            var among = book.Among([a, b, c]);
            foreach (var f in among) output.WriteLine($"  {f}");

            Assert.Equal(2, among.Count);
            Assert.Same(hot, among[0]);           // hottest first
            Assert.Same(cold, among[1]);
            Assert.DoesNotContain(elsewhere, among);
        }

        /// <summary>
        /// Booking the same triangle in a different order finds the same story.
        ///
        /// This is about the *key*, which sorts the camps before hashing them — not about
        /// the camp order stored on the feud. The first version of this test was named for
        /// the ordering and tested the keying, and a mutation that removed the ordering
        /// entirely passed it. See below for the one that actually pins the order.
        /// </summary>
        [Fact]
        public void TheSameStoryBookedInAnyOrder_IsOneFeud()
        {
            var (a, b, c) = (W("Alpha"), W("Bravo"), W("Charlie"));
            var book = new FeudBook();

            var first  = book.GetOrCreate([[a], [b], [c]]);
            var second = book.GetOrCreate([[c], [a], [b]]);   // booked the other way round

            Assert.Same(first, second);

            // `AllIncludingDormant`, because a freshly created feud starts at None intensity
            // and `All` hides those. The first version of this test asserted on `All` and so
            // failed on correct code — and it kept failing through a whole mutation run,
            // where I read its failure as a mutation being killed.
            Assert.Single(book.AllIncludingDormant);
        }

        /// <summary>
        /// A cold story is still history. `Find` answers "do these two have a past", which a
        /// dormant feud is; `Among` answers "what is going on here", which it is not.
        /// </summary>
        [Fact]
        public void ADormantTriangleIsStillFound_ButIsNotALiveStory()
        {
            var (a, b, c) = (W("A"), W("B"), W("C"));
            var book = new FeudBook();
            var cold = book.GetOrCreate([[a], [b], [c]]);

            Assert.Equal(FeudIntensity.None, cold.Intensity);
            Assert.Same(cold, book.Find(a, c));      // history, found
            Assert.Empty(book.Among([a, b, c]));     // nothing live
        }

        /// <summary>
        /// And the stored camp order is deterministic, which the test above cannot show
        /// because it gets the same object back either way. Two separate books, the same
        /// three people, booked in different orders: the camps have to come out the same,
        /// or `Camps[0]` means something different depending on how it was typed — and a
        /// save round trip would not reproduce the structure it wrote.
        /// </summary>
        [Fact]
        public void TheStoredCampOrderIsTheSame_HoweverItWasBooked()
        {
            var (a, b, c) = (W("Alpha"), W("Bravo"), W("Charlie"));

            static List<string> Order(Feud f) =>
                f.Camps.Select(camp => string.Join("+", camp.Select(w => w.RingName))).ToList();

            var one = new FeudBook().GetOrCreate([[a], [b], [c]]);
            var two = new FeudBook().GetOrCreate([[c], [a], [b]]);
            var three = new FeudBook().GetOrCreate([[b], [c], [a]]);

            output.WriteLine("  " + string.Join(" | ", Order(one)));
            output.WriteLine("  " + string.Join(" | ", Order(two)));
            output.WriteLine("  " + string.Join(" | ", Order(three)));

            Assert.Equal(Order(one), Order(two));
            Assert.Equal(Order(one), Order(three));
        }

        [Fact]
        public void AFeudNeedsAtLeastTwoCamps() =>
            Assert.Throws<ArgumentException>(() => new FeudBook().GetOrCreate([[W("Alone")]]));

        /// <summary>
        /// A three-camp story has to survive a save. The v3 format could only write two
        /// sides, so a triangle written by an older build would come back as a rivalry with
        /// its third camp silently gone.
        /// </summary>
        [Fact]
        public void AThreeCampFeudSurvivesASaveAndLoad()
        {
            var roster = new List<Wrestler> { W("Rock"), W("HHH"), W("Foley") };
            var start  = new DateOnly(2025, 1, 6);
            var career = new Models.World.Career
            {
                Promotion   = new Models.World.Promotion { Name = "Test Wrestling" },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster
            };
            var (a, b, c) = (roster[0], roster[1], roster[2]);

            var triangle = career.FeudBook.GetOrCreate([[a], [b], [c]]);
            triangle.SetMinimumIntensity(FeudIntensity.Hot);
            triangle.AddHeat(40);

            var reloaded = WrestlingSim.Persistence.SaveSerializer.FromJson(
                WrestlingSim.Persistence.SaveSerializer.ToJson(career), roster);
            var back = reloaded.FeudBook.All.Single(f => f.Involves(a));

            output.WriteLine($"  before {triangle}");
            output.WriteLine($"  after  {back}");

            Assert.Equal(3, back.Camps.Count);
            Assert.True(back.IsMultiParty);
            Assert.True(back.Opposes(back.Camps[0][0], back.Camps[2][0]));
            Assert.Equal(triangle.Heat, back.Heat, 3);
            Assert.Equal(triangle.Intensity, back.Intensity);
        }
    }
}
