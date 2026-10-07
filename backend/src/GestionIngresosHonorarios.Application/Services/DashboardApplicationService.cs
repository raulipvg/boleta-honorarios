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
        IReadOnlyCollection<string>? institutionKeys,
        CancellationToken cancellationToken)
    {
        if (fromYear < 1900 || toYear < fromYear || toYear - fromYear > 50)
            throw AppError.BadRequest("El intervalo de años solicitado no es válido.");
        var ownerId = ResolveProfessionalId(actor, professionalId);

        var publicRelations = await (
            from relation in _db.ProfessionalInstitutions.AsNoTracking()
            join institution in _db.PublicInstitutions.AsNoTracking() on relation.PublicInstitutionId equals institution.Id
            where relation.ProfessionalId == ownerId
            select new { relation.Id, InstitutionId = institution.Id, institution.Name }
        ).ToListAsync(cancellationToken);

        var privateInstitutionRows = await (
            from liquidation in _db.PrivateLiquidations.AsNoTracking()
            join institution in _db.PrivateInstitutions.AsNoTracking() on liquidation.PrivateInstitutionId equals institution.Id
            where liquidation.ProfessionalId == ownerId
            select new { institution.Id, institution.Name }
        ).Distinct().ToListAsync(cancellationToken);

        var columns = publicRelations
            .Select(x => new DashboardInstitutionDto($"public:{x.Id:D}", x.Name, "public"))
            .Concat(privateInstitutionRows.Select(x => new DashboardInstitutionDto($"private:{x.Id:D}", x.Name, "private")))
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ToList();

        if (institutionKeys is { Count: > 0 })
        {
            var requested = institutionKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requested.Any(key => columns.All(column => !string.Equals(column.Key, key, StringComparison.OrdinalIgnoreCase))))
                throw AppError.NotFound();
            columns = columns.Where(column => requested.Contains(column.Key)).ToList();
        }

        var periods = await _db.MonthlyPeriods.AsNoTracking()
            .Where(x => x.ProfessionalId == ownerId && x.Year >= fromYear && x.Year <= toYear)
            .ToListAsync(cancellationToken);
        var periodIds = periods.Select(x => x.Id).ToArray();
        var publicValues = await _db.PeriodInstitutions.AsNoTracking()
            .Where(x => periodIds.Contains(x.PeriodId))
            .Select(x => new { x.PeriodId, x.ProfessionalInstitutionId, x.NetTotalClp })
            .ToListAsync(cancellationToken);
        var privateValues = await _db.PrivateLiquidations.AsNoTracking()
            .Where(x => periodIds.Contains(x.PeriodId))
            .GroupBy(x => new { x.PeriodId, x.PrivateInstitutionId })
            .Select(group => new
            {
                group.Key.PeriodId,
                group.Key.PrivateInstitutionId,
                NetTotalClp = group.Sum(x => x.NetTotalClp)
            })
            .ToListAsync(cancellationToken);

        var periodsByDate = periods.ToDictionary(x => (x.Year, x.Month));
        var publicByPeriod = publicValues.GroupBy(x => x.PeriodId)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.ProfessionalInstitutionId, y => y.NetTotalClp));
        var privateByPeriod = privateValues.GroupBy(x => x.PeriodId)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.PrivateInstitutionId, y => y.NetTotalClp));

        var rows = new List<DashboardMonthDto>();
        for (var year = (int)fromYear; year <= toYear; year++)
        {
            for (short month = 1; month <= 12; month++)
            {
                var exists = periodsByDate.TryGetValue(((short)year, month), out var period);
                IReadOnlyList<DashboardInstitutionValueDto> monthValues;
                if (!exists)
                {
                    monthValues = columns.Select(x => new DashboardInstitutionValueDto(x.Key, null)).ToList();
                }
                else
                {
                    var publicValuesForPeriod = publicByPeriod.GetValueOrDefault(period!.Id);
                    var privateValuesForPeriod = privateByPeriod.GetValueOrDefault(period!.Id);
                    monthValues = columns.Select(column =>
                    {
                        if (column.Type == "public")
                        {
                            var relationId = Guid.Parse(column.Key["public:".Length..]);
                            return new DashboardInstitutionValueDto(column.Key,
                                publicValuesForPeriod is not null && publicValuesForPeriod.TryGetValue(relationId, out var publicNetValue) ? publicNetValue : 0L);
                        }

                        var privateId = Guid.Parse(column.Key["private:".Length..]);
                        return new DashboardInstitutionValueDto(column.Key,
                            privateValuesForPeriod is not null && privateValuesForPeriod.TryGetValue(privateId, out var privateNetValue) ? privateNetValue : 0L);
                    }).ToList();
                }

                long? publicGross = exists ? period!.GrossTotalClp : null;
                long? publicRetention = exists ? period!.RetentionTotalClp : null;
                long? publicNet = exists ? period!.NetTotalClp : null;
                long? privateGross = exists ? period!.PrivateGrossTotalClp : null;
                long? privateRetention = exists ? period!.PrivateRetentionTotalClp : null;
                long? privateNet = exists ? period!.PrivateNetTotalClp : null;
                long? privateAttentions = exists ? period!.PrivateAttentionCount : null;
                decimal? privateMinutes = exists ? period!.PrivateAttentionMinutes : null;
                var combinedNet = exists
                    ? checked(period!.NetTotalClp + period.PrivateNetTotalClp)
                    : (long?)null;
                if (combinedNet.HasValue && combinedNet.Value > GestionIngresosHonorarios.Domain.Services.IncomeCalculator.MaxExactInteger)
                    throw new OverflowException("El líquido combinado excede el rango entero exacto del cliente.");

                rows.Add(new DashboardMonthDto(
                    (short)year,
                    month,
                    exists,
                    combinedNet,
                    exists ? period!.TotalHours : null,
                    publicGross,
                    publicRetention,
                    publicNet,
                    privateGross,
                    privateRetention,
                    privateNet,
                    privateAttentions,
                    privateMinutes,
                    monthValues));
            }
        }

        return new DashboardDto(ownerId, fromYear, toYear, columns, rows);
    }
}
