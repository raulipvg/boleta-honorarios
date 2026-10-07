using GestionIngresosHonorarios.Infrastructure.Parsing;
using Xunit;

namespace GestionIngresosHonorarios.UnitTests;

public sealed class CebienEmailParserTests
{
    [Fact]
    public async Task ParsesCebienEmailExampleAndReportedBoletaBreakdown()
    {
        var emailBody = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "CentroCebienEmailExample.txt"));

        var result = new CebienEmailParser().Parse(emailBody);

        Assert.Equal("DRA. ALEJANDRA PEZO", result.ProfessionalName);
        Assert.Equal((short)2026, result.ServiceYear);
        Assert.Equal((short)8, result.ServiceMonth);
        Assert.Equal(19L, result.AttentionCount);
        Assert.Equal("Atenciones Psiquiatricas", Assert.Single(result.AttentionCountsByService).ServiceName);
        Assert.Equal(418_000L, result.GrossTotalClp);
        Assert.Equal(15.25m, result.ReportedRetentionPercentage);
        Assert.Equal(63_745L, result.ReportedRetentionClp);
        Assert.Equal(354_255L, result.ReportedNetTotalClp);
    }

    [Fact]
    public async Task RejectsEmailWithoutBoletaBreakdown()
    {
        var emailBody = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "CentroCebienEmailExample.txt"));
        var incomplete = emailBody.Replace("15.25%", "", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => new CebienEmailParser().Parse(incomplete));
    }
}
