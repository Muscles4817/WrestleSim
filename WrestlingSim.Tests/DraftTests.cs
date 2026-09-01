using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The draft — the best single tool for sustaining a split
    /// (docs/wrestling-reference/22-brand-splits.md §5.1). It has to actually reshuffle,
    /// actually reset stale pairings, and repair less when it is held too often.
    /// </summary>
    public class DraftTests
    {
        private static Career Split(int rosterSize, out Brand red, out Brand blue)
        {
            var start = new DateOnly(2025, 1, 6);
            var career = new Career
            {
                Promotion   = new Promotion { Name = "Draft Wrestling", Tier = PromotionTier.National },
                StartDate   = start,
                CurrentDate = start,
                Roster = Enumerable.Range(0, rosterSize)
                    .Select(i => TestRoster.Make($"Wrestler {i:00}", overness: 90 - i))
                    .ToList()
            };

            red  = new Brand { Name = "Red" };
            blue = new Brand { Name = "Blue" };
            career.BeginSplit([red, blue]);

            return career;
        }

        // ── Ordering ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 0)]
        [InlineData(4, 0)]
        [InlineData(5, 1)]
        public void SnakeOrderTurnsAroundEachRound(int pick, int expected) =>
            Assert.Equal(expected, DraftBoard.SnakeIndex(pick, 2));

        [Fact]
        public void SnakeOrderStopsEitherBrandTakingEveryOtherName()
        {
            var career = Split(20, out _, out _);

            var board = DraftBoard.Create(career, seed: 1);
            board.AutoComplete();

            var first = board.Brands[0];
            var taken = board.Picks.Where(p => p.To.Id == first.Id).Select(p => p.Round).ToList();

            // Straight alternation would give one brand every odd pick and a permanent
            // advantage. Under a snake it takes the first and last of each pair of rounds.
            Assert.Equal(10, taken.Count);
            Assert.Equal(20, board.Picks.Count);
        }

        [Fact]
        public void TheWeakerBrandPicksFirst()
        {
            var career = Split(10, out var red, out var blue);

            for (int i = 0; i < 4; i++) { red.RecordShow(80); blue.RecordShow(40); }

            var board = DraftBoard.Create(career, seed: 1);

            Assert.Equal(blue, board.OnTheClock);
        }

        // ── Running the board ────────────────────────────────────────────────

        [Fact]
        public void AutoCompleteDraftsEveryoneExactlyOnce()
        {
            var career = Split(12, out _, out _);

            var board = DraftBoard.Create(career, seed: 4);
            board.AutoComplete();

            Assert.True(board.Complete);
            Assert.Equal(12, board.Picks.Count);
            Assert.Equal(12, board.Picks.Select(p => p.Wrestler.Id).Distinct().Count());
        }

        [Fact]
        public void PickingByHandTakesThatPersonForTheBrandOnTheClock()
        {
            var career = Split(6, out _, out _);
            var board = DraftBoard.Create(career, seed: 4);

            var brand = board.OnTheClock!;
            var wanted = career.Roster.Last();

            var pick = board.Pick(wanted);

            Assert.NotNull(pick);
            Assert.Same(wanted, pick!.Wrestler);
            Assert.Equal(brand, pick.To);
            Assert.DoesNotContain(wanted, board.Available);
        }

        [Fact]
        public void TwoDraftsWithDifferentSeedsDoNotResolveIdentically()
        {
            // A draft that always resolves the same way is a sorting algorithm.
            var a = Split(20, out _, out _);
            var b = Split(20, out _, out _);

            var boardA = DraftBoard.Create(a, seed: 1);
            var boardB = DraftBoard.Create(b, seed: 99);
            boardA.AutoComplete();
            boardB.AutoComplete();

            var sequenceA = boardA.Picks.Select(p => p.Wrestler.Id).ToList();
            var sequenceB = boardB.Picks.Select(p => p.Wrestler.Id).ToList();

            Assert.NotEqual(sequenceA, sequenceB);
        }

        // ── Applying ─────────────────────────────────────────────────────────

        [Fact]
        public void ApplyingADraftPutsEveryoneOnTheBrandThatPickedThem()
        {
            var career = Split(12, out _, out _);

            var board = DraftBoard.Create(career, seed: 5);
            board.AutoComplete();

            var outcome = Draft.Apply(career, board);

            foreach (var pick in outcome.Picks)
                Assert.Equal(pick.To, career.Brands.BrandOf(pick.Wrestler));

            Assert.Empty(career.Brands.Unassigned(career.Roster));
        }

        [Fact]
        public void ADraftRefreshesEveryPairing()
        {
            var career = Split(6, out _, out _);
            var a = career.Roster[0];
            var b = career.Roster[1];

            var feud = career.FeudBook.GetOrCreate(a, b);
            feud.AddHeat(40);
            feud.MatchCount = 5;

            var board = DraftBoard.Create(career, seed: 6);
            board.AutoComplete();
            var outcome = Draft.Apply(career, board);

            Assert.Equal(0, feud.MatchCount);
            Assert.Equal(1, outcome.PairingsRefreshed);
        }

        [Fact]
        public void ADraftEndsTheFeudsItSeparates()
        {
            var career = Split(4, out var red, out var blue);
            var a = career.Roster[0];
            var b = career.Roster[1];

            var feud = career.FeudBook.GetOrCreate(a, b);
            feud.AddHeat(40);

            // Force the two apart rather than trusting the auto-pick to split them.
            var board = DraftBoard.Create(career, seed: 6);
            var first = board.OnTheClock!;
            board.Pick(a);
            board.Pick(b);
            board.AutoComplete();

            var outcome = Draft.Apply(career, board);

            Assert.NotEqual(career.Brands.BrandOf(a), career.Brands.BrandOf(b));
            Assert.Equal(FeudIntensity.None, feud.Intensity);
            Assert.Equal(0, feud.Heat);
            Assert.Equal(1, outcome.FeudsEnded);
            Assert.Contains(new[] { red, blue }, brand => brand == first);
        }

        [Fact]
        public void AFeudThatSurvivesTheDraft_KeepsItsHeat()
        {
            var career = Split(5, out _, out _);
            var a = career.Roster[0];
            var b = career.Roster[1];

            var feud = career.FeudBook.GetOrCreate(a, b);
            feud.AddHeat(40);

            var board = DraftBoard.Create(career, seed: 6);
            var brand = board.OnTheClock!;

            // Snake order over two brands is A, B, B, A — so the first brand's second pick
            // is the fourth of the round, and both of these land on the same show.
            board.Pick(a);
            board.Pick(career.Roster[2]);
            board.Pick(career.Roster[3]);
            board.Pick(b);
            board.AutoComplete();

            Draft.Apply(career, board);

            Assert.Equal(brand, career.Brands.BrandOf(a));
            Assert.Equal(brand, career.Brands.BrandOf(b));
            Assert.Equal(40, feud.Heat);
        }

        // ── Cadence and repair ───────────────────────────────────────────────

        [Fact]
        public void ADraftRestoresIntegrityTowardTheCeilingButNeverPastIt()
        {
            var career = Split(6, out _, out _);

            career.Brands.PermanentErosion = 18;
            career.Brands.Integrity = 40;
            career.CurrentDate = career.CurrentDate.AddDays(Draft.AnnualDays);

            var board = DraftBoard.Create(career, seed: 8);
            board.AutoComplete();
            var outcome = Draft.Apply(career, board);

            Assert.True(outcome.IntegrityAfter > 40);
            Assert.True(outcome.IntegrityAfter <= career.Brands.Ceiling);
            Assert.Equal(82, career.Brands.Ceiling, 6);
            Assert.Equal(career.CurrentDate, career.Brands.LastDraftOn);
            Assert.False(outcome.WasEarly);
        }

        [Fact]
        public void ADraftHeldTooSoonRepairsMuchLess()
        {
            double onTime = Draft.RepairShare(Draft.AnnualDays);
            double early  = Draft.RepairShare(30);

            Assert.True(onTime > early);
            Assert.Equal(0.65, onTime, 6);
            Assert.InRange(early, 0.20, 0.30);
        }

        [Fact]
        public void ADraftNeverRaisesTheCeiling()
        {
            var career = Split(6, out _, out _);

            career.Brands.PermanentErosion = 30;
            career.Brands.Integrity = 20;
            career.CurrentDate = career.CurrentDate.AddDays(Draft.AnnualDays * 3);

            var board = DraftBoard.Create(career, seed: 9);
            board.AutoComplete();
            Draft.Apply(career, board);

            Assert.Equal(30, career.Brands.PermanentErosion, 6);
            Assert.Equal(70, career.Brands.Ceiling, 6);
        }

        [Fact]
        public void TheDraftIsDueAYearAfterTheSplitBegan()
        {
            var career = Split(4, out _, out _);

            Assert.False(Draft.IsDue(career.Brands, career.CurrentDate.AddDays(200)));
            Assert.True(Draft.IsDue(career.Brands, career.CurrentDate.AddDays(Draft.AnnualDays)));
            Assert.Equal(career.StartDate.AddDays(Draft.AnnualDays), Draft.DueOn(career.Brands));
        }

        [Fact]
        public void MovedCountsOnlyThePeopleWhoActuallyChangedBrand()
        {
            var career = Split(4, out var red, out _);

            foreach (var w in career.Roster) career.Brands.Assign(w, red);

            var board = DraftBoard.Create(career, seed: 10);
            board.AutoComplete();
            var outcome = Draft.Apply(career, board);

            int stayed = outcome.Picks.Count(p => p.To.Id == red.Id);
            Assert.Equal(outcome.Picks.Count - stayed, outcome.Moved);
        }
    }
}
