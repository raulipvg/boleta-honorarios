using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.DTOs;

namespace GestionIngresosHonorarios.Application.Contracts;

public interface IIncomeApplicationService
{
    Task<IReadOnlyList<InstitutionDto>> GetInstitutionsAsync(ActorContext actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<RetentionRateDto>> GetRetentionRatesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfessionalSummaryDto>> GetProfessionalsAsync(CancellationToken cancellationToken);
    Task<InstitutionDto> CreateInstitutionAsync(string name, CancellationToken cancellationToken);
    Task<InstitutionDto> UpdateInstitutionAsync(Guid institutionId, string name, bool active, CancellationToken cancellationToken);
    Task<ProfessionalProfileDto> GetProfileAsync(ActorContext actor, CancellationToken cancellationToken);
    Task<ProfessionalProfileDto> UpdateProfileAsync(ActorContext actor, string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfessionalInstitutionDto>> GetProfessionalInstitutionsAsync(ActorContext actor, Guid? professionalId, CancellationToken cancellationToken);
    Task<ProfessionalInstitutionDto> AddProfessionalInstitutionAsync(ActorContext actor, Guid institutionId, CancellationToken cancellationToken);
    Task<ProfessionalInstitutionDto> CreateAndAddProfessionalInstitutionAsync(ActorContext actor, string name, CancellationToken cancellationToken);
    Task RemoveProfessionalInstitutionAsync(ActorContext actor, Guid relationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HourlyRateDto>> GetHourlyRatesAsync(ActorContext actor, Guid relationId, short? year, CancellationToken cancellationToken);
    Task<HourlyRateDto> AddHourlyRateVersionAsync(ActorContext actor, Guid relationId, short year, long hourlyRateClp, CancellationToken cancellationToken);
    Task<MonthlyWorkspaceDto> GetMonthlyWorkspaceAsync(ActorContext actor, Guid? professionalId, short year, short month, CancellationToken cancellationToken);
    Task<HourRecordsPageDto> GetHourRecordsPageAsync(ActorContext actor, Guid? professionalId, short year, short month,
        Guid professionalInstitutionId, int? beforeOrder, int pageSize, CancellationToken cancellationToken);
    Task<MonthlyWorkspaceDto> AddInstitutionToPeriodAsync(ActorContext actor, short year, short month, Guid professionalInstitutionId, CancellationToken cancellationToken);
    Task<MonthlyWorkspaceDto> RemoveInstitutionFromPeriodAsync(ActorContext actor, short year, short month, Guid professionalInstitutionId, CancellationToken cancellationToken);
    Task<SavedHourRecordDto> AddHourRecordAsync(ActorContext actor, short year, short month, Guid professionalInstitutionId, int hours, CancellationToken cancellationToken);
    Task<SavedHourRecordDto> UpdateHourRecordAsync(ActorContext actor, Guid recordId, int hours, long expectedVersion, CancellationToken cancellationToken);
    Task<MonthlyWorkspaceDto> DeleteHourRecordAsync(ActorContext actor, Guid recordId, long expectedVersion, CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(ActorContext actor, Guid? professionalId, short fromYear, short toYear, IReadOnlyCollection<Guid>? institutionIds, CancellationToken cancellationToken);
}
