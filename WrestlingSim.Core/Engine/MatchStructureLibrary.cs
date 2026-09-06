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
                Name        = "TV Formula",
                Description = "The bread-and-butter structure for weekly television. Short, clean, and effective. " +
                              "Heel controls the middle, face explodes back, decisive finish.",
                Tags        = ["Short", "Simple", "Clean"],
                Beats       =
                [
                    Beat("Standard Collar-and-Elbow", BeatControl.Even),
                    Beat("Power Beatdown",             BeatControl.WrestlerB),
                    Beat("Hot Comeback",               BeatControl.WrestlerA),
                    Beat("Clean Victory",              BeatControl.WrestlerA),
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
                    Beat("Hot Start",        BeatControl.WrestlerA),
                    Beat("Power Beatdown",   BeatControl.WrestlerB),
                    Beat("Methodical Grind", BeatControl.WrestlerB),
                    Beat("Hot Comeback",     BeatControl.WrestlerA),
                    Beat("Clean Victory",    BeatControl.WrestlerA),
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
                    Beat("Feeling-Out Process",  BeatControl.Even),
                    Beat("Technical Dissection", BeatControl.WrestlerB),
                    Beat("Strategic Ground Work", BeatControl.WrestlerB),
                    Beat("Hot Comeback",          BeatControl.WrestlerA),
                    Beat("Signature Cover",       BeatControl.WrestlerA),
                    Beat("Shock Kickout",         BeatControl.WrestlerA),
                    Beat("Tap Out",               BeatControl.WrestlerA),
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
                    Beat("Hot Start",       BeatControl.Even),
                    Beat("Aerial Assault",  BeatControl.WrestlerA),
                    Beat("Power Beatdown",  BeatControl.WrestlerB),
                    Beat("Jaw-Dropper",     BeatControl.WrestlerA),
                    Beat("Hot Comeback",    BeatControl.WrestlerA),
                    Beat("Shock Kickout",   BeatControl.WrestlerA),
                    Beat("Clean Victory",   BeatControl.WrestlerA),
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
                    Beat("Hot Start",      BeatControl.Even),
                    Beat("Ringside Brawl", BeatControl.Even),
                    Beat("Power Beatdown", BeatControl.WrestlerB),
                    Beat("Revenge Spot",   BeatControl.WrestlerA),
                    Beat("Signature Cover", BeatControl.WrestlerA),
                    Beat("Clean Victory",  BeatControl.WrestlerA),
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
                    Beat("Full-Crowd War",  BeatControl.Even),
                    Beat("Feud Erupts",     BeatControl.Even),
                    Beat("Power Beatdown",  BeatControl.WrestlerB),
                    Beat("Hot Comeback",    BeatControl.WrestlerA),
                    Beat("Shock Kickout",   BeatControl.WrestlerA),
                    Beat("Shock Kickout",   BeatControl.WrestlerA),
                    Beat("Clean Victory",   BeatControl.WrestlerA),
                ]
            },

            // ── Big Match Epic ───────────────────────────────────────────────

            new MatchStructure
            {
                Name        = "Big Match Epic",
                Description = "The WrestleMania main event structure. A slow build to an enormous peak — " +
                              "psychological warfare, multiple momentum swings, a defining near-fall sequence, " +
                              "and a finish that feels earned.",
                Tags        = ["Long", "Psychology", "Emotional", "Exciting"],
                Beats       =
                [
                    Beat("Feeling-Out Process",  BeatControl.Even),
                    Beat("Power Beatdown",        BeatControl.WrestlerB),
                    Beat("Aerial Assault",        BeatControl.WrestlerA),
                    Beat("Fighting Spirit",       BeatControl.WrestlerA),
                    Beat("Signature Cover",       BeatControl.WrestlerA),
                    Beat("Mind Games",            BeatControl.WrestlerA),
                    Beat("Shock Kickout",         BeatControl.WrestlerA),
                    Beat("Shock Kickout",         BeatControl.WrestlerB),
                    Beat("Dominant Statement",    BeatControl.WrestlerA),
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
            // Which makes the tag changes load-bearing content rather than decoration. Both
            // structures below name every incoming member explicitly, so all six people are
            // legal at some point. The first pass of these two shipped with a single tag
            // change each and left three of the six on the apron for the entire match —
            // §9's "unexplained third man", which §2.5 calls the format's characteristic
            // failure, shipped as a preset.
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
                    // Five tag changes in thirteen beats, three of them on the rudo side.
                    // Doc 25 §3.3 lists "rapid tag rules that allow constant motion" as the
                    // first thing three-a-side changes, and constant motion is the opposite
                    // of the Southern Tag: there is no isolation and no hot tag here at all,
                    // so there is nothing to charge and nothing to spend.
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
                              "rotating on one man — then the hot tag and a fresh third man to finish.",
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
            All.Where(s => s.SideSize == sideSize);
    }
}
