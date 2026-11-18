using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Turns — docs/gimmick-plan.md Part 5, doc 19 §5. "The single largest one-shot heat
    /// generator in wrestling": a full reset of a character, at the cost of everything the old
    /// alignment had built.
    ///
    /// The booked alignment is the player's decision (owner, 17 Nov 2026), so a turn is booked —
    /// as a segment, cold, or as the payoff of a build-up (decided 17 Nov). What it is worth is
    /// priced here, on what doc 19 says decides it:
    ///
    ///   • the crowd's current reading — a heel the crowd has been cheering turns face to an
    ///     enormous reaction; turning somebody against how the crowd feels is a harder sell;
    ///   • the asymmetry — heel turns are easier, face turns need forgiveness (§5.3);
    ///   • the moment — a premium event, not a house show (§5.2 rule 4);
    ///   • how often — "roughly once every 2–4 years per performer" (§5.4);
    ///   • the build — teases that made it make sense in retrospect (§5.2 rule 1);
    ///   • comedy — a comic character's heel turn reads as a joke (§5.3).
    ///
    /// Every number is a design estimate. A payoff of 1.0 is an ordinary, decent turn.
    /// </summary>
    public static class Turns
    {
        public readonly record struct Reading(double Payoff, IReadOnlyList<string> Because);

        /// <summary>What a turn would be worth now, and why — for the builder, before it is booked.</summary>
        public static Reading Forecast(Wrestler w, Alignment to, ShowType type, DateOnly on)
        {
            var because = new List<string>();
            var from = w.Gimmick?.NaturalAlignment ?? Alignment.Tweener;
            var crowd = CrowdReading.Of(w, on);

            double p = to switch
            {
                Alignment.Face => 0.75,     // the crowd has to forgive
                Alignment.Heel => 1.0,      // the crowd enjoys betrayal
                _              => 0.5       // a half-turn confuses (§5.2 rule 3)
            };
            if (to == Alignment.Face) because.Add("A face turn asks the crowd to forgive.");
            if (to == Alignment.Tweener) because.Add("Turning to neither is a half-turn — it confuses as much as it excites.");

            double c = (to, crowd) switch
            {
                (Alignment.Face, CrowdReading.Reaction.Cheering) => 1.6,
                (Alignment.Face, CrowdReading.Reaction.Booing)   => 0.5,
                (Alignment.Heel, CrowdReading.Reaction.Booing)   => 1.3,
                (Alignment.Heel, CrowdReading.Reaction.Cheering) => 0.9,
                _                                               => 1.0
            };
            p *= c;
            if (c >= 1.3) because.Add(to == Alignment.Face
                ? "The crowd has already been cheering them — this is the turn it has been asking for."
                : "The crowd has already turned on them — the booking is catching up with the audience.");
            if (c <= 0.5) because.Add("The crowd is booing them. Asking it to cheer now is a hard sell.");
            if (c is > 0.5 and < 1.0) because.Add("The crowd likes them. Some of it will keep cheering whatever they do.");

            double moment = type switch { ShowType.PremiumEvent => 1.3, ShowType.HouseShow => 0.5, _ => 1.0 };
            p *= moment;
            if (moment > 1) because.Add("A premium event is the moment for it.");
            if (moment < 1) because.Add("A house show is a small room for a big moment; almost nobody will see it.");

            if (w.LastTurned is { } last)
            {
                int days = on.DayNumber - last.DayNumber;
                double often = days < 365 ? 0.3 : days < 730 ? 0.6 : days < 1095 ? 0.85 : 1.0;
                p *= often;
                if (often < 1) because.Add($"They turned {Ago(days)}. A performer who turns every year stops meaning anything.");
            }

            if (w.TeasedToward == to && w.TurnAnticipation > 0.05)
            {
                p *= 1 + 0.8 * w.TurnAnticipation;
                because.Add(w.TurnAnticipation >= 0.6
                    ? "It has been built: the crowd has seen this coming and wants it."
                    : "There has been some build — the crowd half-expects it.");
            }
            else if (w.TurnAnticipation <= 0.05)
                because.Add("Nothing has set it up. A cold turn can shock; it rarely lands as hard as a built one.");

            if (w.Gimmick?.Type == GimmickType.Comedy && to == Alignment.Heel)
            {
                p *= 0.5;
                because.Add("Once the crowd has laughed at you, menace is hard to believe.");
            }

            if (from == to) because.Insert(0, $"They are already {Label(to)}.");
            return new Reading(from == to ? 0 : p, because);
        }

        public static string Words(double payoff) => payoff switch
        {
            >= 1.4 => "huge",
            >= 0.9 => "a real moment",
            >= 0.5 => "muted",
            > 0    => "likely to fall flat",
            _      => "not a turn"
        };

        /// <summary>
        /// The turn happens. The alignment flips, the character is partly made new again, and
        /// the moment is paid in momentum and a little standing. Returns the change to apply and
        /// the line for the report.
        /// </summary>
        public static (StatusChange Change, string Line, double Payoff) Execute(Wrestler w, Alignment to, ShowType type, DateOnly on)
        {
            var reading = Forecast(w, to, type, on);
            double p = reading.Payoff;
            var from = w.Gimmick?.NaturalAlignment ?? Alignment.Tweener;

            if (w.Gimmick is { } g)
            {
                g.NaturalAlignment = to;
                // A turn is "the cheapest full character reboot available" (§5.1).
                double restore = Math.Min(1, 0.6 * p);
                if (g.Freshness < CharacterFreshness.RestCeiling)
                    g.Freshness += (CharacterFreshness.RestCeiling - g.Freshness) * restore;
            }
            w.LastTurned = on;
            w.Turns++;
            w.TurnAnticipation = 0;
            w.TeasedToward = null;
            w.TeaseSince = null;

            string name = w.RingName ?? "They";
            string line = $"{name} has turned {Label(to)} — {Words(p)}.";
            var change = new StatusChange(w, HeatEconomy.DampenGain(w, 3.0 * p), 30.0 * p,
                $"Turned {Label(to)} (was {Label(from)}). {string.Join(" ", reading.Because.Take(2))}");
            return (change, line, p);
        }

        // ── Teases ───────────────────────────────────────────────────────────

        /// <summary>How much one tease builds.</summary>
        public const double TeaseStep = 0.25;

        /// <summary>
        /// A tease: something that belongs to the other alignment. Builds anticipation toward a
        /// turn; a tease the other way starts the count again.
        /// </summary>
        public static string Tease(Wrestler w, Alignment toward, DateOnly on)
        {
            if (w.Gimmick?.NaturalAlignment == toward) return $"{w.RingName} is already {Label(toward)}.";
            if (w.TeasedToward != toward) { w.TurnAnticipation = 0; w.TeaseSince = on; w.TeasedToward = toward; }
            w.TeaseSince ??= on;
            w.TurnAnticipation = Math.Min(1, w.TurnAnticipation + TeaseStep);
            return w.TurnAnticipation >= 0.75
                ? $"{w.RingName} is drifting toward {Label(toward)}, and the crowd can see it."
                : $"A crack in {w.RingName}'s character — something a {Label(toward)} would do.";
        }

        /// <summary>Anticipation fades in a month and a half if nothing feeds it.</summary>
        public const double AnticipationHalfLifeDays = 45;

        /// <summary>Teased this long without a turn, the crowd stops believing it is coming (doc 12 §4).</summary>
        public const int BelieveForDays = 120;

        public static void Fade(IEnumerable<Wrestler> people, DateOnly today, int days)
        {
            if (days <= 0) return;
            foreach (var w in people)
            {
                if (w.TurnAnticipation <= 0) continue;
                bool stale = w.TeaseSince is { } since && today.DayNumber - since.DayNumber > BelieveForDays;
                double halfLife = stale ? 15 : AnticipationHalfLifeDays;
                w.TurnAnticipation *= Math.Pow(0.5, days / halfLife);
                if (w.TurnAnticipation < 0.02) { w.TurnAnticipation = 0; w.TeasedToward = null; w.TeaseSince = null; }
            }
        }

        /// <summary>
        /// How far teasing moves the crowd's reading toward where it is going, for
        /// <see cref="PerformerProfile.Favour"/>: the crowd starts to see it before it happens.
        /// </summary>
        public static double Lean(Wrestler w) => w.TeasedToward switch
        {
            Alignment.Face => 1.2 * w.TurnAnticipation,
            Alignment.Heel => -1.2 * w.TurnAnticipation,
            _              => 0
        };

        public static string? AnticipationWords(Wrestler w) => w.TurnAnticipation switch
        {
            < 0.05 => null,
            < 0.4  => $"A turn toward {Label(w.TeasedToward)} has been hinted at.",
            < 0.75 => $"The crowd senses a turn toward {Label(w.TeasedToward)} coming.",
            _      => $"The crowd is waiting for the turn toward {Label(w.TeasedToward)}. Do not make it wait too long."
        };

        // ── Rivals ───────────────────────────────────────────────────────────

        /// <summary>
        /// Once a month each rival considers turning somebody, when it makes sense to (decided
        /// 17 Nov 2026): a crowd that has gone the other way from the booking, or a character worn
        /// out, and not turned in two years. A better-run promotion acts on it more often; every
        /// turn is priced by the same <see cref="Execute"/> as the player's.
        /// </summary>
        public static void RivalMonth(Career career)
        {
            var today = career.CurrentDate;
            foreach (var rival in career.Rivals.Where(r => !r.Frozen))
            {
                double competence = World.CompetenceOf(rival);
                var type = rival.Promotion.HasTelevision ? ShowType.Television : ShowType.HouseShow;
                foreach (var contract in rival.Contracts)
                {
                    var w = career.FindWrestler(contract.WrestlerId);
                    if (w?.Gimmick is not { } g || w.IsRetired) continue;
                    if (w.LastTurned is { } last && today.DayNumber - last.DayNumber < 730) continue;
                    if (g.NaturalAlignment == Alignment.Tweener) continue;

                    var crowd = CrowdReading.Of(w, today);
                    bool crossed = CrowdReading.Crossed(g.NaturalAlignment, crowd);
                    bool worn = g.Freshness < CharacterFreshness.Worn;
                    if (!crossed && !worn) continue;

                    double chance = (crossed ? 0.15 : 0.06) * Math.Clamp(competence, 0.2, 1.5);
                    var rng = new Random(StableSeed.From(rival.Id, today.DayNumber, w.Id, "turn"));
                    if (rng.NextDouble() >= chance) continue;

                    var to = g.NaturalAlignment == Alignment.Face ? Alignment.Heel : Alignment.Face;
                    var (change, line, _) = Execute(w, to, type, today);
                    HeatEconomy.Apply(change);
                    career.AddNews($"{rival.Name}: {line}");
                }
            }
        }

        // ── Words ────────────────────────────────────────────────────────────

        public static string Label(Alignment? a) => a switch
        {
            Alignment.Face => "face",
            Alignment.Heel => "heel",
            _              => "tweener"
        };

        private static string Ago(int days) => days < 60 ? $"{days} days ago" : days < 730 ? $"{days / 30} months ago" : $"{days / 365} years ago";
    }
}
