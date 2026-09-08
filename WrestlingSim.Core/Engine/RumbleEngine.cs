using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Rumble;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Runs a battle royal or a Rumble.
    ///
    /// **The engine generates the eliminations; the booker does not write them.** That is the
    /// whole reason this is a separate class rather than a match format. Doc 18 §2.5 calls
    /// the format "barely a match", and a booker who had to author twenty-nine falls by hand
    /// would be doing a job nobody does in real life either — what they decide is who is in
    /// it, what order they come out, who wins, and a handful of moments. Everything between
    /// those is texture, and texture is what an engine is for.
    ///
    /// Who goes out when is weighted by connection rather than skill: this is a spectacle,
    /// the crowd's attention is on the people it came for, and a battle royal that dumps its
    /// biggest name at number four has thrown away the only thing it had. Deterministic from
    /// the seed, like everything else here.
    /// </summary>
    public class RumbleEngine(int seed)
    {
        private readonly Random _rng = new(seed);

        public RumbleResult Execute(RumblePlan plan)
        {
            var errors = plan.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    "Invalid rumble:\n" + string.Join("\n", errors.Select(e => "  • " + e)));

            var profiles = plan.Field.ToDictionary(
                e => e.Wrestler, e => new PerformerProfile(e.Wrestler));

            var order      = plan.Field.OrderBy(e => e.Number).ToList();
            var winner     = plan.Winner!;
            var inRing     = new List<Wrestler>();
            var eliminated = new List<RumbleElimination>();
            var commentary = new List<string>();
            var highlights = new List<string>();

            // How many entries each wrestler was still in for, so the iron-man run is read
            // off the match rather than taken on trust.
            var enteredAt = order.ToDictionary(e => e.Wrestler, e => e.Number);
            var leftAt    = new Dictionary<Wrestler, int>();

            var pending = new Queue<RumbleMoment>(plan.Moments.OrderBy(m => m.At));

            commentary.Add(plan.IsBattleRoyal
                ? $"All {plan.Field.Count} of them in the ring at once — over the top rope, " +
                  "both feet on the floor, and you are gone."
                : $"{plan.Field.Count} entrants, {plan.EntryIntervalSeconds} seconds apart. " +
                  "Over the top rope and out.");

            int entryIndex = 0;
            int totalEntries = order.Count;

            while (entryIndex < totalEntries || inRing.Count > 1)
            {
                // ── Somebody comes out ───────────────────────────────────────
                if (entryIndex < totalEntries)
                {
                    // A battle royal puts everybody in at once; a Rumble one at a time.
                    int coming = plan.IsBattleRoyal ? totalEntries : 1;
                    for (int i = 0; i < coming && entryIndex < totalEntries; i++, entryIndex++)
                    {
                        var e = order[entryIndex];
                        inRing.Add(e.Wrestler);

                        if (!plan.IsBattleRoyal)
                            commentary.Add(
                                e.IsSurprise
                                    ? $"That music is NOT on the sheet — and it is {e.Wrestler.RingName} at number {e.Number}!"
                                : e.NumberAnnounced
                                    // The building has had a week with this one. That is the
                                    // whole return on having televised the drawing, and it
                                    // gets said rather than only scored.
                                    ? $"Number {e.Number}: {e.Wrestler.RingName} — the number " +
                                      "this building has been talking about all week."
                                : e.Number <= 2
                                    // The draw is the story, so the bad one gets said. Coming
                                    // out at two is a long night that has not started yet.
                                    ? $"Number {e.Number}: {e.Wrestler.RingName} — and that is " +
                                      "about the worst number you can draw."
                                : e.Number >= totalEntries
                                    ? $"Number {e.Number}: {e.Wrestler.RingName}, last in and " +
                                      "the freshest man in this match by a distance."
                                    : $"Number {e.Number}: {e.Wrestler.RingName}.");
                    }
                }

                // ── A booked moment, if one is due ───────────────────────────
                double through = totalEntries <= 1 ? 1.0 : (double)entryIndex / totalEntries;
                while (pending.Count > 0 && pending.Peek().At <= through)
                {
                    var moment = pending.Dequeue();
                    string line = Narrate(moment, inRing, eliminated, ref commentary);
                    if (line.Length > 0) highlights.Add(line);
                }

                // ── And somebody goes out ────────────────────────────────────
                //
                // How full the ring is allowed to get before somebody has to go, and it is
                // the number that decides whether this reads as a Rumble at all.
                //
                // The first version kept two or three in there, which meant eliminations
                // started at the third entrant and the only people available to be dumped
                // were whoever had come out earliest. Entering at number one was a death
                // sentence — the exact opposite of the format's best story, and a test
                // measuring "do the big names last longest" reported them going out first
                // while the weighting that was supposed to protect them worked perfectly.
                // The bug was upstream of the weighting: nobody else was in the ring.
                //
                // A third of the field, six to ten, is roughly what a Rumble looks like.
                int target = plan.IsBattleRoyal
                    ? 1
                    : Math.Clamp(totalEntries / 3, 4, 10);
                int toGo   = entryIndex >= totalEntries ? inRing.Count - 1
                                                        : Math.Max(0, inRing.Count - target);

                for (int i = 0; i < toGo && inRing.Count > 1; i++)
                {
                    var going = PickVictim(inRing, winner, profiles);
                    var by    = PickEliminator(inRing, going, profiles);

                    inRing.Remove(going);
                    leftAt[going] = entryIndex;

                    eliminated.Add(new RumbleElimination
                    {
                        Wrestler  = going,
                        By        = by,
                        Order     = eliminated.Count + 1,
                        Remaining = inRing.Count
                    });

                    commentary.Add(by is null
                        ? $"{going.RingName} is gone."
                        : $"{by.RingName} dumps {going.RingName} over the top — {going.RingName} is out!");
                }

                if (entryIndex >= totalEntries && inRing.Count <= 1) break;
            }

            // ── Who went the distance ────────────────────────────────────────
            var ironMan = order
                .Select(e => (e.Wrestler, Outlasted: (leftAt.TryGetValue(e.Wrestler, out int l)
                                                        ? l : totalEntries) - e.Number))
                .OrderByDescending(x => x.Outlasted)
                .ThenByDescending(x => profiles[x.Wrestler].Connection)
                .First();

            commentary.Add($"{winner.RingName} wins it.");

            if (ironMan.Outlasted > 0 && ironMan.Wrestler != winner)
                commentary.Add($"But {ironMan.Wrestler.RingName} was out there from number " +
                               $"{enteredAt[ironMan.Wrestler]} — nobody lasted longer.");

            // ── Scoring ──────────────────────────────────────────────────────
            double fieldStarPower = plan.Field.Count == 0
                ? 0.0
                : plan.Field.Select(e => profiles[e.Wrestler].Connection)
                            .OrderByDescending(c => c)
                            .Take(Math.Max(1, plan.Field.Count / 3))
                            .Average();

            var kinds = plan.Moments.Select(m => m.Kind).ToList();

            // The iron-man run counts as a moment when it is a real one, which is what makes
            // it the one moment on the list a booker cannot simply ask for.
            double share = RumbleScoring.IronManShare(ironMan.Outlasted, totalEntries);
            if (share > 0.6) kinds.Add(RumbleMomentKind.IronMan);

            double moments = RumbleScoring.MomentScore(kinds);
            double entry = RumbleScoring.EntryStory(
                enteredAt[winner], totalEntries, plan.IsBattleRoyal,
                winner.Gimmick?.NaturalAlignment ?? Alignment.Face,
                profiles[winner].Conditioning);

            // What a televised drawing bought. Zero unless one happened, so a Rumble booked
            // without one scores exactly what it always did — the drawing is a thing to gain
            // rather than a tax on not having it.
            bool winnerAnnounced = order.First(e => e.Wrestler == winner).NumberAnnounced;
            int  announced       = order.Count(e => e.NumberAnnounced);
            double anticipation  = plan.IsBattleRoyal
                ? 0.0
                : RumbleScoring.Anticipation(winnerAnnounced, announced, totalEntries);

            if (winnerAnnounced)
                commentary.Add($"They have known {winner.RingName}'s number since the drawing, " +
                               "and they have been arguing about it ever since.");

            entry *= 1 + anticipation;

            return new RumbleResult
            {
                Winner           = winner,
                Eliminations     = eliminated,
                IronMan          = ironMan.Wrestler,
                IronManOutlasted = ironMan.Outlasted,
                Highlights       = highlights,
                Commentary       = commentary,
                MomentScore      = moments,
                FieldStarPower   = Math.Clamp(fieldStarPower, 0, 1),
                EntryStory       = entry,
                Anticipation     = anticipation,
                IronManShare     = plan.IsBattleRoyal ? 0.0 : share,
                FinalScore       = RumbleScoring.FinalScore(
                    moments, Math.Clamp(fieldStarPower, 0, 1),
                    entry, plan.IsBattleRoyal ? 0.0 : share)
            };
        }

        /// <summary>
        /// Who goes out next. Never the booked winner, and the crowd's favourites last —
        /// a battle royal that dumps its biggest name early has spent the only thing it had.
        /// </summary>
        private Wrestler PickVictim(List<Wrestler> inRing, Wrestler winner,
                                    Dictionary<Wrestler, PerformerProfile> profiles)
        {
            var candidates = inRing.Where(w => w != winner).ToList();
            if (candidates.Count == 0) return inRing[0];

            // Weight towards the least connected, with enough noise that it is not simply
            // the roster in order.
            var weighted = candidates
                .Select(w => (W: w, Weight: RumbleScoring.EliminationRisk(profiles[w].Connection)
                                            + _rng.NextDouble() * 0.5))
                .OrderByDescending(x => x.Weight)
                .ToList();

            return weighted[0].W;
        }

        /// <summary>Who did the dumping. Nobody, sometimes — in a pile-up nobody owns it.</summary>
        private Wrestler? PickEliminator(List<Wrestler> inRing, Wrestler going,
                                         Dictionary<Wrestler, PerformerProfile> profiles)
        {
            var others = inRing.Where(w => w != going).ToList();
            if (others.Count == 0) return null;
            if (_rng.NextDouble() < 0.15) return null;

            return others
                .Select(w => (W: w, Weight: profiles[w].Connection + _rng.NextDouble() * 0.6))
                .OrderByDescending(x => x.Weight)
                .First().W;
        }

        /// <summary>Says a booked moment out loud, and reports it as a highlight.</summary>
        private static string Narrate(RumbleMoment moment, List<Wrestler> inRing,
                                      List<RumbleElimination> gone, ref List<string> commentary)
        {
            string Name(int i) => moment.Cast.Count > i ? moment.Cast[i].RingName : "somebody";

            string line = moment.Kind switch
            {
                RumbleMomentKind.SurpriseReturn =>
                    $"The place has come apart — {Name(0)} is BACK, and nobody saw it coming!",
                RumbleMomentKind.MassElimination =>
                    $"{Name(0)} is clearing house! Bodies going over the top one after another!",
                RumbleMomentKind.Showdown =>
                    $"{Name(0)} and {Name(1)} — and everything else in this ring has stopped.",
                RumbleMomentKind.Betrayal =>
                    $"{Name(0)} has just thrown out {Name(1)}! Their own partner!",
                RumbleMomentKind.NearElimination =>
                    $"{Name(0)} is hanging on — one hand, both feet off the floor!",
                RumbleMomentKind.IronMan =>
                    $"{Name(0)} has been in this from the start and is still going.",
                _ => ""
            };

            if (line.Length > 0) commentary.Add(line);
            return line;
        }
    }
}
