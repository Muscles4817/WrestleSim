using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// What the crowd came for, and how firmly.
    ///
    /// <paramref name="Strength"/> runs 0 to 1. Zero means the match promises nothing in
    /// particular and anything is fair game; one means the promise is unmistakable and
    /// breaking it is a decision rather than an accident.
    /// </summary>
    public readonly record struct Expectation(MatchStory Wants, double Strength, string Reading)
    {
        /// <summary>Nothing specific. The honest answer for two names with no history.</summary>
        public static readonly Expectation Nothing =
            new(MatchStory.EvenContest, 0.0, "Nothing in particular. This one is yours to shape.");
    }

    /// <summary>
    /// What a match promises before anybody books a beat of it.
    ///
    /// **This is the half that replaced <see cref="MatchType"/> as an input.** That enum
    /// asked the booker to declare how their match should be graded, which is not a decision
    /// a booker has. A promotion never announces "this will be a technical classic". The
    /// promise is made by who is in the match, what the feud has been, what is at stake and
    /// what the rules are — and it is kept or broken entirely by how the match is booked.
    ///
    /// So the expectation is derived and the delivery is the beat sheet, and the interesting
    /// question is the distance between them. Two brawlers in a blood feud having a mat
    /// classic is not a booking error; it is a miss with the audience, which is a different
    /// and more useful kind of failure than a dropdown nobody understood.
    ///
    /// **Deliberately going against it is a real play.** Doc 18 §3.2 has great workers
    /// adjusting to the room, and doc 16 has a crowd that can be won round by something it
    /// did not ask for. Whether that lands is a question about the performers rather than
    /// about the booking, which is why <see cref="CanCarryIt"/> reads psychology and ring IQ
    /// rather than anything on the plan.
    /// </summary>
    public static class MatchExpectation
    {
        /// <summary>
        /// Below this the promise is too vague to hold anybody to. Two midcarders with no
        /// history promise nothing, and grading them against an imagined expectation would
        /// be inventing a standard to fail them by.
        /// </summary>
        public const double VagueBelow = 0.25;

        /// <summary>
        /// What this match promises.
        /// </summary>
        /// <param name="sides">Everyone in it. Their styles are the strongest single signal.</param>
        /// <param name="feud">The rivalry, if there is one.</param>
        /// <param name="stipulation">The rules, which are a promise in themselves.</param>
        /// <param name="forATitle">Whether a belt is on the line.</param>
        public static Expectation Of(
            IReadOnlyList<MatchSide> sides,
            Feud? feud = null,
            Stipulation stipulation = Stipulation.None,
            bool forATitle = false)
        {
            var wrestlers = sides.SelectMany(s => s.Members).ToList();
            if (wrestlers.Count < 2) return Expectation.Nothing;

            // Ordered by how loudly each speaks. A cage says "violence" over the top of two
            // technicians; a nuclear feud says "fight" over the top of a title.
            if (FromStipulation(stipulation) is { } byRules) return byRules;
            if (FromFeud(feud) is { } byStory) return byStory;
            if (FromStyles(wrestlers) is { } byBody) return Stakes(byBody, forATitle);

            return Stakes(Expectation.Nothing, forATitle);
        }

        // ── Where a promise comes from ───────────────────────────────────────

        /// <summary>
        /// The rules. A stipulation is the least deniable promise a card can make, because
        /// it is printed on the poster — doc 20 §6 has the gimmick match as the thing a
        /// feud escalates *into*, which is to say the audience already knows what it is for.
        /// </summary>
        private static Expectation? FromStipulation(Stipulation stipulation) => stipulation switch
        {
            Stipulation.SteelCage =>
                new(MatchStory.Grudge, 0.85, "A cage. They came to watch somebody get hurt."),

            Stipulation.LastManStanding =>
                new(MatchStory.Grudge, 0.95, "Last man standing. Nothing else counts and everybody knows it."),

            Stipulation.IQuit =>
                new(MatchStory.Grudge, 0.9, "I Quit. The crowd is waiting for one of them to break."),

            Stipulation.NoDisqualification =>
                new(MatchStory.Grudge, 0.7, "No disqualification. The rules were removed for a reason."),

            _ => null
        };

        /// <summary>
        /// The story. A hot feud is a promise that this is a fight, and the hotter it is the
        /// less the audience will accept a wrestling match instead.
        /// </summary>
        private static Expectation? FromFeud(Feud? feud) => feud?.Intensity switch
        {
            FeudIntensity.Nuclear =>
                new(MatchStory.Grudge, 0.95, "This has gone as far as it can go. They want a fight."),

            FeudIntensity.Hot =>
                new(MatchStory.Grudge, 0.75, "A hot feud. The crowd is here for the grudge, not the wrestling."),

            FeudIntensity.Building =>
                new(MatchStory.FaceInPeril, 0.45, "A story is building. They want to see it move."),

            _ => null
        };

        /// <summary>
        /// The bodies. Two technicians promise a contest; two high flyers promise a
        /// spectacle; a giant against somebody half his size promises a question about
        /// whether the smaller one survives.
        ///
        /// **The strongest signal when nothing else is talking, and the one that is always
        /// true.** Doc 18 §4 has style as the thing that decides what a match can even be,
        /// and an audience reads a card by the names on it long before it reads anything
        /// else.
        /// </summary>
        private static Expectation? FromStyles(IReadOnlyList<Wrestler> wrestlers)
        {
            var styles = wrestlers.Select(w => w.Style).ToList();

            bool All(WrestlingStyle a, WrestlingStyle b) => styles.All(s => s == a || s == b);
            bool Any(WrestlingStyle s) => styles.Contains(s);

            // A mismatch of size is louder than a match of style: a giant in the ring with
            // somebody much smaller is the only thing anybody is looking at.
            int biggest  = wrestlers.Max(w => w.Physical?.Size ?? 3);
            int smallest = wrestlers.Min(w => w.Physical?.Size ?? 3);

            if (biggest - smallest >= 2)
                return new(MatchStory.DavidAndGoliath, 0.7,
                           "One of them is giving away a great deal of size. That is the match.");

            if (All(WrestlingStyle.Technical, WrestlingStyle.Grappler))
                return new(MatchStory.TechnicalExhibition, 0.8,
                           "Two mat wrestlers. The crowd is expecting a contest.");

            if (All(WrestlingStyle.HighFlyer, WrestlingStyle.Striker) && Any(WrestlingStyle.HighFlyer))
                return new(MatchStory.Spectacle, 0.75,
                           "They fly. Nobody came to watch a headlock.");

            if (All(WrestlingStyle.Brawler, WrestlingStyle.Powerhouse))
                return new(MatchStory.Grudge, 0.6,
                           "Two big men who hit people. This is going to be a fight.");

            // A mixed field promises nothing, and says so with a strength of zero rather
            // than a small one.
            //
            // It sat at 0.3 first, just above VagueBelow, which meant the critique quoted
            // "nothing is being promised that you cannot change" and then told the booker
            // off for changing it. A reading the game describes as no promise has to score
            // as no promise, or the two halves contradict each other on the same screen.
            return Expectation.Nothing;
        }

        /// <summary>
        /// A belt firms up whatever the promise already was rather than changing it. Doc 21
        /// §2 is clear that a title match's job is to feel like it matters, which raises the
        /// bar without saying what kind of match it should be.
        /// </summary>
        private static Expectation Stakes(Expectation baseline, bool forATitle) =>
            forATitle
                ? baseline with
                  {
                      Strength = Math.Min(1.0, baseline.Strength + 0.2),
                      Reading  = baseline.Reading + " There is a belt on the line."
                  }
                : baseline;

        // ── Meeting it, or not ───────────────────────────────────────────────

        /// <summary>
        /// How far the booked story is from the promised one, 0 to 1.
        ///
        /// Not a symmetric table of distances, because the stories are not equidistant. A
        /// crowd promised a fight will take a face-in-peril, which is still a story about
        /// somebody suffering; it will not take a technical exhibition, which is a different
        /// evening entirely.
        /// </summary>
        public static double Distance(MatchStory wanted, MatchStory booked)
        {
            if (wanted == booked) return 0.0;

            return (wanted, booked) switch
            {
                (MatchStory.Grudge, MatchStory.FaceInPeril)              => 0.35,
                (MatchStory.Grudge, MatchStory.DavidAndGoliath)          => 0.45,
                (MatchStory.Grudge, MatchStory.TechnicalExhibition)      => 1.0,
                (MatchStory.Grudge, MatchStory.Showcase)                 => 0.8,

                (MatchStory.TechnicalExhibition, MatchStory.EvenContest) => 0.3,
                (MatchStory.TechnicalExhibition, MatchStory.Grudge)      => 1.0,
                (MatchStory.TechnicalExhibition, MatchStory.Spectacle)   => 0.8,

                (MatchStory.Spectacle, MatchStory.TechnicalExhibition)   => 0.8,
                (MatchStory.Spectacle, MatchStory.EvenContest)           => 0.4,

                (MatchStory.DavidAndGoliath, MatchStory.FaceInPeril)     => 0.3,
                (MatchStory.DavidAndGoliath, MatchStory.EvenContest)     => 0.7,

                (MatchStory.EvenContest, _)                              => 0.4,
                (_, MatchStory.EvenContest)                              => 0.45,

                _ => 0.6
            };
        }

        /// <summary>
        /// Whether these performers can win a crowd round to something it did not ask for.
        ///
        /// Psychology and ring IQ, because that is what the skill actually is: knowing what
        /// the room wants and deciding to give it something else anyway. Doc 18 §3.2 —
        /// "great workers adjust in real time" — and the corollary that limited ones cannot,
        /// so the same booking is a triumph for one pair and a disaster for another.
        /// </summary>
        public static double CanCarryIt(IReadOnlyList<MatchSide> sides)
        {
            var wrestlers = sides.SelectMany(s => s.Members).ToList();
            if (wrestlers.Count == 0) return 0.5;

            // The weakest link, not the average. A match is carried at the pace of whoever
            // is least able to change it, which is why one great worker cannot save a
            // mismatched booking on their own.
            double worst = wrestlers.Min(w =>
                ((w.Mental?.Psychology ?? 60) * 0.6 + (w.Mental?.RingIQ ?? 60) * 0.4) / 100.0);

            return Math.Clamp(worst, 0.0, 1.0);
        }

        /// <summary>
        /// What booking this story against this expectation is worth, in final-score points.
        ///
        /// Positive when the booking serves what the match promised. Negative when it does
        /// not — but scaled by whether these two can carry it, so going against the room is
        /// a gamble on the performers rather than a flat penalty. Two exceptional workers
        /// having a mat classic nobody asked for can still win; two limited ones cannot.
        ///
        /// A vague promise pays and costs nothing either way, because there is nothing there
        /// to keep or break.
        /// </summary>
        public static double Reward(Expectation expectation, MatchStory booked, double canCarryIt)
        {
            if (expectation.Strength < VagueBelow) return 0.0;

            double distance = Distance(expectation.Wants, booked);

            // Met it: worth more the louder the promise was, because a loud promise kept is
            // the match the crowd came for.
            if (distance <= 0.35)
                return (0.35 - distance) / 0.35 * expectation.Strength * 6.0;

            // Missed it. The gamble: a pair who can work the room give most of it back.
            double miss    = (distance - 0.35) / 0.65 * expectation.Strength;
            double carried = Math.Clamp((canCarryIt - 0.55) / 0.35, 0.0, 1.0);

            return -miss * 9.0 * (1.0 - carried * 0.8);
        }

        /// <summary>
        /// The one line the builder shows once the wrestlers are picked, so a booker can see
        /// what they are working with or against before they choose anything else.
        /// </summary>
        public static string Say(Expectation expectation) =>
            expectation.Strength < VagueBelow
                ? Expectation.Nothing.Reading
                : expectation.Reading;
    }
}
