using System.Net;
using System.Text.Json;
using Bomberman.Server.Models;

namespace Bomberman.Server.Infrastructure;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ErrorHandlingMiddleware(RequestDelegate next) { _next = next; }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
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

    private async Task WriteError(HttpContext ctx, HttpStatusCode code, string errCode, string msg)
    {
        if (ctx.Response.HasStarted) return; // <- key fix

        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/json";
        var payload = JsonSerializer.SerializeToUtf8Bytes(new ErrorResponse { ErrorCode = errCode, ErrorMessage = msg }, _json);
        await ctx.Response.BodyWriter.WriteAsync(payload);
    }
}
