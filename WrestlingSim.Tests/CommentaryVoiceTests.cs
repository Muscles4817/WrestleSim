using System.Text.RegularExpressions;
using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The commentary called every wrestler "him".
    ///
    /// That was survivable while the shipped roster was mostly men. It is not now: the roster
    /// is thirty-eight women and thirty-eight men, the match builder supports intergender
    /// bookings on purpose and warns about them rather than blocking them, and a near tag in
    /// a women's tag match read *"Bianca Belair pulled him away"*.
    ///
    /// Eight templates in <see cref="MatchEngine"/>, two beat descriptions and one line of UI.
    /// Wrestling's own vocabulary is left alone where it is the name of a thing — a six-man
    /// tag is a six-man tag, and the face in peril is the face in peril. What changed is the
    /// pronouns that attach to a named performer, and the referee's.
    /// </summary>
    public class CommentaryVoiceTests(ITestOutputHelper output)
    {
        // Gendered pronouns, as whole words. "Comeback"/"finisher"/"they" must not match, and
        // neither must a ring name that happens to contain them.
        private static readonly Regex Gendered =
            new(@"\b(he|him|his|himself|she|her|hers|herself)\b", RegexOptions.IgnoreCase);

        [Fact]
        public void NoBeatTemplate_AssumesAWrestlersGender()
        {
            var offenders = BeatLibrary.All
                .SelectMany(t => new[]
                {
                    (t.Name, Field: "Description", Text: t.Description),
                    (t.Name, Field: "BookerTip",   Text: t.BookerTip ?? "")
                })
                .Where(x => Gendered.IsMatch(x.Text))
                .ToList();

            foreach (var o in offenders) output.WriteLine($"  {o.Name} · {o.Field}: {o.Text}");
            Assert.Empty(offenders);
        }

        /// <summary>
        /// And the same for what the engine actually says, measured rather than grepped —
        /// a template only reachable through one beat type is a template a grep of the file
        /// can still miss if it is built by string concatenation.
        /// </summary>
        [Fact]
        public void NoCommentaryLine_AssumesAWrestlersGender()
        {
            var roster = DataLoaders.LoadEmbeddedWrestlers().Take(8).ToList();
            var offenders = new HashSet<string>();
            int lines = 0;

            foreach (var st in MatchStructureLibrary.All.Where(x => !x.RequiresFeud))
            foreach (int size in new[] { 1, 2, 3 })
            {
                if (st.SideSize != size) continue;

                var sideA = MatchSide.Of(roster.Take(size).ToArray());
                var sideB = MatchSide.Of(roster.Skip(4).Take(size).ToArray());

                foreach (MatchType type in Enum.GetValues<MatchType>())
                for (int seed = 0; seed < 12; seed++)
                {
                    var plan = new MatchPlanModel
                    {
                        SideA = sideA, SideB = sideB, MatchType = type,
                        Beats = st.Beats.Select(x => x.Clone()).ToList()
                    };
                    if (plan.Validate().Count > 0) continue;

                    foreach (var line in new MatchEngine(seed).Execute(plan)
                                             .BeatResults.SelectMany(b => b.Commentary))
                    {
                        lines++;
                        // Strip the ring names first — "Iyo Sky" and the like must not be
                        // scanned for pronouns hiding inside a surname.
                        string stripped = line;
                        foreach (var w in roster) stripped = stripped.Replace(w.RingName, "~");
                        if (Gendered.IsMatch(stripped)) offenders.Add(line);
                    }
                }
            }

            output.WriteLine($"  {lines:N0} commentary lines scanned, {offenders.Count} gendered");
            foreach (var o in offenders.Take(10)) output.WriteLine($"    {o}");

            Assert.Empty(offenders);
        }
    }
}
