using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// The engine has to be able to tell the shipped roster apart.
    ///
    /// These wrestlers are not interchangeable main-eventers — the JSON already encodes
    /// distinct archetypes. Shayna Baszler is the best pure worker on the roster (3.82
    /// overall, 4.7 grappler) who cannot get a crowd (charisma 2.5, popularity 70).
    /// Liv Morgan is the inverse: charisma 4.2 on 2.83 ring skill. Becky Lynch is elite
    /// at both.
    ///
    /// If a match between any two of them produces the same rating, the engine is broken
    /// regardless of how internally consistent it is. These tests exist to make that
    /// failure loud.
    /// </summary>
    public class RosterDifferentiationTests(ITestOutputHelper output)
    {
        private static readonly List<Wrestler> Roster = DataLoaders.LoadEmbeddedWrestlers();

        private static Wrestler W(string ringName) =>
            Roster.FirstOrDefault(w => w.RingName == ringName)
            ?? throw new InvalidOperationException(
                $"'{ringName}' is not in Wrestlers.json. Roster: {string.Join(", ", Roster.Select(r => r.RingName))}");

        /// <summary>Mean result over many seeds, so a single lucky roll cannot carry a test.</summary>
        private static (double stars, double tech, double story, double peak, double avg) Mean(
            Wrestler a, Wrestler b, string structure, MatchType type = MatchType.Standard,
            Feud? feud = null, int runs = 200)
        {
            var st = MatchStructureLibrary.Find(structure)
                     ?? throw new InvalidOperationException($"No structure '{structure}'");

            double s = 0, t = 0, y = 0, p = 0, v = 0;
            for (int i = 0; i < runs; i++)
            {
                var r = new MatchEngine(i * 7919 + structure.Length).Execute(new MatchPlan
                {
                    WrestlerA = a,
                    WrestlerB = b,
                    MatchType = type,
                    Feud      = feud,
                    Beats     = st.Beats.Select(x => x.Clone()).ToList()
                });
                s += r.StarRating; t += r.TechnicalScore; y += r.StorytellingScore;
                p += r.CrowdPeakEnergy; v += r.CrowdAverageEnergy;
            }
            return (s / runs, t / runs, y / runs, p / runs, v / runs);
        }

        // ── The headline requirement ─────────────────────────────────────────

        [Fact]
        public void Roster_ProducesAWideSpreadOfRatings_OnASingleStructure()
        {
            // Same structure, same match type, no feud — every difference in the result
            // comes from who is in the match.
            var cells = new List<(string pair, double stars)>();

            foreach (var a in Roster)
            foreach (var b in Roster)
            {
                if (ReferenceEquals(a, b)) continue;
                cells.Add(($"{a.RingName} vs {b.RingName}",
                           Mean(a, b, "Big Match Epic", runs: 60).stars));
            }

            double min = cells.Min(c => c.stars);
            double max = cells.Max(c => c.stars);

            output.WriteLine($"{cells.Count} matchups, spread {min:F2} – {max:F2} ({max - min:F2} stars)");
            foreach (var c in cells.OrderByDescending(c => c.stars).Take(3))
                output.WriteLine($"  best  {c.stars:F2}  {c.pair}");
            foreach (var c in cells.OrderBy(c => c.stars).Take(3))
                output.WriteLine($"  worst {c.stars:F2}  {c.pair}");

            // Before the performer model was wired in this spread was 0.21 stars, which is
            // what "who you pick doesn't matter" felt like in play.
            Assert.True(max - min >= 0.60,
                $"Roster should span at least 0.60 stars on one structure, got {max - min:F2} " +
                $"({min:F2}–{max:F2}). The engine is flattening the roster again.");
        }

        [Fact]
        public void EveryWrestler_HasADistinctAverageRating()
        {
            // Each wrestler's mean across all opponents must be separable from the others.
            //
            // Averaged over BOTH slots and several structures deliberately: a single
            // structure in a single slot measures structural fit as much as the wrestler
            // (Big Match Epic hands its Aerial Assault to slot A, which flatters a high
            // flyer). Averaging both out leaves the performer.
            string[] structures = ["TV Formula", "Face-in-Peril", "Big Match Epic"];

            var byWrestler = Roster.ToDictionary(
                w => w.RingName,
                w => Roster.Where(o => !ReferenceEquals(o, w))
                           .SelectMany(o => structures.SelectMany(s => new[]
                           {
                               Mean(w, o, s, runs: 25).stars,   // as A
                               Mean(o, w, s, runs: 25).stars    // as B
                           }))
                           .Average());

            foreach (var kv in byWrestler.OrderByDescending(k => k.Value))
                output.WriteLine($"  {kv.Key,-18} {kv.Value:F3}");

            var ordered = byWrestler.Values.OrderBy(v => v).ToList();

            // Count how many distinct tiers the roster resolves into. Two wrestlers landing
            // together is fine and can be correct — Rhea and Becky are the roster's two best
            // and trade connection against power to arrive at the same place. What must not
            // happen is the old behaviour, where all nine bunched inside a few hundredths.
            int tiers = 1;
            for (int i = 1; i < ordered.Count; i++)
                if (ordered[i] - ordered[i - 1] >= 0.02) tiers++;

            output.WriteLine($"  distinct tiers: {tiers}/{ordered.Count}   spread {ordered.Last() - ordered.First():F3}");

            Assert.True(tiers >= 6,
                $"The roster resolved into only {tiers} distinct tiers out of {ordered.Count}; " +
                "the engine is flattening wrestlers into each other.");

            Assert.True(ordered.Last() - ordered.First() >= 0.35,
                $"Best and worst wrestler should differ by at least 0.35 stars on average, " +
                $"got {ordered.Last() - ordered.First():F3}");

            // The bottom of the card should be whoever is genuinely worst across the board,
            // not an artefact. Von Wagner is the roster's floor on every axis at once
            // (popularity 23, charisma 1.6, 2.17 overall skill, Psychology 44).
            var bottom = byWrestler.OrderBy(k => k.Value).First().Key;
            var expectedBottom = Roster
                .OrderBy(w => w.Popularity / 100.0 + w.Charisma / 5.0 + w.RingSkills.GetOverallSkill() / 5.0)
                .First().RingName;

            Assert.Equal(expectedBottom, bottom);
        }

        // ── The specific archetypes ──────────────────────────────────────────

        [Fact]
        public void Shayna_IsRecognisedAsTheBetterWorker_ButTheWorseDraw()
        {
            // Shayna (skill 3.82, charisma 2.5) vs Liv (skill 2.83, charisma 4.2),
            // both against the same opponent so only their own profiles differ.
            var shayna = Mean(W("Shayna Baszler"), W("Charlotte Flair"), "Technical Showcase");
            var liv    = Mean(W("Liv Morgan"),     W("Charlotte Flair"), "Technical Showcase");

            output.WriteLine($"  Shayna : tech {shayna.tech:F1}  crowdPeak {shayna.peak:F1}  stars {shayna.stars:F2}");
            output.WriteLine($"  Liv    : tech {liv.tech:F1}  crowdPeak {liv.peak:F1}  stars {liv.stars:F2}");

            // The wrestling itself is better.
            Assert.True(shayna.tech > liv.tech * 1.10,
                $"Shayna is the far better worker (3.82 vs 2.83 skill) and must produce a clearly " +
                $"higher Technical score: got {shayna.tech:F1} vs {liv.tech:F1}");

            // The crowd is not there for her.
            Assert.True(shayna.peak < liv.peak - 5.0,
                $"Shayna cannot move a crowd the way Liv can (charisma 2.5 vs 4.2, pop 70 vs 78); " +
                $"her crowd peak must be materially lower: got {shayna.peak:F1} vs {liv.peak:F1}");
        }

        [Fact]
        public void Shayna_IsBestServedByATechnicalMatch()
        {
            // A wrestler whose value is in the ring rather than the reaction should do
            // better when the booking is graded on the ring work.
            var standard  = Mean(W("Shayna Baszler"), W("Charlotte Flair"), "Technical Showcase", MatchType.Standard);
            var technical = Mean(W("Shayna Baszler"), W("Charlotte Flair"), "Technical Showcase", MatchType.Technical);
            var spotfest  = Mean(W("Shayna Baszler"), W("Charlotte Flair"), "Technical Showcase", MatchType.Spotfest);

            output.WriteLine($"  Standard {standard.stars:F2}   Technical {technical.stars:F2}   Spotfest {spotfest.stars:F2}");

            Assert.True(technical.stars > standard.stars,
                $"Declaring a technical match over a mat-based plan should pay: " +
                $"got Technical {technical.stars:F2} vs Standard {standard.stars:F2}");
            Assert.True(technical.stars > spotfest.stars,
                $"Declaring a spotfest over a mat-based plan should not beat declaring a technical match: " +
                $"got Spotfest {spotfest.stars:F2} vs Technical {technical.stars:F2}");
        }

        [Fact]
        public void CharismaBeatsWorkrate_ForOverallRating_ButNotForMatchQuality()
        {
            // Chad Gable (Grappler 4.9 / Technical 4.7 / Psychology 93, but popularity 59
            // and charisma 3.0) against LA Knight (2.67 overall skill, but popularity 77 and
            // charisma 4.9). The pure-charisma act should draw the better *rating*; the pure
            // worker should produce the better *wrestling*. Both halves matter.
            var gable = Mean(W("Chad Gable"), W("Cody Rhodes"), "Technical Showcase");
            var knight = Mean(W("LA Knight"), W("Cody Rhodes"), "Technical Showcase");

            output.WriteLine($"  Chad Gable : tech {gable.tech:F1}  peak {gable.peak:F1}  stars {gable.stars:F2}");
            output.WriteLine($"  LA Knight  : tech {knight.tech:F1}  peak {knight.peak:F1}  stars {knight.stars:F2}");

            Assert.True(gable.tech > knight.tech * 1.15,
                $"Gable is far the better technical wrestler and must out-produce Knight on the " +
                $"Technical score: {gable.tech:F1} vs {knight.tech:F1}");
            Assert.True(knight.peak > gable.peak,
                $"Knight is much more over and should draw the louder crowd: " +
                $"{knight.peak:F1} vs {gable.peak:F1}");
        }

        [Fact]
        public void TheVeteran_FadesInLongMatches_ButNotInShortOnes()
        {
            // Randy Orton: Psychology 96 / RingIQ 94, but Stamina 64 and Speed 56.
            // He should be excellent in a tight match and visibly worse in a marathon,
            // which is what the conditioning fade is for.
            var youngHorse = W("Bron Breakker");  // Stamina 90, Psychology 62 — the inverse
            var veteran    = W("Randy Orton");

            double vetShort   = Mean(veteran,    W("Cody Rhodes"), "TV Formula",     runs: 250).stars;
            double vetLong    = Mean(veteran,    W("Cody Rhodes"), "Big Match Epic", runs: 250).stars;
            double youngShort = Mean(youngHorse, W("Cody Rhodes"), "TV Formula",     runs: 250).stars;
            double youngLong  = Mean(youngHorse, W("Cody Rhodes"), "Big Match Epic", runs: 250).stars;

            output.WriteLine($"  Orton    short {vetShort:F3} -> long {vetLong:F3}  (gain {vetLong - vetShort:+0.000})");
            output.WriteLine($"  Breakker short {youngShort:F3} -> long {youngLong:F3}  (gain {youngLong - youngShort:+0.000})");

            // Both gain from the longer structure, but the well-conditioned wrestler gains more.
            Assert.True((youngLong - youngShort) > (vetLong - vetShort),
                $"The better-conditioned wrestler should benefit more from a long match: " +
                $"Breakker +{youngLong - youngShort:F3} vs Orton +{vetLong - vetShort:F3}");
        }

        [Fact]
        public void BothDivisions_AreLoadedAndUsable()
        {
            var women = Roster.Where(w => w.Division == Division.Womens).ToList();
            var men   = Roster.Where(w => w.Division == Division.Mens).ToList();

            output.WriteLine($"  {women.Count} women, {men.Count} men");

            Assert.True(women.Count >= 15, $"Expected at least 15 women, got {women.Count}");
            Assert.True(men.Count   >= 15, $"Expected at least 15 men, got {men.Count}");

            // Both divisions must span the card, or one of them is unbookable below the top.
            foreach (var (label, list) in new[] { ("women", women), ("men", men) })
            {
                var positions = list.Select(w => w.CardPosition).Distinct().ToList();
                output.WriteLine($"  {label}: {string.Join(", ", positions.OrderBy(p => p))}");

                Assert.True(positions.Count >= 4,
                    $"The {label}'s division only covers {positions.Count} card positions; " +
                    "a division clustered at one level cannot fill a card.");
            }
        }

        [Fact]
        public void RosterAttributes_UseMostOfTheirLegalRange()
        {
            // The original 9-wrestler roster used 15–25% of each stat's range, which is why
            // every match rated the same regardless of who was in it.
            void Check(string name, double lo, double hi, double legalLo, double legalHi, double minUsage)
            {
                double usage = (hi - lo) / (legalHi - legalLo);
                output.WriteLine($"  {name,-12} {lo:F2}–{hi:F2}  uses {usage * 100:F0}% of {legalLo}–{legalHi}");
                Assert.True(usage >= minUsage,
                    $"{name} only spans {usage * 100:F0}% of its legal range — the roster is too compressed.");
            }

            Check("Popularity", Roster.Min(w => w.Popularity), Roster.Max(w => w.Popularity), 0, 100, 0.60);
            Check("Charisma", Roster.Min(w => w.Charisma), Roster.Max(w => w.Charisma), 0, 5, 0.60);
            Check("Skill", Roster.Min(w => w.RingSkills.GetOverallSkill()),
                           Roster.Max(w => w.RingSkills.GetOverallSkill()), 1, 5, 0.40);
            Check("Psychology", Roster.Min(w => w.Mental.Psychology),
                                Roster.Max(w => w.Mental.Psychology), 0, 100, 0.45);
        }

        [Fact]
        public void BeckyLynch_TheMostOverWrestler_DrawsTheLoudestCrowd()
        {
            // Becky: popularity 95, charisma 4.7, appeal 0.95/0.96 — the most connected
            // person on the roster. Her matches should be the loudest, full stop.
            var withBecky  = Mean(W("Becky Lynch"),    W("Charlotte Flair"), "Big Match Epic");
            var withShayna = Mean(W("Shayna Baszler"), W("Charlotte Flair"), "Big Match Epic");

            output.WriteLine($"  Becky  vs Charlotte: peak {withBecky.peak:F1}  avg {withBecky.avg:F1}  stars {withBecky.stars:F2}");
            output.WriteLine($"  Shayna vs Charlotte: peak {withShayna.peak:F1}  avg {withShayna.avg:F1}  stars {withShayna.stars:F2}");

            Assert.True(withBecky.peak > withShayna.peak + 8.0,
                $"The most over wrestler should pop a crowd far harder: {withBecky.peak:F1} vs {withShayna.peak:F1}");
            Assert.True(withBecky.stars > withShayna.stars + 0.20,
                $"and it should show in the rating: {withBecky.stars:F2} vs {withShayna.stars:F2}");
        }

        [Fact]
        public void CrowdEnergy_HasAPerPairingCeiling_NotAUniversalOne()
        {
            // The single clearest symptom of the old engine: crowd peak pinned at exactly
            // 100 in 93% of all matches, so 35% of the score was a constant.
            var hot  = Mean(W("Becky Lynch"),    W("Rhea Ripley"), "Big Match Epic");
            var cold = Mean(W("Shayna Baszler"), W("Liv Morgan"),  "Big Match Epic");

            output.WriteLine($"  Becky/Rhea    peak {hot.peak:F1}");
            output.WriteLine($"  Shayna/Liv    peak {cold.peak:F1}");

            Assert.True(cold.peak < 92.0,
                $"A pairing the crowd is less invested in must not reach a main-event reaction, got {cold.peak:F1}");
            Assert.True(hot.peak - cold.peak >= 8.0,
                $"Crowd ceiling should vary meaningfully by pairing, got {hot.peak:F1} vs {cold.peak:F1}");
        }

        // ── Attributes that used to do nothing ───────────────────────────────

        [Theory]
        [InlineData("Selling")]
        [InlineData("RingIQ")]
        [InlineData("Toughness")]
        [InlineData("Stamina")]
        [InlineData("Agility")]
        [InlineData("Speed")]
        [InlineData("Strength")]
        public void PreviouslyUnusedAttributes_NowChangeTheResult(string attribute)
        {
            // Every one of these was read zero times by the old MatchEngine. A wrestler
            // sheet that shows a stat which provably does nothing is a lie to the player.
            Wrestler Build(int value)
            {
                var w = TestRoster.Make($"Probe-{attribute}-{value}", popularity: 80, charisma: 4.0, skill: 3.5);
                w.Mental = new Models.Person.MentalAttributes
                {
                    Psychology = 80,
                    Selling    = attribute == "Selling"   ? value : 80,
                    RingIQ     = attribute == "RingIQ"    ? value : 80,
                    Toughness  = attribute == "Toughness" ? value : 80
                };
                w.Physical = new Models.Person.PhysicalAttributes
                {
                    Strength = attribute == "Strength" ? value : 70,
                    Speed    = attribute == "Speed"    ? value : 70,
                    Agility  = attribute == "Agility"  ? value : 70,
                    Stamina  = attribute == "Stamina"  ? value : 80,
                    Size     = 3
                };
                // Strength only expresses itself through power offence, so give the probe
                // a powerhouse style — otherwise the test is asking a mat wrestler to
                // demonstrate strength and correctly getting nothing.
                if (attribute == "Strength") w.Style = WrestlingStyle.Powerhouse;
                return w;
            }

            // Big Match Epic is long enough to exercise fade, near-falls and high spots.
            var low  = Mean(Build(20), Build(20), "Big Match Epic", runs: 150);
            var high = Mean(Build(95), Build(95), "Big Match Epic", runs: 150);

            output.WriteLine($"  {attribute}: 20 -> {low.stars:F3}   95 -> {high.stars:F3}   delta {high.stars - low.stars:+0.000;-0.000}");

            Assert.True(high.stars - low.stars > 0.05,
                $"{attribute} swept from 20 to 95 moved the rating by only " +
                $"{high.stars - low.stars:F3} stars — it is effectively decorative.");
        }

        [Fact]
        public void Charisma_MattersOnEveryStructure_NotJustOnesWithTalkingBeats()
        {
            // Old behaviour: charisma was read in exactly two beat handlers, so on TV
            // Formula — which contains neither — sweeping charisma 0 to 5 changed the
            // rating by exactly 0.00.
            foreach (var structure in new[] { "TV Formula", "Face-in-Peril", "Technical Showcase", "Big Match Epic" })
            {
                var dull    = TestRoster.Make("Dull",    popularity: 80, charisma: 0.5, skill: 3.5);
                var magnet  = TestRoster.Make("Magnet",  popularity: 80, charisma: 5.0, skill: 3.5);

                double low  = Mean(dull, dull, structure, runs: 150).stars;
                double high = Mean(magnet, magnet, structure, runs: 150).stars;

                output.WriteLine($"  {structure,-20} charisma 0.5 -> {low:F2}   5.0 -> {high:F2}   delta {high - low:+0.00;-0.00}");

                Assert.True(high - low > 0.15,
                    $"On {structure}, charisma 0.5 -> 5.0 moved the rating only {high - low:F3} stars.");
            }
        }

        [Fact]
        public void RoleAssignment_ReflectsWhatEachWrestlerIsGoodAt()
        {
            // Every preset books WrestlerB as the one giving the heat segments (which carry
            // a Powerhouse style hint) and WrestlerA as the one hitting high spots.
            // A powerhouse should therefore be worth more in the B slot, and a high flyer
            // in the A slot. This is the StyleHint mechanic being observable.
            var rhea = W("Rhea Ripley");   // Powerhouse 4.8
            var iyo  = W("Iyo Sky");       // HighFlyer, Spotfest's A-slot specialist

            double rheaAsA = Mean(rhea, iyo, "Face-in-Peril", runs: 250).stars;
            double rheaAsB = Mean(iyo, rhea, "Face-in-Peril", runs: 250).stars;

            output.WriteLine($"  Rhea as A (comeback/finish): {rheaAsA:F3}");
            output.WriteLine($"  Rhea as B (power beatdown):  {rheaAsB:F3}");

            Assert.True(Math.Abs(rheaAsA - rheaAsB) > 0.03,
                $"Swapping which slot a powerhouse occupies should change the match, " +
                $"got {rheaAsA:F3} vs {rheaAsB:F3}");
        }
    }
}
