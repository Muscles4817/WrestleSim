using WrestlingSim.Engine;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The roster's names and the ids derived from them.
    ///
    /// <see cref="Models.Wrestler.Id"/> is a slug of <c>RealName</c> and a save stores
    /// people by id, dropping anyone the roster no longer has along with every reign, feud
    /// and team that referenced them. So a correction to somebody's real name is a
    /// save-breaking change unless the old id is pinned, and these tests are here because
    /// ten names were corrected at once and the breakage would have been silent.
    /// </summary>
    public class RosterIdentityTests
    {
        /// <summary>Ids that existing saves may contain, and must keep resolving.</summary>
        private static readonly string[] LegacyIds =
        [
            "kiah-dean",        // now Kacy Catanzaro (Katana Chance)
            "amanda-wilson",    // now Courtney Stewart (Isla Dawn)
            "ben-carter",       // now Benjamin Timms (Nathan Frazer)
            "jacy-jayne",       // now Taylor Grado
            "tony-d-angelo",    // now Joseph Ariola
            "sol-ruca",         // now Calyx Hampton
            "ivy-nile",         // now Emily Andzulis
            "thea-hail",        // now Madison Knisley
            "karmen-petrovic",  // now Monika Klisara
            "wren-sinclair",    // now Madison Dombkowski
            "gary-blair-jr",    // now Gary Gordon (Angelo Dawkins)
            "christian-brantley", // now Christian Brigham (Carmelo Hayes)
            "stephanie-yim",    // now Stephanie Hym Bell (Michin)
            "rachel-evans",     // now Kayleigh Rae (Alba Fyre)
            "cynthia-lopez"     // now Karissa Rivera (Elektra Lopez)
        ];

        [Fact]
        public void RenamedWrestlersKeepTheIdTheirSavesWereWrittenWith()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();

            foreach (string id in LegacyIds)
                Assert.True(roster.Any(w => w.Id == id),
                    $"No wrestler answers to '{id}'. A save written before the rename " +
                     "would lose them and everything that referenced them.");
        }

        [Fact]
        public void EveryWrestlerHasAUniqueId()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();

            var clashes = roster.GroupBy(w => w.Id)
                                .Where(g => g.Count() > 1)
                                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(w => w.RealName))}")
                                .ToList();

            Assert.True(clashes.Count == 0, "Ids must be unique:\n  " + string.Join("\n  ", clashes));
        }

        [Fact]
        public void EveryShippedTagTeamResolvesAgainstTheRoster()
        {
            // TagTeamSeed matches members by RealName, NOT by Id, and Bind returns null
            // for a team with a member it cannot find — so the team simply never seeds and
            // nothing reports it. Correcting a performer's real name therefore breaks any
            // team that names them, silently.
            //
            // That is not hypothetical: correcting Isla Dawn's real name stopped the
            // Alba Fyre & Isla Dawn team seeding into new careers, and nothing caught it
            // until the next data pass read the file by hand. Hence this test.
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var teams  = DataLoaders.LoadEmbeddedTagTeams();

            Assert.NotEmpty(teams);

            var names = roster.Select(w => w.RealName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var broken = teams
                .Select(t => (t.Name, Missing: t.Members.Where(m => !names.Contains(m)).ToList()))
                .Where(x => x.Missing.Count > 0)
                .Select(x => $"{x.Name}: {string.Join(", ", x.Missing)}")
                .ToList();

            Assert.True(broken.Count == 0,
                "These teams name somebody the roster does not have, so they will not seed:\n  "
                + string.Join("\n  ", broken));
        }

        [Fact]
        public void EveryShippedTagTeamActuallySeeds()
        {
            // The stronger version of the above: not just that the names resolve, but that
            // SeedDefaults really produces every team, which is what a new career gets.
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var seeded = Models.World.TagTeamSeed.SeedDefaults(roster, new DateOnly(2026, 1, 5));

            Assert.Equal(DataLoaders.LoadEmbeddedTagTeams().Count, seeded.Count);
        }

        [Fact]
        public void NobodyIsIdentifiedOnlyByTheirCharacter()
        {
            // A RealName equal to the gimmick name means the roster does not actually know
            // who the performer is. Some wrestlers really do work under their own name, so
            // this is an allow-list rather than a ban — it exists to stop the gimmick name
            // quietly becoming the default for somebody nobody looked up.
            string[] worksUnderTheirOwnName =
            [
                "Shayna Baszler", "Akira Tozawa", "Shinsuke Nakamura",
                "Jade Cargill", "Chelsea Green", "Stephanie Vaquer",

                // Masked luchadors. Both have wrestled their whole careers under the
                // hood and neither legal name is public record, which is the point of
                // the hood — so the ring name is the only name there is.
                "Penta", "Rey Fenix",

                // Wikipedia's infobox gives these three no name but the one they wrestle
                // under. Looked up and found to be the same, rather than not looked up.
                "Jackson Drake", "Brad Baylor", "Ricky Smokes"
            ];

            var unresolved = DataLoaders.LoadEmbeddedWrestlers()
                .Where(w => w.RealName == w.RingName)
                .Select(w => w.RealName)
                .Where(n => !worksUnderTheirOwnName.Contains(n))
                .ToList();

            Assert.True(unresolved.Count == 0,
                "These entries use the character's name as the performer's:\n  " +
                string.Join("\n  ", unresolved));
        }
    }
}
