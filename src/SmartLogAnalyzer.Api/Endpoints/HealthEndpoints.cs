using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Api.Endpoints;

public static class HealthEndpoints
{
    /// <summary>
    /// GET /api/health — Verifica el estado del servicio y la conectividad con la base de datos.
    /// Endpoint de infraestructura básico (requerido por el desafío).
    /// </summary>
    public static async Task<IResult> Health(
        AppDbContext db,
        CancellationToken ct)
    {
        bool databaseReachable = false;
        try
        {
            // Consulta ligera que valida conectividad sin traer datos.
            databaseReachable = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            databaseReachable = false;
        }

        var response = new HealthResponse(
            databaseReachable ? "Healthy" : "Unhealthy",
            DateTimeOffset.UtcNow,
            databaseReachable);

        return databaseReachable
            ? Results.Ok(response)
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
