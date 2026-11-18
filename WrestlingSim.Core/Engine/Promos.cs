using WrestlingSim.Enums;
using WrestlingSim.Models;
using WrestlingSim.Models.Person;
using WrestlingSim.Models.World;
using SegmentItem = WrestlingSim.Models.Segment.Segment;

namespace WrestlingSim.Engine
{
    /// <summary>
    /// Promos as performances — docs/gimmick-plan.md Part 3.
    ///
    /// A promo asks something on three axes at once: how much is written (form), what it
    /// physically asks of them (staging), and what it is meant to make the crowd feel
    /// (register). What it gets back depends on the people: the writers, for whatever is
    /// written, and the performer's skill at that form, in that staging, in that register.
    ///
    /// **Neither form wins by rule** (owner, 17 Nov 2026). The first version gave every
    /// unscripted promo ×1.15 and let no scripted one go wrong; both are gone. A great writing
    /// room and a great deliverer are excellent and reliable; a great improviser is excellent;
    /// a poor talker given a script is dull and a poor talker handed the microphone is a
    /// disaster — and none of that is written below as a rule about the form. It falls out of
    /// the inputs, and so does how far a promo can swing: the spread is wide where the skill
    /// being leaned on is low, whatever the form.
    ///
    /// Every number here is a design estimate. Nobody has measured these.
    /// </summary>
    public static class Promos
    {
        // ── The performer ────────────────────────────────────────────────────

        /// <summary>The note a gimmick's tone plays by default.</summary>
        public static PromoRegister NaturalRegister(GimmickTone? tone) => tone switch
        {
            GimmickTone.Humorous      => PromoRegister.Funny,
            GimmickTone.Cryptic       => PromoRegister.Menacing,
            GimmickTone.Arrogant      => PromoRegister.Arrogant,
            GimmickTone.SelfRighteous => PromoRegister.Arrogant,
            GimmickTone.Inspirational => PromoRegister.Inspirational,
            GimmickTone.Melancholic   => PromoRegister.Emotional,
            GimmickTone.Violent       => PromoRegister.Angry,
            GimmickTone.Chaotic       => PromoRegister.Angry,
            _                         => PromoRegister.Cold
        };

        /// <summary>
        /// Skills for somebody nobody has judged yet: their charisma, everywhere, with their
        /// gimmick's note as the one they play best. A flat shape, honestly so — the real roster
        /// is judged (Wrestlers.json), and a generated wrestler copies a real one's shape.
        /// </summary>
        public static PromoSkills Derived(Wrestler w)
        {
            int c = (int)Math.Round(Math.Clamp(w.Charisma, 0, 5) * 20);
            return new PromoSkills
            {
                Delivery = c, Improvisation = Math.Max(0, c - 5),
                Stillness = c, RingPresence = c, CrowdWork = c, SetPiece = c, Physicality = c,
                Registers = [new RegisterSkill { Register = NaturalRegister(w.Gimmick?.Tone), Rating = c }],
                Range = Math.Max(0, c - 15)
            };
        }

        public static PromoSkills SkillsOf(Wrestler w) => w.Promo ?? Derived(w);

        // ── The segment ──────────────────────────────────────────────────────

        /// <summary>What a segment physically asks, when the booker has not said.</summary>
        public static PromoStaging StagingOf(SegmentItem s) => s.Staging ?? (s.Location, s.Type) switch
        {
            (SegmentLocation.Backstage or SegmentLocation.GMOffice, _) => PromoStaging.Seated,
            (SegmentLocation.Crowd, _)                                 => PromoStaging.Roaming,
            (SegmentLocation.ParkingLot, _)                            => PromoStaging.Physical,
            (_, SegmentType.ContractSigning)                           => PromoStaging.Seated,
            (_, SegmentType.Brawl)                                     => PromoStaging.Physical,
            _                                                          => PromoStaging.Ring
        };

        /// <summary>The note this speaker is asked to play: the segment's, or their own.</summary>
        public static PromoRegister RegisterFor(SegmentItem s, Wrestler speaker) =>
            s.Register ?? NaturalRegister(speaker.Gimmick?.Tone);

        // ── The writers ──────────────────────────────────────────────────────
        //
        // A creative team rated 1–10, upgraded with money like a coach in a football management
        // career mode (owner, 17 Nov 2026). **A stopgap**, written down as one: the intended
        // system is a writers' room of named writers with their own strengths, where the
        // decision is the combination rather than the most expensive (plan §3.3).

        public const int MinCreative = 1, MaxCreative = 10;

        /// <summary>Where a promotion of this size starts. Any promotion can buy its way to ten.</summary>
        public static int StartingCreative(PromotionTier tier) => tier switch
        {
            PromotionTier.Local       => 1,
            PromotionTier.Independent => 2,
            PromotionTier.SuperIndie  => 3,
            PromotionTier.Established => 5,
            PromotionTier.National    => 6,
            PromotionTier.Global      => 7,
            _                         => 2
        };

        public static int CreativeOf(Promotion p) => p.CreativeLevel ?? StartingCreative(p.Tier);

        /// <summary>
        /// What a level costs a day *above what the tier's overheads already pay for*. The
        /// opening team is in the overhead figures the business tests pin, so it is free here;
        /// every level bought above it costs, and the price climbs with the square.
        /// </summary>
        public static decimal UpkeepPerDay(PromotionTier tier, int level)
        {
            int start = StartingCreative(tier);
            return level <= start ? 0m : 15m * (level * level - start * start);
        }

        /// <summary>What it costs, once, to go up a level: sixty days of what the new level costs to keep.</summary>
        public static decimal UpgradeCost(int toLevel) =>
            60m * 15m * (toLevel * toLevel - (toLevel - 1) * (toLevel - 1));

        /// <summary>The writing, on the performers' 0–100 scale.</summary>
        public static int Writing(int level) => Math.Clamp(level, MinCreative, MaxCreative) * 10;

        public static string CreativeWords(int level) => level switch
        {
            >= 9 => "A great writing room",
            >= 7 => "A strong writing room",
            >= 5 => "A capable writing room",
            >= 3 => "A thin writing room",
            _    => "Barely a writing room"
        };

        // ── One person, one promo ────────────────────────────────────────────

        /// <summary>
        /// What a promo is likely to come out at for one speaker, 0–100, and how far it can
        /// swing. <see cref="Deciding"/> names what decided it, for the report.
        /// </summary>
        public readonly record struct Reading(double Expected, double Spread, string Deciding, double FormSkill);

        /// <summary>
        /// Characters a register works against: a monster asked to be afraid, a comedy act asked
        /// to be menacing. Sometimes that is the story; it is always harder to land.
        /// </summary>
        public static double OutOfCharacter(GimmickType? type, PromoRegister r) => (type, r) switch
        {
            (GimmickType.Monster or GimmickType.Superstar, PromoRegister.Fearful) => 0.80,
            (GimmickType.Monster or GimmickType.DarkPersona or GimmickType.Cryptic or GimmickType.CultLeader, PromoRegister.Funny) => 0.80,
            (GimmickType.Comedy, PromoRegister.Menacing or PromoRegister.Cold) => 0.80,
            (GimmickType.Underdog, PromoRegister.Arrogant or PromoRegister.Menacing) => 0.85,
            (GimmickType.Egotist or GimmickType.Sellout, PromoRegister.Inspirational or PromoRegister.Fearful) => 0.85,
            _ => 1.0
        };

        public static Reading Read(Wrestler w, PromoForm form, PromoStaging staging, PromoRegister register, int writing)
        {
            var p = SkillsOf(w);
            var natural = p.Natural(register);

            // The form: the writing and the reading of it, multiplied so neither can carry the
            // other — a great reader cannot rescue bad writing, and great writing dies in a
            // poor reader's mouth. Free is the performer alone. Bullet points are both halves.
            double scripted = Math.Pow(Math.Max(1, p.Delivery), 0.55) * Math.Pow(Math.Max(1, writing), 0.45);
            double free = p.Improvisation;
            double formSkill = form switch
            {
                PromoForm.Scripted => scripted,
                PromoForm.Free     => free,
                _                  => 0.5 * scripted + 0.5 * free
            };

            // Where a register comes off best in one form, the other form costs in it.
            if (natural?.Lean is { } lean && lean != PromoForm.BulletPoints && form != PromoForm.BulletPoints)
                formSkill *= lean == form ? 1.05 : 0.85;

            double stagingSkill  = p.Staging(staging);
            double registerSkill = p.Register(register) * OutOfCharacter(w.Gimmick?.Type, register);

            // Combined as a weighted geometric mean, so a real gap anywhere shows: a superb
            // talker asked to brawl, or to be afraid, is not superb at it.
            double performance = Math.Pow(Math.Max(1, formSkill), 0.40)
                               * Math.Pow(Math.Max(1, stagingSkill), 0.25)
                               * Math.Pow(Math.Max(1, registerSkill), 0.35);

            // Presence: charisma is not talking, but the crowd looks at whoever has it.
            double expected = Math.Clamp(0.85 * performance + 0.15 * Math.Clamp(w.Charisma, 0, 5) * 20, 0, 100);

            // How far it can swing comes from how weak the leaned-on skill is, whatever the form.
            // Psychology keeps an off-the-cuff promo on the rails, and only an off-the-cuff one.
            double leaned = Math.Min(formSkill, Math.Min(stagingSkill, registerSkill));
            double spread = 4 + 0.15 * (100 - Math.Clamp(leaned, 0, 100));
            if (form != PromoForm.Scripted && (w.Mental?.Psychology ?? 70) < 60) spread += 4;

            string deciding =
                formSkill <= stagingSkill && formSkill <= registerSkill ? "form"
                : stagingSkill <= registerSkill ? "staging" : "register";

            return new Reading(expected, spread, deciding, formSkill);
        }

        /// <summary>A promo cut: the expected figure and a normal roll on its spread.</summary>
        public static double Cut(Reading r, Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Math.Clamp(r.Expected + z * r.Spread, 0, 100);
        }

        /// <summary>
        /// What turns a promo's 0–100 into the 0–5 a segment's impact reads, where charisma used
        /// to sit. **Measured, not asserted**: the shipped roster's mean charisma less the mean
        /// promo a typical promotion gets from them (bullet points, the established tier's
        /// writers, their own note), so the typical segment is worth what it was and segment
        /// values on a card stay in range. A roster change or a change to the promo model moves
        /// it; <c>ATypicalPromoLandsWhereATypicalPromoDid</c> fails when the two part company —
        /// re-measure, do not widen.
        /// </summary>
        public const double Centring = 0.298;

        /// <summary>A promo's value on the 0–5 scale a segment reads.</summary>
        public static double OnSegmentScale(double promo) => Math.Max(0, promo / 20.0 + Centring);

        /// <summary>Below this a promo has fallen apart.</summary>
        public const double Bombed = 30;

        /// <summary>At or above this it is one people remember.</summary>
        public const double Landed = 85;

        // ── Words ────────────────────────────────────────────────────────────

        public static string Band(double v) => v switch
        {
            >= 95 => "elite",
            >= 85 => "strong",
            >= 70 => "good",
            >= 55 => "serviceable",
            >= 40 => "weak",
            _     => "keep them off the microphone"
        };

        public static string Name(PromoForm f) => f switch
        {
            PromoForm.Scripted     => "scripted",
            PromoForm.BulletPoints => "bullet points",
            _                      => "free rein"
        };

        public static string Name(PromoStaging s) => s switch
        {
            PromoStaging.Seated   => "seated",
            PromoStaging.Ring     => "in the ring",
            PromoStaging.Roaming  => "working the crowd",
            PromoStaging.SetPiece => "a set piece",
            _                     => "physical"
        };

        public static string Name(PromoRegister r) => r.ToString().ToLowerInvariant();

        /// <summary>The report's line for one speaker: what happened, and the axis that decided it.</summary>
        public static string Line(Wrestler w, PromoForm form, PromoStaging staging, PromoRegister register, Reading r, double cut)
        {
            string who = w.RingName ?? "They";
            if (cut >= Landed)
                return form == PromoForm.Scripted
                    ? $"{who} took what was written and made every word their own."
                    : $"Handed the microphone, {who} found the crowd and never let go.";
            if (cut < Bombed)
                return (form, r.Deciding) switch
                {
                    (PromoForm.Scripted, "form") => $"Scripted, and it showed: {who} sounded like they were reading every line.",
                    (_, "form")                  => $"{who} lost the thread with nothing written to hold on to — dead air.",
                    (_, "staging")               => $"{who} was out of their element {Name(staging)}, and the segment died with it.",
                    _                            => $"Asked to be {Name(register)}, {who} could not sell it, and the crowd never bought it."
                };
            if (cut < 50)
                return r.Deciding switch
                {
                    "form"    => form == PromoForm.Scripted ? $"{who} got through the script. Nobody will remember it." : $"{who} rambled; it never found a point.",
                    "staging" => $"{who} looked uncomfortable {Name(staging)}.",
                    _         => $"{who} never quite landed {Name(register)}."
                };
            return null!;
        }

        /// <summary>The builder's word on what this promo, as set up, is likely to get from them.</summary>
        public static string Expect(Wrestler w, PromoForm form, PromoStaging staging, PromoRegister register, Reading r)
        {
            string who = w.RingName ?? "They";
            string band = Band(r.Expected);
            string because = r.Deciding switch
            {
                "form"    => form == PromoForm.Free ? "off the cuff" : form == PromoForm.Scripted ? "reading a script" : "on bullet points",
                "staging" => Name(staging),
                _         => $"being {Name(register)}"
            };
            return r.Expected >= 70
                ? $"{who}: {band}."
                : $"{who}: {band} — the weak spot is {because}.";
        }

        // ── What the booker can tell ─────────────────────────────────────────

        /// <summary>One line of the profile's promo reading.</summary>
        public sealed record PromoLine(string Label, string Words);

        /// <summary>
        /// What the player can tell about how somebody talks, sharpening with time (owner, 17 Nov
        /// 2026), the way <see cref="Scouting"/> does: anybody can see what someone is known for
        /// on television; three months with you puts each part in a broad band; a year gives the
        /// exact word, and which form each of their notes comes off best in. Exhibition, with no
        /// career, sees everything.
        /// </summary>
        public static (string Knowledge, IReadOnlyList<PromoLine> Lines) Describe(Career? career, Wrestler w)
        {
            var p = SkillsOf(w);
            int? months = career is null ? 99 : Scouting.MonthsWithYou(career, w);
            int know = months is not { } m ? 0 : m < 3 ? 0 : m < 12 ? 1 : 2;

            var parts = new (string Label, int Value)[]
            {
                ("Reading a script", p.Delivery), ("Off the cuff", p.Improvisation),
                ("Seated", p.Stillness), ("In the ring", p.RingPresence), ("Working the crowd", p.CrowdWork),
                ("Set pieces", p.SetPiece), ("When it gets physical", p.Physicality)
            };
            var lines = new List<PromoLine>();

            if (know == 0)
            {
                // What anyone could see: what they are known for, and what they are not.
                var best  = parts.OrderByDescending(x => x.Value).Take(2).Select(x => x.Label.ToLowerInvariant());
                var worst = parts.OrderBy(x => x.Value).First();
                var note  = p.Registers.OrderByDescending(r => r.Rating).FirstOrDefault();
                lines.Add(new("Known for", string.Join(" and ", best) + (note is null ? "" : $"; best when {Name(note.Register)}")));
                lines.Add(new("Seldom seen", worst.Label.ToLowerInvariant()));
                return (career is not null && months is null
                            ? "From what anyone can see on television. Sign them to learn more."
                            : "From what anyone can see. Three months with you and it sharpens.", lines);
            }

            string Word(int v) => know == 1 ? (v >= 80 ? "strong" : v >= 60 ? "fine" : "weak") : Band(v);
            foreach (var (label, value) in parts) lines.Add(new(label, Word(value)));
            foreach (var r in p.Registers)
                lines.Add(new($"When {Name(r.Register)}", Word(r.Rating)
                    + (know == 2 && r.Lean is { } lean ? $" — best {Name(lean)}" : "")));
            lines.Add(new("Anything else", Word(p.Range)));
            return (know == 1 ? "Three months with you. A year and you will know them exactly." : "A year or more with you.", lines);
        }
    }
}
