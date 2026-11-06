using FluentAssertions;
using SmartLogAnalyzer.Core.Dtos;
using SmartLogAnalyzer.Core.Validators;
using Xunit;

namespace SmartLogAnalyzer.UnitTests.Validators;

public class LogFilterValidatorTests
{
    private readonly LogFilterValidator _validator = new();

    [Fact]
    public void Filtro_Valido_Devuelve_Exito()
    {
        var filter = new LogFilter { Page = 1, PageSize = 50, ServiceName = "payments", Level = "error" };

        var result = _validator.Validate(filter);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Pagina_Cero_Devuelve_Error()
    {
        var filter = new LogFilter { Page = 0, PageSize = 50 };
        var result = _validator.Validate(filter);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Page");
    }

    [Fact]
    public void TamanoPagina_MayorA100_Devuelve_Error()
    {
        var filter = new LogFilter { Page = 1, PageSize = 101 };
        var result = _validator.Validate(filter);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Nivel_Invalido_Devuelve_Error()
    {
        var filter = new LogFilter { Page = 1, PageSize = 10, Level = "critico" };
        var result = _validator.Validate(filter);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Level");
    }

    [Fact]
    public void FechaFrom_PosteriorATo_Devuelve_Error()
    {
        var filter = new LogFilter
        {
            Page = 1, PageSize = 10,
            From = DateTimeOffset.UtcNow.AddDays(1),
            To = DateTimeOffset.UtcNow
        };
        var result = _validator.Validate(filter);
        result.IsValid.Should().BeFalse();
    }
}
