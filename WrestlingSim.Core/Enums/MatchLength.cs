using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WrestlingSim.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchLength
    {
        Short = 5,
        Medium = 10,
        Long = 20
    }
}
