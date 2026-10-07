namespace GestionIngresosHonorarios.Application.DTOs;

public sealed record ParsedPrivateLiquidation(
    string PayerRut,
    string CollectorRut,
    string LiquidationNumber,
    DateOnly LiquidationDate,
    short Year,
    short Month,
    short Fortnight,
    string PaymentService,
    string ExecutorName,
    long? ServiceTotalClp,
    long GrossTotalClp,
    long AttentionCount,
    long? ReportedAttentionCount,
    long SumPayValuesClp);

public sealed record PrivateLiquidationFile(Stream Content, string FileName);
