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

        /// <summary>
        /// 0–100. The accumulated audience relationship — how much this audience cares
        /// about this person. A **stock**: slow to build, slow to lose.
        ///
        /// Distinct from <see cref="Momentum"/>, which is the flow. Someone can be very
        /// over and cold (a beloved veteran nobody is currently excited about) or barely
        /// over and red hot (a new signing generating buzz with no depth behind it).
        /// See docs/wrestling-reference/17-heat-and-getting-over.md §1.1.
        ///
        /// Continuous rather than whole: a single result often moves standing by a
        /// fraction of a point, and rounding each one to an integer would discard every
        /// small change instead of letting them accumulate.
        /// </summary>
        public double Overness { get; set; }

        /// <summary>
        /// −100…+100, centred on 0. Which way this person is currently trending. A
        /// **flow**: fast to gain, fast to lose, and it decays toward zero when nothing
        /// is happening to them.
        ///
        /// Positive means rising — reactions growing week over week. Negative means
        /// cooling. It is what makes "getting hot" and "going cold" expressible at all.
        /// </summary>
        public double Momentum { get; set; }

        /// <summary>
        /// The last show this person appeared on, in world time. Absence with no story is
        /// how a mid-carder quietly cools (doc 17 §3.9), so the clock needs to know.
        /// Null means they have not worked yet this career.
        /// </summary>
        public DateOnly? LastAppearance { get; set; }
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
        /// How over this person reads *right now*: the stock, pushed around by the flow.
        ///
        /// Momentum is deliberately worth much less than Overness — being hot lets you
        /// punch above your weight for a while, it does not make you a main eventer. This
        /// is what the crowd actually responds to, so it is what the engine reads.
        /// </summary>
        public double EffectiveOverness =>
            Math.Clamp(Overness + Momentum * MomentumWeight, 0, 100);

        /// <summary>How far momentum can carry someone past their standing, either way.</summary>
        public const double MomentumWeight = 0.15;

        /// <summary>
        /// Where this wrestler sits on a card, derived from how over they are. Purely a
        /// presentation aid — the engine reads the underlying attributes, not this.
        /// </summary>
        /// <summary>Overness rounded for display.</summary>
        public int OvernessDisplay => (int)Math.Round(Overness);

        public CardPosition CardPosition => (int)Math.Round(EffectiveOverness) switch
        {
            >= 88 => CardPosition.MainEvent,
            >= 74 => CardPosition.UpperCard,
            >= 56 => CardPosition.Midcard,
            >= 36 => CardPosition.LowerCard,
            _     => CardPosition.Enhancement
        };

        public double BaseMatchScore => RingSkills.GetStandardScore(Style);
        public double TechnicalMatchScore => RingSkills.GetTechnicalScore();


        public Wrestler(string realName, Gimmick gimmick, double overness, RingSkills ringSkills, double charisma, WrestlingStyle style)
        {
            RealName = realName;
            Gimmick = gimmick;
            PreviousGimmicks = new List<Gimmick>();
            Overness = overness;
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
