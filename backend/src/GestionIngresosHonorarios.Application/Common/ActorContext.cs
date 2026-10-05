namespace GestionIngresosHonorarios.Application.Common;

public sealed record ActorContext(Guid UserId, Guid? ProfessionalId, bool IsAdministrator, bool IsProfessional);
