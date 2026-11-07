namespace SmartLogAnalyzer.Core.Entities;

/// <summary>
/// Niveles de severidad de un log (alineados con Serilog / Microsoft.Extensions.Logging).
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
    Fatal = 5
}

/// <summary>
/// Entrada de log de un microservicio. Es la entidad raíz del agregado de logs.
/// </summary>
public class LogEntry
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = default!;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = default!;
    public DateTimeOffset Timestamp { get; set; }
    public string? Source { get; set; }           // Origen del log
    public string? TraceId { get; set; }          // Identificador de trazabilidad distribuida
    public string? Metadata { get; set; }         // Metadatos arbitrarios del log
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Resultado de un análisis sobre un conjunto de logs. Persistido para auditoría y consulta.
/// </summary>
public class Analysis
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;             // Resumen en lenguaje natural
    public string ProblemCategories { get; set; } = "[]";        // JSON: categorización de problemas detectados
    public string ActionSuggestions { get; set; } = "[]";        // JSON: sugerencias de acción
    public int LogCount { get; set; }
    public DateTimeOffset? PeriodFrom { get; set; }
    public DateTimeOffset? PeriodTo { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Completed;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AnalysisLog> LogEntries { get; set; } = new List<AnalysisLog>();
}

/// <summary>
/// Entidad de unión entre análisis y logs (relación muchos-a-muchos). Permite saber qué logs
/// participaron en cada análisis sin denormalizar identificadores en JSON.
/// </summary>
public class AnalysisLog
{
    public Guid AnalysisId { get; set; }
    public Analysis Analysis { get; set; } = default!;
    public Guid LogEntryId { get; set; }
    public LogEntry LogEntry { get; set; } = default!;
}

public enum AnalysisStatus
{
    Pending,
    Completed,
    Failed
}
