using WrestlingSim.Enums;

namespace WrestlingSim.Models.Person
{
    /// <summary>
    /// One injury, and when its owner is cleared to work again.
    ///
    /// Held as a **date rather than a countdown**, because the clock in this game does not
    /// always tick a day at a time: running a show on a date three weeks out moves
    /// <see cref="World.Career.CurrentDate"/> straight there, and anything decremented once
    /// per <c>AdvanceOneDay</c> would quietly skip those weeks. A wrestler is fit when the
    /// calendar says so.
    /// </summary>
    public class Injury
    {
        public required BodyPart Part { get; init; }

        /// <summary>The night it happened.</summary>
        public required DateOnly Sustained { get; init; }

        /// <summary>The first day they can be booked again.</summary>
        public required DateOnly ClearedOn { get; init; }

        /// <summary>What it was reported as at the time. For the roster sheet and the save.</summary>
        public int WeeksOut { get; init; }

        /// <summary>Whether this is still keeping them out on a given day.</summary>
        public bool KeepsOut(DateOnly today) => today < ClearedOn;

        /// <summary>Days left, or zero once cleared.</summary>
        public int DaysLeft(DateOnly today) => Math.Max(0, ClearedOn.DayNumber - today.DayNumber);

        /// <summary>How it reads on a roster sheet — "Knee — out 30 weeks".</summary>
        public string Describe(DateOnly today) => $"{Label(Part)} — {Outlook(today)}";

        /// <summary>
        /// How it reads after a name or a word that already owns the dash — "knee, out 30
        /// weeks". <see cref="Describe"/> in that position gives "Rhea Ripley — Knee — out
        /// 30 weeks", which has two dashes doing different jobs and reads as a stutter.
        /// </summary>
        public string Reason(DateOnly today) => $"{Label(Part).ToLowerInvariant()}, {Outlook(today)}";

        private string Outlook(DateOnly today)
        {
            int days = DaysLeft(today);
            if (days == 0) return "cleared";

            int weeks = (int)Math.Ceiling(days / 7.0);
            return weeks == 1 ? "back next week" : $"out {weeks} weeks";
        }

        public static string Label(BodyPart part) => part switch
        {
            BodyPart.Concussion => "Concussion",
            BodyPart.Back       => "Back",
            BodyPart.Shoulder   => "Shoulder",
            BodyPart.Knee       => "Knee",
            BodyPart.Ankle      => "Ankle",
            BodyPart.Bone       => "Fracture",
            BodyPart.Elbow      => "Elbow",
            BodyPart.Pectoral   => "Pectoral",
            BodyPart.Achilles   => "Achilles",
            BodyPart.Neck       => "Neck",
            _                   => part.ToString()
        };
    }
}
