using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WrestlingSim.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MoveType
    {
        Takedown,
        Throw,
        Slam,
        Submission,
        Strike,
        Aerial,
        Technical,
    }
}

// Technical to be removed? If so json to be fixed
// Pin?
// Tag team moves?