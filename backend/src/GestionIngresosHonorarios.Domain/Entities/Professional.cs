namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class Professional
{
    private Professional() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Professional(Guid userId, string name)
    {
        if (userId == Guid.Empty) throw new ArgumentException("El usuario es obligatorio.", nameof(userId));
        UserId = userId;
        Rename(name);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("El nombre debe contener entre 1 y 200 caracteres.", nameof(name));
        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
