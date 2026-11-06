using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Infrastructure.Repositories;

public class AnalysisRepository : IAnalysisRepository
{
    private readonly AppDbContext _db;

    public AnalysisRepository(AppDbContext db) => _db = db;

    public async Task<Analysis> AddAsync(Analysis analysis, CancellationToken cancellationToken = default)
    {
        var entry = await _db.Analyses.AddAsync(analysis, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    public async Task<Analysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Analyses
            .Include(a => a.LogEntries)
            .ThenInclude(al => al.LogEntry)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<IEnumerable<Analysis>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        _db.Analyses
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IEnumerable<Analysis>)t.Result, cancellationToken);
}
