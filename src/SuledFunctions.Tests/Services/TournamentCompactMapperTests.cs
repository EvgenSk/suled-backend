using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using Xunit;

namespace SuledFunctions.Tests.Services;

/// <summary>
/// Unit tests for TournamentCompactMapper.
/// Verifies correct conversion between full and compact tournament formats.
/// </summary>
public class TournamentCompactMapperTests
{
    [Fact]
    public void ToCompact_WithValidTournament_ConvertsCorrectly()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        
        // Assert
        Assert.Equal(tournament.Id, compact.Id);
        Assert.Equal(tournament.Name, compact.Name);
        Assert.Equal(tournament.CreatedDate, compact.CreatedDate);
        Assert.Equal(tournament.BlobFileName, compact.BlobFileName);
        Assert.Equal(tournament.Location, compact.Location);
        Assert.Equal(tournament.Status, compact.Status);
        Assert.Equal(tournament.Pairs.Count, compact.Pairs.Count);
        Assert.Equal(tournament.Rounds.Count, compact.Rounds.Count);
    }
    
    [Fact]
    public void ToCompact_PairIds_AreSequentialIntegers()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        
        // Assert
        var pairIds = compact.Pairs.Select(p => p.Id).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, pairIds);
    }
    
    [Fact]
    public void ToCompact_Players_AreArrays()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        
        // Assert
        var firstPair = compact.Pairs[0];
        Assert.Equal(2, firstPair.Player1.Length);
        Assert.Equal(2, firstPair.Player2.Length);
        Assert.Equal("Dmitri", firstPair.Player1[0]);
        Assert.Equal("", firstPair.Player1[1]); // No surname
        Assert.Equal("Natalja", firstPair.Player2[0]);
    }
    
    [Fact]
    public void ToCompact_Games_AreArrays()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        
        // Assert
        var firstPair = compact.Pairs[0];
        var firstGame = firstPair.Games[0];
        
        Assert.True(firstGame.Length >= 3);
        Assert.Equal(1, firstGame[0]); // Round
        Assert.Equal(6, firstGame[1]); // Court
        Assert.Equal(2, firstGame[2]); // Opponent ID (should be mapped to int)
    }
    
    [Fact]
    public void ToCompact_RemovesComputedFields()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        
        // Assert - verify computed properties exist but are not serialized
        var firstPair = compact.Pairs[0];
        Assert.NotEmpty(firstPair.DisplayName); // Computed property works
        Assert.True(firstPair.GameCount > 0); // Computed property works
    }
    
    [Fact]
    public void FromCompact_WithValidCompact_ExpandsCorrectly()
    {
        // Arrange
        var original = CreateSampleTournament();
        var compact = TournamentCompactMapper.ToCompact(original);
        
        // Act
        var expanded = TournamentCompactMapper.FromCompact(compact);
        
        // Assert
        Assert.Equal(original.Id, expanded.Id);
        Assert.Equal(original.Name, expanded.Name);
        Assert.Equal(original.CreatedDate, expanded.CreatedDate);
        Assert.Equal(original.Location, expanded.Location);
        Assert.Equal(original.Status, expanded.Status);
        Assert.Equal(original.Pairs.Count, expanded.Pairs.Count);
    }
    
    [Fact]
    public void FromCompact_Players_AreReconstructed()
    {
        // Arrange
        var original = CreateSampleTournament();
        var compact = TournamentCompactMapper.ToCompact(original);
        
        // Act
        var expanded = TournamentCompactMapper.FromCompact(compact);
        
        // Assert
        var firstPair = expanded.Pairs[0];
        Assert.Equal("Dmitri", firstPair.PairInfo.Player1.Name);
        Assert.Null(firstPair.PairInfo.Player1.Surname);
        Assert.Equal("Dmitri", firstPair.PairInfo.Player1.FullName);
        
        Assert.Equal("Natalja", firstPair.PairInfo.Player2.Name);
        Assert.Equal("Dmitri & Natalja", firstPair.PairInfo.DisplayName);
    }
    
    [Fact]
    public void FromCompact_Games_AreReconstructed()
    {
        // Arrange
        var original = CreateSampleTournament();
        var compact = TournamentCompactMapper.ToCompact(original);
        
        // Act
        var expanded = TournamentCompactMapper.FromCompact(compact);
        
        // Assert
        var firstPair = expanded.Pairs[0];
        var firstGame = firstPair.Games[0];
        
        Assert.NotEmpty(firstGame.Id);
        Assert.Equal(expanded.Id, firstGame.TournamentId);
        Assert.Equal(1, firstGame.Round);
        Assert.Equal(6, firstGame.CourtNumber);
        Assert.NotNull(firstGame.OpponentPair);
        Assert.NotEmpty(firstGame.OpponentPair.DisplayName);
    }
    
    [Fact]
    public void RoundTrip_PreservesAllData()
    {
        // Arrange
        var original = CreateSampleTournament();
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(original);
        var restored = TournamentCompactMapper.FromCompact(compact);
        
        // Assert - verify key data is preserved
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Location, restored.Location);
        Assert.Equal(original.Pairs.Count, restored.Pairs.Count);
        
        // Verify all pairs
        for (int i = 0; i < original.Pairs.Count; i++)
        {
            var origPair = original.Pairs[i];
            var restPair = restored.Pairs[i];
            
            Assert.Equal(origPair.DisplayName, restPair.DisplayName);
            Assert.Equal(origPair.Games.Count, restPair.Games.Count);
            
            // Verify games
            for (int j = 0; j < origPair.Games.Count; j++)
            {
                var origGame = origPair.Games[j];
                var restGame = restPair.Games[j];
                
                Assert.Equal(origGame.Round, restGame.Round);
                Assert.Equal(origGame.CourtNumber, restGame.CourtNumber);
                Assert.Equal(origGame.Status, restGame.Status);
            }
        }
    }
    
    [Fact]
    public void RoundTrip_WithSurnames_PreservesNames()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Add pair with surnames
        var pairWithSurnames = new TournamentPair
        {
            PairInfo = new Pair
            {
                Player1 = new Player { Name = "Evgeny", Surname = "Smith" },
                Player2 = new Player { Name = "Yulia", Surname = "Jones" }
            },
            Games = new List<PairGame>()
        };
        tournament.Pairs.Add(pairWithSurnames);
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);
        
        // Assert
        var lastPair = restored.Pairs.Last();
        Assert.Equal("Evgeny", lastPair.PairInfo.Player1.Name);
        Assert.Equal("Smith", lastPair.PairInfo.Player1.Surname);
        Assert.Equal("Evgeny Smith", lastPair.PairInfo.Player1.FullName);
        Assert.Equal("Yulia", lastPair.PairInfo.Player2.Name);
        Assert.Equal("Jones", lastPair.PairInfo.Player2.Surname);
        Assert.Equal("Yulia Jones", lastPair.PairInfo.Player2.FullName);
        Assert.Equal("Evgeny Smith & Yulia Jones", lastPair.PairInfo.DisplayName);
    }
    
    [Fact]
    public void RoundTrip_WithGameStatus_PreservesStatus()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        tournament.Pairs[0].Games[0].Status = GameStatus.InProgress;
        tournament.Pairs[0].Games[1].Status = GameStatus.Completed;
        
        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);
        
        // Assert
        Assert.Equal(GameStatus.InProgress, restored.Pairs[0].Games[0].Status);
        Assert.Equal(GameStatus.Completed, restored.Pairs[0].Games[1].Status);
    }
    
    [Fact]
    public void ToCompact_NullTournament_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TournamentCompactMapper.ToCompact(null!));
    }
    
    [Fact]
    public void FromCompact_NullCompact_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => TournamentCompactMapper.FromCompact(null!));
    }
    
    [Fact]
    public void CompactFormat_IsSmallerThanFullFormat()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        
        // Act
        var fullJson = System.Text.Json.JsonSerializer.Serialize(tournament);
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var compactJson = System.Text.Json.JsonSerializer.Serialize(compact);
        
        // Assert - compact should be significantly smaller
        Assert.True(compactJson.Length < fullJson.Length * 0.5, 
            $"Compact format ({compactJson.Length} bytes) should be at least 50% smaller than full format ({fullJson.Length} bytes)");
    }
    
    /// <summary>
    /// Create a sample tournament for testing.
    /// </summary>
    private static Tournament CreateSampleTournament()
    {
        var tournament = new Tournament
        {
            Id = "f9d0db76-8907-47a5-84ea-b248e6d74dc0",
            Name = "Test Tournament",
            CreatedDate = new DateTime(2025, 11, 25, 20, 39, 31, DateTimeKind.Utc),
            BlobFileName = "tournament.xlsx",
            StartDate = new DateTime(2025, 11, 26),
            StartTime = new TimeSpan(20, 0, 0),
            EndTime = new TimeSpan(21, 30, 0),
            Location = "Tallink",
            Division = "Group A",
            Description = "Test Description",
            Rules = "15(16)",
            Warmup = new TimeSpan(0, 5, 0),
            Status = TournamentStatus.Upcoming,
            Rounds = new List<TournamentRound>
            {
                new() { RoundNumber = 1, StartTime = new TimeOnly(20, 5), EndTime = new TimeOnly(20, 20), GameCount = 3 },
                new() { RoundNumber = 2, StartTime = new TimeOnly(20, 25), EndTime = new TimeOnly(20, 40), GameCount = 3 }
            }
        };
        
        // Create three pairs
        var pair1 = new Pair
        {
            Player1 = new Player { Name = "Dmitri" },
            Player2 = new Player { Name = "Natalja" }
        };
        var pair2 = new Pair
        {
            Player1 = new Player { Name = "Evgeny", Surname = "S" },
            Player2 = new Player { Name = "Yulia", Surname = "K" }
        };
        var pair3 = new Pair
        {
            Player1 = new Player { Name = "Iris" },
            Player2 = new Player { Name = "Tarvo" }
        };
        
        // Trigger ID generation
        _ = pair1.Id;
        _ = pair2.Id;
        _ = pair3.Id;
        
        // Create tournament pairs with games
        tournament.Pairs = new List<TournamentPair>
        {
            new()
            {
                PairInfo = pair1,
                Games = new List<PairGame>
                {
                    new() { Round = 1, CourtNumber = 6, OpponentPair = pair2, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 7, OpponentPair = pair3, Status = GameStatus.Scheduled }
                }
            },
            new()
            {
                PairInfo = pair2,
                Games = new List<PairGame>
                {
                    new() { Round = 1, CourtNumber = 6, OpponentPair = pair1, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 8, OpponentPair = pair3, Status = GameStatus.Scheduled }
                }
            },
            new()
            {
                PairInfo = pair3,
                Games = new List<PairGame>
                {
                    new() { Round = 1, CourtNumber = 7, OpponentPair = pair1, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 8, OpponentPair = pair2, Status = GameStatus.Scheduled }
                }
            }
        };
        
        return tournament;
    }
}
