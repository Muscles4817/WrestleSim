using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    public class Feud
    {
        public required Wrestler WrestlerA { get; init; }
        public required Wrestler WrestlerB { get; init; }
        public FeudIntensity Intensity { get; set; }
        public List<FeudHistoryTag> History { get; set; } = new();

        /// <summary>
        /// Lifetime meetings between these two, for display and history. This is the
        /// honest tally and it never falls — what the crowd still remembers is
        /// <see cref="RememberedMeetings"/>, which is a different number.
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// The date they last wrestled, if that match was on a dated card. Null for
        /// exhibition matches, which have no place on a calendar.
        /// </summary>
        public DateOnly? LastMatchDate { get; private set; }

        /// <summary>
        /// How many of their meetings the audience still had in mind as of
        /// <see cref="LastMatchDate"/>. Fractional, because forgetting is gradual.
        /// Read it through <see cref="MeetingsRemembered"/>, which ages it to a date.
        /// </summary>
        public double RememberedMeetings { get; private set; }

        /// <summary>
        /// Accumulated heat from booked segments and matches. Intensity is derived from
        /// this, so a feud is something you build by booking rather than something you declare.
        /// </summary>
        public double Heat { get; private set; }

        // Heat required to reach each intensity tier.
        public const double ColdThreshold     = 5;
        public const double BuildingThreshold = 15;
        public const double HotThreshold      = 30;
        public const double NuclearThreshold  = 50;

        public double IntensityMultiplier => Intensity switch
        {
            FeudIntensity.None     => 1.00,
            FeudIntensity.Cold     => 1.05,
            FeudIntensity.Building => 1.15,
            FeudIntensity.Hot      => 1.30,
            FeudIntensity.Nuclear  => 1.50,
            _                      => 1.00
        };

        // Crowd energy bonus at match start from feud heat
        public double StartingEnergyBonus => Intensity switch
        {
            FeudIntensity.Cold     => 3,
            FeudIntensity.Building => 7,
            FeudIntensity.Hot      => 12,
            FeudIntensity.Nuclear  => 18,
            _                      => 0
        };

        public bool HasTag(FeudHistoryTag tag) => History.Contains(tag);

        // ── Match-count decay ────────────────────────────────────────────────
        // docs/wrestling-reference/20-storylines-and-feuds.md §9.1 and
        // docs/wrestling-reference/17-heat-and-getting-over.md §4.
        //
        // A specific match-up is the fastest-decaying thing in the business: doc 17 §4.1
        // gives it 2–4 encounters before it needs a stipulation or a gap. The 4th meeting
        // between the same two people draws roughly half to two thirds of what the 1st did.

        /// <summary>
        /// Days a pairing can sit idle before the audience starts forgetting it at all.
        ///
        /// Roughly one pay-per-view cycle. The point of the grace period is that a feud
        /// run at a normal pace — weekly television, a monthly blow-off — must not be
        /// allowed to launder its own repetition. Three matches in three months is three
        /// matches, not one.
        /// </summary>
        public const int FreshnessGraceDays = 60;

        /// <summary>
        /// Days of further idleness that wipe one remembered meeting.
        ///
        /// Four months. Doc 17 §4.1 gives a new character 6–18 months of freshness and
        /// calls a single match-up the quickest-decaying thing on the list, so a meeting
        /// should fade faster than that. In practice this means a three-match series left
        /// alone for eight months reads as roughly a second meeting rather than a fourth,
        /// which is the behaviour doc 17 §4.2 asks for: absence is the main tool.
        /// </summary>
        public const int FreshnessRecoveryDays = 120;

        /// <summary>
        /// How many meetings the crowd still has in mind on a given date. Fractional.
        /// Pass null when there is no world clock — an exhibition match — in which case
        /// nothing is forgotten.
        /// </summary>
        public double MeetingsRemembered(DateOnly? today)
        {
            if (LastMatchDate is not { } last || today is not { } now) return RememberedMeetings;

            int idle = Math.Max(0, now.DayNumber - last.DayNumber);
            double forgotten = Math.Max(0, idle - FreshnessGraceDays) / (double)FreshnessRecoveryDays;
            return Math.Max(0, RememberedMeetings - forgotten);
        }

        /// <summary>
        /// Which meeting the next match between these two would read as, from the
        /// audience's point of view. 1.0 means they have never seen it.
        /// </summary>
        public double NextMeetingNumber(DateOnly? today) => MeetingsRemembered(today) + 1.0;

        /// <summary>
        /// Whether a third meeting would read as the blow-off. There are no stipulations
        /// in the game yet, so heat stands in for one: a rivalry the crowd is actually
        /// invested in has an ending worth turning up for, a lukewarm one is just another
        /// match on the card.
        /// </summary>
        public bool ReadsAsBlowOff => Intensity >= FeudIntensity.Hot;

        /// <summary>
        /// What this pairing is worth relative to the first time the crowd saw it, on a
        /// given date. 1.0 = as good as new.
        /// </summary>
        public double Familiarity(DateOnly? today) =>
            FamiliarityFor(NextMeetingNumber(today), ReadsAsBlowOff);

        /// <summary>
        /// The decay curve from doc 20 §9.1, as relative draw against the first meeting:
        ///
        ///   1st  100%
        ///   2nd   90%   (doc: 85–95%)
        ///   3rd   85%, or 100% as a blow-off  (doc: 90–110%, the stipulation adds value)
        ///   4th   65%   (doc: 50–70% and falling)
        ///   5th   57%
        ///   6th   50%
        ///   7th+  45%   floor — a match nobody wants still happens in front of somebody
        ///
        /// Interpolated between the whole numbers because a pairing recovers gradually, so
        /// a part-forgotten series lands between two rows of the table rather than
        /// snapping from one to the next.
        /// </summary>
        public static double FamiliarityFor(double meetingNumber, bool blowOff = false)
        {
            double[] curve = [1.00, 1.00, 0.90, blowOff ? 1.00 : 0.85, 0.65, 0.57, 0.50, 0.45];

            double n = Math.Max(1.0, meetingNumber);
            if (n >= curve.Length - 1) return curve[^1];

            int lower = (int)n;
            double t = n - lower;
            return curve[lower] + (curve[lower + 1] - curve[lower]) * t;
        }

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Adds heat and re-derives Intensity. Returns true if the feud moved up a tier,
        /// so callers can report the escalation to the player.
        /// </summary>
        public bool AddHeat(double amount)
        {
            if (amount <= 0) return false;

            var before = Intensity;
            Heat += amount;
            Intensity = IntensityFor(Heat);
            return Intensity > before;
        }

        /// <summary>
        /// Sets Heat directly and re-derives Intensity. For loading a save only — normal
        /// play must go through AddHeat so a feud is something you book, not something
        /// you assign.
        /// </summary>
        public void RestoreHeat(double heat)
        {
            Heat = Math.Max(0, heat);
            Intensity = IntensityFor(Heat);
        }

        /// <summary>
        /// Books one more match between these two. Banks what the crowd still remembers
        /// at that date and adds this meeting to it, so time already served is credited
        /// once and then the clock restarts from here.
        ///
        /// Pass the date of the card. Null for an exhibition, which has no calendar and
        /// therefore no way to recover freshness.
        /// </summary>
        public void RecordMatch(DateOnly? date)
        {
            RememberedMeetings = MeetingsRemembered(date) + 1.0;
            MatchCount++;
            if (date is { } d) LastMatchDate = d;
        }

        /// <summary>
        /// Sets the remembered-meeting state directly. For loading a save only, in the
        /// same spirit as <see cref="RestoreHeat"/>.
        /// </summary>
        public void RestoreMeetings(double remembered, DateOnly? lastMatchDate)
        {
            RememberedMeetings = Math.Max(0, remembered);
            LastMatchDate      = lastMatchDate;
        }

        /// <summary>
        /// Ends the feud, keeping its history. The two have not stopped having been
        /// rivals — they have stopped having anywhere to be rivals, which is what a draft
        /// does to a pairing it separates
        /// (docs/wrestling-reference/22-brand-splits.md §5.1).
        /// </summary>
        public void Conclude()
        {
            Heat = 0;
            Intensity = FeudIntensity.None;
        }

        /// <summary>Stamps a history tag onto the feud. Duplicates are ignored.</summary>
        public bool AddTag(FeudHistoryTag tag)
        {
            if (History.Contains(tag)) return false;
            History.Add(tag);
            return true;
        }

        /// <summary>
        /// Forces the feud to at least the given intensity, topping up Heat to match.
        /// Used when the player sets a feud up by hand rather than booking it.
        /// </summary>
        public void SetMinimumIntensity(FeudIntensity intensity)
        {
            double required = HeatFor(intensity);
            if (Heat < required) Heat = required;
            if (Intensity < intensity) Intensity = intensity;
        }

        public static FeudIntensity IntensityFor(double heat) => heat switch
        {
            >= NuclearThreshold  => FeudIntensity.Nuclear,
            >= HotThreshold      => FeudIntensity.Hot,
            >= BuildingThreshold => FeudIntensity.Building,
            >= ColdThreshold     => FeudIntensity.Cold,
            _                    => FeudIntensity.None
        };

        public static double HeatFor(FeudIntensity intensity) => intensity switch
        {
            FeudIntensity.Nuclear  => NuclearThreshold,
            FeudIntensity.Hot      => HotThreshold,
            FeudIntensity.Building => BuildingThreshold,
            FeudIntensity.Cold     => ColdThreshold,
            _                      => 0
        };

        /// <summary>Heat still needed before the next tier unlocks; null at Nuclear.</summary>
        public double? HeatToNextTier => Intensity switch
        {
            FeudIntensity.None     => ColdThreshold - Heat,
            FeudIntensity.Cold     => BuildingThreshold - Heat,
            FeudIntensity.Building => HotThreshold - Heat,
            FeudIntensity.Hot      => NuclearThreshold - Heat,
            _                      => null
        };

        public bool Involves(Wrestler w) => w == WrestlerA || w == WrestlerB;

        public override string ToString() =>
            $"{WrestlerA.RingName} vs {WrestlerB.RingName} — {Intensity} ({Heat:F0} heat)";
    }
}
