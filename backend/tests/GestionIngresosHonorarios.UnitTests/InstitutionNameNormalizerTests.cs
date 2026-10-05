using GestionIngresosHonorarios.Domain.Services;
using Xunit;

namespace GestionIngresosHonorarios.IntegrationTests;

public sealed class InstitutionNameNormalizerTests
{
    [Fact]
    public void DisplayNameCollapsesWhitespaceAndPreservesAccents()
    {
        Assert.Equal("Hospital de Concepción", InstitutionNameNormalizer.NormalizeDisplayName("  Hospital\tde Concepción  "));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("---")]
    public void RejectsNamesWithoutLettersOrNumbers(string name) =>
        Assert.Throws<ArgumentException>(() => InstitutionNameNormalizer.NormalizeDisplayName(name));
}
