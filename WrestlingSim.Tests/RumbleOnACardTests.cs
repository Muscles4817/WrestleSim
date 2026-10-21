using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;
using WrestlingSim.Persistence;
using WrestlingSim.Models.World;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// **A battle royal on a show card.**
    ///
    /// The Rumble shipped reachable from the exhibition menu and nowhere else, which by this
    /// project's own rule is half a feature — and worse than usual here, because
    /// `ShowSimulator` dispatched card items with a `_ => 0` fallthrough. A Rumble added to a
    /// career card would have run, scored **zero**, and dragged the show's rating down with a
    /// number that meant nothing.
    ///
    /// Three seams, and each one had to be found rather than assumed: the simulator's
    /// dispatch, the status economy (which is genuinely different for this format), and the
    /// save file (where `ToDto` returns null for anything it does not know, so a career
    /// Rumble would have vanished on reload).
    /// </summary>
    public class RumbleOnACardTests(ITestOutputHelper output)
    {
        private static Wrestler W(string n, int over = 60) =>
            TestRoster.Make(n, overness: over, charisma: 3.0, skill: 3.0);

        private static Career NewCareer(List<Wrestler> roster)
        {
            var start = new DateOnly(2026, 1, 6);
            return new Career
            {
                Promotion   = new Promotion { Name = "Rumble Wrestling" },
                StartDate   = start,
                CurrentDate = start,
                Roster      = roster
            };
        }

        private static RumblePlan Rumble(IReadOnlyList<Wrestler> field, Wrestler winner) => new()
        {
            Field = field.Select((w, i) => new RumbleEntrant { Wrestler = w, Number = i + 1 }).ToList(),
            EntryIntervalSeconds = 90,
            Winner = winner
        };

        // ── The status economy ───────────────────────────────────────────────

        /// <summary>
        /// **Going out costs almost nothing, and that is the point of the format.**
        ///
        /// Nobody in a battle royal was beaten — twenty-nine people went over the top in a
        /// scramble. Making that cost real standing would break the one thing this match is
        /// for: elevating somebody without spending anybody.
        /// </summary>
        [Fact]
        public void GoingOutCostsAlmostNothing()
        {
            var field  = Enumerable.Range(1, 20).Select(i => W($"W{i}")).ToList();
            var winner = field[0];

            var rumble = HeatEconomy.ForRumble(winner, field, starRating: 3.5);
            var single = HeatEconomy.ForMatch(winner, field[1], starRating: 3.5,
                                              FinishWeight.Decisive);

            double perLoser = rumble.All.Where(c => c.Wrestler != winner)
                                        .Average(c => c.OvernessDelta);

            output.WriteLine($"  out of a battle royal: {perLoser:F3}");
            output.WriteLine($"  beaten in a singles  : {single.Loser.OvernessDelta:F3}");

            Assert.True(perLoser < 0, "going out cost nothing at all");
            Assert.True(Math.Abs(perLoser) < Math.Abs(single.Loser.OvernessDelta) / 3.0,
                "going out of a battle royal cost near what being pinned costs — the format " +
                "is supposed to be cheap");
        }

        /// <summary>
        /// **Winning is worth what the field was worth, not what the field was sized.**
        ///
        /// Thirty enhancement wrestlers is not a bigger night than five main-eventers, and a
        /// booker who thinks otherwise is counting bodies. So the rub reads the field's
        /// standing, with the size only scaling it and doing so with heavy diminishing
        /// returns.
        /// </summary>
        [Fact]
        public void WinningIsWorthWhatTheFieldWasWorth()
        {
            var winner = W("Winner");

            var stacked = new[] { winner }
                .Concat(Enumerable.Range(1, 9).Select(i => W($"Star{i}", over: 90))).ToList();
            var padded = new[] { winner }
                .Concat(Enumerable.Range(1, 29).Select(i => W($"Nobody{i}", over: 15))).ToList();

            double strong = HeatEconomy.ForRumble(winner, stacked, 3.5).Winner.OvernessDelta;
            double weak   = HeatEconomy.ForRumble(winner, padded,  3.5).Winner.OvernessDelta;

            output.WriteLine($"  ten main-eventers : +{strong:F3}");
            output.WriteLine($"  thirty nobodies   : +{weak:F3}");

            Assert.True(strong > weak,
                "a padded field out-rubbed a stacked one — the rule is counting bodies");
        }

        /// <summary>
        /// **A genuine iron-man run is worth something to somebody who did not win.** The
        /// format's second rub, and a real one: the wrestler who came out early and was still
        /// there at the end is made by the match whoever won it.
        /// </summary>
        [Fact]
        public void GoingTheDistanceIsWorthSomethingWithoutWinning()
        {
            var field  = Enumerable.Range(1, 20).Select(i => W($"W{i}")).ToList();
            var winner = field[0];
            var iron   = field[5];

            var withRun = HeatEconomy.ForRumble(winner, field, 3.5, iron, ironManShare: 0.8);
            var without = HeatEconomy.ForRumble(winner, field, 3.5, iron, ironManShare: 0.1);

            double ran     = withRun.All.Single(c => c.Wrestler == iron).OvernessDelta;
            double did_not = without.All.Single(c => c.Wrestler == iron).OvernessDelta;

            output.WriteLine($"  outlasted 80% of the field: {ran:+0.000;-0.000}");
            output.WriteLine($"  went out early            : {did_not:+0.000;-0.000}");

            Assert.True(ran > 0,       "going the distance was worth nothing");
            Assert.True(did_not < 0,   "going out early was still a gain");
        }

        // ── On a card ────────────────────────────────────────────────────────

        /// <summary>
        /// **The show simulator runs it and scores it.**
        ///
        /// It used to fall through `_ => 0`. A Rumble on a card scored zero and pulled the
        /// show's rating down with it — running and lying, which is the failure this project
        /// keeps writing down as the one to avoid.
        /// </summary>
        [Fact]
        public void AShowRunsARumbleAndScoresIt()
        {
            var roster = Enumerable.Range(1, 12).Select(i => W($"W{i}", over: 55 + i)).ToList();
            var rumble = Rumble(roster, roster[^1]);

            var show = new Show
            {
                Name = "Test Show",
                Date = new DateTime(2026, 1, 25),
                TotalDurationMinutes = 120,
                Card = { rumble }
            };

            var result = new ShowSimulator(new FeudBook(), seed: 4).Simulate(show);
            var item = result.Items.Single();

            output.WriteLine($"  {item.Label}: raw {item.RawScore:F1}, score {item.Score:F1}");
            foreach (var n in item.Notes) output.WriteLine($"    {n}");

            Assert.True(item.RawScore > 0, "the rumble scored zero — it fell through the dispatch");
            Assert.NotNull(item.RumbleResult);
            Assert.Contains(item.Notes, n => n.Contains("wins the"));
            Assert.True(result.OverallRating > 0);
        }

        /// <summary>And it moves standing — the winner up, the field barely down.</summary>
        [Fact]
        public void ARumbleOnACardMovesStanding()
        {
            var roster = Enumerable.Range(1, 12).Select(i => W($"W{i}", over: 60)).ToList();
            var winner = roster[3];

            var show = new Show
            {
                Name = "Test Show",
                Date = new DateTime(2026, 1, 25),
                TotalDurationMinutes = 120,
                Card = { Rumble(roster, winner) }
            };

            var result = new ShowSimulator(new FeudBook(), seed: 4).Simulate(show);
            foreach (var c in result.StatusChanges)
                output.WriteLine($"  {c.Wrestler.RingName,-6} {c.OvernessDelta:+0.00;-0.00}  {c.Reason}");

            var win = result.StatusChanges.SingleOrDefault(c => c.Wrestler == winner);
            Assert.NotNull(win);
            Assert.True(win!.OvernessDelta > 0, "the winner of a battle royal gained nothing");
        }

        // ── Saved and reloaded ───────────────────────────────────────────────

        /// <summary>
        /// **A booked Rumble survives a save and reload.**
        ///
        /// `SaveSerializer.ToDto` returns null for a card item it does not recognise, so
        /// before this a career Rumble was written as nothing and was simply gone when the
        /// player came back. Silent, and not the kind of thing a player finds out about
        /// until the show they booked is missing its main event.
        /// </summary>
        [Fact]
        public void ARumbleSurvivesASaveAndReload()
        {
            var roster = Enumerable.Range(1, 10).Select(i => W($"W{i}")).ToList();
            var rumble = Rumble(roster, roster[2]);
            rumble.Moments =
            [
                new() { Kind = RumbleMomentKind.SurpriseReturn, Cast = [roster[7]], At = 0.4 },
                new() { Kind = RumbleMomentKind.Showdown, Cast = [roster[0], roster[1]], At = 0.7 }
            ];

            var career = NewCareer(roster);
            var show = career.Schedule("Royal Rumble", career.CurrentDate, ShowType.PremiumEvent);
            show.Card.Add(rumble);

            var reloaded = SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster);
            var back = Assert.IsType<RumblePlan>(reloaded.Shows.Single().Card.Single());

            output.WriteLine($"  {back.Name}, winner {back.Winner?.RingName}, " +
                             $"{back.Moments.Count} moments, {back.EntryIntervalSeconds}s apart");

            Assert.Equal(10, back.Field.Count);
            Assert.Equal(rumble.Winner!.Id, back.Winner!.Id);
            Assert.Equal(90, back.EntryIntervalSeconds);
            Assert.Equal(2, back.Moments.Count);
            Assert.Equal(RumbleMomentKind.SurpriseReturn, back.Moments[0].Kind);
            Assert.Equal(roster[7].Id, back.Moments[0].Cast.Single().Id);

            // Entry order is the story, so it has to come back in order.
            Assert.Equal(Enumerable.Range(1, 10), back.Field.Select(e => e.Number));
            Assert.Empty(back.Validate());
        }

        /// <summary>
        /// A battle royal round-trips too — the interval is the one number that separates the
        /// formats, so a save that lost it would reload a different match.
        /// </summary>
        [Fact]
        public void ABattleRoyalRoundTripsAsABattleRoyal()
        {
            var roster = Enumerable.Range(1, 8).Select(i => W($"W{i}")).ToList();
            var royal = Rumble(roster, roster[0]);
            royal.EntryIntervalSeconds = 0;

            var career = NewCareer(roster);
            var show = career.Schedule("Show", career.CurrentDate, ShowType.PremiumEvent);
            show.Card.Add(royal);

            var back = Assert.IsType<RumblePlan>(
                SaveSerializer.FromJson(SaveSerializer.ToJson(career), roster)
                              .Shows.Single().Card.Single());

            Assert.True(back.IsBattleRoyal);
            Assert.Equal(0, back.EntryIntervalSeconds);
        }

        /// <summary>
        /// And a card naming somebody the roster has lost is dropped whole rather than
        /// rebuilt a wrestler short — the rule the match branch already follows, because a
        /// thirty-strong Rumble quietly becoming a twenty-nine is a booking nobody made.
        /// </summary>
        [Fact]
        public void ARumbleNamingADepartedWrestler_IsDroppedWhole()
        {
            var roster = Enumerable.Range(1, 10).Select(i => W($"W{i}")).ToList();
            var career = NewCareer(roster);
            var show = career.Schedule("Show", career.CurrentDate, ShowType.PremiumEvent);
            show.Card.Add(Rumble(roster, roster[0]));

            string json = SaveSerializer.ToJson(career);
            var thinner = roster.Take(9).ToList();     // one of them has left

            var reloaded = SaveSerializer.FromJson(json, thinner);
            output.WriteLine($"  card items after the departure: {reloaded.Shows.Single().Card.Count}");
            Assert.Empty(reloaded.Shows.Single().Card);
        }
    }
}
