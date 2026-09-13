using System.Net;
using System.Text.Json;
using UniversalPOS.Application.Common.Exceptions;

namespace UniversalPOS.Api.Middleware;

/// <summary>
/// Centralized exception handling: maps known AppExceptions to safe, specific HTTP
/// responses; anything else is logged with full detail server-side and returns a
/// generic 500 to the client — stack traces never reach the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            InvalidCredentialsException e => (HttpStatusCode.Unauthorized, e.Message, (object?)null),
            AccountLockedOutException e => (HttpStatusCode.Locked, e.Message, null),
            NotFoundException e => (HttpStatusCode.NotFound, e.Message, null),
            ForbiddenException e => (HttpStatusCode.Forbidden, e.Message, null),
            ValidationFailedException e => (HttpStatusCode.BadRequest, e.Message, (object?)e.Errors),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", null),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception {ExceptionType} processing {Method} {Path}", exception.GetType().Name, context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var body = JsonSerializer.Serialize(new { error = message, errors, correlationId = context.TraceIdentifier });
        await context.Response.WriteAsync(body);
    }
}
