using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    public class SegmentAction
    {
        public SegmentActionType ActionType { get; set; } // Talk, Interrupt, Attack
        public Wrestler Performer { get; set; }
        public Wrestler? Target { get; set; } // Required for physical actions
        public string Dialogue { get; set; } = "";
        public double HeatImpact { get; set; } // Feud heat change
        public double OvernessImpact { get; set; } // Wrestler overness change

        /// <summary>
        /// Base audience impact of this action before charisma, location and delivery.
        /// Set from the action template so template choice is mechanically meaningful.
        /// </summary>
        public double BaseImpact { get; set; }

        /// <summary>Label from the template this was built from, for play-by-play.</summary>
        public string Label { get; set; } = "";

        /// <summary>Physical actions land on somebody; talking does not have to.</summary>
        public bool RequiresTarget => ActionType is
            SegmentActionType.Attack or SegmentActionType.Betrayal;

        /// <summary>Physical actions carry injury risk and generate the most heat.</summary>
        public bool IsPhysical => ActionType is
            SegmentActionType.Attack or SegmentActionType.Betrayal or SegmentActionType.RunIn;
    }
}
