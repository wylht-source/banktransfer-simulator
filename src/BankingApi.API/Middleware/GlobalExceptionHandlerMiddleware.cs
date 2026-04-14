using BankingApi.Domain.Exceptions;
using System.Text.Json;

namespace BankingApi.API.Middleware;

public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? "unknown";

        var (statusCode, message) = exception switch
        {
            DomainException de when de.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, de.Message),

            DomainException de when de.Message.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status403Forbidden, de.Message),

            DomainException de
                => (StatusCodes.Status400BadRequest, de.Message),

            OperationCanceledException
                => (499, "Request was cancelled by the client."),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        // Log 500s with full details — lower severity for expected domain errors
        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception,
                "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
        else if (statusCode == StatusCodes.Status403Forbidden)
            logger.LogWarning(
                "AccessDenied — Path: {Path}, CorrelationId: {CorrelationId}",
                context.Request.Path, correlationId);
        else
            logger.LogWarning(
                "Handled exception [{StatusCode}] {Message}. CorrelationId: {CorrelationId}",
                statusCode, message, correlationId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = message,
            correlationId = correlationId
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
