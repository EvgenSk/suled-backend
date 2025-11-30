using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using SuledFunctions.Exceptions;
using System.Net;
using System.Text.Json;

namespace SuledFunctions.Middleware;

/// <summary>
/// Global exception handling middleware for Azure Functions
/// </summary>
public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in function {FunctionName}", context.FunctionDefinition.Name);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(FunctionContext context, Exception exception)
    {
        var (statusCode, errorResponse) = exception switch
        {
            ValidationException validationEx => (validationEx.StatusCode, new ErrorResponse
            {
                Error = validationEx.Message,
                StatusCode = (int)validationEx.StatusCode,
                Errors = validationEx.Errors,
                TraceId = context.TraceContext.TraceParent
            }),
            
            TournamentNotFoundException notFoundEx => (notFoundEx.StatusCode, new ErrorResponse
            {
                Error = notFoundEx.Message,
                StatusCode = (int)notFoundEx.StatusCode,
                TraceId = context.TraceContext.TraceParent
            }),
            
            FileProcessingException fileEx => (fileEx.StatusCode, new ErrorResponse
            {
                Error = fileEx.Message,
                StatusCode = (int)fileEx.StatusCode,
                TraceId = context.TraceContext.TraceParent
            }),
            
            AppException appEx => (appEx.StatusCode, new ErrorResponse
            {
                Error = appEx.Message,
                StatusCode = (int)appEx.StatusCode,
                TraceId = context.TraceContext.TraceParent
            }),
            
            _ => (HttpStatusCode.InternalServerError, new ErrorResponse
            {
                Error = "An internal server error occurred",
                StatusCode = 500,
                TraceId = context.TraceContext.TraceParent
            })
        };

        // Get the HTTP request data from the context
        var httpReqData = await context.GetHttpRequestDataAsync();
        if (httpReqData != null)
        {
            var response = httpReqData.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

            context.GetInvocationResult().Value = response;
        }
    }
}

/// <summary>
/// Standard error response format
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}
