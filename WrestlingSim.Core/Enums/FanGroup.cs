using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Enums
{
    public enum FanGroup
    {
        Casual,

        /// <summary>
        /// Watches every week and follows the stories — "the audience the weekly product is
        /// actually made for" (docs/wrestling-reference/23 §2.2). Split from Casual by how often
        /// they watch, not by who they like: the roster has no Regular ratings, and
        /// <see cref="Engine.AudienceModel.Appeal"/> reads Casual's for them.
        /// Appended to nothing — added by name, and saves store groups by name.
        /// </summary>
        Regular,
        Hardcore,
        Online,
        International,
        Families,
        Kids
    }
}
