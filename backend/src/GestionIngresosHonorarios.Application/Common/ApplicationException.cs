namespace GestionIngresosHonorarios.Application.Common;

public sealed class ApplicationException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ApplicationException BadRequest(string message) => new(message, 400);
    public static ApplicationException Forbidden() => new("No está autorizado para realizar esta operación.", 403);
    public static ApplicationException NotFound() => new("No se encontró el recurso solicitado.", 404);
    public static ApplicationException Conflict(string message) => new(message, 409);
}
