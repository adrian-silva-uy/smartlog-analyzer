using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Api.Endpoints;

public static class HealthEndpoints
{
    /// <summary>
    /// GET /api/health — Estado del servicio y conectividad con la base de datos.
    /// Req: GET /api/health
    /// </summary>
    public static async Task<IResult> Health(
        AppDbContext db,
        CancellationToken ct)
    {
        bool databaseReachable = false;
        try
        {
            // Ping ligero a la base de datos sin traer datos.
            databaseReachable = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            databaseReachable = false;
        }

        var response = new HealthResponse(
            databaseReachable ? "Healthy" : "Degraded",
            DateTimeOffset.UtcNow,
            databaseReachable);

        return Results.Ok(response);
    }
}
