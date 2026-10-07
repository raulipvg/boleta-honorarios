namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PrivatePaymentRule
{
    private PrivatePaymentRule() { }

    public Guid Id { get; private set; }
    public Guid PrivateInstitutionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public PrivatePaymentRule(Guid privateInstitutionId, string code, int version)
    {
        if (privateInstitutionId == Guid.Empty)
            throw new ArgumentException("La institución privada es obligatoria.", nameof(privateInstitutionId));
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 100)
            throw new ArgumentException("El código de regla debe contener entre 1 y 100 caracteres.", nameof(code));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));

        PrivateInstitutionId = privateInstitutionId;
        Code = code.Trim();
        Version = version;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
