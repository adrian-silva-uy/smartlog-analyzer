using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Infrastructure.Repositories;

public class LogRepository : ILogRepository
{
    private readonly AppDbContext _db;

    public LogRepository(AppDbContext db) => _db = db;

    public async Task<LogEntry> AddAsync(LogEntry log, CancellationToken cancellationToken = default)
    {
        var entry = await _db.LogEntries.AddAsync(log, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    public async Task<PagedResult<LogEntry>> GetAsync(LogFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.LogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.ServiceName))
            query = query.Where(e => e.ServiceName.Contains(filter.ServiceName!));

        if (!string.IsNullOrWhiteSpace(filter.Level) &&
            Enum.TryParse<LogLevel>(filter.Level, true, out var level))
        {
            query = query.Where(e => e.Level == level);
        }

        if (filter.From.HasValue)
            query = query.Where(e => e.Timestamp >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(e => e.Timestamp <= filter.To.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLowerInvariant();
            query = query.Where(e =>
                e.ServiceName.ToLower().Contains(search) ||
                e.Message.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<LogEntry>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<IEnumerable<LogAggregation>> GetAggregationAsync(
        LogAggregationRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.LogEntries.AsQueryable();

        if (request.From.HasValue)
            query = query.Where(e => e.Timestamp >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(e => e.Timestamp <= request.To.Value);

        // Proyecto a un tipo anónimo con Count() para que EF Core traduzca GROUP BY + COUNT a SQL.
        // Luego mapeo a LogAggregation en memoria, para que evite errores de traducción de LINQ.
        var rows = request.GroupBy.ToLowerInvariant() switch
        {
            "service" => await query
                .GroupBy(e => e.ServiceName)
                .Select(g => new { Key = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken),
            "level" => await query
                .GroupBy(e => e.Level.ToString())
                .Select(g => new { Key = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken),
            _ => await query
                .GroupBy(e => e.ServiceName)
                .Select(g => new { Key = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken)
        };

        return rows.Select(x => new LogAggregation(x.Key, x.Count)).ToList();
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.LogEntries.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task<IEnumerable<LogEntry>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        // esto podria optimizarse pero funciona para el prototipo
        return await _db.LogEntries
            .Where(e => idList.Contains(e.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
