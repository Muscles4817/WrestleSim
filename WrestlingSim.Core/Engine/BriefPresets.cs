using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Whether a preset wants an alliance, expressed as a rule rather than as side indices.
    ///
    /// A preset cannot name sides — it does not know who is in the match, and the same chip
    /// has to work on any three-way. So it says what *kind* of alliance the shape wants and
    /// it is resolved against the cast at the point of use.
    /// </summary>
    public enum AllianceHint
    {
        /// <summary>Nobody teams up. Correct for every singles preset.</summary>
        None,

        /// <summary>
        /// The two who are not booked to win work the winner over, and it breaks. The
        /// archetypal three-way: two of them agree the favourite is the problem, right up
        /// until one of them decides the other is.
        /// </summary>
        AgainstTheWinner,

        /// <summary>The same, and it holds. Two of them simply beat the third.</summary>
        AgainstTheWinnerAndHolds
    }

    /// <summary>A named starting point for a brief, and what it is for.</summary>
    /// <param name="MinimumSides">How many corners the shape needs. Three for a three-way.</param>
    /// <param name="MinimumPerSide">
    /// How many bodies a side needs. Two for a tag preset, three for a trios one. Separate
    /// from <paramref name="MinimumSides"/> because they are different questions — a trios
    /// match is two sides of three, and gating it on side count would offer it to a
    /// three-way instead.
    /// </param>
    public readonly record struct BriefPreset(
        string Name, string Description, MatchBrief Brief,
        int MinimumSides = 2, AllianceHint Alliance = AllianceHint.None,
        int MinimumPerSide = 1);

    /// <summary>
    /// The old structure library, as briefs.
    ///
    /// **Nothing was lost when the structure picker went.** A booker who wants a standard
    /// television match should not have to answer five questions to get one, and these are
    /// that fast path: one tap fills the brief and every field stays adjustable afterwards.
    /// What changed is that the named list stopped being the *only* way to book, and that
    /// picking one no longer hands you the identical beat sheet it handed you last week.
    ///
    /// The names are kept deliberately. They are what the structures were called, and a
    /// player who learned them should find them where they left them.
    /// </summary>
    public static class BriefPresets
    {
        public static IReadOnlyList<BriefPreset> All { get; } =
        [
            new("TV Formula",
                "The bread-and-butter weekly television match. Short, clean and effective.",
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Television, Finish = FinishKind.Clean }),

            new("Face-in-Peril",
                "The Hogan/Cena formula. Shine, cut-off, a long beating, and a comeback that has been paid for.",
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse, Finish = FinishKind.Clean }),

            new("Technical Showcase",
                "Mat psychology, limb work and a submission payoff. Rewards ring IQ.",
                new MatchBrief { Story = MatchStory.TechnicalExhibition, Length = MatchScale.Workhorse, Finish = FinishKind.Submission }),

            new("Spotfest",
                "High spots from the bell. Aerial work carries the crowd rather than psychology.",
                new MatchBrief { Story = MatchStory.Spectacle, Length = MatchScale.Workhorse, Finish = FinishKind.Clean }),

            new("Grudge Brawl",
                "A hate-filled contest that spills everywhere. Revenge spots and ringside chaos.",
                new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.Workhorse, Finish = FinishKind.Clean }),

            new("Big Match",
                "Two heat and comeback cycles and a real finishing stretch. A pay-per-view semi-main.",
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.BigMatch, Finish = FinishKind.Dominant }),

            new("Epic",
                "Thirty-plus minutes. Needs two people the crowd will watch that long, and the gas to work it.",
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Epic, Finish = FinishKind.Dominant }),

            new("Squash",
                "One-sided on purpose. Somebody is being made to look unbeatable.",
                new MatchBrief { Story = MatchStory.Showcase, Length = MatchScale.Opener, Finish = FinishKind.Dominant }),

            // ── Multi-man ────────────────────────────────────────────────────
            //
            // The library's four multi-man structures were eight beats each, twelve minutes
            // at the longest, and identical in length whoever was in them. These are what
            // they were trying to be.

            new("Triple Threat",
                "Two of them decide the favourite is the problem, until one decides the other is.",
                new MatchBrief { Story = MatchStory.EvenContest, Length = MatchScale.Workhorse, Finish = FinishKind.Clean },
                MinimumSides: 3, Alliance: AllianceHint.AgainstTheWinner),

            new("Three-Way Main Event",
                "The long version. Two cycles, a real finishing stretch, and the alliance breaking in the middle of it.",
                new MatchBrief { Story = MatchStory.EvenContest, Length = MatchScale.BigMatch, Finish = FinishKind.Dominant },
                MinimumSides: 3, Alliance: AllianceHint.AgainstTheWinner),

            new("Stolen Fall",
                "Two of them wreck each other and the third takes it. The finish the format exists for.",
                new MatchBrief { Story = MatchStory.Grudge, Length = MatchScale.Workhorse, Finish = FinishKind.Stolen },
                MinimumSides: 3, Alliance: AllianceHint.AgainstTheWinner),

            new("Two On One",
                "They gang up and stay ganged up. The quieter booking, and the one that makes somebody look unbeatable for surviving it.",
                new MatchBrief { Story = MatchStory.DavidAndGoliath, Length = MatchScale.Workhorse, Finish = FinishKind.Clean },
                MinimumSides: 3, Alliance: AllianceHint.AgainstTheWinnerAndHolds),

            new("Giant Killer",
                "Somebody is giving away a great deal of size, and the match is about whether they survive it.",
                new MatchBrief { Story = MatchStory.DavidAndGoliath, Length = MatchScale.Workhorse, Finish = FinishKind.Clean }),

            // ── Tag and trios ────────────────────────────────────────────────
            //
            // Two of these carry the same brief as a singles preset above, because a tag
            // Face-in-Peril *is* a Face-in-Peril — the shape does not change with the body
            // count, only the furniture does. What changes is the name a booker of that
            // match wants to see, which is why `Matching` prefers the most specific preset
            // the shape allows rather than the first one in this list.

            new("Southern Tag",
                "Cut the ring in half. A long isolation, a corner they keep not reaching, and a hot tag that has been paid for twice over.",
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.Workhorse, Finish = FinishKind.Clean },
                MinimumPerSide: 2),

            new("Tag Sprint",
                "In and out fast, both teams even, nobody isolated long enough to build heat. An opener that gets the crowd moving.",
                new MatchBrief { Story = MatchStory.EvenContest, Length = MatchScale.Opener, Finish = FinishKind.Clean },
                MinimumPerSide: 2),

            new("Six-Man War",
                "Three fresh opponents rotating on one man, twice over, and everybody in before the end.",
                new MatchBrief { Story = MatchStory.FaceInPeril, Length = MatchScale.BigMatch, Finish = FinishKind.Dominant },
                MinimumPerSide: 3),

            new("Lucha Trios",
                "Constant motion. Bodies change instead of the pace dropping, and nobody is kept from a corner long enough to be in peril.",
                new MatchBrief { Story = MatchStory.Spectacle, Length = MatchScale.Workhorse, Finish = FinishKind.Clean },
                MinimumPerSide: 3)
        ];

        /// <summary>The ones that make sense for a match of this shape.</summary>
        public static IEnumerable<BriefPreset> For(int sideCount, int perSide = 1) =>
            All.Where(p => sideCount >= p.MinimumSides && perSide >= p.MinimumPerSide);

        /// <summary>
        /// Which two sides a preset's alliance means, given who is booked to win.
        ///
        /// Null when the preset wants none, or when there are not two people left to form
        /// one — which is every singles match, asked as "are there two others" rather than
        /// as a separate rule about side counts.
        /// </summary>
        public static (int First, int Second)? Resolve(AllianceHint hint, int winningSide, int sideCount)
        {
            if (hint == AllianceHint.None) return null;

            var against = Enumerable.Range(0, sideCount).Where(s => s != winningSide).ToList();
            return against.Count >= 2 ? (against[0], against[1]) : null;
        }

        public static BriefPreset? Find(string name) =>
            All.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Name: not null } hit
                ? hit
                : null;

        /// <summary>
        /// The preset a brief currently matches, or null once it has been adjusted away from
        /// all of them. Used to keep a preset chip lit while it still describes the booking
        /// and to let go of it the moment it does not.
        /// </summary>
        /// <remarks>
        /// Most specific first. Several presets share a brief — a tag Face-in-Peril and a
        /// singles one are the same booking — so the answer has to depend on the match as
        /// well as on the brief, or a booker who taps "Southern Tag" watches the chip light
        /// up over "Face-in-Peril" instead.
        /// </remarks>
        public static string? Matching(MatchBrief brief, int sideCount = 2, int perSide = 1) =>
            For(sideCount, perSide)
                .Where(p => p.Brief.Story  == brief.Story
                         && p.Brief.Length == brief.Length
                         && p.Brief.Finish == brief.Finish)
                .OrderByDescending(p => p.MinimumPerSide)
                .ThenByDescending(p => p.MinimumSides)
                .Select(p => p.Name)
                .FirstOrDefault();

        /// <summary>
        /// The one a booker most likely wants, given what the match already promises.
        ///
        /// Offered rather than applied. A suggestion that picks itself is a decision taken
        /// away from the player, and the whole point of deriving the expectation is to inform
        /// the booking rather than to make it.
        /// </summary>
        public static string SuggestedFor(Expectation promised, int perSide = 1) =>
            (promised.Wants, perSide) switch
            {
                // The tag answers first, so a six-man that promises spectacle is pointed at
                // the lucha preset rather than at a singles spotfest it cannot book.
                (MatchStory.Spectacle,   >= 3) => "Lucha Trios",
                (MatchStory.FaceInPeril, >= 3) => "Six-Man War",

                (MatchStory.Grudge,              _) => "Grudge Brawl",
                (MatchStory.TechnicalExhibition, _) => "Technical Showcase",
                (MatchStory.Spectacle,           _) => "Spotfest",
                (MatchStory.DavidAndGoliath,     _) => "Giant Killer",

                // Southern Tag is the tag match's Face-in-Peril, so it is the fallback and
                // not a story of its own. Written as both, the two arms answered for each
                // other and neither could be shown to matter.
                (_, >= 2) => "Southern Tag",
                _         => "Face-in-Peril"
            };
    }
}
