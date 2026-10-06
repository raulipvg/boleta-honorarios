using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthApplicationService auth,
    IAntiforgery antiforgery,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf()
    {
        NoStore();
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginPayload payload, CancellationToken cancellationToken)
    {
        ValidateBrowserOrigin();
        await antiforgery.ValidateRequestAsync(HttpContext);
        NoStore();
        var result = await auth.LoginAsync(payload.UserName, payload.Password, cancellationToken);
        if (result is null) return Unauthorized(new { message = "Nombre de usuario o contraseña incorrectos." });
        SetRefreshCookie(result);
        return Ok(result.Token);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        ValidateBrowserOrigin();
        await antiforgery.ValidateRequestAsync(HttpContext);
        NoStore();
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], cancellationToken);
        if (result is null)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }
        SetRefreshCookie(result);
        return Ok(result.Token);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        ValidateBrowserOrigin();
        await antiforgery.ValidateRequestAsync(HttpContext);
        NoStore();
        var refreshToken = Request.Cookies[RefreshCookieName];
        ClearRefreshCookie();
        try
        {
            await auth.LogoutAsync(refreshToken, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No se pudo revocar la sesión durante el cierre de sesión.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        ValidateBrowserOrigin();
        await antiforgery.ValidateRequestAsync(HttpContext);
        NoStore();
        await auth.LogoutAllAsync(User.GetSubjectId(), cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthIdentityDto>> Me(CancellationToken cancellationToken)
    {
        NoStore();
        return Ok(await auth.GetIdentityAsync(User.GetSubjectId(), cancellationToken));
    }

    [HttpPost("password/change")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordPayload payload, CancellationToken cancellationToken)
    {
        NoStore();
        await auth.ChangePasswordAsync(User.GetSubjectId(), payload.CurrentPassword, payload.NewPassword, cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    private void SetRefreshCookie(AuthSessionResult result)
    {
        if (string.IsNullOrEmpty(result.RefreshToken) || result.RefreshExpiresAt is null) return;
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = result.RefreshExpiresAt
        });
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/"
    });

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private void ValidateBrowserOrigin()
    {
        var suppliedOrigin = Request.Headers.Origin.ToString();
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [configuration["Cors:AllowedOrigin"] ?? string.Empty];
        var site = Request.Headers["Sec-Fetch-Site"].ToString();
        if (string.IsNullOrWhiteSpace(suppliedOrigin)
            || !allowedOrigins.Contains(suppliedOrigin, StringComparer.Ordinal)
            || string.Equals(site, "cross-site", StringComparison.OrdinalIgnoreCase))
            throw GestionIngresosHonorarios.Application.Common.ApplicationException.BadRequest("El origen de la solicitud no está permitido.");
    }

    private const string RefreshCookieName = "__Host-RefreshToken";
}

public sealed record LoginPayload(
    [param: Required, StringLength(256, MinimumLength = 1)] string UserName,
    [param: Required, StringLength(1024, MinimumLength = 1)] string Password);

public sealed record ChangePasswordPayload(
    [param: Required, StringLength(1024, MinimumLength = 1)] string CurrentPassword,
    [param: Required, StringLength(1024, MinimumLength = 15)] string NewPassword);
