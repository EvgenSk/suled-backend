using SuledFunctions.Models;
using SuledFunctions.Services.Excel.Interfaces;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Converts game-centered data structure to pair-centered structure
/// </summary>
public class PairStructureConverter : IPairStructureConverter
{
    /// <summary>
    /// Converts game-centered data structure to pair-centered structure
    /// Groups games by pair and creates PairGame objects with opponents
    /// </summary>
    public List<TournamentPair> ConvertGamesToPairCentricStructure(List<Game> games, string tournamentId)
    {
        // Dictionary to group games by pair
        var pairGamesMap = new Dictionary<string, List<PairGame>>();
        
        foreach (var game in games)
        {
            // Create game from Pair1's perspective
            var pairGame1 = new PairGame
            {
                Id = game.Id,
                TournamentId = tournamentId,
                Round = game.Round,
                CourtNumber = game.CourtNumber,
                OpponentPair = game.Pair2,
                ScheduledTime = game.ScheduledTime,
                Status = game.Status
            };
            
            if (!pairGamesMap.ContainsKey(game.Pair1.Id))
            {
                pairGamesMap[game.Pair1.Id] = new List<PairGame>();
            }
            pairGamesMap[game.Pair1.Id].Add(pairGame1);
            
            // Create game from Pair2's perspective
            var pairGame2 = new PairGame
            {
                Id = game.Id,
                TournamentId = tournamentId,
                Round = game.Round,
                CourtNumber = game.CourtNumber,
                OpponentPair = game.Pair1,
                ScheduledTime = game.ScheduledTime,
                Status = game.Status
            };
            
            if (!pairGamesMap.ContainsKey(game.Pair2.Id))
            {
                pairGamesMap[game.Pair2.Id] = new List<PairGame>();
            }
            pairGamesMap[game.Pair2.Id].Add(pairGame2);
        }
        
        // Create TournamentPair objects
        var tournamentPairs = new List<TournamentPair>();
        
        // Get unique pairs (we need to reconstruct the Pair object)
        var pairDict = new Dictionary<string, Pair>();
        foreach (var game in games)
        {
            if (!pairDict.ContainsKey(game.Pair1.Id))
            {
                pairDict[game.Pair1.Id] = game.Pair1;
            }
            if (!pairDict.ContainsKey(game.Pair2.Id))
            {
                pairDict[game.Pair2.Id] = game.Pair2;
            }
        }
        
        foreach (var (pairId, pair) in pairDict)
        {
            var tournamentPair = new TournamentPair
            {
                PairInfo = pair,
                Games = pairGamesMap[pairId].OrderBy(g => g.Round).ThenBy(g => g.CourtNumber).ToList()
            };
            tournamentPairs.Add(tournamentPair);
        }
        
        return tournamentPairs.OrderBy(p => p.DisplayName).ToList();
    }
}
