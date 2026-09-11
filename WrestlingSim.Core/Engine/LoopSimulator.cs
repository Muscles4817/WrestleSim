using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Runs a house show loop: a cast, a number of towns, and nothing on the line.
    ///
    /// **This is the only engine in the game that does not produce a rating**, and that is the
    /// point rather than an omission. `ShowType.HouseShow` is defined as the night the
    /// television audience was not at, so there is no score to give and nothing for one to
    /// mean. What a loop produces is condition: sharpness up, fatigue up, and the injury risk
    /// that comes with being in a ring at all.
    ///
    /// It therefore moves no overness, no momentum, no feud heat and no titles, and it does
    /// not write <c>LastAppearance</c> — so the absence clock keeps running on a wrestler who
    /// is working six towns a week. Being in a ring and being on television are different
    /// currencies, and the loop only pays in the first one.
    ///
    /// **The nights are run one at a time rather than multiplied out**, because both meters
    /// are non-linear in the thing they read. Fatigue makes the next night more dangerous
    /// (`InjuryRisk.FatigueRisk`), sharpness saturates as it climbs, and a wrestler hurt in
    /// town two does not work towns three and four. Multiplying a single night by four would
    /// get all three of those wrong in the direction that flatters the booker.
    /// </summary>
    public sealed class LoopSimulator
    {
        private readonly Random _rand;
        private readonly PromotionTier _tier;

        public LoopSimulator(int? seed = null, PromotionTier tier = PromotionTier.Established)
        {
            _rand = seed is { } s ? new Random(s) : new Random();
            _tier = tier;
        }

        public LoopResult Run(HouseShowLoop loop, DateOnly date)
        {
            var result = new LoopResult
            {
                Towns           = loop.Towns,
                MinutesPerNight = loop.MinutesPerNight
            };

            double pace = RingCondition.IntensityWeight(loop.Pace);

            foreach (var wrestler in loop.Cast)
            {
                double sharpBefore = wrestler.Sharpness;
                double tiredBefore = wrestler.Fatigue;

                int  worked = 0;
                int? hurtOn = null;

                for (int night = 1; night <= loop.NightsEach; night++)
                {
                    // Somebody carrying an injury does not go out for the next town.
                    if (wrestler.Injury is { } carried && carried.KeepsOut(date)) break;

                    Work(wrestler, loop.MinutesPerNight, pace);
                    worked++;

                    // No `break` here. The guard at the top of the loop already refuses to send
                    // out somebody carrying an injury, and the one just applied is carried —
                    // so an explicit second statement of "hurt people do not work" could only
                    // ever agree with it, or drift from it.
                    if (RollInjury(wrestler, loop.Pace, night, date) is { } report)
                    {
                        result.Injuries.Add(report);
                        hurtOn = night;
                    }
                }

                result.Workers.Add(new LoopWorker
                {
                    Wrestler        = wrestler,
                    SharpnessBefore = sharpBefore,
                    SharpnessAfter  = wrestler.Sharpness,
                    FatigueBefore   = tiredBefore,
                    FatigueAfter    = wrestler.Fatigue,
                    HurtOnNight     = hurtOn,
                    NightsWorked    = worked
                });
            }

            return result;
        }

        /// <summary>
        /// One night in one town. The same billing a televised match does, because it is the
        /// same thing happening to the same body — a house show match is not a lesser kind of
        /// match, it is a match nobody filmed.
        /// </summary>
        private static void Work(Wrestler w, double minutes, double pace)
        {
            double conditioning = new PerformerProfile(w).BaseConditioning;
            double upkeep = RingCondition.SelfMaintenance(
                w.Mental?.Psychology ?? 70, w.Mental?.RingIQ ?? 70);

            w.Fatigue = Math.Clamp(
                w.Fatigue + RingCondition.MatchCost(minutes, pace, w.Style, 1.0, conditioning), 0, 100);

            w.Sharpness = Math.Clamp(
                w.Sharpness + RingCondition.SharpnessGain(minutes, pace, upkeep), 0, 100);
        }

        /// <summary>
        /// Whether tonight hurt them, at the same per-beat rate a televised match uses.
        ///
        /// <c>InjuryRisk.PerBeat</c> is priced per beat, and a night is not one beat — so the
        /// nightly roll is the per-beat risk across the beats a match of this length holds.
        /// Charging it once a night would make the loop the safest place in the game to put
        /// anybody, which is the opposite of what a road schedule is.
        /// </summary>
        private InjuryReport? RollInjury(Wrestler w, BeatIntensity pace, int night, DateOnly date)
        {
            var part = InjuryRisk.PickPart(w, _rand);

            // Beats in a night, at the engine's own rough rate of one per two minutes. The
            // beat index passed in is the middle of the match rather than the start, because
            // the risk curve reads it as "how deep into this are they".
            int beats = Math.Max(1, BeatsPerNight);
            double conditioning = new PerformerProfile(w).BaseConditioning;

            for (int beat = 0; beat < beats; beat++)
            {
                double risk = InjuryRisk.PerBeat(
                    w, pace, beat, conditioning, opponent: null, _tier, part);

                if (_rand.NextDouble() >= risk) continue;

                var (low, high) = InjuryRisk.WeeksOut(part);
                int weeks = _rand.Next(low, high + 1);

                var injury = new Injury
                {
                    Part      = part,
                    Sustained = date,
                    ClearedOn = date.AddDays(weeks * 7),
                    WeeksOut  = weeks
                };

                w.Injury = injury;
                w.InjuryHistory.Add(injury);

                return new InjuryReport
                {
                    Wrestler = w,
                    Injury   = injury,
                    Repeat   = w.InjuryHistory.Count(i => i.Part == part) > 1
                };
            }

            return null;
        }

        /// <summary>Beats in a house show match, at the engine's rate of one per two minutes.</summary>
        private const int BeatsPerNight = 6;
    }
}
