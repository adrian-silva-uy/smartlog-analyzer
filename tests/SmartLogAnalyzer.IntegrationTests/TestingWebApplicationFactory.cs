using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartLogAnalyzer.Infrastructure.Data;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.LLM;

namespace SmartLogAnalyzer.IntegrationTests;

/// <summary>
/// Factoría de la aplicación para tests de integración.
/// Sustituye SQL Server por una base de datos InMemory con nombre único por instancia
/// (aislamiento entre pruebas) y mantiene el mock de LLM, de modo que los tests no
/// requieran infraestructura externa ni API keys.
/// </summary>
public class TestingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"SmartLogTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Eliminar registros de SqlServer y usar InMemory para pruebas aisladas.
            var descriptors = services
                .Where(d => d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Mock de LLM explícito (sin red, sin API key, sin costo).
            var llmDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILLMService));
            if (llmDescriptor is not null)
                services.Remove(llmDescriptor);
            services.AddScoped<ILLMService, MockLLMService>();
        });
    }
}
