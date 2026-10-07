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

        var periods = await _db.MonthlyPeriods.AsNoTracking()
            .Where(x => x.ProfessionalId == ownerId && x.Year >= fromYear && x.Year <= toYear)
            .ToListAsync(cancellationToken);
        var periodIds = periods.Select(x => x.Id).ToArray();

        var publicRelations = await (
            from relation in _db.ProfessionalInstitutions.AsNoTracking()
            join institution in _db.PublicInstitutions.AsNoTracking() on relation.PublicInstitutionId equals institution.Id
            where relation.ProfessionalId == ownerId
            select new PublicInstitutionRelation(relation.Id, institution.Id, institution.Name, institution.NormalizedName)
        ).ToListAsync(cancellationToken);

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

        var publicColumnByRelation = publicRelations.ToDictionary(
            relation => relation.ProfessionalInstitutionId,
            relation => GetPublicColumn(relation.PublicInstitutionId, relation.Name, relation.NormalizedName));
        var publicValuesByPeriod = publicValues
            .GroupBy(x => x.PeriodId)
            .ToDictionary(
                period => period.Key,
                period => period
                    .GroupBy(value => publicColumnByRelation[value.ProfessionalInstitutionId].Key)
                    .ToDictionary(group => group.Key, group => group.Sum(value => value.NetTotalClp), StringComparer.Ordinal));
        var privateValuesByPeriod = privateValues
            .GroupBy(x => x.PeriodId)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.PrivateInstitutionId, y => y.NetTotalClp));

        var publicKeysWithIncome = publicValues
            .Where(value => value.NetTotalClp > 0)
            .Select(value => publicColumnByRelation[value.ProfessionalInstitutionId].Key)
            .ToHashSet(StringComparer.Ordinal);
        var publicColumns = publicRelations
            .Select(relation => publicColumnByRelation[relation.ProfessionalInstitutionId])
            .GroupBy(column => column.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .Where(column => publicKeysWithIncome.Contains(column.Key))
            .ToList();

        var privateInstitutionIdsWithIncome = privateValues
            .Where(value => value.NetTotalClp > 0)
            .Select(value => value.PrivateInstitutionId)
            .Distinct()
            .ToArray();
        List<DashboardInstitutionDto> privateColumns = [];
        if (privateInstitutionIdsWithIncome.Length > 0)
        {
            privateColumns = await _db.PrivateInstitutions.AsNoTracking()
                .Where(x => privateInstitutionIdsWithIncome.Contains(x.Id))
                .OrderBy(x => x.Name)
                .Select(x => new DashboardInstitutionDto($"private:{x.Id:D}", x.Name, "private"))
                .ToListAsync(cancellationToken);
        }

        var allColumns = publicColumns.Concat(privateColumns)
            .OrderBy(x => x.Name, StringComparer.CurrentCulture)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ToList();
        var columns = allColumns;
        if (institutionKeys is { Count: > 0 })
        {
            var requested = institutionKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requested.Any(key => allColumns.All(column => !string.Equals(column.Key, key, StringComparison.OrdinalIgnoreCase))))
                throw AppError.NotFound();
            columns = allColumns.Where(column => requested.Contains(column.Key)).ToList();
        }

        var periodsByDate = periods.ToDictionary(x => (x.Year, x.Month));
        var rows = new List<DashboardMonthDto>();
        for (var year = (int)fromYear; year <= toYear; year++)
        {
            for (short month = 1; month <= 12; month++)
            {
                var exists = periodsByDate.TryGetValue(((short)year, month), out var period);
                IReadOnlyList<DashboardInstitutionValueDto> monthValues;
                if (!exists)
                {
                    monthValues = columns.Select(column => new DashboardInstitutionValueDto(column.Key, null)).ToList();
                }
                else
                {
                    var publicForPeriod = publicValuesByPeriod.GetValueOrDefault(period!.Id);
                    var privateForPeriod = privateValuesByPeriod.GetValueOrDefault(period!.Id);
                    monthValues = columns.Select(column =>
                    {
                        if (column.Type == "public")
                            return new DashboardInstitutionValueDto(column.Key,
                                publicForPeriod is not null && publicForPeriod.TryGetValue(column.Key, out var publicNet) ? publicNet : 0L);

                        var privateId = Guid.Parse(column.Key["private:".Length..]);
                        return new DashboardInstitutionValueDto(column.Key,
                            privateForPeriod is not null && privateForPeriod.TryGetValue(privateId, out var privateNet) ? privateNet : 0L);
                    }).ToList();
                }

                var combinedNet = exists
                    ? checked(period!.NetTotalClp + period.PrivateNetTotalClp)
                    : (long?)null;
                if (combinedNet.HasValue && combinedNet.Value > GestionIngresosHonorarios.Domain.Services.IncomeCalculator.MaxExactInteger)
                    throw new OverflowException("El líquido combinado excede el rango entero exacto del cliente.");

                rows.Add(new DashboardMonthDto((short)year, month, exists, combinedNet, monthValues));
            }
        }

        return new DashboardDto(ownerId, fromYear, toYear, columns, rows);
    }

    private static DashboardInstitutionDto GetPublicColumn(Guid institutionId, string name, string normalizedName)
    {
        var key = new string(normalizedName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return key switch
        {
            "sapulorenzoarenas" or "lorenzoarenas" => new DashboardInstitutionDto("public:alias:sapu-lorenzo-arenas", "SAPU Lorenzo Arenas", "public"),
            "sartucapel" or "tucapel" => new DashboardInstitutionDto("public:alias:sar-tucapel", "SAR TUCAPEL", "public"),
            _ => new DashboardInstitutionDto($"public:{institutionId:D}", name, "public")
        };
    }

    private sealed record PublicInstitutionRelation(Guid ProfessionalInstitutionId, Guid PublicInstitutionId, string Name, string NormalizedName);
}
