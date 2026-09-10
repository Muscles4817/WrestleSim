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
    public readonly record struct BriefPreset(
        string Name, string Description, MatchBrief Brief,
        int MinimumSides = 2, AllianceHint Alliance = AllianceHint.None);

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
                new MatchBrief { Story = MatchStory.DavidAndGoliath, Length = MatchScale.Workhorse, Finish = FinishKind.Clean })
        ];

        /// <summary>The ones that make sense for a match of this shape.</summary>
        public static IEnumerable<BriefPreset> For(int sideCount) =>
            All.Where(p => sideCount >= p.MinimumSides);

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
        public static string? Matching(MatchBrief brief) =>
            All.FirstOrDefault(p => p.Brief.Story  == brief.Story
                                 && p.Brief.Length == brief.Length
                                 && p.Brief.Finish == brief.Finish).Name;

        /// <summary>
        /// The one a booker most likely wants, given what the match already promises.
        ///
        /// Offered rather than applied. A suggestion that picks itself is a decision taken
        /// away from the player, and the whole point of deriving the expectation is to inform
        /// the booking rather than to make it.
        /// </summary>
        public static string SuggestedFor(Expectation promised) => promised.Wants switch
        {
            MatchStory.Grudge              => "Grudge Brawl",
            MatchStory.TechnicalExhibition => "Technical Showcase",
            MatchStory.Spectacle           => "Spotfest",
            MatchStory.DavidAndGoliath     => "Giant Killer",
            _                              => "Face-in-Peril"
        };
    }
}
