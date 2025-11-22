using SuledFunctions.Models;

namespace SuledFunctions.Services.Excel.Interfaces;

/// <summary>
/// Interface for converting game-centered data structure to pair-centered structure
/// </summary>
public interface IPairStructureConverter
{
    /// <summary>
    /// Converts game-centered data structure to pair-centered structure
    /// Groups games by pair and creates PairGame objects with opponents
    /// </summary>
    List<TournamentPair> ConvertGamesToPairCentricStructure(List<Game> games, string tournamentId);
}
