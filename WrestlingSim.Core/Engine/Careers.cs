using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Arriving and leaving (living-world plan, Part 3). A class of rookies each January, sized to
    /// hold the world's population steady; retirement every month, by the things that end careers
    /// in real life — the body, injuries, having nowhere to work — and held off by the thing that
    /// keeps people going, being wanted at the top. Older names often go part-time first.
    ///
    /// **No protection for real people.** The 14 Nov plan had a switch to hold the real roster in
    /// place for some years. On 15 Nov the owner asked why it would be needed if the model is
    /// sensible, and it is not: a star still at the top, under contract and healthy has a small
    /// chance of retiring, because that is what keeps real stars going. What retires a real person
    /// here is what would retire them there. Being early or late against what actually happens is
    /// the simulation's divergence from history, which every career has from its first night.
    /// </summary>
    public static class Careers
    {
        // ── Calibration (first guesses for Part 8; doc 15: retirement mostly at 35–45) ──

        /// <summary>The chance of retiring within a year, by age alone, before anything else is weighed.</summary>
        private static readonly (int Age, double Chance)[] ByAge =
        [
            (24, 0.003), (30, 0.008), (35, 0.03), (38, 0.06), (40, 0.09),
            (43, 0.15), (46, 0.24), (50, 0.40), (55, 0.60)
        ];

        public sealed record Outlook(double Chance, IReadOnlyList<string> Why);

        /// <summary>
        /// How likely somebody is to retire within a year from today, and what is pushing which
        /// way. Every factor is a reason a real wrestler stops or keeps going.
        /// </summary>
        public static Outlook RetirementChance(Career career, Wrestler w)
        {
            var today = career.CurrentDate;
            var why = new List<string>();
            int age = w.AgeOn(today) ?? 30;
            double chance = Interpolate(age);

            // The body: injuries that took months, and one keeping them out now.
            int serious = w.InjuryHistory.Count(i => i.WeeksOut >= 12);
            if (serious > 0) { chance *= 1 + 0.3 * serious; why.Add(serious == 1 ? "a serious injury behind them" : $"{serious} serious injuries behind them"); }
            if (w.Injury is { } now && now.KeepsOut(today) && now.WeeksOut >= 26) { chance *= 2; why.Add("a long injury now"); }

            // Nowhere to work. Counted from when they could first have worked in this world.
            var seen = w.LastAppearance ?? career.StartDate;
            int idleMonths = Math.Max(0, (today.DayNumber - seen.DayNumber) / 30);
            if (idleMonths > 6) { chance *= Math.Min(4, 1 + idleMonths / 6.0); why.Add($"no match in {idleMonths} months"); }
            // The washing-out the funnel has after debut (doc 15): young, unknown, and unbooked for a year.
            if (age < 30 && idleMonths >= 12 && w.Overness < 25) { chance += 0.15; why.Add("young, unknown and unbooked"); }

            // Being wanted at the top keeps people going: money, and the chance that does not come back.
            bool contracted = career.IsGuaranteed(w) || career.SignedTo(w) is not null;
            if (contracted && w.Overness >= 85) { chance *= 0.35; why.Add("a top name under contract"); }
            else if (contracted && w.Overness >= 70) { chance *= 0.5; why.Add("near the top, under contract"); }

            return new Outlook(Math.Clamp(chance, 0, 0.95), why);
        }

        private static double Interpolate(int age)
        {
            if (age <= ByAge[0].Age) return ByAge[0].Chance;
            for (int i = 0; i < ByAge.Length - 1; i++)
            {
                var (a0, c0) = ByAge[i];
                var (a1, c1) = ByAge[i + 1];
                if (age <= a1) return c0 + (c1 - c0) * (age - a0) / (double)(a1 - a0);
            }
            return ByAge[^1].Chance;
        }

        // ── The month ────────────────────────────────────────────────────────

        /// <summary>
        /// The month's retirements. A name somebody would still pay to see — popular enough, old
        /// enough — goes part-time first rather than leaving; a part-timer, or anybody else,
        /// retires. Seeded by the date and the person, so the same world does the same thing.
        /// </summary>
        public static void Month(Career career)
        {
            var today = career.CurrentDate;
            foreach (var w in career.Roster.Where(w => !w.IsRetired).ToList())
            {
                var outlook = RetirementChance(career, w);
                double monthly = 1 - Math.Pow(1 - outlook.Chance, 1 / 12.0);
                var rng = new Random(StableSeed.From(w.Id, today.DayNumber, "retire"));
                if (rng.NextDouble() >= monthly) continue;

                int age = w.AgeOn(today) ?? 30;
                if (!w.IsPartTime && age >= 38 && w.Overness >= 60)
                    GoPartTime(career, w);
                else
                    Retire(career, w, outlook);
            }

            if (today.Month == 1) Intake(career);
        }

        public static void GoPartTime(Career career, Wrestler w)
        {
            w.PartTimeSince = career.CurrentDate;
            bool yours = career.ContractOf(w) is not null;
            career.AddNews($"{w.RingName} is cutting back to big shows only.", aboutYou: yours);
        }

        /// <summary>
        /// Gone: off every roster, their belts vacated, their teams broken up, their name kept.
        /// A retirement ends a deal without a payout or a non-compete — nobody is leaving for a
        /// rival.
        /// </summary>
        public static void Retire(Career career, Wrestler w, Outlook? why = null)
        {
            var today = career.CurrentDate;
            w.RetiredOn = today;
            w.PartTimeSince = null;

            bool yours = career.ContractOf(w) is { } mine;
            bool notable = yours || w.Overness >= 50 || career.Titles.HeldBy(w).Any();
            if (career.ContractOf(w) is { } contract)
            {
                career.Contracts.Remove(contract);
                career.Log(w, "retired");
            }
            career.VacateTitles(w, "Retired");
            foreach (var team in career.Teams.Where(t => t.IsActive && t.Members.Contains(w)))
                team.Disbanded = today;

            foreach (var rival in career.Rivals)
            {
                if (rival.ContractOf(w) is { } theirs)
                {
                    World.Vacate(career, rival, w, "Retired");
                    rival.Contracts.Remove(theirs);
                    notable |= rival.IsActive && w.Overness >= 40;
                }
                rival.Favourites.Remove(w.Id);
            }
            career.Bids.RemoveAll(b => b.WrestlerId == w.Id);
            World.ContractsChanged();

            if (notable)
            {
                int? age = w.AgeOn(today);
                string reason = why is { Why.Count: > 0 } ? $" ({string.Join(", ", why.Why)})" : "";
                career.AddNews($"{w.RingName} has retired{(age is { } a ? $" at {a}" : "")}{reason}.", aboutYou: yours);
            }
        }

        // ── The class ────────────────────────────────────────────────────────

        /// <summary>
        /// January's class of rookies: as many as it takes to bring the world back to the size it
        /// was made at, so retirements are replaced rather than the population drifting. They start
        /// at the bottom — nobody has heard of them — spread across the regions, the player's first.
        /// Most are ordinary; a few have real potential; one in a long while is a prodigy.
        /// </summary>
        public static List<Wrestler> Intake(Career career)
        {
            var today = career.CurrentDate;
            if (career.PopulationTarget <= 0) career.PopulationTarget = career.Roster.Count(w => !w.IsRetired);
            int size = career.PopulationTarget - career.Roster.Count(w => !w.IsRetired);
            var made = new List<Wrestler>();
            if (size <= 0) return made;

            int seed = StableSeed.From(career.WorldChoice?.Seed ?? 0, today.Year, "class");
            var rng = new Random(seed);
            var generator = new WrestlerGenerator(DataLoaders.LoadEmbeddedWrestlers(), career.Roster, rng.Next(), today.Year);
            var regions = Enum.GetValues<Region>();
            int start = (int)(career.Promotion.Region ?? 0);

            for (int i = 0; i < size; i++)
            {
                var division = rng.NextDouble() < 0.55 ? Division.Mens : Division.Womens;
                var w = generator.Make(division, PromotionTier.Local, regions[(start + i) % regions.Length], rookie: true);
                w.Development = Growth.Draw(w, today, new Random(StableSeed.From(w.Id, "growth")));
                Prospect(w, rng);
                w.Sharpness = RingCondition.OpeningSharpness(RingCondition.SelfMaintenance(w.Mental.Psychology, w.Mental.RingIQ));
                career.Roster.Add(w);
                made.Add(w);
            }

            if (made.Count > 0)
            {
                var talked = made.OrderByDescending(w => w.Development!.Ceiling.Sum()).Take(3).Select(w => w.RingName);
                career.AddNews($"{made.Count} rookies debut this year. The ones people are talking about: {string.Join(", ", talked)}.");
            }
            World.ContractsChanged();
            return made;
        }

        /// <summary>
        /// How much room a rookie has: about four in five ordinary, about one in six a real
        /// prospect, one in fifty a prodigy. Raises the ceilings, not where they start.
        /// </summary>
        private static void Prospect(Wrestler w, Random rng)
        {
            double roll = rng.NextDouble();
            double lift = roll < 0.02 ? 1.35 + 0.15 * rng.NextDouble()
                        : roll < 0.18 ? 1.12 + 0.18 * rng.NextDouble()
                        : 1.0 + 0.08 * rng.NextDouble();
            var d = w.Development!;
            for (int i = 0; i < d.Ceiling.Length; i++) d.Ceiling[i] = Math.Round(d.Ceiling[i] * lift, 1);
        }
    }
}
