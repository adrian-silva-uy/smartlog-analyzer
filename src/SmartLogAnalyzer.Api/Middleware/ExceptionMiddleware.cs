using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Dtos;

namespace SmartLogAnalyzer.Api.Middleware;

/// <summary>
/// Middleware de manejo centralizado de excepciones.
/// Req: respuestas de error estandarizadas (ProblemDetails / RFC 7807).
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            await WriteProblemAsync(context, StatusCodes.Status504GatewayTimeout,
                "timeout", "Tiempo de espera agotado procesando la solicitud.");
        }
        catch (SqlException)
        {
            await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable,
                "database_unavailable", "No se pudo conectar con la base de datos en este momento.");
        }
        catch (DbUpdateException)
        {
            await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable,
                "database_unavailable", "No se pudo completar la operación en la base de datos.");
        }
        catch (Exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "server_error", "Ocurrió un error interno procesando la solicitud.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string type, string title)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = status;

        var problem = new ErrorResponse(type, title, status,
            $"Se produjo un error de tipo '{type}' al procesar la solicitud.");
        return context.Response.WriteAsJsonAsync(problem);
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ExceptionMiddleware>();
}
