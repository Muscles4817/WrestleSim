using WrestlingSim.Enums;

namespace WrestlingSim.Models.Rumble
{
    /// <summary>
    /// A battle royal or a Rumble.
    ///
    /// **Not a <see cref="MatchPlan.MatchPlan"/>, and that is the design rather than a
    /// shortcut.** Doc 18 §2.5 calls this format "barely a match: a vehicle for a spectacle,
    /// a surprise return, and one story told in eliminations. Judged on moments, not on
    /// work." Two things follow, and both of them rule out the beat model:
    ///
    ///   • **It cannot be expressed.** `BeatControl` names four sides and `Validate` caps a
    ///     plan at four; a thirty-person field has no way to say who a beat is for. That is
    ///     not a limit to widen — a booker does not write twenty-nine falls, and a format
    ///     where they would have to is a format the beat model is the wrong shape for.
    ///   • **It cannot be graded.** The engine's whole scoring model is how well a match was
    ///     *worked*, and doc 18 says explicitly that this one is not judged that way.
    ///
    /// So what a booker decides here is what a booker actually decides: who is in it, what
    /// order they come out, who wins, and a handful of moments. The sequence in between is
    /// the engine's to generate, because nobody books it in real life either.
    /// </summary>
    public class RumblePlan : ICardItem
    {
        /// <summary>Everybody in it, in the order they come out.</summary>
        public List<RumbleEntrant> Field { get; set; } = new();

        /// <summary>
        /// Seconds between entries. Zero is a battle royal — everybody starts together.
        ///
        /// The one number that separates the two formats, and doc 18 is specific that it is
        /// the difference that matters: "it is the entries that make the Rumble a story
        /// rather than a scramble". <see cref="Engine.RumbleEngine"/> scores that claim
        /// rather than restating it.
        /// </summary>
        public int EntryIntervalSeconds { get; set; } = 90;

        /// <summary>True when everybody starts together — a battle royal, not a Rumble.</summary>
        public bool IsBattleRoyal => EntryIntervalSeconds <= 0;

        /// <summary>
        /// The moments the booker is paying for. The whole of what is bookable here beyond
        /// the field and the winner, because the format is a delivery system for these.
        /// </summary>
        public List<RumbleMoment> Moments { get; set; } = new();

        /// <summary>Who the booker has decided wins it.</summary>
        public Wrestler? Winner { get; set; }

        /// <summary>What is on the line, if anything.</summary>
        public string? Stakes { get; set; }

        // ── ICardItem ────────────────────────────────────────────────────────

        public string Name =>
            Field.Count == 0 ? "Battle Royal"
            : IsBattleRoyal  ? $"{Field.Count}-Wrestler Battle Royal"
                             : $"{Field.Count}-Wrestler Rumble";

        public CardItemKind Kind => CardItemKind.Match;

        /// <summary>
        /// Runtime. A Rumble runs as long as its entries take plus the closing stretch; a
        /// battle royal is short, because with everybody in at once there is nothing to wait
        /// for.
        /// </summary>
        public int DurationMinutes => IsBattleRoyal
            ? Math.Clamp(4 + Field.Count / 4, 5, 20)
            : Math.Clamp((Field.Count * EntryIntervalSeconds) / 60 + 5, 10, 70);

        public IReadOnlyList<Wrestler> Wrestlers => Field.Select(e => e.Wrestler).ToList();

        /// <summary>What is wrong with this booking, in words a booker can act on.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (Field.Count < 4)
                errors.Add($"A battle royal needs a field. This one has {Field.Count}; " +
                           "below four it is a multi-man match, which the match builder books " +
                           "properly and this does not.");

            if (Field.Count != Field.Select(e => e.Wrestler).Distinct().Count())
                errors.Add("Somebody is in this twice.");

            var numbers = Field.Select(e => e.Number).ToList();
            if (numbers.Count != numbers.Distinct().Count())
                errors.Add("Two wrestlers have the same entry number.");

            if (Winner is null)
                errors.Add("Somebody has to win it.");
            else if (!Field.Any(e => e.Wrestler == Winner))
                errors.Add($"{Winner.RingName} wins it without being in it.");

            foreach (var moment in Moments)
                foreach (var w in moment.Cast)
                    if (!Field.Any(e => e.Wrestler == w))
                        errors.Add($"{moment.Kind} involves {w.RingName}, who is not in the match.");

            // A showdown needs two people to show down.
            foreach (var moment in Moments.Where(m => m.Kind == RumbleMomentKind.Showdown))
                if (moment.Cast.Count < 2)
                    errors.Add("A showdown is two rivals meeting in the ring. This one names " +
                               $"{moment.Cast.Count}.");

            return errors;
        }
    }

    /// <summary>One wrestler and the number they come out at.</summary>
    public class RumbleEntrant
    {
        public required Wrestler Wrestler { get; init; }

        /// <summary>
        /// Entry position, 1-based. All 1 in a battle royal, where everybody starts together.
        ///
        /// This is the number the format's best stories are made of — going the distance
        /// from two is a career night, and doc 18 says the entries are what make it a story
        /// rather than a scramble.
        /// </summary>
        public int Number { get; init; } = 1;

        /// <summary>
        /// Nobody knew they were coming. The surprise return doc 18 names as one of the three
        /// things this format is a vehicle for.
        /// </summary>
        public bool IsSurprise { get; init; }
    }
}
