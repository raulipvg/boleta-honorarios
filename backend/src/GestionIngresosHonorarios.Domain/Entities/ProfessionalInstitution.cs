namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class ProfessionalInstitution
{
    private ProfessionalInstitution() { }

    public Guid Id { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public Guid PublicInstitutionId { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ProfessionalInstitution(Guid professionalId, Guid publicInstitutionId)
    {
        if (professionalId == Guid.Empty || publicInstitutionId == Guid.Empty)
            throw new ArgumentException("La relación requiere profesional e institución.");
        ProfessionalId = professionalId;
        PublicInstitutionId = publicInstitutionId;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool active)
    {
        Active = active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
