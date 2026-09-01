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
        /// <summary>
        /// Stable identity for saves. Derived from RealName unless the roster data sets
        /// one explicitly, because RealName is already what FeudBook keys on and is the
        /// only field guaranteed not to change with a gimmick swap.
        ///
        /// A save stores wrestler state against this, so it must stay stable across
        /// roster edits — rename someone in Wrestlers.json and existing saves lose them.
        /// </summary>
        public string Id
        {
            get => _id ??= SlugOf(RealName);
            set => _id = string.IsNullOrWhiteSpace(value) ? null : value;
        }
        private string? _id;

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
        public Division Division { get; set; } = Division.Womens;

        /// <summary>
        /// Where this wrestler sits on a card, derived from popularity. Purely a
        /// presentation aid — the engine reads the underlying attributes, not this.
        /// </summary>
        public CardPosition CardPosition => Popularity switch
        {
            >= 88 => CardPosition.MainEvent,
            >= 74 => CardPosition.UpperCard,
            >= 56 => CardPosition.Midcard,
            >= 36 => CardPosition.LowerCard,
            _     => CardPosition.Enhancement
        };

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

        /// <summary>Lower-case, punctuation-free form of a name, for use as a stable key.</summary>
        private static string SlugOf(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";

            var chars = name.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-');

            // Collapse runs of separators so "J. J.  Smith" and "J-J-Smith" agree.
            var slug = new string(chars.ToArray());
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Trim('-');
        }

        public void ChangeName(string name)
        {
            Gimmick.ChangeName(name);
        }

        public void ChangeGimmick(Gimmick gimmick)
        { 
            PreviousGimmicks.Add(gimmick);
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
