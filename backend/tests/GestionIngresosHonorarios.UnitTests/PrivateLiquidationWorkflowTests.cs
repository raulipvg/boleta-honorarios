using System.Text;
using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Application.Services;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Infrastructure.Data;
using GestionIngresosHonorarios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PrivateLiquidationWorkflowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gestion_honorarios_private_test")
        .WithUsername("integration_owner")
        .WithPassword("Integration-only-passphrase-2026")
        .Build();

    public Guid UserId { get; private set; }
    public Guid ProfessionalId { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.AnnualRetentionRates.Add(new AnnualRetentionRate { Year = 2026, Percentage = 15.25m });

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "private-integration-professional",
            NormalizedUserName = "PRIVATE-INTEGRATION-PROFESSIONAL",
            PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), "Integration-only-passphrase-2026"),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Active = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        var professional = new Professional(user.Id, "Profesional privado de integración");
        professional.SetRut("19.091.616-2");
        db.Professionals.Add(professional);
        await db.SaveChangesAsync();

        UserId = user.Id;
        ProfessionalId = professional.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task ImportsEachPdfCalculatesRetentionIndividuallyAndDeletesOwnerData()
    {
        await using var db = CreateContext();
        var files = new MemoryPrivateLiquidationStorage();
        var service = CreateService(db, files, new SamplePrivateLiquidationParser());
        var actor = new ActorContext(UserId, ProfessionalId, IsAdministrator: false, IsProfessional: true);
        var firstFile = Encoding.ASCII.GetBytes("%PDF-1.7\nFIRST-SAMPLE");
        var secondFile = Encoding.ASCII.GetBytes("%PDF-1.7\nSECOND-SAMPLE");

        var firstPreview = await service.PreviewAsync(actor, new MemoryStream(firstFile), firstFile.Length,
            "first.pdf", 12, CancellationToken.None);
        Assert.Equal(12, firstPreview.MinutesPerAttention);
        Assert.Equal(516L, firstPreview.TotalAttentionMinutes);
        Assert.Equal(94_091, firstPreview.RetentionTotalClp);
        Assert.Equal(522_897, firstPreview.NetTotalClp);
        Assert.Empty(files.Files);

        var first = await service.ImportAsync(actor, new MemoryStream(firstFile), firstFile.Length,
            "first.pdf", 12, firstPreview.Sha256, firstPreview.AppliedRetentionPercentage, CancellationToken.None);
        Assert.Equal(43, first.ReportedAttentionCount);

        var correctedAnnualRate = await db.AnnualRetentionRates.SingleAsync(x => x.Year == 2026);
        correctedAnnualRate.Percentage = 18m;
        await db.SaveChangesAsync();

        var secondPreview = await service.PreviewAsync(actor, new MemoryStream(secondFile), secondFile.Length,
            "second.pdf", 20, CancellationToken.None);
        Assert.Equal(80L, secondPreview.TotalAttentionMinutes);
        Assert.Equal(15.25m, secondPreview.AppliedRetentionPercentage);
        var second = await service.ImportAsync(actor, new MemoryStream(secondFile), secondFile.Length,
            "second.pdf", 20, secondPreview.Sha256, secondPreview.AppliedRetentionPercentage, CancellationToken.None);

        Assert.Equal(11_203, second.RetentionTotalClp);
        Assert.Equal(62_257, second.NetTotalClp);
        Assert.Equal(4, second.ReportedAttentionCount);
        Assert.Equal(2, await db.PrivateLiquidations.CountAsync());
        Assert.Equal(2, files.Files.Count);

        var period = await db.MonthlyPeriods.SingleAsync(x => x.ProfessionalId == ProfessionalId && x.Year == 2026 && x.Month == 9);
        Assert.Equal(690_448, period.PrivateGrossTotalClp);
        Assert.Equal(105_294, period.PrivateRetentionTotalClp);
        Assert.Equal(585_154, period.PrivateNetTotalClp);
        Assert.Equal(47, period.PrivateAttentionCount);
        Assert.Equal(596L, period.PrivateAttentionMinutes);

        var administrator = new ActorContext(Guid.NewGuid(), null, IsAdministrator: true, IsProfessional: false);
        var downloaded = await service.DownloadAsync(administrator, first.Id, CancellationToken.None);
        await using (downloaded.Content)
        {
            using var reader = new StreamReader(downloaded.Content, Encoding.ASCII);
            Assert.Contains("FIRST-SAMPLE", await reader.ReadToEndAsync());
        }
        var administratorDelete = await Assert.ThrowsAsync<AppError>(
            () => service.DeleteAsync(administrator, first.Id, CancellationToken.None));
        Assert.Equal(403, administratorDelete.StatusCode);

        var sapu = new PublicInstitution("SAPU Lorenzo Arenas");
        var lorenzoAlias = new PublicInstitution("Lorenzo Arenas");
        var sar = new PublicInstitution("Tucapel");
        var noIncome = new PublicInstitution("Hospital sin ingresos");
        db.PublicInstitutions.AddRange(sapu, lorenzoAlias, sar, noIncome);
        await db.SaveChangesAsync();
        var sapuRelation = new ProfessionalInstitution(ProfessionalId, sapu.Id);
        var lorenzoRelation = new ProfessionalInstitution(ProfessionalId, lorenzoAlias.Id);
        var sarRelation = new ProfessionalInstitution(ProfessionalId, sar.Id);
        var noIncomeRelation = new ProfessionalInstitution(ProfessionalId, noIncome.Id);
        db.ProfessionalInstitutions.AddRange(sapuRelation, lorenzoRelation, sarRelation, noIncomeRelation);
        await db.SaveChangesAsync();
        db.AnnualHourlyRates.AddRange(
            new AnnualHourlyRate(sapuRelation.Id, 2026, 1, 1_000),
            new AnnualHourlyRate(lorenzoRelation.Id, 2026, 1, 1_000),
            new AnnualHourlyRate(sarRelation.Id, 2026, 1, 2_000),
            new AnnualHourlyRate(noIncomeRelation.Id, 2026, 1, 500));
        await db.SaveChangesAsync();

        var income = new IncomeApplicationService(db);
        foreach (var relationId in new[] { sapuRelation.Id, lorenzoRelation.Id, sarRelation.Id })
        {
            await income.AddInstitutionToPeriodAsync(actor, 2026, 9, relationId, CancellationToken.None);
            await income.AddHourRecordAsync(actor, 2026, 9, relationId, 1, CancellationToken.None);
        }

        var dashboard = await income.GetDashboardAsync(actor, null, 2026, 2026, null, CancellationToken.None);
        var dashboardMonth = Assert.Single(dashboard.Months, x => x.Month == 9);
        Assert.Equal(588_543, dashboardMonth.TotalNetClp);
        Assert.Equal(3, dashboard.Institutions.Count);
        Assert.Contains(dashboard.Institutions, x => x.Type == "public" && x.Name == "SAPU Lorenzo Arenas");
        Assert.Contains(dashboard.Institutions, x => x.Type == "public" && x.Name == "SAR TUCAPEL");
        Assert.Contains(dashboard.Institutions, x => x.Type == "private" && x.Name == "Sanatorio Alemán");
        Assert.DoesNotContain(dashboard.Institutions, x => x.Name == "Hospital sin ingresos");
        var sanatorioColumn = dashboard.Institutions.Single(x => x.Type == "private");
        var institutionValues = dashboardMonth.Institutions.ToDictionary(x => x.InstitutionKey, x => x.NetTotalClp);
        Assert.Equal(1_694, institutionValues["public:alias:sapu-lorenzo-arenas"]);
        Assert.Equal(1_695, institutionValues["public:alias:sar-tucapel"]);
        Assert.Equal(585_154, institutionValues[sanatorioColumn.Key]);

        var filteredDashboard = await income.GetDashboardAsync(actor, null, 2026, 2026,
            [sanatorioColumn.Key], CancellationToken.None);
        Assert.Equal(sanatorioColumn.Key, Assert.Single(filteredDashboard.Institutions).Key);
        Assert.Equal(588_543, Assert.Single(filteredDashboard.Months, x => x.Month == 9).TotalNetClp);

        var duplicate = await Assert.ThrowsAsync<AppError>(() =>
            service.PreviewAsync(actor, new MemoryStream(firstFile), firstFile.Length, "first.pdf", 12, CancellationToken.None));
        Assert.Equal(409, duplicate.StatusCode);

        var correctedFile = Encoding.ASCII.GetBytes("%PDF-1.7\nFIRST-SAMPLE-CORRECTED-EXPORT");
        var businessKeyConflict = await Assert.ThrowsAsync<AppError>(() =>
            service.PreviewAsync(actor, new MemoryStream(correctedFile), correctedFile.Length, "first-corrected.pdf", 12, CancellationToken.None));
        Assert.Equal(409, businessKeyConflict.StatusCode);

        await service.DeleteAsync(actor, first.Id, CancellationToken.None);
        Assert.Equal(1, await db.PrivateLiquidations.CountAsync());
        Assert.Single(files.Files);
        Assert.Equal(73_460, period.PrivateGrossTotalClp);
        Assert.Equal(11_203, period.PrivateRetentionTotalClp);
        Assert.Equal(62_257, period.PrivateNetTotalClp);
        Assert.Equal(4, period.PrivateAttentionCount);
        Assert.Equal(80L, period.PrivateAttentionMinutes);

        var replacementPreview = await service.PreviewAsync(actor, new MemoryStream(correctedFile), correctedFile.Length,
            "first-corrected.pdf", 12, CancellationToken.None);
        await service.ImportAsync(actor, new MemoryStream(correctedFile), correctedFile.Length,
            "first-corrected.pdf", 12, replacementPreview.Sha256, replacementPreview.AppliedRetentionPercentage, CancellationToken.None);
        Assert.Equal(2, await db.PrivateLiquidations.CountAsync());
        Assert.Equal(690_448, period.PrivateGrossTotalClp);
        Assert.Equal(47, period.PrivateAttentionCount);
        Assert.Equal(3_389, period.NetTotalClp);
    }

    [Fact]
    public async Task RejectsUnknownPayerAndMismatchedCollectorWithoutPersistingFiles()
    {
        await using var db = CreateContext();
        var files = new MemoryPrivateLiquidationStorage();
        var actor = new ActorContext(UserId, ProfessionalId, IsAdministrator: false, IsProfessional: true);
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nSAMPLE");
        var unknownPayerService = CreateService(db, files, new SamplePrivateLiquidationParser(unknownPayer: true));

        var unknownPayer = await Assert.ThrowsAsync<AppError>(() =>
            unknownPayerService.PreviewAsync(actor, new MemoryStream(bytes), bytes.Length, "unknown.pdf", 15, CancellationToken.None));
        Assert.Equal(409, unknownPayer.StatusCode);
        Assert.Empty(files.Files);

        var zeroMinutes = await Assert.ThrowsAsync<AppError>(() =>
            unknownPayerService.PreviewAsync(actor, new MemoryStream(bytes), bytes.Length, "zero-minutes.pdf", 0, CancellationToken.None));
        Assert.Equal(400, zeroMinutes.StatusCode);
        Assert.Empty(files.Files);

        var negativeMinutes = await Assert.ThrowsAsync<AppError>(() =>
            unknownPayerService.PreviewAsync(actor, new MemoryStream(bytes), bytes.Length, "negative-minutes.pdf", -1, CancellationToken.None));
        Assert.Equal(400, negativeMinutes.StatusCode);
        Assert.Empty(files.Files);

        var tooLargeFile = new byte[1_048_577];
        "%PDF-"u8.CopyTo(tooLargeFile);
        var tooLarge = await Assert.ThrowsAsync<AppError>(() =>
            unknownPayerService.PreviewAsync(actor, new MemoryStream(tooLargeFile), tooLargeFile.Length, "too-large.pdf", 15, CancellationToken.None));
        Assert.Equal(400, tooLarge.StatusCode);
        Assert.Empty(files.Files);

        var wrongCollectorService = CreateService(db, files, new SamplePrivateLiquidationParser(collectorRut: "17.123.456-5"));
        var wrongCollector = await Assert.ThrowsAsync<AppError>(() =>
            wrongCollectorService.PreviewAsync(actor, new MemoryStream(bytes), bytes.Length, "wrong-owner.pdf", 15, CancellationToken.None));
        Assert.Equal(409, wrongCollector.StatusCode);
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task IntegerMinuteMigrationRejectsFractionalHistoricalValuesWithoutRounding()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync("20261007025339_AddReportedAttentionCountToPrivateLiquidations");

        var period = new MonthlyPeriod(ProfessionalId, 2026, 9, 15.25m);
        db.MonthlyPeriods.Add(period);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE periodos_mensuales SET minutos_atencion_privados = 2.5 WHERE id = {period.Id}");

        var migrationFailure = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
        Assert.Contains("No se pueden migrar los minutos", migrationFailure.ToString());

        var preservedFraction = await db.Database.SqlQuery<decimal>(
            $"SELECT minutos_atencion_privados AS \"Value\" FROM periodos_mensuales WHERE id = {period.Id}")
            .SingleAsync();
        Assert.Equal(2.5m, preservedFraction);
    }

    private PrivateLiquidationApplicationService CreateService(
        AppDbContext db, IPrivateLiquidationFileStorage storage, IPrivateLiquidationPdfParser parser) =>
        new(db, parser, storage, NullLogger<PrivateLiquidationApplicationService>.Instance);

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options);

    private sealed class SamplePrivateLiquidationParser(bool unknownPayer = false, string collectorRut = "19.091.616-2")
        : IPrivateLiquidationPdfParser
    {
        public async Task<ParsedPrivateLiquidation> ParseAsync(Stream pdf, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(pdf, Encoding.ASCII, leaveOpen: true);
            var input = await reader.ReadToEndAsync(cancellationToken);
            var isFirst = input.Contains("FIRST-SAMPLE", StringComparison.Ordinal);
            var gross = isFirst ? 616_988L : 73_460L;
            return new ParsedPrivateLiquidation(
                unknownPayer ? "12345678-5" : isFirst ? "76389986-1" : "88611600-4",
                collectorRut,
                isFirst ? "220081" : "220080",
                new DateOnly(2026, 9, 22),
                2026,
                9,
                1,
                "CONSULTAS MÉDICAS",
                "EJECUTOR DE PRUEBA",
                gross,
                gross,
                isFirst ? 43 : 4,
                isFirst ? 43 : 4,
                gross);
        }
    }

    private sealed class MemoryPrivateLiquidationStorage : IPrivateLiquidationFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public async Task StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Files.Add(storageKey, buffer.ToArray());
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new MemoryStream(Files[storageKey], writable: false));
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
