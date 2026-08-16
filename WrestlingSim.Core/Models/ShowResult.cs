using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSim.Models
{
    public class ShowResult
    {
        public double OverallRating { get; set; }
        public Dictionary<string, double> Breakdown { get; set; } = new();
    }
}
