using System.Text;

namespace GestionIngresosHonorarios.Domain.Services;

public static class InstitutionNameNormalizer
{
    public static string NormalizeDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre debe contener entre 1 y 200 caracteres.", nameof(name));
        if (name.Any(character => char.IsControl(character) && !char.IsWhiteSpace(character)))
            throw new ArgumentException("El nombre no puede contener caracteres de control.", nameof(name));

        var displayName = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (displayName.Length > 200)
            throw new ArgumentException("El nombre debe contener entre 1 y 200 caracteres.", nameof(name));
        if (!displayName.EnumerateRunes().Any(Rune.IsLetterOrDigit))
            throw new ArgumentException("El nombre debe incluir al menos una letra o un número.", nameof(name));

        return displayName;
    }
}
