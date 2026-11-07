namespace SmartLogAnalyzer.Core.Dtos;

/// <summary>
/// Filtros para listar logs (todos opcionales).
/// </summary>
public record LogFilter
{
    public string? ServiceName { get; init; }
    public string? Level { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Search { get; init; }            // Búsqueda libre en mensaje y servicio
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Resultado paginado de logs
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Resultado de agregación de logs (por servicio, nivel o fecha)
/// </summary>
public record LogAggregation(string Key, int Count);

/// <summary>
/// Solicitud de agregación
/// </summary>
public record LogAggregationRequest(string GroupBy, DateTimeOffset? From, DateTimeOffset? To);

/// <summary>
/// DTO de entrada para ingestar un log
/// </summary>
public record IngestLogRequest(
    string ServiceName,
    string Level,
    string Message,
    DateTimeOffset Timestamp,
    string? Source,
    string? TraceId,
    string? Metadata
);

/// <summary>
/// DTO de respuesta de un log ingestado
/// </summary>
public record LogResponse(
    Guid Id,
    string ServiceName,
    string Level,
    string Message,
    DateTimeOffset Timestamp,
    string? Source,
    DateTimeOffset CreatedAt
);

/// <summary>
/// DTO de solicitud de análisis (se envían los ids de los logs)
/// </summary>
public record AnalyzeLogsRequest(IEnumerable<Guid> LogIds);

/// <summary>
/// DTO de respuesta de un análisis
/// </summary>
public record AnalysisResponse(
    Guid Id,
    string Title,
    string Summary,
    IEnumerable<string> ProblemCategories,
    IEnumerable<string> ActionSuggestions,
    int LogCount,
    DateTimeOffset? PeriodFrom,
    DateTimeOffset? PeriodTo,
    string Status,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Respuesta envuelta para errores de la API (ProblemDetails)
/// </summary>
public record ErrorResponse(string Type, string Title, int Status, string Detail, IEnumerable<string>? Errors = null);
