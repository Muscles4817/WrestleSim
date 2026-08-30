using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Tests
{
    public class SegmentLibraryTests
    {
        private static List<Wrestler> Cast(int n) =>
            Enumerable.Range(1, n).Select(i => TestRoster.Make($"Wrestler {i}")).ToList();

        // ── Template library ─────────────────────────────────────────────────

        [Fact]
        public void EverySegmentFactoryBuilder_IsReachable()
        {
            // Before this branch only CreatePromo had a caller; the other ten builders
            // were written and unreferenced.
            Assert.Equal(11, SegmentTemplateLibrary.All.Count);
        }

        [Fact]
        public void EveryTemplate_BuildsAValidSegment()
        {
            foreach (var template in SegmentTemplateLibrary.All)
            {
                var cast = Cast(Math.Max(template.MinParticipants, 2));
                var segment = template.Create(cast, "Test dialogue.");

                Assert.NotEmpty(segment.Actions);
                Assert.NotEmpty(segment.Participants);
                Assert.Equal(template.Type, segment.Type);
                Assert.Equal(template.Location, segment.Location);

                var errors = segment.Validate();
                Assert.True(errors.Count == 0,
                    $"{template.Name} produced an invalid segment: {string.Join("; ", errors)}");
            }
        }

        [Fact]
        public void EveryTemplate_RunsThroughTheSimulator()
        {
            foreach (var template in SegmentTemplateLibrary.All)
            {
                var segment = template.Create(Cast(Math.Max(template.MinParticipants, 2)), "Test dialogue.");
                var result  = new SegmentSimulator(13).Simulate(segment);

                Assert.InRange(result.AudienceImpact, 0, 10);
                Assert.True(result.HeatGenerated >= 0);
            }
        }

        [Fact]
        public void Template_StampsItsDeclaredHistoryTags()
        {
            foreach (var template in SegmentTemplateLibrary.All.Where(t => t.HistoryTags.Count > 0))
            {
                var segment = template.Create(Cast(Math.Max(template.MinParticipants, 2)));
                Assert.Equal(template.HistoryTags.OrderBy(t => t), segment.HistoryTags.OrderBy(t => t));
            }
        }

        [Fact]
        public void Template_RejectsAnUndersizedCast()
        {
            var beatdown = SegmentTemplateLibrary.Find("Post-Match Beatdown")!;
            Assert.Throws<ArgumentException>(() => beatdown.Create(Cast(1)));
        }

        [Fact]
        public void Bookable_FiltersByRosterSize()
        {
            Assert.All(SegmentTemplateLibrary.Bookable(1), t => Assert.Equal(1, t.MinParticipants));
            Assert.Equal(SegmentTemplateLibrary.All.Count, SegmentTemplateLibrary.Bookable(20).Count());
        }

        [Fact]
        public void FactionDominance_CastsVictimFirstThenTheFaction()
        {
            var cast = Cast(4); // victim + three faction members
            var segment = SegmentTemplateLibrary.Find("Faction Dominance")!.Create(cast);

            // Everybody is in it, and the victim never performs an action.
            Assert.Equal(4, segment.Participants.Count);
            Assert.DoesNotContain(segment.Actions, a => a.Performer == cast[0]);
            Assert.All(segment.Actions.Where(a => a.Target != null), a => Assert.Equal(cast[0], a.Target));
        }

        [Fact]
        public void PostMatchBeatdown_ScalesWithAttackerCount()
        {
            var solo = SegmentTemplateLibrary.Find("Post-Match Beatdown")!.Create(Cast(2));
            var gang = SegmentTemplateLibrary.Find("Post-Match Beatdown")!.Create(Cast(5));

            Assert.True(gang.Actions.Count > solo.Actions.Count);
        }

        [Fact]
        public void AuthorityAnnouncement_UnlocksTheOutsidePartyBeatTag()
        {
            // ManagerConflict is one of the two tags MatchPlan.Validate accepts for
            // ThirdPartyPullIn, so this template is a way to earn that beat by booking.
            var template = SegmentTemplateLibrary.Find("Authority Announcement")!;
            Assert.Contains(FeudHistoryTag.ManagerConflict, template.HistoryTags);
        }

        // ── Action library ───────────────────────────────────────────────────

        [Fact]
        public void EveryActionTemplate_BuildsAUsableAction()
        {
            var performer = TestRoster.Babyface;
            var target    = TestRoster.Heel;

            foreach (var template in SegmentActionLibrary.All)
            {
                var action = template.ToAction(performer, template.RequiresTarget ? target : null, "Line.");

                Assert.Equal(template.ActionType, action.ActionType);
                Assert.Same(performer, action.Performer);
                Assert.True(action.BaseImpact > 0);
                Assert.InRange(action.OvernessImpact, 0.5, 3.0);
                Assert.Equal(template.Name, action.Label);
            }
        }

        [Fact]
        public void ActionTemplates_HaveUniqueNames()
        {
            var names = SegmentActionLibrary.All.Select(t => t.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void SegmentTemplates_HaveUniqueNamesAndAreFindable()
        {
            foreach (var template in SegmentTemplateLibrary.All)
                Assert.Same(template, SegmentTemplateLibrary.Find(template.Name));

            var names = SegmentTemplateLibrary.All.Select(t => t.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void PhysicalActions_OutHeatVerbalOnes()
        {
            double talk    = SegmentActionLibrary.Find("Cut a Promo")!.Heat;
            double weapon  = SegmentActionLibrary.Find("Weapon Shot")!.Heat;
            double turn    = SegmentActionLibrary.Find("Turn on a Partner")!.Heat;

            Assert.True(weapon > talk);
            Assert.True(turn > weapon);
        }
    }
}
