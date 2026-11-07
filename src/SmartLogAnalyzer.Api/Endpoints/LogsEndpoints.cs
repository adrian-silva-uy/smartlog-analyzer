using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;
using CoreLogLevel = SmartLogAnalyzer.Core.Entities.LogLevel;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Api.Endpoints;

public static class LogsEndpoints
{
    /// <summary>
    /// POST /api/logs — Ingiere un log de un microservicio.
    /// Req: POST /api/logs con validación de entrada.
    /// </summary>
    public static async Task<IResult> IngestLog(
        [FromBody] IngestLogRequest request,
        ILogRepository repository,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CoreLogLevel>(request.Level, true, out var level))
        {
            // Defensa extra por si el validador se salta algo.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "Level", new[] { "Nivel de log inválido." } }
            });
        }

        var log = new LogEntry
        {
            Id = Guid.NewGuid(),
            ServiceName = request.ServiceName,
            Level = level,
            Message = request.Message,
            Timestamp = request.Timestamp,
            Source = request.Source,
            TraceId = request.TraceId,
            Metadata = request.Metadata,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var created = await repository.AddAsync(log, ct);
            return Results.Created($"/api/logs/{created.Id}", created.ToResponse());
        }
        catch (SqlException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base de datos para guardar el log.",
                type: "database_unavailable");
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo guardar el log en la base de datos.",
                type: "database_unavailable");
        }
    }

    /// <summary>
    /// GET /api/logs — Lista logs con filtros (servicio, nivel, rango de fechas, búsqueda) y paginación.
    /// Consulta de agregación ligera con total de registros para el paginador.
    /// </summary>
    public static async Task<IResult> ListLogs(
        ILogRepository repository,
        CancellationToken ct,
        string? serviceName = null,
        string? level = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? search = null,
        int page = 1,
        int pageSize = 50)
    {
        var filter = new LogFilter
        {
            ServiceName = serviceName,
            Level = level,
            From = from,
            To = to,
            Search = search,
            Page = page,
            PageSize = pageSize
        };
        try
        {
            var result = await repository.GetAsync(filter, ct);
            var items = result.Items.Select(l => l.ToResponse()).ToList();
            return Results.Ok(new PagedResult<LogResponse>(items, result.TotalCount, result.Page, result.PageSize));
        }
        catch (SqlException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base de datos para listar los logs.",
                type: "database_unavailable");
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo consultar los logs en la base de datos.",
                type: "database_unavailable");
        }
    }

    /// <summary>
    /// GET /api/logs/aggregation — Consulta de agregación requerida por el desafío.
    /// Agrupa logs por servicio o nivel dentro de un rango de fechas opcional.
    /// Ejemplos: /api/logs/aggregation?groupBy=service
    ///           /api/logs/aggregation?groupBy=level&from=2026-01-01&to=2026-01-31
    /// </summary>
    public static async Task<IResult> AggregateLogs(
        string? groupBy = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        ILogRepository repository = null!,
        CancellationToken ct = default)
    {
        var request = new LogAggregationRequest(groupBy ?? "service", from, to);
        try
        {
            var aggregations = await repository.GetAggregationAsync(request, ct);
            return Results.Ok(aggregations);
        }
        catch (SqlException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base de datos para calcular la agregación.",
                type: "database_unavailable");
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo calcular la agregación en la base de datos.",
                type: "database_unavailable");
        }
    }

    // TODO: evaluar agregar índice compuesto en (ServiceName, Timestamp) para optimizar consultas de agregación por servicio en rangos de fecha amplios.
}
