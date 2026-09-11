using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Which name a booker should be offered first.
    ///
    /// This lives in Core rather than in the picker component because it is a booking
    /// question, not a rendering one. Every roster list in the app used to be
    /// `OrderByDescending(Overness)` — so after choosing side A, with the game knowing
    /// exactly who A has a live feud with and who the crowd has already watched A fight four
    /// times this month, side B was offered in order of how popular people are.
    ///
    /// It is also here because the component could not be tested. A UX review pointed out
    /// that the selection rewrite shipped with no regression test at all, and the honest
    /// answer was not to defend that but to move the part that deserves one somewhere a test
    /// can reach it. What is left in the component is layout.
    /// </summary>
    public static class BookingSuggestions
    {
        /// <summary>Why a name is being offered where it is, and what to say about it.</summary>
        public readonly record struct Suggestion(
            Wrestler Wrestler, SuggestionBand Band, string Reason, string? BookedAs,
            string? Condition = null);

        public enum SuggestionBand
        {
            /// <summary>A live rivalry with the side already picked.</summary>
            Story = 1,

            /// <summary>Somebody's standing tag partner.</summary>
            Partner = 2,

            /// <summary>On the most recent card.</summary>
            Recent = 10,

            /// <summary>Nothing in particular — sorted by card position, then popularity.</summary>
            Plain = 20,

            /// <summary>The crowd has been shown this pairing too often.</summary>
            WornOut = 60,

            /// <summary>Already in this match. Visible, and tapping swaps.</summary>
            Booked = 90
        }

        /// <summary>
        /// Freshness below which a pairing is worth warning about rather than suggesting.
        /// Doc 20 §9.1 — three matches is the natural life of a pairing.
        /// </summary>
        public const double StaleBelow = 0.75;

        /// <summary>
        /// Ranks a pool for one slot.
        ///
        /// <paramref name="against"/> is the side being picked *against*, and may be empty —
        /// which it is for the whole of the normal fill order while side A is being filled.
        /// The first version gated the standing-partner band on it being non-empty, so that
        /// band never appeared where it is most useful: picking A's partner with A chosen.
        /// </summary>
        public static IReadOnlyList<Suggestion> Rank(
            IEnumerable<Wrestler> pool,
            IReadOnlyList<Wrestler> against,
            FeudBook feuds,
            Func<Wrestler, string?> bookedAs,
            Func<Wrestler, Wrestler?> standingPartnerOf,
            IReadOnlySet<Wrestler> recentlyBooked,
            DateOnly? today) =>
            // The ring-condition reading is bolted on after the fact rather than threaded
            // through Describe's six return paths, because it is orthogonal to all of them:
            // being cooked is equally worth saying about somebody in a hot feud and
            // somebody with no story at all.
            pool.Select(w => Describe(w, against, feuds, bookedAs, standingPartnerOf, recentlyBooked, today)
                             with { Condition = ConditionOf(w, today) })
                .OrderBy(SortKey)
                .ThenByDescending(x => x.Wrestler.Overness)
                .ToList();

        /// <summary>
        /// The one line under a name that says whether they can work at all, and how well.
        ///
        /// Injury first and unconditionally: being cooked is advice and being hurt is a
        /// fact, so a wrestler who is out does not also get told he needs a rest.
        ///
        /// Being out on the road comes next, and for the same reason — it is where they are
        /// rather than how they feel. The picker offered touring wrestlers with no trace of
        /// it, so a booker could put somebody in a televised main event on a night their own
        /// standing instruction had them in a gym two states away, and find out from the
        /// report. It is still an offer and not a gate: a card beats the towns for that
        /// wrestler, at the price of one town, which is an ordinary week rather than a
        /// mistake. The row says so; it does not decide.
        /// </summary>
        private static string? ConditionOf(Wrestler w, DateOnly? today)
        {
            if (today is not { } date) return RingCondition.WarningFor(w);

            if (w.Injury is { } injury && injury.KeepsOut(date))
                return $"unavailable — {injury.Reason(date)}";

            if (w.IsTouring(date))
                return $"out on the road until {w.TouringUntil!.Value:d MMM}";

            return RingCondition.WarningFor(w);
        }

        /// <summary>
        /// Where a suggestion sorts. The band, except that <see cref="SuggestionBand.Plain"/>
        /// subdivides by card position.
        ///
        /// That subdivision is not cosmetic and losing it was a real bug. A plain row's
        /// heading *is* its card position, so if plain rows are ordered by popularity alone
        /// the headings stop being contiguous — `CardPosition` is derived from
        /// `EffectiveOverness`, which is overness plus a momentum term, so anybody on a
        /// streak crosses a band boundary without moving in an overness sort. Review
        /// reproduced `MAIN EVENT, MIDCARD, UPPER CARD, LOWER CARD, ENHANCEMENT` in a real
        /// career after eight shows.
        ///
        /// The consequence was not just untidy headings. The picker groups these rows for
        /// display, so a non-contiguous order meant the rendered order and the ranked order
        /// were different lists, and the keyboard cursor indexed the wrong one: by the tenth
        /// arrow press the highlight was 23 rows off screen and Enter booked a name that was
        /// not visible. That is the same keyboard bug round 1 found, reintroduced in a worse
        /// form by a refactor that was supposed to be behaviour-preserving — the picker now
        /// derives its cursor from the order it actually renders, so this cannot desync
        /// again even if a future band is added out of order.
        /// </summary>
        private static int SortKey(Suggestion s) =>
            s.Band == SuggestionBand.Plain
                ? (int)SuggestionBand.Plain + (CardPosition.MainEvent - s.Wrestler.CardPosition)
                : (int)s.Band;

        private static Suggestion Describe(
            Wrestler w,
            IReadOnlyList<Wrestler> against,
            FeudBook feuds,
            Func<Wrestler, string?> bookedAs,
            Func<Wrestler, Wrestler?> standingPartnerOf,
            IReadOnlySet<Wrestler> recentlyBooked,
            DateOnly? today)
        {
            string position = w.CardPosition.Label();

            // Booked already: visible and swappable, but last. Ranking these first — which is
            // how it shipped, on the reasoning that a mis-pick should be easy to see — makes
            // the top row of every picker somebody already in the match, so tapping the
            // obvious thing swaps two slots instead of filling an empty one. Filling six
            // slots left one filled.
            if (bookedAs(w) is { } used)
                return new Suggestion(w, SuggestionBand.Booked, $"{position} · {used} — tap to swap", used);

            var feud = against.Count > 0 ? feuds.Find([w], against) : null;
            if (feud is not null && against.Count > 0)
            {
                double freshness = feud.Familiarity(today);
                string other = string.Join(" & ", against.Select(x => x.RingName));

                // Staleness is checked *before* intensity and outside it: how often the crowd
                // has been asked to watch this is not the same question as whether a story is
                // attached. It matters more since A3, because a neglected feud now decays to
                // Intensity.None with its match count intact — and that is exactly the
                // pairing that most needs the warning.
                if (freshness < StaleBelow)
                    return new Suggestion(w, SuggestionBand.WornOut,
                        $"{position} · meeting {feud.NextMeetingNumber(today):F0} with {other} — " +
                        $"{freshness * 100:F0}% of what the first one drew", null);

                if (feud.Intensity > FeudIntensity.None)
                    return new Suggestion(w, SuggestionBand.Story,
                        $"{position} · {feud.Intensity.ToString().ToLowerInvariant()} feud with {other}" +
                        (feud.MatchCount > 0
                            ? $", {feud.MatchCount} match{(feud.MatchCount == 1 ? "" : "es")} in"
                            : ""), null);
            }

            // `!against.Contains(mate)` is load-bearing: without it the picker offers a
            // standing team's two members *against each other*, at the top of the list, with
            // "X's partner" as the reason. The guard existed before the extraction and was
            // dropped along with a separate gate that genuinely needed removing.
            if (standingPartnerOf(w) is { } mate
                && bookedAs(mate) is not null
                && !against.Contains(mate))
                return new Suggestion(w, SuggestionBand.Partner,
                    $"{position} · {mate.RingName}'s partner", null);

            if (recentlyBooked.Contains(w))
                return new Suggestion(w, SuggestionBand.Recent, $"{position} · on the last card", null);

            return new Suggestion(w, SuggestionBand.Plain, Plain(w, today), null);
        }

        /// <summary>A name with no story attached still has to say something usable.</summary>
        private static string Plain(Wrestler w, DateOnly? today)
        {
            // Not the alignment. It used to be appended here, in grey, at the end of a line
            // whose job is "why is this name in front of me" — and it is now a coloured badge
            // beside the ring name, which is where a booker looks for it. Said in both places
            // the row read "Powerhouse · heel" under a HEEL badge, and the reason line is the
            // half that has to fit on a phone.
            var parts = new List<string> { w.Style.ToString() };

            if (today is { } date && w.LastAppearance is { } seen)
            {
                int days = date.DayNumber - seen.DayNumber;
                if (days > 21) parts.Add($"not seen in {days / 7} weeks");
            }

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// The class a band's heading and reason line are painted with.
        ///
        /// The reason line is the row's argument for itself, and it was `--muted` for all of
        /// them — so "there is a story here", which is the single most useful thing the
        /// picker can say, was the same grey as the wrestling style beside it.
        ///
        /// Only three of the six are coloured, and that is the point rather than an omission.
        /// A story is the reason to book somebody, a worn-out pairing is the reason not to,
        /// and a name already in the match is not a candidate at all. The rest are the
        /// ordinary case, and colouring the ordinary case is how a palette stops meaning
        /// anything — the app has been here before with twenty-six amber notices.
        /// </summary>
        public static string Tone(SuggestionBand band) => band switch
        {
            SuggestionBand.Story   => "band--story",
            SuggestionBand.WornOut => "band--worn",
            SuggestionBand.Booked  => "band--booked",
            _                      => ""
        };

        /// <summary>The band's heading, for display.</summary>
        public static string Heading(SuggestionBand band, Wrestler w) => band switch
        {
            SuggestionBand.Story    => "There is a story here",
            SuggestionBand.Partner  => "Suggested",
            SuggestionBand.Recent   => "Recently booked",
            SuggestionBand.WornOut  => "The crowd has seen this",
            SuggestionBand.Booked   => "Already in this match",
            _                       => w.CardPosition.Label()
        };
    }
}
