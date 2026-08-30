using WrestlingSim.Enums;
using WrestlingSim.Factories;
using WrestlingSim.Models;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Catalogue of every pre-built segment the booker can put on a card.
    /// Every SegmentFactory builder is reachable from here — previously only
    /// CreatePromo had a caller and the other ten were unreferenced.
    /// </summary>
    public static class SegmentTemplateLibrary
    {
        public const string CatTalking   = "Talking";
        public const string CatAngle     = "Angle";
        public const string CatPhysical  = "Physical";
        public const string CatAuthority = "Authority";

        public static IReadOnlyList<SegmentTemplate> All { get; } = new List<SegmentTemplate>
        {
            // ── Talking ──────────────────────────────────────────────────────

            new SegmentTemplate
            {
                Name             = "Ring Promo",
                Description      = "One wrestler, one microphone, one crowd. The foundation of every angle.",
                Category         = CatTalking,
                Type             = SegmentType.Promo,
                Location         = SegmentLocation.Ring,
                ParticipantRoles = ["Speaker"],
                UsesDialogue     = true,
                BookerTip        = "Impact is almost entirely charisma. Builds little heat on its own.",
                Tags             = ["Charisma"],
                Build            = (p, d) => SegmentFactory.CreatePromo(p[0], Fallback(d, "This is my time."))
            },

            new SegmentTemplate
            {
                Name             = "Backstage Interview",
                Description      = "A quieter setting — until the other wrestler walks into shot and attacks.",
                Category         = CatTalking,
                Type             = SegmentType.Promo,
                Location         = SegmentLocation.Backstage,
                ParticipantRoles = ["Interviewee", "Interrupter"],
                HistoryTags      = [FeudHistoryTag.PersonalInsult],
                BookerTip        = "Backstage reaches a smaller crowd (×0.85) but reliably escalates a feud.",
                Tags             = ["Feud", "Physical"],
                Build            = (p, d) => SegmentFactory.CreateBackstageInterview(p[0], p[1])
            },

            new SegmentTemplate
            {
                Name             = "Face-to-Face Confrontation",
                Description      = "Two wrestlers, nose to nose, trading promos in front of a hot crowd.",
                Category         = CatTalking,
                Type             = SegmentType.Confrontation,
                Location         = SegmentLocation.Ring,
                ParticipantRoles = ["Speaker", "Interrupter"],
                HistoryTags      = [FeudHistoryTag.PersonalInsult],
                UsesDialogue     = true,
                BookerTip        = "The standard feud builder. Two charismatic talkers make this land.",
                Tags             = ["Charisma", "Feud"],
                Build            = (p, d) => SegmentFactory.CreateConfrontation(
                                        p[0], p[1],
                                        Fallback(d, "You have no idea what you have started."),
                                        "You talk too much.")
            },

            // ── Angle ────────────────────────────────────────────────────────

            new SegmentTemplate
            {
                Name             = "Contract Signing",
                Description      = "A table, two pens, and a brawl. The traditional go-home escalation.",
                Category         = CatAngle,
                Type             = SegmentType.ContractSigning,
                Location         = SegmentLocation.Ring,
                ParticipantRoles = ["Challenger", "Champion"],
                HistoryTags      = [FeudHistoryTag.PersonalInsult, FeudHistoryTag.ChampionshipRivalry],
                BookerTip        = "Longest segment on the card. Stamps ChampionshipRivalry — save it for a title feud.",
                Tags             = ["Feud", "Title"],
                Build            = (p, d) => SegmentFactory.CreateContractSigning(p[0], p[1], endsInBrawl: true)
            },

            new SegmentTemplate
            {
                Name             = "Championship Celebration",
                Description      = "The champion basks in it — and someone walks out to spoil the moment.",
                Category         = CatAngle,
                Type             = SegmentType.Celebration,
                Location         = SegmentLocation.Ring,
                ParticipantRoles = ["Champion", "Interrupter"],
                HistoryTags      = [FeudHistoryTag.ChampionshipRivalry, FeudHistoryTag.TitleStolen],
                BookerTip        = "Cheap, effective way to start the next title programme the night after a win.",
                Tags             = ["Title", "Feud"],
                Build            = (p, d) => SegmentFactory.CreateChampionCelebration(p[0], p[1])
            },

            new SegmentTemplate
            {
                Name             = "Surprise Return",
                Description      = "Unannounced music, a stunned crowd, and an immediate attack.",
                Category         = CatAngle,
                Type             = SegmentType.SurpriseReturn,
                Location         = SegmentLocation.Ring,
                ParticipantRoles = ["Returning wrestler", "Victim"],
                BookerTip        = "Unscripted, so it carries botch risk — but the ×1.15 raw bonus is worth it.",
                Tags             = ["Shock", "Physical"],
                Build            = (p, d) => SegmentFactory.CreateSurpriseReturn(p[0], p[1])
            },

            new SegmentTemplate
            {
                Name             = "Betrayal",
                Description      = "A partner turns. One movement rewrites everything that came before it.",
                Category         = CatAngle,
                Type             = SegmentType.Brawl,
                Location         = SegmentLocation.Ring,
                ParticipantRoles = ["Betrayer", "Victim"],
                HistoryTags      = [FeudHistoryTag.Betrayal],
                BookerTip        = "The most heat of any single segment. Stamps Betrayal for later Revenge Spot payoffs.",
                Tags             = ["Turn", "Feud", "Emotional"],
                Build            = (p, d) => SegmentFactory.CreateBetrayal(p[0], p[1])
            },

            // ── Physical ─────────────────────────────────────────────────────

            new SegmentTemplate
            {
                Name                    = "Post-Match Beatdown",
                Description             = "The bell has rung and it is still going. One wrestler left laid out.",
                Category                = CatPhysical,
                Type                    = SegmentType.Brawl,
                Location                = SegmentLocation.Ring,
                ParticipantRoles        = ["Victim", "Attacker"],
                AllowsExtraParticipants = true,
                ExtraParticipantRole    = "Additional attacker",
                BookerTip               = "Book it directly after the victim's match. Heat scales with attacker count.",
                Tags                    = ["Physical", "Feud"],
                Build                   = (p, d) => SegmentFactory.CreatePostMatchBeatdown(p[0], p.Skip(1).ToList())
            },

            new SegmentTemplate
            {
                Name                    = "Faction Dominance",
                Description             = "A group makes a statement at one wrestler's expense. Numbers game.",
                Category                = CatPhysical,
                Type                    = SegmentType.Brawl,
                Location                = SegmentLocation.Ring,
                ParticipantRoles        = ["Victim", "Faction leader"],
                AllowsExtraParticipants = true,
                ExtraParticipantRole    = "Faction member",
                HistoryTags             = [FeudHistoryTag.FactionConflict],
                BookerTip               = "Stamps FactionConflict. Heat is split across pairings, so bigger is not always better.",
                Tags                    = ["Physical", "Faction"],
                Build                   = (p, d) => SegmentFactory.CreateFactionDominance(p.Skip(1).ToList(), p[0])
            },

            new SegmentTemplate
            {
                Name             = "Crowd Brawl",
                Description      = "It starts at ringside and ends somewhere in the concourse. Total chaos.",
                Category         = CatPhysical,
                Type             = SegmentType.Brawl,
                Location         = SegmentLocation.Crowd,
                ParticipantRoles = ["Wrestler A", "Wrestler B"],
                BookerTip        = "Crowd location gives the best reaction multiplier (×1.10) of any setting.",
                Tags             = ["Physical", "Chaos"],
                Build            = (p, d) => SegmentFactory.CreateCrowdBrawl(p[0], p[1])
            },

            // ── Authority ────────────────────────────────────────────────────

            new SegmentTemplate
            {
                Name                    = "Authority Announcement",
                Description             = "The general manager makes a match, adds a stipulation, or overrules a result.",
                Category                = CatAuthority,
                Type                    = SegmentType.Promo,
                Location                = SegmentLocation.Ring,
                ParticipantRoles        = ["Authority figure", "Affected wrestler"],
                AllowsExtraParticipants = true,
                ExtraParticipantRole    = "Affected wrestler",
                HistoryTags             = [FeudHistoryTag.ManagerConflict],
                UsesDialogue            = true,
                BookerTip               = "Stamps ManagerConflict, which is one of the two tags that unlock the Outside Party beat.",
                Tags                    = ["Authority", "Feud"],
                Build                   = (p, d) => SegmentFactory.CreateGMAnnouncement(
                                                p[0],
                                                Fallback(d, "At the next pay-per-view, you two settle this."),
                                                p.Skip(1).ToList())
            },
        };

        // ── Query helpers ────────────────────────────────────────────────────

        public static IReadOnlyList<string> Categories { get; } =
            All.Select(t => t.Category).Distinct().ToList();

        public static IEnumerable<SegmentTemplate> ByCategory(string category) =>
            All.Where(t => t.Category == category);

        public static SegmentTemplate? Find(string name) =>
            All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Templates that can be staffed from a roster of the given size.</summary>
        public static IEnumerable<SegmentTemplate> Bookable(int rosterSize) =>
            All.Where(t => t.MinParticipants <= rosterSize);

        private static string Fallback(string value, string standIn) =>
            string.IsNullOrWhiteSpace(value) ? standIn : value;
    }
}
