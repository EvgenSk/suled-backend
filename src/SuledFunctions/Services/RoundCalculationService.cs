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
    private const int DefaultGameDurationMinutes = 15;
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
        
        var currentTime = tournamentStartDate.Add(tournamentStartTime);
        var rounds = new List<TournamentRound>();

        foreach (var roundNumber in roundNumbers)
        {
            // Get games for this round
            var roundGames = allGames.Where(g => g.Round == roundNumber).ToList();
            var gameCount = roundGames.Count;

            // Count unique games (each game appears twice in pair-centered structure)
            var uniqueGameCount = gameCount / 2;

            // Determine max courts used in this round
            var maxCourt = roundGames.Max(g => g.CourtNumber);
            
            // Calculate round duration based on games per court
            // If we have 4 games and 2 courts, games run in parallel (2 games per court sequentially)
            var gamesPerCourt = uniqueGameCount > 0 && maxCourt > 0 
                ? (int)Math.Ceiling((double)uniqueGameCount / maxCourt) 
                : 1;
            
            var roundDurationMinutes = gamesPerCourt * DefaultGameDurationMinutes;
            
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
            currentTime = roundEnd.AddMinutes(DefaultBreakBetweenRoundsMinutes);
            
            // Check if we've exceeded the daily end time
            if (currentTime.TimeOfDay > tournamentEndTime)
            {
                // Move to next day
                var nextDay = tournament.EndDate ?? tournamentStartDate.AddDays(1);
                if (currentTime.Date < nextDay)
                {
                    currentTime = currentTime.Date.AddDays(1).Add(tournamentStartTime);
                }
            }
        }

        _logger.LogInformation("Calculated {Count} rounds for tournament {TournamentId}", 
            rounds.Count, tournament.Id);

        return rounds;
    }
}
