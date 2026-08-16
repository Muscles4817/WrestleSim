using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    public class SegmentAction
    {
        public SegmentActionType ActionType { get; set; } // Talk, Interrupt, Attack
        public Wrestler Performer { get; set; }
        public Wrestler Target { get; set; } // Optional
        public string Dialogue { get; set; } // For promos
        public double HeatImpact { get; set; } // Feud heat change
        public double OvernessImpact { get; set; } // Wrestler overness change
    }

}
