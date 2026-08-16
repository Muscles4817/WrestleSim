using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Models.Person
{
    public class PhysicalAttributes
    {
        public int Strength { get; set; }
        public int Speed { get; set; }
        public int Agility { get; set; }
        public int Stamina { get; set; }
        public int Size { get; set; } // optional scale: 1-5 or real kg
    }
}
