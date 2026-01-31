using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using System.Text.Json;
using Xunit;

namespace SuledFunctions.Tests.Services;

/// <summary>
/// Tests for JSON serialization/deserialization of compact tournament format.
/// Ensures correct JSON structure for Cosmos DB storage.
/// </summary>
public class TournamentCompactMapperSerializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void TournamentCompact_SerializesToJson_Correctly()
    {
        // Arrange
        var compact = new TournamentCompact
        {
            Id = "test-123",
            Name = "Test Tournament",
            Location = "Test Location",
            Pairs = new List<PairCompact>
            {
                new()
                {
                    Id = 1,
                    Player1 = new[] { "John", "Doe" },
                    Player2 = new[] { "Jane", "" },
                    Games = new List<int[]>
                    {
                        new[] { 1, 6, 2 },
                        new[] { 2, 7, 3 }
                    }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(compact, _jsonOptions);

        // Assert
        Assert.Contains("\"id\":\"test-123\"", json);
        Assert.Contains("\"name\":\"Test Tournament\"", json);
        Assert.Contains("\"player1\":[\"John\",\"Doe\"]", json);
        Assert.Contains("\"player2\":[\"Jane\",\"\"]", json);
        Assert.Contains("\"games\":[[1,6,2],[2,7,3]]", json);
    }

    [Fact]
    public void TournamentCompact_DeserializesFromJson_Correctly()
    {
        // Arrange
        var json = @"{
            ""id"": ""test-123"",
            ""name"": ""Test Tournament"",
            ""createdDate"": ""2025-11-25T20:00:00Z"",
            ""blobFileName"": ""test.xlsx"",
            ""location"": ""Test Location"",
            ""division"": ""Group A"",
            ""description"": """",
            ""rules"": ""15(16)"",
            ""status"": 0,
            ""rounds"": [],
            ""pairs"": [
                {
                    ""id"": 1,
                    ""player1"": [""John"", ""Doe""],
                    ""player2"": [""Jane"", """"],
                    ""games"": [[1, 6, 2], [2, 7, 3]]
                }
            ]
        }";

        // Act
        var compact = JsonSerializer.Deserialize<TournamentCompact>(json, _jsonOptions);

        // Assert
        Assert.NotNull(compact);
        Assert.Equal("test-123", compact!.Id);
        Assert.Equal("Test Tournament", compact.Name);
        Assert.Single(compact.Pairs);
        
        var pair = compact.Pairs[0];
        Assert.Equal(1, pair.Id);
        Assert.Equal(new[] { "John", "Doe" }, pair.Player1);
        Assert.Equal(new[] { "Jane", "" }, pair.Player2);
        Assert.Equal(2, pair.Games.Count);
        Assert.Equal(new[] { 1, 6, 2 }, pair.Games[0]);
    }

    [Fact]
    public void PairCompact_JsonPropertyNames_AreCorrect()
    {
        // Arrange
        var pair = new PairCompact
        {
            Id = 1,
            Player1 = new[] { "Alice", "Smith" },
            Player2 = new[] { "Bob", "" },
            Games = new List<int[]> { new[] { 1, 6, 2 } }
        };

        // Act
        var json = JsonSerializer.Serialize(pair, _jsonOptions);

        // Assert
        Assert.Contains("\"player1\"", json);
        Assert.Contains("\"player2\"", json);
        Assert.DoesNotContain("\"p1\"", json);
        Assert.DoesNotContain("\"p2\"", json);
    }

    [Fact]
    public void PairCompact_ComputedProperties_NotSerialized()
    {
        // Arrange
        var pair = new PairCompact
        {
            Id = 1,
            Player1 = new[] { "Alice", "Smith" },
            Player2 = new[] { "Bob", "Jones" },
            Games = new List<int[]> { new[] { 1, 6, 2 }, new[] { 2, 7, 3 } }
        };

        // Act
        var json = JsonSerializer.Serialize(pair, _jsonOptions);

        // Assert - computed properties should not appear in JSON
        Assert.DoesNotContain("displayName", json.ToLower());
        Assert.DoesNotContain("gameCount", json.ToLower());
        
        // But the properties should still be accessible
        Assert.Equal("Alice Smith & Bob Jones", pair.DisplayName);
        Assert.Equal(2, pair.GameCount);
    }

    [Fact]
    public void TournamentCompact_RoundTrip_PreservesData()
    {
        // Arrange
        var original = CreateSampleTournament();
        var compact = TournamentCompactMapper.ToCompact(original);

        // Act - serialize and deserialize
        var json = JsonSerializer.Serialize(compact, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<TournamentCompact>(json, _jsonOptions);
        var restored = TournamentCompactMapper.FromCompact(deserialized!);

        // Assert
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Location, restored.Location);
        Assert.Equal(original.Pairs.Count, restored.Pairs.Count);
        Assert.Equal(original.Pairs[0].DisplayName, restored.Pairs[0].DisplayName);
        Assert.Equal(original.Pairs[0].Games.Count, restored.Pairs[0].Games.Count);
    }

    [Fact]
    public void CompactFormat_JsonSize_IsSmallerThanFullFormat()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        var compact = TournamentCompactMapper.ToCompact(tournament);

        // Act
        var fullJson = JsonSerializer.Serialize(tournament, _jsonOptions);
        var compactJson = JsonSerializer.Serialize(compact, _jsonOptions);

        // Assert
        Assert.True(compactJson.Length < fullJson.Length * 0.5,
            $"Compact JSON ({compactJson.Length} bytes) should be at least 50% smaller than full JSON ({fullJson.Length} bytes)");
    }

    [Fact]
    public void CompactFormat_WithEmptySurnames_SerializesCorrectly()
    {
        // Arrange
        var compact = new TournamentCompact
        {
            Id = "test",
            Name = "Test",
            Pairs = new List<PairCompact>
            {
                new()
                {
                    Id = 1,
                    Player1 = new[] { "Alice", "" },
                    Player2 = new[] { "Bob", "" },
                    Games = new List<int[]>()
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(compact, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<TournamentCompact>(json, _jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("", deserialized!.Pairs[0].Player1[1]);
        Assert.Equal("", deserialized.Pairs[0].Player2[1]);
    }

    [Fact]
    public void CompactFormat_WithGameStatus_SerializesCorrectly()
    {
        // Arrange
        var tournament = CreateSampleTournament();
        tournament.Pairs[0].Games[0].Status = GameStatus.InProgress;
        tournament.Pairs[0].Games[1].Status = GameStatus.Completed;
        
        var compact = TournamentCompactMapper.ToCompact(tournament);

        // Act
        var json = JsonSerializer.Serialize(compact, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<TournamentCompact>(json, _jsonOptions);
        var restored = TournamentCompactMapper.FromCompact(deserialized!);

        // Assert - game status should be preserved
        Assert.Equal(GameStatus.InProgress, restored.Pairs[0].Games[0].Status);
        Assert.Equal(GameStatus.Completed, restored.Pairs[0].Games[1].Status);
    }

    [Fact]
    public void CompactFormat_WithNullableFields_HandlesCorrectly()
    {
        // Arrange
        var compact = new TournamentCompact
        {
            Id = "test",
            Name = "Test",
            StartDate = null,
            EndDate = null,
            StartTime = null,
            EndTime = null,
            Warmup = null,
            Pairs = new List<PairCompact>()
        };

        // Act
        var json = JsonSerializer.Serialize(compact, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<TournamentCompact>(json, _jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.StartDate);
        Assert.Null(deserialized.EndDate);
        Assert.Null(deserialized.StartTime);
        Assert.Null(deserialized.EndTime);
        Assert.Null(deserialized.Warmup);
    }

    private static Tournament CreateSampleTournament()
    {
        var tournament = new Tournament
        {
            Id = "test-id",
            Name = "Test Tournament",
            CreatedDate = DateTime.UtcNow,
            BlobFileName = "test.xlsx",
            Location = "Test Location",
            Division = "Group A",
            Status = TournamentStatus.Upcoming,
            Rounds = new List<TournamentRound>()
        };

        var pair1 = new Pair
        {
            Player1 = new Player { Name = "Alice", Surname = "Smith" },
            Player2 = new Player { Name = "Bob" }
        };
        var pair2 = new Pair
        {
            Player1 = new Player { Name = "Charlie" },
            Player2 = new Player { Name = "David", Surname = "Jones" }
        };

        _ = pair1.Id;
        _ = pair2.Id;

        tournament.Pairs = new List<TournamentPair>
        {
            new()
            {
                PairInfo = pair1,
                Games = new List<PairGame>
                {
                    new() { Round = 1, CourtNumber = 6, OpponentPair = pair2, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 7, OpponentPair = pair2, Status = GameStatus.Scheduled }
                }
            },
            new()
            {
                PairInfo = pair2,
                Games = new List<PairGame>
                {
                    new() { Round = 1, CourtNumber = 6, OpponentPair = pair1, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 7, OpponentPair = pair1, Status = GameStatus.Scheduled }
                }
            }
        };

        return tournament;
    }
}
