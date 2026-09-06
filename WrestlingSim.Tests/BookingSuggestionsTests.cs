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

        /// <summary>
        /// **Card position, not popularity** — and the difference between them is momentum.
        ///
        /// The test above is named for this and cannot detect it: with no momentum, card
        /// position and overness give the same order, so it passes just as happily under
        /// `OrderByDescending(Overness)`. It is the one test in this class that the
        /// popularity-only mutation does not kill, for exactly that reason.
        ///
        /// It matters because `CardPosition` comes off `EffectiveOverness`, which is overness
        /// plus a momentum term — so anybody on a streak crosses a band boundary without
        /// moving in an overness sort. A plain row's heading *is* its card position, so an
        /// overness-only order produces non-contiguous headings, and review reproduced
        /// `MAIN EVENT, MIDCARD, UPPER CARD, LOWER CARD, ENHANCEMENT` in a real career.
        ///
        /// The picker then groups these rows to render them, so a ranking that does not
        /// arrive grouped is a *different list* from the one on screen — which is how the
        /// keyboard cursor ended up 23 rows from the highlight, with Enter booking a name
        /// that was not visible. The component no longer indexes the ranked list, so that
        /// specific failure cannot recur; this keeps the headings contiguous, which is the
        /// other half.
        /// </summary>
        [Fact]
        public void MomentumMovesSomebodyBetweenTiers_AndTheOrderFollowsTheTierNotTheNumber()
        {
            // The cold streak has to land in the *middle* of the overness order, not at its
            // head — an out-of-tier name at either end is still contiguous, so the first
            // version of this test passed under the very mutation it was written for. It
            // takes four names to state the property.
            var top   = W("Top", 95);
            var cold  = W("Cold Streak", 92);   // MainEvent on paper, dropped a tier by form
            var mid   = W("Steady", 90);
            var low   = W("Also Steady", 89);

            cold.Momentum = -90;                // 92 − 13.5 = 78.5, an upper-carder

            var ranked = Rank([cold, top, low, mid]);

            foreach (var r in ranked)
                output.WriteLine($"  {r.Wrestler.RingName,-12} overness {r.Wrestler.Overness:F0} " +
                                 $"effective {r.Wrestler.EffectiveOverness:F1} → {r.Wrestler.CardPosition}");

            // The premise: momentum has actually moved somebody across a boundary. Without
            // this the assertion below is vacuous.
            Assert.NotEqual(top.CardPosition, cold.CardPosition);

            // Headings are contiguous — every row of a card position sits together. Under a
            // popularity-only sort the cold streak sorts first on 90 while belonging to a
            // lower tier, and the tiers interleave.
            var positions = ranked.Select(r => r.Wrestler.CardPosition).ToList();
            Assert.Equal(positions.Distinct().Count(), CountRuns(positions));
        }

        private static int CountRuns<T>(IReadOnlyList<T> xs)
        {
            int runs = 0;
            for (int i = 0; i < xs.Count; i++)
                if (i == 0 || !Equals(xs[i], xs[i - 1])) runs++;
            return runs;
        }

        /// <summary>
        /// A standing partner already booked on the *other* side is not a suggestion, it is
        /// a mistake. The guard existed before the sort moved into Core and was dropped in
        /// the move, so the picker offered a team's two members against each other at the
        /// top of the list, labelled "X's partner". Review found it in a real career with a
        /// seeded team.
        /// </summary>
        [Fact]
        public void ATeamMateAlreadyOnTheOtherSide_IsNotSuggestedAsOpposition()
        {
            var booked = W("Booked", 80);
            var mate   = W("Mate", 45);
            var other  = W("Other", 60);

            var ranked = Rank(
                [mate, other],
                against: [booked],                              // their partner is the opposition
                bookedAs: w => w == booked ? "Side A" : null,
                partnerOf: w => w == mate ? booked : null);

            var m = ranked.Single(r => r.Wrestler == mate);
            Assert.NotEqual(Band.Partner, m.Band);
            Assert.DoesNotContain("partner", m.Reason);
        }

        /// <summary>
        /// The staleness threshold, which review measured as uncovered: moving it from 0.75
        /// to 0.999 — so that every pairing with any history reads as worn out — failed
        /// nothing.
        /// </summary>
        [Fact]
        public void APairingWithAFewMatchesLeftInIt_IsNotYetWornOut()
        {
            var rival  = W("Rival", 70);
            var facing = W("Facing", 70);

            var feuds = new FeudBook();
            var feud  = feuds.GetOrCreate(rival, facing);
            feud.SetMinimumIntensity(FeudIntensity.Hot);
            feud.RecordMatch(Today);

            var only = Rank([rival], against: [facing], feuds: feuds).Single();
            output.WriteLine($"  after one meeting: freshness {feud.Familiarity(Today):F3} → {only.Band}");

            Assert.True(feud.Familiarity(Today) > BookingSuggestions.StaleBelow,
                "One prior meeting is not a worn-out pairing.");
            Assert.Equal(Band.Story, only.Band);
        }

        /// <summary>
        /// The band headings are the only thing a user reads about why a name is where it
        /// is, and nothing checked them: review replaced every one with "BANANA" and the
        /// suite stayed green.
        /// </summary>
        [Theory]
        [InlineData(Band.Story,   "There is a story here")]
        [InlineData(Band.Partner, "Suggested")]
        [InlineData(Band.Recent,  "Recently booked")]
        [InlineData(Band.WornOut, "The crowd has seen this")]
        [InlineData(Band.Booked,  "Already in this match")]
        public void EachBandSaysWhyItIsThere(Band band, string expected) =>
            Assert.Equal(expected, BookingSuggestions.Heading(band, W("Anyone")));

        /// <summary>A plain row has no story, so its heading is where it sits on the card.</summary>
        [Fact]
        public void APlainRowIsHeadedByItsCardPosition()
        {
            var w = W("Anyone", 94);
            Assert.Equal(w.CardPosition.Label(), BookingSuggestions.Heading(Band.Plain, w));
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
