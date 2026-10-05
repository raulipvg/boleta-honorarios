namespace GestionIngresosHonorarios.Application.Authorization;

public static class PermissionCatalog
{
    public const string UsersRead = "users:account:read";
    public const string UsersCreate = "users:account:create";
    public const string UsersResetPassword = "users:account:reset-password";
    public const string InstitutionsRead = "institutions:public:read";
    public const string InstitutionsManage = "institutions:public:manage";
    public const string ProfileRead = "professional:profile:read";
    public const string ProfileUpdate = "professional:profile:update";
    public const string RelationshipsRead = "relationships:professional:read";
    public const string RelationshipsManage = "relationships:professional:manage";
    public const string RatesRead = "rates:hourly:read";
    public const string RatesCreate = "rates:hourly:create";
    public const string PeriodsRead = "periods:monthly:read";
    public const string PeriodsManage = "periods:monthly:manage";
    public const string HoursManage = "hours:record:manage";
    public const string DashboardRead = "dashboard:income:read";
    public const string RetentionRead = "configuration:retention:read";

    private static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["ADMINISTRADOR"] =
        [
            UsersRead, UsersCreate, UsersResetPassword,
            InstitutionsRead, InstitutionsManage,
            RelationshipsRead, RatesRead, PeriodsRead, DashboardRead, RetentionRead
        ],
        ["PROFESIONAL"] =
        [
            InstitutionsRead, ProfileRead, ProfileUpdate, RelationshipsRead, RelationshipsManage,
            RatesRead, RatesCreate, PeriodsRead, PeriodsManage, HoursManage, DashboardRead, RetentionRead
        ]
    };

    public static string[] ForRoles(IEnumerable<string> roles) => roles
        .Where(RolePermissions.ContainsKey)
        .SelectMany(role => RolePermissions[role])
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public static bool HasPermission(IEnumerable<string> roles, string permission) =>
        roles.Any(role => RolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(permission, StringComparer.Ordinal));

    public static bool IsKnownRole(string role) => RolePermissions.ContainsKey(role);
}
