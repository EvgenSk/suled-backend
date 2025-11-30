using System.Text.Json.Serialization;

namespace SuledFunctions.Models.DTOs;

/// <summary>
/// Standard API response wrapper for successful operations
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// The response data
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    /// <summary>
    /// Optional message describing the result
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Timestamp of the response
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Data = data,
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public static ApiResponse<T> Ok(string message)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message
        };
    }
}

/// <summary>
/// Response for created resources
/// </summary>
public class CreatedResponse<T> : ApiResponse<T>
{
    /// <summary>
    /// The ID of the created resource
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Creates a created response
    /// </summary>
    public static CreatedResponse<T> Created(string id, T data, string? message = null)
    {
        return new CreatedResponse<T>
        {
            Id = id,
            Data = data,
            Success = true,
            Message = message
        };
    }
}

/// <summary>
/// Paginated response wrapper
/// </summary>
public class PagedResponse<T> : ApiResponse<IEnumerable<T>>
{
    /// <summary>
    /// Total number of items available
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Indicates if there is a next page
    /// </summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Indicates if there is a previous page
    /// </summary>
    [JsonPropertyName("hasPreviousPage")]
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Creates a paged response
    /// </summary>
    public static PagedResponse<T> Create(IEnumerable<T> data, int totalCount, int page, int pageSize)
    {
        return new PagedResponse<T>
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Success = true
        };
    }
}
