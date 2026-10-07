using GestionIngresosHonorarios.Domain.Services;

namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PrivateLiquidation
{
    private PrivateLiquidation() { }

    public Guid Id { get; private set; }
    public Guid PeriodId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public short Year { get; private set; }
    public short Month { get; private set; }
    public short Fortnight { get; private set; }
    public Guid PrivateInstitutionId { get; private set; }
    public Guid PayerEntityId { get; private set; }
    public Guid PaymentRuleId { get; private set; }
    public string CollectorRut { get; private set; } = string.Empty;
    public string LiquidationNumber { get; private set; } = string.Empty;
    public DateOnly LiquidationDate { get; private set; }
    public string PaymentService { get; private set; } = string.Empty;
    public string ExecutorName { get; private set; } = string.Empty;
    public long? ServiceTotalClp { get; private set; }
    public long GrossTotalClp { get; private set; }
    public long RetentionTotalClp { get; private set; }
    public long NetTotalClp { get; private set; }
    public long AttentionCount { get; private set; }
    public long? ReportedAttentionCount { get; private set; }
    public int MinutesPerAttention { get; private set; }
    public long TotalAttentionMinutes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }

    public PrivateLiquidation(
        Guid periodId,
        Guid professionalId,
        short year,
        short month,
        short fortnight,
        Guid privateInstitutionId,
        Guid payerEntityId,
        Guid paymentRuleId,
        string collectorRut,
        string liquidationNumber,
        DateOnly liquidationDate,
        string paymentService,
        string executorName,
        long? serviceTotalClp,
        long grossTotalClp,
        long retentionTotalClp,
        long netTotalClp,
        long attentionCount,
        long? reportedAttentionCount,
        int minutesPerAttention,
        string sha256,
        string storageKey,
        string originalFileName,
        long fileSizeBytes)
    {
        if (periodId == Guid.Empty || professionalId == Guid.Empty || privateInstitutionId == Guid.Empty
            || payerEntityId == Guid.Empty || paymentRuleId == Guid.Empty)
            throw new ArgumentException("La liquidación debe estar asociada a período, profesional, institución, pagador y regla.");
        if (year < 1900) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        if (fortnight is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(fortnight));
        if (string.IsNullOrWhiteSpace(liquidationNumber) || liquidationNumber.Trim().Length > 50)
            throw new ArgumentException("El número de liquidación es obligatorio.", nameof(liquidationNumber));
        if (string.IsNullOrWhiteSpace(paymentService) || paymentService.Trim().Length > 200)
            throw new ArgumentException("El servicio de pago es obligatorio.", nameof(paymentService));
        if ((executorName?.Trim().Length ?? 0) > 200) throw new ArgumentOutOfRangeException(nameof(executorName));
        if (grossTotalClp is < 0 or > IncomeCalculator.MaxExactInteger
            || retentionTotalClp is < 0 or > IncomeCalculator.MaxExactInteger
            || netTotalClp is < 0 or > IncomeCalculator.MaxExactInteger
            || serviceTotalClp is < 0 or > IncomeCalculator.MaxExactInteger)
            throw new ArgumentOutOfRangeException(nameof(grossTotalClp), "Los importes deben ser CLP enteros exactos no negativos.");
        if (retentionTotalClp > grossTotalClp || netTotalClp != grossTotalClp - retentionTotalClp)
            throw new InvalidOperationException("Los importes de la liquidación no son consistentes.");
        if (attentionCount < 1 || attentionCount > IncomeCalculator.MaxExactInteger)
            throw new ArgumentOutOfRangeException(nameof(attentionCount));
        if (reportedAttentionCount is < 0 or > IncomeCalculator.MaxExactInteger)
            throw new ArgumentOutOfRangeException(nameof(reportedAttentionCount));
        if (minutesPerAttention <= 0)
            throw new ArgumentOutOfRangeException(nameof(minutesPerAttention), "Los minutos por atención deben ser mayores que cero.");
        if (fileSizeBytes is < 1 or > 1_048_576)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "El PDF no puede superar 1 MB.");
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("El hash SHA-256 no es válido.", nameof(sha256));
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey)
            throw new ArgumentException("La referencia del archivo no es válida.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 255)
            throw new ArgumentException("El nombre del archivo no es válido.", nameof(originalFileName));

        PeriodId = periodId;
        ProfessionalId = professionalId;
        Year = year;
        Month = month;
        Fortnight = fortnight;
        PrivateInstitutionId = privateInstitutionId;
        PayerEntityId = payerEntityId;
        PaymentRuleId = paymentRuleId;
        CollectorRut = ChileanRut.NormalizeAndValidate(collectorRut);
        LiquidationNumber = liquidationNumber.Trim();
        LiquidationDate = liquidationDate;
        PaymentService = paymentService.Trim();
        ExecutorName = executorName?.Trim() ?? string.Empty;
        ServiceTotalClp = serviceTotalClp;
        GrossTotalClp = grossTotalClp;
        RetentionTotalClp = retentionTotalClp;
        NetTotalClp = netTotalClp;
        AttentionCount = attentionCount;
        ReportedAttentionCount = reportedAttentionCount;
        MinutesPerAttention = minutesPerAttention;
        TotalAttentionMinutes = checked(AttentionCount * MinutesPerAttention);
        if (TotalAttentionMinutes > IncomeCalculator.MaxExactInteger)
            throw new OverflowException("Los minutos totales exceden el rango entero exacto permitido.");
        Sha256 = sha256.ToUpperInvariant();
        StorageKey = storageKey;
        OriginalFileName = Path.GetFileName(originalFileName);
        FileSizeBytes = fileSizeBytes;
        ImportedAt = DateTimeOffset.UtcNow;
    }
}
