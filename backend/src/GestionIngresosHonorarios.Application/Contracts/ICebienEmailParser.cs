using GestionIngresosHonorarios.Application.DTOs;

namespace GestionIngresosHonorarios.Application.Contracts;

public interface ICebienEmailParser
{
    ParsedCebienEmail Parse(string emailBody);
}
