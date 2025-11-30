using System.Net;

namespace SuledFunctions.Exceptions;

/// <summary>
/// Base exception for all application-specific exceptions
/// </summary>
public abstract class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }

    protected AppException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message)
    {
        StatusCode = statusCode;
    }

    protected AppException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Exception thrown when a tournament is not found
/// </summary>
public class TournamentNotFoundException : AppException
{
    public string TournamentId { get; }

    public TournamentNotFoundException(string tournamentId)
        : base($"Tournament with ID '{tournamentId}' was not found", HttpStatusCode.NotFound)
    {
        TournamentId = tournamentId;
    }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : AppException
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(string message, Dictionary<string, string[]>? errors = null)
        : base(message, HttpStatusCode.BadRequest)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public ValidationException(string field, string error)
        : base($"Validation failed for {field}", HttpStatusCode.BadRequest)
    {
        Errors = new Dictionary<string, string[]>
        {
            { field, new[] { error } }
        };
    }
}

/// <summary>
/// Exception thrown when a file processing error occurs
/// </summary>
public class FileProcessingException : AppException
{
    public string? FileName { get; }

    public FileProcessingException(string message, string? fileName = null)
        : base(message, HttpStatusCode.BadRequest)
    {
        FileName = fileName;
    }

    public FileProcessingException(string message, Exception innerException, string? fileName = null)
        : base(message, innerException, HttpStatusCode.BadRequest)
    {
        FileName = fileName;
    }
}
