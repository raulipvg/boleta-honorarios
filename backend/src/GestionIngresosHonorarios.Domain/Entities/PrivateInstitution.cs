namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PrivateInstitution
{
    private PrivateInstitution() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool Active { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public PrivateInstitution(string name)
    {
        Rename(name);
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("El nombre debe contener entre 1 y 200 caracteres.", nameof(name));
        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
