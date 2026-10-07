using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Application.Services;

public sealed partial class IncomeApplicationService
{
    private const int WorkspaceRecordPageSize = 100;
    public async Task<MonthlyWorkspaceDto> GetMonthlyWorkspaceAsync(
        ActorContext actor, Guid? professionalId, short year, short month, CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        var ownerId = ResolveProfessionalId(actor, professionalId);
        var period = await _db.MonthlyPeriods.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProfessionalId == ownerId && x.Year == year && x.Month == month, cancellationToken);
        if (period is null)
            return new MonthlyWorkspaceDto(null, ownerId, year, month, false, null, 0, 0, 0, 0,
                0L, Array.Empty<PeriodInstitutionDto>());
        return await BuildWorkspaceAsync(period, cancellationToken);
    }

    public async Task<HourRecordsPageDto> GetHourRecordsPageAsync(
        ActorContext actor,
        Guid? professionalId,
        short year,
        short month,
        Guid professionalInstitutionId,
        int? beforeOrder,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        if (pageSize is < 1 or > 100 || beforeOrder < 0)
            throw AppError.BadRequest("El cursor o tamaño de página no es válido.");
        var ownerId = ResolveProfessionalId(actor, professionalId);
        var period = await _db.MonthlyPeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProfessionalId == ownerId && x.Year == year && x.Month == month, cancellationToken)
            ?? throw AppError.NotFound();
        var institution = await _db.PeriodInstitutions.AsNoTracking().SingleOrDefaultAsync(
            x => x.PeriodId == period.Id && x.ProfessionalId == ownerId && x.ProfessionalInstitutionId == professionalInstitutionId,
            cancellationToken) ?? throw AppError.NotFound();

        var query = _db.HourRecords.AsNoTracking().Where(x => x.PeriodInstitutionId == institution.Id);
        if (beforeOrder.HasValue) query = query.Where(x => x.Order < beforeOrder.Value);
        var page = await query.OrderByDescending(x => x.Order).Take(pageSize + 1)
            .Select(x => new HourRecordDto(x.Id, x.Hours, x.Order, x.Version))
            .ToListAsync(cancellationToken);
        var hasMore = page.Count > pageSize;
        if (hasMore) page.RemoveAt(page.Count - 1);
        page.Reverse();
        return new HourRecordsPageDto(page, hasMore, hasMore && page.Count > 0 ? page[0].Order : null);
    }

    public async Task<MonthlyWorkspaceDto> AddInstitutionToPeriodAsync(
        ActorContext actor, short year, short month, Guid professionalInstitutionId, CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        var ownerId = RequireOwnProfessional(actor);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await GetOrCreatePeriodAsync(ownerId, year, month, cancellationToken);
        await LockPeriodAsync(period.Id, cancellationToken);

        var relation = await _db.ProfessionalInstitutions.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == professionalInstitutionId && x.ProfessionalId == ownerId && x.Active, cancellationToken)
            ?? throw AppError.NotFound();
        var existing = await _db.PeriodInstitutions.SingleOrDefaultAsync(
            x => x.PeriodId == period.Id && x.ProfessionalInstitutionId == relation.Id, cancellationToken);
        if (existing is null)
        {
            var tariffVersion = await _db.AnnualHourlyRates
                .Where(x => x.ProfessionalInstitutionId == relation.Id && x.Year == year)
                .OrderByDescending(x => x.Version)
                .Select(x => (int?)x.Version)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw AppError.Conflict("Debe configurar una tarifa para esta institución y año antes de agregarla al período.");

            var order = await _db.PeriodInstitutions.Where(x => x.PeriodId == period.Id)
                .Select(x => (int?)x.Order).MaxAsync(cancellationToken) ?? 0;
            _db.PeriodInstitutions.Add(new PeriodInstitution(
                period.Id, ownerId, year, relation.Id, tariffVersion, checked(order + 1)));
            await _db.SaveChangesAsync(cancellationToken);
            await RecalculatePeriodAsync(period, cancellationToken);
        }

        var workspace = await GetMonthlyWorkspaceAsync(actor, null, year, month, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return workspace;
    }

    public async Task<MonthlyWorkspaceDto> RemoveInstitutionFromPeriodAsync(
        ActorContext actor, short year, short month, Guid professionalInstitutionId, CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        var ownerId = RequireOwnProfessional(actor);
        var period = await _db.MonthlyPeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProfessionalId == ownerId && x.Year == year && x.Month == month, cancellationToken)
            ?? throw AppError.NotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lockedPeriod = await _db.MonthlyPeriods.SingleOrDefaultAsync(x => x.Id == period.Id, cancellationToken)
            ?? throw AppError.NotFound();
        await LockPeriodAsync(lockedPeriod.Id, cancellationToken);
        var periodInstitution = await _db.PeriodInstitutions.SingleOrDefaultAsync(
            x => x.PeriodId == lockedPeriod.Id && x.ProfessionalId == ownerId && x.ProfessionalInstitutionId == professionalInstitutionId,
            cancellationToken) ?? throw AppError.NotFound();
        if (await _db.HourRecords.AnyAsync(x => x.PeriodInstitutionId == periodInstitution.Id, cancellationToken))
            throw AppError.Conflict("No se puede retirar la institución mientras tenga registros de horas.");

        _db.PeriodInstitutions.Remove(periodInstitution);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculatePeriodAsync(lockedPeriod, cancellationToken);
        var workspace = await GetMonthlyWorkspaceAsync(actor, null, year, month, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return workspace;
    }

    public async Task<SavedHourRecordDto> AddHourRecordAsync(
        ActorContext actor, short year, short month, Guid professionalInstitutionId, int hours, CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        if (hours < 1) throw AppError.BadRequest("Las horas deben ser un entero mayor o igual a 1.");
        var ownerId = RequireOwnProfessional(actor);
        var period = await _db.MonthlyPeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProfessionalId == ownerId && x.Year == year && x.Month == month, cancellationToken)
            ?? throw AppError.Conflict("Agregue primero una institución al período.");

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lockedPeriod = await _db.MonthlyPeriods.SingleOrDefaultAsync(x => x.Id == period.Id, cancellationToken)
            ?? throw AppError.NotFound();
        await LockPeriodAsync(lockedPeriod.Id, cancellationToken);
        var periodInstitution = await _db.PeriodInstitutions.SingleOrDefaultAsync(
            x => x.PeriodId == lockedPeriod.Id && x.ProfessionalId == ownerId && x.ProfessionalInstitutionId == professionalInstitutionId,
            cancellationToken) ?? throw AppError.NotFound();
        var order = await _db.HourRecords.Where(x => x.PeriodInstitutionId == periodInstitution.Id)
            .Select(x => (int?)x.Order).MaxAsync(cancellationToken) ?? 0;
        var record = new HourRecord(periodInstitution.Id, hours, checked(order + 1));
        _db.HourRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculatePeriodAsync(lockedPeriod, cancellationToken);
        var workspace = await GetMonthlyWorkspaceAsync(actor, null, year, month, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SavedHourRecordDto(new HourRecordDto(record.Id, record.Hours, record.Order, record.Version), workspace);
    }

    public async Task<SavedHourRecordDto> UpdateHourRecordAsync(
        ActorContext actor, Guid recordId, int hours, long expectedVersion, CancellationToken cancellationToken)
    {
        if (hours < 1) throw AppError.BadRequest("Las horas deben ser un entero mayor o igual a 1.");
        var ownerId = RequireOwnProfessional(actor);
        var location = await (
            from hourRecord in _db.HourRecords.AsNoTracking()
            join institution in _db.PeriodInstitutions.AsNoTracking() on hourRecord.PeriodInstitutionId equals institution.Id
            where hourRecord.Id == recordId && institution.ProfessionalId == ownerId
            select new { institution.PeriodId }
        ).SingleOrDefaultAsync(cancellationToken) ?? throw AppError.NotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await _db.MonthlyPeriods.SingleOrDefaultAsync(x => x.Id == location.PeriodId, cancellationToken)
            ?? throw AppError.NotFound();
        await LockPeriodAsync(period.Id, cancellationToken);
        var recordToUpdate = await _db.HourRecords.SingleOrDefaultAsync(x => x.Id == recordId, cancellationToken)
            ?? throw AppError.NotFound();
        if (recordToUpdate.Version != expectedVersion)
            throw AppError.Conflict("El registro cambió en otra sesión. Se recargó el período; revise y reaplique su edición.");

        recordToUpdate.ChangeHours(hours);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculatePeriodAsync(period, cancellationToken);
        var workspace = await GetMonthlyWorkspaceAsync(actor, null, period.Year, period.Month, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SavedHourRecordDto(new HourRecordDto(recordToUpdate.Id, recordToUpdate.Hours, recordToUpdate.Order, recordToUpdate.Version), workspace);
    }

    public async Task<MonthlyWorkspaceDto> DeleteHourRecordAsync(ActorContext actor, Guid recordId, long expectedVersion, CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnProfessional(actor);
        var location = await (
            from hourRecord in _db.HourRecords.AsNoTracking()
            join institution in _db.PeriodInstitutions.AsNoTracking() on hourRecord.PeriodInstitutionId equals institution.Id
            where hourRecord.Id == recordId && institution.ProfessionalId == ownerId
            select new { institution.PeriodId }
        ).SingleOrDefaultAsync(cancellationToken) ?? throw AppError.NotFound();

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await _db.MonthlyPeriods.SingleOrDefaultAsync(x => x.Id == location.PeriodId, cancellationToken)
            ?? throw AppError.NotFound();
        await LockPeriodAsync(period.Id, cancellationToken);
        var record = await _db.HourRecords.SingleOrDefaultAsync(x => x.Id == recordId, cancellationToken)
            ?? throw AppError.NotFound();
        if (record.Version != expectedVersion)
            throw AppError.Conflict("El registro cambió en otra sesión. Se recargó el período; revise antes de eliminarlo.");

        _db.HourRecords.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculatePeriodAsync(period, cancellationToken);
        var workspace = await GetMonthlyWorkspaceAsync(actor, null, period.Year, period.Month, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return workspace;
    }

    private async Task<MonthlyPeriod> GetOrCreatePeriodAsync(Guid professionalId, short year, short month, CancellationToken cancellationToken)
    {
        var existing = await _db.MonthlyPeriods.SingleOrDefaultAsync(
            x => x.ProfessionalId == professionalId && x.Year == year && x.Month == month, cancellationToken);
        if (existing is not null) return existing;

        var retention = await _db.AnnualRetentionRates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Year == year, cancellationToken)
            ?? throw AppError.Conflict("No existe una tasa de retención configurada para el año solicitado.");
        var period = new MonthlyPeriod(professionalId, year, month, retention.Percentage);
        _db.MonthlyPeriods.Add(period);
        await _db.SaveChangesAsync(cancellationToken);
        return period;
    }

    private async Task LockPeriodAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM periodos_mensuales WHERE id = @periodId FOR UPDATE";
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "periodId";
        parameter.Value = periodId;
        command.Parameters.Add(parameter);
        _ = await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task RecalculatePeriodAsync(MonthlyPeriod period, CancellationToken cancellationToken)
    {
        var institutions = await _db.PeriodInstitutions
            .Where(x => x.PeriodId == period.Id)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);
        long totalHours = 0;
        long totalGross = 0;
        long totalRetention = 0;
        long totalNet = 0;

        foreach (var institution in institutions)
        {
            var rate = await _db.AnnualHourlyRates.AsNoTracking().SingleOrDefaultAsync(
                x => x.ProfessionalInstitutionId == institution.ProfessionalInstitutionId
                    && x.Year == institution.Year && x.Version == institution.HourlyRateVersion,
                cancellationToken) ?? throw new InvalidOperationException("La tarifa histórica aplicada no existe.");
            var hours = await _db.HourRecords.Where(x => x.PeriodInstitutionId == institution.Id)
                .Select(x => (long)x.Hours).SumAsync(cancellationToken);
            var totals = IncomeCalculator.Calculate(hours, rate.HourlyRateClp, period.AppliedRetentionPercentage);
            institution.UpdateTotals(totals.Hours, totals.GrossClp, totals.RetentionClp, totals.NetClp);
            totalHours = checked(totalHours + totals.Hours);
            totalGross = checked(totalGross + totals.GrossClp);
            totalRetention = checked(totalRetention + totals.RetentionClp);
            totalNet = checked(totalNet + totals.NetClp);
        }

        period.UpdateTotals(totalHours, totalGross, totalRetention, totalNet);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MonthlyWorkspaceDto> BuildWorkspaceAsync(MonthlyPeriod period, CancellationToken cancellationToken)
    {
        var rows = await (
            from item in _db.PeriodInstitutions.AsNoTracking()
            join relation in _db.ProfessionalInstitutions.AsNoTracking() on item.ProfessionalInstitutionId equals relation.Id
            join institution in _db.PublicInstitutions.AsNoTracking() on relation.PublicInstitutionId equals institution.Id
            join rate in _db.AnnualHourlyRates.AsNoTracking()
                on new { item.ProfessionalInstitutionId, item.Year, Version = item.HourlyRateVersion }
                equals new { rate.ProfessionalInstitutionId, rate.Year, rate.Version }
            where item.PeriodId == period.Id
            orderby item.Order
            select new { Item = item, Institution = institution, Rate = rate }
        ).ToListAsync(cancellationToken);

        var ids = rows.Select(x => x.Item.Id).ToArray();
        var countRows = await _db.HourRecords.AsNoTracking().Where(x => ids.Contains(x.PeriodInstitutionId))
            .GroupBy(x => x.PeriodInstitutionId)
            .Select(group => new { PeriodInstitutionId = group.Key, Count = (long)group.Count() })
            .ToListAsync(cancellationToken);
        var counts = countRows.ToDictionary(x => x.PeriodInstitutionId, x => x.Count);
        var dtoInstitutions = new List<PeriodInstitutionDto>(rows.Count);
        foreach (var row in rows)
        {
            var page = await _db.HourRecords.AsNoTracking()
                .Where(x => x.PeriodInstitutionId == row.Item.Id)
                .OrderByDescending(x => x.Order)
                .Take(WorkspaceRecordPageSize + 1)
                .Select(x => new HourRecordDto(x.Id, x.Hours, x.Order, x.Version))
                .ToListAsync(cancellationToken);
            var hasMore = page.Count > WorkspaceRecordPageSize;
            if (hasMore) page.RemoveAt(page.Count - 1);
            page.Reverse();
            dtoInstitutions.Add(new PeriodInstitutionDto(
                row.Item.ProfessionalInstitutionId,
                row.Institution.Name,
                row.Rate.HourlyRateClp,
                row.Rate.Version,
                row.Item.TotalHours,
                row.Item.GrossTotalClp,
                row.Item.RetentionTotalClp,
                row.Item.NetTotalClp,
                row.Item.Version,
                counts.GetValueOrDefault(row.Item.Id),
                hasMore,
                hasMore && page.Count > 0 ? page[0].Order : null,
                page));
        }

        return new MonthlyWorkspaceDto(
            period.Id, period.ProfessionalId, period.Year, period.Month, true, period.AppliedRetentionPercentage,
            period.TotalHours, period.GrossTotalClp, period.RetentionTotalClp, period.NetTotalClp,
            period.Version, dtoInstitutions);
    }

    private static void ValidatePeriod(short year, short month)
    {
        if (year < 1900 || month is < 1 or > 12) throw AppError.BadRequest("Año o mes fuera de rango.");
    }
}
