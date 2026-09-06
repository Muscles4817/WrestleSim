using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// A named, pre-built sequence of beats representing a recognisable match structure.
    /// Structures live in MatchStructureLibrary; the user picks one as a starting point
    /// and edits the beats before executing.
    /// </summary>
    public class MatchStructure
    {
        public required string Name        { get; init; }
        public required string Description { get; init; }

        /// <summary>
        /// How many people per side this structure is written for. 1 is a singles
        /// structure; 2 is a tag structure and its beats will not validate against a side
        /// with nobody on the apron. The booking UI filters on this so a player is never
        /// offered a hot tag for a singles match.
        /// </summary>
        public int SideSize { get; init; } = 1;

        /// <summary>Tags for filtering (e.g. "Technical", "Brawl", "Feud").</summary>
        public IReadOnlyList<string> Tags { get; init; } = [];

        /// <summary>
        /// Whether this structure requires an active feud on the MatchPlan to pass validation.
        /// Users should be warned when selecting a feud-gated structure.
        /// </summary>
        public bool RequiresFeud { get; init; } = false;

        /// <summary>
        /// The pre-populated beat sequence. WrestlerA = the face / intended winner by default.
        /// Every control assignment can be changed before the plan is executed.
        /// </summary>
        public required IReadOnlyList<MatchBeat> Beats { get; init; }

        public override string ToString() => Name;
    }
}
