using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Enums
{
    public enum Alignment
    {
        Face,
        Heel,
        Tweener
    }

    public static class AlignmentExtensions
    {
        /// <summary>The word, for a badge.</summary>
        public static string Label(this Alignment a) => a switch
        {
            Alignment.Face => "Face",
            Alignment.Heel => "Heel",
            _              => "Tweener"
        };

        /// <summary>
        /// The modifier a badge is styled with.
        ///
        /// Face and heel are the two colours the palette already carries, because they are
        /// the pair the whole booking rests on. A tweener is deliberately *not* a third
        /// colour: it is the absence of one, and painting it would say there are three kinds
        /// of alignment to weigh when there are two and an undecided.
        /// </summary>
        public static string Badge(this Alignment a) => a switch
        {
            Alignment.Face => "badge--face",
            Alignment.Heel => "badge--heel",
            _              => "badge--muted"
        };
    }
}
