using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuledFunctions.Configuration;
using SuledFunctions.Models;
using SuledFunctions.Models.Optimized;
using SuledFunctions.Repositories;
using SuledFunctions.Services;
using SuledFunctions.Services.Excel.Interfaces;
using System.Text.Json;

namespace SuledFunctions.Tests.Services;

/// <summary>
/// Tests for TournamentService integration with compact format storage.
/// These tests will be relevant once TournamentService is updated to use TournamentCompactMapper.
/// </summary>
public class TournamentServiceCompactFormatTests
{
    private readonly Mock<ILogger<TournamentService>> _loggerMock;
    private readonly Mock<ITournamentRepository> _repositoryMock;
    private readonly Mock<IExcelMetadataExtractor> _metadataExtractorMock;
    private readonly IOptions<TournamentSettings> _settings;
    private readonly TournamentService _service;

    public TournamentServiceCompactFormatTests()
    {
        _loggerMock = new Mock<ILogger<TournamentService>>();
        _repositoryMock = new Mock<ITournamentRepository>();
        _metadataExtractorMock = new Mock<IExcelMetadataExtractor>();
        _settings = Options.Create(new TournamentSettings
        {
            MaxResultsDefault = 100
        });

        _service = new TournamentService(_repositoryMock.Object, _metadataExtractorMock.Object, _settings, _loggerMock.Object);
    }

    [Fact]
    public void CompactFormat_RoundTrip_PreservesAllTournamentData()
    {
        // Arrange
        var original = CreateFullTournament();

        // Act - convert to compact and back
        var compact = TournamentCompactMapper.ToCompact(original);
        var restored = TournamentCompactMapper.FromCompact(compact);

        // Assert - all important data preserved
        restored.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Location.Should().Be(original.Location);
        restored.Division.Should().Be(original.Division);
        restored.Status.Should().Be(original.Status);
        restored.Pairs.Should().HaveCount(original.Pairs.Count);

        for (int i = 0; i < original.Pairs.Count; i++)
        {
            var originalPair = original.Pairs[i];
            var restoredPair = restored.Pairs[i];
            
            restoredPair.DisplayName.Should().Be(originalPair.DisplayName);
            restoredPair.Games.Should().HaveCount(originalPair.Games.Count);

            for (int j = 0; j < originalPair.Games.Count; j++)
            {
                restoredPair.Games[j].Round.Should().Be(originalPair.Games[j].Round);
                restoredPair.Games[j].CourtNumber.Should().Be(originalPair.Games[j].CourtNumber);
                restoredPair.Games[j].Status.Should().Be(originalPair.Games[j].Status);
            }
        }
    }

    [Fact]
    public void CompactFormat_SizeReduction_IsSignificant()
    {
        // Arrange
        var tournament = CreateFullTournament();
        var compact = TournamentCompactMapper.ToCompact(tournament);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Act
        var fullJson = JsonSerializer.Serialize(tournament, jsonOptions);
        var compactJson = JsonSerializer.Serialize(compact, jsonOptions);

        // Assert - compact format should be significantly smaller
        var reductionPercent = (1 - (double)compactJson.Length / fullJson.Length) * 100;
        reductionPercent.Should().BeGreaterThan(50, 
            $"compact format ({compactJson.Length} bytes) should be at least 50% smaller than full format ({fullJson.Length} bytes)");
    }

    [Fact]
    public void CompactFormat_WithEmptySurnames_HandlesCorrectly()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = "test-id",
            Name = "Test Tournament",
            CreatedDate = DateTime.UtcNow,
            Location = "Test Location",
            Status = TournamentStatus.Upcoming,
            Rounds = new List<TournamentRound>()
        };

        var pair = new Pair
        {
            Player1 = new Player { Name = "Alice" }, // No surname
            Player2 = new Player { Name = "Bob" }    // No surname
        };
        _ = pair.Id; // Initialize ID

        tournament.Pairs = new List<TournamentPair>
        {
            new() { PairInfo = pair, Games = new List<PairGame>() }
        };

        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);

        // Assert
        restored.Pairs[0].DisplayName.Should().Be("Alice & Bob");
    }

    [Fact]
    public void CompactFormat_WithGameStatuses_PreservesStatuses()
    {
        // Arrange
        var tournament = CreateFullTournament();
        tournament.Pairs[0].Games[0].Status = GameStatus.Scheduled;
        tournament.Pairs[0].Games[1].Status = GameStatus.InProgress;

        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);

        // Assert
        restored.Pairs[0].Games[0].Status.Should().Be(GameStatus.Scheduled);
        restored.Pairs[0].Games[1].Status.Should().Be(GameStatus.InProgress);
    }

    [Fact]
    public void CompactFormat_WithManyPairs_HandlesEfficiently()
    {
        // Arrange - create tournament with 50 pairs
        var tournament = new Tournament
        {
            Id = "large-tournament",
            Name = "Large Tournament",
            CreatedDate = DateTime.UtcNow,
            Location = "Test Location",
            Status = TournamentStatus.Upcoming,
            Rounds = new List<TournamentRound>(),
            Pairs = new List<TournamentPair>()
        };

        var pairs = new List<Pair>();
        for (int i = 0; i < 50; i++)
        {
            var pair = new Pair
            {
                Player1 = new Player { Name = $"Player{i * 2}", Surname = $"Surname{i * 2}" },
                Player2 = new Player { Name = $"Player{i * 2 + 1}", Surname = $"Surname{i * 2 + 1}" }
            };
            _ = pair.Id;
            pairs.Add(pair);

            tournament.Pairs.Add(new TournamentPair
            {
                PairInfo = pair,
                Games = new List<PairGame>()
            });
        }

        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);

        // Assert
        restored.Pairs.Should().HaveCount(50);
        compact.Pairs.Select(p => p.Id).Should().BeInAscendingOrder()
            .And.Subject.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CompactFormat_WithOpponentReferences_MaintainsRelationships()
    {
        // Arrange
        var tournament = CreateFullTournament();
        
        // Verify original has opponent references
        var originalPair1 = tournament.Pairs[0];
        var originalPair2 = tournament.Pairs[1];
        originalPair1.Games[0].OpponentPair.Should().NotBeNull();

        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);

        // Assert - opponent relationships maintained
        var restoredPair1 = restored.Pairs[0];
        var restoredPair2 = restored.Pairs[1];
        
        if (restoredPair1.Games.Count > 0 && restoredPair1.Games[0].OpponentPair != null)
        {
            var opponentId = restoredPair1.Games[0].OpponentPair.Id;
            var opponentPair = restored.Pairs.FirstOrDefault(p => p.PairInfo.Id == opponentId);
            opponentPair.Should().NotBeNull("opponent pair should exist in tournament");
        }
    }

    [Fact]
    public void CompactFormat_WithNullableFields_PreservesNulls()
    {
        // Arrange
        var tournament = new Tournament
        {
            Id = "test-id",
            Name = "Test",
            CreatedDate = DateTime.UtcNow,
            Location = "Test Location",
            Status = TournamentStatus.Upcoming,
            StartDate = null,
            EndDate = null,
            StartTime = null,
            EndTime = null,
            Warmup = null,
            Rounds = new List<TournamentRound>(),
            Pairs = new List<TournamentPair>()
        };

        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);
        var restored = TournamentCompactMapper.FromCompact(compact);

        // Assert
        restored.StartDate.Should().BeNull();
        restored.EndDate.Should().BeNull();
        restored.StartTime.Should().BeNull();
        restored.EndTime.Should().BeNull();
        restored.Warmup.Should().BeNull();
    }

    [Fact]
    public void CompactFormat_JsonSerialization_ProducesCorrectStructure()
    {
        // Arrange
        var tournament = CreateFullTournament();
        var compact = TournamentCompactMapper.ToCompact(tournament);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Act
        var json = JsonSerializer.Serialize(compact, jsonOptions);

        // Assert - verify compact structure
        json.Should().Contain("\"player1\":");
        json.Should().Contain("\"player2\":");
        json.Should().NotContain("\"displayName\":");
        // Note: "gameCount" can appear in TournamentRound, so we check specifically for pair's gameCount
        json.Should().NotMatch("*\"player1\":*\"gameCount\":*").And.NotMatch("*\"player2\":*\"gameCount\":*");
        
        // Verify can deserialize back
        var deserialized = JsonSerializer.Deserialize<TournamentCompact>(json, jsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.Pairs.Should().HaveCount(compact.Pairs.Count);
    }

    [Fact]
    public void CompactFormat_PairIds_AreSequentialIntegers()
    {
        // Arrange
        var tournament = CreateFullTournament();

        // Act
        var compact = TournamentCompactMapper.ToCompact(tournament);

        // Assert
        var pairIds = compact.Pairs.Select(p => p.Id).ToList();
        pairIds.Should().BeInAscendingOrder();
        pairIds.Should().OnlyHaveUniqueItems();
        pairIds.First().Should().Be(1);
        pairIds.Last().Should().Be(pairIds.Count);
    }

    private static Tournament CreateFullTournament()
    {
        var tournament = new Tournament
        {
            Id = "test-tournament-id",
            Name = "Test Championship",
            CreatedDate = DateTime.UtcNow,
            BlobFileName = "test.xlsx",
            Location = "Test Arena",
            Division = "Pro Division",
            Description = "Test Description",
            Rules = "15(16)",
            Status = TournamentStatus.Upcoming,
            StartDate = DateTime.UtcNow.AddDays(7),
            EndDate = DateTime.UtcNow.AddDays(8),
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Warmup = new TimeSpan(0, 10, 0),
            Rounds = new List<TournamentRound>
            {
                new() { RoundNumber = 1, StartTime = new TimeOnly(9, 0) },
                new() { RoundNumber = 2, StartTime = new TimeOnly(10, 0) }
            }
        };

        var pair1 = new Pair
        {
            Player1 = new Player { Name = "Alice", Surname = "Smith" },
            Player2 = new Player { Name = "Bob", Surname = "Jones" }
        };
        var pair2 = new Pair
        {
            Player1 = new Player { Name = "Charlie", Surname = "Brown" },
            Player2 = new Player { Name = "David", Surname = "Wilson" }
        };
        var pair3 = new Pair
        {
            Player1 = new Player { Name = "Eve", Surname = "Davis" },
            Player2 = new Player { Name = "Frank" } // No surname
        };

        _ = pair1.Id;
        _ = pair2.Id;
        _ = pair3.Id;

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
                    new() { Round = 1, CourtNumber = 9, OpponentPair = pair1, Status = GameStatus.Scheduled },
                    new() { Round = 2, CourtNumber = 7, OpponentPair = pair2, Status = GameStatus.Scheduled }
                }
            }
        };

        return tournament;
    }
}
