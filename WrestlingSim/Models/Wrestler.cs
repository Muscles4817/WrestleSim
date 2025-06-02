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
        public string RingName { get; set; }
        public string RealName { get; set; }
        public List<string> PreviousNames { get; set; }
        public int Popularity { get; set; }
        public RingSkills RingSkills { get; set; }
        public PhysicalAttributes Physical { get; set; } = new();
        public MentalAttributes Mental { get; set; } = new();
        public Gimmick Gimmick { get; set; } = new();
        public double Charisma { get; set; }
        public List<Move> Moveset { get; set; }
        public List<Signature> Signature { get; set; }
        public WrestlingStyle Style { get; set; }

        public double BaseMatchScore => RingSkills.GetStandardScore(Style);
        public double TechnicalMatchScore => RingSkills.GetTechnicalScore();


        public Wrestler(string ringName, string realName, int popularity, RingSkills ringSkills, double charisma, WrestlingStyle style)
        {
            RingName = ringName;
            RealName = realName ?? ringName;                // If no RealName given, deafults to RingName - Probably should be other way around?
            PreviousNames = new List<string>();              
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

        public void ChangeName(string name)
        {
            PreviousNames.Add(RingName);
            RingName = name;
        }
    }
}
