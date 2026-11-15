using System.Diagnostics;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.World;
using WrestlingSim.Persistence;
using Xunit;

namespace WrestlingSim.Tests
{
    /// <summary>
    /// A measuring tool, not a test: makes a real world of each size (not a stand-in) and
    /// reports what it looks like and what it costs. Does nothing unless told where to write.
    /// <code>MAKE_LOG=/tmp/make.log MAKE_SIZE=Full MAKE_SOURCE=Database dotnet test --filter "FullyQualifiedName~WorldMakingProbe"</code>
    /// </summary>
    public class WorldMakingProbe
    {
        [Fact]
        public void Make()
        {
            string log = Environment.GetEnvironmentVariable("MAKE_LOG") ?? "";
            if (log.Length == 0) return;
            var size = Enum.Parse<WorldSize>(Environment.GetEnvironmentVariable("MAKE_SIZE") ?? "Full");
            var source = Enum.Parse<WorldSource>(Environment.GetEnvironmentVariable("MAKE_SOURCE") ?? "Database");
            var tier = Enum.Parse<PromotionTier>(Environment.GetEnvironmentVariable("MAKE_TIER") ?? "Independent");
            int weeks = int.Parse(Environment.GetEnvironmentVariable("MAKE_WEEKS") ?? "4");
            var lines = new List<string>();
            void Say(string s) { lines.Add(s); File.WriteAllLines(log, lines); }

            var day = new DateOnly(2026, 1, 5);
            var sw = Stopwatch.StartNew();
            var database = DataLoaders.LoadEmbeddedWrestlers();
            var career = new Career
            {
                Promotion = new Promotion { Name = "Probe Wrestling", Tier = tier, Region = Region.Midwest },
                StartDate = day, CurrentDate = day, Roster = DataLoaders.LoadEmbeddedWrestlers()
            };
            var choice = new WorldChoice(size, source, StableSeed.From("probe"));
            World.Populate(career, choice, database);
            career.OpenBooks(day);
            career.Titles.SeedDefaults(career.Promotion.Name, day);
            World.Open(career, day, choice);
            Say($"{size}/{source}: {career.Roster.Count} wrestlers ({career.Roster.Count(w => w.IsGenerated)} generated), {career.Rivals.Count} rivals; made in {sw.ElapsedMilliseconds} ms");

            foreach (var g in career.Rivals.GroupBy(r => r.Promotion.Tier).OrderByDescending(g => g.Key))
            {
                var sizes = g.Select(r => r.Contracts.Count).ToList();
                var over = g.SelectMany(r => r.Contracts).Select(c => career.FindWrestler(c.WrestlerId)!.Overness).OrderBy(o => o).ToList();
                Say($"  {g.Key}: {g.Count()} promotions, rosters {sizes.Min()}–{sizes.Max()}, overness median {over[over.Count / 2]:0} top {over[^1]:0}; e.g. {string.Join(", ", g.Take(3).Select(r => r.Name))}");
            }
            Say($"  player ({tier}): {career.Contracts.Count} on the books");
            var free = career.Roster.Count(w => career.SignedTo(w) is null && !career.IsGuaranteed(w));
            Say($"  not held exclusively: {free}");
            var sample = career.Roster.Where(w => w.IsGenerated).Take(5);
            foreach (var w in sample)
                Say($"  e.g. {w.RingName} ({w.RealName}), {w.Division}, {w.Region}, born {w.BornIn}, over {w.Overness:0}, skill {w.RingSkills.GetOverallSkill():0.0}, cha {w.Charisma}, {w.Gimmick.Type} '{w.Gimmick.PersonaDescriptor}', finisher {w.Signature.FirstOrDefault()?.Name}");

            sw.Restart();
            int steps = World.PlanOpening(career).Count();
            Say($"  opening plans: {steps} promotions in {sw.ElapsedMilliseconds} ms");

            string Parts(RivalPromotion r)
            {
                var free = World.FreeAgents(career).Where(w => r.ContractOf(w) is null).ToList();
                string Of(IEnumerable<Wrestler> ws) => string.Join(",", ws.Select(w => $"{w.Id}:{w.EffectiveOverness:0.0}"));
                return $"names[{Of(free.Take(5))}] ring[{Of(World.BestInTheRing(free).Take(3))}] value[{Of(World.BestValue(career, free).Take(3))}]";
            }

            for (int week = 0; week < weeks; week++)
            {
                var before = career.Rivals.Where(r => r.Promotion.HasTelevision).ToDictionary(r => r.Id, r => (
                    Roster: string.Join(",", r.Contracts.Select(c => c.WrestlerId).OrderBy(x => x)),
                    Short: string.Join(",", World.Opportunity(career, r)),
                    Cash: (int)Math.Floor(r.Books.Cash / Math.Max(1m, r.Books.OpeningCash) * 10m),
                    Expiring: career.ExpiringWithin(TalentMarket.ExpiryWarningDays).Count(c => c.IsGuaranteed)));
                var indie = career.Rivals.Where(r => !r.Promotion.HasTelevision && r.IsActive).ToDictionary(r => r.Id, r => (
                    Roster: string.Join(",", r.Contracts.Select(c => c.WrestlerId).OrderBy(x => x)),
                    Cash: r.Books.Cash < 0 ? "debt" : r.Books.Cash < r.Books.OpeningCash / 2 ? "short" : "fine"));
                World.Phases = new();
                sw.Restart();
                for (int d = 0; d < 7; d++) career.AdvanceOneDay();
                long total = sw.ElapsedMilliseconds;
                var parts = World.Phases.OrderByDescending(p => p.Value.Ticks)
                    .Select(p => $"{p.Key} {p.Value.Ticks * 1000 / Stopwatch.Frequency} ms/{p.Value.Count}");
                Say($"  week {week + 1}: {total} ms — {string.Join(", ", parts)}");
                World.Phases = null;
                int roster = 0, shortl = 0, cash = 0, exp = 0;
                foreach (var r in career.Rivals.Where(r => r.Promotion.HasTelevision))
                {
                    var b = before[r.Id];
                    if (b.Roster != string.Join(",", r.Contracts.Select(c => c.WrestlerId).OrderBy(x => x))) roster++;
                    var now = string.Join(",", World.Opportunity(career, r));
                    if (b.Short != now) { shortl++; if (shortl == 1) Say($"      was {b.Short}\n      now {now}"); }
                    if (b.Cash != (int)Math.Floor(r.Books.Cash / Math.Max(1m, r.Books.OpeningCash) * 10m)) cash++;
                }
                Say($"    of {before.Count} TV promotions, changed this week: roster {roster}, opportunity {shortl}, cash step {cash}");
                int iRoster = 0, iCash = 0;
                foreach (var r in career.Rivals.Where(r => !r.Promotion.HasTelevision && r.IsActive))
                {
                    var b = indie[r.Id];
                    var nowIds = r.Contracts.Select(c => c.WrestlerId).OrderBy(x => x).ToList();
                    if (b.Roster != string.Join(",", nowIds))
                    {
                        iRoster++;
                        if (iRoster <= 2)
                        {
                            var was = b.Roster.Split(',').ToHashSet();
                            Say($"      {r.Name}: in {string.Join(" ", nowIds.Where(x => !was.Contains(x)).Take(4))} out {string.Join(" ", was.Where(x => !nowIds.Contains(x)).Take(4))}; last decision {r.Decisions.LastOrDefault()?.Date} {r.Decisions.LastOrDefault()?.Move}");
                        }
                    }
                    if (b.Cash != (r.Books.Cash < 0 ? "debt" : r.Books.Cash < r.Books.OpeningCash / 2 ? "short" : "fine")) iCash++;
                }
                int iTop = career.Rivals.Where(r => !r.Promotion.HasTelevision && r.IsActive).Count(r => r.QuietSince is not { } q || (career.CurrentDate.DayNumber - q.DayNumber) <= 7);
                Say($"    of {indie.Count} indies, changed this week: roster {iRoster}, money state {iCash}; looked this week {iTop}");
            }
            sw.Restart();
            var json = SaveSerializer.ToJson(career);
            Say($"  save {json.Length / 1024:N0} KB in {sw.ElapsedMilliseconds} ms; active rivals {career.Rivals.Count(r => r.IsActive)}");
        }
    }
}
