using WrestlingSim.Enums;
using WrestlingSim.Models.Rumble;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// How a battle royal is graded, which is not how a match is graded.
    ///
    /// Doc 18 §2.5: "Judged on moments, not on work." Everything the match engine measures —
    /// technical output, the shape of a heat segment, whether the finish was earned — needs
    /// beats to measure, and this format has none. Grading it on that machinery would not be
    /// generous or harsh, it would be answering a different question and printing the number
    /// anyway.
    ///
    /// So there is no technical component here at all. What there is instead:
    ///
    ///   • **Moments**, with the value of each falling off as the same kind repeats. Twelve
    ///     showdowns is not twelve times a Rumble; it is a match with no showdowns in it,
    ///     because a thing that happens twelve times is the texture rather than the moment.
    ///   • **The field**, because a battle royal with nobody in it the crowd came for is
    ///     thirty minutes of nothing however it is booked.
    ///   • **The winner's story**, which is mostly their entry number — doc 18 says the
    ///     entries are what make this a story rather than a scramble, so the entries have to
    ///     be worth something or that sentence is decoration.
    ///
    /// Every one of these is a pure function so it can be tested as itself, which this
    /// codebase learned the hard way.
    /// </summary>
    public static class RumbleScoring
    {
        /// <summary>
        /// What one moment is worth before repetition, 0–1 before weighting.
        ///
        /// The ordering is a claim about wrestling, not a knob: a surprise return is the
        /// biggest single thing this format can produce and needs nothing from the match
        /// around it, while a near-elimination is a spot the crowd has seen a hundred times.
        /// </summary>
        public static double MomentWeight(RumbleMomentKind kind) => kind switch
        {
            RumbleMomentKind.SurpriseReturn  => 1.00,
            RumbleMomentKind.IronMan         => 0.90,
            RumbleMomentKind.Showdown        => 0.75,
            RumbleMomentKind.MassElimination => 0.60,
            RumbleMomentKind.Betrayal        => 0.70,
            RumbleMomentKind.NearElimination => 0.30,
            _                                => 0.25
        };

        /// <summary>
        /// How fast a kind of moment wears out when it is repeated.
        ///
        /// A second surprise return is still a surprise; a second near-elimination is a spot
        /// the crowd has already sat through, and by the fourth they are waiting for it to
        /// end. So the cheap moments decay hardest, which is the opposite of what a booker
        /// reaching for filler wants and exactly why the rule is here.
        /// </summary>
        public static double MomentDecay(RumbleMomentKind kind) => kind switch
        {
            RumbleMomentKind.SurpriseReturn  => 0.80,
            RumbleMomentKind.Showdown        => 0.70,
            RumbleMomentKind.Betrayal        => 0.65,
            RumbleMomentKind.MassElimination => 0.55,
            RumbleMomentKind.NearElimination => 0.40,
            _                                => 0.60
        };

        /// <summary>
        /// What a run of moments is worth in total, 0–1.
        ///
        /// Each repeat of a kind is worth its decay again, so the third near-elimination is
        /// worth 0.4² of the first. Divided by a target of six rather than by the count, so
        /// that booking more moments genuinely is better up to a point and then stops paying
        /// — a Rumble is not improved by making every minute a highlight, because then none
        /// of them is one.
        /// </summary>
        public static double MomentScore(IEnumerable<RumbleMomentKind> moments)
        {
            var seen = new Dictionary<RumbleMomentKind, int>();
            double total = 0;

            foreach (var kind in moments)
            {
                int already = seen.TryGetValue(kind, out int n) ? n : 0;
                total += MomentWeight(kind) * Math.Pow(MomentDecay(kind), already);
                seen[kind] = already + 1;
            }

            return Math.Clamp(total / 6.0, 0, 1);
        }

        /// <summary>
        /// What the winner's entry number is worth as a story, 0–1.
        ///
        /// **Not simply "earlier is better", and the first version's being exactly that was a
        /// design hole rather than a simplification.** A booker who picks the order freely
        /// and is rewarded for a low number has no decision to make: every winner enters at
        /// two, the format's best story becomes a box to tick, and a mechanic with one right
        /// answer is not a mechanic.
        ///
        /// What a number is worth depends on who drew it, which is how it works in the ring:
        ///
        ///   • A **face** who comes out early and outlasts the field is the format's biggest
        ///     rub. Thirty minutes of surviving is the whole story, and the crowd is counting.
        ///   • A **heel** who comes out early gets far less for it. Nobody wants to admire a
        ///     heel's endurance; the sympathy the run generates is the wrong currency for them.
        ///   • A **heel** who swans out last and steals it is worth the most they can be worth
        ///     here — a fix everybody can see, and the fury is the point. Heat is engagement
        ///     (A5), and for a heel it is the goal rather than the cost.
        ///   • A **face** winning from last is the flat one: a coronation nobody bought.
        ///
        /// So the low number stops being universally correct and becomes a question about who
        /// you are making tonight.
        ///
        /// **Conditioning gates the face's version**, because going the distance from number
        /// two has to be believable. A wrestler with no gas tank outlasting twenty-eight
        /// people is not an underdog story, it is a booking the crowd can see through. It
        /// does not gate the heel's steal at all — arriving fresh and last requires nothing.
        ///
        /// **Zero for a battle royal**, and not as a penalty. Everybody started together, so
        /// there is no number to have a story about — which is doc 18's "it is the entries
        /// that make the Rumble a story rather than a scramble", stated as a number instead
        /// of a sentence.
        /// </summary>
        public static double EntryStory(int winnerNumber, int fieldSize, bool battleRoyal,
                                        Alignment alignment = Alignment.Face,
                                        double conditioning = 1.0)
        {
            if (battleRoyal || fieldSize <= 1) return 0.0;

            int number = Math.Clamp(winnerNumber, 1, fieldSize);

            // How much of the field they were out there for, and its complement.
            double early = (double)(fieldSize - number) / (fieldSize - 1);
            double late  = 1.0 - early;

            return alignment switch
            {
                // Believability is the whole of it: an unconditioned wrestler cannot sell
                // having lasted an hour, so the rub is what the crowd will accept.
                Alignment.Face => early * Math.Clamp(0.45 + conditioning * 0.55, 0, 1),

                // The steal. Worth less at its best than a face's iron-man run at its best,
                // because outrage is a smaller currency than admiration — but it peaks at the
                // opposite end, which is the point.
                Alignment.Heel => late * 0.8,

                // Nobody is quite sure how to feel, so neither reading pays in full.
                _ => Math.Max(early * 0.6, late * 0.4)
            };
        }

        /// <summary>
        /// How long the longest run was, as a share of the field, 0–1.
        ///
        /// Read off the match rather than booked, because an iron-man run is the one moment
        /// here nobody can promise in advance — it is made of everything that happened while
        /// they were still in there.
        /// </summary>
        public static double IronManShare(int entriesOutlasted, int fieldSize) =>
            fieldSize <= 1 ? 0.0 : Math.Clamp((double)entriesOutlasted / (fieldSize - 1), 0, 1);

        /// <summary>
        /// How likely somebody is to be the next one dumped, before the roll.
        ///
        /// Weighted against connection, because this is a spectacle and the crowd's
        /// attention is on the people it came for: a battle royal that throws its biggest
        /// name out at number four has spent the only thing it had. Higher means likelier to
        /// go.
        ///
        /// A pure function because the version inside the engine could not be tested. A
        /// single-seed check on "do the big names last longest" passed with the weighting
        /// replaced by `_rng.NextDouble()` — entry position decides who is even *in the ring*
        /// to be picked, so an end-to-end test measures that and reports it as this. Fourth
        /// time this codebase has made that mistake in a day, and the fix is the same one it
        /// keeps arriving at: if a rule can only be seen through a whole match, extract it.
        /// </summary>
        public static double EliminationRisk(double connection) =>
            Math.Clamp(1.0 - connection * 0.7, 0, 1);

        /// <summary>
        /// The final score out of 100.
        ///
        /// The weights are the argument. Moments are half of it because doc 18 says the
        /// format is judged on them; the field is a third because a battle royal is mostly
        /// who is in it; the entry story and the iron-man run split the rest, and both of
        /// them are zero in a battle royal — where there are no entries and, everybody
        /// having started together, no run to have outlasted anybody.
        ///
        /// A battle royal therefore tops out lower than a Rumble of the same field, which is
        /// doc 18's claim about entries made falsifiable rather than repeated.
        /// </summary>
        public static double FinalScore(double momentScore, double fieldStarPower,
                                        double entryStory, double ironManShare) =>
            Math.Clamp(
                momentScore    * 50.0 +
                fieldStarPower * 30.0 +
                entryStory     * 12.0 +
                ironManShare   *  8.0,
                0, 100);
    }
}
