using System.Net;
using System.Text.Json;
using Bomberman.Server.Models;

namespace Bomberman.Server.Infrastructure;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    public ErrorHandlingMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
            // Map 404 to contract if needed
            if (ctx.Response.StatusCode == (int)HttpStatusCode.NotFound && !ctx.Response.HasStarted)
            {
                await WriteError(ctx, HttpStatusCode.NotFound, "not_found", "Resource not found.");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(ctx, HttpStatusCode.Unauthorized, "unauthorized", ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteError(ctx, HttpStatusCode.BadRequest, "invalid_input", ex.Message);
        }
        catch (Exception)
        {
            await WriteError(ctx, HttpStatusCode.InternalServerError, "server_error", "Unexpected server error.");
        }
    }

    private static async Task WriteError(HttpContext ctx, HttpStatusCode code, string errCode, string msg)
    {
        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new ErrorResponse { ErrorCode = errCode, ErrorMessage = msg });
        await ctx.Response.WriteAsync(payload);
    }
}
