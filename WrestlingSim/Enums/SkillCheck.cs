using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WrestlingSim.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SkillCheck
    {
        VeryEasy = 1,
        Easy = 2,
        Medium = 3,
        Hard = 4,
        VeryHard = 5,
    }
}
// can be change to ChallengeLevel or ChallengeRating