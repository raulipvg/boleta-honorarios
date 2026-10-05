using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Application.Services;

public sealed partial class IncomeApplicationService
{
    public async Task<IReadOnlyList<RetentionRateDto>> GetRetentionRatesAsync(CancellationToken cancellationToken)
    {
        return await _db.AnnualRetentionRates.AsNoTracking().OrderBy(x => x.Year)
            .Select(x => new RetentionRateDto(x.Year, x.Percentage)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProfessionalSummaryDto>> GetProfessionalsAsync(CancellationToken cancellationToken)
    {
        return await _db.Professionals.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ProfessionalSummaryDto(x.Id, x.Name)).ToListAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(
        ActorContext actor,
        Guid? professionalId,
        short fromYear,
        short toYear,
        IReadOnlyCollection<Guid>? institutionIds,
        CancellationToken cancellationToken)
    {
        if (fromYear < 1900 || toYear < fromYear || toYear - fromYear > 50)
            throw AppError.BadRequest("El intervalo de años solicitado no es válido.");
        var ownerId = ResolveProfessionalId(actor, professionalId);
        var relations = await (
            from relation in _db.ProfessionalInstitutions.AsNoTracking()
            join institution in _db.PublicInstitutions.AsNoTracking() on relation.PublicInstitutionId equals institution.Id
            where relation.ProfessionalId == ownerId
            select new { RelationId = relation.Id, institution.Id, institution.Name }
        ).ToListAsync(cancellationToken);

        var selectedRelations = relations;
        if (institutionIds is { Count: > 0 })
        {
            var requested = institutionIds.ToHashSet();
            if (requested.Any(id => relations.All(x => x.Id != id))) throw AppError.NotFound();
            selectedRelations = relations.Where(x => requested.Contains(x.Id)).ToList();
        }

        var columns = selectedRelations
            .Select(x => new DashboardInstitutionDto(x.Id, x.RelationId, x.Name))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ToList();
        var periods = await _db.MonthlyPeriods.AsNoTracking()
            .Where(x => x.ProfessionalId == ownerId && x.Year >= fromYear && x.Year <= toYear)
            .ToListAsync(cancellationToken);
        var periodIds = periods.Select(x => x.Id).ToArray();
        var values = await _db.PeriodInstitutions.AsNoTracking()
            .Where(x => periodIds.Contains(x.PeriodId))
            .Select(x => new { x.PeriodId, x.ProfessionalInstitutionId, x.NetTotalClp })
            .ToListAsync(cancellationToken);
        var periodsByDate = periods.ToDictionary(x => (x.Year, x.Month));
        var valuesByPeriod = values.GroupBy(x => x.PeriodId)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.ProfessionalInstitutionId, y => y.NetTotalClp));

        var rows = new List<DashboardMonthDto>();
        for (var year = (int)fromYear; year <= toYear; year++)
        {
            for (short month = 1; month <= 12; month++)
            {
                var exists = periodsByDate.TryGetValue(((short)year, month), out var period);
                IReadOnlyList<DashboardInstitutionValueDto> monthValues;
                if (!exists)
                {
                    monthValues = columns.Select(x => new DashboardInstitutionValueDto(x.Id, null)).ToList();
                }
                else
                {
                    var institutionValues = valuesByPeriod.GetValueOrDefault(period!.Id);
                    monthValues = columns.Select(x => new DashboardInstitutionValueDto(
                        x.Id,
                        institutionValues is not null && institutionValues.TryGetValue(x.ProfessionalInstitutionId, out var net) ? net : 0L)).ToList();
                }

                rows.Add(new DashboardMonthDto(
                    (short)year,
                    month,
                    exists,
                    exists ? period!.NetTotalClp : null,
                    monthValues));
            }
        }

        return new DashboardDto(ownerId, fromYear, toYear, columns, rows);
    }
}
