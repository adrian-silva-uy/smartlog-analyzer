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

// ─── Logging estructurado (Serilog) ─────────────────────────────────────────
// Justificación: en un analizador de logs es incoherente no usar logging estructurado.
// Serilog brinda enriquecimiento, sinks (consola/archivo/SEQ) y formato JSON listo para producción.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
builder.Host.UseSerilog();

// ─── Servicios ──────────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// DbContext se registra dentro de AddInfrastructure (SQL Server con retry policy).
// Se aplica el seed automáticamente al arrancar para que la app esté lista tras `docker compose up`.
builder.Services.AddHostedService<SeedHostedService>();

// FluentValidation: validadores registrados desde el ensamblado de Core.
// ASP.NET Core los aplica automáticamente a los parámetros bound de los minimal APIs (400 ValidationProblem).
// Registro explícito de validadores como IValidator<T>. ASP.NET Core los aplica automáticamente
// a los parámetros bound de los minimal APIs, devolviendo 400 ValidationProblem cuando fallan.
builder.Services.AddScoped<IValidator<IngestLogRequest>, IngestLogRequestValidator>();
builder.Services.AddScoped<IValidator<AnalyzeLogsRequest>, AnalyzeLogsRequestValidator>();
builder.Services.AddScoped<IValidator<LogFilter>, LogFilterValidator>();

// ProblemDetails: respuestas de error estandarizadas (RFC 7807) con código HTTP correcto.
builder.Services.AddProblemDetails();

// OpenAPI / Swagger: documentación automática de la API (extra justificado: facilita el consumo y pruebas).
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
}

// Manejo centralizado de excepciones: convierte excepciones no controladas en respuestas
// ProblemDetails consistentes con código HTTP adecuado (500 por defecto).
app.UseMiddleware<ExceptionMiddleware>();

app.UseStatusCodePages(); // Devuelve cuerpo para códigos de estado sin body (ej. 404).

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
/// Servicio en segundo plano que aplica migraciones y seed al arrancar la aplicación.
/// Justificación: permite que `docker compose up` levante todo sin pasos manuales adicionales,
/// cumpliendo el requisito de despliegue con un solo comando.
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
            _logger.LogError(ex, "Error al migrar/sembrar la base de datos.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Mapeo de entidades a DTOs de respuesta. Separado para mantener los endpoints libres de lógica de presentación.
/// </summary>
public static class LogMapping
{
    public static LogResponse ToResponse(this LogEntry log) =>
        new(log.Id, log.ServiceName, log.Level.ToString(), log.Message, log.Timestamp, log.Source, log.CreatedAt);
}
