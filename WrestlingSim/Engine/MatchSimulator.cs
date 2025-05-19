using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WrestlingSim.Models;

namespace WrestlingSim.Engine
{
    public class MatchSimulator
    {
        private readonly Match _match;
        
        public MatchSimulator(Match match)
        {
            _match = match;
        }

        public void Simulate()
        {
            Console.WriteLine("");
            Console.WriteLine("WELCOME TO A NEW MATCH");
            Console.WriteLine($"Match: {_match.WrestlerA.Name} vs {_match.WrestlerB.Name}");

            double matchRating = _match.CalculateMatchRating();
            string winner = _match.CalculateWinner();

            Console.WriteLine($"MATCH RATING: {matchRating}");
            Console.WriteLine($"WINNER: {winner}");
            Console.WriteLine("");
        }
    }
}
