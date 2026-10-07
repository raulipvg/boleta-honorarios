using System.Data;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    ICebienEmailParser cebienParser,
    IPrivateLiquidationFileStorage storage,
    ILogger<PrivateLiquidationApplicationService> logger) : IPrivateLiquidationApplicationService
{
    private const long MaxPdfBytes = 1_048_576;
    private const string RuleCode = "SANATORIO_ALEMAN_PARTICIPACIONES";
    private const string CebienRuleCode = "CENTRO_CEBIEN_EMAIL";
    private const string CebienPayerRut = "76015783-K";
    private readonly IApplicationDbContext _db = db;
    private readonly IPrivateLiquidationPdfParser _parser = parser;
    private readonly ICebienEmailParser _cebienParser = cebienParser;
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
            var period = await GetOrCreatePeriodAsync(prepared.ProfessionalId, prepared.AccountingYear,
                prepared.AccountingMonth, cancellationToken);
            await LockPeriodAsync(period.Id, cancellationToken);
            if (period.AppliedRetentionPercentage != expectedRetentionPercentage)
                throw AppError.Conflict("La tasa anual aplicada al período cambió después de la previsualización. Analiza nuevamente el PDF.");

            if (await IsDuplicateAsync(prepared.ProfessionalId, prepared.Payer.Id, prepared.Parsed, prepared.PdfHash, cancellationToken))
                throw AppError.Conflict("Esta liquidación ya fue importada. Elimine la versión anterior antes de volver a cargarla.");

            var totals = IncomeCalculator.CalculateFromGross(prepared.Parsed.GrossTotalClp, period.AppliedRetentionPercentage);
            var liquidation = new PrivateLiquidation(
                period.Id,
                prepared.ProfessionalId,
                prepared.Parsed.ServiceYear,
                prepared.Parsed.ServiceMonth,
                period.Year,
                period.Month,
                prepared.Parsed.Fortnight,
                prepared.Payer.PrivateInstitutionId,
                prepared.Payer.Id,
                prepared.Rule.Id,
                prepared.Parsed.CollectorRut,
                null,
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
                prepared.FileSizeBytes,
                PrivateLiquidationSourceType.Pdf,
                null,
                null);
            _db.PrivateLiquidations.Add(liquidation);
            await _db.SaveChangesAsync(cancellationToken);

            await RecalculatePrivateTotalsAsync(period, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;

            return ToDto(liquidation, prepared.Payer, "Sanatorio Alemán", period.AppliedRetentionPercentage);
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

    public async Task<PrivateLiquidationPreviewDto> PreviewCebienEmailAsync(
        ActorContext actor, string emailBody, short accountingYear, short accountingMonth, int minutesPerAttention,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareCebienEmailAsync(
            actor, emailBody, accountingYear, accountingMonth, minutesPerAttention, cancellationToken);
        await EnsureCebienEmailNotDuplicateAsync(prepared, cancellationToken);
        return ToCebienPreview(prepared);
    }

    public async Task<PrivateLiquidationDto> ImportCebienEmailAsync(
        ActorContext actor, string emailBody, short accountingYear, short accountingMonth, int minutesPerAttention,
        string expectedSha256, decimal expectedRetentionPercentage, CancellationToken cancellationToken)
    {
        var prepared = await PrepareCebienEmailAsync(
            actor, emailBody, accountingYear, accountingMonth, minutesPerAttention, cancellationToken);
        if (!string.Equals(prepared.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw AppError.Conflict("El cuerpo del correo cambió después de la previsualización. Analízalo nuevamente.");

        await EnsureCebienEmailNotDuplicateAsync(prepared, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await GetOrCreatePeriodAsync(prepared.ProfessionalId,
            prepared.AccountingYear, prepared.AccountingMonth, cancellationToken);
        await LockPeriodAsync(period.Id, cancellationToken);
        if (period.AppliedRetentionPercentage != expectedRetentionPercentage)
            throw AppError.Conflict("La tasa anual aplicada al Mes contable cambió después de la previsualización. Analiza nuevamente el correo.");

        await EnsureCebienEmailNotDuplicateAsync(prepared, cancellationToken);
        var totals = IncomeCalculator.CalculateFromGross(prepared.Parsed.GrossTotalClp, period.AppliedRetentionPercentage);
        EnsureCebienBreakdownMatches(prepared.Parsed, period.AppliedRetentionPercentage, totals);

        var liquidation = CreateCebienLiquidation(prepared, period, totals);
        _db.PrivateLiquidations.Add(liquidation);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculatePrivateTotalsAsync(period, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(liquidation, prepared.Payer, prepared.PrivateInstitutionName, period.AppliedRetentionPercentage);
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

        if (year.HasValue) query = query.Where(x => x.Liquidation.AccountingYear == year.Value);
        if (month.HasValue) query = query.Where(x => x.Liquidation.AccountingMonth == month.Value);

        return await query.OrderByDescending(x => x.Liquidation.AccountingYear)
            .ThenByDescending(x => x.Liquidation.AccountingMonth)
            .ThenByDescending(x => x.Liquidation.LiquidationDate)
            .Select(x => new PrivateLiquidationDto(
                x.Liquidation.Id,
                x.Institution.Name,
                x.Payer.Id,
                x.Payer.LegalName,
                x.Payer.Rut,
                ToSourceCode(x.Liquidation.SourceType),
                x.Liquidation.CollectorRut,
                x.Liquidation.ReportedProfessionalName,
                x.Liquidation.LiquidationNumber,
                x.Liquidation.LiquidationDate,
                x.Liquidation.ServiceYear,
                x.Liquidation.ServiceMonth,
                x.Liquidation.AccountingYear,
                x.Liquidation.AccountingMonth,
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
        if (liquidation.SourceType != PrivateLiquidationSourceType.Pdf || liquidation.StorageKey is null
            || liquidation.OriginalFileName is null)
            throw AppError.BadRequest("Esta liquidación no tiene un PDF descargable.");
        var content = await _storage.OpenReadAsync(liquidation.StorageKey, cancellationToken);
        return new PrivateLiquidationFile(content, liquidation.OriginalFileName);
    }

    public async Task<string> ReadEmailSourceAsync(
        ActorContext actor, Guid liquidationId, CancellationToken cancellationToken)
    {
        var liquidation = await _db.PrivateLiquidations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == liquidationId, cancellationToken) ?? throw AppError.NotFound();
        EnsureCanRead(actor, liquidation.ProfessionalId);
        if (liquidation.SourceType != PrivateLiquidationSourceType.EmailBody || liquidation.SourceBody is null)
            throw AppError.BadRequest("Esta liquidación no tiene un cuerpo de correo asociado.");
        return liquidation.SourceBody;
    }

    public async Task DeleteAsync(ActorContext actor, Guid liquidationId, CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnProfessional(actor);
        var liquidation = await _db.PrivateLiquidations.SingleOrDefaultAsync(
            x => x.Id == liquidationId && x.ProfessionalId == ownerId, cancellationToken) ?? throw AppError.NotFound();
        var storageKey = liquidation.StorageKey;
        var fileBackup = storageKey is null ? null : await TryReadFileAsync(storageKey, cancellationToken);
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
            if (storageKey is not null)
            {
                await _storage.DeleteAsync(storageKey, cancellationToken);
                fileRemoved = true;
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (fileRemoved && fileBackup is not null && storageKey is not null)
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

        if (parsed.LiquidationDate.Year < 1900)
            throw AppError.BadRequest("El año de la fecha de liquidación no es válido.");
        var accountingYear = checked((short)parsed.LiquidationDate.Year);
        var accountingMonth = checked((short)parsed.LiquidationDate.Month);

        var period = await _db.MonthlyPeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProfessionalId == professionalId && x.Year == accountingYear && x.Month == accountingMonth, cancellationToken);
        var retentionPercentage = period?.AppliedRetentionPercentage
            ?? await _db.AnnualRetentionRates.AsNoTracking()
                .Where(x => x.Year == accountingYear)
                .Select(x => (decimal?)x.Percentage)
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw AppError.Conflict("No existe una tasa de retención configurada para el año de la fecha de liquidación.");
        var totals = IncomeCalculator.CalculateFromGross(parsed.GrossTotalClp, retentionPercentage);

        return new PreparedLiquidation(
            professionalId, parsed, payer, rule, accountingYear, accountingMonth, retentionPercentage, totals,
            minutesPerAttention, pdfBytes, hash, originalFileName, fileSizeBytes);
    }

    private async Task<PreparedCebienEmail> PrepareCebienEmailAsync(
        ActorContext actor, string emailBody, short accountingYear, short accountingMonth, int minutesPerAttention,
        CancellationToken cancellationToken)
    {
        var professionalId = RequireOwnProfessional(actor);
        if (string.IsNullOrWhiteSpace(emailBody))
            throw AppError.BadRequest("Pega el cuerpo del correo de Centro Cebien.");
        if (accountingYear < 1900 || accountingMonth is < 1 or > 12)
            throw AppError.BadRequest("Selecciona un Mes contable válido.");
        if (minutesPerAttention < 1)
            throw AppError.BadRequest("Los minutos por atención deben ser enteros mayores que cero.");

        ParsedCebienEmail parsed;
        try
        {
            parsed = _cebienParser.Parse(emailBody);
        }
        catch (InvalidDataException exception)
        {
            throw AppError.BadRequest(exception.Message);
        }

        var professional = await _db.Professionals.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == professionalId, cancellationToken)
            ?? throw AppError.Conflict("No existe un perfil profesional asociado a la cuenta autenticada.");
        if (!string.Equals(NormalizeProfessionalName(professional.Name),
                NormalizeProfessionalName(parsed.ProfessionalName), StringComparison.Ordinal))
            throw AppError.Conflict("El nombre PROFESIONAL del correo de Centro Cebien no coincide con tu perfil.");

        var payer = await _db.PrivatePayerEntities.AsNoTracking().SingleOrDefaultAsync(
            x => x.Rut == CebienPayerRut && x.Active, cancellationToken)
            ?? throw new InvalidOperationException("No está configurado el pagador de Centro Cebien.");
        var rule = await _db.PrivatePaymentRules.AsNoTracking().SingleOrDefaultAsync(
            x => x.PrivateInstitutionId == payer.PrivateInstitutionId && x.Code == CebienRuleCode && x.Active,
            cancellationToken)
            ?? throw new InvalidOperationException("No está configurada la regla de correo de Centro Cebien.");
        var institutionName = await _db.PrivateInstitutions.AsNoTracking()
            .Where(x => x.Id == payer.PrivateInstitutionId)
            .Select(x => x.Name)
            .SingleAsync(cancellationToken);

        var period = await _db.MonthlyPeriods.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProfessionalId == professionalId && x.Year == accountingYear && x.Month == accountingMonth,
            cancellationToken);
        var retentionPercentage = period?.AppliedRetentionPercentage
            ?? await _db.AnnualRetentionRates.AsNoTracking()
                .Where(x => x.Year == accountingYear)
                .Select(x => (decimal?)x.Percentage)
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw AppError.Conflict("No existe una tasa de retención configurada para el año del Mes contable.");
        var totals = IncomeCalculator.CalculateFromGross(parsed.GrossTotalClp, retentionPercentage);
        EnsureCebienBreakdownMatches(parsed, retentionPercentage, totals);

        var normalizedBody = NormalizeEmailBody(emailBody);
        var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedBody)));
        var businessKey = CreateCebienBusinessKey(
            professionalId, payer.Id, accountingYear, accountingMonth, parsed);

        return new PreparedCebienEmail(
            professionalId, parsed, payer, rule, institutionName, accountingYear, accountingMonth,
            retentionPercentage, totals, minutesPerAttention, emailBody, sha256, businessKey);
    }

    private static void EnsureCebienBreakdownMatches(
        ParsedCebienEmail parsed, decimal retentionPercentage, GrossIncomeTotals totals)
    {
        if (parsed.ReportedRetentionPercentage != retentionPercentage
            || parsed.ReportedRetentionClp != totals.RetentionClp
            || parsed.ReportedNetTotalClp != totals.NetClp)
            throw AppError.Conflict("La tasa o el desglose de retención del correo no coincide con el cálculo del año contable.");
    }

    private async Task EnsureCebienEmailNotDuplicateAsync(
        PreparedCebienEmail prepared, CancellationToken cancellationToken)
    {
        if (await _db.PrivateLiquidations.AsNoTracking().AnyAsync(x =>
                x.ProfessionalId == prepared.ProfessionalId
                && (x.Sha256 == prepared.Sha256
                    || (x.SourceType == PrivateLiquidationSourceType.EmailBody && x.BusinessKey == prepared.BusinessKey)),
                cancellationToken))
            throw AppError.Conflict("Este correo ya fue importado. Elimina la liquidación anterior antes de cargar una corrección.");
    }

    private static PrivateLiquidation CreateCebienLiquidation(
        PreparedCebienEmail prepared, MonthlyPeriod period, GrossIncomeTotals totals) => new(
        period.Id,
        prepared.ProfessionalId,
        prepared.Parsed.ServiceYear,
        prepared.Parsed.ServiceMonth,
        period.Year,
        period.Month,
        null,
        prepared.Payer.PrivateInstitutionId,
        prepared.Payer.Id,
        prepared.Rule.Id,
        null,
        prepared.Parsed.ProfessionalName,
        null,
        null,
        prepared.Parsed.PaymentService,
        null,
        null,
        totals.GrossClp,
        totals.RetentionClp,
        totals.NetClp,
        prepared.Parsed.AttentionCount,
        null,
        prepared.MinutesPerAttention,
        prepared.Sha256,
        null,
        null,
        null,
        PrivateLiquidationSourceType.EmailBody,
        prepared.EmailBody,
        prepared.BusinessKey);

    private static string CreateCebienBusinessKey(
        Guid professionalId, Guid payerId, short accountingYear, short accountingMonth, ParsedCebienEmail parsed)
    {
        var attentionCounts = string.Join(";", parsed.AttentionCountsByService
            .OrderBy(x => NormalizeProfessionalName(x.ServiceName), StringComparer.Ordinal)
            .Select(x => $"{NormalizeProfessionalName(x.ServiceName)}={x.Count.ToString(CultureInfo.InvariantCulture)}"));
        var identity = string.Join('|',
            professionalId.ToString("N"),
            payerId.ToString("N"),
            $"{accountingYear:D4}-{accountingMonth:D2}",
            $"{parsed.ServiceYear:D4}-{parsed.ServiceMonth:D2}",
            parsed.GrossTotalClp.ToString(CultureInfo.InvariantCulture),
            attentionCounts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static string NormalizeEmailBody(string emailBody) => string.Join('\n',
        emailBody.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => Regex.Replace(line.Trim(), @"\s+", " ", RegexOptions.CultureInvariant))
            .Where(line => line.Length > 0));

    private static string NormalizeProfessionalName(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        var tokens = Regex.Split(withoutAccents.ToUpperInvariant(), @"[^A-Z0-9]+", RegexOptions.CultureInvariant)
            .Where(token => token.Length > 0)
            .ToList();
        if (tokens.Count > 0 && tokens[0] is "DRA" or "DR" or "DOCTORA" or "DOCTOR")
            tokens.RemoveAt(0);
        return string.Join(' ', tokens);
    }

    private static PrivateLiquidationPreviewDto ToCebienPreview(PreparedCebienEmail prepared) => new(
        prepared.Sha256,
        ToSourceCode(PrivateLiquidationSourceType.EmailBody),
        prepared.PrivateInstitutionName,
        prepared.Payer.Id,
        prepared.Payer.LegalName,
        prepared.Payer.Rut,
        null,
        prepared.Parsed.ProfessionalName,
        null,
        null,
        prepared.Parsed.ServiceYear,
        prepared.Parsed.ServiceMonth,
        prepared.AccountingYear,
        prepared.AccountingMonth,
        null,
        prepared.Parsed.PaymentService,
        null,
        prepared.Parsed.AttentionCountsByService,
        null,
        prepared.Totals.GrossClp,
        prepared.RetentionPercentage,
        prepared.Totals.RetentionClp,
        prepared.Totals.NetClp,
        prepared.Parsed.AttentionCount,
        null,
        prepared.MinutesPerAttention,
        checked(prepared.Parsed.AttentionCount * prepared.MinutesPerAttention),
        null,
        null);

    private static string ToSourceCode(PrivateLiquidationSourceType sourceType) => sourceType switch
    {
        PrivateLiquidationSourceType.Pdf => "pdf",
        PrivateLiquidationSourceType.EmailBody => "email",
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType))
    };

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
                    && x.ServiceYear == parsed.ServiceYear && x.ServiceMonth == parsed.ServiceMonth
                    && x.Fortnight == parsed.Fortnight)), cancellationToken);

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
        ToSourceCode(PrivateLiquidationSourceType.Pdf),
        "Sanatorio Alemán",
        prepared.Payer.Id,
        prepared.Payer.LegalName,
        prepared.Payer.Rut,
        prepared.Parsed.CollectorRut,
        null,
        prepared.Parsed.LiquidationNumber,
        prepared.Parsed.LiquidationDate,
        prepared.Parsed.ServiceYear,
        prepared.Parsed.ServiceMonth,
        prepared.AccountingYear,
        prepared.AccountingMonth,
        prepared.Parsed.Fortnight,
        prepared.Parsed.PaymentService,
        prepared.Parsed.ExecutorName,
        Array.Empty<CebienAttentionCount>(),
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

    private static PrivateLiquidationDto ToDto(
        PrivateLiquidation liquidation, PrivatePayerEntity payer, string privateInstitutionName, decimal appliedRetentionPercentage) => new(
        liquidation.Id,
        privateInstitutionName,
        payer.Id,
        payer.LegalName,
        payer.Rut,
        ToSourceCode(liquidation.SourceType),
        liquidation.CollectorRut,
        liquidation.ReportedProfessionalName,
        liquidation.LiquidationNumber,
        liquidation.LiquidationDate,
        liquidation.ServiceYear,
        liquidation.ServiceMonth,
        liquidation.AccountingYear,
        liquidation.AccountingMonth,
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
        short AccountingYear,
        short AccountingMonth,
        decimal RetentionPercentage,
        GrossIncomeTotals Totals,
        int MinutesPerAttention,
        byte[] PdfBytes,
        string PdfHash,
        string OriginalFileName,
        long FileSizeBytes);

    private sealed record PreparedCebienEmail(
        Guid ProfessionalId,
        ParsedCebienEmail Parsed,
        PrivatePayerEntity Payer,
        PrivatePaymentRule Rule,
        string PrivateInstitutionName,
        short AccountingYear,
        short AccountingMonth,
        decimal RetentionPercentage,
        GrossIncomeTotals Totals,
        int MinutesPerAttention,
        string EmailBody,
        string Sha256,
        string BusinessKey);
}
