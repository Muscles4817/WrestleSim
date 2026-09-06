using Xunit.Abstractions;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The shipped data itself, as opposed to what the engine does with it.
    ///
    /// <see cref="RosterDifferentiationTests"/> already guards the shape of the roster's
    /// attributes. What it cannot see is whether the data is internally coherent: a card
    /// that has a main event and nothing under it, a gimmick with no appeal ratings, or a
    /// tag team naming somebody who is not on the roster and therefore silently vanishing.
    /// Those are all authoring mistakes rather than engine faults, and they are only ever
    /// caught by looking at the file.
    /// </summary>
    public class ShippedRosterTests(ITestOutputHelper output)
    {
        private static readonly List<Wrestler> Roster = DataLoaders.LoadEmbeddedWrestlers();
        private static readonly DateOnly Day = new(2025, 6, 1);

        [Fact]
        public void TheRosterLoads_AndBothDivisionsFillACard()
        {
            var byDivision = Roster.GroupBy(w => w.Division)
                                   .ToDictionary(g => g.Key, g => g.ToList());

            output.WriteLine($"  {Roster.Count} wrestlers");
            foreach (var (division, list) in byDivision.OrderBy(k => k.Key.ToString()))
            {
                var counts = list.GroupBy(w => w.CardPosition)
                                 .OrderByDescending(g => g.Key)
                                 .Select(g => $"{g.Key.Label()} {g.Count()}");
                output.WriteLine($"  {division,-7} {list.Count,3}   {string.Join(" · ", counts)}");
            }

            // Every division needs a full card, not just a top: an enhancement talent to
            // beat and a midcard to elevate out of are what make a push mean anything.
            foreach (var (division, list) in byDivision)
                foreach (var position in Enum.GetValues<CardPosition>())
                    Assert.True(list.Any(w => w.CardPosition == position),
                        $"The {division} division has nobody at {position.Label()}, " +
                        "so that rung of the card cannot be booked.");
        }

        [Fact]
        public void EveryWrestler_IsFullyAuthored()
        {
            var problems = new List<string>();

            foreach (var w in Roster)
            {
                string who = w.RingName ?? w.RealName;

                if (string.IsNullOrWhiteSpace(w.RealName)) problems.Add($"{who}: no real name");
                if (w.Gimmick is not { } g) { problems.Add($"{who}: no gimmick"); continue; }

                if (string.IsNullOrWhiteSpace(g.Name)) problems.Add($"{who}: gimmick has no name");
                if (string.IsNullOrWhiteSpace(g.PersonaDescriptor)) problems.Add($"{who}: no persona descriptor");
                if (g.GimmickTraits is not { Count: > 0 }) problems.Add($"{who}: no gimmick traits");
                if (g.AppealRatings is not { Count: > 0 }) problems.Add($"{who}: no fan-group appeal");
                else foreach (var a in g.AppealRatings)
                {
                    if (!Enum.TryParse<FanGroup>(a.Group, ignoreCase: true, out _))
                        problems.Add($"{who}: '{a.Group}' is not a fan group");
                    if (a.AppealScore is < 0 or > 1)
                        problems.Add($"{who}: appeal to {a.Group} is {a.AppealScore}, outside 0–1");
                }

                if (g.Freshness is < 0 or > 1) problems.Add($"{who}: freshness {g.Freshness} outside 0–1");
                if (w.Charisma is < 0 or > 5) problems.Add($"{who}: charisma {w.Charisma} outside 0–5");
                if (w.Overness is < 0 or > 100) problems.Add($"{who}: overness {w.Overness} outside 0–100");
                if (w.Physical.Size is < 1 or > 5) problems.Add($"{who}: size {w.Physical.Size} outside 1–5");

                if (w.Moveset is not { Count: > 0 }) problems.Add($"{who}: no moves");
                if (w.Signature is not { Count: > 0 }) problems.Add($"{who}: no signature");
                else if (!w.Signature.Any(s => s.IsFinisher)) problems.Add($"{who}: no finisher");
            }

            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void RingNamesAndIdentities_AreUnique()
        {
            // Ids key saves, and the booking screens pick people by ring name. Two of
            // either and the game quietly loses one of them.
            var duplicateIds = Roster.GroupBy(w => w.Id, StringComparer.OrdinalIgnoreCase)
                                     .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            var duplicateNames = Roster.GroupBy(w => w.RingName, StringComparer.OrdinalIgnoreCase)
                                       .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            Assert.True(duplicateIds.Count == 0, $"Duplicate ids: {string.Join(", ", duplicateIds)}");
            Assert.True(duplicateNames.Count == 0, $"Duplicate ring names: {string.Join(", ", duplicateNames)}");
        }

        // ── Seeded teams ─────────────────────────────────────────────────────

        [Fact]
        public void SeededTeams_BindToTheRosterInstancesTheCareerWillBook()
        {
            var teams = TagTeamSeed.SeedDefaults(Roster, Day);

            output.WriteLine($"  {teams.Count} teams");
            foreach (var t in teams)
                output.WriteLine($"  {t.Name,-24} {string.Join(" & ", t.Members.Select(m => m.RingName)),-34} " +
                                 $"{t.MatchesTogether,4} matches · {t.ChemistryLabel} ({t.Chemistry:F2}) · {t.TenureLabel(Day)}");

            Assert.NotEmpty(teams);

            foreach (var team in teams)
            {
                Assert.True(team.Members.Count >= 2, $"{team.Name} has fewer than two members");

                // Reference equality, not name equality: a team holding copies would build
                // chemistry for people the career is not booking.
                foreach (var m in team.Members)
                    Assert.Contains(Roster, r => ReferenceEquals(r, m));

                // A team spanning divisions is bookable but is not something to ship by
                // default, and is almost always an authoring slip.
                Assert.True(team.Members.Select(m => m.Division).Distinct().Count() == 1,
                    $"{team.Name} spans both divisions");

                Assert.InRange(team.Chemistry, 0, 1);
                Assert.True(team.IsActive, $"{team.Name} starts disbanded");
                Assert.True(team.Formed < Day, $"{team.Name} has not formed yet on day one");
            }

            // The list has to be a set of distinct pairs, or Career.TeamFor cannot tell
            // which team two people are.
            var pairs = teams.Select(t => string.Join("|", t.Members.Select(m => m.Id).OrderBy(x => x))).ToList();
            Assert.Equal(pairs.Count, pairs.Distinct().Count());

            // Somebody has to have been together long enough to read as an act. A division
            // where every team formed last week is the state this seeding exists to avoid.
            Assert.Contains(teams, t => t.Chemistry >= 0.85 && t.MatchesTogether >= 50);

            // And not everybody: a team still finding it is where the player's own team
            // building starts from.
            Assert.Contains(teams, t => t.Chemistry < 0.7);

            Assert.Contains(teams, t => t.Members[0].Division == Division.Mens);
            Assert.Contains(teams, t => t.Members[0].Division == Division.Womens);
        }

        [Fact]
        public void SeededTeams_AreSkippedRatherThanBrokenWhenAMemberIsMissing()
        {
            // The seed file names people by real name. If a roster edit removes one of
            // them the team has to disappear, not bind to a half-side or throw on startup.
            var thinned = Roster.Skip(1).ToList();

            var full   = TagTeamSeed.SeedDefaults(Roster, Day);
            var missing = TagTeamSeed.SeedDefaults(thinned, Day);

            Assert.True(missing.Count <= full.Count);
            Assert.All(missing, t => Assert.All(t.Members, m => Assert.Contains(m, thinned)));

            Assert.Empty(TagTeamSeed.SeedDefaults(new List<Wrestler>(), Day));
        }

        [Fact]
        public void SeededTeams_StartInsideTheirGracePeriod()
        {
            // Chemistry decays from the last night a team worked. A seeded team that opens
            // the save already outside its grace period starts losing what it was given
            // before the player has booked anything.
            foreach (var team in TagTeamSeed.SeedDefaults(Roster, Day))
            {
                double before = team.Chemistry;
                team.Decay(Day);
                Assert.Equal(before, team.Chemistry, 6);
            }
        }
    }
}
