using Microsoft.Extensions.Configuration;
using System.Text.Json;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Interfaces;

namespace SmartLogAnalyzer.Infrastructure.LLM;

/// <summary>
/// Mock realista del servicio de LLM. Permite desarrollar y probar sin API key ni costo.
/// Req: LLM integration — mock real por defecto.
/// Variables de entorno:
///  - MOCK_LLM_DELAY_MS: demora artificial (predeterminado: 400 ms).
///  - MOCK_LLM_MALFORMED: si es "true", devuelve JSON inesperado para probar robustez.
///  - MOCK_LLM_TIMEOUT: si es "true", simula timeout del LLM.
/// Para la integración real registrar una implementación de ILLMService (ver OpenAILLMService).
/// </summary>
public class MockLLMService : ILLMService
{
    private static readonly Random _random = new();

    public async Task<LLMAnalysisResult> AnalyzeAsync(
        IEnumerable<LogEntry> logs,
        CancellationToken cancellationToken = default)
    {
        var logList = logs.ToList();
        if (logList.Count == 0)
        {
            // Lista vacía: devuelve resultado vacío y no rompe el endpoint.
            return new LLMAnalysisResult("No hay logs para analizar.", Enumerable.Empty<string>(), Enumerable.Empty<string>());
        }
        // El mock no replica exactamente el formato de OpenAI. Con LLM__Provider=openai ajusta el mapeo en OpenAILLMService.
        var envDelay = Environment.GetEnvironmentVariable("MOCK_LLM_DELAY_MS");
        var delayMs = string.IsNullOrWhiteSpace(envDelay) || !int.TryParse(envDelay ?? string.Empty, out var parsed)
            ? 400
            : parsed;

        var malformed = Environment.GetEnvironmentVariable("MOCK_LLM_MALFORMED") == "true";
        var timeout = Environment.GetEnvironmentVariable("MOCK_LLM_TIMEOUT") == "true";

        // Simula demora excesiva (timeout). Respeta la cancellationToken del llamador.
        if (timeout)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new OperationCanceledException("El LLM superó el tiempo de espera.", cancellationToken);
        }

        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var errorLogs = logList.Where(l => l.Level is LogLevel.Error or LogLevel.Critical or LogLevel.Fatal).ToList();
        var warnLogs = logList.Where(l => l.Level == LogLevel.Warning).ToList();

        var summary = malformed
            ? "{{ malformed json without closing brace"
            : BuildSummary(logList, errorLogs, warnLogs);

        var categories = malformed
            ? new[] { "not-a-category", "???", null! }
            : BuildCategories(errorLogs, warnLogs);

        var suggestions = malformed
            ? new[] { "123", null! } // simula valores inesperados/nulos del LLM
            : BuildSuggestions(errorLogs, warnLogs);

        return new LLMAnalysisResult(summary, categories, suggestions);
    }

    private static string BuildSummary(IReadOnlyList<LogEntry> all, IReadOnlyList<LogEntry> errors, IReadOnlyList<LogEntry> warns)
    {
        var services = all.Select(l => l.ServiceName).Distinct().OrderBy(s => s).ToList();
        var byService = all.GroupBy(l => l.ServiceName).ToDictionary(g => g.Key!, g => g.Count());
        var servicesText = string.Join(", ", services);

        return $"Se analizaron {all.Count} logs de {services.Count} servicio(s) ({servicesText}). " +
               $"Distribución por servicio: {string.Join("; ", byService.Select(kv => $"{kv.Key}={kv.Value}"))}. " +
               $"Se detectaron {errors.Count} evento(s) de error/crítico y {warns.Count} advertencia(s). " +
               (errors.Count > 0
                   ? "El patrón predominante sugiere un componente con fallas recurrentes que requiere revisión inmediata."
                   : "No se detectaron errores críticos; el comportamiento está dentro de parámetros esperados.");
    }

    private static IEnumerable<string> BuildCategories(IReadOnlyList<LogEntry> errors, IReadOnlyList<LogEntry> warns)
    {
        var categories = new List<string>();
        if (errors.Count > 0) categories.Add("Errores de aplicación");
        if (errors.Count > 3) categories.Add("Tasa de error elevada");
        if (warns.Count > 0) categories.Add("Advertencias de degradación");
        if (categories.Count == 0) categories.Add("Comportamiento nominal");
        return categories;
    }

    private static IEnumerable<string> BuildSuggestions(IReadOnlyList<LogEntry> errors, IReadOnlyList<LogEntry> warns)
    {
        var suggestions = new List<string>();
        if (errors.Count > 0)
            suggestions.Add("Revisar los stack traces de los logs de error críticos y correlacionar por TraceId.");
        if (errors.Count > 3)
            suggestions.Add("Evaluar un despliegue revertido o rollback del servicio afectado mientras se investiga.");
        if (warns.Count > 0)
            suggestions.Add("Monitorear métricas de latencia y saturación del servicio para prevenir escalamiento a error.");
        if (errors.Count == 0 && warns.Count == 0)
            suggestions.Add("No se requiere acción inmediata; continuar con el monitoreo de rutina.");
        return suggestions;
    }
}

/// <summary>
/// Esqueleto de la integración real con OpenAI (o cualquier proveedor compatible con su API de chat).
/// Req: OpenAILLMService skeleton disponible. No activo por defecto.
/// </summary>
public class OpenAILLMService : ILLMService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public OpenAILLMService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _apiKey = configuration["LLM__ApiKey"] ?? string.Empty;
        _baseUrl = configuration["LLM__BaseUrl"] ?? "https://api.openai.com/v1";
    }

    public async Task<LLMAnalysisResult> AnalyzeAsync(
        IEnumerable<LogEntry> logs,
        CancellationToken cancellationToken = default)
    {
        var logEntries = logs.ToList();
        var prompt = BuildPrompt(logEntries);

        var request = new
        {
            model = "gpt-4o-mini",
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "Eres un analista de logs experto. Responde SIEMPRE en formato JSON válido con las claves: summary, problem_categories (array de strings), action_suggestions (array de strings). Responde en español." },
                new { role = "user", content = prompt }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/chat/completions");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

        using var contentDoc = JsonDocument.Parse(content);
        var summary = contentDoc.RootElement.GetProperty("summary").GetString()!;
        var categories = contentDoc.RootElement.GetProperty("problem_categories").EnumerateArray().Select(x => x.GetString()!).ToList();
        var suggestions = contentDoc.RootElement.GetProperty("action_suggestions").EnumerateArray().Select(x => x.GetString()!).ToList();

        return new LLMAnalysisResult(summary, categories, suggestions);
    }

    private static string BuildPrompt(IReadOnlyList<LogEntry> logs)
    {
        var lines = logs.Select(l =>
            $"[{l.Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz}] {l.Level} [{l.ServiceName}] {l.Message}{(string.IsNullOrWhiteSpace(l.TraceId) ? "" : $" traceId={l.TraceId}")}");
        return $"Analiza el siguiente conjunto de logs de microservicios y devuelve un resumen, categorías de problemas y sugerencias de acción:\n\n{string.Join("\n", lines)}";
    }
}

// La validación de la respuesta del LLM ocurre en AnalysisEndpoints.cs.
