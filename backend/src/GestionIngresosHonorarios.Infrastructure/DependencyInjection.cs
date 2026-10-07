using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Infrastructure.Data;
using GestionIngresosHonorarios.Infrastructure.Identity;
using GestionIngresosHonorarios.Infrastructure.Parsing;
using GestionIngresosHonorarios.Infrastructure.Security;
using GestionIngresosHonorarios.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionIngresosHonorarios.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default es obligatorio.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<GestionIngresosHonorarios.Application.Contracts.IApplicationDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<GestionIngresosHonorarios.Application.Contracts.IPrivateLiquidationPdfParser, SanatorioAlemanLiquidationPdfParser>();
        services.AddSingleton<GestionIngresosHonorarios.Application.Contracts.IPrivateLiquidationFileStorage, LocalPrivateLiquidationFileStorage>();
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 15;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        services.AddScoped<IdentityAccessServices>();
        services.AddScoped<IActorContextProvider>(provider => provider.GetRequiredService<IdentityAccessServices>());
        services.AddScoped<IPermissionResolver>(provider => provider.GetRequiredService<IdentityAccessServices>());
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddScoped<IAccountAdministrationService, AccountAdministrationService>();
        services.AddScoped<IAuthSessionValidator, AuthSessionValidator>();
        return services;
    }
}
