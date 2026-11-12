namespace WrestlingSim.Models.World
{
    /// <summary>
    /// What one promotion's audience has been shown lately. A promotion has one; a wrestler who
    /// moves to another promotion arrives in front of an audience that has none of this, which is
    /// why a return or a debut is fresh (docs/wrestling-reference/10 §2.7, 17 §2.9).
    ///
    /// Two separate things live here, and they are not one meter:
    ///
    ///   • <see cref="Acts"/> — how much the same each *wrestler's* act has been.
    ///   • <see cref="ShowSameness"/> — how much the same the *show* has been, whoever is on it.
    ///
    /// Neither is screen time. Research and the reference agree that being seen a lot does not
    /// wear an audience out — nothing new happening does (doc 17 §4: "freshness, the underlying
    /// variable"). See <see cref="Engine.AudienceModel"/>.
    /// </summary>
    public class Audience
    {
        /// <summary>By <see cref="Wrestler.Id"/>.</summary>
        public Dictionary<string, Act> Acts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>0–100. How alike the promotion's recent shows have been.</summary>
        public double ShowSameness { get; set; }

        /// <summary>The last few shows' shapes, newest last, for comparing the next one against.</summary>
        public List<ShowShape> Recent { get; set; } = new();

        public Act? ActOf(Wrestler w) => Acts.GetValueOrDefault(w.Id);

        public double SamenessOf(Wrestler w) => ActOf(w)?.Sameness ?? 0;
    }

    /// <summary>One wrestler, as this audience has been seeing them.</summary>
    public class Act
    {
        /// <summary>0–100. Rises when they appear with nothing new; falls when something changes.</summary>
        public double Sameness { get; set; }

        /// <summary>
        /// Wins in a row. Doc 17 §3.11: "winning too much, too easily" — dominance without
        /// vulnerability. The part of the audience that follows closest tires of it first.
        /// </summary>
        public int Streak { get; set; }

        /// <summary>What kind of thing they did last time, to tell whether this time differs.</summary>
        public string LastKind { get; set; } = "";

        /// <summary>The gimmick they were last seen under: a repackage is something new.</summary>
        public string LastGimmick { get; set; } = "";

        public DateOnly? LastSeen { get; set; }
    }

    /// <summary>Who was on a show and what the show was made of.</summary>
    public class ShowShape
    {
        public DateOnly Date { get; set; }
        public List<string> Cast { get; set; } = new();

        /// <summary>One entry per card item: its kind, format and stipulation.</summary>
        public List<string> Formats { get; set; } = new();
    }

    /// <summary>What the show did to the audience's patience, for the show report.</summary>
    public sealed class AudienceReport
    {
        public double ShowSamenessBefore { get; init; }
        public double ShowSamenessAfter { get; init; }

        /// <summary>Fraction taken off the overall rating for the show being like the last few. 0–0.2.</summary>
        public double SamenessPenalty { get; init; }

        /// <summary>How much of the runtime was filled, 0–1+.</summary>
        public double Filled { get; init; }

        /// <summary>Fraction taken off for leaving the slot under-filled. 0–0.3.</summary>
        public double UnderrunPenalty { get; init; }

        /// <summary>The acts the audience is tiring of, most stale first, with why in words.</summary>
        public List<string> Notes { get; init; } = new();
    }
}
