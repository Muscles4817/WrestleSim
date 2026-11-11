using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Which move a wrestler finishes with, and what it is worth.
    ///
    /// **Until this existed, nothing read <see cref="Signature"/> at all.** Every roster
    /// entry carried a named finisher with its own standing, and every finish in the game
    /// said "hits the finisher". The move names were decoration: authored, shipped, and
    /// unreachable from the one moment they describe.
    ///
    /// <see cref="Signature.TagExclusive"/> was the worst of it — a field declared on the
    /// model, documented in its own constructor as meaning "the move can only be competed
    /// as a tag team move", and referenced by nothing. A double-team finish could not be
    /// expressed because no code asked whether a move was one.
    ///
    /// Three rules, and they are all this class is:
    ///
    ///   • **A tag finisher needs a tag match.** A move for two people is unavailable to
    ///     one, always, with no exception for how over it is.
    ///   • **It also needs a team.** Two wrestlers who have never worked together do not
    ///     hit a drilled double-team move on the night, so the side's chemistry gates it —
    ///     which is the first thing in the engine to read
    ///     <see cref="MatchSide.Chemistry"/> at the finish rather than during the match.
    ///   • **A finisher's standing is worth something.** A spear the audience has watched
    ///     end matches for a decade closes harder than a cutter nobody has a feeling about.
    /// </summary>
    public static class FinisherBook
    {
        /// <summary>
        /// The median finisher standing across the shipped roster, so a typical finisher
        /// is worth exactly 1.00 and this change does not quietly re-rate every match in
        /// the game. Same centring discipline as <c>PerformerProfile</c>, and it moves when
        /// the roster does.
        /// </summary>
        public const double TypicalCredibility = 62.0;

        /// <summary>
        /// How much a finisher's own standing swings the finish, per point of normalised
        /// standing away from typical.
        ///
        /// The first draft set this at 0.55, and the test asserting the swing is modest
        /// measured what that actually bought: a **48% spread** between the roster's best
        /// finisher and its worst, which is not a nudge, it is the move deciding the
        /// finish. At 0.35 the spread is 29% — the most established finisher in the game
        /// closes about 12% harder than typical and the least believed about 13% softer.
        /// That is large enough to feel and small enough that the finish is still carried
        /// by who is winning and whether they earned it.
        /// </summary>
        private const double CredibilityGain = 0.35;

        /// <summary>
        /// Chemistry a side needs before its double-team finish is on the table.
        ///
        /// Set at the bottom of what a real team carries rather than in the middle: the
        /// question this answers is "have these two ever drilled this", not "are they any
        /// good". A thrown-together pairing reads 0.00 and is excluded; a team that has
        /// drifted apart still knows the move.
        /// </summary>
        public const double DrilledEnough = 0.20;

        /// <summary>
        /// Whether a side can go to a double-team finish tonight: more than one of them,
        /// and enough of a team to have a move of their own.
        /// </summary>
        public static bool CanUseTagFinish(MatchSide? side) =>
            side is { IsTag: true } && side.Chemistry >= DrilledEnough;

        /// <summary>
        /// Whether this signature is usable by a wrestler on this side.
        /// </summary>
        public static bool IsAvailable(Signature sig, MatchSide? side) =>
            !sig.TagExclusive || CanUseTagFinish(side);

        /// <summary>
        /// The finisher this wrestler goes to, or null if the roster gives them none — in
        /// which case the engine's generic wording stands, exactly as before.
        ///
        /// A team's own finish outranks a singles one when it is available, because that is
        /// the move the match has been building to. The singles finisher is the fallback,
        /// never the other way round.
        /// </summary>
        public static Signature? For(Wrestler? wrestler, MatchSide? side)
        {
            var finishers = wrestler?.Signature;
            if (finishers is null || finishers.Count == 0) return null;

            Signature? singles = null;

            foreach (var sig in finishers)
            {
                if (sig is null || !sig.IsFinisher) continue;

                if (sig.TagExclusive)
                {
                    if (CanUseTagFinish(side)) return sig;
                    continue;
                }

                singles ??= sig;
            }

            return singles;
        }

        /// <summary>
        /// What the finisher's standing multiplies the finish by. Centred on
        /// <see cref="TypicalCredibility"/>, so an unknown finisher and a missing one are
        /// both 1.00 and the engine's existing numbers are undisturbed.
        /// </summary>
        public static double Credibility(Signature? sig)
        {
            if (sig is null) return 1.0;

            double norm = Math.Clamp(sig.Overness, 0, 100) / 100.0;
            double typical = TypicalCredibility / 100.0;

            return Math.Clamp(1.0 + (norm - typical) * CredibilityGain, 0.85, 1.15);
        }

        /// <summary>
        /// How the move is named in the play-by-play: "the Spear", or "the finisher" when
        /// the roster does not say. The article is included so callers read naturally.
        ///
        /// Twelve of the roster's move names already begin with one — Kofi Kingston
        /// finishes with The Chalkboard — and the first version of this printed "ends it
        /// with the The Chalkboard". A name that brings its own article keeps it.
        /// </summary>
        public static string Describe(Signature? sig)
        {
            string? name = sig?.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return "the finisher";

            return StartsWithArticle(name) ? name : $"the {name}";
        }

        private static bool StartsWithArticle(string name) =>
            name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("a ",  StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("an ", StringComparison.OrdinalIgnoreCase);
    }
}
