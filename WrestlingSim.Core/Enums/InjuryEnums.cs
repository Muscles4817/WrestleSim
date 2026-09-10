using System.Text.Json.Serialization;

namespace WrestlingSim.Enums
{
    /// <summary>
    /// What got hurt. Doc 15 §2.1's table, which is ordered by how often each one actually
    /// happens rather than by how bad it is.
    ///
    /// The list stops short of doc 15's catastrophic spinal injury and of §2.2's chronic
    /// conditions — degenerative discs, arthritis, the cumulative concussion picture. Those
    /// end careers, and this game has no way to end one: there is no retirement, no age, and
    /// no aging curve for them to sit on. Modelling an injury that should finish somebody
    /// and then handing them back after nine months would say something false about the
    /// most serious thing in the reference.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BodyPart
    {
        /// <summary>Doc 15: "very common; historically undiagnosed", and §2.2's real long-term issue.</summary>
        Concussion,

        /// <summary>"Extremely common." Weeks to career-ending; this models the weeks end.</summary>
        Back,

        /// <summary>"Very common" — labrum, rotator cuff, separation.</summary>
        Shoulder,

        /// <summary>Common. An ACL is most of a year.</summary>
        Knee,

        /// <summary>Common, and the quickest one back.</summary>
        Ankle,

        /// <summary>Orbital, jaw, ribs, hands. Occasional.</summary>
        Bone,

        /// <summary>Elbow and biceps tears.</summary>
        Elbow,

        /// <summary>Doc 15: "common in powerlifting-built performers", which is why size drives it.</summary>
        Pectoral,

        /// <summary>Occasional and devastating.</summary>
        Achilles,

        /// <summary>
        /// Wrestling's signature career-ender (doc 15 §2.3). Rare here, long when it happens,
        /// and the one whose history weighs heaviest on what comes next.
        /// </summary>
        Neck
    }
}
