namespace SuledFunctions.Common;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    /// <summary>
    /// HTTP-related constants
    /// </summary>
    public static class Http
    {
        public const string ContentTypeMultipartFormData = "multipart/form-data";
        public const string ContentTypeApplicationJson = "application/json";
        public const string HeaderContentType = "Content-Type";
        public const string HeaderContentDisposition = "Content-Disposition";
    }

    /// <summary>
    /// File-related constants
    /// </summary>
    public static class Files
    {
        public const string DefaultTournamentFileName = "tournament.xlsx";
        public const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    /// <summary>
    /// Validation error messages
    /// </summary>
    public static class ErrorMessages
    {
        public const string MissingContentType = "Content-Type header is required";
        public const string InvalidContentType = "Content-Type must be multipart/form-data";
        public const string EmptyFile = "No file data received";
        public const string RequestTimeout = "Request timeout while reading file";
        public const string ProcessingError = "Failed to process tournament file";
        public const string TournamentNotFound = "Tournament not found";
        public const string InvalidTournamentId = "Invalid tournament ID";
        public const string InvalidFileType = "Only .xlsx and .xls files are supported";
    }

    /// <summary>
    /// Success messages
    /// </summary>
    public static class SuccessMessages
    {
        public const string TournamentUploaded = "Tournament uploaded successfully";
        public const string TournamentDeleted = "Tournament deleted successfully";
    }

    /// <summary>
    /// Environment variable names
    /// </summary>
    public static class EnvironmentVariables
    {
        public const string CosmosDbConnection = "CosmosDbConnection";
        public const string CosmosDbName = "CosmosDbName";
        public const string CosmosContainerName = "CosmosContainerName";
        public const string StorageAccountConnectionString = "StorageAccountConnectionString";
    }
}
