using FluentAssertions;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Validators;
using Xunit;

namespace SmartLogAnalyzer.UnitTests.Validators;

public class AnalyzeLogsRequestValidatorTests
{
    private readonly AnalyzeLogsRequestValidator _validator = new();

    [Fact]
    public void Lista_Con_Ids_Validos_Devuelve_Exito()
    {
        var request = new AnalyzeLogsRequest(new[] { Guid.NewGuid(), Guid.NewGuid() });

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Lista_Vacia_Devuelve_Error()
    {
        var request = new AnalyzeLogsRequest(Array.Empty<Guid>());

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LogIds");
    }

    [Fact]
    public void Lista_Nula_Devuelve_Error()
    {
        var request = new AnalyzeLogsRequest(null!);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LogIds");
    }

    [Fact]
    public void Id_Vacio_GuidEmpty_Devuelve_Error()
    {
        var request = new AnalyzeLogsRequest(new[] { Guid.NewGuid(), Guid.Empty });

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LogIds");
    }
}
