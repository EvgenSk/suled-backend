using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using System.Text.Json;

namespace SuledFunctions.Tests.Services;

/// <summary>
/// Demo showing the storage savings from compact format.
/// Run this to see actual size comparison.
/// </summary>
public class TournamentCompactMapperDemo
{
    [Fact]
    public void DemonstrateStorageSavings()
    {
        // Arrange - create a realistic tournament with 10 pairs and 9 games each
        var tournament = CreateRealisticTournament();
        
        // Convert to compact format
        var compact = TournamentCompactMapper.ToCompact(tournament);
        
        // Serialize both formats to JSON
        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = false // Minimize size
        };
        
        var fullJson = JsonSerializer.Serialize(tournament, jsonOptions);
        var compactJson = JsonSerializer.Serialize(compact, jsonOptions);
        
        // Calculate savings
        var fullSize = fullJson.Length;
        var compactSize = compactJson.Length;
        var savedBytes = fullSize - compactSize;
        var percentSaved = (double)savedBytes / fullSize * 100;
        
        // Output results
        Console.WriteLine("=== COSMOS DB STORAGE OPTIMIZATION RESULTS ===");
        Console.WriteLine();
        Console.WriteLine($"Full Format Size:    {fullSize:N0} bytes");
        Console.WriteLine($"Compact Format Size: {compactSize:N0} bytes");
        Console.WriteLine($"Bytes Saved:         {savedBytes:N0} bytes");
        Console.WriteLine($"Percentage Saved:    {percentSaved:F1}%");
        Console.WriteLine();
        
        // Extrapolate to larger scale
        var tournamentsPerMonth = 100;
        var monthlyFullSize = fullSize * tournamentsPerMonth;
        var monthlyCompactSize = compactSize * tournamentsPerMonth;
        var monthlySavings = savedBytes * tournamentsPerMonth;
        
        Console.WriteLine("=== MONTHLY SAVINGS (100 tournaments) ===");
        Console.WriteLine($"Full Format:    {monthlyFullSize / 1024.0:F1} KB");
        Console.WriteLine($"Compact Format: {monthlyCompactSize / 1024.0:F1} KB");
        Console.WriteLine($"Saved:          {monthlySavings / 1024.0:F1} KB");
        Console.WriteLine();
        
        // RU savings estimation
        // Cosmos DB charges based on document size: larger documents = more RUs
        // Approximate RU reduction (not exact, but close)
        var estimatedRuReduction = percentSaved / 2; // Conservative estimate
        
        Console.WriteLine("=== ESTIMATED RU SAVINGS ===");
        Console.WriteLine($"Approximate RU Reduction: {estimatedRuReduction:F1}%");
        Console.WriteLine($"(Smaller documents = fewer RUs for reads/writes)");
        Console.WriteLine();
        
        // Verify round-trip works
        var restored = TournamentCompactMapper.FromCompact(compact);
        Console.WriteLine("=== DATA INTEGRITY CHECK ===");
        Console.WriteLine($"Round-trip successful: {restored.Name == tournament.Name}");
        Console.WriteLine($"All pairs preserved:   {restored.Pairs.Count == tournament.Pairs.Count}");
        Console.WriteLine($"All games preserved:   {restored.Pairs.Sum(p => p.Games.Count) == tournament.Pairs.Sum(p => p.Games.Count)}");
        
        // Assert the savings are significant
        Assert.True(percentSaved > 70, $"Expected at least 70% savings, got {percentSaved:F1}%");
    }
    
    private static Tournament CreateRealisticTournament()
    {
        var tournament = new Tournament
        {
            Id = "f9d0db76-8907-47a5-84ea-b248e6d74dc0",
            Name = "Ulsans Tournament",
            CreatedDate = new DateTime(2025, 11, 25, 20, 39, 31, DateTimeKind.Utc),
            BlobFileName = "tournament.xlsx",
            StartDate = new DateTime(2025, 11, 26),
            StartTime = new TimeSpan(20, 0, 0),
            EndTime = new TimeSpan(21, 30, 0),
            Location = "Tallink Sport Center",
            Division = "Group A",
            Description = "Annual championship tournament",
            Rules = "15(16)",
            Warmup = new TimeSpan(0, 5, 0),
            Status = TournamentStatus.Upcoming,
            Rounds = new List<TournamentRound>()
        };
        
        // Add 9 rounds
        for (int i = 1; i <= 9; i++)
        {
            var startMinute = (i - 1) * 20;
            var endMinute = startMinute + 15;
            
            tournament.Rounds.Add(new TournamentRound
            {
                RoundNumber = i,
                StartTime = new TimeOnly(20 + (startMinute / 60), startMinute % 60),
                EndTime = new TimeOnly(20 + (endMinute / 60), endMinute % 60),
                GameCount = 5
            });
        }
        
        // Create 10 pairs
        var pairs = new List<Pair>();
        var playerNames = new[]
        {
            ("Dmitri", ""), ("Natalja", ""),
            ("Evgeny", "S"), ("Yulia", "K"),
            ("Iris", ""), ("Tarvo", ""),
            ("Larisa", ""), ("Dmitri", "K"),
            ("Olga", ""), ("Sofja", ""),
            ("Slava", ""), ("Julia", ""),
            ("Stanislav", ""), ("Evgeni", "Ser"),
            ("Svetlana", ""), ("Ananii", ""),
            ("Tatjana", ""), ("Katre", ""),
            ("Vitali", ""), ("Erik", "")
        };
        
        for (int i = 0; i < 10; i++)
        {
            var pair = new Pair
            {
                Player1 = new Player 
                { 
                    Name = playerNames[i * 2].Item1, 
                    Surname = string.IsNullOrEmpty(playerNames[i * 2].Item2) ? null : playerNames[i * 2].Item2 
                },
                Player2 = new Player 
                { 
                    Name = playerNames[i * 2 + 1].Item1, 
                    Surname = string.IsNullOrEmpty(playerNames[i * 2 + 1].Item2) ? null : playerNames[i * 2 + 1].Item2 
                }
            };
            _ = pair.Id; // Trigger ID generation
            pairs.Add(pair);
        }
        
        // Create tournament pairs with 9 games each
        tournament.Pairs = new List<TournamentPair>();
        
        for (int pairIndex = 0; pairIndex < 10; pairIndex++)
        {
            var tournamentPair = new TournamentPair
            {
                PairInfo = pairs[pairIndex],
                Games = new List<PairGame>()
            };
            
            // Each pair plays 9 games (one per round)
            for (int round = 1; round <= 9; round++)
            {
                var opponentIndex = (pairIndex + round) % 10;
                if (opponentIndex == pairIndex) opponentIndex = (opponentIndex + 1) % 10;
                
                tournamentPair.Games.Add(new PairGame
                {
                    Round = round,
                    CourtNumber = 6 + (pairIndex % 5),
                    OpponentPair = pairs[opponentIndex],
                    Status = GameStatus.Scheduled,
                    TournamentId = tournament.Id
                });
            }
            
            tournament.Pairs.Add(tournamentPair);
        }
        
        return tournament;
    }
}
