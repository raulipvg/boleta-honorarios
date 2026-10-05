namespace GestionIngresosHonorarios.Application.DTOs;

public sealed record InstitutionDto(Guid Id, string Name, bool Active);
public sealed record RetentionRateDto(short Year, decimal Percentage);
public sealed record ProfessionalSummaryDto(Guid Id, string Name);
public sealed record ProfessionalProfileDto(Guid Id, Guid UserId, string Name);
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
public sealed record DashboardInstitutionDto(Guid Id, Guid ProfessionalInstitutionId, string Name);
public sealed record DashboardInstitutionValueDto(Guid InstitutionId, long? NetTotalClp);
public sealed record DashboardMonthDto(short Year, short Month, bool PeriodExists, long? TotalNetClp, IReadOnlyList<DashboardInstitutionValueDto> Institutions);
public sealed record DashboardDto(Guid ProfessionalId, short FromYear, short ToYear, IReadOnlyList<DashboardInstitutionDto> Institutions, IReadOnlyList<DashboardMonthDto> Months);
