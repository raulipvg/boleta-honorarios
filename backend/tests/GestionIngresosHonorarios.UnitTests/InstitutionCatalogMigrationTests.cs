using GestionIngresosHonorarios.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace GestionIngresosHonorarios.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class InstitutionCatalogMigrationTests
{
    [Fact]
    public async Task ConsolidatesEquivalentCatalogRowsAndPreservesProfessionalRelations()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("institution_cleanup_test")
            .WithUsername("integration_owner")
            .WithPassword("Integration-only-passphrase-2026")
            .Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261005173446_InitialCreate");

        var canonicalInstitutionId = Guid.NewGuid();
        var duplicateInstitutionId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var firstProfessionalId = Guid.NewGuid();
        var secondProfessionalId = Guid.NewGuid();
        var firstRelationId = Guid.NewGuid();
        var secondRelationId = Guid.NewGuid();
        const string canonicalName = "  HÓSPITAL   Norte  ";
        const string duplicateName = "Hospital-Norte";

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO instituciones_publicas (id, nombre, activa)
            VALUES ({canonicalInstitutionId}, {canonicalName}, TRUE),
                   ({duplicateInstitutionId}, {duplicateName}, FALSE)
            """);
        await InsertProfessionalAsync(db, firstUserId, firstProfessionalId, "primero");
        await InsertProfessionalAsync(db, secondUserId, secondProfessionalId, "segundo");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO profesionales_instituciones (id, profesional_id, institucion_publica_id)
            VALUES ({firstRelationId}, {firstProfessionalId}, {canonicalInstitutionId}),
                   ({secondRelationId}, {secondProfessionalId}, {duplicateInstitutionId})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tarifas_hora_anuales_versiones (profesional_institucion_id, anio, version, valor_hora_clp)
            VALUES ({firstRelationId}, {2026}, {1}, {31_488L}),
                   ({secondRelationId}, {2026}, {1}, {35_000L})
            """);

        await migrator.MigrateAsync();

        var institution = await db.PublicInstitutions.AsNoTracking().SingleAsync();
        Assert.Equal(canonicalInstitutionId, institution.Id);
        Assert.Equal("HÓSPITAL Norte", institution.Name);
        Assert.Equal("hospital norte", institution.NormalizedName);

        var relations = await db.ProfessionalInstitutions.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, relations.Count);
        Assert.Contains(relations, relation => relation.Id == firstRelationId && relation.PublicInstitutionId == canonicalInstitutionId);
        Assert.Contains(relations, relation => relation.Id == secondRelationId && relation.PublicInstitutionId == canonicalInstitutionId);
        var rates = await db.AnnualHourlyRates.AsNoTracking().OrderBy(x => x.ProfessionalInstitutionId).ToListAsync();
        Assert.Equal(2, rates.Count);
        Assert.Contains(rates, rate => rate.ProfessionalInstitutionId == firstRelationId && rate.HourlyRateClp == 31_488);
        Assert.Contains(rates, rate => rate.ProfessionalInstitutionId == secondRelationId && rate.HourlyRateClp == 35_000);
    }

    [Fact]
    public async Task RefusesUnsafeMergeAndLeavesDuplicateRelationsUntouched()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("institution_merge_conflict_test")
            .WithUsername("integration_owner")
            .WithPassword("Integration-only-passphrase-2026")
            .Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261005173446_InitialCreate");

        var userId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var firstInstitutionId = Guid.NewGuid();
        var secondInstitutionId = Guid.NewGuid();
        var firstRelationId = Guid.NewGuid();
        var secondRelationId = Guid.NewGuid();
        const string userName = "overlapping-professional";
        const string normalizedUserName = "OVERLAPPING-PROFESSIONAL";
        const string professionalName = "Profesional con relaciones duplicadas";
        const string firstInstitutionName = "Hospital Norte";
        const string secondInstitutionName = "HÓSPITAL-Norte";

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO usuarios (id, user_name, normalized_user_name, password_hash, security_stamp, concurrency_stamp)
            VALUES ({userId}, {userName}, {normalizedUserName}, 'test-hash', 'test-security', 'test-concurrency')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO profesionales (id, usuario_id, nombre)
            VALUES ({professionalId}, {userId}, {professionalName})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO instituciones_publicas (id, nombre, activa)
            VALUES ({firstInstitutionId}, {firstInstitutionName}, TRUE),
                   ({secondInstitutionId}, {secondInstitutionName}, TRUE)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO profesionales_instituciones (id, profesional_id, institucion_publica_id)
            VALUES ({firstRelationId}, {professionalId}, {firstInstitutionId}),
                   ({secondRelationId}, {professionalId}, {secondInstitutionId})
            """);

        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

        Assert.Equal(2, await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM instituciones_publicas").SingleAsync());
        Assert.Equal(2, await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM profesionales_instituciones").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM information_schema.columns
            WHERE table_name = 'instituciones_publicas' AND column_name = 'nombre_normalizado'
            """).SingleAsync());
        Assert.Equal(0, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20261005200202_InstitutionNameNormalization'
            """).SingleAsync());

        var links = await db.Database.SqlQuery<Guid>($"""
            SELECT institucion_publica_id AS "Value"
            FROM profesionales_instituciones
            ORDER BY id
            """).ToListAsync();
        Assert.Contains(firstInstitutionId, links);
        Assert.Contains(secondInstitutionId, links);
    }

    private static async Task InsertProfessionalAsync(AppDbContext db, Guid userId, Guid professionalId, string suffix)
    {
        var userName = "migration-" + suffix;
        var normalizedUserName = userName.ToUpperInvariant();
        var professionalName = "Profesional " + suffix;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO usuarios (id, user_name, normalized_user_name, password_hash, security_stamp, concurrency_stamp)
            VALUES ({userId}, {userName}, {normalizedUserName}, 'test-hash', 'test-security', 'test-concurrency')
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO profesionales (id, usuario_id, nombre)
            VALUES ({professionalId}, {userId}, {professionalName})
            """);
    }
}
