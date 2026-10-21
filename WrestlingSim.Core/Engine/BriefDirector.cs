using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>One phase of a generated match, with the beat that ended up filling it.</summary>
    public sealed class WrittenPhase
    {
        public required MatchPhase Phase { get; init; }
        public required MatchBeat  Beat  { get; init; }
        public required string     Why   { get; init; }

        /// <summary>The template's name, for the review sheet.</summary>
        public required string Name { get; init; }
    }

    /// <summary>
    /// A match written from a brief: the phases, the beats, and why each one is there.
    /// </summary>
    public sealed class WrittenMatch
    {
        public required IReadOnlyList<WrittenPhase> Phases { get; init; }

        public IReadOnlyList<MatchBeat> Beats => Phases.Select(p => p.Beat).ToList();

        public int Minutes => Phases.Sum(p => p.Beat.DurationMinutes);
    }

    /// <summary>
    /// Fills the shape <see cref="PhaseGrammar"/> produced with actual beats.
    ///
    /// **The split matters.** The grammar is deterministic: a face-in-peril big match is
    /// always opening, shine, two cycles, two near falls, finish. What fills those slots is
    /// not, which is how the same brief gives a different match every time while still being
    /// the match you asked for. The old structure library had no such split, so booking
    /// Face-in-Peril twice gave the identical eleven beats twice.
    ///
    /// Selection is weighted rather than uniform, on three things:
    ///
    /// - **The story**, which is the strongest pull. A grudge wants a ringside brawl where a
    ///   technical exhibition wants a wear-down hold, and both are heat-section beats.
    /// - **The wrestler's style**, so a powerhouse is not handed an aerial spot. Doc 18 §4 is
    ///   direct about this: a move only works on the body that can do it.
    /// - **What has already been used**, so a seventeen-beat epic does not run the same heat
    ///   segment three times. This is worth real points — <c>VarietyNudge</c> already scores
    ///   a plan on how many distinct beat types it uses.
    ///
    /// Seeded off the brief so a given brief is reproducible, and off
    /// <see cref="MatchBrief.Draft"/> so the re-roll button genuinely re-rolls.
    /// </summary>
    public static class BriefDirector
    {
        /// <summary>
        /// Writes the match.
        /// </summary>
        /// <param name="brief">What the booker asked for.</param>
        /// <param name="sides">The sides, in plan order. Their styles steer beat choice.</param>
        public static WrittenMatch Write(MatchBrief brief, IReadOnlyList<MatchSide> sides)
        {
            int sideSize = sides.Count == 0 ? 1 : sides.Max(s => s.Members.Count);
            var shape  = PhaseGrammar.Shape(brief, sides.Count, sideSize);
            var rand   = new Random(SeedFor(brief, sides));
            var used   = new List<BeatType>();
            var phases = new List<WrittenPhase>(shape.Count);

            foreach (var slot in shape)
            {
                var template = Choose(slot, brief, sides, used, rand, sideSize);
                used.Add(template.Type);

                // The slot's intensity and duration win over the template's defaults. The
                // grammar knows where in the match this sits and the template does not —
                // a heat segment in the second cycle of an epic is a different thing from
                // the same beat opening a television match.
                var beat = template.ToMatchBeat(slot.Control, slot.Intensity, slot.Duration);

                // Who it is aimed at, where the grammar knows and the beat cannot guess.
                if (slot.Against is { } target && target != slot.Control) beat.Against = target;

                // Which partner comes in, for the beats that rotate a side.
                if (slot.Incoming is { } partner) beat.IncomingIndex = partner;

                phases.Add(new WrittenPhase
                {
                    Phase = slot.Phase,
                    Beat  = beat,
                    Why   = slot.Why,
                    Name  = template.Name
                });
            }

            return new WrittenMatch { Phases = phases };
        }

        /// <summary>
        /// Reproducible for a given brief and cast, and different for a different draft.
        ///
        /// <c>StableSeed</c> rather than <c>HashCode.Combine</c>, which .NET randomises per
        /// process — a generated match would then be different every time the app started,
        /// and a saved plan would not regenerate.
        /// </summary>
        private static int SeedFor(MatchBrief brief, IReadOnlyList<MatchSide> sides) =>
            StableSeed.From(
                brief.Story.ToString(), brief.Length.ToString(), brief.Finish.ToString(),
                brief.WinningSide.ToString(), brief.Draft.ToString(),
                string.Join("|", sides.SelectMany(s => s.Members).Select(w => w.Id)));

        // ── Choosing a beat ──────────────────────────────────────────────────

        private static BeatTemplate Choose(
            PhaseSlot slot, MatchBrief brief, IReadOnlyList<MatchSide> sides,
            IReadOnlyList<BeatType> used, Random rand, int sideSize)
        {
            var pool = Pool(slot.Phase, brief, sideSize).ToList();

            // Never an empty pool. A phase with nothing legal in it is a grammar bug and
            // should surface as one rather than as a silently dropped beat.
            if (pool.Count == 0)
                throw new InvalidOperationException(
                    $"No beat template fits phase {slot.Phase} in a {brief.Story} match.");

            var worker = WorkerOf(slot.Control, sides);

            var weighted = pool
                .Select(t => (Template: t, Weight: Weigh(t, slot, brief, worker, used)))
                .Where(x => x.Weight > 0)
                .ToList();

            if (weighted.Count == 0) return pool[rand.Next(pool.Count)];

            double roll = rand.NextDouble() * weighted.Sum(x => x.Weight);
            foreach (var (template, weight) in weighted)
            {
                roll -= weight;
                if (roll <= 0) return template;
            }
            return weighted[^1].Template;
        }

        /// <summary>Which beat types can fill a phase at all.</summary>
        private static IEnumerable<BeatTemplate> Pool(MatchPhase phase, MatchBrief brief, int sideSize)
        {
            var types = TypesFor(phase, brief, sideSize);
            return BeatLibrary.All.Where(t => types.Contains(t.Type));
        }

        /// <summary>
        /// Which beat types can fill a phase at all.
        ///
        /// **Side size changes the furniture, not the skeleton.** A tag match's heat is an
        /// isolation because what is being denied is a corner rather than a comeback; its
        /// hope spot is a near tag; its comeback is a hot tag. Doc 18 §2.3's stages are the
        /// same stages, which is the argument for one grammar rather than a second structure
        /// library for tags.
        ///
        /// The exception is lucha, and doc 25 §3.3 is explicit that it is a different match
        /// rather than a variant: three a side is the *default* there, the rules allow
        /// constant motion, and there is no long isolation to deny anybody. So a spectacle
        /// worked by teams keeps the singles furniture and gets the tags instead.
        /// </summary>
        private static IReadOnlyList<BeatType> TypesFor(MatchPhase phase, MatchBrief brief, int sideSize = 1)
        {
            bool tag   = sideSize > 1;
            bool lucha = tag && brief.Story == MatchStory.Spectacle;

            if (tag && !lucha)
            {
                switch (phase)
                {
                    case MatchPhase.Heat:     return [BeatType.Isolation];
                    case MatchPhase.HopeSpot: return [BeatType.NearTag];
                    case MatchPhase.Comeback: return [BeatType.HotTag];
                }
            }

            // A lucha trios has no isolation and no hot tag to build to, so its "heat" is
            // the other team simply having the better of it.
            if (lucha && phase == MatchPhase.Heat) return [BeatType.DoubleTeam, BeatType.HighSpot];

            switch (phase)
            {
                case MatchPhase.Rotation: return [BeatType.Tag, BeatType.BlindTag];
                case MatchPhase.Tandem:   return [BeatType.DoubleTeam];
                case MatchPhase.AllFour:  return [BeatType.AllFourBrawl];
                case MatchPhase.Save:     return [BeatType.SaveBreakup];
            }

            return SinglesTypesFor(phase, brief);
        }

        private static IReadOnlyList<BeatType> SinglesTypesFor(MatchPhase phase, MatchBrief brief) => phase switch
        {
            MatchPhase.Opening => brief.Story switch
            {
                MatchStory.Grudge or MatchStory.Spectacle => [BeatType.HotOpening],
                MatchStory.TechnicalExhibition            => [BeatType.SlowOpening],
                // A big match opens slowly. There is time to establish and the form uses
                // it — and it keeps the running time deterministic, because the opening is
                // the one slot whose duration comes from the template rather than the
                // grammar. Two templates with different defaults would make a big match
                // twenty-three or twenty-five minutes depending on a coin flip.
                _ when brief.Length is MatchScale.BigMatch or MatchScale.Epic
                                                          => [BeatType.SlowOpening],
                _                                         => [BeatType.StandardOpening, BeatType.HotOpening]
            },

            // A spectacle's "shine" is a high spot, because that is what a spectacle has
            // instead of a shine.
            MatchPhase.Shine => brief.Story == MatchStory.Spectacle
                ? [BeatType.HighSpot]
                : [BeatType.Shine],

            // A grudge does not cut somebody off so much as drag them into the crowd.
            MatchPhase.Cutoff => brief.Story == MatchStory.Grudge
                ? [BeatType.Cutoff, BeatType.CrowdBrawl]
                : [BeatType.Cutoff],

            MatchPhase.Heat => brief.Story switch
            {
                MatchStory.Grudge    => [BeatType.HeatSegment, BeatType.CrowdBrawl],
                MatchStory.Spectacle => [BeatType.HeatSegment, BeatType.HighSpot],
                _                    => [BeatType.HeatSegment]
            },

            MatchPhase.HopeSpot => brief.Story == MatchStory.Grudge
                ? [BeatType.HopeSpot, BeatType.RevengeSpot]
                : [BeatType.HopeSpot],

            MatchPhase.Rest        => [BeatType.RestHold],

            MatchPhase.Escalation  => brief.Story == MatchStory.Grudge
                ? [BeatType.FeudalEscalation, BeatType.PsychologicalWarfare]
                : [BeatType.PsychologicalWarfare],

            MatchPhase.Comeback    => [BeatType.Comeback],

            MatchPhase.Stretch => brief.Story == MatchStory.Spectacle
                ? [BeatType.NearFall, BeatType.HighSpot]
                : [BeatType.NearFall],


            MatchPhase.Outside     => [BeatType.ThirdPartyPullIn],

            MatchPhase.Alliance    => [BeatType.Alliance],
            MatchPhase.Betrayal    => [BeatType.Betrayal],
            MatchPhase.Disposal    => [BeatType.DisposalSpot],

            // Coming back in is the third man breaking up what the other two were doing.
            // Not a new beat type: the pin break *is* the return, and inventing a second
            // beat that means "he is back" would be a beat with nothing to do.
            MatchPhase.Return      => [BeatType.PinBreak],

            MatchPhase.Finish      => [FinishType(brief.Finish)],

            _                      => []
        };

        /// <summary>
        /// Every beat type a story can produce, across every phase and every length.
        ///
        /// One definition of what belongs in a story, shared by the generator and by the
        /// critique. The first version of the critique carried its own hand-written list of
        /// what was out of place, and the two disagreed immediately: a grudge is given a rest
        /// hold and a shine by the grammar, and the critique called both of them off-story.
        /// A game that flags the beats it just wrote for you is contradicting itself.
        ///
        /// Finishes are always in, whatever the story. Which one to use is named by the brief
        /// outright and checked separately, so counting it here would be marking the same
        /// thing twice.
        /// </summary>
        public static IReadOnlySet<BeatType> TypesWithin(MatchStory story)
        {
            var types = new HashSet<BeatType>();

            foreach (MatchScale scale in Enum.GetValues<MatchScale>())
            foreach (int sideSize in new[] { 1, 2, 3 })
            {
                var brief = new MatchBrief { Story = story, Length = scale };
                foreach (MatchPhase phase in Enum.GetValues<MatchPhase>())
                    foreach (var type in TypesFor(phase, brief, sideSize))
                        types.Add(type);
            }

            foreach (FinishKind finish in Enum.GetValues<FinishKind>())
                types.Add(FinishType(finish));

            return types;
        }

        /// <summary>The finish the booker asked for, as the beat that produces it.</summary>
        public static BeatType FinishType(FinishKind finish) => finish switch
        {
            FinishKind.Clean            => BeatType.FinishClean,
            FinishKind.Dominant         => BeatType.FinishSuperFinisher,
            FinishKind.Submission       => BeatType.FinishSubmission,
            FinishKind.Stolen           => BeatType.FinishRollup,
            FinishKind.Interference     => BeatType.FinishInterference,
            FinishKind.Disqualification => BeatType.FinishDQ,
            FinishKind.CountOut         => BeatType.FinishCountout,
            _                           => BeatType.FinishClean
        };

        // ── Weighting ────────────────────────────────────────────────────────

        private static double Weigh(
            BeatTemplate template, PhaseSlot slot, MatchBrief brief,
            Wrestler? worker, IReadOnlyList<BeatType> used)
        {
            double weight = 1.0;

            // Story fit. The strongest pull, because it is what the booker asked for.
            weight *= StoryFit(template, brief.Story);

            // Whose body is doing it.
            if (worker is not null) weight *= StyleFit(template, worker.Style);

            // Repetition. Not a ban — a long match legitimately runs two heat segments —
            // but a heavy discount, so the third one only happens if nothing else fits.
            int seen = used.Count(t => t == template.Type);
            if (seen > 0) weight *= Math.Pow(0.35, seen);

            // A beat the feud cannot carry is not a candidate. The plan validator would
            // reject it later, and a generator that produces invalid plans is worse than
            // one that produces dull ones.
            if (template.RequiredFeudIntensity > FeudIntensity.None) weight = 0;

            return weight;
        }

        /// <summary>
        /// How much a template suits the story, off the tags and the style hint the library
        /// already carries.
        ///
        /// Read off tags rather than a per-template table so that adding a beat to the
        /// library puts it in circulation without touching this file. The failure mode of
        /// the old structure library was that a new beat had to be hand-written into a
        /// structure or no booker ever saw it, which is how `Hope Spot` shipped reachable
        /// only from three singles structures and from none of the multi-man ones.
        ///
        /// **This dominates <see cref="StyleFit"/>, and the order matters.** The first
        /// version had them the other way round, and a technical exhibition worked by a
        /// powerhouse drew Power Beatdown for its heat — style ×2.5 beat story ×3.0 ÷ 2.5.
        /// That is the generator quietly overruling the booker. If a powerhouse cannot
        /// carry the technical match you asked for, the right place to say so is the score
        /// and the expectation reading, not a substituted beat you never chose.
        /// </summary>
        private static double StoryFit(BeatTemplate template, MatchStory story)
        {
            bool Has(string tag) => template.Tags.Contains(tag);
            bool Worked(WrestlingStyle style) => template.StyleHint == style;

            return story switch
            {
                MatchStory.TechnicalExhibition =>
                    Worked(WrestlingStyle.Technical) || Has("Technical") ? 4.0
                  : Has("Slow")                                          ? 2.0
                  : Has("Brawling") || Has("Chaos") || Has("Fast")        ? 0.2
                  : 1.0,

                MatchStory.Grudge =>
                    Has("Feud") || Has("Brawling") || Has("Chaos")        ? 4.0
                  : Worked(WrestlingStyle.Brawler) || Has("Emotional")    ? 2.5
                  : Has("Technical") || Has("Slow")                       ? 0.2
                  : 1.0,

                MatchStory.Spectacle =>
                    Has("Aerial") || Has("Risky")                         ? 4.0
                  : Has("Exciting") || Has("Fast")                        ? 2.5
                  : Has("Slow") || Has("Technical")                       ? 0.2
                  : 1.0,

                MatchStory.DavidAndGoliath =>
                    Worked(WrestlingStyle.Powerhouse) || Has("Power")     ? 4.0
                  : Has("Physical") || Has("Impact")                      ? 2.0
                  : Has("Technical")                                      ? 0.35
                  : 1.0,

                MatchStory.Showcase =>
                    Has("Dominant") || Has("Power") || Has("Physical")    ? 3.0
                  : Has("Emotional")                                      ? 0.35
                  : 1.0,

                MatchStory.FaceInPeril =>
                    Has("Emotional") || Has("Story")                      ? 2.5
                  : Has("Slow") || Has("Physical")                        ? 1.5
                  : 1.0,

                // An even contest has no thumb on the scale, which is the point of it.
                _ => 1.0
            };
        }

        /// <summary>
        /// Whether this body can do this beat.
        ///
        /// A discount rather than a ban, because a powerhouse *can* go to the top rope and
        /// occasionally does, and a model that says never is more wrong than one that says
        /// rarely. The template's <c>StyleHint</c> is the honest signal: it already exists
        /// to say "this beat is worked as power" regardless of who is working it.
        ///
        /// Deliberately a narrow range. This modulates the story's choice; it does not make
        /// it. A wide range here means the cast quietly rewrites the booking.
        /// </summary>
        private static double StyleFit(BeatTemplate template, WrestlingStyle style)
        {
            if (template.StyleHint is not { } hint) return 1.0;
            if (hint == style) return 1.5;

            return Family(hint) == Family(style) ? 1.1 : Adjacent(hint, style) ? 0.8 : 0.4;
        }

        /// <summary>
        /// What a style is fundamentally doing, for judging how far a beat is from the body
        /// working it.
        ///
        /// The first version listed four pairs by hand and gave everything else the same
        /// 0.85, which meant most of the roster was interchangeable to the generator: a
        /// technician and a powerhouse handed the same brief drew the identical sheet,
        /// because every weight in it was the same number. A style that never changes an
        /// outcome is decoration.
        /// </summary>
        private static StyleFamily Family(WrestlingStyle style) => style switch
        {
            WrestlingStyle.Powerhouse => StyleFamily.Force,
            WrestlingStyle.Brawler    => StyleFamily.Force,
            WrestlingStyle.Striker    => StyleFamily.Strikes,
            WrestlingStyle.HighFlyer  => StyleFamily.Air,
            WrestlingStyle.Technical  => StyleFamily.Mat,
            WrestlingStyle.Grappler   => StyleFamily.Mat,
            _                         => StyleFamily.Mat
        };

        private enum StyleFamily { Force, Strikes, Air, Mat }

        /// <summary>
        /// Families that share something. Striking sits between force and the air; the mat
        /// and the air are the two that have nothing to say to each other, which is why a
        /// technician doing a shooting star and a high flyer doing a wear-down hold are both
        /// the beat looking wrong on the body.
        /// </summary>
        private static bool Adjacent(WrestlingStyle a, WrestlingStyle b)
        {
            var (x, y) = (Family(a), Family(b));
            return (x, y) is (StyleFamily.Force, StyleFamily.Strikes)
                          or (StyleFamily.Strikes, StyleFamily.Force)
                          or (StyleFamily.Strikes, StyleFamily.Air)
                          or (StyleFamily.Air, StyleFamily.Strikes)
                          or (StyleFamily.Mat, StyleFamily.Force)
                          or (StyleFamily.Force, StyleFamily.Mat);
        }

        /// <summary>Whoever is working this beat, or null when it is worked evenly.</summary>
        private static Wrestler? WorkerOf(BeatControl control, IReadOnlyList<MatchSide> sides)
        {
            int side = control switch
            {
                BeatControl.WrestlerA => 0,
                BeatControl.WrestlerB => 1,
                BeatControl.SideC     => 2,
                _                     => -1
            };

            return side >= 0 && side < sides.Count ? sides[side].Members.FirstOrDefault() : null;
        }
    }
}
