using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    public class Feud
    {
        /// <summary>
        /// The camps in the feud. A singles rivalry is two camps of one; a team rivalry is
        /// two camps of several, and is genuinely its own thing rather than the sum of the
        /// four singles feuds inside it — the crowd's appetite for The Usos vs The New Day
        /// is separate from its appetite for any one of those men against any other.
        ///
        /// **There can be more than two.** A three-way programme is one story with three
        /// camps, not three separate rivalries that happen to overlap: when Rock, Triple H
        /// and Foley are in a triangle, "Rock vs Foley" is not a thing anybody was following
        /// on its own. Faction warfare is the same shape with more bodies, and a betrayal is
        /// a camp splitting rather than a new feud starting.
        ///
        /// <see cref="SideA"/> and <see cref="SideB"/> are shims over the first two, so every
        /// existing two-camp feud, save and test keeps working untouched.
        /// </summary>
        public List<List<Wrestler>> Camps { get; init; } = [new(), new()];

        /// <summary>The first camp. Shim over <see cref="Camps"/>.</summary>
        public List<Wrestler> SideA
        {
            get => Camps[0];
            init { while (Camps.Count < 1) Camps.Add(new()); Camps[0] = value; }
        }

        /// <summary>The second camp. Shim over <see cref="Camps"/>.</summary>
        public List<Wrestler> SideB
        {
            get => Camps[1];
            init { while (Camps.Count < 2) Camps.Add(new()); Camps[1] = value; }
        }

        /// <summary>Everyone in the feud, whichever camp they are in.</summary>
        public IEnumerable<Wrestler> Participants => Camps.SelectMany(c => c);

        /// <summary>True when the story has more than two camps in it.</summary>
        public bool IsMultiParty => Camps.Count > 2;

        /// <summary>Which camp this wrestler is in, or null if they are not in the feud.</summary>
        public int? CampOf(Wrestler w)
        {
            for (int i = 0; i < Camps.Count; i++)
                if (Camps[i].Contains(w)) return i;
            return null;
        }

        /// <summary>
        /// True when these two are on opposite sides of this story.
        ///
        /// Not the same as both being in it: two members of the same faction are in the feud
        /// together and have no grievance with each other, which is the distinction that makes
        /// a betrayal mean something — before it they share a camp, after it they do not.
        /// </summary>
        public bool Opposes(Wrestler a, Wrestler b) =>
            CampOf(a) is { } x && CampOf(b) is { } y && x != y;

        /// <summary>Shim over <see cref="SideA"/>; see <see cref="Models.MatchPlan.MatchPlan"/>.</summary>
        public Wrestler WrestlerA
        {
            get => SideA[0];
            init => SideA.Add(value);
        }

        public Wrestler WrestlerB
        {
            get => SideB[0];
            init => SideB.Add(value);
        }

        /// <summary>True when any camp in the rivalry is a team.</summary>
        public bool IsTeamFeud => Camps.Any(c => c.Count > 1);

        public string SideAName => string.Join(" & ", SideA.Select(w => w.RingName));
        public string SideBName => string.Join(" & ", SideB.Select(w => w.RingName));
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
        /// <summary>
        /// What the feud puts in the room before the bell, after distrust. A crowd that has
        /// been shown five unfinished stories brings less to the sixth.
        /// </summary>
        public double StartingEnergyBonus => RawStartingEnergyBonus * Credibility;

        private double RawStartingEnergyBonus => Intensity switch
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

            // New heat on a settled feud starts a new chapter. Without this a blow-off was
            // permanent in the wrong sense: `GetOrCreate` returns the same object for a
            // pairing forever, so two people who ever finished a programme could never
            // feud again — and the rematch years later is one of the oldest things in
            // wrestling. Distrust deliberately does *not* clear: what the booker taught the
            // crowd about whether their stories finish outlives the story.
            if (Concluded)
            {
                Concluded       = false;
                MatchesSinceHot = 0;
                ChaptersSettled++;

                // Doc 31's A3 brief also asks that **continuing past the blow-off be
                // penalised**, and the first version made it free — which is worse than
                // not having a blow-off, because it lets a booker take the payoff and
                // keep the programme.
                //
                // The cost is time-sensitive, because the two cases are genuinely
                // different. Restarting a fortnight after the cage match is telling the
                // audience the ending they were sold did not count, and it is exactly
                // doc 20 §6.2's scarcity argument — a stipulation used again immediately
                // means nothing. Reviving the same rivalry two years later is one of the
                // oldest and best things in wrestling and should cost nothing at all.
                if (ConcludedOn is { } settled && LastAdvanced is { } now
                    && now.DayNumber - settled.DayNumber < RespectTheEndingDays)
                    Distrust = Math.Clamp(Distrust + ReopenedTooSoonDistrust, 0, 1);
            }

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

            // Not the same thing as a blow-off, deliberately: nothing was *settled*, the
            // two were separated. So `Concluded` stays false — these two still have
            // unfinished business if a later draft puts them back together — but the
            // patience clock resets, because a programme that was taken off the player is
            // not one the player refused to pay off.
            MatchesSinceHot = 0;
            DecayedTo       = null;
        }

        // ── Decay ────────────────────────────────────────────────────────────

        /// <summary>
        /// Marks the feud as advanced today, restarting the decay clock. Called whenever
        /// anything happens between these two on screen — a segment, a match, a run-in.
        /// Deliberately separate from <see cref="AddHeat"/>: a beatdown that adds no heat
        /// because the feud is already Nuclear is still television, and still keeps the
        /// story in front of the audience.
        /// </summary>
        public void Advance(DateOnly? date)
        {
            if (date is not { } d) return;
            LastAdvanced = d;
            DecayedTo    = null;
        }

        /// <summary>Days a feud can go unadvanced before it starts bleeding heat.</summary>
        public const int HeatGraceDays = 14;

        /// <summary>
        /// Heat kept per day once past the grace. Doc 20 §9 lists "a feud left off TV for
        /// three weeks loses its heat" as one of the things that kills them.
        ///
        /// Measured against that, not asserted: three weeks idle is seven days past the
        /// grace and costs **27.6%**; a month costs 51%; two months costs 88%. The first
        /// draft of this comment said three weeks "should cost most of it", which the
        /// constant does not do and review caught. Fourteen days of grace is what makes the
        /// three-week figure modest — a feud is not punished for missing one week of
        /// television, and doc 20 §9 does not say it should be.
        /// </summary>
        public const double HeatDailyRetention = 0.955;

        /// <summary>The last day anything advanced this feud — a match or a segment.</summary>
        public DateOnly? LastAdvanced { get; private set; }

        /// <summary>How far decay has already been charged, so the clock cannot double-bill.</summary>
        public DateOnly? DecayedTo { get; set; }

        /// <summary>
        /// Bleeds heat for time spent ignored. Called by the world clock.
        ///
        /// Heat only ever accumulated before this. That made a feud a ratchet: every
        /// segment you ever booked was still paying off months later, and there was no cost
        /// to starting five stories and finishing none.
        /// </summary>
        public void ApplyDailyDecay(DateOnly today)
        {
            if (Concluded || Heat <= 0) return;
            if (LastAdvanced is not { } last) return;

            var from = DecayedTo ?? last.AddDays(HeatGraceDays);
            if (today <= from) return;

            int days = today.DayNumber - from.DayNumber;
            Heat = Math.Max(0, Heat * Math.Pow(HeatDailyRetention, days));
            Intensity = IntensityFor(Heat);
            DecayedTo = today;

            // A feud that has cooled below Hot is not being told any more, so the clock
            // measuring "how long have you been refusing to pay this off" stops and resets.
            // Distrust already accrued stays — the crowd learned something — but a
            // programme that quietly died should not have its next chapter start one match
            // away from the patience limit.
            if (Intensity < FeudIntensity.Hot) MatchesSinceHot = 0;
        }

        // ── The blow-off ─────────────────────────────────────────────────────

        /// <summary>True once this feud has been paid off and ended.</summary>
        public bool Concluded { get; private set; }

        /// <summary>When the most recent chapter was settled.</summary>
        public DateOnly? ConcludedOn { get; private set; }

        /// <summary>How many chapters of this rivalry have been paid off and reopened.</summary>
        public int ChaptersSettled { get; private set; }

        /// <summary>
        /// Matches worked since this feud became worth blowing off. Doc 20 §9.1: three
        /// matches is the natural life of a feud, and the third is the one that should end
        /// it.
        /// </summary>
        public int MatchesSinceHot { get; private set; }

        /// <summary>
        /// 0–1. How much the audience has stopped believing this story is going anywhere.
        ///
        /// Doc 20 §9 lists the interference loop — every match ends in a run-in, nothing
        /// resolves — as a feud killer, and §6.1 says a blow-off must *resolve*. A booker
        /// who keeps escalating and never pays off is teaching the crowd not to invest,
        /// and that lesson outlives the feud: distrust suppresses what this pairing can
        /// ever draw again.
        /// </summary>
        public double Distrust { get; private set; }

        /// <summary>Matches past the third before distrust starts accruing.</summary>
        public const int PatienceMatches = 3;

        /// <summary>
        /// How long an ending is owed before restarting the same programme reads as a
        /// revival rather than as the ending not having counted. Six months.
        /// </summary>
        public const int RespectTheEndingDays = 180;

        /// <summary>What restarting a settled feud inside that window costs.</summary>
        public const double ReopenedTooSoonDistrust = 0.25;

        /// <summary>
        /// What a blow-off is worth right now, as a multiplier on the match.
        ///
        /// Proportional to what was actually built — doc 20 §5, the stipulation must match
        /// the escalation. A blow-off declared on a feud nobody has been following is the
        /// unearned resolution §9 warns about, and is worth less than not declaring one.
        /// </summary>
        public double BlowOffPayoff => PayoffFor(Intensity);

        /// <summary>
        /// The same curve as a plain function, so the booking screens can price a blow-off
        /// on a feud the player is declaring by hand and has not created yet.
        /// </summary>
        public static double PayoffFor(FeudIntensity intensity) => intensity switch
        {
            FeudIntensity.Nuclear  => 1.45,
            FeudIntensity.Hot      => 1.28,
            FeudIntensity.Building => 1.05,
            _                      => 0.72   // unearned: the audience was not told this mattered
        };

        /// <summary>Whether declaring a blow-off would actually pay off.</summary>
        public bool WorthBlowingOff => Intensity >= FeudIntensity.Hot;

        /// <summary>
        /// A blow-off that did not resolve anything.
        ///
        /// Doc 20 §6.1 puts **resolve — someone definitively wins** first in the list of
        /// what a blow-off has to do, and §9 names the interference loop — every match ends
        /// in a run-in, nothing is settled — as one of the things that kills a feud. So a
        /// blow-off booked to a disqualification, a count-out or a run-in does not settle
        /// the story, and it costs more than an ordinary unresolved match: the crowd was
        /// told this was the end and it was not.
        ///
        /// This is what makes declaring a blow-off a decision rather than a free bonus.
        /// </summary>
        public const double BrokenPromiseDistrust = 0.30;

        /// <summary>
        /// The promise was made and not kept. The feud stays open, and the pairing pays
        /// 1.67× what an ordinary unresolved match costs (0.30 against 0.18).
        /// </summary>
        public void RecordBrokenPromise()
        {
            MatchesSinceHot++;
            Distrust = Math.Clamp(Distrust + BrokenPromiseDistrust, 0, 1);
        }

        /// <summary>
        /// Pays the feud off and ends it. The debt is settled — doc 20 §3.1.
        /// </summary>
        public void BlowOff(DateOnly? date)
        {
            Concluded   = true;
            ConcludedOn = date;
            Heat        = 0;
            Intensity   = FeudIntensity.None;
            DecayedTo   = null;

            // The blow-off is itself an event, so it is the last thing that advanced the
            // feud. This is what the reopen check in AddHeat measures "too soon" against
            // when nothing else has happened since.
            if (date is { } d) LastAdvanced = d;
            Distrust    = Math.Max(0, Distrust - 0.35);  // resolving earns some belief back
        }

        /// <summary>
        /// Records that a match happened without resolving anything. Past the third, the
        /// audience starts to conclude nothing is at stake.
        /// </summary>
        public void RecordUnresolved()
        {
            if (Intensity < FeudIntensity.Hot) return;

            MatchesSinceHot++;
            if (MatchesSinceHot > PatienceMatches)
                Distrust = Math.Clamp(Distrust + 0.18, 0, 1);
        }

        /// <summary>
        /// What this pairing can still draw, after everything the booker has taught the
        /// crowd about whether their stories finish. 1.0 is untainted.
        /// </summary>
        public double Credibility => 1.0 - Distrust * 0.45;

        /// <summary>
        /// Restores the decay clock from a save. For loading only, in the same spirit as
        /// <see cref="RestoreHeat"/> — <see cref="Advance"/> would stamp today's date and
        /// hand every loaded feud a free reset.
        /// </summary>
        public void RestoreDecay(DateOnly? lastAdvanced, DateOnly? decayedTo)
        {
            LastAdvanced = lastAdvanced;
            DecayedTo    = decayedTo;
        }

        /// <summary>Restores resolution state from a save. Loading only.</summary>
        public void RestoreResolution(bool concluded, DateOnly? concludedOn,
                                      int matchesSinceHot, double distrust,
                                      int chaptersSettled = 0)
        {
            Concluded       = concluded;
            ConcludedOn     = concludedOn;
            MatchesSinceHot = Math.Max(0, matchesSinceHot);
            Distrust        = Math.Clamp(distrust, 0, 1);
            ChaptersSettled = Math.Max(0, chaptersSettled);
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

        public bool Involves(Wrestler w) => CampOf(w) is not null;

        /// <summary>Every camp's billing, for display.</summary>
        public IEnumerable<string> CampNames =>
            Camps.Select(c => string.Join(" & ", c.Select(w => w.RingName)));

        public override string ToString() =>
            $"{string.Join(" vs ", CampNames)} — {Intensity} ({Heat:F0} heat)";
    }
}
