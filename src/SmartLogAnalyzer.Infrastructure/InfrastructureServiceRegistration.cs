using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;
using SmartLogAnalyzer.Infrastructure.LLM;
using SmartLogAnalyzer.Infrastructure.Repositories;

namespace SmartLogAnalyzer.Infrastructure;

/// <summary>
/// Punto único de registro de la capa de Infraestructura.
/// Decisiones (documentadas en ARCHITECTURE.md):
///  - LLM por defecto: MockLLMService (sin costo, sin API key). Se cambia a OpenAILLMService
///    estableciendo LLM__Provider=openai y las variables de entorno correspondientes.
///  - Base de datos: SQL Server (proveedor nativo de .NET, excelente soporte en Docker y EF Core).
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.CommandTimeout(30);
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            }));

        services.AddScoped<ILogRepository, LogRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();

        // Selección de proveedor de LLM. Por defecto mock realista (sin costo, sin API key).
        // Para usar la integración real con OpenAI: LLM__Provider=openai + LLM__ApiKey.
        var llmProvider = configuration["LLM__Provider"] ?? "mock";

        if (llmProvider == "openai")
        {
            var apiKey = configuration["LLM__ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("LLM__ApiKey es obligatoria cuando LLM__Provider=openai.");

            services.AddHttpClient<ILLMService, OpenAILLMService>(client =>
            {
                client.BaseAddress = new Uri(configuration["LLM__BaseUrl"] ?? "https://api.openai.com/v1");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            services.AddScoped<ILLMService, MockLLMService>();
        }

        return services;
    }
}
