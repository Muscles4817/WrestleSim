using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models
{
    public class Weapons
    {
        public string Name { get; set; }
        public SkillCheck Risk { get; set; }
        public SkillCheck Impact { get; set; }

        public Weapons (string name, SkillCheck risk, SkillCheck impact)
        {
            Name = name;
            Risk = risk;
            Impact = impact;
        }
    }
}

// suggested by CGPT:
// risk, impact, crowdreaction, isCommon, 
// isIllegal (if weapon breaks match rules - not sure how that would work as different matches have different rules - better to have this in match types)