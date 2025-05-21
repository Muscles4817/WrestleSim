using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models
{
    public class Signature
    {
        public string Name { get; set; }
        public Move Move { get; set; }
        public bool IsFinisher { get; set; }
        public SkillCheck ImpactModifier { get; set; }
        public SkillCheck Overness { get; set; }

        public Signature (string name, Move move, bool isFinisher, SkillCheck impactModifier, SkillCheck overness)
        {
            Name = name;
            Move = move;
            IsFinisher = isFinisher;
            ImpactModifier = impactModifier;
            Overness = overness;
        }

        public void functionname()
        {
            // This is an example of an empty function which throws a not implemented exception
            throw new NotImplementedException();
        }
    }
}
