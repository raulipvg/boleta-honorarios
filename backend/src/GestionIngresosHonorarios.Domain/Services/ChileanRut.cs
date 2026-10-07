namespace GestionIngresosHonorarios.Domain.Services;

public static class ChileanRut
{
    public static string NormalizeAndValidate(string rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
            throw new ArgumentException("El RUT es obligatorio.", nameof(rut));

        var compact = new string(rut.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (compact.Length < 2 || compact.Length > 9)
            throw new ArgumentException("El RUT no tiene un formato válido.", nameof(rut));

        var body = compact[..^1];
        if (!body.All(char.IsDigit) || !int.TryParse(body, out _))
            throw new ArgumentException("El RUT no tiene un formato válido.", nameof(rut));

        var verifier = compact[^1];
        if (verifier != CalculateVerifier(body))
            throw new ArgumentException("El dígito verificador del RUT no es válido.", nameof(rut));

        return $"{body}-{verifier}";
    }

    public static bool TryNormalize(string? rut, out string normalized)
    {
        try
        {
            normalized = NormalizeAndValidate(rut ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static char CalculateVerifier(string body)
    {
        var sum = 0;
        var multiplier = 2;
        for (var index = body.Length - 1; index >= 0; index--)
        {
            sum += (body[index] - '0') * multiplier;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        return (11 - sum % 11) switch
        {
            11 => '0',
            10 => 'K',
            var digit => (char)('0' + digit)
        };
    }
}
