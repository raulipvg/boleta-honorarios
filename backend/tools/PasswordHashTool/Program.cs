using GestionIngresosHonorarios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

Console.WriteLine("Generador local de hash de contraseña ASP.NET Core Identity.");
Console.WriteLine("El texto ingresado no se muestra ni se guarda en archivos.");
Console.Write("Nombre de usuario inicial: ");
var userName = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(userName) || userName.Length > 256)
{
    Console.Error.WriteLine("El nombre de usuario no es válido.");
    return 2;
}
var password = ReadSecret("Contraseña temporal del administrador (mínimo 15 caracteres): ");
if (password.Length < 15)
{
    Console.Error.WriteLine("La contraseña debe tener al menos 15 caracteres.");
    return 2;
}

var hasher = new PasswordHasher<ApplicationUser>(Options.Create(new PasswordHasherOptions()));
var hash = hasher.HashPassword(new ApplicationUser { Id = Guid.NewGuid() }, password);
Console.WriteLine();
Console.WriteLine($"INITIAL_ADMIN_USERNAME={userName}");
Console.WriteLine($"INITIAL_ADMIN_NORMALIZED_USERNAME={new UpperInvariantLookupNormalizer().NormalizeName(userName)}");
Console.WriteLine("INITIAL_ADMIN_PASSWORD_HASH (trátelo como secreto de despliegue):");
Console.WriteLine(hash);
return 0;

static string ReadSecret(string prompt)
{
    if (Console.IsInputRedirected)
        return Console.ReadLine() ?? string.Empty;

    Console.Write(prompt);
    var value = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (value.Length > 0) value.Length--;
            continue;
        }
        if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
    Console.WriteLine();
    return value.ToString();
}
