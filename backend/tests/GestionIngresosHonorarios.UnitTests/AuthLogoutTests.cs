using GestionIngresosHonorarios.Api.Controllers;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Infrastructure.Data;
using GestionIngresosHonorarios.Infrastructure.Identity;
using GestionIngresosHonorarios.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace GestionIngresosHonorarios.IntegrationTests;

public sealed class AuthLogoutTests : IAsyncLifetime
{
    private const string RefreshCookie = "__Host-RefreshToken";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gestion_honorarios_auth_test")
        .WithUsername("auth_integration_owner")
        .WithPassword("Auth-integration-only-passphrase-2026")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "logout-integration",
            NormalizedUserName = "LOGOUT-INTEGRATION",
            PasswordHash = "not-used-by-this-test",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Active = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        UserId = user.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Guid UserId { get; set; }

    [Fact]
    public async Task LogoutRevokesCurrentAndRotatedRefreshTokensAndPreventsRestore()
    {
        const string currentRefreshToken = "current-refresh-token-for-test";
        const string rotatedRefreshToken = "rotated-refresh-token-for-test";
        var now = DateTimeOffset.UtcNow;
        var currentSession = NewSession(HashToken(currentRefreshToken), now);
        var rotatedSession = NewSession(HashToken("replacement-refresh-token"), now);

        await using (var db = CreateContext())
        {
            db.AuthSessions.AddRange(currentSession, rotatedSession);
            db.RotatedRefreshTokens.Add(new RotatedRefreshTokenRecord
            {
                Id = Guid.NewGuid(),
                SessionId = rotatedSession.Id,
                RefreshFamilyId = rotatedSession.RefreshFamilyId,
                RefreshTokenHash = HashToken(rotatedRefreshToken),
                RotatedAt = now,
            });
            await db.SaveChangesAsync();

            var service = CreateAuthService(db);
            await service.LogoutAsync(currentRefreshToken, CancellationToken.None);
            await service.LogoutAsync(rotatedRefreshToken, CancellationToken.None);
        }

        await using var verificationDb = CreateContext();
        var sessions = await verificationDb.AuthSessions.AsNoTracking().ToListAsync();
        Assert.All(sessions, session => Assert.NotNull(session.RevokedAt));

        var verificationService = CreateAuthService(verificationDb);
        Assert.Null(await verificationService.RefreshAsync(currentRefreshToken, CancellationToken.None));
        Assert.Null(await verificationService.RefreshAsync(rotatedRefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task LogoutExpiresRefreshCookieEvenWhenSessionRevocationFails()
    {
        const string refreshToken = "refresh-token-for-cookie-test";
        var auth = new FakeAuthApplicationService((token, _) =>
        {
            Assert.Equal(refreshToken, token);
            throw new InvalidOperationException("Simulated revocation failure.");
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://app.test",
            })
            .Build();
        var controller = new AuthController(
            auth,
            new AllowAntiforgery(),
            configuration,
            NullLogger<AuthController>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.test");
        context.Request.Headers["Origin"] = "https://app.test";
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers["Cookie"] = $"{RefreshCookie}={refreshToken}";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Logout(CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<StatusCodeResult>(result).StatusCode);
        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshCookie}=", setCookie);
        Assert.Contains("expires=", setCookie.ToLowerInvariant());
        Assert.Contains("path=/", setCookie.ToLowerInvariant());
        Assert.Contains("secure", setCookie.ToLowerInvariant());
        Assert.Contains("httponly", setCookie.ToLowerInvariant());
    }

    private AuthSessionRecord NewSession(string refreshTokenHash, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        Sid = Guid.NewGuid(),
        RefreshFamilyId = Guid.NewGuid(),
        RefreshTokenHash = refreshTokenHash,
        CreatedAt = now,
        LastUsedAt = now,
        IdleExpiresAt = now.AddHours(8),
        AbsoluteExpiresAt = now.AddDays(7),
    };

    private AuthApplicationService CreateAuthService(AppDbContext db) => new(
        null!, null!, null!, db, null!, NullLogger<AuthApplicationService>.Instance);

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options);

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class AllowAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();
    }

    private sealed class FakeAuthApplicationService(Func<string?, CancellationToken, Task> logout) : IAuthApplicationService
    {
        public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken) => logout(refreshToken, cancellationToken);
        public Task<AuthSessionResult?> LoginAsync(string userName, string password, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthSessionResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthIdentityDto> GetIdentityAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
