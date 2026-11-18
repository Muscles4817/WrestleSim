using System.Text.Json.Serialization;
using WrestlingSim.Enums;

namespace WrestlingSim.Models.Person
{
    /// <summary>
    /// How somebody talks — docs/gimmick-plan.md §3.2. Factored, because a rating for every
    /// form × staging × register would be 120 numbers nobody could research or read: a form
    /// skill, a staging skill and a register skill, combined when a promo is cut.
    ///
    /// All 0–100, on the roster's charisma scale (docs/roster-rubric.md). Charisma stays its
    /// own thing — presence, not talking.
    /// </summary>
    public class PromoSkills
    {
        /// <summary>Making written words sound like their own.</summary>
        public int Delivery { get; set; } = 70;

        /// <summary>Finding it live, with no script.</summary>
        public int Improvisation { get; set; } = 70;

        public int Stillness { get; set; } = 70;
        public int RingPresence { get; set; } = 70;
        public int CrowdWork { get; set; } = 70;
        public int SetPiece { get; set; } = 70;
        public int Physicality { get; set; } = 70;

        /// <summary>The notes they land naturally: one to three.</summary>
        public List<RegisterSkill> Registers { get; set; } = new();

        /// <summary>How well they land everything else.</summary>
        public int Range { get; set; } = 55;

        public int Staging(PromoStaging s) => s switch
        {
            PromoStaging.Seated   => Stillness,
            PromoStaging.Ring     => RingPresence,
            PromoStaging.Roaming  => CrowdWork,
            PromoStaging.SetPiece => SetPiece,
            _                     => Physicality
        };

        public RegisterSkill? Natural(PromoRegister r) => Registers.FirstOrDefault(x => x.Register == r);

        /// <summary>Their skill in a register: the rating where it is natural, their range where it is not.</summary>
        public int Register(PromoRegister r) => Natural(r)?.Rating ?? Range;

        public PromoSkills Clone() => new()
        {
            Delivery = Delivery, Improvisation = Improvisation,
            Stillness = Stillness, RingPresence = RingPresence, CrowdWork = CrowdWork,
            SetPiece = SetPiece, Physicality = Physicality, Range = Range,
            Registers = Registers.Select(r => new RegisterSkill { Register = r.Register, Rating = r.Rating, Lean = r.Lean }).ToList()
        };
    }

    public class RegisterSkill
    {
        public PromoRegister Register { get; set; }
        public int Rating { get; set; }

        /// <summary>
        /// The form this register comes off best in, when there is a pattern: funny off the cuff
        /// but needing a script to land the serious material.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PromoForm? Lean { get; set; }
    }
}
