namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class AnnualHourlyRate
{
    private AnnualHourlyRate() { }

    public Guid ProfessionalInstitutionId { get; private set; }
    public short Year { get; private set; }
    public int Version { get; private set; }
    public long HourlyRateClp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public AnnualHourlyRate(Guid professionalInstitutionId, short year, int version, long hourlyRateClp)
    {
        if (professionalInstitutionId == Guid.Empty) throw new ArgumentException("La relación es obligatoria.", nameof(professionalInstitutionId));
        if (year < 1900) throw new ArgumentOutOfRangeException(nameof(year));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (hourlyRateClp is < 0 or > Services.IncomeCalculator.MaxExactInteger)
            throw new ArgumentOutOfRangeException(nameof(hourlyRateClp), "La tarifa debe ser un CLP entero representable exactamente por el cliente.");
        ProfessionalInstitutionId = professionalInstitutionId;
        Year = year;
        Version = version;
        HourlyRateClp = hourlyRateClp;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
