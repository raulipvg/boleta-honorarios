using GestionIngresosHonorarios.Application.DTOs;

namespace GestionIngresosHonorarios.Application.Contracts;

public interface IPrivateLiquidationPdfParser
{
    Task<ParsedPrivateLiquidation> ParseAsync(Stream pdf, CancellationToken cancellationToken);
}
