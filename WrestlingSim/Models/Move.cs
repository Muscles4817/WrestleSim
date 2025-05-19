using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models
{
    public class Move
    {
        public string Name { get; set; }
        public int Difficulty { get; set; }
        public int Impact { get; set; }
        public int Risk { get; set; }
        public MoveType Type { get; set; }
        
        public Move (string name, int difficulty, int impact, MoveType type)
        {
            Name = name;
            Difficulty = difficulty;
            Impact = impact;
            Type = type;
        }
    }
}
