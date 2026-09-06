using Xunit.Abstractions;
using WrestlingSim.Engine;
using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.MatchPlan;
using MatchPlanModel = WrestlingSim.Models.MatchPlan.MatchPlan;
using MatchType = WrestlingSim.Enums.MatchType;

namespace WrestlingSim.Tests
{
    public class ZzProbe(ITestOutputHelper o)
    {
        private static readonly List<Wrestler> R = DataLoaders.LoadEmbeddedWrestlers();

        [Fact]
        public void Probe()
        {
            var inv = new List<double>();
            var byDom = new Dictionary<ReactionKind,int>();
            int repFires = 0, beats = 0; var rep = new List<double>();

            foreach (var st in MatchStructureLibrary.ForSideSize(1).Where(x => !x.RequiresFeud))
            foreach (var a in R)
            foreach (var b in R)
            {
                if (ReferenceEquals(a,b)) continue;
                var plan = new MatchPlanModel { WrestlerA=a, WrestlerB=b, MatchType=MatchType.Standard,
                    Beats = st.Beats.Select(x=>x.Clone()).ToList() };
                var r = new MatchEngine(HashCode.Combine(st.Name,a.Id,b.Id)&0x7FFFFFFF).Execute(plan);
                inv.Add(r.Reaction.Investment);
                byDom[r.Reaction.Dominant] = byDom.GetValueOrDefault(r.Reaction.Dominant)+1;
                foreach (var br in r.BeatResults)
                {
                    beats++;
                    rep.Add(br.RepetitionFactor);
                }
            }

            inv.Sort();
            o.WriteLine($"  n={inv.Count}");
            foreach (var p in new[]{0.01,0.05,0.10,0.25,0.50,0.75,0.90,0.95,0.99})
                o.WriteLine($"  p{p*100:00} = {inv[(int)(inv.Count*p)]:F4}");
            o.WriteLine($"  mean = {inv.Average():F4}   min {inv[0]:F4}  max {inv[^1]:F4}");
            rep.Sort();
            o.WriteLine($"  RepetitionFactor: min {rep[0]:F3} p01 {rep[(int)(rep.Count*0.01)]:F3} " +
                        $"p05 {rep[(int)(rep.Count*0.05)]:F3} p10 {rep[(int)(rep.Count*0.10)]:F3} " +
                        $"p25 {rep[(int)(rep.Count*0.25)]:F3} median {rep[rep.Count/2]:F3}");
            foreach (var t in new[]{0.5,0.6,0.7,0.75,0.8,0.85,0.9})
                o.WriteLine($"    <{t:F2}: {rep.Count(x=>x<t)*100.0/rep.Count:F2}% of beats");
            foreach (var kv in byDom.OrderByDescending(k=>k.Value))
                o.WriteLine($"  dominant {kv.Key,-12} {kv.Value}");

            // What share sits at the current clamp bounds?
            const double typ = 0.50, swing = 0.70;
            int atCeil = inv.Count(x => 1.0+(x-typ)*swing >= 1.06);
            int atFloor= inv.Count(x => 1.0+(x-typ)*swing <= 0.65);
            o.WriteLine($"  at ceiling {atCeil*100.0/inv.Count:F1}%   at floor {atFloor*100.0/inv.Count:F1}%");
        }
    }
}
