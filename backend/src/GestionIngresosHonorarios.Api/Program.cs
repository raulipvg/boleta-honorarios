using GestionIngresosHonorarios.Api.Authorization;
using GestionIngresosHonorarios.Api.Middleware;
using GestionIngresosHonorarios.Application;
using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Infrastructure;
using GestionIngresosHonorarios.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Net;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("gestion-ingresos-honorarios");
var dataProtectionPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
if (!builder.Environment.IsDevelopment())
{
    var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
    var privateKeyPath = builder.Configuration["DataProtection:PrivateKeyPath"];
    if (string.IsNullOrWhiteSpace(dataProtectionPath)
        || string.IsNullOrWhiteSpace(certificatePath)
        || string.IsNullOrWhiteSpace(privateKeyPath))
        throw new InvalidOperationException("Producción requiere key ring persistente y certificado de cifrado de Data Protection.");
    var certificate = X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
    dataProtection.ProtectKeysWithCertificate(certificate);
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
        if (IPAddress.TryParse(proxy, out var address)) options.KnownProxies.Add(address);
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [builder.Configuration["Cors:AllowedOrigin"] ?? string.Empty];
allowedOrigins = allowedOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)).Distinct(StringComparer.Ordinal).ToArray();
builder.Services.AddCors(options => options.AddPolicy("Spa", policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowCredentials().WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "X-CSRF-TOKEN", "If-Match");
}));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    // Local Compose binds only to loopback HTTP. Production always requires Secure + __Host-.
    options.Cookie.Name = builder.Environment.IsDevelopment() ? "Antiforgery" : "__Host-Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var jwtIssuer = new JwtTokenIssuer(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(jwtIssuer);
var clientId = builder.Configuration["Jwt:ClientId"] ?? throw new InvalidOperationException("Falta Jwt:ClientId.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = jwtIssuer.CreateValidationParameters();
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            var tokenClientId = principal?.FindFirst("client_id")?.Value;
            var subject = principal?.FindFirst("sub")?.Value;
            if (!string.Equals(tokenClientId, clientId, StringComparison.Ordinal)
                || !Guid.TryParse(subject, out var userId))
            {
                context.Fail("El perfil del token no es válido.");
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var issuedAtText = principal?.FindFirst("iat")?.Value;
            var notBeforeText = principal?.FindFirst("nbf")?.Value;
            var expiresAtText = principal?.FindFirst("exp")?.Value;
            var tokenId = principal?.FindFirst("jti")?.Value;
            if (!long.TryParse(issuedAtText, out var issuedAt)
                || !long.TryParse(notBeforeText, out var notBefore)
                || !long.TryParse(expiresAtText, out var expiresAt)
                || !Guid.TryParse(tokenId, out _)
                || expiresAt <= issuedAt
                || expiresAt - issuedAt > 600
                || issuedAt > now + 30
                || notBefore > now + 30
                || principal!.Claims.Any(claim => claim.Type is "email" or "role" or "permission" or "permissionCodes"))
            {
                context.Fail("Los claims del access token no son válidos.");
                return;
            }

            if (principal?.FindFirst("pwd_change")?.Value == "true") return;
            var sid = principal?.FindFirst("sid")?.Value;
            if (!Guid.TryParse(sid, out var sessionId))
            {
                context.Fail("La sesión asociada al token no es válida.");
                return;
            }
            var validator = context.HttpContext.RequestServices.GetRequiredService<GestionIngresosHonorarios.Application.Contracts.IAuthSessionValidator>();
            if (!await validator.IsActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
                context.Fail("La sesión fue revocada o expiró.");
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    AddPermission(options, PermissionCatalog.UsersRead);
    AddPermission(options, PermissionCatalog.UsersCreate);
    AddPermission(options, PermissionCatalog.UsersResetPassword);
    AddPermission(options, PermissionCatalog.InstitutionsRead);
    AddPermission(options, PermissionCatalog.InstitutionsManage);
    AddPermission(options, PermissionCatalog.ProfileRead);
    AddPermission(options, PermissionCatalog.ProfileUpdate);
    AddPermission(options, PermissionCatalog.RelationshipsRead);
    AddPermission(options, PermissionCatalog.RelationshipsManage);
    AddPermission(options, PermissionCatalog.RatesRead);
    AddPermission(options, PermissionCatalog.RatesCreate);
    AddPermission(options, PermissionCatalog.PeriodsRead);
    AddPermission(options, PermissionCatalog.PeriodsManage);
    AddPermission(options, PermissionCatalog.HoursManage);
    AddPermission(options, PermissionCatalog.DashboardRead);
    AddPermission(options, PermissionCatalog.RetentionRead);
    AddPermission(options, PermissionCatalog.PrivateLiquidationsRead);
    AddPermission(options, PermissionCatalog.PrivateLiquidationsCreate);
    AddPermission(options, PermissionCatalog.PrivateLiquidationsDelete);
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
    await next();
});
app.UseRouting();
app.UseCors("Spa");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PasswordChangeEnforcementMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (GestionIngresosHonorarios.Infrastructure.Data.AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503)).AllowAnonymous();

app.Run();

static void AddPermission(AuthorizationOptions options, string permission) =>
    options.AddPolicy(permission, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new PermissionRequirement(permission));
    });

public partial class Program { }
