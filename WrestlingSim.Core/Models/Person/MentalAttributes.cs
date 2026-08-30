using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Models.Person
{
    /// <summary>
    /// Mental attributes, 0–100.
    ///
    /// Default to competent-but-unremarkable for the same reason as
    /// <see cref="PhysicalAttributes"/>: the engine reads these, so an unset value must not
    /// silently mean "the worst performer imaginable".
    /// </summary>
    public class MentalAttributes
    {
        public int Psychology { get; set; } = 70;
        public int Selling { get; set; } = 70;
        public int RingIQ { get; set; } = 70;
        public int Toughness { get; set; } = 75;
    }
}
