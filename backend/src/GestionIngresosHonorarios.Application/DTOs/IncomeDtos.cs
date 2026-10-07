namespace GestionIngresosHonorarios.Application.DTOs;

public sealed record InstitutionDto(Guid Id, string Name, bool Active);
public sealed record RetentionRateDto(short Year, decimal Percentage);
public sealed record ProfessionalSummaryDto(Guid Id, string Name);
public sealed record ProfessionalProfileDto(Guid Id, Guid UserId, string Name, string? Rut);
public sealed record HourlyRateDto(short Year, int Version, long HourlyRateClp, DateTimeOffset CreatedAt);
public sealed record ProfessionalInstitutionDto(Guid Id, Guid PublicInstitutionId, string InstitutionName, bool Active, IReadOnlyList<HourlyRateDto> Rates);
public sealed record HourRecordDto(Guid Id, int Hours, int Order, long Version);
public sealed record SavedHourRecordDto(HourRecordDto Record, MonthlyWorkspaceDto Workspace);
public sealed record PeriodInstitutionDto(
    Guid ProfessionalInstitutionId,
    string InstitutionName,
    long HourlyRateClp,
    int HourlyRateVersion,
    long TotalHours,
    long GrossTotalClp,
    long RetentionTotalClp,
    long NetTotalClp,
    long Version,
    long RecordsCount,
    bool HasMoreRecords,
    int? NextBeforeOrder,
    IReadOnlyList<HourRecordDto> Records);
public sealed record HourRecordsPageDto(IReadOnlyList<HourRecordDto> Records, bool HasMoreRecords, int? NextBeforeOrder);
public sealed record MonthlyWorkspaceDto(
    Guid? PeriodId,
    Guid ProfessionalId,
    short Year,
    short Month,
    bool Exists,
    decimal? AppliedRetentionPercentage,
    long TotalHours,
    long GrossTotalClp,
    long RetentionTotalClp,
    long NetTotalClp,
    long Version,
    IReadOnlyList<PeriodInstitutionDto> Institutions);
public sealed record PrivateLiquidationPreviewDto(
    string Sha256,
    string SourceType,
    string PrivateInstitutionName,
    Guid PayerEntityId,
    string PayerLegalName,
    string PayerRut,
    string? CollectorRut,
    string? ReportedProfessionalName,
    string? LiquidationNumber,
    DateOnly? LiquidationDate,
    short ServiceYear,
    short ServiceMonth,
    short AccountingYear,
    short AccountingMonth,
    short? Fortnight,
    string PaymentService,
    string? ExecutorName,
    IReadOnlyList<CebienAttentionCount> AttentionCountsByService,
    long? ServiceTotalClp,
    long GrossTotalClp,
    decimal AppliedRetentionPercentage,
    long RetentionTotalClp,
    long NetTotalClp,
    long AttentionCount,
    long? ReportedAttentionCount,
    int MinutesPerAttention,
    long TotalAttentionMinutes,
    long? FileSizeBytes,
    string? OriginalFileName);

public sealed record PrivateLiquidationDto(
    Guid Id,
    string PrivateInstitutionName,
    Guid PayerEntityId,
    string PayerLegalName,
    string PayerRut,
    string SourceType,
    string? CollectorRut,
    string? ReportedProfessionalName,
    string? LiquidationNumber,
    DateOnly? LiquidationDate,
    short ServiceYear,
    short ServiceMonth,
    short AccountingYear,
    short AccountingMonth,
    short? Fortnight,
    string PaymentService,
    string? ExecutorName,
    long? ServiceTotalClp,
    decimal AppliedRetentionPercentage,
    long GrossTotalClp,
    long RetentionTotalClp,
    long NetTotalClp,
    long AttentionCount,
    long? ReportedAttentionCount,
    int MinutesPerAttention,
    long TotalAttentionMinutes,
    DateTimeOffset ImportedAt);

public sealed record PrivateLiquidationListItemDto(
    Guid Id,
    string PrivateInstitutionName,
    Guid PayerEntityId,
    string PayerLegalName,
    string PayerRut,
    string SourceType,
    string? ReportedProfessionalName,
    string? LiquidationNumber,
    DateOnly? LiquidationDate,
    short ServiceYear,
    short ServiceMonth,
    short AccountingYear,
    short AccountingMonth,
    string PaymentService,
    decimal AppliedRetentionPercentage,
    long GrossTotalClp,
    long RetentionTotalClp,
    long NetTotalClp,
    long AttentionCount,
    int MinutesPerAttention,
    long TotalAttentionMinutes,
    DateTimeOffset ImportedAt);

public sealed record CebienEmailPreviewRequest(
    string EmailBody,
    short AccountingYear,
    short AccountingMonth,
    int MinutesPerAttention);

public sealed record CebienEmailImportRequest(
    string EmailBody,
    short AccountingYear,
    short AccountingMonth,
    int MinutesPerAttention,
    string ExpectedSha256,
    decimal ExpectedRetentionPercentage);

public sealed record DashboardInstitutionDto(string Key, string Name, string Type);
public sealed record DashboardInstitutionValueDto(string InstitutionKey, long? NetTotalClp);
public sealed record DashboardMonthDto(
    short Year,
    short Month,
    bool PeriodExists,
    long? TotalNetClp,
    IReadOnlyList<DashboardInstitutionValueDto> Institutions);
public sealed record DashboardDto(Guid ProfessionalId, short FromYear, short ToYear, IReadOnlyList<DashboardInstitutionDto> Institutions, IReadOnlyList<DashboardMonthDto> Months);
