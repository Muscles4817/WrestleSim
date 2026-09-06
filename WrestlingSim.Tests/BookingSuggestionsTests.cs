using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Band = WrestlingSim.Engine.BookingSuggestions.SuggestionBand;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Which name a booker is offered first.
    ///
    /// These exist because a UX review pointed out that the selection rewrite shipped with no
    /// regression test at all — the whole thing lived in a Blazor component the test project
    /// cannot reach — and the honest answer was to move the part that deserved one somewhere
    /// a test could get at it rather than to argue the browser run was enough. A browser run
    /// proves it worked once; it does not stop it breaking.
    ///
    /// Every rule here replaced `OrderByDescending(Overness)`, which is what every roster
    /// list in the app used to be: after picking side A, with the game knowing exactly who A
    /// has a live feud with and who the crowd has watched A fight four times this month, side
    /// B was offered in order of how popular people are.
    /// </summary>
    public class BookingSuggestionsTests(ITestOutputHelper output)
    {
        private static readonly DateOnly Today = new(2026, 3, 1);

        private static Wrestler W(string name, double overness = 70) =>
            TestRoster.Make(name, overness: overness);

        private IReadOnlyList<BookingSuggestions.Suggestion> Rank(
            IEnumerable<Wrestler> pool,
            IReadOnlyList<Wrestler>? against = null,
            FeudBook? feuds = null,
            Func<Wrestler, string?>? bookedAs = null,
            Func<Wrestler, Wrestler?>? partnerOf = null,
            IReadOnlySet<Wrestler>? recent = null)
        {
            var result = BookingSuggestions.Rank(
                pool, against ?? [], feuds ?? new FeudBook(),
                bookedAs ?? (_ => null), partnerOf ?? (_ => null),
                recent ?? new HashSet<Wrestler>(), Today);

            foreach (var r in result)
                output.WriteLine($"  {r.Band,-8} {r.Wrestler.RingName,-14} {r.Reason}");
            return result;
        }

        [Fact]
        public void WithNothingToGoOn_ItFallsBackToCardPosition()
        {
            var star = W("Star", 94);
            var mid  = W("Mid", 62);
            var jobber = W("Jobber", 24);

            var ranked = Rank([jobber, mid, star]);

            Assert.All(ranked, r => Assert.Equal(Band.Plain, r.Band));
            Assert.Equal(new[] { "Star", "Mid", "Jobber" },
                         ranked.Select(r => r.Wrestler.RingName));
        }

        [Fact]
        public void ALiveFeudWithTheOtherSide_ComesFirst_EvenOverAMuchBiggerName()
        {
            var star   = W("Star", 96);
            var rival  = W("Rival", 55);
            var facing = W("Facing", 80);

            var feuds = new FeudBook();
            var feud  = feuds.GetOrCreate(rival, facing);
            feud.SetMinimumIntensity(FeudIntensity.Hot);

            var ranked = Rank([star, rival], against: [facing], feuds: feuds);

            Assert.Equal(Band.Story, ranked[0].Band);
            Assert.Equal("Rival", ranked[0].Wrestler.RingName);
            Assert.Contains("hot feud with Facing", ranked[0].Reason);
        }

        /// <summary>
        /// And a pairing the crowd is sick of sinks — *whether or not a story is attached*.
        ///
        /// The first version nested this inside a live-feud check, so a worn-out pairing whose
        /// feud had gone cold got no warning at all. That is newly reachable since A3, which
        /// decays a neglected feud to `Intensity.None` while its match count survives — so
        /// the pairing that most needs the warning was exactly the one that stopped getting it.
        /// </summary>
        [Theory]
        [InlineData(FeudIntensity.Hot)]
        [InlineData(FeudIntensity.None)]
        public void APairingTheCrowdHasSeenTooOften_Sinks_StoryOrNot(FeudIntensity intensity)
        {
            var tired  = W("Tired", 90);
            var fresh  = W("Fresh", 50);
            var facing = W("Facing", 80);

            var feuds = new FeudBook();
            var feud  = feuds.GetOrCreate(tired, facing);
            if (intensity > FeudIntensity.None) feud.SetMinimumIntensity(intensity);
            for (int i = 0; i < 6; i++) feud.RecordMatch(Today);

            var ranked = Rank([tired, fresh], against: [facing], feuds: feuds);

            Assert.Equal(Band.WornOut, ranked.Single(r => r.Wrestler == tired).Band);
            Assert.Equal("Fresh", ranked[0].Wrestler.RingName);
            Assert.Contains("of what the first one drew", ranked[^1].Reason);
        }

        /// <summary>
        /// A standing partner is suggested as soon as their partner is booked — which is the
        /// common case, and the one the first version could not reach. It gated this on the
        /// *opposing* side being non-empty, and for a side-A slot that is side B, which is
        /// empty for the whole of the normal fill order. So the tier never fired where it is
        /// most useful: picking A's partner with A already chosen.
        /// </summary>
        [Fact]
        public void AStandingPartner_IsSuggestedAsSoonAsTheirPartnerIsBooked()
        {
            var booked  = W("Booked", 80);
            var mate    = W("Mate", 45);
            var nobody  = W("Nobody", 60);

            var ranked = Rank(
                [nobody, mate],
                against: [],                                   // side B still empty
                bookedAs: w => w == booked ? "Side A" : null,
                partnerOf: w => w == mate ? booked : null);

            Assert.Equal(Band.Partner, ranked[0].Band);
            Assert.Equal("Mate", ranked[0].Wrestler.RingName);
            Assert.Contains("Booked's partner", ranked[0].Reason);
        }

        /// <summary>
        /// Names already in the match stay visible and sort **last**.
        ///
        /// They were ranked first at one point, on the reasoning that a mis-pick should be
        /// easy to see. What that does is make the top row of every picker somebody already
        /// booked, so tapping the obvious thing swaps two slots instead of filling an empty
        /// one — a browser run measured six picks leaving one slot filled.
        /// </summary>
        [Fact]
        public void NamesAlreadyInTheMatch_StayVisibleAndSortLast()
        {
            var already = W("Already", 99);
            var free    = W("Free", 30);

            var ranked = Rank([already, free], bookedAs: w => w == already ? "Corner A" : null);

            Assert.Equal("Free", ranked[0].Wrestler.RingName);
            Assert.Equal(Band.Booked, ranked[^1].Band);
            Assert.Equal("Corner A", ranked[^1].BookedAs);
            Assert.Contains("tap to swap", ranked[^1].Reason);
        }

        [Fact]
        public void SomebodyOnTheLastCard_IsOfferedAheadOfSomebodyWithNothingToSay()
        {
            var recent = W("Recent", 40);
            var plain  = W("Plain", 85);

            var ranked = Rank([plain, recent], recent: new HashSet<Wrestler> { recent });

            Assert.Equal(Band.Recent, ranked[0].Band);
            Assert.Equal("Recent", ranked[0].Wrestler.RingName);
        }

        /// <summary>
        /// The whole point, in one assertion: every band beats popularity. If this passes with
        /// the ranking replaced by `OrderByDescending(Overness)` it is testing nothing, and
        /// that is what the app did before.
        /// </summary>
        [Fact]
        public void EveryBandBeatsPopularity()
        {
            var facing  = W("Facing", 80);
            var booked  = W("Booked", 80);

            var story   = W("Story", 20);
            var partner = W("Partner", 21);
            var recent  = W("Recent", 22);
            var plain   = W("Plain", 99);
            var worn    = W("Worn", 98);
            var already = W("Already", 97);

            var feuds = new FeudBook();
            feuds.GetOrCreate(story, facing).SetMinimumIntensity(FeudIntensity.Hot);
            var stale = feuds.GetOrCreate(worn, facing);
            stale.SetMinimumIntensity(FeudIntensity.Hot);
            for (int i = 0; i < 6; i++) stale.RecordMatch(Today);

            var ranked = Rank(
                [plain, worn, already, recent, partner, story],
                against: [facing], feuds: feuds,
                bookedAs: w => w == already ? "Corner A" : w == booked ? "Side A" : null,
                partnerOf: w => w == partner ? booked : null,
                recent: new HashSet<Wrestler> { recent });

            Assert.Equal(new[] { "Story", "Partner", "Recent", "Plain", "Worn", "Already" },
                         ranked.Select(r => r.Wrestler.RingName));
        }
    }
}
