using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Infrastructure.Data;

/// <summary>
/// Seed de datos de ejemplo para probar sin ingestar logs manualmente.
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (await db.LogEntries.AnyAsync())
            return; // Ya existe información, no se vuelve a sembrar.

        var logs = new List<LogEntry>();
        var services = new[] { "payments", "inventory", "notifications" };
        var rng = new Random(42);
        var baseTime = DateTimeOffset.UtcNow.AddDays(-2);

        // 120 logs de ejemplo para probar paginación, agregación y análisis.
        for (int i = 0; i < 120; i++)
        {
            var service = services[rng.Next(services.Length)];
            var level = (LogLevel)rng.Next(6);
            // Sesgo intencional: payments con más errores para probar agregación.
            if (service == "payments" && rng.Next(100) < 25)
                level = LogLevel.Error;

            logs.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                ServiceName = service,
                Level = level,
                Message = level >= LogLevel.Error
                    ? $"Excepción al procesar transacción en {service}: tiempo de respuesta superado."
                    : $"Operación normal en {service} completada correctamente.",
                Timestamp = baseTime.AddMinutes(rng.Next(0, 60 * 48)),
                Source = $"k8s-pod-{service}-{rng.Next(1, 5)}",
                TraceId = $"trace-{rng.Next(1000, 9999)}",
                Metadata = "{\"requestId\":\"req-\",\"version\":\"1.0.0\"}"
            });
        }

        await db.LogEntries.AddRangeAsync(logs);
        await db.SaveChangesAsync();
    }
}
