using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.IntegrationTests;

namespace SmartLogAnalyzer.IntegrationTests.Endpoints;

public class AnalysisEndpointsTests : IClassFixture<TestingWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AnalysisEndpointsTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        // Restaura variables de entorno del mock de LLM entre pruebas.
        Environment.SetEnvironmentVariable("MOCK_LLM_TIMEOUT", null);
    }

    private async Task<(Guid Id, LogResponse Log)> ObtenerUnLogExistente()
    {
        var response = await _client.GetAsync("/api/logs?pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<LogResponse>>(JsonOptions);
        var log = page!.Items.First();
        return (log.Id, log);
    }

    [Fact]
    public async Task Analyze_LogsValidos_Retorna_Analisis_Completo()
    {
        var (id, _) = await ObtenerUnLogExistente();

        var request = new AnalyzeLogsRequest(new[] { id });
        var response = await _client.PostAsJsonAsync("/api/logs/analyze", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var analysis = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);
        analysis!.Id.Should().NotBe(Guid.Empty);
        analysis.LogCount.Should().Be(1);
        analysis.Summary.Should().NotBeNullOrWhiteSpace();
        analysis.ProblemCategories.Should().NotBeEmpty();
        analysis.ActionSuggestions.Should().NotBeEmpty();
        analysis.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Analyze_LogInexistente_Retorna_404()
    {
        var request = new AnalyzeLogsRequest(new[] { Guid.NewGuid() });
        var response = await _client.PostAsJsonAsync("/api/logs/analyze", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analyze_ListaVacia_Retorna_400()
    {
        var request = new AnalyzeLogsRequest(Array.Empty<Guid>());
        var response = await _client.PostAsJsonAsync("/api/logs/analyze", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Analysis_Existente_Retorna_200()
    {
        // Primero crea un análisis.
        var (id, _) = await ObtenerUnLogExistente();
        var create = await _client.PostAsJsonAsync("/api/logs/analyze",
            new AnalyzeLogsRequest(new[] { id }), JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);

        // Luego lo recupera por id.
        var response = await _client.GetAsync($"/api/analysis/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var analysis = await response.Content.ReadFromJsonAsync<AnalysisResponse>(JsonOptions);
        analysis!.Id.Should().Be(created.Id);
        analysis.Summary.Should().Be(created.Summary);
    }

    [Fact]
    public async Task Get_Analysis_Inexistente_Retorna_404()
    {
        var response = await _client.GetAsync($"/api/analysis/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analyze_Con_Llm_Timeout_Retorna_504()
    {
        Environment.SetEnvironmentVariable("MOCK_LLM_TIMEOUT", "true");

        try
        {
            var (id, _) = await ObtenerUnLogExistente();
            var response = await _client.PostAsJsonAsync("/api/logs/analyze",
                new AnalyzeLogsRequest(new[] { id }), JsonOptions);

            response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MOCK_LLM_TIMEOUT", null);
        }
    }
}
