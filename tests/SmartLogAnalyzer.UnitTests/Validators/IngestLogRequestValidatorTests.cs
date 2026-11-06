using FluentAssertions;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Entities;
using SmartLogAnalyzer.Core.Validators;
using Xunit;

namespace SmartLogAnalyzer.UnitTests.Validators;

public class IngestLogRequestValidatorTests
{
    private readonly IngestLogRequestValidator _validator = new();

    [Fact]
    public void Solicitud_Valida_Devuelve_Exito()
    {
        var request = new IngestLogRequest(
            "payments", "Error", "Fallo al procesar pago",
            DateTimeOffset.UtcNow.AddMinutes(-1), "api", "trace-1", "{}");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Servicio_Vacio_Devuelve_Error()
    {
        var request = new IngestLogRequest("", "Information", "msg",
            DateTimeOffset.UtcNow, null, null, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ServiceName");
    }

    [Fact]
    public void Nivel_Invalido_Devuelve_Error()
    {
        var request = new IngestLogRequest("svc", "Fatalissimo", "msg",
            DateTimeOffset.UtcNow, null, null, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Level");
    }

    [Fact]
    public void Mensaje_Vacio_Devuelve_Error()
    {
        var request = new IngestLogRequest("svc", "Information", "",
            DateTimeOffset.UtcNow, null, null, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }

    [Fact]
    public void Timestamp_En_El_Futuro_Devuelve_Error()
    {
        var request = new IngestLogRequest("svc", "Information", "msg",
            DateTimeOffset.UtcNow.AddHours(1), null, null, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public void Mensaje_Muy_Largo_Devuelve_Error()
    {
        var request = new IngestLogRequest("svc", "Information", new string('x', 4001),
            DateTimeOffset.UtcNow, null, null, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Message");
    }
}
