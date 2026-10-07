using GestionIngresosHonorarios.Domain.Services;

namespace GestionIngresosHonorarios.Domain.Entities;

public sealed class PrivateLiquidation
{
    private PrivateLiquidation() { }

    public Guid Id { get; private set; }
    public Guid PeriodId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public short ServiceYear { get; private set; }
    public short ServiceMonth { get; private set; }
    public short AccountingYear { get; private set; }
    public short AccountingMonth { get; private set; }
    public short? Fortnight { get; private set; }
    public Guid PrivateInstitutionId { get; private set; }
    public Guid PayerEntityId { get; private set; }
    public Guid PaymentRuleId { get; private set; }
    public PrivateLiquidationSourceType SourceType { get; private set; }
    public string? CollectorRut { get; private set; }
    public string? ReportedProfessionalName { get; private set; }
    public string? LiquidationNumber { get; private set; }
    public DateOnly? LiquidationDate { get; private set; }
    public string PaymentService { get; private set; } = string.Empty;
    public string? ExecutorName { get; private set; }
    public long? ServiceTotalClp { get; private set; }
    public long GrossTotalClp { get; private set; }
    public long RetentionTotalClp { get; private set; }
    public long NetTotalClp { get; private set; }
    public long AttentionCount { get; private set; }
    public long? ReportedAttentionCount { get; private set; }
    public int MinutesPerAttention { get; private set; }
    public long TotalAttentionMinutes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string? StorageKey { get; private set; }
    public string? OriginalFileName { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string? SourceBody { get; private set; }
    public string? BusinessKey { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }

    public PrivateLiquidation(
        Guid periodId,
        Guid professionalId,
        short serviceYear,
        short serviceMonth,
        short accountingYear,
        short accountingMonth,
        short? fortnight,
        Guid privateInstitutionId,
        Guid payerEntityId,
        Guid paymentRuleId,
        string? collectorRut,
        string? reportedProfessionalName,
        string? liquidationNumber,
        DateOnly? liquidationDate,
        string paymentService,
        string? executorName,
        long? serviceTotalClp,
        long grossTotalClp,
        long retentionTotalClp,
        long netTotalClp,
        long attentionCount,
        long? reportedAttentionCount,
        int minutesPerAttention,
        string sha256,
        string? storageKey,
        string? originalFileName,
        long? fileSizeBytes,
        PrivateLiquidationSourceType sourceType,
        string? sourceBody,
        string? businessKey)
    {
        if (periodId == Guid.Empty || professionalId == Guid.Empty || privateInstitutionId == Guid.Empty
            || payerEntityId == Guid.Empty || paymentRuleId == Guid.Empty)
            throw new ArgumentException("La liquidación debe estar asociada a período, profesional, institución, pagador y regla.");
        if (serviceYear < 1900) throw new ArgumentOutOfRangeException(nameof(serviceYear));
        if (serviceMonth is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(serviceMonth));
        if (accountingYear < 1900) throw new ArgumentOutOfRangeException(nameof(accountingYear));
        if (accountingMonth is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(accountingMonth));
        if (!Enum.IsDefined(sourceType)) throw new ArgumentOutOfRangeException(nameof(sourceType));
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
        if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("El hash SHA-256 no es válido.", nameof(sha256));

        switch (sourceType)
        {
            case PrivateLiquidationSourceType.Pdf:
                if (fortnight is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(fortnight));
                if (string.IsNullOrWhiteSpace(liquidationNumber) || liquidationNumber.Trim().Length > 50)
                    throw new ArgumentException("El número de liquidación es obligatorio.", nameof(liquidationNumber));
                if (liquidationDate is null || liquidationDate.Value.Year < 1900)
                    throw new ArgumentOutOfRangeException(nameof(liquidationDate));
                if (string.IsNullOrWhiteSpace(collectorRut))
                    throw new ArgumentException("El RUT del cobrador es obligatorio para una liquidación PDF.", nameof(collectorRut));
                if (fileSizeBytes is < 1 or > 1_048_576)
                    throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "El PDF no puede superar 1 MB.");
                if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey
                    || !string.Equals(Path.GetExtension(storageKey), ".pdf", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("La referencia del PDF no es válida.", nameof(storageKey));
                if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 255)
                    throw new ArgumentException("El nombre del archivo no es válido.", nameof(originalFileName));
                if (sourceBody is not null || businessKey is not null || reportedProfessionalName is not null)
                    throw new ArgumentException("Una liquidación PDF no admite campos de origen de correo.", nameof(sourceType));
                break;

            case PrivateLiquidationSourceType.EmailBody:
                if (fortnight is not null || !string.IsNullOrWhiteSpace(liquidationNumber) || liquidationDate is not null
                    || collectorRut is not null || !string.IsNullOrWhiteSpace(executorName)
                    || serviceTotalClp is not null || reportedAttentionCount is not null
                    || storageKey is not null || originalFileName is not null || fileSizeBytes is not null)
                    throw new ArgumentException("El correo de Cebien no puede completar campos que la fuente no informa.", nameof(sourceType));
                if (string.IsNullOrWhiteSpace(reportedProfessionalName) || reportedProfessionalName.Trim().Length > 200)
                    throw new ArgumentException("El nombre de profesional del correo es obligatorio.", nameof(reportedProfessionalName));
                if (string.IsNullOrWhiteSpace(sourceBody))
                    throw new ArgumentException("El cuerpo original del correo es obligatorio.", nameof(sourceBody));
                if (string.IsNullOrWhiteSpace(businessKey) || businessKey.Length != 64 || !businessKey.All(Uri.IsHexDigit))
                    throw new ArgumentException("La clave de negocio del correo no es válida.", nameof(businessKey));
                break;
        }

        PeriodId = periodId;
        ProfessionalId = professionalId;
        ServiceYear = serviceYear;
        ServiceMonth = serviceMonth;
        AccountingYear = accountingYear;
        AccountingMonth = accountingMonth;
        Fortnight = fortnight;
        PrivateInstitutionId = privateInstitutionId;
        PayerEntityId = payerEntityId;
        PaymentRuleId = paymentRuleId;
        SourceType = sourceType;
        CollectorRut = collectorRut is null ? null : ChileanRut.NormalizeAndValidate(collectorRut);
        ReportedProfessionalName = reportedProfessionalName?.Trim();
        LiquidationNumber = liquidationNumber?.Trim();
        LiquidationDate = liquidationDate;
        PaymentService = paymentService.Trim();
        ExecutorName = executorName?.Trim();
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
        OriginalFileName = originalFileName is null ? null : Path.GetFileName(originalFileName);
        FileSizeBytes = fileSizeBytes;
        SourceBody = sourceBody;
        BusinessKey = businessKey?.ToUpperInvariant();
        ImportedAt = DateTimeOffset.UtcNow;
    }
}
