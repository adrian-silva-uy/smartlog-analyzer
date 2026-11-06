using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
    /// Validación automática vía FluentValidation (registro en Program.cs).
    /// </summary>
    public static async Task<IResult> IngestLog(
        IngestLogRequest request,
        ILogRepository repository,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CoreLogLevel>(request.Level, true, out var level))
        {
            // El validador ya cubre este caso, pero se mantiene una barrera de defensa en el endpoint.
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

        var created = await repository.AddAsync(log, ct);
        return Results.Created($"/api/logs/{created.Id}", created.ToResponse());
    }

    /// <summary>
    /// GET /api/logs — Lista logs con filtros (servicio, nivel, rango de fechas, búsqueda) y paginación.
    /// Consulta de agregación ligera con total de registros para el paginador.
    /// </summary>
    public static async Task<IResult> ListLogs(
        LogFilter filter,
        ILogRepository repository,
        CancellationToken ct)
    {
        var result = await repository.GetAsync(filter, ct);
        var items = result.Items.Select(l => l.ToResponse()).ToList();
        return Results.Ok(new PagedResult<LogResponse>(items, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>
    /// GET /api/logs/aggregation — Consulta de agregación requerida por el desafío.
    /// Agrupa logs por servicio o nivel dentro de un rango de fechas opcional.
    /// Ejemplos: /api/logs/aggregation?groupBy=service
    ///           /api/logs/aggregation?groupBy=level&from=2026-01-01&to=2026-01-31
    /// </summary>
    public static async Task<IResult> AggregateLogs(
        LogAggregationRequest request,
        ILogRepository repository,
        CancellationToken ct)
    {
        var aggregations = await repository.GetAggregationAsync(request, ct);
        return Results.Ok(aggregations);
    }

    // TODO: evaluar agregar índice compuesto en (ServiceName, Timestamp) para optimizar consultas de agregación por servicio en rangos de fecha amplios.
}
