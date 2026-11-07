using FluentAssertions;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.LLM;
using Xunit;

namespace SmartLogAnalyzer.UnitTests.LLM;

public class MockLLMServiceTests : IDisposable
{
    private readonly MockLLMService _service = new();

    public void Dispose()
    {
        // Restaura variables de entorno para no afectar a otras pruebas.
        Environment.SetEnvironmentVariable("MOCK_LLM_DELAY_MS", null);
        Environment.SetEnvironmentVariable("MOCK_LLM_MALFORMED", null);
        Environment.SetEnvironmentVariable("MOCK_LLM_TIMEOUT", null);
    }

    [Fact]
    public async Task Respuesta_Valida_Devuelve_Resumen_Categorias_Y_Sugerencias()
    {
        Environment.SetEnvironmentVariable("MOCK_LLM_DELAY_MS", "50");

        var logs = new[]
        {
            new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Error, Message = "fallo pago", Timestamp = DateTimeOffset.UtcNow },
            new LogEntry { Id = Guid.NewGuid(), ServiceName = "inventory", Level = LogLevel.Information, Message = "ok", Timestamp = DateTimeOffset.UtcNow }
        };

        var result = await _service.AnalyzeAsync(logs);

        result.Summary.Should().NotBeNullOrWhiteSpace();
        result.ProblemCategories.Should().NotBeEmpty();
        result.ActionSuggestions.Should().NotBeEmpty();
        result.Summary.Should().Contain("2 logs");
    }

    [Fact]
    public async Task Formato_Inesperado_Devuelve_Valores_Invalidos()
    {
        Environment.SetEnvironmentVariable("MOCK_LLM_MALFORMED", "true");

        var logs = new[]
        {
            new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Error, Message = "fallo", Timestamp = DateTimeOffset.UtcNow }
        };

        var result = await _service.AnalyzeAsync(logs);

        // El mock simula un LLM que devuelve formato inesperado: nulos y valores no esperados.
        result.ProblemCategories.Should().Contain(c => c == null || c == "???" || c == "not-a-category");
        result.ActionSuggestions.Should().Contain(s => s == "123" || s == null);
    }

    [Fact]
    public async Task Demora_Excesiva_Genera_Timeout()
    {
        Environment.SetEnvironmentVariable("MOCK_LLM_TIMEOUT", "true");

        var logs = new[]
        {
            new LogEntry { Id = Guid.NewGuid(), ServiceName = "payments", Level = LogLevel.Error, Message = "fallo", Timestamp = DateTimeOffset.UtcNow }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var act = async () => await _service.AnalyzeAsync(logs, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
