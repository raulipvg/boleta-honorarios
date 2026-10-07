using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestionIngresosHonorarios.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext
{
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<PublicInstitution> PublicInstitutions => Set<PublicInstitution>();
    public DbSet<ProfessionalInstitution> ProfessionalInstitutions => Set<ProfessionalInstitution>();
    public DbSet<AnnualHourlyRate> AnnualHourlyRates => Set<AnnualHourlyRate>();
    public DbSet<MonthlyPeriod> MonthlyPeriods => Set<MonthlyPeriod>();
    public DbSet<PeriodInstitution> PeriodInstitutions => Set<PeriodInstitution>();
    public DbSet<HourRecord> HourRecords => Set<HourRecord>();
    public DbSet<AnnualRetentionRate> AnnualRetentionRates => Set<AnnualRetentionRate>();
    public DbSet<PrivateInstitution> PrivateInstitutions => Set<PrivateInstitution>();
    public DbSet<PrivatePayerEntity> PrivatePayerEntities => Set<PrivatePayerEntity>();
    public DbSet<PrivatePaymentRule> PrivatePaymentRules => Set<PrivatePaymentRule>();
    public DbSet<PrivateLiquidation> PrivateLiquidations => Set<PrivateLiquidation>();
    public DbSet<AuthSessionRecord> AuthSessions => Set<AuthSessionRecord>();
    public DbSet<RotatedRefreshTokenRecord> RotatedRefreshTokens => Set<RotatedRefreshTokenRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ConfigureIdentity(builder);
        ConfigureDomain(builder);
        ConfigureSessions(builder);
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("usuarios", table =>
            {
                table.HasCheckConstraint("ck_usuarios_user_name_not_blank", "length(btrim(user_name)) > 0");
                table.HasCheckConstraint("ck_usuarios_access_failed_count", "access_failed_count >= 0");
            });
            entity.Property(user => user.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(user => user.UserName).HasColumnName("user_name").HasMaxLength(256).IsRequired();
            entity.Property(user => user.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256).IsRequired();
            entity.HasIndex(user => user.NormalizedUserName).IsUnique();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256);
            entity.HasIndex(user => user.NormalizedEmail).IsUnique().HasFilter("normalized_email IS NOT NULL");
            entity.Property(user => user.EmailConfirmed).HasColumnName("email_confirmed").HasDefaultValue(false);
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
            entity.Property(user => user.SecurityStamp).HasColumnName("security_stamp").HasColumnType("text").IsRequired();
            entity.Property(user => user.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasColumnType("text").IsConcurrencyToken().IsRequired();
            entity.Property(user => user.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
            entity.Property(user => user.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed").HasDefaultValue(false);
            entity.Property(user => user.TwoFactorEnabled).HasColumnName("two_factor_enabled").HasDefaultValue(false);
            entity.Property(user => user.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(user => user.LockoutEnabled).HasColumnName("lockout_enabled").HasDefaultValue(true);
            entity.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count").HasDefaultValue(0);
            entity.Property(user => user.Active).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(user => user.MustChangePassword).HasColumnName("cambio_contrasena_requerido").HasDefaultValue(false);
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("roles", table => table.HasCheckConstraint(
                "ck_roles_codigo", "codigo IN ('ADMINISTRADOR', 'PROFESIONAL')"));
            entity.Property(role => role.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(role => role.Code).HasColumnName("codigo").HasMaxLength(50).IsRequired();
            entity.HasIndex(role => role.Code).IsUnique();
            entity.Property(role => role.Name).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(role => role.NormalizedName).HasColumnName("normalized_name").HasMaxLength(100).IsRequired();
            entity.HasIndex(role => role.NormalizedName).IsUnique();
            entity.Property(role => role.ConcurrencyStamp).HasColumnName("concurrency_stamp").HasColumnType("text").IsRequired();
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("usuarios_roles");
            entity.Property(role => role.UserId).HasColumnName("usuario_id");
            entity.Property(role => role.RoleId).HasColumnName("rol_id");
            entity.HasKey(role => new { role.UserId, role.RoleId });
            entity.Property<DateTimeOffset>("AssignedAt").HasColumnName("asignado_at").ValueGeneratedOnAdd().HasDefaultValueSql("now()");
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("usuarios_claims");
            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.UserId).HasColumnName("usuario_id");
            entity.Property(claim => claim.ClaimType).HasColumnName("claim_type");
            entity.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("usuarios_logins");
            entity.Property(login => login.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
            entity.Property(login => login.ProviderKey).HasColumnName("provider_key").HasMaxLength(128);
            entity.Property(login => login.ProviderDisplayName).HasColumnName("provider_display_name");
            entity.Property(login => login.UserId).HasColumnName("usuario_id");
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("usuarios_tokens");
            entity.Property(token => token.UserId).HasColumnName("usuario_id");
            entity.Property(token => token.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
            entity.Property(token => token.Name).HasColumnName("nombre").HasMaxLength(128);
            entity.Property(token => token.Value).HasColumnName("valor");
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("roles_claims");
            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.RoleId).HasColumnName("rol_id");
            entity.Property(claim => claim.ClaimType).HasColumnName("claim_type");
            entity.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
        });
    }

    private static void ConfigureDomain(ModelBuilder builder)
    {
        builder.Entity<Professional>(entity =>
        {
            entity.ToTable("profesionales", table => table.HasCheckConstraint(
                "ck_profesionales_nombre_not_blank", "length(btrim(nombre)) > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Rut).HasColumnName("rut").HasMaxLength(10);
            entity.HasIndex(x => x.Rut).IsUnique().HasFilter("rut IS NOT NULL");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PublicInstitution>(entity =>
        {
            entity.ToTable("instituciones_publicas", table => table.HasCheckConstraint(
                "ck_instituciones_publicas_nombre_not_blank", "length(btrim(nombre)) > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(x => x.NormalizedName).HasColumnName("nombre_normalizado")
                .HasColumnType("text")
                .HasComputedColumnSql("normalize_public_institution_name(nombre)", stored: true);
            entity.Property(x => x.Active).HasColumnName("activa").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.Active, x.Name });
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        builder.Entity<ProfessionalInstitution>(entity =>
        {
            entity.ToTable("profesionales_instituciones");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.ProfessionalId });
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ProfessionalId).HasColumnName("profesional_id");
            entity.Property(x => x.PublicInstitutionId).HasColumnName("institucion_publica_id");
            entity.Property(x => x.Active).HasColumnName("activa").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ProfessionalId, x.PublicInstitutionId }).IsUnique();
            entity.HasIndex(x => new { x.ProfessionalId, x.Active });
            entity.HasOne<Professional>().WithMany().HasForeignKey(x => x.ProfessionalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PublicInstitution>().WithMany().HasForeignKey(x => x.PublicInstitutionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AnnualHourlyRate>(entity =>
        {
            entity.ToTable("tarifas_hora_anuales_versiones", table =>
            {
                table.HasCheckConstraint("ck_tarifas_hora_anio", "anio BETWEEN 1900 AND 32767");
                table.HasCheckConstraint("ck_tarifas_hora_version", "version >= 1");
                table.HasCheckConstraint("ck_tarifas_hora_valor_no_negativo", "valor_hora_clp >= 0");
                table.HasCheckConstraint("ck_tarifas_hora_clp_json_exact", "valor_hora_clp <= 9007199254740991");
            });
            entity.HasKey(x => new { x.ProfessionalInstitutionId, x.Year, x.Version });
            entity.Property(x => x.ProfessionalInstitutionId).HasColumnName("profesional_institucion_id");
            entity.Property(x => x.Year).HasColumnName("anio").ValueGeneratedNever();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.HourlyRateClp).HasColumnName("valor_hora_clp");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne<ProfessionalInstitution>().WithMany().HasForeignKey(x => x.ProfessionalInstitutionId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AnnualRetentionRate>(entity =>
        {
            entity.ToTable("retencion_boleta_honorarios", table =>
            {
                table.HasCheckConstraint("ck_retencion_boleta_anio", "anio BETWEEN 1900 AND 32767");
                table.HasCheckConstraint("ck_retencion_boleta_porcentaje", "porcentaje BETWEEN 0 AND 100");
            });
            entity.HasKey(x => x.Year);
            entity.Property(x => x.Year).HasColumnName("anio").ValueGeneratedNever();
            entity.Property(x => x.Percentage).HasColumnName("porcentaje").HasPrecision(7, 4);
        });

        builder.Entity<MonthlyPeriod>(entity =>
        {
            entity.ToTable("periodos_mensuales", table =>
            {
                table.HasCheckConstraint("ck_periodos_mensuales_mes", "mes BETWEEN 1 AND 12");
                table.HasCheckConstraint("ck_periodos_mensuales_retencion_aplicada", "retencion_porcentaje_aplicado BETWEEN 0 AND 100");
                table.HasCheckConstraint("ck_periodos_mensuales_totales_no_negativos", "total_horas >= 0 AND bruto_total_clp >= 0 AND retencion_total_clp >= 0 AND liquido_total_clp >= 0");
                table.HasCheckConstraint("ck_periodos_mensuales_json_exact", "total_horas <= 9007199254740991 AND bruto_total_clp <= 9007199254740991 AND retencion_total_clp <= 9007199254740991 AND liquido_total_clp <= 9007199254740991");
                table.HasCheckConstraint("ck_periodos_mensuales_liquido_consistente", "retencion_total_clp <= bruto_total_clp AND liquido_total_clp = bruto_total_clp - retencion_total_clp");
                table.HasCheckConstraint("ck_periodos_mensuales_version", "version >= 1");
                table.HasCheckConstraint("ck_periodos_mensuales_privados_no_negativos", "bruto_privado_total_clp >= 0 AND retencion_privada_total_clp >= 0 AND liquido_privado_total_clp >= 0 AND atenciones_privadas >= 0 AND minutos_atencion_privados BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_periodos_mensuales_privados_liquido", "retencion_privada_total_clp <= bruto_privado_total_clp AND liquido_privado_total_clp = bruto_privado_total_clp - retencion_privada_total_clp");
                table.HasCheckConstraint("ck_periodos_mensuales_privados_json_exact", "bruto_privado_total_clp <= 9007199254740991 AND retencion_privada_total_clp <= 9007199254740991 AND liquido_privado_total_clp <= 9007199254740991 AND atenciones_privadas <= 9007199254740991");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.ProfessionalId, x.Year });
            entity.HasAlternateKey(x => new { x.Id, x.ProfessionalId, x.Year, x.Month });
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ProfessionalId).HasColumnName("profesional_id");
            entity.Property(x => x.Year).HasColumnName("anio");
            entity.Property(x => x.Month).HasColumnName("mes");
            entity.Property(x => x.AppliedRetentionPercentage).HasColumnName("retencion_porcentaje_aplicado").HasPrecision(7, 4);
            entity.Property(x => x.TotalHours).HasColumnName("total_horas");
            entity.Property(x => x.GrossTotalClp).HasColumnName("bruto_total_clp");
            entity.Property(x => x.RetentionTotalClp).HasColumnName("retencion_total_clp");
            entity.Property(x => x.NetTotalClp).HasColumnName("liquido_total_clp");
            entity.Property(x => x.PrivateGrossTotalClp).HasColumnName("bruto_privado_total_clp").HasDefaultValue(0L);
            entity.Property(x => x.PrivateRetentionTotalClp).HasColumnName("retencion_privada_total_clp").HasDefaultValue(0L);
            entity.Property(x => x.PrivateNetTotalClp).HasColumnName("liquido_privado_total_clp").HasDefaultValue(0L);
            entity.Property(x => x.PrivateAttentionCount).HasColumnName("atenciones_privadas").HasDefaultValue(0L);
            entity.Property(x => x.PrivateAttentionMinutes).HasColumnName("minutos_atencion_privados").HasColumnType("bigint").HasDefaultValue(0L);
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ProfessionalId, x.Year, x.Month }).IsUnique();
            entity.HasOne<Professional>().WithMany().HasForeignKey(x => x.ProfessionalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AnnualRetentionRate>().WithMany().HasForeignKey(x => x.Year).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigurePrivateIncome(builder);

        builder.Entity<PeriodInstitution>(entity =>
        {
            entity.ToTable("periodos_instituciones", table =>
            {
                table.HasCheckConstraint("ck_periodos_instituciones_totales_no_negativos", "total_horas >= 0 AND bruto_total_clp >= 0 AND retencion_total_clp >= 0 AND liquido_total_clp >= 0");
                table.HasCheckConstraint("ck_periodos_instituciones_json_exact", "total_horas <= 9007199254740991 AND bruto_total_clp <= 9007199254740991 AND retencion_total_clp <= 9007199254740991 AND liquido_total_clp <= 9007199254740991");
                table.HasCheckConstraint("ck_periodos_instituciones_liquido_consistente", "retencion_total_clp <= bruto_total_clp AND liquido_total_clp = bruto_total_clp - retencion_total_clp");
                table.HasCheckConstraint("ck_periodos_instituciones_version", "version >= 1");
                table.HasCheckConstraint("ck_periodos_instituciones_orden", "orden >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PeriodId).HasColumnName("periodo_id");
            entity.Property(x => x.ProfessionalId).HasColumnName("profesional_id");
            entity.Property(x => x.Year).HasColumnName("anio");
            entity.Property(x => x.ProfessionalInstitutionId).HasColumnName("profesional_institucion_id");
            entity.Property(x => x.HourlyRateVersion).HasColumnName("tarifa_hora_version");
            entity.Property(x => x.TotalHours).HasColumnName("total_horas");
            entity.Property(x => x.GrossTotalClp).HasColumnName("bruto_total_clp");
            entity.Property(x => x.RetentionTotalClp).HasColumnName("retencion_total_clp");
            entity.Property(x => x.NetTotalClp).HasColumnName("liquido_total_clp");
            entity.Property(x => x.Order).HasColumnName("orden").HasDefaultValue(0);
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.PeriodId, x.ProfessionalInstitutionId }).IsUnique();
            entity.HasIndex(x => new { x.ProfessionalId, x.Year });
            entity.HasOne<MonthlyPeriod>().WithMany(x => x.Institutions)
                .HasForeignKey(x => new { x.PeriodId, x.ProfessionalId, x.Year })
                .HasPrincipalKey(x => new { x.Id, x.ProfessionalId, x.Year }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProfessionalInstitution>().WithMany()
                .HasForeignKey(x => new { x.ProfessionalInstitutionId, x.ProfessionalId })
                .HasPrincipalKey(x => new { x.Id, x.ProfessionalId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AnnualHourlyRate>().WithMany()
                .HasForeignKey(x => new { x.ProfessionalInstitutionId, x.Year, x.HourlyRateVersion })
                .HasPrincipalKey(x => new { x.ProfessionalInstitutionId, x.Year, x.Version }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HourRecord>(entity =>
        {
            entity.ToTable("registros_horas", table =>
            {
                table.HasCheckConstraint("ck_registros_horas_minimo_uno", "horas >= 1");
                table.HasCheckConstraint("ck_registros_horas_version", "version >= 1");
                table.HasCheckConstraint("ck_registros_horas_orden", "orden >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PeriodInstitutionId).HasColumnName("periodo_institucion_id");
            entity.Property(x => x.Hours).HasColumnName("horas");
            entity.Property(x => x.Order).HasColumnName("orden");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.PeriodInstitutionId, x.Order }).IsUnique();
            entity.HasOne<PeriodInstitution>().WithMany(x => x.HourRecords).HasForeignKey(x => x.PeriodInstitutionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSessions(ModelBuilder builder)
    {
        builder.Entity<AuthSessionRecord>(entity =>
        {
            entity.ToTable("sesiones_auth", table => table.HasCheckConstraint(
                "ck_sesiones_auth_expiraciones", "expira_inactividad_at <= expira_absoluta_at"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.UserId).HasColumnName("usuario_id");
            entity.Property(x => x.Sid).HasColumnName("sid");
            entity.Property(x => x.RefreshFamilyId).HasColumnName("familia_refresh_id");
            entity.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(128);
            entity.Property(x => x.CreatedAt).HasColumnName("creada_at").HasDefaultValueSql("now()");
            entity.Property(x => x.LastUsedAt).HasColumnName("ultimo_uso_at").HasDefaultValueSql("now()");
            entity.Property(x => x.IdleExpiresAt).HasColumnName("expira_inactividad_at");
            entity.Property(x => x.AbsoluteExpiresAt).HasColumnName("expira_absoluta_at");
            entity.Property(x => x.RevokedAt).HasColumnName("revocada_at");
            entity.HasIndex(x => x.Sid).IsUnique();
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.AbsoluteExpiresAt }).HasFilter("revocada_at IS NULL");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RotatedRefreshTokenRecord>(entity =>
        {
            entity.ToTable("sesiones_refresh_rotados");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.SessionId).HasColumnName("sesion_id");
            entity.Property(x => x.RefreshFamilyId).HasColumnName("familia_refresh_id");
            entity.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(128);
            entity.Property(x => x.RotatedAt).HasColumnName("rotado_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasIndex(x => x.RefreshFamilyId);
            entity.HasOne<AuthSessionRecord>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePrivateIncome(ModelBuilder builder)
    {
        var sanatorioAlemánId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660001");
        var serviciosPayerId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660002");
        var clinicaPayerId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660003");
        var participationsRuleId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660004");
        var centroCebienId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660005");
        var cebienPayerId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660006");
        var cebienEmailRuleId = Guid.Parse("8d1040c1-3da3-42fd-842a-c87eba660007");
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.Entity<PrivateInstitution>(entity =>
        {
            entity.ToTable("instituciones_privadas", table => table.HasCheckConstraint(
                "ck_instituciones_privadas_nombre_no_vacio", "length(btrim(nombre)) > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Active).HasColumnName("activa").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasData(
                new
                {
                    Id = sanatorioAlemánId,
                    Name = "Sanatorio Alemán",
                    Active = true,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new
                {
                    Id = centroCebienId,
                    Name = "Centro Cebien",
                    Active = true,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                });
        });

        builder.Entity<PrivatePayerEntity>(entity =>
        {
            entity.ToTable("entidades_pagadoras_privadas", table =>
            {
                table.HasCheckConstraint("ck_entidades_pagadoras_privadas_nombre_no_vacio", "length(btrim(razon_social)) > 0");
                table.HasCheckConstraint("ck_entidades_pagadoras_privadas_rut_no_vacio", "length(btrim(rut)) > 0");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.PrivateInstitutionId });
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PrivateInstitutionId).HasColumnName("institucion_privada_id");
            entity.Property(x => x.Rut).HasColumnName("rut").HasMaxLength(10).IsRequired();
            entity.Property(x => x.LegalName).HasColumnName("razon_social").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Active).HasColumnName("activa").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Rut).IsUnique();
            entity.HasIndex(x => new { x.PrivateInstitutionId, x.Active });
            entity.HasOne<PrivateInstitution>().WithMany().HasForeignKey(x => x.PrivateInstitutionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasData(
                new
                {
                    Id = serviciosPayerId,
                    PrivateInstitutionId = sanatorioAlemánId,
                    Rut = "76389986-1",
                    LegalName = "SERVICIOS SANATORIO ALEMAN SPA",
                    Active = true,
                    CreatedAt = seedDate
                },
                new
                {
                    Id = clinicaPayerId,
                    PrivateInstitutionId = sanatorioAlemánId,
                    Rut = "88611600-4",
                    LegalName = "CLINICA SANATORIO ALEMAN SPA",
                    Active = true,
                    CreatedAt = seedDate
                },
                new
                {
                    Id = cebienPayerId,
                    PrivateInstitutionId = centroCebienId,
                    Rut = "76015783-K",
                    LegalName = "DR. BENJAMIN VICENTE PARADA Y CIA. LIMITADA",
                    Active = true,
                    CreatedAt = seedDate
                });
        });

        builder.Entity<PrivatePaymentRule>(entity =>
        {
            entity.ToTable("reglas_pago_privadas", table => table.HasCheckConstraint(
                "ck_reglas_pago_privadas_version", "version >= 1"));
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.PrivateInstitutionId });
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PrivateInstitutionId).HasColumnName("institucion_privada_id");
            entity.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.Active).HasColumnName("activa").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.PrivateInstitutionId, x.Code, x.Version }).IsUnique();
            entity.HasOne<PrivateInstitution>().WithMany().HasForeignKey(x => x.PrivateInstitutionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasData(
                new
                {
                    Id = participationsRuleId,
                    PrivateInstitutionId = sanatorioAlemánId,
                    Code = "SANATORIO_ALEMAN_PARTICIPACIONES",
                    Version = 1,
                    Active = true,
                    CreatedAt = seedDate
                },
                new
                {
                    Id = cebienEmailRuleId,
                    PrivateInstitutionId = centroCebienId,
                    Code = "CENTRO_CEBIEN_EMAIL",
                    Version = 1,
                    Active = true,
                    CreatedAt = seedDate
                });
        });

        builder.Entity<PrivateLiquidation>(entity =>
        {
            entity.ToTable("liquidaciones_privadas", table =>
            {
                table.HasCheckConstraint("ck_liquidaciones_privadas_anio", "anio BETWEEN 1900 AND 32767");
                table.HasCheckConstraint("ck_liquidaciones_privadas_mes", "mes BETWEEN 1 AND 12");
                table.HasCheckConstraint("ck_liquidaciones_privadas_anio_contable", "anio_contable BETWEEN 1900 AND 32767");
                table.HasCheckConstraint("ck_liquidaciones_privadas_mes_contable", "mes_contable BETWEEN 1 AND 12");
                table.HasCheckConstraint("ck_liquidaciones_privadas_quincena", "quincena IS NULL OR quincena IN (1, 2)");
                table.HasCheckConstraint("ck_liquidaciones_privadas_importes_no_negativos", "bruto_total_clp >= 0 AND retencion_total_clp >= 0 AND liquido_total_clp >= 0 AND (total_servicio_clp IS NULL OR total_servicio_clp >= 0)");
                table.HasCheckConstraint("ck_liquidaciones_privadas_liquido_consistente", "retencion_total_clp <= bruto_total_clp AND liquido_total_clp = bruto_total_clp - retencion_total_clp");
                table.HasCheckConstraint("ck_liquidaciones_privadas_json_exact", "bruto_total_clp <= 9007199254740991 AND retencion_total_clp <= 9007199254740991 AND liquido_total_clp <= 9007199254740991 AND atenciones <= 9007199254740991");
                table.HasCheckConstraint("ck_liquidaciones_privadas_atenciones", "atenciones > 0");
                table.HasCheckConstraint("ck_liquidaciones_privadas_atenciones_reportadas", "atenciones_reportadas_pdf IS NULL OR atenciones_reportadas_pdf >= 0");
                table.HasCheckConstraint("ck_liquidaciones_privadas_minutos", "minutos_por_atencion >= 1 AND minutos_totales BETWEEN 0 AND 9007199254740991");
                table.HasCheckConstraint("ck_liquidaciones_privadas_archivo", "tamano_archivo_bytes IS NULL OR tamano_archivo_bytes BETWEEN 1 AND 1048576");
                table.HasCheckConstraint("ck_liquidaciones_privadas_origen", "(tipo_fuente = 1 AND rut_cobrador IS NOT NULL AND numero_liquidacion IS NOT NULL AND fecha_liquidacion IS NOT NULL AND quincena IS NOT NULL AND ejecutor IS NOT NULL AND storage_key IS NOT NULL AND nombre_archivo IS NOT NULL AND tamano_archivo_bytes IS NOT NULL AND nombre_profesional_informado IS NULL AND cuerpo_fuente IS NULL AND clave_negocio IS NULL) OR (tipo_fuente = 2 AND rut_cobrador IS NULL AND numero_liquidacion IS NULL AND fecha_liquidacion IS NULL AND quincena IS NULL AND ejecutor IS NULL AND total_servicio_clp IS NULL AND atenciones_reportadas_pdf IS NULL AND storage_key IS NULL AND nombre_archivo IS NULL AND tamano_archivo_bytes IS NULL AND length(btrim(nombre_profesional_informado)) > 0 AND length(btrim(cuerpo_fuente)) > 0 AND clave_negocio IS NOT NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PeriodId).HasColumnName("periodo_id");
            entity.Property(x => x.ProfessionalId).HasColumnName("profesional_id");
            entity.Property(x => x.ServiceYear).HasColumnName("anio");
            entity.Property(x => x.ServiceMonth).HasColumnName("mes");
            entity.Property(x => x.AccountingYear).HasColumnName("anio_contable");
            entity.Property(x => x.AccountingMonth).HasColumnName("mes_contable");
            entity.Property(x => x.Fortnight).HasColumnName("quincena");
            entity.Property(x => x.SourceType).HasColumnName("tipo_fuente").HasColumnType("smallint").HasConversion<short>().HasDefaultValue(PrivateLiquidationSourceType.Pdf);
            entity.Property(x => x.PrivateInstitutionId).HasColumnName("institucion_privada_id");
            entity.Property(x => x.PayerEntityId).HasColumnName("entidad_pagadora_id");
            entity.Property(x => x.PaymentRuleId).HasColumnName("regla_pago_id");
            entity.Property(x => x.CollectorRut).HasColumnName("rut_cobrador").HasMaxLength(10);
            entity.Property(x => x.ReportedProfessionalName).HasColumnName("nombre_profesional_informado").HasMaxLength(200);
            entity.Property(x => x.LiquidationNumber).HasColumnName("numero_liquidacion").HasMaxLength(50);
            entity.Property(x => x.LiquidationDate).HasColumnName("fecha_liquidacion").HasColumnType("date");
            entity.Property(x => x.PaymentService).HasColumnName("servicio_pago").HasMaxLength(200);
            entity.Property(x => x.ExecutorName).HasColumnName("ejecutor").HasMaxLength(200);
            entity.Property(x => x.ServiceTotalClp).HasColumnName("total_servicio_clp");
            entity.Property(x => x.GrossTotalClp).HasColumnName("bruto_total_clp");
            entity.Property(x => x.RetentionTotalClp).HasColumnName("retencion_total_clp");
            entity.Property(x => x.NetTotalClp).HasColumnName("liquido_total_clp");
            entity.Property(x => x.AttentionCount).HasColumnName("atenciones");
            entity.Property(x => x.ReportedAttentionCount).HasColumnName("atenciones_reportadas_pdf");
            entity.Property(x => x.MinutesPerAttention).HasColumnName("minutos_por_atencion").HasColumnType("integer");
            entity.Property(x => x.TotalAttentionMinutes).HasColumnName("minutos_totales").HasColumnType("bigint");
            entity.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("character(64)").IsRequired();
            entity.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(100);
            entity.Property(x => x.OriginalFileName).HasColumnName("nombre_archivo").HasMaxLength(255);
            entity.Property(x => x.FileSizeBytes).HasColumnName("tamano_archivo_bytes");
            entity.Property(x => x.SourceBody).HasColumnName("cuerpo_fuente").HasColumnType("text");
            entity.Property(x => x.BusinessKey).HasColumnName("clave_negocio").HasColumnType("character(64)");
            entity.Property(x => x.ImportedAt).HasColumnName("importada_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ProfessionalId, x.Sha256 }).IsUnique();
            entity.HasIndex(x => new { x.ProfessionalId, x.PayerEntityId, x.LiquidationNumber, x.ServiceYear, x.ServiceMonth, x.Fortnight }).IsUnique();
            entity.HasIndex(x => new { x.ProfessionalId, x.AccountingYear, x.AccountingMonth });
            entity.HasIndex(x => new { x.ProfessionalId, x.BusinessKey }).IsUnique().HasFilter("tipo_fuente = 2 AND clave_negocio IS NOT NULL");
            entity.HasOne<Professional>().WithMany().HasForeignKey(x => x.ProfessionalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MonthlyPeriod>().WithMany()
                .HasForeignKey(x => new { x.PeriodId, x.ProfessionalId, x.AccountingYear, x.AccountingMonth })
                .HasPrincipalKey(x => new { x.Id, x.ProfessionalId, x.Year, x.Month }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PrivatePayerEntity>().WithMany()
                .HasForeignKey(x => new { x.PayerEntityId, x.PrivateInstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.PrivateInstitutionId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PrivatePaymentRule>().WithMany()
                .HasForeignKey(x => new { x.PaymentRuleId, x.PrivateInstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.PrivateInstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
