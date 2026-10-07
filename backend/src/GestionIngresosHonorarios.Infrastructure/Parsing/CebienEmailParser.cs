using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;

namespace GestionIngresosHonorarios.Infrastructure.Parsing;

public sealed class CebienEmailParser : ICebienEmailParser
{
    private static readonly Regex ProfessionalRegex = new(
        @"^PROFESIONAL\s*:?\s*(?<name>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ServicePeriodRegex = new(
        @"MES\s+DE\s+ATENCI[OÓ]N\s*:?\s*MES\s+(?<month>[\p{L}]+)\s+(?<year>\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ServiceCountRegex = new(
        @"^(?<service>.+?)\s+(?<count>\d+)\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex RateRegex = new(
        @"(?<rate>\d+(?:[.,]\d{1,4})?)\s*%", RegexOptions.CultureInvariant);
    private static readonly Regex AmountRegex = new(
        @"(?<!\d)(?<amount>\d[\d.,\s]*\d|\d)(?!\d)", RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, short> MonthNumbers = new Dictionary<string, short>(StringComparer.Ordinal)
    {
        ["ENERO"] = 1,
        ["FEBRERO"] = 2,
        ["MARZO"] = 3,
        ["ABRIL"] = 4,
        ["MAYO"] = 5,
        ["JUNIO"] = 6,
        ["JULIO"] = 7,
        ["AGOSTO"] = 8,
        ["SEPTIEMBRE"] = 9,
        ["SETIEMBRE"] = 9,
        ["OCTUBRE"] = 10,
        ["NOVIEMBRE"] = 11,
        ["DICIEMBRE"] = 12
    };

    public ParsedCebienEmail Parse(string emailBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailBody);
        var lines = emailBody.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Replace(line.Trim(), @"\s+", " ", RegexOptions.CultureInvariant))
            .Where(line => line.Length > 0)
            .ToArray();
        var normalizedText = NormalizeForMatching(string.Join('\n', lines));

        if (!normalizedText.Contains("CENTRO CEBIEN", StringComparison.Ordinal))
            throw new InvalidDataException("El correo no corresponde al formato conocido de Centro Cebien.");

        var professionalLine = lines
            .Select(line => ProfessionalRegex.Match(line))
            .FirstOrDefault(match => match.Success);
        var professionalName = professionalLine?.Groups["name"].Value.Trim();
        if (string.IsNullOrWhiteSpace(professionalName))
            throw new InvalidDataException("No se encontró el nombre del profesional en el correo.");

        var periodLine = lines
            .Select(line => ServicePeriodRegex.Match(line))
            .FirstOrDefault(match => match.Success);
        if (periodLine is null || !periodLine.Success)
            throw new InvalidDataException("No se encontró MES DE ATENCION y su año en el correo.");

        var normalizedMonth = NormalizeForMatching(periodLine.Groups["month"].Value);
        if (!MonthNumbers.TryGetValue(normalizedMonth, out var serviceMonth))
            throw new InvalidDataException("El mes de atención del correo no es válido.");
        if (!short.TryParse(periodLine.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var serviceYear)
            || serviceYear < 1900)
            throw new InvalidDataException("El año de atención del correo no es válido.");

        var tableStart = Array.FindIndex(lines, line => NormalizeForMatching(line).Contains("TIPO DE PRESTACION", StringComparison.Ordinal));
        var breakdownStart = Array.FindIndex(lines, line => NormalizeForMatching(line).Contains("DESGLOSE BOLETA DE HONORARIOS", StringComparison.Ordinal));
        if (tableStart < 0 || breakdownStart <= tableStart)
            throw new InvalidDataException("No se encontró la tabla de prestaciones del correo.");

        var serviceCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var index = tableStart + 1; index < breakdownStart; index++)
        {
            var match = ServiceCountRegex.Match(lines[index]);
            if (!match.Success) continue;
            if (!long.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                || count > 9_007_199_254_740_991)
                throw new InvalidDataException("La cantidad de una prestación está fuera de rango.");
            if (count == 0) continue;

            var serviceName = match.Groups["service"].Value.Trim();
            if (serviceCounts.TryGetValue(serviceName, out var existing))
                serviceCounts[serviceName] = checked(existing + count);
            else
                serviceCounts.Add(serviceName, count);
        }

        if (serviceCounts.Count == 0)
            throw new InvalidDataException("No se encontraron cantidades de atenciones en el correo.");

        var (gross, reportedRate, reportedRetention, reportedNet) = ParseReportedAmounts(lines, breakdownStart);
        var countsByService = serviceCounts.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new CebienAttentionCount(pair.Key, pair.Value))
            .ToArray();

        return new ParsedCebienEmail(
            professionalName,
            serviceYear,
            serviceMonth,
            countsByService,
            gross,
            reportedRate,
            reportedRetention,
            reportedNet);
    }

    private static (long Gross, decimal Rate, long Retention, long Net) ParseReportedAmounts(
        IReadOnlyList<string> lines, int breakdownStart)
    {
        long? gross = null;
        long? retention = null;
        long? net = null;
        decimal? rate = null;

        for (var index = breakdownStart + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            var rateMatch = RateRegex.Match(line);
            if (rateMatch.Success)
            {
                var rateText = rateMatch.Groups["rate"].Value.Replace(',', '.');
                if (!decimal.TryParse(rateText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedRate)
                    || parsedRate is < 0 or > 100)
                    throw new InvalidDataException("La tasa de retención del correo no es válida.");
                rate = parsedRate;

                var amountText = line[(rateMatch.Index + rateMatch.Length)..];
                var amountMatch = AmountRegex.Match(amountText);
                if (amountMatch.Success) retention = ParseClp(amountMatch.Groups["amount"].Value);
                continue;
            }

            // The period label may repeat after "Atenciones efectuadas mes"; it is not a CLP amount.
            if (line.Any(char.IsLetter)) continue;
            var clpMatch = AmountRegex.Match(line);
            if (!clpMatch.Success) continue;
            var amount = ParseClp(clpMatch.Groups["amount"].Value);
            if (gross is null) gross = amount;
            else if (net is null) net = amount;
        }

        if (gross is null || rate is null || retention is null || net is null)
            throw new InvalidDataException("No se pudo leer el bruto, la tasa, la retención y el líquido del desglose de boleta.");
        return (gross.Value, rate.Value, retention.Value, net.Value);
    }

    private static long ParseClp(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || !long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            || amount > 9_007_199_254_740_991)
            throw new InvalidDataException("Un importe del correo no es un CLP entero válido.");
        return amount;
    }

    private static string NormalizeForMatching(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(withoutAccents, @"\s+", " ", RegexOptions.CultureInvariant).Trim().ToUpperInvariant();
    }
}
