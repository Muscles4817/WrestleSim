using WrestlingSim.Enums;

namespace WrestlingSim.Models.World
{
    /// <summary>A standing note on how somebody's promos are written (docs/gimmick-plan.md §3.4).</summary>
    public sealed class PromoNote
    {
        /// <summary>Their usual material.</summary>
        public PromoForm Usual { get; set; } = PromoForm.BulletPoints;

        /// <summary>Anything that is not meant to be funny, when that should differ. Null is the same as usual.</summary>
        public PromoForm? Serious { get; set; }
    }
}
