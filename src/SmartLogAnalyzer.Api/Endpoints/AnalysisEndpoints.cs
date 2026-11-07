using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Api.Endpoints;

public static class AnalysisEndpoints
{
    /// <summary>
    /// POST /api/logs/analyze — Envía un conjunto de logs al LLM para su análisis.
    /// Orquesta: obtiene los logs, llama al LLM, valida la respuesta y persiste el análisis.
    /// Este metodo esta un poco largo, podria refactorizarse pero funciona.
    /// </summary>
    public static async Task<IResult> AnalyzeLogs(
        [FromBody] AnalyzeLogsRequest request,
        ILogRepository logRepository,
        IAnalysisRepository analysisRepository,
        ILLMService llmService,
        CancellationToken ct)
    {
        var logIds = request.LogIds.Distinct().ToList();

        if (logIds.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "LogIds", new[] { "Se debe especificar al menos un log para analizar." } }
            });

        List<LogEntry> logs;
        try
        {
            logs = (await logRepository.GetByIdsAsync(logIds, ct)).ToList();
        }
        catch (SqlException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base de datos para obtener los logs.",
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

        var missing = logIds.Except(logs.Select(l => l.Id)).ToList();
        if (missing.Count > 0)
            return Results.NotFound(new
            {
                error = "Uno o más logs no existen.",
                missingIds = missing
            });

        // Llamada al LLM con su propio timeout.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        LLMAnalysisResult llmResult;
        try
        {
            llmResult = await llmService.AnalyzeAsync(logs, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        // Valida que el LLM devuelva el formato esperado.
        if (!IsValidLLMResult(llmResult, out var validationError))
            return Results.StatusCode(StatusCodes.Status502BadGateway);

        var periodFrom = logs.Min(l => l.Timestamp);
        var periodTo = logs.Max(l => l.Timestamp);

        var analysis = new Analysis
        {
            Id = Guid.NewGuid(),
            Title = $"Análisis de {logs.Count} log(es)",
            Summary = llmResult.Summary,
            ProblemCategories = JsonSerializer.Serialize(llmResult.ProblemCategories),
            ActionSuggestions = JsonSerializer.Serialize(llmResult.ActionSuggestions),
            LogCount = logs.Count,
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            Status = AnalysisStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            LogEntries = logs.Select(l => new AnalysisLog
            {
                LogEntryId = l.Id,
                AnalysisId = Guid.NewGuid() // placeholder, se reasigna abajo
            }).ToList()
        };

        // Corregir los AnalysisId de la unión tras saber el id del análisis.
        foreach (var al in analysis.LogEntries)
            al.AnalysisId = analysis.Id;

        try
        {
            await analysisRepository.AddAsync(analysis, ct);
        }
        catch (SqlException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base de datos para guardar el análisis.",
                type: "database_unavailable");
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo guardar el análisis en la base de datos.",
                type: "database_unavailable");
        }

        return Results.Ok(MapToResponse(analysis));
    }

    /// <summary>
    /// GET /api/analysis/{id} — Obtiene el resultado de un análisis persistido.
    /// </summary>
    public static async Task<IResult> GetAnalysis(
        Guid id,
        IAnalysisRepository analysisRepository,
        CancellationToken ct)
    {
        Analysis? analysis;
        try
        {
            analysis = await analysisRepository.GetByIdAsync(id, ct);
        }
        catch (SqlException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo conectar con la base de datos para obtener el análisis.",
                type: "database_unavailable");
        }
        catch (DbUpdateException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Base de datos no disponible",
                detail: "No se pudo consultar el análisis en la base de datos.",
                type: "database_unavailable");
        }

        if (analysis is null)
            return Results.NotFound(new { error = $"No existe el análisis con id {id}." });

        return Results.Ok(MapToResponse(analysis));
    }

    private static bool IsValidLLMResult(LLMAnalysisResult result, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(result.Summary))
        {
            error = "El resumen del LLM está vacío.";
            return false;
        }

        var categories = result.ProblemCategories?.ToList() ?? new List<string>();
        if (categories.Any(c => string.IsNullOrWhiteSpace(c)))
        {
            error = "Las categorías del LLM contienen valores nulos o vacíos (formato inesperado).";
            return false;
        }

        var suggestions = result.ActionSuggestions?.ToList() ?? new List<string>();
        if (suggestions.Any(s => string.IsNullOrWhiteSpace(s)))
        {
            error = "Las sugerencias del LLM contienen valores nulos o vacíos (formato inesperado).";
            return false;
        }

        return true;
    }

    private static AnalysisResponse MapToResponse(Analysis analysis)
    {
        var categories = JsonSerializer.Deserialize<List<string>>(analysis.ProblemCategories ?? "[]") ?? new List<string>();
        var suggestions = JsonSerializer.Deserialize<List<string>>(analysis.ActionSuggestions ?? "[]") ?? new List<string>();

        return new AnalysisResponse(
            analysis.Id,
            analysis.Title,
            analysis.Summary,
            categories,
            suggestions,
            analysis.LogCount,
            analysis.PeriodFrom,
            analysis.PeriodTo,
            analysis.Status.ToString(),
            analysis.CreatedAt);
    }
}
