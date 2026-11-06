using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartLogAnalyzer.IntegrationTests;

namespace SmartLogAnalyzer.IntegrationTests.Endpoints;

public class HealthEndpointTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HealthEndpointTests(TestingWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Retorna_200_Y_BaseDeDatos_Alcanzable()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        body!.Status.Should().Be("Healthy");
        body.DatabaseReachable.Should().BeTrue();
    }
}

/// <summary>
/// Duplicado intencional del DTO de HealthResponse para evitar acoplar los tests
/// al espacio de nombres del proyecto Api (los tests sólo validan el contrato HTTP).
/// </summary>
public record HealthResponse(string Status, DateTimeOffset CheckedAt, bool DatabaseReachable);
