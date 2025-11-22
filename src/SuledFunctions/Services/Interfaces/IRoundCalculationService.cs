using SuledFunctions.Models;

namespace SuledFunctions.Services.Interfaces;

/// <summary>
/// Service for calculating round schedules based on tournament metadata
/// </summary>
public interface IRoundCalculationService
{
    /// <summary>
    /// Calculate round start and end times based on tournament metadata and games
    /// </summary>
    List<TournamentRound> CalculateRounds(Tournament tournament);
}
