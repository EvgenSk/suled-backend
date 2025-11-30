using SuledFunctions.Models.DTOs;

namespace SuledFunctions.TelegramBot.Services;

/// <summary>
/// Interface for pair-related operations
/// </summary>
public interface IPairService
{
    Task<List<PairDto>> GetAllPairsAsync();
    Task<PairDto?> GetPairByIdAsync(string pairId);
}
