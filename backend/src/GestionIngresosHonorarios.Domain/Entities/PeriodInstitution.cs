namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PeriodInstitution
{
    private PeriodInstitution() { }

    public Guid Id { get; private set; }
    public Guid PeriodId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public short Year { get; private set; }
    public Guid ProfessionalInstitutionId { get; private set; }
    public int HourlyRateVersion { get; private set; }
    public long TotalHours { get; private set; }
    public long GrossTotalClp { get; private set; }
    public long RetentionTotalClp { get; private set; }
    public long NetTotalClp { get; private set; }
    public int Order { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<HourRecord> HourRecords { get; private set; } = new List<HourRecord>();

    public PeriodInstitution(Guid periodId, Guid professionalId, short year, Guid professionalInstitutionId, int hourlyRateVersion, int order)
    {
        PeriodId = periodId;
        ProfessionalId = professionalId;
        Year = year;
        ProfessionalInstitutionId = professionalInstitutionId;
        HourlyRateVersion = hourlyRateVersion;
        Order = order;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTotals(long hours, long gross, long retention, long net)
    {
        if (hours < 0 || gross < 0 || retention < 0 || net < 0 || retention > gross || net != gross - retention)
            throw new InvalidOperationException("Los totales institucionales no son consistentes.");
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
