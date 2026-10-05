using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GestionIngresosHonorarios.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIncomeApplicationService, IncomeApplicationService>();
        return services;
    }
}
