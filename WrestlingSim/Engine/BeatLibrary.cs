using WrestlingSim.Enums;
using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Catalogue of every named beat archetype available to the player when
    /// booking a match. Templates define what a beat IS; the player then
    /// supplies who has Control and optional intensity/duration overrides.
    /// </summary>
    public static class BeatLibrary
    {
        // ── Categories ───────────────────────────────────────────────────────

        public const string CatOpening      = "Opening";
        public const string CatControl      = "Control";
        public const string CatComeback     = "Comeback";
        public const string CatRestHold     = "Rest Hold";
        public const string CatSpot         = "Spot";
        public const string CatNearFall     = "Near Fall";
        public const string CatStorytelling = "Storytelling";
        public const string CatFinish       = "Finish";

        // ── Full catalogue ───────────────────────────────────────────────────

        public static IReadOnlyList<BeatTemplate> All { get; } = new List<BeatTemplate>
        {
            // ── Openings ─────────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Hot Start",
                Description      = "Both wrestlers explode at the bell with no feeling-out process. Immediately physical.",
                Category         = CatOpening,
                Type             = BeatType.HotOpening,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Best when both wrestlers are over and the crowd is already hot.",
                Tags             = ["Fast", "Exciting"]
            },

            new BeatTemplate
            {
                Name             = "Feeling-Out Process",
                Description      = "A measured, respectful start. Both wrestlers circle and test holds, establishing the chess-match tone.",
                Category         = CatOpening,
                Type             = BeatType.SlowOpening,
                DefaultIntensity = BeatIntensity.Low,
                DefaultDuration  = BeatDuration.Medium,
                BookerTip        = "Maximises the Technical score ceiling. Pairs well with a long match structure.",
                Tags             = ["Technical", "Slow"]
            },

            new BeatTemplate
            {
                Name             = "Standard Collar-and-Elbow",
                Description      = "A conventional opening exchange. Sets the match's tone without committing to either extreme.",
                Category         = CatOpening,
                Type             = BeatType.StandardOpening,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Safe default for any match type.",
                Tags             = ["Neutral"]
            },

            // ── Control ──────────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Power Beatdown",
                Description      = "Dominant physical control — slams, suplexes, ground-and-pound. Establishes the heel's threat level.",
                Category         = CatControl,
                Type             = BeatType.HeatSegment,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Medium,
                BookerTip        = "Heels with low crowd disposition build more tension here.",
                Tags             = ["Power", "Physical"]
            },

            new BeatTemplate
            {
                Name             = "Technical Dissection",
                Description      = "Methodical body-part targeting over an extended period. Establishes a limb story for the finish.",
                Category         = CatControl,
                Type             = BeatType.HeatSegment,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Long,
                BookerTip        = "Contributes the most Technical score of any heat option. Pair with a submission finish.",
                Tags             = ["Technical", "Slow"]
            },

            new BeatTemplate
            {
                Name             = "Methodical Grind",
                Description      = "Slow, suffocating control. Wears the opponent down mentally as much as physically. Crowd patience is tested.",
                Category         = CatControl,
                Type             = BeatType.HeatSegment,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Long,
                BookerTip        = "Use sparingly — a single long grind reads as deliberate. Two in a row reads as boring.",
                Tags             = ["Slow", "Physical"]
            },

            new BeatTemplate
            {
                Name             = "Suplex Run",
                Description      = "Repeated suplex offense in quick succession. The crowd counts along — or tires of it.",
                Category         = CatControl,
                Type             = BeatType.HeatSegment,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Medium,
                BookerTip        = "Effective once. A second Suplex Run in the same match kills the crowd.",
                Tags             = ["Power", "Repetitive"]
            },

            new BeatTemplate
            {
                Name             = "Explosive Flurry",
                Description      = "A short, violent burst of high-intensity offense. Swings momentum hard in one direction.",
                Category         = CatControl,
                Type             = BeatType.HeatSegment,
                DefaultIntensity = BeatIntensity.Extreme,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Great for re-establishing control after a near-fall or comeback. Keeps urgency high.",
                Tags             = ["Fast", "Exciting", "Physical"]
            },

            // ── Comebacks ────────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Hot Comeback",
                Description      = "The crowd pop moment. Running moves, signature spots, the place comes alive. Classic babyface fire.",
                Category         = CatComeback,
                Type             = BeatType.Comeback,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Earns a bigger pop the deeper the momentum deficit going in.",
                Tags             = ["Fast", "Exciting"]
            },

            new BeatTemplate
            {
                Name             = "Fighting Spirit",
                Description      = "Absorbing punishment and firing back on instinct. The never-say-die moment. Forces respect from any crowd.",
                Category         = CatComeback,
                Type             = BeatType.Comeback,
                DefaultIntensity = BeatIntensity.Extreme,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Maximum earnedBonus at extreme momentum deficit. Use as the match's centrepiece.",
                Tags             = ["Fast", "Exciting", "Emotional"]
            },

            new BeatTemplate
            {
                Name             = "Slow Burn Rally",
                Description      = "A gradual, earned recovery. The babyface inches back into it over several exchanges rather than one burst.",
                Category         = CatComeback,
                Type             = BeatType.Comeback,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Medium,
                BookerTip        = "Lower crowd energy spike than a Hot Comeback, but higher storytelling contribution.",
                Tags             = ["Slow", "Emotional"]
            },

            // ── Rest Holds ───────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Wear-Down Hold",
                Description      = "The ref checks the arm. The crowd grows restless. Cools the match after a high-energy sequence.",
                Category         = CatRestHold,
                Type             = BeatType.RestHold,
                DefaultIntensity = BeatIntensity.Low,
                DefaultDuration  = BeatDuration.Long,
                BookerTip        = "Deliberately drains crowd energy. Use to create a valley before the next peak.",
                Tags             = ["Slow", "Technical"]
            },

            new BeatTemplate
            {
                Name             = "Strategic Ground Work",
                Description      = "Working a hold with visible purpose — targeting a specific body part, not just resting.",
                Category         = CatRestHold,
                Type             = BeatType.RestHold,
                DefaultIntensity = BeatIntensity.Low,
                DefaultDuration  = BeatDuration.Medium,
                BookerTip        = "Less crowd drain than the long version. Keeps the Technical narrative alive.",
                Tags             = ["Technical", "Slow"]
            },

            // ── Spots ────────────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Aerial Assault",
                Description      = "A high-risk move from elevation — moonsault, frog splash, diving elbow. Scales with the wrestler's HighFlyer skill.",
                Category         = CatSpot,
                Type             = BeatType.HighSpot,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Works best mid-match to shift momentum. The crowd reacts based on the mover's aerial skill.",
                Tags             = ["Aerial", "Exciting"]
            },

            new BeatTemplate
            {
                Name             = "Jaw-Dropper",
                Description      = "The move nobody expected. A 450° splash from the big man, a shooting star from the powerhouse. Maximum spectacle.",
                Category         = CatSpot,
                Type             = BeatType.HighSpot,
                DefaultIntensity = BeatIntensity.Extreme,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Landing this is about wrestler HighFlyer skill. A 5.0 flyer hits it clean. A 1.0 makes it memorable for other reasons.",
                Tags             = ["Aerial", "Exciting", "Risky"]
            },

            new BeatTemplate
            {
                Name             = "Ringside Brawl",
                Description      = "The match spills to the floor. Barricades, announce tables, referee starts counting.",
                Category         = CatSpot,
                Type             = BeatType.CrowdBrawl,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Useful mid-match reset. Adds unpredictability without requiring a feud.",
                Tags             = ["Brawling", "Exciting"]
            },

            new BeatTemplate
            {
                Name             = "Full-Crowd War",
                Description      = "A sustained brawl through the crowd itself. Weapons, chaos, the audience parts to let them through.",
                Category         = CatSpot,
                Type             = BeatType.CrowdBrawl,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Long,
                BookerTip        = "Best in grudge matches. Can substitute for a FeudalEscalation when the feud isn't formally coded.",
                Tags             = ["Brawling", "Exciting", "Physical"]
            },

            // ── Near Falls ───────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Signature Cover",
                Description      = "A big move lands, the cover is made, the crowd holds its breath for a two-count.",
                Category         = CatNearFall,
                Type             = BeatType.NearFall,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "The bread-and-butter near-fall. Most effective before the match's energy has peaked.",
                Tags             = ["Exciting"]
            },

            new BeatTemplate
            {
                Name             = "Shock Kickout",
                Description      = "The move that should have ended it. The crowd cannot believe the shoulder went up. Diminishing returns apply.",
                Category         = CatNearFall,
                Type             = BeatType.NearFall,
                DefaultIntensity = BeatIntensity.Extreme,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Use once for maximum impact. The second Shock Kickout in a match never lands as hard as the first.",
                Tags             = ["Exciting", "Emotional"]
            },

            new BeatTemplate
            {
                Name             = "Counter Roll-Up",
                Description      = "A scramble near-fall out of a counter or reversal. Quick, electric, unpredictable.",
                Category         = CatNearFall,
                Type             = BeatType.NearFall,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Good momentum-equaliser. Use to give the underdog a flash of hope without a full comeback.",
                Tags             = ["Fast", "Technical"]
            },

            // ── Storytelling ─────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Mind Games",
                Description      = "A calculated taunt or psychological play. Gets inside the opponent's head. Charisma-driven.",
                Category         = CatStorytelling,
                Type             = BeatType.PsychologicalWarfare,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Story contribution scales with the controlling wrestler's Charisma. Works in any match.",
                Tags             = ["Psychological", "Charisma"]
            },

            new BeatTemplate
            {
                Name             = "Trash Talk",
                Description      = "Verbal and physical taunting — getting in the opponent's face, mocking them in front of their own crowd.",
                Category         = CatStorytelling,
                Type             = BeatType.PsychologicalWarfare,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Bigger story contribution than Mind Games. Works best when the taunting wrestler is despised.",
                Tags             = ["Psychological", "Charisma"]
            },

            new BeatTemplate
            {
                Name             = "Revenge Spot",
                Description      = "A callback — the wrestler does to their opponent exactly what was done to them. The crowd recognises it immediately.",
                Category         = CatStorytelling,
                Type             = BeatType.RevengeSpot,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Highly effective with a feud. Still works without one if there's an in-match callback to mirror.",
                Tags             = ["Emotional", "Feud"]
            },

            new BeatTemplate
            {
                Name             = "Feud Erupts",
                Description      = "Months of built-up tension releases at once. Both wrestlers lose all composure. The arena explodes.",
                Category         = CatStorytelling,
                Type             = BeatType.FeudalEscalation,
                DefaultIntensity = BeatIntensity.Extreme,
                DefaultDuration  = BeatDuration.Short,
                BookerTip        = "Requires feud at Building intensity or higher. The payoff of every prior encounter.",
                Tags             = ["Emotional", "Feud", "Exciting"],
                RequiredFeudIntensity = FeudIntensity.Building
            },

            new BeatTemplate
            {
                Name             = "Outside Party",
                Description      = "A third party connected to the feud makes their presence felt — a manager, family member, or faction ally.",
                Category         = CatStorytelling,
                Type             = BeatType.ThirdPartyPullIn,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Requires feud history tag FamilyInvolved or ManagerConflict. Injects chaos into a stalled match.",
                Tags             = ["Feud", "Chaos"],
                RequiredFeudIntensity = FeudIntensity.Building
            },

            new BeatTemplate
            {
                Name             = "Goes It Alone",
                Description      = "The wrestler sends away their own outside help and chooses to compete alone. One of the biggest crowd pops in wrestling when the moment is earned.",
                Category         = CatStorytelling,
                Type             = BeatType.AlliesRejected,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Must follow a ThirdPartyPullIn earlier in the match. Crowd reaction scales with the wrestler's own disposition — higher = Toronto Hogan level pop.",
                Tags             = ["Emotional", "Character", "Crowd"]
            },

            // ── Finishes ─────────────────────────────────────────────────────

            new BeatTemplate
            {
                Name             = "Clean Victory",
                Description      = "Hits the finisher, covers, three-count. Decisive and unambiguous. The crowd knows exactly who won and why.",
                Category         = CatFinish,
                Type             = BeatType.FinishClean,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Earn it: the finish quality penalty applies if momentum doesn't favour the winner.",
                Tags             = ["Clean"]
            },

            new BeatTemplate
            {
                Name             = "Dominant Statement",
                Description      = "A second finisher on top of the first. An emphatic, unmistakable victory that sends a message.",
                Category         = CatFinish,
                Type             = BeatType.FinishSuperFinisher,
                DefaultIntensity = BeatIntensity.Extreme,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Best used to establish dominance over a respected opponent. Overkill is the point.",
                Tags             = ["Clean", "Dominant"]
            },

            new BeatTemplate
            {
                Name             = "Roll-Up Steal",
                Description      = "A surprise small package or cradle. The crowd pops for the audacity. The loser has nobody to blame but themselves.",
                Category         = CatFinish,
                Type             = BeatType.FinishRollup,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Good for heels stealing a win or for setting up a rematch. Not penalised for being unearned.",
                Tags             = ["Surprise", "Heel"]
            },

            new BeatTemplate
            {
                Name             = "Tap Out",
                Description      = "The submission hold is locked in. The opponent fights it, tests the crowd's patience, then finally taps.",
                Category         = CatFinish,
                Type             = BeatType.FinishSubmission,
                DefaultIntensity = BeatIntensity.High,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Pairs naturally with Technical Dissection earlier in the match. The limb story pays off.",
                Tags             = ["Technical", "Clean"]
            },

            new BeatTemplate
            {
                Name             = "Dirty Win",
                Description      = "Outside interference tips the result. The villain escapes with the title by any means necessary.",
                Category         = CatFinish,
                Type             = BeatType.FinishInterference,
                DefaultIntensity = BeatIntensity.Medium,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Keeps the heel strong while protecting the face. Sets up the rematch. Crowds hate it — that's the idea.",
                Tags             = ["Heel", "Feud", "Controversy"]
            },

            new BeatTemplate
            {
                Name             = "DQ Finish",
                Description      = "Someone loses control and crosses a line. The referee has no choice. A win on paper that satisfies nobody.",
                Category         = CatFinish,
                Type             = BeatType.FinishDQ,
                DefaultIntensity = BeatIntensity.Low,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Lowest crowd satisfaction of any finish. Use when protecting both wrestlers is the only priority.",
                Tags             = ["Controversy", "Heel"]
            },

            new BeatTemplate
            {
                Name             = "Count-Out",
                Description      = "The wrestler can't beat the referee's count. A hollow, unsatisfying result the crowd resents.",
                Category         = CatFinish,
                Type             = BeatType.FinishCountout,
                DefaultIntensity = BeatIntensity.Low,
                DefaultDuration  = BeatDuration.Brief,
                BookerTip        = "Even weaker than a DQ finish. Reserve for injury angles or deliberate heel cowardice spots.",
                Tags             = ["Controversy"]
            },
        };

        // ── Query helpers ────────────────────────────────────────────────────

        public static IReadOnlyList<string> Categories { get; } =
            All.Select(t => t.Category).Distinct().ToList();

        public static IEnumerable<BeatTemplate> ByCategory(string category) =>
            All.Where(t => t.Category == category);

        public static IEnumerable<BeatTemplate> ForBeatType(BeatType type) =>
            All.Where(t => t.Type == type);

        /// <summary>
        /// Returns only templates that are legally bookable given the current feud state.
        /// Applies the same gating rules as MatchPlan.Validate().
        /// </summary>
        public static IEnumerable<BeatTemplate> Available(Feud? feud) =>
            All.Where(t => t.RequiredFeudIntensity == FeudIntensity.None
                        || (feud != null && feud.Intensity >= t.RequiredFeudIntensity));

        public static BeatTemplate? Find(string name) =>
            All.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
