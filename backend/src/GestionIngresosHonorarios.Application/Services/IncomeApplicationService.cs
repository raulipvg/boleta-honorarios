using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Application.Services;

public sealed partial class IncomeApplicationService(IApplicationDbContext db) : IIncomeApplicationService
{
    private readonly IApplicationDbContext _db = db;

    public async Task<IReadOnlyList<InstitutionDto>> GetInstitutionsAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var query = _db.PublicInstitutions.AsNoTracking();
        if (!actor.IsAdministrator) query = query.Where(x => x.Active);
        return await query.OrderBy(x => x.Name)
            .Select(x => new InstitutionDto(x.Id, x.Name, x.Active))
            .ToListAsync(cancellationToken);
    }

    public async Task<InstitutionDto> CreateInstitutionAsync(string name, CancellationToken cancellationToken)
    {
        var institution = new PublicInstitution(name);
        _db.PublicInstitutions.Add(institution);
        await _db.SaveChangesAsync(cancellationToken);
        return new InstitutionDto(institution.Id, institution.Name, institution.Active);
    }

    public async Task<InstitutionDto> UpdateInstitutionAsync(Guid institutionId, string name, bool active, CancellationToken cancellationToken)
    {
        var institution = await _db.PublicInstitutions.SingleOrDefaultAsync(x => x.Id == institutionId, cancellationToken)
            ?? throw AppError.NotFound();
        institution.Rename(name);
        institution.SetActive(active);
        await _db.SaveChangesAsync(cancellationToken);
        return new InstitutionDto(institution.Id, institution.Name, institution.Active);
    }

    public async Task<ProfessionalProfileDto> GetProfileAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var professionalId = actor.ProfessionalId ?? throw AppError.NotFound();
        var profile = await _db.Professionals.AsNoTracking()
            .Where(x => x.Id == professionalId && x.UserId == actor.UserId)
            .Select(x => new ProfessionalProfileDto(x.Id, x.UserId, x.Name))
            .SingleOrDefaultAsync(cancellationToken);
        return profile ?? throw AppError.NotFound();
    }

    public async Task<ProfessionalProfileDto> UpdateProfileAsync(ActorContext actor, string name, CancellationToken cancellationToken)
    {
        var professionalId = actor.ProfessionalId ?? throw AppError.NotFound();
        var profile = await _db.Professionals.SingleOrDefaultAsync(x => x.Id == professionalId && x.UserId == actor.UserId, cancellationToken)
            ?? throw AppError.NotFound();
        profile.Rename(name);
        await _db.SaveChangesAsync(cancellationToken);
        return new ProfessionalProfileDto(profile.Id, profile.UserId, profile.Name);
    }

    public async Task<IReadOnlyList<ProfessionalInstitutionDto>> GetProfessionalInstitutionsAsync(
        ActorContext actor, Guid? professionalId, CancellationToken cancellationToken)
    {
        var targetId = ResolveProfessionalId(actor, professionalId);
        var rows = await (
            from relation in _db.ProfessionalInstitutions.AsNoTracking()
            join institution in _db.PublicInstitutions.AsNoTracking() on relation.PublicInstitutionId equals institution.Id
            where relation.ProfessionalId == targetId
            orderby relation.Active descending, institution.Name
            select new { relation, institution }
        ).ToListAsync(cancellationToken);

        var relationIds = rows.Select(x => x.relation.Id).ToArray();
        var rates = await _db.AnnualHourlyRates.AsNoTracking()
            .Where(x => relationIds.Contains(x.ProfessionalInstitutionId))
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Version)
            .Select(x => new { x.ProfessionalInstitutionId, Dto = new HourlyRateDto(x.Year, x.Version, x.HourlyRateClp, x.CreatedAt) })
            .ToListAsync(cancellationToken);
        var ratesByRelation = rates.GroupBy(x => x.ProfessionalInstitutionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<HourlyRateDto>)x.Select(y => y.Dto).ToList());

        return rows.Select(x => new ProfessionalInstitutionDto(
            x.relation.Id,
            x.institution.Id,
            x.institution.Name,
            x.relation.Active,
            ratesByRelation.GetValueOrDefault(x.relation.Id, Array.Empty<HourlyRateDto>()))).ToList();
    }

    public async Task<ProfessionalInstitutionDto> AddProfessionalInstitutionAsync(ActorContext actor, Guid institutionId, CancellationToken cancellationToken)
    {
        if (actor.IsAdministrator) throw AppError.Forbidden();
        var professionalId = RequireOwnProfessional(actor);
        var institution = await _db.PublicInstitutions.SingleOrDefaultAsync(x => x.Id == institutionId && x.Active, cancellationToken)
            ?? throw AppError.NotFound();
        var existing = await _db.ProfessionalInstitutions.SingleOrDefaultAsync(
            x => x.ProfessionalId == professionalId && x.PublicInstitutionId == institutionId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Active)
            {
                existing.SetActive(true);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return new ProfessionalInstitutionDto(existing.Id, institution.Id, institution.Name, existing.Active, Array.Empty<HourlyRateDto>());
        }

        var relation = new ProfessionalInstitution(professionalId, institutionId);
        _db.ProfessionalInstitutions.Add(relation);
        await _db.SaveChangesAsync(cancellationToken);
        return new ProfessionalInstitutionDto(relation.Id, institution.Id, institution.Name, relation.Active, Array.Empty<HourlyRateDto>());
    }

    public async Task RemoveProfessionalInstitutionAsync(ActorContext actor, Guid relationId, CancellationToken cancellationToken)
    {
        var professionalId = RequireOwnProfessional(actor);
        var relation = await _db.ProfessionalInstitutions.SingleOrDefaultAsync(
            x => x.Id == relationId && x.ProfessionalId == professionalId, cancellationToken)
            ?? throw AppError.NotFound();
        relation.SetActive(false);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HourlyRateDto>> GetHourlyRatesAsync(
        ActorContext actor, Guid relationId, short? year, CancellationToken cancellationToken)
    {
        var relation = await _db.ProfessionalInstitutions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == relationId, cancellationToken) ?? throw AppError.NotFound();
        EnsureCanReadProfessional(actor, relation.ProfessionalId);
        var query = _db.AnnualHourlyRates.AsNoTracking().Where(x => x.ProfessionalInstitutionId == relationId);
        if (year.HasValue) query = query.Where(x => x.Year == year.Value);
        return await query.OrderByDescending(x => x.Year).ThenByDescending(x => x.Version)
            .Select(x => new HourlyRateDto(x.Year, x.Version, x.HourlyRateClp, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<HourlyRateDto> AddHourlyRateVersionAsync(
        ActorContext actor, Guid relationId, short year, long hourlyRateClp, CancellationToken cancellationToken)
    {
        if (actor.IsAdministrator) throw AppError.Forbidden();
        var professionalId = RequireOwnProfessional(actor);
        if (year < 1900 || hourlyRateClp < 0) throw AppError.BadRequest("Año o tarifa fuera de rango.");

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var relation = await _db.ProfessionalInstitutions.SingleOrDefaultAsync(
            x => x.Id == relationId && x.ProfessionalId == professionalId, cancellationToken)
            ?? throw AppError.NotFound();
        var latestVersion = await _db.AnnualHourlyRates
            .Where(x => x.ProfessionalInstitutionId == relationId && x.Year == year)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken) ?? 0;
        var rate = new AnnualHourlyRate(relation.Id, year, checked(latestVersion + 1), hourlyRateClp);
        _db.AnnualHourlyRates.Add(rate);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new HourlyRateDto(rate.Year, rate.Version, rate.HourlyRateClp, rate.CreatedAt);
    }

    private static Guid RequireOwnProfessional(ActorContext actor)
    {
        if (!actor.IsProfessional || actor.ProfessionalId is null) throw AppError.Forbidden();
        return actor.ProfessionalId.Value;
    }

    private static Guid ResolveProfessionalId(ActorContext actor, Guid? requestedProfessionalId)
    {
        if (actor.IsAdministrator)
            return requestedProfessionalId ?? actor.ProfessionalId ?? throw AppError.BadRequest("Debe seleccionar un profesional.");
        if (actor.ProfessionalId is null) throw AppError.Forbidden();
        if (requestedProfessionalId.HasValue && requestedProfessionalId.Value != actor.ProfessionalId.Value)
            throw AppError.Forbidden();
        return actor.ProfessionalId.Value;
    }

    private static void EnsureCanReadProfessional(ActorContext actor, Guid professionalId)
    {
        if (actor.IsAdministrator) return;
        if (actor.ProfessionalId != professionalId) throw AppError.NotFound();
    }
}
