using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Entities;

namespace SmartLogAnalyzer.Infrastructure.Data;

/// <summary>
/// Contexto de Entity Framework Core para SmartLog Analyzer.
/// Modelo de datos:
///  - LogEntries: logs de los microservicios.
///  - Analyses: resultados de los análisis de IA.
///  - AnalysisLogs: unión muchos-a-muchos entre análisis y logs.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<LogEntry> LogEntries { get; set; }
    public DbSet<Analysis> Analyses { get; set; }
    public DbSet<AnalysisLog> AnalysisLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Índices para las consultas de listado y agregación más frecuentes.
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasIndex(e => e.ServiceName);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.Metadata).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.ProblemCategories).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ActionSuggestions).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<AnalysisLog>(entity =>
        {
            entity.HasKey(al => new { al.AnalysisId, al.LogEntryId });
            entity.HasOne(al => al.Analysis)
                .WithMany(a => a.LogEntries)
                .HasForeignKey(al => al.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(al => al.LogEntry)
                .WithMany()
                .HasForeignKey(al => al.LogEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
