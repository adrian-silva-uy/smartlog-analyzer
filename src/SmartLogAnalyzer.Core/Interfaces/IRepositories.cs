using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;

namespace SmartLogAnalyzer.Core.Interfaces;

/// <summary>
/// Contrato del repositorio de logs.
/// </summary>
public interface ILogRepository
{
    Task<LogEntry> AddAsync(LogEntry log, CancellationToken cancellationToken = default);
    Task<PagedResult<LogEntry>> GetAsync(LogFilter filter, CancellationToken cancellationToken = default);
    Task<IEnumerable<LogAggregation>> GetAggregationAsync(LogAggregationRequest request, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<LogEntry>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contrato del repositorio de análisis.
/// </summary>
public interface IAnalysisRepository
{
    Task<Analysis> AddAsync(Analysis analysis, CancellationToken cancellationToken = default);
    Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Analysis>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contrato de integración con el LLM.
/// Req: LLM integration — mock real por defecto, OpenAI como esqueleto.
/// </summary>
public interface ILLMService
{
    Task<LLMAnalysisResult> AnalyzeAsync(IEnumerable<LogEntry> logs, CancellationToken cancellationToken = default);
}

public record LLMAnalysisResult(string Summary, IEnumerable<string> ProblemCategories, IEnumerable<string> ActionSuggestions);
