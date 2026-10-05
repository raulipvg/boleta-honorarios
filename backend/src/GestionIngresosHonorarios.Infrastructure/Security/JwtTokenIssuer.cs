using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GestionIngresosHonorarios.Infrastructure.Security;

public sealed class JwtTokenIssuer : IDisposable
{
    private readonly List<RSA> _ownedKeys = [];
    private readonly Dictionary<string, SecurityKey> _validationKeys = new(StringComparer.Ordinal);
    private readonly RsaSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _clientId;

    public JwtTokenIssuer(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer es obligatorio.");
        _audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience es obligatorio.");
        _clientId = configuration["Jwt:ClientId"] ?? throw new InvalidOperationException("Jwt:ClientId es obligatorio.");
        var path = configuration["Jwt:PrivateKeyPath"];
        var keyId = configuration["Jwt:KeyId"] ?? throw new InvalidOperationException("Jwt:KeyId es obligatorio.");

        var rsa = RSA.Create();
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(path))
        {
            rsa.KeySize = 3072;
        }
        else if (environment.IsDevelopment() && !File.Exists(path))
        {
            rsa.KeySize = 3072;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("No se encontró la llave privada RSA configurada para JWT.");
            rsa.ImportFromPem(File.ReadAllText(path));
        }

        if (rsa.KeySize < 3072) throw new InvalidOperationException("La llave RSA JWT debe tener al menos 3072 bits.");
        try
        {
            if (rsa.ExportParameters(true).D is null)
                throw new InvalidOperationException("La llave JWT configurada no contiene material privado.");
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("No fue posible cargar la llave privada JWT.", exception);
        }
        _ownedKeys.Add(rsa);
        _signingKey = new RsaSecurityKey(rsa) { KeyId = keyId };
        _validationKeys.Add(keyId, _signingKey);

        foreach (var previousKey in configuration.GetSection("Jwt:PreviousKeys").GetChildren())
        {
            var previousKeyId = previousKey["KeyId"];
            var publicKeyPath = previousKey["PublicKeyPath"];
            if (string.IsNullOrWhiteSpace(previousKeyId) && string.IsNullOrWhiteSpace(publicKeyPath)) continue;
            if (string.IsNullOrWhiteSpace(previousKeyId) || string.IsNullOrWhiteSpace(publicKeyPath) || !File.Exists(publicKeyPath))
                throw new InvalidOperationException("Cada llave JWT anterior requiere KeyId y PublicKeyPath válidos.");
            var publicRsa = RSA.Create();
            publicRsa.ImportFromPem(File.ReadAllText(publicKeyPath));
            if (publicRsa.KeySize < 3072) throw new InvalidOperationException("Las llaves RSA JWT deben tener al menos 3072 bits.");
            var previousValidationKey = new RsaSecurityKey(publicRsa) { KeyId = previousKeyId };
            if (!_validationKeys.TryAdd(previousKeyId, previousValidationKey))
                throw new InvalidOperationException("Los kid de validación JWT deben ser únicos.");
            _ownedKeys.Add(publicRsa);
        }
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _issuer,
        ValidateAudience = true,
        ValidAudience = _audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKeyResolver = (_, _, kid, _) =>
            kid is not null && _validationKeys.TryGetValue(kid, out var key) ? [key] : [],
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        ValidTypes = ["at+jwt"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role"
    };

    public AuthTokenDto CreateAccessToken(ApplicationUser user, Guid sessionId, bool requiresPasswordChange = false)
    {
        var now = DateTime.UtcNow;
        var lifetime = requiresPasswordChange ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(10);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("client_id", _clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", sessionId.ToString()),
            new("pwd_change", requiresPasswordChange ? "true" : "false"),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var jwt = new JwtSecurityToken(_issuer, _audience, claims, now, now.Add(lifetime),
            new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));
        jwt.Header["kid"] = _signingKey.KeyId;
        jwt.Header["typ"] = "at+jwt";
        return new AuthTokenDto(new JwtSecurityTokenHandler().WriteToken(jwt), (int)lifetime.TotalSeconds, requiresPasswordChange);
    }

    public void Dispose()
    {
        foreach (var key in _ownedKeys) key.Dispose();
    }
}
