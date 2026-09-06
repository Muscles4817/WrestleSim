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
    /// Eight templates in <see cref="MatchEngine"/>, three beat descriptions, a structure
    /// description and three lines of UI.
    ///
    /// Note what these tests do <b>not</b> cover: nothing here scans `.razor` or `README.md`,
    /// because neither is reachable through the object model. Two of the three UI strings
    /// and both README fixes are therefore unguarded, and a future edit could reintroduce
    /// them silently. Guarding them means a file-scanning test, which is a different kind of
    /// test with different failure modes; recorded rather than pretended away.
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

        /// <summary>
        /// The nouns, which the pronoun sweep missed because it was looking for pronouns.
        ///
        /// Wrestling's own vocabulary is deliberately exempt where it is the *name* of a
        /// thing — a six-man tag is a six-man tag, "the legal man" is what the rule is
        /// called. What this catches is "one man", "the big man", "a fresh third man":
        /// a person in a role, described by gender, in text a player reads.
        /// </summary>
        private static readonly Regex GenderedNoun =
            new(@"\b(one|the|a|another|third|fourth|fresh|big|same|other) (man|woman|guy|girl)\b",
                RegexOptions.IgnoreCase);

        [Fact]
        public void NoLibraryText_AssumesAWrestlersGender()
        {
            // Structures as well as beats. Review found `Six-Man War`'s Description carrying
            // "three heels rotating on one man … a fresh third man to finish" — the same
            // phrasing that had just been rewritten in `Face in Peril` — because nothing
            // scanned MatchStructureLibrary.
            var offenders = BeatLibrary.All
                .SelectMany(t => new[]
                {
                    (t.Name, Field: "Description", Text: t.Description),
                    (t.Name, Field: "BookerTip",   Text: t.BookerTip ?? "")
                })
                .Concat(MatchStructureLibrary.All.Select(
                    st => (st.Name, Field: "Description", Text: st.Description)))
                .Where(x => Gendered.IsMatch(x.Text) || GenderedNoun.IsMatch(x.Text))
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
            var seen = new HashSet<BeatType>();
            int lines = 0;

            void Scan(MatchPlanModel plan, int seed)
            {
                if (plan.Validate().Count > 0) return;

                foreach (var beat in new MatchEngine(seed).Execute(plan).BeatResults)
                {
                    seen.Add(beat.BeatType);
                    foreach (var line in beat.Commentary)
                    {
                        lines++;
                        // Strip the ring names first — a surname containing "her" or "his"
                        // would otherwise false-positive for ever, and a test that cries
                        // wolf gets disabled inside a month.
                        string stripped = line;
                        foreach (var w in roster) stripped = stripped.Replace(w.RingName, "~");
                        if (Gendered.IsMatch(stripped) || GenderedNoun.IsMatch(stripped))
                            offenders.Add(line);
                    }
                }
            }

            // Every shipped structure, at every side size it is legal for.
            foreach (var st in MatchStructureLibrary.All.Where(x => !x.RequiresFeud))
            {
                int size = st.SideSize;
                var sideA = MatchSide.Of(roster.Take(size).ToArray());
                var sideB = MatchSide.Of(roster.Skip(4).Take(size).ToArray());

                foreach (MatchType type in Enum.GetValues<MatchType>())
                for (int seed = 0; seed < 12; seed++)
                    Scan(new MatchPlanModel
                    {
                        SideA = sideA, SideB = sideB, MatchType = type,
                        Beats = st.Beats.Select(x => x.Clone()).ToList()
                    }, seed);
            }

            // And every beat the shipped structures do not reach.
            //
            // Review mutated each of the eight changed templates back to its old wording one
            // at a time: six failed this test and **two did not**. The disqualification
            // finish appears in no preset at all, and the low-charge hot tag needs a charge
            // under 0.85 that no preset produces. Both were found by grep, which is exactly
            // what the build log claimed this test had improved on — so it now books every
            // beat type in the library by hand rather than only what the presets happen to
            // use, and the coverage is asserted rather than hoped for.
            foreach (var template in BeatLibrary.All)
            {
                foreach (int size in new[] { 1, 2, 3 })
                {
                    var sideA = MatchSide.Of(roster.Take(size).ToArray());
                    var sideB = MatchSide.Of(roster.Skip(4).Take(size).ToArray());

                    var beats = new List<MatchBeat>
                    {
                        BeatLibrary.Find("Standard Collar-and-Elbow")!.ToMatchBeat(BeatControl.Even)
                    };

                    // A finish has to be the only finish and has to be last; everything else
                    // is booked twice so both control sides render.
                    if (template.ToMatchBeat(BeatControl.WrestlerA).IsFinish)
                    {
                        beats.Add(template.ToMatchBeat(BeatControl.WrestlerA));
                    }
                    else
                    {
                        beats.Add(template.ToMatchBeat(BeatControl.WrestlerB));
                        beats.Add(template.ToMatchBeat(BeatControl.WrestlerA));
                        beats.Add(BeatLibrary.Find("Clean Victory")!.ToMatchBeat(BeatControl.WrestlerA));
                    }

                    // Feud-flavoured beats will not validate without one, and two of them
                    // want specific history tags on top of the intensity.
                    var feud = new Feud { SideA = sideA.Members.ToList(), SideB = sideB.Members.ToList() };
                    feud.SetMinimumIntensity(FeudIntensity.Nuclear);
                    foreach (var tag in Enum.GetValues<FeudHistoryTag>()) feud.AddTag(tag);

                    // And AlliesRejected only validates after a ThirdPartyPullIn, so it gets
                    // one. This is the last of the six the preset sweep could not reach.
                    if (beats.Any(x => x.Type == BeatType.AlliesRejected))
                        beats.Insert(1, BeatLibrary.All.First(t => t.Type == BeatType.ThirdPartyPullIn)
                                                       .ToMatchBeat(BeatControl.WrestlerB));

                    for (int seed = 0; seed < 4; seed++)
                        Scan(new MatchPlanModel { SideA = sideA, SideB = sideB, Feud = feud,
                            Beats = beats.Select(x => x.Clone()).ToList() }, seed);
                }
            }

            var unreached = Enum.GetValues<BeatType>().Where(t => !seen.Contains(t)).ToList();
            output.WriteLine($"  {lines:N0} commentary lines scanned across {seen.Count} beat types; " +
                             $"{offenders.Count} gendered");
            foreach (var o in offenders.Take(10)) output.WriteLine($"    {o}");
            if (unreached.Count > 0) output.WriteLine($"  never reached: {string.Join(", ", unreached)}");

            Assert.Empty(offenders);

            // The coverage itself is the assertion that stops this decaying. A beat type
            // nothing books is a beat type nothing checks.
            Assert.True(unreached.Count == 0,
                $"{unreached.Count} beat types never ran, so their commentary is unscanned: " +
                string.Join(", ", unreached));
        }
    }
}
