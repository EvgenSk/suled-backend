using SuledFunctions.Models;
using SuledFunctions.Services.Excel.Interfaces;

namespace SuledFunctions.Services.Excel;

/// <summary>
/// Converts game-centered data structure to pair-centered structure
/// </summary>
public class PairStructureConverter : IPairStructureConverter
{
    public List<TournamentPair> ConvertGamesToPairCentricStructure(List<Game> games, string tournamentId)
    {
        var pairGamesMap = BuildPairGamesMap(games, tournamentId);
        var pairDict = BuildPairDictionary(games);

        return pairDict
            .Select(kvp => new TournamentPair
            {
                PairInfo = kvp.Value,
                Games = pairGamesMap[kvp.Key].OrderBy(g => g.Round).ThenBy(g => g.CourtNumber).ToList()
            })
            .OrderBy(p => p.DisplayName)
            .ToList();
    }

    private static Dictionary<string, List<PairGame>> BuildPairGamesMap(List<Game> games, string tournamentId)
    {
        var map = new Dictionary<string, List<PairGame>>();
        foreach (var game in games)
        {
            if (!map.TryGetValue(game.Pair1.Id, out var list1)) map[game.Pair1.Id] = list1 = [];
            list1.Add(CreatePairGame(game, tournamentId, game.Pair2));
            if (!map.TryGetValue(game.Pair2.Id, out var list2)) map[game.Pair2.Id] = list2 = [];
            list2.Add(CreatePairGame(game, tournamentId, game.Pair1));
        }
        return map;
    }

    private static PairGame CreatePairGame(Game game, string tournamentId, Pair opponent) => new()
    {
        Id = game.Id,
        TournamentId = tournamentId,
        Round = game.Round,
        CourtNumber = game.CourtNumber,
        OpponentPair = opponent,
        ScheduledTime = game.ScheduledTime,
        Status = game.Status
    };

    private static Dictionary<string, Pair> BuildPairDictionary(List<Game> games)
    {
        var dict = new Dictionary<string, Pair>();
        foreach (var game in games)
        {
            dict.TryAdd(game.Pair1.Id, game.Pair1);
            dict.TryAdd(game.Pair2.Id, game.Pair2);
        }
        return dict;
    }
}
