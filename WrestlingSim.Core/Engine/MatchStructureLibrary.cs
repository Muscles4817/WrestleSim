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
                    Beat("All Four In",               BeatControl.Even),
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
