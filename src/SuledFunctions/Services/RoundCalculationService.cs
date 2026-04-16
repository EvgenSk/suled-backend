using Microsoft.Extensions.Logging;
using SuledFunctions.Models;
using SuledFunctions.Services.Interfaces;

namespace SuledFunctions.Services;

/// <summary>
/// Service for calculating round schedules based on tournament metadata
/// </summary>
public class RoundCalculationService(ILogger<RoundCalculationService> logger) : IRoundCalculationService
{
    // Default assumptions if not specified
    private const int DefaultWarmupMinutes = 5;
    private const int DefaultBreakBetweenRoundsMinutes = 5;
    private static readonly TimeSpan DefaultStartTime = new TimeSpan(9, 0, 0); // 9:00 AM
    private static readonly TimeSpan DefaultEndTime = new TimeSpan(18, 0, 0); // 6:00 PM

    public List<TournamentRound> CalculateRounds(Tournament tournament)
    {
        var allGames = tournament.Pairs?.SelectMany(p => p.Games).ToList() ?? [];
        var roundNumbers = allGames.Select(g => g.Round).Distinct().OrderBy(r => r).ToList();

        if (roundNumbers.Count == 0)
        {
            logger.LogWarning("Cannot calculate rounds: tournament has no rounds");
            return [];
        }

        var roundDuration = CalculateRoundDurationMinutes(tournament, roundNumbers.Count);
        var currentTime = GetRoundsStartTime(tournament);
        var rounds = new List<TournamentRound>();

        foreach (var roundNumber in roundNumbers)
        {
            var roundEnd = currentTime.AddMinutes(roundDuration);
            rounds.Add(new TournamentRound
            {
                RoundNumber = roundNumber,
                StartTime = TimeOnly.FromDateTime(currentTime),
                EndTime = TimeOnly.FromDateTime(roundEnd),
                GameCount = allGames.Count(g => g.Round == roundNumber) / 2
            });
            currentTime = roundEnd.AddMinutes(DefaultBreakBetweenRoundsMinutes);
        }

        logger.LogInformation("Calculated {Count} rounds for tournament {TournamentId}",
            rounds.Count, tournament.Id);
        return rounds;
    }

    private double CalculateRoundDurationMinutes(Tournament tournament, int roundCount)
    {
        var startTime = tournament.StartTime ?? DefaultStartTime;
        var endTime = tournament.EndTime ?? DefaultEndTime;
        var warmupMinutes = (tournament.Warmup ?? TimeSpan.FromMinutes(DefaultWarmupMinutes)).TotalMinutes;
        var totalBreakMinutes = (roundCount - 1) * DefaultBreakBetweenRoundsMinutes;
        var available = (endTime - startTime).TotalMinutes - warmupMinutes - totalBreakMinutes;
        var duration = available / roundCount;

        logger.LogInformation(
            "Round duration calculated: {RoundDuration:F1} min ({RoundCount} rounds, {TotalDay} min day, {Warmup} min warmup, {Breaks} min total breaks)",
            duration, roundCount, (endTime - startTime).TotalMinutes, warmupMinutes, totalBreakMinutes);
        return duration;
    }

    private static DateTime GetRoundsStartTime(Tournament tournament) =>
        (tournament.StartDate ?? DateTime.UtcNow.Date)
            .Add(tournament.StartTime ?? DefaultStartTime)
            .AddMinutes((tournament.Warmup ?? TimeSpan.FromMinutes(DefaultWarmupMinutes)).TotalMinutes);
}
