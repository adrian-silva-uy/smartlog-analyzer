using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;
using SmartLogAnalyzer.Api.Endpoints;
using SmartLogAnalyzer.Api.Middleware;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;
using FluentValidation;
using SmartLogAnalyzer.Core.Validators;
using SmartLogAnalyzer.Infrastructure;
using SmartLogAnalyzer.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ─── Logging estructurado (Serilog) — Req: logging estructurado ─────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Servicios ──────────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// DbContext se registra dentro de AddInfrastructure (SQL Server con retry policy).
// El seed se aplica manualmente con: dotnet ef database update
// builder.Services.AddHostedService<SeedHostedService>();

// Req: validación de entrada con FluentValidation
builder.Services.AddScoped<IValidator<IngestLogRequest>, IngestLogRequestValidator>();
builder.Services.AddScoped<IValidator<AnalyzeLogsRequest>, AnalyzeLogsRequestValidator>();
builder.Services.AddScoped<IValidator<LogFilter>, LogFilterValidator>();

// Req: respuestas de error estandarizadas (ProblemDetails / RFC 7807)
builder.Services.AddProblemDetails();

// Req: Swagger/OpenAPI en la raíz /
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartLog Analyzer API",
        Version = "v1",
        Description = "API para ingestar logs de microservicios, agregarlos y analizarlos con IA."
    });
});

var app = builder.Build();

// ─── Middleware ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.RoutePrefix = string.Empty); // Swagger en la raíz /
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseMiddleware<ExceptionMiddleware>();
}

app.UseStatusCodePages(); // Devuelve cuerpo para estados sin body (ej. 404).

// ─── Endpoints ──────────────────────────────────────────────────────────────
var logs = app.MapGroup("/api/logs");
logs.MapPost("/", LogsEndpoints.IngestLog)
    .WithName("IngestLog")
    .WithSummary("Ingerir un log")
    .Produces<LogResponse>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status500InternalServerError);

logs.MapGet("/", LogsEndpoints.ListLogs)
    .WithName("ListLogs")
    .WithSummary("Listar logs con filtros y paginación")
    .Produces<PagedResult<LogResponse>>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest);

logs.MapGet("/aggregation", LogsEndpoints.AggregateLogs)
    .WithName("AggregateLogs")
    .WithSummary("Agregación de logs por servicio o nivel")
    .Produces<IEnumerable<LogAggregation>>(StatusCodes.Status200OK);

logs.MapPost("/analyze", AnalysisEndpoints.AnalyzeLogs)
    .WithName("AnalyzeLogs")
    .WithSummary("Enviar logs al LLM para su análisis")
    .Produces<AnalysisResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status502BadGateway); // LLM devolvió formato inválido

app.MapGet("/api/analysis/{id}", AnalysisEndpoints.GetAnalysis)
    .WithName("GetAnalysis")
    .WithSummary("Obtener el resultado de un análisis por id")
    .Produces<AnalysisResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/health", HealthEndpoints.Health)
    .WithName("Health")
    .WithSummary("Verificar el estado del servicio y la base de datos")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

app.Run();

/// <summary>
/// Respuesta del health check: estado de la aplicación y conectividad con la base de datos.
/// </summary>
public record HealthResponse(string Status, DateTimeOffset CheckedAt, bool DatabaseReachable);
// TODO: agregar mas informacion al health (memoria, disco, etc.) cuando tengamos tiempo

/// <summary>
/// Servicio en segundo plano que aplica migraciones y seed al arrancar.
/// Req: despliegue con un solo comando (docker compose up).
/// </summary>
public class SeedHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SeedHostedService> _logger;

    public SeedHostedService(IServiceProvider services, ILogger<SeedHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                try
        {
            await db.Database.MigrateAsync(cancellationToken);
            await SeedData.InitializeAsync(db);
            _logger.LogInformation("Base de datos migrada y sembrada correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo migrar/sembrar la base de datos al arrancar. La aplicación continuará sin base de datos.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Mapeo de entidades a DTOs de respuesta.
/// </summary>
public static class LogMapping
{
    public static LogResponse ToResponse(this LogEntry log) =>
        new(log.Id, log.ServiceName, log.Level.ToString(), log.Message, log.Timestamp, log.Source, log.CreatedAt);
}
