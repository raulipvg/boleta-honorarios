using GestionIngresosHonorarios.Infrastructure.Parsing;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace GestionIngresosHonorarios.UnitTests;

public sealed class SanatorioAlemanLiquidationPdfParserTests
{
    [Fact]
    public void ExtractsHeaderRowsAndTotalsAcrossPageBoundaries()
    {
        const string text = """
            LIQUIDACION POR PARTICIPACIONES
            76389986-1
            SERVICIOS SANATORIO ALEMAN SPA
            Nro : 220081
            Rut cobrador : 19091616-2
            Cobrador : PROFESIONAL DE PRUEBA
            Fecha Liquidacion : 22-09-2026
            Servicio de pago : CONSULTAS MEDICAS
            Perido : 09-2026 / 1era. QUINCENA
            Ejecutor : PROFESIONAL DE PRUEBA
            Servicio Nro Atencion Nombre Paciente Fecha Atencion Prevision Cod prestacion Prestacion Valor Venta Factor Pago Valor Pago
            CONSULTAS MEDICAS - PRESENCIAL
            5461247-T Paciente Uno 02-09-2026 Fonasa 01.01.001 Consulta Medicina General $25.884 79.50% $20.578
            5461316-T Paciente Dos 02-09-2026 Fonasa 01.01.001 Consulta Medicina General $15.130 79.50% $12.028
            TOTAL SERVICIO : $32.606
            TOTAL LIQUIDACION : $32.606
            Nro. Liquidacion: 220081 // Participaciones // cant. cancelaciones : 2
            """;

        var result = SanatorioAlemanLiquidationPdfParser.ParseExtractedText(text);

        Assert.Equal("76389986-1", result.PayerRut);
        Assert.Equal("19091616-2", result.CollectorRut);
        Assert.Equal("220081", result.LiquidationNumber);
        Assert.Equal(new DateOnly(2026, 9, 22), result.LiquidationDate);
        Assert.Equal((short)2026, result.Year);
        Assert.Equal((short)9, result.Month);
        Assert.Equal((short)1, result.Fortnight);
        Assert.Equal("CONSULTAS MEDICAS", result.PaymentService);
        Assert.False(string.IsNullOrWhiteSpace(result.ExecutorName));
        Assert.Equal(32_606, result.GrossTotalClp);
        Assert.Equal(32_606, result.SumPayValuesClp);
        Assert.Equal(32_606, result.ServiceTotalClp);
        Assert.Equal(2, result.AttentionCount);
        Assert.Equal(2, result.ReportedAttentionCount);
    }

    [Fact]
    public void RejectsTextWithoutLiquidationTotals()
    {
        const string text = "LIQUIDACION POR PARTICIPACIONES\n76389986-1\nSin tabla ni total";

        Assert.Throws<InvalidDataException>(() => SanatorioAlemanLiquidationPdfParser.ParseExtractedText(text));
    }

    [Fact]
    public async Task ReadsPdfTextWithPdfPigBeforeApplyingTheTemplateParser()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var lines = new[]
        {
            "LIQUIDACION POR PARTICIPACIONES",
            "76389986-1",
            "SERVICIOS SANATORIO ALEMAN SPA",
            "Nro. Liquidacion: 220081",
            "Rut cobrador: 19091616-2",
            "Fecha Liquidacion: 22-09-2026",
            "Servicio de pago: CONSULTAS MEDICAS",
            "Periodo: 09-2026 / 1era QUINCENA",
            "Ejecutor: PROFESIONAL DE PRUEBA",
            "Servicio Nro Atencion Nombre Fecha Prevision Cod Prestacion Venta Factor Pago",
            "CONSULTAS MEDICAS PRESENCIAL",
            "5461247-T Paciente Uno 02-09-2026 Fonasa 01.01.001 Consulta $25.884 79.50% $20.578",
            "5461316-T Paciente Dos 02-09-2026 Fonasa 01.01.001 Consulta $15.130 79.50% $12.028",
            "TOTAL SERVICIO: $32.606",
            "TOTAL LIQUIDACION: $32.606",
            "cant. cancelaciones: 2"
        };
        var y = 800;
        foreach (var line in lines)
        {
            page.AddText(line, 9, new PdfPoint(24, y), font);
            y -= 18;
        }

        var bytes = builder.Build();
        await using var stream = new MemoryStream(bytes, writable: false);
        var result = await new SanatorioAlemanLiquidationPdfParser().ParseAsync(stream, CancellationToken.None);

        Assert.Equal("76389986-1", result.PayerRut);
        Assert.Equal("19091616-2", result.CollectorRut);
        Assert.Equal("220081", result.LiquidationNumber);
        Assert.False(string.IsNullOrWhiteSpace(result.ExecutorName));
        Assert.Equal(2, result.AttentionCount);
        Assert.Equal(32_606, result.GrossTotalClp);
        Assert.Equal(32_606, result.SumPayValuesClp);
    }
}
