using GestionIngresosHonorarios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GestionIngresosHonorarios.Application.Contracts;

// Explicit MVP exception: Application uses EF Core query types behind this port;
// controllers still exchange DTOs, and Domain remains persistence-independent.
public interface IApplicationDbContext
{
    DbSet<Professional> Professionals { get; }
    DbSet<PublicInstitution> PublicInstitutions { get; }
    DbSet<ProfessionalInstitution> ProfessionalInstitutions { get; }
    DbSet<AnnualHourlyRate> AnnualHourlyRates { get; }
    DbSet<MonthlyPeriod> MonthlyPeriods { get; }
    DbSet<PeriodInstitution> PeriodInstitutions { get; }
    DbSet<HourRecord> HourRecords { get; }
    DbSet<AnnualRetentionRate> AnnualRetentionRates { get; }
    DbSet<PrivateInstitution> PrivateInstitutions { get; }
    DbSet<PrivatePayerEntity> PrivatePayerEntities { get; }
    DbSet<PrivatePaymentRule> PrivatePaymentRules { get; }
    DbSet<PrivateLiquidation> PrivateLiquidations { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class AnnualRetentionRate
{
    public short Year { get; set; }
    public decimal Percentage { get; set; }
}
