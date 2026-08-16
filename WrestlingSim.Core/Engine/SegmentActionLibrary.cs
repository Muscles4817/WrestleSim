using WrestlingSim.Enums;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Catalogue of every named segment action available when building a segment
    /// from scratch. The segment-side counterpart of BeatLibrary.
    /// </summary>
    public static class SegmentActionLibrary
    {
        public const string CatTalking = "Talking";
        public const string CatPhysical = "Physical";
        public const string CatTurn = "Turn";

        public static IReadOnlyList<SegmentActionTemplate> All { get; } = new List<SegmentActionTemplate>
        {
            // ── Talking ──────────────────────────────────────────────────────

            new SegmentActionTemplate
            {
                Name        = "Cut a Promo",
                Description = "Straight to the microphone. Impact scales with the speaker's charisma.",
                Category    = CatTalking,
                ActionType  = SegmentActionType.Talk,
                BaseImpact  = 2.0,
                BookerTip   = "Safe and reliable. A low-charisma wrestler talking at length is dead air.",
                Tags        = ["Charisma"]
            },

            new SegmentActionTemplate
            {
                Name        = "Trash Talk",
                Description = "Personal, pointed abuse aimed at a rival. Builds real animosity.",
                Category    = CatTalking,
                ActionType  = SegmentActionType.Talk,
                BaseImpact  = 2.2,
                Heat        = 2.0,
                HistoryTag  = FeudHistoryTag.PersonalInsult,
                BookerTip   = "The cheapest way to start a feud from nothing. Stamps PersonalInsult.",
                Tags        = ["Charisma", "Feud"]
            },

            new SegmentActionTemplate
            {
                Name        = "Issue a Challenge",
                Description = "Call someone out and name the stipulation. Sets up the match.",
                Category    = CatTalking,
                ActionType  = SegmentActionType.Talk,
                BaseImpact  = 2.0,
                Heat        = 2.5,
                BookerTip   = "Good heat for the words spent. Pair with a Confrontation to escalate.",
                Tags        = ["Feud"]
            },

            new SegmentActionTemplate
            {
                Name        = "Interrupt",
                Description = "Music hits mid-sentence. Cuts the other wrestler off and flips the segment.",
                Category    = CatTalking,
                ActionType  = SegmentActionType.Interrupt,
                BaseImpact  = 1.5,
                Heat        = 1.0,
                BookerTip   = "Needs somebody talking first to interrupt — put a promo above it.",
                Tags        = ["Charisma"]
            },

            new SegmentActionTemplate
            {
                Name        = "Stand Tall",
                Description = "No words. Pose over a fallen rival and let the picture do the work.",
                Category    = CatTalking,
                ActionType  = SegmentActionType.Talk,
                BaseImpact  = 1.2,
                Heat        = 0.5,
                BookerTip   = "Cheap closer for a beatdown. Almost no botch risk since nothing is said.",
                Tags        = ["Visual"]
            },

            // ── Physical ─────────────────────────────────────────────────────

            new SegmentActionTemplate
            {
                Name        = "Blindside Attack",
                Description = "Jump the target from behind before they know you are there.",
                Category    = CatPhysical,
                ActionType  = SegmentActionType.Attack,
                BaseImpact  = 3.0,
                Heat        = 4.0,
                BookerTip   = "Strong heat. Carries injury risk — Toughness on the target resists it.",
                Tags        = ["Physical", "Feud"]
            },

            new SegmentActionTemplate
            {
                Name        = "Weapon Shot",
                Description = "A chair, a title belt, a kendo stick. Escalates the feud past talking.",
                Category    = CatPhysical,
                ActionType  = SegmentActionType.Attack,
                BaseImpact  = 3.5,
                Heat        = 5.0,
                OvernessScale = 1.1,
                BookerTip   = "The most heat any single action generates short of a turn. Highest injury risk.",
                Tags        = ["Physical", "Feud", "Risky"]
            },

            new SegmentActionTemplate
            {
                Name        = "Run-In",
                Description = "Hit the ring at full speed and change the complexion of the segment.",
                Category    = CatPhysical,
                ActionType  = SegmentActionType.RunIn,
                BaseImpact  = 2.5,
                Heat        = 3.0,
                BookerTip   = "Injects a third party without needing them to talk. No target required.",
                Tags        = ["Physical", "Chaos"]
            },

            // ── Turn ─────────────────────────────────────────────────────────

            new SegmentActionTemplate
            {
                Name        = "Turn on a Partner",
                Description = "The betrayal. An ally becomes the enemy in one movement.",
                Category    = CatTurn,
                ActionType  = SegmentActionType.Betrayal,
                BaseImpact  = 4.0,
                Heat        = 6.0,
                OvernessScale = 1.2,
                HistoryTag  = FeudHistoryTag.Betrayal,
                BookerTip   = "The single biggest heat generator. Stamps Betrayal, which unlocks Revenge Spot payoffs.",
                Tags        = ["Turn", "Feud", "Emotional"]
            },
        };

        // ── Query helpers ────────────────────────────────────────────────────

        public static IReadOnlyList<string> Categories { get; } =
            All.Select(t => t.Category).Distinct().ToList();

        public static IEnumerable<SegmentActionTemplate> ByCategory(string category) =>
            All.Where(t => t.Category == category);

        public static SegmentActionTemplate? Find(string name) =>
            All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
