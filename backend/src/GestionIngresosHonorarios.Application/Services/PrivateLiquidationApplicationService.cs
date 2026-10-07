using System.Data;
using System.Security.Cryptography;
using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Application.Services;

public sealed class PrivateLiquidationApplicationService(
    IApplicationDbContext db,
    IPrivateLiquidationPdfParser parser,
    IPrivateLiquidationFileStorage storage,
    ILogger<PrivateLiquidationApplicationService> logger) : IPrivateLiquidationApplicationService
{
    private const long MaxPdfBytes = 1_048_576;
    private const string RuleCode = "SANATORIO_ALEMAN_PARTICIPACIONES";
    private readonly IApplicationDbContext _db = db;
    private readonly IPrivateLiquidationPdfParser _parser = parser;
    private readonly IPrivateLiquidationFileStorage _storage = storage;
    private readonly ILogger<PrivateLiquidationApplicationService> _logger = logger;

    public async Task<PrivateLiquidationPreviewDto> PreviewAsync(
        ActorContext actor, Stream pdf, long fileSizeBytes, string fileName, int minutesPerAttention,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(actor, pdf, fileSizeBytes, fileName, minutesPerAttention, cancellationToken);
        await EnsureNotDuplicateAsync(prepared.ProfessionalId, prepared.Parsed, prepared.PdfHash, cancellationToken);
        return ToPreview(prepared, prepared.RetentionPercentage);
    }

    public async Task<PrivateLiquidationDto> ImportAsync(
        ActorContext actor, Stream pdf, long fileSizeBytes, string fileName, int minutesPerAttention,
        string expectedSha256, decimal expectedRetentionPercentage, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(actor, pdf, fileSizeBytes, fileName, minutesPerAttention, cancellationToken);
        if (!string.Equals(prepared.PdfHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw AppError.Conflict("El PDF cambió después de la previsualización. Analícelo nuevamente.");

        await EnsureNotDuplicateAsync(prepared.ProfessionalId, prepared.Parsed, prepared.PdfHash, cancellationToken);

        var storageKey = $"{Guid.NewGuid():N}.pdf";
        await using (var content = new MemoryStream(prepared.PdfBytes, writable: false))
            await _storage.StoreAsync(storageKey, content, cancellationToken);

        var committed = false;
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var period = await GetOrCreatePeriodAsync(prepared.ProfessionalId, prepared.Parsed.Year,
                prepared.Parsed.Month, cancellationToken);
            await LockPeriodAsync(period.Id, cancellationToken);
            if (period.AppliedRetentionPercentage != expectedRetentionPercentage)
                throw AppError.Conflict("La tasa anual aplicada al período cambió después de la previsualización. Analiza nuevamente el PDF.");

            if (await IsDuplicateAsync(prepared.ProfessionalId, prepared.Payer.Id, prepared.Parsed, prepared.PdfHash, cancellationToken))
                throw AppError.Conflict("Esta liquidación ya fue importada. Elimine la versión anterior antes de volver a cargarla.");

            var totals = IncomeCalculator.CalculateFromGross(prepared.Parsed.GrossTotalClp, period.AppliedRetentionPercentage);
            var liquidation = new PrivateLiquidation(
                period.Id,
                prepared.ProfessionalId,
                period.Year,
                period.Month,
                prepared.Parsed.Fortnight,
                prepared.Payer.PrivateInstitutionId,
                prepared.Payer.Id,
                prepared.Rule.Id,
                prepared.Parsed.CollectorRut,
                prepared.Parsed.LiquidationNumber,
                prepared.Parsed.LiquidationDate,
                prepared.Parsed.PaymentService,
                prepared.Parsed.ExecutorName,
                prepared.Parsed.ServiceTotalClp,
                totals.GrossClp,
                totals.RetentionClp,
                totals.NetClp,
                prepared.Parsed.AttentionCount,
                prepared.Parsed.ReportedAttentionCount,
                prepared.MinutesPerAttention,
                prepared.PdfHash,
                storageKey,
                prepared.OriginalFileName,
                prepared.FileSizeBytes);
            _db.PrivateLiquidations.Add(liquidation);
            await _db.SaveChangesAsync(cancellationToken);

            await RecalculatePrivateTotalsAsync(period, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;

            return ToDto(liquidation, prepared.Payer, period.AppliedRetentionPercentage);
        }
        catch
        {
            if (!committed)
            {
                try { await _storage.DeleteAsync(storageKey, CancellationToken.None); }
                catch (Exception cleanupError)
                {
                    _logger.LogError(cleanupError, "No se pudo limpiar el PDF temporal {StorageKey} después de una importación fallida.", storageKey);
                }
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<PrivateLiquidationDto>> ListAsync(
        ActorContext actor, Guid? professionalId, short? year, short? month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 1900)
            throw AppError.BadRequest("El período solicitado no es válido.");
        var ownerId = ResolveProfessionalId(actor, professionalId);
        var query =
            from liquidation in _db.PrivateLiquidations.AsNoTracking()
            join payer in _db.PrivatePayerEntities.AsNoTracking() on liquidation.PayerEntityId equals payer.Id
            join institution in _db.PrivateInstitutions.AsNoTracking() on liquidation.PrivateInstitutionId equals institution.Id
            join period in _db.MonthlyPeriods.AsNoTracking() on liquidation.PeriodId equals period.Id
            where liquidation.ProfessionalId == ownerId
            select new { Liquidation = liquidation, Payer = payer, Institution = institution, Period = period };

        if (year.HasValue) query = query.Where(x => x.Liquidation.Year == year.Value);
        if (month.HasValue) query = query.Where(x => x.Liquidation.Month == month.Value);

        return await query.OrderByDescending(x => x.Liquidation.Year)
            .ThenByDescending(x => x.Liquidation.Month)
            .ThenByDescending(x => x.Liquidation.Fortnight)
            .ThenByDescending(x => x.Liquidation.LiquidationDate)
            .Select(x => new PrivateLiquidationDto(
                x.Liquidation.Id,
                x.Institution.Name,
                x.Payer.Id,
                x.Payer.LegalName,
                x.Payer.Rut,
                x.Liquidation.CollectorRut,
                x.Liquidation.LiquidationNumber,
                x.Liquidation.LiquidationDate,
                x.Liquidation.Year,
                x.Liquidation.Month,
                x.Liquidation.Fortnight,
                x.Liquidation.PaymentService,
                x.Liquidation.ExecutorName,
                x.Liquidation.ServiceTotalClp,
                x.Period.AppliedRetentionPercentage,
                x.Liquidation.GrossTotalClp,
                x.Liquidation.RetentionTotalClp,
                x.Liquidation.NetTotalClp,
                x.Liquidation.AttentionCount,
                x.Liquidation.ReportedAttentionCount,
                x.Liquidation.MinutesPerAttention,
                x.Liquidation.TotalAttentionMinutes,
                x.Liquidation.ImportedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PrivateLiquidationFile> DownloadAsync(
        ActorContext actor, Guid liquidationId, CancellationToken cancellationToken)
    {
        var liquidation = await _db.PrivateLiquidations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == liquidationId, cancellationToken) ?? throw AppError.NotFound();
        EnsureCanRead(actor, liquidation.ProfessionalId);
        var content = await _storage.OpenReadAsync(liquidation.StorageKey, cancellationToken);
        return new PrivateLiquidationFile(content, liquidation.OriginalFileName);
    }

    public async Task DeleteAsync(ActorContext actor, Guid liquidationId, CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnProfessional(actor);
        var liquidation = await _db.PrivateLiquidations.SingleOrDefaultAsync(
            x => x.Id == liquidationId && x.ProfessionalId == ownerId, cancellationToken) ?? throw AppError.NotFound();
        var storageKey = liquidation.StorageKey;
        var fileBackup = await TryReadFileAsync(storageKey, cancellationToken);
        var fileRemoved = false;

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var period = await _db.MonthlyPeriods.SingleOrDefaultAsync(
                x => x.Id == liquidation.PeriodId && x.ProfessionalId == ownerId, cancellationToken) ?? throw AppError.NotFound();
            await LockPeriodAsync(period.Id, cancellationToken);

            _db.PrivateLiquidations.Remove(liquidation);
            await _db.SaveChangesAsync(cancellationToken);
            await RecalculatePrivateTotalsAsync(period, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _storage.DeleteAsync(storageKey, cancellationToken);
            fileRemoved = true;
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (fileRemoved && fileBackup is not null)
            {
                try
                {
                    await using var backup = new MemoryStream(fileBackup, writable: false);
                    await _storage.StoreAsync(storageKey, backup, CancellationToken.None);
                }
                catch (Exception restoreError)
                {
                    _logger.LogCritical(restoreError, "No se pudo restaurar el PDF {StorageKey} tras fallar su eliminación.", storageKey);
                }
            }
            throw;
        }
    }

    private async Task<PreparedLiquidation> PrepareAsync(
        ActorContext actor, Stream pdf, long fileSizeBytes, string fileName, int minutesPerAttention,
        CancellationToken cancellationToken)
    {
        var professionalId = RequireOwnProfessional(actor);
        if (fileSizeBytes is < 1 or > MaxPdfBytes)
            throw AppError.BadRequest("El PDF debe tener un tamaño entre 1 byte y 1 MB.");
        if (minutesPerAttention < 1)
            throw AppError.BadRequest("Los minutos por atención deben ser enteros mayores que cero.");

        var originalFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 255
            || !string.Equals(Path.GetExtension(originalFileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw AppError.BadRequest("El archivo debe ser un PDF válido.");

        var pdfBytes = await ReadPdfBytesAsync(pdf, cancellationToken);
        if (pdfBytes.LongLength != fileSizeBytes)
            throw AppError.BadRequest("El tamaño reportado del PDF no coincide con el contenido recibido.");
        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));
        ParsedPrivateLiquidation parsed;
        try
        {
            await using var pdfStream = new MemoryStream(pdfBytes, writable: false);
            parsed = await _parser.ParseAsync(pdfStream, cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            throw AppError.BadRequest(exception.Message);
        }

        try
        {
            parsed = parsed with
            {
                PayerRut = ChileanRut.NormalizeAndValidate(parsed.PayerRut),
                CollectorRut = ChileanRut.NormalizeAndValidate(parsed.CollectorRut)
            };
        }
        catch (ArgumentException exception)
        {
            throw AppError.BadRequest(exception.Message);
        }

        var professional = await _db.Professionals.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == professionalId && x.Rut != null, cancellationToken)
            ?? throw AppError.Conflict("Completa y valida tu RUT en el perfil antes de importar liquidaciones.");
        if (!string.Equals(professional.Rut, parsed.CollectorRut, StringComparison.Ordinal))
            throw AppError.Conflict("El RUT del cobrador del PDF no coincide con el RUT de tu perfil.");

        var payer = await _db.PrivatePayerEntities.AsNoTracking().SingleOrDefaultAsync(
            x => x.Rut == parsed.PayerRut && x.Active, cancellationToken);
        if (payer is null)
            throw AppError.Conflict("El RUT pagador no está configurado para Sanatorio Alemán. Configúralo y vuelve a cargar el PDF.");

        if (parsed.ReportedAttentionCount.HasValue && parsed.ReportedAttentionCount.Value != parsed.AttentionCount)
            throw AppError.Conflict("La cantidad de atenciones leída no coincide con el cierre del PDF. Corrige el archivo y vuelve a cargarlo.");
        if (parsed.SumPayValuesClp != parsed.GrossTotalClp)
            throw AppError.Conflict("La suma de Valor Pago no coincide con TOTAL LIQUIDACIÓN. Corrige el archivo y vuelve a cargarlo.");
        if (parsed.ServiceTotalClp.HasValue && parsed.ServiceTotalClp.Value != parsed.GrossTotalClp)
            throw AppError.Conflict("La suma de los subtotales de servicio no coincide con TOTAL LIQUIDACIÓN. Corrige el archivo y vuelve a cargarlo.");

        var rule = await _db.PrivatePaymentRules.AsNoTracking().SingleOrDefaultAsync(
            x => x.PrivateInstitutionId == payer.PrivateInstitutionId && x.Code == RuleCode && x.Active, cancellationToken)
            ?? throw new InvalidOperationException("No está configurada la regla de liquidación de Sanatorio Alemán.");

        var period = await _db.MonthlyPeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProfessionalId == professionalId && x.Year == parsed.Year && x.Month == parsed.Month, cancellationToken);
        var retentionPercentage = period?.AppliedRetentionPercentage
            ?? await _db.AnnualRetentionRates.AsNoTracking()
                .Where(x => x.Year == parsed.Year)
                .Select(x => (decimal?)x.Percentage)
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw AppError.Conflict("No existe una tasa de retención configurada para el año de la liquidación.");
        var totals = IncomeCalculator.CalculateFromGross(parsed.GrossTotalClp, retentionPercentage);

        return new PreparedLiquidation(
            professionalId, parsed, payer, rule, retentionPercentage, totals,
            minutesPerAttention, pdfBytes, hash, originalFileName, fileSizeBytes);
    }

    private async Task<MonthlyPeriod> GetOrCreatePeriodAsync(Guid professionalId, short year, short month, CancellationToken cancellationToken)
    {
        var existing = await _db.MonthlyPeriods.SingleOrDefaultAsync(
            x => x.ProfessionalId == professionalId && x.Year == year && x.Month == month, cancellationToken);
        if (existing is not null) return existing;

        var retention = await _db.AnnualRetentionRates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Year == year, cancellationToken)
            ?? throw AppError.Conflict("No existe una tasa de retención configurada para el año de la liquidación.");
        var period = new MonthlyPeriod(professionalId, year, month, retention.Percentage);
        _db.MonthlyPeriods.Add(period);
        await _db.SaveChangesAsync(cancellationToken);
        return period;
    }

    private async Task RecalculatePrivateTotalsAsync(MonthlyPeriod period, CancellationToken cancellationToken)
    {
        var rows = await _db.PrivateLiquidations.AsNoTracking()
            .Where(x => x.PeriodId == period.Id)
            .Select(x => new { x.GrossTotalClp, x.RetentionTotalClp, x.NetTotalClp, x.AttentionCount, x.TotalAttentionMinutes })
            .ToListAsync(cancellationToken);
        var gross = rows.Aggregate(0L, (total, row) => checked(total + row.GrossTotalClp));
        var retention = rows.Aggregate(0L, (total, row) => checked(total + row.RetentionTotalClp));
        var net = rows.Aggregate(0L, (total, row) => checked(total + row.NetTotalClp));
        var attentionCount = rows.Aggregate(0L, (total, row) => checked(total + row.AttentionCount));
        var attentionMinutes = rows.Aggregate(0L, (total, row) => checked(total + row.TotalAttentionMinutes));
        period.UpdatePrivateTotals(gross, retention, net, attentionCount, attentionMinutes);
    }

    private async Task LockPeriodAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM periodos_mensuales WHERE id = @periodId FOR UPDATE";
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "periodId";
        parameter.Value = periodId;
        command.Parameters.Add(parameter);
        _ = await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task EnsureNotDuplicateAsync(Guid professionalId, ParsedPrivateLiquidation parsed, string sha256, CancellationToken cancellationToken)
    {
        var payerId = await _db.PrivatePayerEntities.AsNoTracking()
            .Where(x => x.Rut == parsed.PayerRut)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!payerId.HasValue) return; // PrepareAsync returns the user-facing unknown payer error.
        if (await IsDuplicateAsync(professionalId, payerId.Value, parsed, sha256, cancellationToken))
            throw AppError.Conflict("Esta liquidación ya fue importada. Elimine la versión anterior antes de volver a cargarla.");
    }

    private Task<bool> IsDuplicateAsync(Guid professionalId, Guid payerId, ParsedPrivateLiquidation parsed,
        string sha256, CancellationToken cancellationToken) =>
        _db.PrivateLiquidations.AsNoTracking().AnyAsync(x => x.ProfessionalId == professionalId
            && (x.Sha256 == sha256
                || (x.PayerEntityId == payerId && x.LiquidationNumber == parsed.LiquidationNumber
                    && x.Year == parsed.Year && x.Month == parsed.Month && x.Fortnight == parsed.Fortnight)), cancellationToken);

    private async Task<byte[]> ReadPdfBytesAsync(Stream source, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[32 * 1024];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxPdfBytes)
                throw AppError.BadRequest("El PDF no puede superar 1 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw AppError.BadRequest("El archivo cargado no contiene una firma PDF válida.");
        return bytes;
    }

    private static PrivateLiquidationPreviewDto ToPreview(PreparedLiquidation prepared, decimal retentionPercentage) => new(
        prepared.PdfHash,
        "Sanatorio Alemán",
        prepared.Payer.Id,
        prepared.Payer.LegalName,
        prepared.Payer.Rut,
        prepared.Parsed.CollectorRut,
        prepared.Parsed.LiquidationNumber,
        prepared.Parsed.LiquidationDate,
        prepared.Parsed.Year,
        prepared.Parsed.Month,
        prepared.Parsed.Fortnight,
        prepared.Parsed.PaymentService,
        prepared.Parsed.ExecutorName,
        prepared.Parsed.ServiceTotalClp,
        prepared.Totals.GrossClp,
        retentionPercentage,
        prepared.Totals.RetentionClp,
        prepared.Totals.NetClp,
        prepared.Parsed.AttentionCount,
        prepared.Parsed.ReportedAttentionCount,
        prepared.MinutesPerAttention,
        checked(prepared.Parsed.AttentionCount * prepared.MinutesPerAttention),
        prepared.FileSizeBytes,
        prepared.OriginalFileName);

    private static PrivateLiquidationDto ToDto(PrivateLiquidation liquidation, PrivatePayerEntity payer, decimal appliedRetentionPercentage) => new(
        liquidation.Id,
        "Sanatorio Alemán",
        payer.Id,
        payer.LegalName,
        payer.Rut,
        liquidation.CollectorRut,
        liquidation.LiquidationNumber,
        liquidation.LiquidationDate,
        liquidation.Year,
        liquidation.Month,
        liquidation.Fortnight,
        liquidation.PaymentService,
        liquidation.ExecutorName,
        liquidation.ServiceTotalClp,
        appliedRetentionPercentage,
        liquidation.GrossTotalClp,
        liquidation.RetentionTotalClp,
        liquidation.NetTotalClp,
        liquidation.AttentionCount,
        liquidation.ReportedAttentionCount,
        liquidation.MinutesPerAttention,
        liquidation.TotalAttentionMinutes,
        liquidation.ImportedAt);

    private static Guid RequireOwnProfessional(ActorContext actor)
    {
        if (actor.IsAdministrator || !actor.IsProfessional || actor.ProfessionalId is null) throw AppError.Forbidden();
        return actor.ProfessionalId.Value;
    }

    private static Guid ResolveProfessionalId(ActorContext actor, Guid? requestedProfessionalId)
    {
        if (actor.IsAdministrator)
            return requestedProfessionalId ?? actor.ProfessionalId ?? throw AppError.BadRequest("Debe seleccionar un profesional.");
        if (actor.ProfessionalId is null) throw AppError.Forbidden();
        if (requestedProfessionalId.HasValue && requestedProfessionalId.Value != actor.ProfessionalId.Value)
            throw AppError.Forbidden();
        return actor.ProfessionalId.Value;
    }

    private static void EnsureCanRead(ActorContext actor, Guid professionalId)
    {
        if (!actor.IsAdministrator && actor.ProfessionalId != professionalId) throw AppError.NotFound();
    }

    private async Task<byte[]?> TryReadFileAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await using var source = await _storage.OpenReadAsync(storageKey, cancellationToken);
            await using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private sealed record PreparedLiquidation(
        Guid ProfessionalId,
        ParsedPrivateLiquidation Parsed,
        PrivatePayerEntity Payer,
        PrivatePaymentRule Rule,
        decimal RetentionPercentage,
        GrossIncomeTotals Totals,
        int MinutesPerAttention,
        byte[] PdfBytes,
        string PdfHash,
        string OriginalFileName,
        long FileSizeBytes);
}
