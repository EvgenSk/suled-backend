using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Service for calculating round schedules based on tournament metadata
/// </summary>
public class RoundCalculationService : IRoundCalculationService
{
    private readonly ILogger<RoundCalculationService> _logger;
    
    // Default assumptions if not specified
    private const int DefaultWarmupMinutes = 5;
    private const int DefaultBreakBetweenRoundsMinutes = 5;
    private static readonly TimeSpan DefaultStartTime = new TimeSpan(9, 0, 0); // 9:00 AM
    private static readonly TimeSpan DefaultEndTime = new TimeSpan(18, 0, 0); // 6:00 PM

    public RoundCalculationService(ILogger<RoundCalculationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate round start and end times based on tournament metadata and games
    /// </summary>
    public List<TournamentRound> CalculateRounds(Tournament tournament)
    {
        if (tournament.Pairs == null || tournament.Pairs.Count == 0)
        {
            _logger.LogWarning("Cannot calculate rounds: tournament has no pairs/games");
            return new List<TournamentRound>();
        }

        // Get all unique rounds from games
        var allGames = tournament.Pairs.SelectMany(p => p.Games).ToList();
        var roundNumbers = allGames.Select(g => g.Round).Distinct().OrderBy(r => r).ToList();

        if (!roundNumbers.Any())
        {
            _logger.LogWarning("Cannot calculate rounds: no games found");
            return new List<TournamentRound>();
        }

        // Determine tournament start date and time
        var tournamentStartDate = tournament.StartDate ?? DateTime.UtcNow.Date;
        var tournamentStartTime = tournament.StartTime ?? DefaultStartTime;
        var tournamentEndTime = tournament.EndTime ?? DefaultEndTime;

        var totalDayMinutes = (tournamentEndTime - tournamentStartTime).TotalMinutes;
        var warmupMinutes = (tournament.Warmup ?? TimeSpan.FromMinutes(DefaultWarmupMinutes)).TotalMinutes;
        var breakMinutes = DefaultBreakBetweenRoundsMinutes;
        var roundCount = roundNumbers.Count;

        // Available time = total day - warmup - breaks between rounds
        var totalBreakMinutes = (roundCount - 1) * breakMinutes;
        var availableForRoundsMinutes = totalDayMinutes - warmupMinutes - totalBreakMinutes;
        var roundDurationMinutes = availableForRoundsMinutes / roundCount;

        _logger.LogInformation(
            "Round duration calculated: {RoundDuration:F1} min ({RoundCount} rounds, {TotalDay} min day, {Warmup} min warmup, {Breaks} min total breaks)",
            roundDurationMinutes, roundCount, totalDayMinutes, warmupMinutes, totalBreakMinutes);

        var currentTime = tournamentStartDate.Add(tournamentStartTime).AddMinutes(warmupMinutes);
        var rounds = new List<TournamentRound>();

        foreach (var roundNumber in roundNumbers)
        {
            // Count unique games (each game appears twice in pair-centered structure)
            var roundGames = allGames.Where(g => g.Round == roundNumber).ToList();
            var uniqueGameCount = roundGames.Count / 2;

            var roundStart = currentTime;
            var roundEnd = roundStart.AddMinutes(roundDurationMinutes);

            rounds.Add(new TournamentRound
            {
                RoundNumber = roundNumber,
                StartTime = TimeOnly.FromDateTime(roundStart),
                EndTime = TimeOnly.FromDateTime(roundEnd),
                GameCount = uniqueGameCount
            });

            // Move to next round start time (with break)
            currentTime = roundEnd.AddMinutes(breakMinutes);
        }

        _logger.LogInformation("Calculated {Count} rounds for tournament {TournamentId}", 
            rounds.Count, tournament.Id);

        return rounds;
    }
}
