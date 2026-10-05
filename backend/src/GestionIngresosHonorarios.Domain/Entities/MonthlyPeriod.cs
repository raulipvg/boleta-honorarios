namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class MonthlyPeriod
{
    private MonthlyPeriod() { }

    public Guid Id { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public short Year { get; private set; }
    public short Month { get; private set; }
    public decimal AppliedRetentionPercentage { get; private set; }
    public long TotalHours { get; private set; }
    public long GrossTotalClp { get; private set; }
    public long RetentionTotalClp { get; private set; }
    public long NetTotalClp { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<PeriodInstitution> Institutions { get; private set; } = new List<PeriodInstitution>();

    public MonthlyPeriod(Guid professionalId, short year, short month, decimal retentionPercentage)
    {
        if (professionalId == Guid.Empty) throw new ArgumentException("El profesional es obligatorio.", nameof(professionalId));
        if (year < 1900) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (retentionPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(retentionPercentage));
        ProfessionalId = professionalId;
        Year = year;
        Month = month;
        AppliedRetentionPercentage = retentionPercentage;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTotals(long hours, long gross, long retention, long net)
    {
        if (hours < 0 || gross < 0 || retention < 0 || net < 0 || retention > gross || net != gross - retention)
            throw new InvalidOperationException("Los totales del período no son consistentes.");
        if (hours > Services.IncomeCalculator.MaxExactInteger || gross > Services.IncomeCalculator.MaxExactInteger
            || retention > Services.IncomeCalculator.MaxExactInteger || net > Services.IncomeCalculator.MaxExactInteger)
            throw new OverflowException("Los totales exceden el rango entero exacto permitido para CLP.");
        TotalHours = hours;
        GrossTotalClp = gross;
        RetentionTotalClp = retention;
        NetTotalClp = net;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
