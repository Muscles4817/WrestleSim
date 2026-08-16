using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Models.Person
{
    /// <summary>
    /// Physical attributes, 0–100 (Size is a 1–5 scale).
    ///
    /// These default to competent-but-unremarkable rather than zero. The match engine reads
    /// them, so a wrestler constructed without physicals — a test fixture, a quick edit, a
    /// partially-filled JSON record — should read as an ordinary performer, not as someone
    /// with no stamina, speed or strength at all.
    /// </summary>
    public class PhysicalAttributes
    {
        public int Strength { get; set; } = 70;
        public int Speed { get; set; } = 70;
        public int Agility { get; set; } = 70;
        public int Stamina { get; set; } = 75;
        public int Size { get; set; } = 3; // optional scale: 1-5 or real kg
    }
}
