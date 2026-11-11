using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// Finishers, and the tag-exclusive rule that is the whole reason this exists.
    /// </summary>
    public class FinisherBookTests
    {
        private static Signature Fin(string name, int over, bool tag = false) =>
            new(name,
                new Move(name, [MoveType.Slam], SkillCheck.Medium, SkillCheck.Medium,
                         SkillCheck.VeryHard, false),
                isFinisher: true, impactModifier: SkillCheck.VeryHard,
                overness: over, tagExclusive: tag);

        private static Wrestler Hand(string name, params Signature[] sigs)
        {
            var w = TestRoster.Make(name);
            w.Signature = [.. sigs];
            return w;
        }

        private static MatchSide Team(double chemistry, params Wrestler[] members)
        {
            var side = MatchSide.Of(members);
            if (members.Length > 1)
                side.Team = new TagTeam
                {
                    Name = "Test Team",
                    Members = [.. members],
                    Formed = new DateOnly(2026, 1, 1),
                    Chemistry = chemistry
                };
            return side;
        }

        // ── The rule ─────────────────────────────────────────────────────────

        [Fact]
        public void ATagFinisherIsNeverAvailableInSingles()
        {
            // The whole point. A move for two people cannot be hit by one, however over it
            // is and whatever else the wrestler has.
            var solo = Hand("Lone Hand", Fin("Double Stomp Reverse DDT", 90, tag: true));
            var side = Team(1.0, solo);

            Assert.False(FinisherBook.CanUseTagFinish(side));
            Assert.Null(FinisherBook.For(solo, side));
            Assert.Equal("the finisher", FinisherBook.Describe(FinisherBook.For(solo, side)));
        }

        [Fact]
        public void ASinglesFinisherIsUsedWhenTheTagOneIsUnavailable()
        {
            var w = Hand("Both Hands",
                Fin("Spear", 80),
                Fin("Midnight Hour", 90, tag: true));

            var alone = Team(1.0, w);
            Assert.Equal("Spear", FinisherBook.For(w, alone)?.Name);
        }

        [Fact]
        public void TheTeamFinishOutranksTheSinglesOneInATagMatch()
        {
            // A match built to the double-team finish should end with it, not with one
            // man's singles move.
            var a = Hand("One", Fin("Spear", 80), Fin("Midnight Hour", 70, tag: true));
            var b = Hand("Two", Fin("Cutter", 50), Fin("Midnight Hour", 70, tag: true));
            var side = Team(0.85, a, b);

            Assert.True(FinisherBook.CanUseTagFinish(side));
            Assert.Equal("Midnight Hour", FinisherBook.For(a, side)?.Name);
            Assert.Equal("Midnight Hour", FinisherBook.For(b, side)?.Name);
        }

        [Fact]
        public void TwoStrangersDoNotHitADrilledDoubleTeamMove()
        {
            // A side of two with no team behind it reads 0.00 chemistry. They are in a tag
            // match and still have no move of their own, which is the second half of the
            // rule — being two is necessary and not sufficient.
            var a = Hand("Stranger", Fin("Spear", 80), Fin("Midnight Hour", 70, tag: true));
            var b = Hand("Other", Fin("Cutter", 50));

            var adHoc = MatchSide.Of(a, b);   // no Team assigned
            Assert.Equal(0.0, adHoc.Chemistry, 6);
            Assert.False(FinisherBook.CanUseTagFinish(adHoc));
            Assert.Equal("Spear", FinisherBook.For(a, adHoc)?.Name);
        }

        [Fact]
        public void ADriftedTeamStillKnowsTheMove()
        {
            // The threshold asks "have these two ever drilled this", not "are they any
            // good" — so a team whose chemistry has decayed keeps its finish.
            var a = Hand("One", Fin("Spear", 80), Fin("Midnight Hour", 70, tag: true));
            var b = Hand("Two", Fin("Cutter", 50), Fin("Midnight Hour", 70, tag: true));

            Assert.True(FinisherBook.CanUseTagFinish(Team(FinisherBook.DrilledEnough, a, b)));
            Assert.False(FinisherBook.CanUseTagFinish(Team(FinisherBook.DrilledEnough - 0.01, a, b)));
        }

        // ── Credibility ──────────────────────────────────────────────────────

        [Fact]
        public void ATypicalFinisherIsWorthExactlyNothingExtra()
        {
            // Centred, so wiring this in does not silently re-rate every match in the game.
            Assert.Equal(1.0, FinisherBook.Credibility(Fin("Median", (int)FinisherBook.TypicalCredibility)), 6);
            Assert.Equal(1.0, FinisherBook.Credibility(null), 6);
        }

        [Fact]
        public void AnEstablishedFinisherClosesHarderThanOneNobodyBelieves()
        {
            double spear  = FinisherBook.Credibility(Fin("Spear", 96));
            double nobody = FinisherBook.Credibility(Fin("Cutter", 24));

            Assert.True(spear > 1.0);
            Assert.True(nobody < 1.0);

            // And the swing is small: the finish is carried by who is winning and whether
            // they earned it. The move is the last few per cent.
            Assert.True(spear / nobody < 1.35,
                $"Finisher standing swung the finish by {spear / nobody:P0}, which is too much.");
        }

        [Fact]
        public void Describe_NamesTheMoveOrFallsBackCleanly()
        {
            Assert.Equal("the Spear", FinisherBook.Describe(Fin("Spear", 90)));
            Assert.Equal("the finisher", FinisherBook.Describe(null));
            Assert.Equal("the finisher", FinisherBook.Describe(Fin("  ", 90)));
        }

        [Fact]
        public void AMoveThatBringsItsOwnArticleDoesNotGetASecond()
        {
            // "ends it with the The Chalkboard" is what the first version printed.
            Assert.Equal("The Chalkboard", FinisherBook.Describe(Fin("The Chalkboard", 60)));
            Assert.Equal("An Honest Day's Work", FinisherBook.Describe(Fin("An Honest Day's Work", 60)));

            // And a name merely beginning with those letters is not an article.
            Assert.Equal("the Theatre Kick", FinisherBook.Describe(Fin("Theatre Kick", 60)));
            Assert.Equal("the Anaconda Vice", FinisherBook.Describe(Fin("Anaconda Vice", 60)));
        }

        [Fact]
        public void NoShippedFinisherReadsWithADoubleArticle()
        {
            var bad = DataLoaders.LoadEmbeddedWrestlers()
                .SelectMany(w => w.Signature.Where(s => s.IsFinisher))
                .Select(FinisherBook.Describe)
                .Where(t => t.StartsWith("the The ", StringComparison.OrdinalIgnoreCase)
                         || t.StartsWith("the A ",   StringComparison.Ordinal)
                         || t.StartsWith("the An ",  StringComparison.Ordinal))
                .ToList();

            Assert.True(bad.Count == 0, "Double article: " + string.Join(", ", bad));
        }

        // ── Against the shipped roster ───────────────────────────────────────

        [Fact]
        public void TheShippedTagFinishersResolveOnlyForTheirOwnTeam()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers();
            var teams  = TagTeamSeed.SeedDefaults(roster, new DateOnly(2026, 1, 5));

            var withTagFinish = roster
                .Where(w => w.Signature.Any(s => s.TagExclusive))
                .ToList();

            Assert.NotEmpty(withTagFinish);

            foreach (var w in withTagFinish)
            {
                // Alone, they fall back to their singles finisher — never the team move.
                var solo = FinisherBook.For(w, MatchSide.Of(w));
                Assert.NotNull(solo);
                Assert.False(solo!.TagExclusive,
                    $"{w.RingName} would hit a double-team finish on their own.");

                // Beside their actual partner, the team move is what they go to.
                var team = teams.FirstOrDefault(t => t.Members.Contains(w));
                Assert.NotNull(team);

                var side = MatchSide.Of([.. team!.Members]);
                side.Team = team;

                Assert.True(FinisherBook.For(w, side)?.TagExclusive,
                    $"{w.RingName} does not reach their team's finish beside {team.Name}.");
            }
        }

        [Fact]
        public void EveryShippedFinisherIsNamed()
        {
            // A finisher with no name would print as "the finisher", which is the state
            // this whole class replaced.
            var unnamed = DataLoaders.LoadEmbeddedWrestlers()
                .SelectMany(w => w.Signature.Where(s => s.IsFinisher)
                                  .Select(s => (w.RingName, s.Name)))
                .Where(x => string.IsNullOrWhiteSpace(x.Name))
                .ToList();

            Assert.True(unnamed.Count == 0,
                "These finishers have no name: " + string.Join(", ", unnamed.Select(x => x.RingName)));
        }
    }
}
