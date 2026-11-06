using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;
using SmartLogAnalyzer.Infrastructure.Repositories;
using Xunit;

namespace SmartLogAnalyzer.UnitTests.Services;

public class LogRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ILogRepository _repo;

    public LogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _repo = new LogRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Agregar_Y_Obtener_PorId_Devuelve_El_Mismo_Log()
    {
        var log = new LogEntry
        {
            Id = Guid.NewGuid(),
            ServiceName = "payments",
            Level = LogLevel.Error,
            Message = "fallo de pago",
            Timestamp = DateTimeOffset.UtcNow
        };

        var creado = await _repo.AddAsync(log);

        var existe = await _repo.ExistsAsync(creado.Id);
        existe.Should().BeTrue();
        creado.ServiceName.Should().Be("payments");
        creado.Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public async Task Obtener_Filtra_Por_Servicio_Y_Nivel()
    {
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Error, Message = "e1", Timestamp = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Information, Message = "e2", Timestamp = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "inventory", Level = LogLevel.Error, Message = "e3", Timestamp = DateTimeOffset.UtcNow });

        var resultado = await _repo.GetAsync(new SmartLogAnalyzer.Core.Dtos.LogFilter
        {
            ServiceName = "payments", Level = "error", Page = 1, PageSize = 10
        });

        resultado.Items.Should().HaveCount(1);
        resultado.TotalCount.Should().Be(1);
        resultado.Items[0].ServiceName.Should().Be("payments");
        resultado.Items[0].Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public async Task Obtener_Pagina_Respeta_Paginacion()
    {
        for (int i = 0; i < 25; i++)
            await _repo.AddAsync(new LogEntry
            {
                Id = Guid.NewGuid(), ServiceName = "svc", Level = LogLevel.Information,
                Message = $"msg {i}", Timestamp = DateTimeOffset.UtcNow
            });

        var pagina1 = await _repo.GetAsync(new SmartLogAnalyzer.Core.Dtos.LogFilter { Page = 1, PageSize = 10 });
        var pagina2 = await _repo.GetAsync(new SmartLogAnalyzer.Core.Dtos.LogFilter { Page = 2, PageSize = 10 });

        pagina1.Items.Should().HaveCount(10);
        pagina1.TotalCount.Should().Be(25);
        pagina1.TotalPages.Should().Be(3);
        pagina2.Items.Should().HaveCount(10);
    }

    // este test esta un poco repetitivo con el de nivel pero lo dejo asi
    [Fact]
    public async Task Agregacion_Por_Servicio_Returns_Counts()
    {
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Error, Message = "m1", Timestamp = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Error, Message = "m2", Timestamp = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "inventory", Level = LogLevel.Warning, Message = "m3", Timestamp = DateTimeOffset.UtcNow });

        var agregacion = (await _repo.GetAggregationAsync(new SmartLogAnalyzer.Core.Dtos.LogAggregationRequest("service", null, null))).ToList();

        agregacion.Should().HaveCount(2);
        agregacion.First(a => a.Key == "payments").Count.Should().Be(2);
        agregacion.First(a => a.Key == "inventory").Count.Should().Be(1);
    }

    [Fact]
    public async Task Agregacion_Por_Nivel_Returns_Counts()
    {
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "svc", Level = LogLevel.Error, Message = "m1", Timestamp = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "svc", Level = LogLevel.Error, Message = "m2", Timestamp = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new LogEntry { Id = Guid.NewGuid(), ServiceName = "svc", Level = LogLevel.Information, Message = "m3", Timestamp = DateTimeOffset.UtcNow });

        var agregacion = (await _repo.GetAggregationAsync(new SmartLogAnalyzer.Core.Dtos.LogAggregationRequest("level", null, null))).ToList();

        agregacion.Should().HaveCount(2);
        agregacion.First(a => a.Key == "Error").Count.Should().Be(2);
        agregacion.First(a => a.Key == "Information").Count.Should().Be(1);
    }
}
