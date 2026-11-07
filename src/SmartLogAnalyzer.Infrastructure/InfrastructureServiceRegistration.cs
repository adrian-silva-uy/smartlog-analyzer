using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartLogAnalyzer.Core.Interfaces;
using SmartLogAnalyzer.Infrastructure.Data;
using SmartLogAnalyzer.Infrastructure.LLM;
using SmartLogAnalyzer.Infrastructure.Repositories;

namespace SmartLogAnalyzer.Infrastructure;

/// <summary>
/// Registro de servicios de la capa de Infraestructura.
/// Req: SQL Server + EF Core, LLM mock por defecto y OpenAI como opción.
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

        // Req: LLM integration — mock real por defecto, OpenAI como esqueleto.
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
