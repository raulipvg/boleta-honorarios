namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class HourRecord
{
    private HourRecord() { }

    public Guid Id { get; private set; }
    public Guid PeriodInstitutionId { get; private set; }
    public int Hours { get; private set; }
    public int Order { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public HourRecord(Guid periodInstitutionId, int hours, int order)
    {
        if (hours < 1) throw new ArgumentOutOfRangeException(nameof(hours), "Las horas deben ser un entero mayor o igual a 1.");
        PeriodInstitutionId = periodInstitutionId;
        Hours = hours;
        Order = order;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeHours(int hours)
    {
        if (hours < 1) throw new ArgumentOutOfRangeException(nameof(hours), "Las horas deben ser un entero mayor o igual a 1.");
        Hours = hours;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
