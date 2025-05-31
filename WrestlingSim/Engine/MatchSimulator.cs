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
            Console.WriteLine($"Match: {_match.WrestlerA.RingName} vs {_match.WrestlerB.RingName}");

            double matchRating = _match.CalculateMatchRating();
            var result = _match.CalculateWinner();

            // Load all moves from Json - This will need to be changed to get wrestlers specific moves
            var fileName = "MoveList.json";
            List<Move> moves = DataLoaders.LoadMoves(fileName);

            // Get random move from list
            Random random = new Random();
            var LastMove = moves[random.Next(moves.Count)];

            Console.WriteLine();

            Console.WriteLine($"MATCH RATING: {matchRating}");
            Console.WriteLine($"{result.Item1} used {LastMove.Name} to beat {result.Item2}");
            Console.WriteLine($"WINNER: {result.Item1}");
            Console.WriteLine("");
        }
    }
}
