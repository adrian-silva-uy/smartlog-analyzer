using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.IntegrationTests;

namespace SmartLogAnalyzer.IntegrationTests.Endpoints;

public class LogsEndpointsTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LogsEndpointsTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_Valido_Retorna_201_Y_Cuerpo_Con_Id()
    {
        var request = new IngestLogRequest(
            "payments", "Error", "Fallo al procesar pago por tarjeta",
            DateTimeOffset.UtcNow.AddSeconds(-10), "api-gateway", "trace-abc", "{\"monto\":100}");

        var response = await _client.PostAsJsonAsync("/api/logs", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var body = await response.Content.ReadFromJsonAsync<LogResponse>(JsonOptions);
        body!.Id.Should().NotBe(Guid.Empty);
        body.ServiceName.Should().Be("payments");
        body.Level.Should().Be("Error");
    }

    [Fact]
    public async Task Post_Invalido_Retorna_400_Con_ProblemaDeValidacion()
    {
        var request = new IngestLogRequest("", "NivelInexistente", "",
            DateTimeOffset.UtcNow.AddHours(1), null, null, null);

        var response = await _client.PostAsJsonAsync("/api/logs", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ServiceName");
        body.Should().Contain("Level");
        body.Should().Contain("Message");
    }

    [Fact]
    public async Task Get_Logs_Retorna_ListaPaginadaConTotales()
    {
        var response = await _client.GetAsync("/api/logs?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<LogResponse>>(JsonOptions);
        body!.Items.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Get_Logs_Filtra_Por_Servicio()
    {
        var response = await _client.GetAsync("/api/logs?serviceName=payments&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<LogResponse>>(JsonOptions);
        body!.Items.Should().NotBeEmpty();
        body.Items.Should().OnlyContain(l => l.ServiceName == "payments");
    }

    [Fact]
    public async Task Get_Logs_Filtra_Por_Nivel()
    {
        var response = await _client.GetAsync("/api/logs?level=Error&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<LogResponse>>(JsonOptions);
        body!.Items.Should().NotBeEmpty();
        body.Items.Should().OnlyContain(l => l.Level == "Error");
    }

    [Fact]
    public async Task Get_Aggregation_Por_Servicio_Retorna_Conteos()
    {
        var response = await _client.GetAsync("/api/logs/aggregation?groupBy=service");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<LogAggregation>>(JsonOptions);
        body.Should().NotBeNull();
        body!.Should().Contain(a => a.Key == "payments" && a.Count > 0);
    }

    [Fact]
    public async Task Get_Aggregation_Por_Nivel_Retorna_Conteos()
    {
        var response = await _client.GetAsync("/api/logs/aggregation?groupBy=level");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<LogAggregation>>(JsonOptions);
        body.Should().NotBeNull();
        body!.Should().Contain(a => a.Key == "Error" && a.Count > 0);
    }
}
