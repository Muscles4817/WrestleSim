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
        
        public Move (string name, List<MoveType> types, SkillCheck risk, SkillCheck difficulty, SkillCheck impact, bool requiresWeapons)
        {
            Name = name;
            Types = types;
            Risk = risk;                            
            Difficulty = difficulty;                
            Impact = impact;                        
            RequiresWeapons = requiresWeapons;      // If true move can only be performed with a weapon
            WeaponsSet = new List<Weapons>();       // List of weapons that move can be performed with. If empty move cannot be performed with weapon
        }

        public void AddWeapon(Weapons weapon)
        {
            WeaponsSet.Add(weapon);
        }

        public void RemoveWeapon(Weapons weapon) 
        {
            WeaponsSet.Remove(weapon); 
        }
    }
}

