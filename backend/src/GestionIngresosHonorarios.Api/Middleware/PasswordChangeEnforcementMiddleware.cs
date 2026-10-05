namespace GestionIngresosHonorarios.Api.Middleware;

public sealed class PasswordChangeEnforcementMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/password/change",
        "/api/auth/logout",
        "/api/auth/csrf"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var mustChange = context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirst("pwd_change")?.Value == "true";
        if (mustChange && !AllowedPaths.Contains(context.Request.Path.Value ?? string.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "about:blank",
                title = "Cambio de contraseña requerido",
                status = 403,
                detail = "Debe cambiar su contraseña temporal antes de continuar.",
                traceId = context.TraceIdentifier
            });
            return;
        }
        await next(context);
    }
}
