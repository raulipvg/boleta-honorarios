using GestionIngresosHonorarios.Domain.Services;

namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PrivatePayerEntity
{
    private PrivatePayerEntity() { }

    public Guid Id { get; private set; }
    public Guid PrivateInstitutionId { get; private set; }
    public string Rut { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public bool Active { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public PrivatePayerEntity(Guid privateInstitutionId, string rut, string legalName)
    {
        if (privateInstitutionId == Guid.Empty)
            throw new ArgumentException("La institución privada es obligatoria.", nameof(privateInstitutionId));
        if (string.IsNullOrWhiteSpace(legalName) || legalName.Trim().Length > 200)
            throw new ArgumentException("La razón social debe contener entre 1 y 200 caracteres.", nameof(legalName));

        PrivateInstitutionId = privateInstitutionId;
        Rut = ChileanRut.NormalizeAndValidate(rut);
        LegalName = legalName.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
