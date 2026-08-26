using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models
{
    public class Gimmick
    {
        public string Name { get; set; }
        public GimmickType Type { get; set; }                // Monster, Anti-Hero, Showman, etc.
        public string PersonaDescriptor { get; set; }        // "Cursed Cowboy", "Corporate Sellout"
        public GimmickTone Tone { get; set; }                // Serious, Comedic, Cryptic, etc.
        public Alignment NaturalAlignment { get; set; }      // Face, Heel, Tweener
        public int PopularityModifier { get; set; }          // Affects crowd engagement
        public GimmickDurability Durability { get; set; }    // How long before it gets stale
        public double Freshness { get; set; }                // 0.0 to 1.0, decays over time
        public List<string> GimmickTraits { get; set; }       // Optional: extra flavor flags
        public List<FanGroupAppeal> AppealRatings { get; set; } // Different fanbase responses
        public List<string> PreviousNames { get; set; }

        public Gimmick()
        {
            GimmickTraits = new List<string>();
            AppealRatings = new List<FanGroupAppeal>();
            PreviousNames = new List<string>();
        }

        public Gimmick(string name) : this()
        {
            Name = name;
        }

        public void ChangeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == Name)
                return;

            PreviousNames ??= new List<string>();

            if (!string.IsNullOrWhiteSpace(Name))
                PreviousNames.Add(Name);

            Name = name;
        }
    }
}
