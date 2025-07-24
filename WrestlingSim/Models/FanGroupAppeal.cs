using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Enums;

namespace WrestlingSim.Models
{
    public class FanGroupAppeal
    {
        public string Group { get; set; }
        public double AppealScore { get; set; } // 0.0 to 1.0
    }
}
