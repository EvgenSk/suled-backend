using System.ComponentModel.DataAnnotations;

namespace SuledFunctions.Configuration;

/// <summary>
/// Configuration settings for Cosmos DB connection
/// </summary>
public class CosmosDbSettings
{
    public const string SectionName = "CosmosDb";

    /// <summary>
    /// Cosmos DB connection string
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Cosmos DB database name
    /// </summary>
    [Required]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Cosmos DB container name for tournaments
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = string.Empty;
}
