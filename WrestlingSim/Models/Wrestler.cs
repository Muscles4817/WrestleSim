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
        public string Name { get; set; }
        public int Popularity { get; set; }

        public RingSkills RingSkills { get; set; }
        public PhysicalAttributes Physical { get; set; } = new();
        public MentalAttributes Mental { get; set; } = new();
        public double Charisma { get; set; }
        public List<Move> Moveset { get; set; }
        public List<Signature> Signature { get; set; }
        public WrestlingStyle Style { get; set; }

        public double BaseMatchScore => RingSkills.GetStandardScore(Style);
        public double TechnicalMatchScore => RingSkills.GetTechnicalScore();


        public Wrestler(string name, int popularity, RingSkills ringSkills, double charisma, WrestlingStyle style)
        {
            Name = name;
            Popularity = popularity;
            RingSkills = ringSkills ?? new RingSkills();
            Charisma = charisma;
            Moveset = new List<Move>();
            Signature = new List<Signature>();
            Style = style;
            Physical = new PhysicalAttributes();
            Mental = new MentalAttributes();
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
    }
}
