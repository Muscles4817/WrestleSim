using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    public class Segment
    {
        public string Name { get; set; }
        public SegmentType Type { get; set; } // Promo, Brawl, etc.
        public SegmentLocation Location { get; set; }
        public List<SegmentAction> Actions { get; set; } = new();
        public List<Wrestler> Participants { get; set; } = new();
        public bool IsScripted { get; set; } // False = more botch risk
        public double AudienceImpact { get; set; } // Calculated later
        public double HeatImpact { get; set; } // For ongoing feuds

        public Segment(string name, SegmentType type, SegmentLocation location, bool isScripted)
        {
            Name = name;
            Type = type;
            Location = location;
            IsScripted = isScripted;
        }

        public void AddParticipant(Wrestler wrestler)
        {
            Participants.Add(wrestler);
        }

        public void AddAction(SegmentAction action)
        {
            Actions.Add(action);
        }
    }
}
