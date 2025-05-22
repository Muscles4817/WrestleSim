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
        public List<MoveType> Types { get; set; }
        public SkillCheck Risk { get; set; }
        public SkillCheck Difficulty { get; set; }
        public SkillCheck Impact { get; set; }
        public bool RequiresWeapons { get; set; } 
        public List<Weapons> WeaponsSet { get; set; }
        
        public Move (string name, List<MoveType> types, SkillCheck risk, SkillCheck difficulty, SkillCheck impact, bool requiresWeapons, List<Weapons> weaponsSet)
        {
            Name = name;
            Types = types;
            Risk = risk;
            Difficulty = difficulty;
            Impact = impact;
            RequiresWeapons = requiresWeapons;
            WeaponsSet = weaponsSet;
        }
    }
}

// RequiresWeapon bool. Applicable Weapons (if empty no weapons can be used, if weapons on list then that weapon can be used for the move even if not a RequiresWeapon move)
// reconsider name for WeaponSet? 