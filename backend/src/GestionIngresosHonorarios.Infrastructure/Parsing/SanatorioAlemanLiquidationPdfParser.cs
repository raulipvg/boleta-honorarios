using System.Globalization;
using System.Text.RegularExpressions;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Application.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace GestionIngresosHonorarios.Infrastructure.Parsing;

public sealed class SanatorioAlemanLiquidationPdfParser : IPrivateLiquidationPdfParser
{
    private static readonly Regex AttentionNumberRegex = new(@"(?<!\d)\d{6,8}\s*-\s*T(?!\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RutRegex = new(@"(?<!\d)(?:\d{1,2}\.?\d{3}\.?\d{3}|\d{7,8})\s*-?\s*[0-9K](?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MoneyRegex = new(@"\$\s*([0-9][0-9.\s,]*)", RegexOptions.CultureInvariant);
    private static readonly Regex ServiceTotalRegex = new(@"TOTAL\s+SERVICIO\s*:?\s*\$?\s*([0-9][0-9.\s,]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public Task<ParsedPrivateLiquidation> ParseAsync(Stream pdf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var document = PdfDocument.Open(pdf);
            if (document.NumberOfPages < 1)
                throw new InvalidDataException("El PDF no contiene páginas procesables.");

            var pages = new List<string>();
            string? executorName = null;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(ContentOrderTextExtractor.GetText(page));
                executorName ??= ExtractExecutorFromPage(page);
            }

            var text = string.Join('\n', pages);
            if (string.IsNullOrWhiteSpace(text) || !text.Contains("LIQUID", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("No se encontró texto reconocible de una liquidación por participaciones.");

            return Task.FromResult(ParseExtractedText(text, executorName));
        }
        catch (InvalidDataException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException("PdfPig no pudo leer el archivo. Verifica que sea un PDF con texto extraíble.", exception);
        }
    }

    public static ParsedPrivateLiquidation ParseExtractedText(string text, string? executorName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = NormalizeLayout(text);

        var rutValues = RutRegex.Matches(normalized).Select(match => NormalizeRut(match.Value))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (rutValues.Length < 2)
            throw new InvalidDataException("No se encontraron los RUT del pagador y del cobrador.");
        var payerRut = rutValues[0];
        var collectorRut = ExtractCollectorRut(normalized, rutValues, payerRut);
        var liquidationNumber = ExtractFirst(normalized,
            @"NRO\.?\s*LIQUIDACI[OÓ]N\s*:?\s*(\d{3,})", "No se encontró el número de liquidación.");
        var liquidationDate = ParseLiquidationDate(normalized);

        var periodMatch = Regex.Match(normalized,
            @"(?<!\d)(\d{1,2})\s*[-/]\s*(\d{4})\s*/\s*(1\s*ERA|2\s*DA)\.?\s*QUINCENA",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!periodMatch.Success)
            throw new InvalidDataException("No se encontró el mes y la quincena del período de servicio.");

        var month = ParseShort(periodMatch.Groups[1].Value, "El mes del período no es válido.");
        var year = ParseShort(periodMatch.Groups[2].Value, "El año del período no es válido.");
        if (month is < 1 or > 12 || year < 1900)
            throw new InvalidDataException("El período de servicio está fuera de rango.");
        var fortnight = periodMatch.Groups[3].Value.StartsWith('1') ? (short)1 : (short)2;

        var paymentService = ExtractServiceBeforePeriod(normalized)
            ?? ExtractSection(normalized, @"SERVICIO\s+DE\s+PAGO", @"PER[IÍ]O?D[OÓ]");
        if (string.IsNullOrWhiteSpace(paymentService))
            throw new InvalidDataException("No se pudo identificar el servicio de pago.");
        executorName ??= ExtractSection(normalized,
            @"EJECUTOR", @"SERVICIO\s+NRO|NRO\s+ATENCI[OÓ]N|NOMBRE\s+PACIENTE|TOTAL\s+SERVICIO|TOTAL\s+LIQUIDACI[OÓ]N");
        var executor = CleanField(executorName ?? string.Empty);

        var liquidationMatches = Regex.Matches(normalized,
            @"TOTAL\s+LIQUIDACI[OÓ]N\s*:?\s*\$?\s*([0-9][0-9.\s,]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (liquidationMatches.Count == 0)
            throw new InvalidDataException("No se encontró TOTAL LIQUIDACIÓN.");
        var gross = ParseClp(liquidationMatches[^1].Groups[1].Value);

        var serviceTotals = ServiceTotalRegex.Matches(normalized)
            .Select(match => ParseClp(match.Groups[1].Value))
            .ToArray();
        long? serviceTotal = serviceTotals.Length == 0 ? null : serviceTotals.Aggregate(0L, (total, value) => checked(total + value));

        var attentionMatches = AttentionNumberRegex.Matches(normalized);
        if (attentionMatches.Count == 0)
            throw new InvalidDataException("No se encontraron filas con número de atención.");

        var detailEnd = liquidationMatches[^1].Index;
        var sumPayValues = SumAttentionPayValues(normalized, attentionMatches, detailEnd);
        var reportedAttentionCount = ExtractOptionalLong(normalized,
            @"CANT\.?\s*CANCELACIONES\s*:?\s*(\d+)");

        return new ParsedPrivateLiquidation(
            payerRut,
            collectorRut,
            liquidationNumber,
            liquidationDate,
            year,
            month,
            fortnight,
            CleanField(paymentService),
            CleanField(executor),
            serviceTotal,
            gross,
            attentionMatches.Count,
            reportedAttentionCount,
            sumPayValues);
    }

    private static long SumAttentionPayValues(string text, MatchCollection attentionMatches, int detailEnd)
    {
        long total = 0;
        for (var index = 0; index < attentionMatches.Count; index++)
        {
            var current = attentionMatches[index];
            if (current.Index >= detailEnd) break;
            var nextIndex = index + 1 < attentionMatches.Count ? attentionMatches[index + 1].Index : detailEnd;
            nextIndex = Math.Min(nextIndex, detailEnd);
            var row = text[current.Index..nextIndex];
            var subtotalIndex = Regex.Match(row, @"TOTAL\s+SERVICIO", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (subtotalIndex.Success) row = row[..subtotalIndex.Index];

            var amounts = MoneyRegex.Matches(row);
            if (amounts.Count == 0)
                throw new InvalidDataException("No se pudo leer el Valor Pago de una fila de atención.");

            // Valor Pago is the last CLP amount in each detail row. Factor Pago is a percentage and
            // Total Liquidación remains the authoritative gross; the factor is never applied again.
            total = checked(total + ParseClp(amounts[^1].Groups[1].Value));
        }

        return total;
    }

    private static string ExtractCollectorRut(string text, IReadOnlyList<string> rutValues, string payerRut)
    {
        var collectorLabel = Regex.Match(text, @"RUT\s*COBRADOR", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (collectorLabel.Success)
        {
            var match = RutRegex.Match(text[collectorLabel.Index..]);
            if (match.Success)
            {
                var labeledRut = NormalizeRut(match.Value);
                if (!string.Equals(labeledRut, payerRut, StringComparison.Ordinal)) return labeledRut;
            }
        }

        // The template places field labels and values in separate columns, so PdfPig's reading
        // order can list the two header RUTs without the adjacent label. Their visual/header order
        // is payer first, collector second.
        return rutValues.FirstOrDefault(rut => !string.Equals(rut, payerRut, StringComparison.Ordinal))
            ?? throw new InvalidDataException("No se encontró un RUT de cobrador distinto al del pagador.");
    }

    private static string NormalizeRut(string value)
    {
        try
        {
            return GestionIngresosHonorarios.Domain.Services.ChileanRut.NormalizeAndValidate(value);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Un RUT leído del PDF no es válido.", exception);
        }
    }

    private static string ExtractFirst(string text, string pattern, string error)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException(error);
        return match.Groups[1].Value.Trim();
    }

    private static string? ExtractSection(string text, string startPattern, string endPattern)
    {
        var start = Regex.Match(text, startPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!start.Success) return null;
        var valueStart = start.Index + start.Length;
        var tail = text[valueStart..];
        var end = Regex.Match(tail, endPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (end.Success) tail = tail[..end.Index];
        return tail.Trim(' ', ':', '\t', '\r', '\n');
    }

    private static DateOnly ParseDate(string value)
    {
        if (!DateOnly.TryParseExact(value, ["dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidDataException("La fecha de liquidación no tiene un formato válido.");
        return date;
    }

    private static DateOnly ParseLiquidationDate(string text)
    {
        var explicitDate = Regex.Match(text,
            @"FECHA\s+LIQUIDACI[OÓ]N\s*:?\s*(\d{1,2}[-/]\d{1,2}[-/]\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (explicitDate.Success) return ParseDate(explicitDate.Groups[1].Value);

        // In the supplied layout the label is positioned in a separate header column and may
        // precede the table's "Fecha Atención" heading in PdfPig's content order. The first date
        // in the document is the liquidation date; attention dates follow the detail rows.
        var firstDate = Regex.Match(text, @"(?<!\d)(\d{1,2}[-/]\d{1,2}[-/]\d{4})(?!\d)", RegexOptions.CultureInvariant);
        if (!firstDate.Success) throw new InvalidDataException("No se encontró la fecha de liquidación.");
        return ParseDate(firstDate.Groups[1].Value);
    }

    private static string? ExtractServiceBeforePeriod(string text)
    {
        var match = Regex.Match(text,
            @"PER[IÍ]O?D[OÓ]\s*:?\s*(.+?)\s+(\d{1,2}\s*[-/]\s*\d{4}\s*/)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var service = CleanField(match.Groups[1].Value);
        return service.Any(char.IsLetter) ? service : null;
    }

    private static string? ExtractExecutorFromPage(Page page)
    {
        var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        var label = words
            .Where(word => NormalizeWord(word.Text) == "EJECUTOR")
            .OrderByDescending(word => word.BoundingBox.Bottom)
            .FirstOrDefault();
        if (label is null) return null;

        var rowWords = words
            .Where(word => Math.Abs((double)(word.BoundingBox.Bottom - label.BoundingBox.Bottom)) <= 2.0
                && word.BoundingBox.Left >= label.BoundingBox.Right)
            .OrderBy(word => word.BoundingBox.Left)
            .Select(word => word.Text)
            .Where(value => NormalizeWord(value) != "COLON");
        var value = CleanField(string.Join(' ', rowWords).Replace(":", string.Empty, StringComparison.Ordinal));
        return value.Length == 0 ? null : value;
    }

    private static string NormalizeWord(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static short ParseShort(string value, string error) =>
        short.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException(error);

    private static long? ExtractOptionalLong(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static long ParseClp(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            || amount > GestionIngresosHonorarios.Domain.Services.IncomeCalculator.MaxExactInteger)
            throw new InvalidDataException("Un monto CLP del PDF está fuera de rango.");
        return amount;
    }

    private static string CleanField(string value) => Regex.Replace(value, @"\s+", " ").Trim();

    private static string NormalizeLayout(string text) => text
        .Replace('\u00A0', ' ')
        .Replace('\r', '\n')
        .Replace("\u0000", string.Empty);
}
