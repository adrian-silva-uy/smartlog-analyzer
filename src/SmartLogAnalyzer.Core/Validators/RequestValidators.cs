using FluentValidation;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;

namespace SmartLogAnalyzer.Core.Validators;

/// <summary>
/// Validador de IngestLogRequest (Req: validación de entrada).
/// </summary>
public class IngestLogRequestValidator : AbstractValidator<IngestLogRequest>
{
    private static readonly string[] ValidLevels =
        Enum.GetNames<LogLevel>().Select(n => n.ToLowerInvariant()).ToArray();

    public IngestLogRequestValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty().WithMessage("El nombre del servicio es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre del servicio no puede superar los 100 caracteres.");

        RuleFor(x => x.Level)
            .NotEmpty().WithMessage("El nivel es obligatorio.")
            .Must(BeValidLevel).WithMessage($"Nivel inválido. Valores permitidos: {string.Join(", ", ValidLevels)}.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("El mensaje es obligatorio.")
            .MaximumLength(4000).WithMessage("El mensaje no puede superar los 4000 caracteres.");

        RuleFor(x => x.Timestamp)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("El timestamp no puede estar en el futuro (máx. 5 minutos de tolerancia).");
    }

    private static bool BeValidLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return false;
        return Enum.TryParse<LogLevel>(level, true, out _);
    }
}

/// <summary>
/// Validador de AnalyzeLogsRequest.
/// </summary>
public class AnalyzeLogsRequestValidator : AbstractValidator<AnalyzeLogsRequest>
{
    public AnalyzeLogsRequestValidator()
    {
        RuleFor(x => x.LogIds)
            .NotNull().WithMessage("Se debe especificar al menos un log para analizar.")
            .NotEmpty().WithMessage("La lista de logs no puede estar vacía.")
            .Must(ids => ids != null && ids.All(id => id != Guid.Empty))
            .WithMessage("Uno o más identificadores de log son inválidos (Guid.Empty).");
    }
}

/// <summary>
/// Validador de LogFilter.
/// </summary>
public class LogFilterValidator : AbstractValidator<LogFilter>
{
    public LogFilterValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("La página debe ser mayor a 0.");
        RuleFor(x => x.PageSize)
            .GreaterThan(0).WithMessage("El tamaño de página debe ser mayor a 0.")
            .LessThanOrEqualTo(100).WithMessage("El tamaño de página no puede superar los 100.");
        RuleFor(x => x.Level)
            .Must(BeValidLevelOrNull).WithMessage($"Nivel inválido. Valores permitidos: {string.Join(", ", Enum.GetNames<LogLevel>().Select(n => n.ToLowerInvariant()))}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Level));
        RuleFor(x => x)
            .Must(HaveValidDateRange)
            .WithMessage("La fecha 'From' no puede ser posterior a 'To'.")
            .When(x => x.From.HasValue && x.To.HasValue);
    }

    private static bool BeValidLevelOrNull(string? level) =>
        string.IsNullOrWhiteSpace(level) || Enum.TryParse<LogLevel>(level, true, out _);

    private static bool HaveValidDateRange(LogFilter f) => f.From!.Value <= f.To!.Value;
}
