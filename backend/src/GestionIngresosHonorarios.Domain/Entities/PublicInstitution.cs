using GestionIngresosHonorarios.Domain.Services;

namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PublicInstitution
{
    private PublicInstitution() { }

    public PublicInstitution(string name)
    {
        Rename(name);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool Active { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Rename(string name)
    {
        Name = InstitutionNameNormalizer.NormalizeDisplayName(name);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool active)
    {
        Active = active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
