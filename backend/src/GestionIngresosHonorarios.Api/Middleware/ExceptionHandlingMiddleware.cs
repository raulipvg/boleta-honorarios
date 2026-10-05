using GestionIngresosHonorarios.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, title, detail) = Map(exception);
            if (status >= 500) logger.LogError(exception, "Fallo no controlado en la solicitud {TraceId}", context.TraceIdentifier);
            else if (status == 409) logger.LogInformation("Conflicto de solicitud {TraceId}: {ErrorType}", context.TraceIdentifier, exception.GetType().Name);

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            });
        }
    }

    private static (int Status, string Title, string Detail) Map(Exception exception)
    {
        if (exception is AppError appError)
            return (appError.StatusCode, Reason(appError.StatusCode), appError.Message);
        if (exception is DbUpdateConcurrencyException)
            return (409, "Conflicto", "El recurso cambió en otra operación. Recargue los datos y vuelva a intentarlo.");
        if (exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
            return (409, "Conflicto", "La operación coincidió con otro cambio. Recargue los datos y vuelva a intentarlo.");
        if (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            return (409, "Conflicto", "Ya existe un recurso con esos datos.");
        if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } })
            return (409, "Conflicto", "La operación coincidió con otro cambio. Recargue los datos y vuelva a intentarlo.");
        if (exception is AntiforgeryValidationException)
            return (400, "Solicitud inválida", "La validación antifalsificación falló. Recargue la página e inténtelo de nuevo.");
        if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
            return (409, "Conflicto", "Ya existe un recurso con esos datos.");
        if (exception is ArgumentException or ArgumentOutOfRangeException)
            return (400, "Solicitud inválida", exception.Message);
        if (exception is OverflowException)
            return (400, "Monto fuera de rango", "El valor excede el rango exacto permitido para CLP.");
        if (exception is UnauthorizedAccessException)
            return (401, "No autenticado", "La sesión no es válida o ha expirado.");
        return (500, "Error interno", "No fue posible completar la solicitud.");
    }

    private static string Reason(int status) => status switch
    {
        400 => "Solicitud inválida",
        403 => "Acceso denegado",
        404 => "Recurso no encontrado",
        409 => "Conflicto",
        _ => "Error de solicitud"
    };
}
