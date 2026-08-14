using System.ComponentModel.DataAnnotations;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Web.Middleware;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        var (statusCode, title) = exception switch
        {
            UnauthorizedAccessException    => (StatusCodes.Status401Unauthorized,        "Unauthorized"),
            ValidationException            => (StatusCodes.Status400BadRequest,          "Bad Request"),
            ArgumentNullException          => (StatusCodes.Status400BadRequest,          "Bad Request"),
            InvalidApiKeyException         => (StatusCodes.Status400BadRequest,          "Bad Request"),
            OpenProjectRequestException    => (StatusCodes.Status400BadRequest,          "Bad Request"),
            InitializerInstanceException   => (StatusCodes.Status400BadRequest,          "Bad Request"),
            ActiveSessionConflictException => (StatusCodes.Status409Conflict,            "Conflict"),
            _                              => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        };

        if (exception is ActiveSessionConflictException conflict)
        {
            problem.Extensions["workPackageId"] = conflict.WorkPackageId;
            problem.Extensions["taskName"]      = conflict.TaskName;
            problem.Extensions["startedAt"]     = conflict.StartedAt;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}