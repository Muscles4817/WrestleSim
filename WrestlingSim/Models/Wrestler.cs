using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;
using WrestlingSim.Models.Person;

namespace WrestlingSim.Models
{
    public class Wrestler
    {
        public string RingName => Gimmick?.Name;
        public string RealName { get; set; }
        public List<Gimmick> PreviousGimmicks { get; set; }
        public int Popularity { get; set; }
        public RingSkills RingSkills { get; set; }
        public PhysicalAttributes Physical { get; set; } = new();
        public MentalAttributes Mental { get; set; } = new();
        public Gimmick Gimmick { get; set; }
        public double Charisma { get; set; }
        public List<Move> Moveset { get; set; }
        public List<Signature> Signature { get; set; }
        public WrestlingStyle Style { get; set; }

        public double BaseMatchScore => RingSkills.GetStandardScore(Style);
        public double TechnicalMatchScore => RingSkills.GetTechnicalScore();


        public Wrestler(string realName, Gimmick gimmick, int popularity, RingSkills ringSkills, double charisma, WrestlingStyle style)
        {
            RealName = realName;
            Gimmick = gimmick;
            PreviousGimmicks = new List<Gimmick>();              
            Popularity = popularity;
            RingSkills = ringSkills ?? new RingSkills();
            Charisma = charisma;
            Moveset = new List<Move>();
            Signature = new List<Signature>();
            Style = style;
            Physical = new PhysicalAttributes();
            Mental = new MentalAttributes();
        }

        public void AssignGimmick(Gimmick gimmick)
        {
            Gimmick = gimmick;
        }

        public void ChangeGimmick(Gimmick gimmick)
        {
            if (gimmick == null || ReferenceEquals(gimmick, Gimmick))
                return;

            PreviousGimmicks ??= new List<Gimmick>();

            if (Gimmick != null)
                PreviousGimmicks.Add(Gimmick);

            Gimmick = gimmick;
        }
            
        public void AddMove(Move move)
        {
            Moveset.Add(move);
        }

        public void RemoveMove(Move move)
        { 
            Moveset.Remove(move);
        }

        public void AddSignature(Signature signature)
        {
            Signature.Add(signature);
        }

        public void RemoveSignature(Signature signature)
        {
            Signature.Remove(signature);
        }

        public void ChangeName(string name)
        {
            Gimmick.ChangeName(name);
        }

        public List<string> PreviousNames()
        {
            var names = new List<string>();

            if (PreviousGimmicks != null)
            {
                names.AddRange(
                    PreviousGimmicks
                        .Where(g => g != null)
                        .SelectMany(g => new[] { g.Name }.Concat(g.PreviousNames ?? Enumerable.Empty<string>()))
                );
            }

            if (Gimmick != null)
            {
                names.Add(Gimmick.Name);
                if (Gimmick.PreviousNames != null)
                    names.AddRange(Gimmick.PreviousNames);
            }

            return names;
        }

    }
}
