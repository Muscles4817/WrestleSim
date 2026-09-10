using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Catalogue of named match structures. Each structure is a sensible default beat
    /// sequence the player can select and then customise before booking.
    /// WrestlerA is the face / intended winner in all defaults.
    /// </summary>
    public static class MatchStructureLibrary
    {
        private static MatchBeat Beat(string templateName, BeatControl control,
            BeatIntensity? intensity = null, BeatDuration? duration = null) =>
            BeatLibrary.Find(templateName)!.ToMatchBeat(control, intensity, duration);

        /// <summary>
        /// A tag change that names who comes in. On a two-man side the default — next man
        /// round — is the only possible answer, so nothing needed this. On a trio it is the
        /// difference between the third man working the match and standing on the apron for
        /// all of it.
        /// </summary>
        /// <summary>Aims a beat at a side — who is disposed of, whose cover is broken, who is pinned.</summary>
        private static MatchBeat Against(MatchBeat beat, BeatControl against)
        {
            beat.Against = against;
            return beat;
        }

        private static MatchBeat Tag(string templateName, BeatControl control, int incoming)
        {
            var beat = Beat(templateName, control);
            beat.IncomingIndex = incoming;
            return beat;
        }

        public static IReadOnlyList<MatchStructure> All { get; } = new List<MatchStructure>
        {
            // ── TV Formula ───────────────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Fatal Four-Way",
                Description = "Four ways to win. Two pairs work at once, so nobody is spare — " +
                              "smoother than a three-way and, for the same reason, less dramatic.",
                SideCount   = 4,
                Tags        = ["Multi-Man", "No DQ", "Spot"],
                Beats       =
                [
                    Beat("Hot Start",         BeatControl.Even),
                    Against(Beat("Disposal Spot", BeatControl.WrestlerA), BeatControl.SideD),
                    Beat("Signature Cover",   BeatControl.WrestlerB),
                    Beat("Shock Kickout",     BeatControl.WrestlerB),
                    Against(Beat("Pin Break", BeatControl.SideC), BeatControl.WrestlerB),
                    Against(Beat("Disposal Spot", BeatControl.WrestlerA), BeatControl.SideC),
                    Beat("Hot Comeback",      BeatControl.WrestlerA),
                    Against(Beat("Clean Victory", BeatControl.WrestlerA), BeatControl.WrestlerB)
                ]
            },

            // ── Multi-man (doc 18 §2.5) ──────────────────────────────────────
            //
            // Both run the format's loop rather than a tag formula: establish all three,
            // dispose of one, work the pair, near fall, break. The disposal is what makes the
            // near falls mean anything — outside a disposal window every cover in a three-way
            // is breakable and the crowd knows it.

            new MatchStructure
            {
                Name        = "Triple Threat",
                Description = "Three ways to win and no disqualification. Somebody is disposed of, " +
                              "the other two work, and the fall is stolen off the back of it.",
                SideCount   = 3,
                Tags        = ["Multi-Man", "No DQ", "Spot"],
                Beats       =
                [
                    Beat("Hot Start",   BeatControl.Even),
                    Against(Beat("Disposal Spot", BeatControl.WrestlerB), BeatControl.SideC),
                    Beat("Signature Cover", BeatControl.WrestlerB),
                    Beat("Shock Kickout",      BeatControl.WrestlerB),
                    Against(Beat("Pin Break",  BeatControl.SideC), BeatControl.WrestlerB),
                    Against(Beat("Disposal Spot", BeatControl.SideC), BeatControl.WrestlerB),
                    Beat("Hot Comeback",       BeatControl.WrestlerA),
                    Against(Beat("Clean Victory", BeatControl.WrestlerA), BeatControl.SideC)
                ]
            },

            new MatchStructure
            {
                Name        = "The Grudge Three-Way",
                Description = "Two of them care more about each other than the match. They wreck " +
                              "each other and the third steals it — the finish the format exists for.",
                SideCount   = 3,
                RequiresFeud = true,
                Tags        = ["Multi-Man", "Story", "Feud"],
                Beats       =
                [
                    Beat("Hot Start",   BeatControl.Even),
                    Against(Beat("Disposal Spot", BeatControl.WrestlerA), BeatControl.SideC),
                    Beat("Signature Cover", BeatControl.WrestlerA),
                    Beat("Shock Kickout",      BeatControl.WrestlerA),
                    Against(Beat("Spite Break", BeatControl.WrestlerB), BeatControl.WrestlerA),
                    Against(Beat("Ignored Opportunity", BeatControl.WrestlerA), BeatControl.WrestlerB),
                    Against(Beat("Mutual Destruction", BeatControl.WrestlerA), BeatControl.WrestlerB),
                    Against(Beat("Roll-Up Steal", BeatControl.SideC), BeatControl.WrestlerA)
                ]
            },

            // ── Elimination (doc 18 §2.5) ────────────────────────────────────
            //
            // "Falls remove people; last one standing wins... The drama moves from the fall
            // to the *order* of eliminations." So both of these are built around the order
            // rather than the finish, and both leave work between the falls — which the
            // engine measures, because a scramble of three falls in a row is the way this
            // format goes wrong.

            new MatchStructure
            {
                Name        = "Triple Threat Elimination",
                Description = "No stolen fall and nobody left unaccounted for — one goes out, " +
                              "and what is left is the singles match the crowd has been waiting for.",
                SideCount   = 3,
                Tags        = ["Multi-Man", "Elimination", "No DQ"],
                Beats       =
                [
                    Beat("Hot Start",       BeatControl.Even),
                    Against(Beat("Disposal Spot", BeatControl.WrestlerB), BeatControl.SideC),
                    Beat("Signature Cover", BeatControl.WrestlerB),
                    Against(Beat("Elimination", BeatControl.WrestlerB), BeatControl.SideC),
                    Beat("Methodical Grind", BeatControl.WrestlerB),
                    Beat("Shock Kickout",    BeatControl.WrestlerA),
                    Beat("Hot Comeback",     BeatControl.WrestlerA),
                    Against(Beat("Clean Victory", BeatControl.WrestlerA), BeatControl.WrestlerB)
                ]
            },

            new MatchStructure
            {
                Name        = "Four-Way Elimination",
                Description = "Three falls, spread out, and the field thins around the two who " +
                              "were always going to be there at the end.",
                SideCount   = 4,
                Tags        = ["Multi-Man", "Elimination", "No DQ"],
                Beats       =
                [
                    Beat("Hot Start",       BeatControl.Even),
                    Against(Beat("Disposal Spot", BeatControl.WrestlerA), BeatControl.SideD),
                    Beat("Signature Cover", BeatControl.WrestlerB),
                    Against(Beat("Elimination", BeatControl.WrestlerB), BeatControl.SideD),
                    Beat("Methodical Grind", BeatControl.WrestlerB),
                    Against(Beat("Pin Break", BeatControl.WrestlerA), BeatControl.WrestlerB),
                    Against(Beat("Elimination", BeatControl.WrestlerA), BeatControl.SideC),
                    Beat("Shock Kickout",   BeatControl.WrestlerB),
                    Beat("Hot Comeback",    BeatControl.WrestlerA),
                    Against(Beat("Clean Victory", BeatControl.WrestlerA), BeatControl.WrestlerB)
                ]
            },

            // ── Handicap (doc 18 §2.5) ───────────────────────────────────────
            //
            // "Almost never a contest; it is a *statement*, and the statement is usually
            // about the lone man's toughness rather than the outcome." So neither of these
            // is built to be won — they are built to give the lone wrestler moments, which
            // is what the engine grades a handicap match on.
            //
            // Both are short on purpose. Being outnumbered compounds, so the longer one of
            // these runs the less he has left, and a long handicap match is a slaughter
            // whatever the booking intended.

            new MatchStructure
            {
                Name        = "Two on One",
                Description = "Nobody is winning this from underneath. What the match is for " +
                              "is whether they make them work for it — two comebacks, and " +
                              "neither one enough.",
                SideSize    = 1,
                SideSizeB   = 2,
                Tags        = ["Handicap", "Story", "Statement"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Power Beatdown",  BeatControl.WrestlerB),
                    Beat("Hot Comeback",    BeatControl.WrestlerA),
                    Beat("Methodical Grind", BeatControl.WrestlerB),
                    Beat("Hot Comeback",    BeatControl.WrestlerA),
                    Beat("Signature Cover", BeatControl.WrestlerB),
                    Beat("Clean Victory",   BeatControl.WrestlerB)
                ]
            },

            new MatchStructure
            {
                Name        = "Beat the Odds",
                Description = "The upset, which costs the pair more than it gives the winner — " +
                              "two people beaten by one is a bill somebody pays later.",
                SideSize    = 1,
                SideSizeB   = 2,
                Tags        = ["Handicap", "Story", "Upset"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Power Beatdown",  BeatControl.WrestlerB),
                    Beat("Hot Comeback",    BeatControl.WrestlerA),
                    Beat("Miscommunication", BeatControl.WrestlerB),
                    Beat("Shock Kickout",   BeatControl.WrestlerA),
                    Beat("Roll-Up Steal",   BeatControl.WrestlerA)
                ]
            },

            new MatchStructure
            {
                Name        = "Three on One",
                Description = "A statement rather than a match. Short, because being " +
                              "outnumbered compounds and a long one is just a slaughter.",
                SideSize    = 1,
                SideSizeB   = 3,
                Tags        = ["Handicap", "Squash", "Statement"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Power Beatdown", BeatControl.WrestlerB),
                    Beat("Hot Comeback",   BeatControl.WrestlerA),
                    Beat("Power Beatdown", BeatControl.WrestlerB),
                    Beat("Clean Victory",  BeatControl.WrestlerB)
                ]
            },

            // ── Survivor Series (doc 18 §2.5, the elimination bullet) ────────
            //
            // Four a side, tag rules, falls take out *people*. What makes it its own match
            // rather than a long tag is that the numbers go lopsided partway through and
            // stay that way — so the survivors spend the back half working a handicap match
            // they arrived at rather than one anybody booked.
            //
            // Booked to leave two standing, because a clean sweep says the losing team was
            // worthless and a sole survivor is a bigger card than most nights need.

            new MatchStructure
            {
                Name        = "Survivor Series",
                Description = "Four a side, tag rules, and a fall sends you to the back. The " +
                              "story is who is left at the end — and whoever is outnumbered " +
                              "in the meantime feels it.",
                SideSize    = 4,
                Tags        = ["Elimination", "Tag", "Survivor"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Shine",           BeatControl.WrestlerA),
                    Against(Beat("Elimination", BeatControl.WrestlerA), BeatControl.WrestlerB),
                    Beat("Cut-Off",         BeatControl.WrestlerB),
                    Against(Beat("Elimination", BeatControl.WrestlerB), BeatControl.WrestlerA),
                    Beat("Face in Peril",   BeatControl.WrestlerB),
                    Against(Beat("Elimination", BeatControl.WrestlerB), BeatControl.WrestlerA),
                    Beat("Hot Tag",         BeatControl.WrestlerA),
                    Against(Beat("Elimination", BeatControl.WrestlerA), BeatControl.WrestlerB),
                    Beat("Double Team",     BeatControl.WrestlerA),
                    Against(Beat("Elimination", BeatControl.WrestlerA), BeatControl.WrestlerB),
                    Beat("Shock Kickout",   BeatControl.WrestlerB),
                    Against(Beat("Clean Victory", BeatControl.WrestlerA), BeatControl.WrestlerB)
                ]
            },

            new MatchStructure
            {
                Name        = "TV Formula",
                Description = "The bread-and-butter structure for weekly television. Short, clean, and effective. " +
                              "Heel controls the middle, face explodes back, decisive finish.",
                Tags        = ["Short", "Simple", "Clean"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even, duration: BeatDuration.Brief),
                    Beat("Shine",                     BeatControl.WrestlerA, duration: BeatDuration.Short),
                    Beat("Cut-Off",                   BeatControl.WrestlerB, duration: BeatDuration.Brief),
                    Beat("Power Beatdown",            BeatControl.WrestlerB, duration: BeatDuration.Short),
                    Beat("Hot Comeback",              BeatControl.WrestlerA),
                    Beat("Clean Victory",             BeatControl.WrestlerA)
                ]
            },

            // ── Face-in-Peril ────────────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Face-in-Peril",
                Description = "The Hogan/Cena formula. The babyface dominates early, gets cut off and endures " +
                              "a long beatdown, then fires back and finishes strong. Maximum crowd sympathy arc.",
                Tags        = ["Classic", "Babyface", "Crowd"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even, duration: BeatDuration.Brief),
                    Beat("Shine",                     BeatControl.WrestlerA, duration: BeatDuration.Short),
                    Beat("Cut-Off",                   BeatControl.WrestlerB),
                    Beat("Power Beatdown",            BeatControl.WrestlerB, duration: BeatDuration.Short),
                    Beat("Hope Spot",                 BeatControl.WrestlerA),
                    Beat("Wear-Down Hold",            BeatControl.WrestlerB, duration: BeatDuration.Brief),
                    Beat("Desperation Strike",        BeatControl.WrestlerA),
                    Beat("Hot Comeback",              BeatControl.WrestlerA),
                    Beat("Signature Cover",           BeatControl.WrestlerA),
                    Beat("Shock Kickout",             BeatControl.WrestlerA),
                    Beat("Clean Victory",             BeatControl.WrestlerA)
                ]
            },

            // ── Technical Showcase ───────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Technical Showcase",
                Description = "The Bret/HBK/Benoit structure. Built on mat psychology, limb targeting, " +
                              "and a submission payoff. Rewards wrestlers with high RingIQ and Psychology.",
                Tags        = ["Technical", "Psychology", "Long"],
                Beats       =
                [
                    Beat("Feeling-Out Process",   BeatControl.Even),
                    Beat("Shine",                 BeatControl.WrestlerA, duration: BeatDuration.Short),
                    Beat("Cut-Off",               BeatControl.WrestlerB, duration: BeatDuration.Brief),
                    Beat("Technical Dissection",  BeatControl.WrestlerB, duration: BeatDuration.Medium),
                    Beat("Hope Spot",             BeatControl.WrestlerA),
                    Beat("Strategic Ground Work", BeatControl.WrestlerB),
                    Beat("Desperation Strike",    BeatControl.WrestlerA),
                    Beat("Hot Comeback",          BeatControl.WrestlerA),
                    Beat("Signature Cover",       BeatControl.WrestlerA),
                    Beat("Shock Kickout",         BeatControl.WrestlerA),
                    Beat("Tap Out",               BeatControl.WrestlerA)
                ]
            },

            // ── Spotfest ─────────────────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Spotfest",
                Description = "High-spot driven from bell to bell. Aerial moves carry the crowd rather than " +
                              "psychology. Cruiserweight and ladder match territory.",
                Tags        = ["Fast", "Aerial", "Exciting"],
                Beats       =
                [
                    Beat("Hot Start",        BeatControl.Even),
                    Beat("Aerial Assault",   BeatControl.WrestlerA),
                    Beat("Explosive Flurry", BeatControl.WrestlerB),
                    Beat("Jaw-Dropper",      BeatControl.WrestlerB),
                    Beat("Power Beatdown",   BeatControl.WrestlerB, duration: BeatDuration.Short),
                    Beat("Aerial Assault",   BeatControl.WrestlerA),
                    Beat("Hot Comeback",     BeatControl.WrestlerA),
                    Beat("Signature Cover",  BeatControl.WrestlerA),
                    Beat("Shock Kickout",    BeatControl.WrestlerA),
                    Beat("Jaw-Dropper",      BeatControl.WrestlerA),
                    Beat("Clean Victory",    BeatControl.WrestlerA)
                ]
            },

            // ── Grudge Brawl ─────────────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Grudge Brawl",
                Description = "A hate-filled contest that spills everywhere. Revenge spots and ringside chaos " +
                              "tell the story. Works without a formal feud but benefits from one.",
                Tags        = ["Brawling", "Physical", "Emotional"],
                Beats       =
                [
                    Beat("Hot Start",         BeatControl.Even),
                    Beat("Ringside Brawl",    BeatControl.WrestlerB),
                    Beat("Power Beatdown",    BeatControl.WrestlerB),
                    Beat("Desperation Strike",BeatControl.WrestlerA),
                    Beat("Revenge Spot",      BeatControl.WrestlerA),
                    Beat("Hot Comeback",      BeatControl.WrestlerA),
                    Beat("Signature Cover",   BeatControl.WrestlerA),
                    Beat("Shock Kickout",     BeatControl.WrestlerA),
                    Beat("Clean Victory",     BeatControl.WrestlerA)
                ]
            },

            // ── Feud Blowoff ─────────────────────────────────────────────────

            new MatchStructure
            {
                Name         = "Feud Blowoff",
                Description  = "The definitive end to a feud. Sustained crowd brawl, the full weight of history " +
                               "erupting at once, multiple near-falls. Requires an active feud at Building intensity.",
                Tags         = ["Feud", "Emotional", "Exciting"],
                RequiresFeud = true,
                Beats        =
                [
                    Beat("Hot Start",       BeatControl.Even),
                    Beat("Full-Crowd War",  BeatControl.Even, duration: BeatDuration.Short),
                    Beat("Feud Erupts",     BeatControl.WrestlerB),
                    Beat("Cut-Off",         BeatControl.WrestlerB),
                    Beat("Power Beatdown",  BeatControl.WrestlerB),
                    Beat("Hope Spot",       BeatControl.WrestlerA),
                    Beat("Revenge Spot",    BeatControl.WrestlerA),
                    Beat("Fighting Spirit", BeatControl.WrestlerA),
                    Beat("Signature Cover", BeatControl.WrestlerA),
                    Beat("Shock Kickout",   BeatControl.WrestlerA),
                    Beat("Shock Kickout",   BeatControl.WrestlerA),
                    Beat("Clean Victory",   BeatControl.WrestlerA)
                ]
            },

            // ── Big Match ───────────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Big Match",
                Description = "Two heat/comeback cycles and a real finishing stretch — doc 18 §3.1's " +
                              "fifteen-to-twenty-five-minute shape. What a pay-per-view semi-main is.",
                Tags        = ["Long", "Psychology", "Emotional", "Exciting"],
                Beats       =
                [
                    Beat("Feeling-Out Process", BeatControl.Even),
                    Beat("Shine",               BeatControl.WrestlerA, duration: BeatDuration.Short),
                    Beat("Cut-Off",             BeatControl.WrestlerB),
                    Beat("Power Beatdown",      BeatControl.WrestlerB),
                    Beat("Hope Spot",           BeatControl.WrestlerA),
                    Beat("Hot Comeback",        BeatControl.WrestlerA),
                    Beat("Cut-Off",             BeatControl.WrestlerB),
                    Beat("Methodical Grind",    BeatControl.WrestlerB, duration: BeatDuration.Short),
                    Beat("Desperation Strike",  BeatControl.WrestlerA),
                    Beat("Fighting Spirit",     BeatControl.WrestlerA),
                    Beat("Signature Cover",     BeatControl.WrestlerA),
                    Beat("Shock Kickout",       BeatControl.WrestlerA),
                    Beat("Dominant Statement",  BeatControl.WrestlerA)
                ]
            },

            // ── The epic (doc 18 §2.4, §3.1) ─────────────────────────────────
            //
            // "25–40 minutes, multiple false finishes. Reserved for the biggest matches;
            // loses its power if used often." There was nothing in this library that
            // reached that band — the longest singles template ran sixteen minutes and was
            // called Big Match, which meant a booker had no way to lay out the main
            // event of the biggest show of the year.
            //
            // Two full heat/comeback cycles, four false finishes, and it is deliberately
            // punishing: seventeen beats runs the engine's length-versus-conditioning
            // penalty hard, so asking two wrestlers who cannot go long to work this loses
            // the room. Doc 18 says exactly that — an epic "requires two performers with
            // enough over-ness to hold attention and enough conditioning to work it".

            new MatchStructure
            {
                Name        = "Epic",
                Description = "Thirty-plus minutes, two heat sections and four false finishes. " +
                              "Needs two people the crowd will watch that long, and the gas to do it.",
                Tags        = ["Epic", "Main Event", "Crowd"],
                Beats       =
                [
                    Beat("Feeling-Out Process",  BeatControl.Even),
                    Beat("Shine",                BeatControl.WrestlerA),
                    Beat("Cut-Off",              BeatControl.WrestlerB),
                    Beat("Power Beatdown",       BeatControl.WrestlerB),
                    Beat("Hope Spot",            BeatControl.WrestlerA),
                    Beat("Wear-Down Hold",       BeatControl.WrestlerB, duration: BeatDuration.Short),
                    Beat("Desperation Strike",   BeatControl.WrestlerA),
                    Beat("Hot Comeback",         BeatControl.WrestlerA),
                    Beat("Cut-Off",              BeatControl.WrestlerB),
                    Beat("Methodical Grind",     BeatControl.WrestlerB, duration: BeatDuration.Medium),
                    Beat("Hope Spot",            BeatControl.WrestlerA),
                    Beat("Mind Games",           BeatControl.WrestlerB),
                    Beat("Fighting Spirit",      BeatControl.WrestlerA),
                    Beat("Signature Cover",      BeatControl.WrestlerA),
                    Beat("Shock Kickout",        BeatControl.WrestlerA),
                    Beat("Shock Kickout",        BeatControl.WrestlerA),
                    Beat("Dominant Statement",   BeatControl.WrestlerA)
                ]
            },

            // ── Tag structures ───────────────────────────────────────────────
            //
            // Side A is the face team throughout. The shape is always the same and the
            // shape is the point: establish them, cut them off, keep one man from his
            // corner, deny the tag, then pay it off. See docs/tag-matches-plan.md §2.

            new MatchStructure
            {
                Name        = "Southern Tag",
                Description = "The canonical tag match. Shine, cut-off, a long isolation broken up by two " +
                              "denied tags, then the hot tag and the breakdown. Everything the format is for.",
                Tags        = ["Tag", "Classic", "Crowd"],
                SideSize    = 2,
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Shine",                     BeatControl.WrestlerA),
                    Beat("Cut-Off",                   BeatControl.WrestlerB),
                    Beat("Face in Peril",             BeatControl.WrestlerB),
                    Beat("Near Tag",                  BeatControl.WrestlerB),
                    Beat("Face in Peril",             BeatControl.WrestlerB),
                    Beat("Near Tag",                  BeatControl.WrestlerB),
                    Beat("Hot Tag",                   BeatControl.WrestlerA),
                    Beat("Double Team",               BeatControl.WrestlerA),
                    Beat("Everybody In",              BeatControl.Even),
                    Beat("Shock Kickout",             BeatControl.WrestlerB),
                    Beat("Save",                      BeatControl.WrestlerA),
                    Beat("Clean Victory",             BeatControl.WrestlerA),
                ]
            },

            new MatchStructure
            {
                Name        = "Formula Tag",
                Description = "The television version of the Southern Tag — one isolation shorter and no " +
                              "breakdown. Fits a nine-minute slot and still pays off the hot tag.",
                Tags        = ["Tag", "Short", "Television"],
                SideSize    = 2,
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Shine",                     BeatControl.WrestlerA),
                    Beat("Cut-Off",                   BeatControl.WrestlerB),
                    Beat("Face in Peril",             BeatControl.WrestlerB),
                    Beat("Near Tag",                  BeatControl.WrestlerB),
                    Beat("Hot Tag",                   BeatControl.WrestlerA),
                    Beat("Double Team",               BeatControl.WrestlerA),
                    Beat("Clean Victory",             BeatControl.WrestlerA),
                ]
            },

            new MatchStructure
            {
                Name        = "Tag Sprint",
                Description = "The opener. No isolation, no peril — just tandem offence, a miscommunication " +
                              "and a flash finish. Deliberately does not use the hot tag.",
                Tags        = ["Tag", "Short", "Fast"],
                SideSize    = 2,
                Beats       =
                [
                    Beat("Hot Start",         BeatControl.Even),
                    Beat("Double Team",       BeatControl.WrestlerA),
                    Beat("Double Team",       BeatControl.WrestlerB),
                    Beat("Miscommunication",  BeatControl.WrestlerB),
                    Beat("Blind Tag",         BeatControl.WrestlerA),
                    Beat("Counter Roll-Up",   BeatControl.WrestlerA),
                    Beat("Roll-Up Steal",     BeatControl.WrestlerA),
                ]
            },

            // ── Trios ────────────────────────────────────────────────────────
            //
            // Three a side is still two sides, so the tag machinery applies without
            // change — docs/wrestling-reference/18-match-craft.md §2.5. What it does not
            // do by itself is give the third man anything to do. The engine has no term
            // for headcount and deliberately should not: a side is read from its members,
            // so a third man matters exactly as much as he is *in the match*. Crowd fields
            // read the whole side, so he lifts the room from the apron; craft fields read
            // only the legal performers, so he contributes nothing to the work until he is
            // tagged in.
            //
            // Which makes the tag changes load-bearing content rather than decoration. Every
            // tag that needs to name its incoming member does — the exception is Six-Man
            // War's Hot Tag, where next-man-round is the answer anyway — so all six people
            // are legal at some point in both. The first pass of these two shipped with a
            // single tag change each and left three of the six on the apron for the entire
            // match, which is doc 18 §9's "unexplained third man" in everything but the
            // detail that §9 puts him on the floor rather than on the apron.
            //
            // The two are deliberately different matches, not one match at two lengths:
            // Six-Man War is the American six-man (the Southern Tag with a deeper heat and
            // a fresh man for the finish); Lucha Trios is the CMLL default described in
            // doc 25 §3.3 — rapid tags, constant motion, no long isolation at all.

            new MatchStructure
            {
                Name        = "Lucha Trios",
                Description = "The Arena México default. Rapid tags and constant motion — all six " +
                              "work, nobody is isolated — into a dive sequence and a fall from nowhere.",
                Tags        = ["Trios", "Fast", "Lucha"],
                SideSize    = 3,
                Beats       =
                [
                    // Four tag changes in thirteen beats, two of them on the rudo side.
                    // Doc 25 §3.3's second bullet is "rapid tag rules that allow constant
                    // motion", and constant motion is the opposite of the Southern Tag:
                    // there is no isolation and no hot tag here at all, so there is nothing
                    // to charge and nothing to spend.
                    Beat("Hot Start",                 BeatControl.Even),
                    Beat("Shine",                     BeatControl.WrestlerA),
                    Tag ("Quick Tag",                 BeatControl.WrestlerA, incoming: 1),
                    Beat("Double Team",               BeatControl.WrestlerA),
                    Tag ("Blind Tag",                 BeatControl.WrestlerB, incoming: 1),
                    Beat("Cut-Off",                   BeatControl.WrestlerB),
                    Beat("Double Team",               BeatControl.WrestlerB),
                    Tag ("Quick Tag",                 BeatControl.WrestlerB, incoming: 2),
                    Beat("Aerial Assault",            BeatControl.WrestlerA),
                    Tag ("Quick Tag",                 BeatControl.WrestlerA, incoming: 2),
                    Beat("Everybody In",              BeatControl.Even),
                    Beat("Jaw-Dropper",               BeatControl.WrestlerA),
                    Beat("Roll-Up Steal",             BeatControl.WrestlerA),
                ]
            },

            new MatchStructure
            {
                Name        = "Six-Man War",
                Description = "The American six-man. A deeper heat than a tag can carry — three heels " +
                              "rotating on whoever is cut off — then the hot tag and a fresh partner to finish.",
                Tags        = ["Trios", "Classic", "Crowd"],
                SideSize    = 3,
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Shine",                     BeatControl.WrestlerA),
                    Beat("Cut-Off",                   BeatControl.WrestlerB),
                    Beat("Face in Peril",             BeatControl.WrestlerB),
                    Beat("Near Tag",                  BeatControl.WrestlerB),
                    // The heels rotating is what a third man buys the *heat*. The charge is
                    // tracked per side and credited to whoever is being worked over, so a
                    // heel tag costs the face nothing — his corner keeps everything the
                    // isolation paid in. Three fresh men working one is a longer heat that
                    // still reads as a beating rather than as padding.
                    Tag ("Quick Tag",                 BeatControl.WrestlerB, incoming: 1),
                    Beat("Face in Peril",             BeatControl.WrestlerB),
                    Beat("Near Tag",                  BeatControl.WrestlerB),
                    // The third heel tags in fresh for the last stretch of the beating.
                    Tag ("Quick Tag",                 BeatControl.WrestlerB, incoming: 2),
                    // Three isolations, one per heel — the deeper heat the Description
                    // claims, and the thing two a side genuinely cannot book. A near tag
                    // between each keeps the run at one, so this costs no patience: doc 18
                    // §2.3 says a long heat is good and the hope spots are what make it so.
                    Beat("Face in Peril",             BeatControl.WrestlerB),
                    Beat("Hot Tag",                   BeatControl.WrestlerA),
                    Beat("Double Team",               BeatControl.WrestlerA),
                    // And what a third man buys the *finish*: somebody who has not been in
                    // the match yet takes the fall. On two a side this beat cannot exist.
                    Tag ("Quick Tag",                 BeatControl.WrestlerA, incoming: 2),
                    Beat("Everybody In",              BeatControl.Even),
                    // Restored. Dropping the Shock Kickout left the Save breaking up a pin
                    // the match had never shown anybody attempt.
                    Beat("Shock Kickout",             BeatControl.WrestlerA),
                    Beat("Save",                      BeatControl.WrestlerB),
                    Beat("Clean Victory",             BeatControl.WrestlerA),
                ]
            },
        };

        // ── Query helpers ────────────────────────────────────────────────────

        public static MatchStructure? Find(string name) =>
            All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        public static IEnumerable<MatchStructure> WithTag(string tag) =>
            All.Where(s => s.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Structures a match of this shape can actually use. A singles match cannot work
        /// a hot tag, and a tag match offered only singles structures never gets to be one.
        /// </summary>
        public static IEnumerable<MatchStructure> ForSideSize(int sideSize) =>
            ForShape(2, sideSize);

        /// <summary>
        /// The structures written for a given match shape. Both dimensions matter: a
        /// singles structure has no third side to name and a triple-threat structure's
        /// finish names a side a two-sided match does not have.
        /// </summary>
        public static IEnumerable<MatchStructure> ForShape(int sideCount, int sideSize) =>
            ForShape(sideCount, sideSize, sideSize);

        /// <summary>
        /// The same, for a shape whose sides are different sizes. A handicap structure is
        /// written against a specific imbalance — a plan for one against two does not work
        /// for one against three, because how long the lone wrestler can plausibly hold out
        /// is the whole of the booking.
        /// </summary>
        public static IEnumerable<MatchStructure> ForShape(int sideCount, int sizeA, int sizeB) =>
            All.Where(s => s.SideCount == sideCount && s.SideSize == sizeA && s.SizeB == sizeB);
    }
}
