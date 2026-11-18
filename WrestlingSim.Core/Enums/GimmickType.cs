using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Enums
{
    public enum GimmickType
    {
        Monster,
        Underdog,
        AntiHero,
        Rebel,
        CultLeader,
        Showman,
        Realist,
        Comedy,
        Traditionalist,
        Loner,
        Superstar,
        Egotist,
        DarkPersona,
        Sellout,
        Cryptic,
        Daredevil,
        Gimmickless,

        /// <summary>
        /// No type given. Appended rather than first so stored names and numbers keep their
        /// meaning, and made the default on <see cref="Models.Gimmick"/> — before it, a gimmick
        /// built without a type was silently a <see cref="Monster"/>, the enum's first value,
        /// and paid a monster's prices once character types priced results.
        /// </summary>
        Unspecified
    }
}
