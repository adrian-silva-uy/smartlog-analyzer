using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using SmartLogAnalyzer.Core.Dtos;

namespace SmartLogAnalyzer.Api.Middleware;

/// <summary>
/// Middleware de manejo centralizado de excepciones.
/// Justificación (senior): toda excepción no controlada se convierte en una respuesta
/// ProblemDetails (RFC 7807) consistente, sin filtrar stack traces al cliente y con el
/// código HTTP correcto (504 para timeouts, 500 para el resto).
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
        catch (Exception ex)
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
        // En modo desarrollo se podría enriquecer con stack trace, pero en producción se omite para no exponer información interna.

        return context.Response.WriteAsJsonAsync(problem);
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ExceptionMiddleware>();
}
