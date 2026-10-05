using Microsoft.AspNetCore.Identity;

namespace GestionIngresosHonorarios.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool Active { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public string Code { get; set; } = string.Empty;
}
