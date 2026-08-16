using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Models.Segment;

namespace WrestlingSim.Models
{
    public class Show
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string Location { get; set; }
        public int AudienceSize { get; set; }
        public List<object> Card { get; set; } = new(); // Mix of Matches & Segments
        public int TotalDurationMinutes { get; set; } = 180;
    }
}
