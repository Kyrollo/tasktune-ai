using System.Net;
using System.Text.Json;
using Serilog;

namespace TaskTune.API.Middleware;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            Log.Error(ex, "Unhandled request exception. TraceId: {TraceId}", traceId);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                type = "about:blank",
                title = "An internal server error occurred.",
                status = StatusCodes.Status500InternalServerError,
                traceId,
                detail = _environment.IsDevelopment() ? ex.Message : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
