using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    /// <summary>A wrestler, and the alignment a segment turns them to or teases them toward.</summary>
    public sealed record AlignmentMove(Wrestler Who, Alignment To);
}
