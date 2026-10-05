using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.Services;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Infrastructure.Data;
using GestionIngresosHonorarios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace GestionIngresosHonorarios.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class MonthlyIncomeWorkflowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gestion_honorarios_test")
        .WithUsername("integration_owner")
        .WithPassword("Integration-only-passphrase-2026")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.AnnualRetentionRates.Add(new AnnualRetentionRate { Year = 2026, Percentage = 15.25m });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "integration-professional",
            NormalizedUserName = "INTEGRATION-PROFESSIONAL",
            PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), "Integration-only-passphrase-2026"),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Active = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        var professional = new Professional(user.Id, "Profesional de integración");
        var institution = new PublicInstitution("Hospital de integración");
        db.Professionals.Add(professional);
        db.PublicInstitutions.Add(institution);
        await db.SaveChangesAsync();

        var relation = new ProfessionalInstitution(professional.Id, institution.Id);
        db.ProfessionalInstitutions.Add(relation);
        await db.SaveChangesAsync();
        db.AnnualHourlyRates.Add(new AnnualHourlyRate(relation.Id, 2026, 1, 31_488));
        await db.SaveChangesAsync();

        UserId = user.Id;
        ProfessionalId = professional.Id;
        RelationId = relation.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public Guid UserId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public Guid RelationId { get; private set; }

    [Fact]
    public async Task HourMutationsCommitDetailAndBothAggregateLevelsAndKeepSnapshots()
    {
        await using var db = CreateContext();
        var service = new IncomeApplicationService(db);
        var actor = new ActorContext(UserId, ProfessionalId, IsAdministrator: false, IsProfessional: true);

        await service.AddInstitutionToPeriodAsync(actor, 2026, 9, RelationId, CancellationToken.None);
        for (var index = 0; index < 7; index++)
            await service.AddHourRecordAsync(actor, 2026, 9, RelationId, 6, CancellationToken.None);

        var workspace = await service.GetMonthlyWorkspaceAsync(actor, null, 2026, 9, CancellationToken.None);
        Assert.Equal(42, workspace.TotalHours);
        Assert.Equal(1_322_496, workspace.GrossTotalClp);
        Assert.Equal(201_681, workspace.RetentionTotalClp);
        Assert.Equal(1_120_815, workspace.NetTotalClp);
        Assert.Equal(1, Assert.Single(workspace.Institutions).HourlyRateVersion);

        var updated = await service.AddHourlyRateVersionAsync(actor, RelationId, 2026, 40_000, CancellationToken.None);
        Assert.Equal(2, updated.Version);
        var historicalWorkspace = await service.GetMonthlyWorkspaceAsync(actor, null, 2026, 9, CancellationToken.None);
        Assert.Equal(1, Assert.Single(historicalWorkspace.Institutions).HourlyRateVersion);
        Assert.Equal(31_488, historicalWorkspace.Institutions[0].HourlyRateClp);

        var institution = Assert.Single(historicalWorkspace.Institutions);
        var conflict = await Assert.ThrowsAsync<GestionIngresosHonorarios.Application.Common.ApplicationException>(
            () => service.RemoveInstitutionFromPeriodAsync(actor, 2026, 9, RelationId, CancellationToken.None));
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(7, institution.Records.Count);

        for (var index = 0; index < 100; index++)
            await service.AddHourRecordAsync(actor, 2026, 9, RelationId, 1, CancellationToken.None);

        var recentWorkspace = await service.GetMonthlyWorkspaceAsync(actor, null, 2026, 9, CancellationToken.None);
        var recentInstitution = Assert.Single(recentWorkspace.Institutions);
        Assert.Equal(107, recentInstitution.RecordsCount);
        Assert.Equal(100, recentInstitution.Records.Count);
        Assert.True(recentInstitution.HasMoreRecords);
        Assert.Equal(8, recentInstitution.Records[0].Order);

        var olderPage = await service.GetHourRecordsPageAsync(actor, null, 2026, 9, RelationId,
            recentInstitution.NextBeforeOrder, 100, CancellationToken.None);
        Assert.Equal(7, olderPage.Records.Count);
        Assert.False(olderPage.HasMoreRecords);
        Assert.Equal(1, olderPage.Records[0].Order);
    }

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options);
}
