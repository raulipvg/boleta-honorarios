using GestionIngresosHonorarios.Domain.Services;
using Xunit;

namespace GestionIngresosHonorarios.UnitTests;

public sealed class ChileanRutTests
{
    [Theory]
    [InlineData("19.091.616-2", "19091616-2")]
    [InlineData("76389986-1", "76389986-1")]
    [InlineData("88.611.600-4", "88611600-4")]
    public void NormalizesValidRuts(string input, string expected)
    {
        Assert.Equal(expected, ChileanRut.NormalizeAndValidate(input));
    }

    [Theory]
    [InlineData("19.091.616-3")]
    [InlineData("no-es-un-rut")]
    [InlineData("")]
    public void RejectsInvalidRuts(string input)
    {
        Assert.Throws<ArgumentException>(() => ChileanRut.NormalizeAndValidate(input));
    }
}
