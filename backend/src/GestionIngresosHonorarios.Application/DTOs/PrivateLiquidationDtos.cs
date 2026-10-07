namespace GestionIngresosHonorarios.Application.DTOs;

public sealed record ParsedPrivateLiquidation(
    string PayerRut,
    string CollectorRut,
    string LiquidationNumber,
    DateOnly LiquidationDate,
    short ServiceYear,
    short ServiceMonth,
    short Fortnight,
    string PaymentService,
    string ExecutorName,
    long? ServiceTotalClp,
    long GrossTotalClp,
    long AttentionCount,
    long? ReportedAttentionCount,
    long SumPayValuesClp);

public sealed record CebienAttentionCount(string ServiceName, long Count);

public sealed record ParsedCebienEmail(
    string ProfessionalName,
    short ServiceYear,
    short ServiceMonth,
    IReadOnlyList<CebienAttentionCount> AttentionCountsByService,
    long GrossTotalClp,
    decimal ReportedRetentionPercentage,
    long ReportedRetentionClp,
    long ReportedNetTotalClp)
{
    public long AttentionCount => AttentionCountsByService.Aggregate(0L, (total, line) => checked(total + line.Count));
    public string PaymentService => string.Join(", ", AttentionCountsByService.Select(line => line.ServiceName));
}

public sealed record PrivateLiquidationFile(Stream Content, string FileName);
