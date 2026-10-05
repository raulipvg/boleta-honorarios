namespace GestionIngresosHonorarios.Domain.Services;

public sealed record IncomeTotals(long Hours, long GrossClp, long RetentionClp, long NetClp);

public static class IncomeCalculator
{
    // JSON numbers are represented as IEEE-754 doubles in the browser; this bound preserves exact CLP integers.
    public const long MaxExactInteger = 9_007_199_254_740_991;

    public static IncomeTotals Calculate(IEnumerable<int> hourEntries, long hourlyRateClp, decimal retentionPercentage)
    {
        ArgumentNullException.ThrowIfNull(hourEntries);
        if (hourlyRateClp is < 0 or > MaxExactInteger) throw new ArgumentOutOfRangeException(nameof(hourlyRateClp));
        if (retentionPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(retentionPercentage));

        var hours = hourEntries.Aggregate(0L, (total, item) =>
        {
            if (item < 1) throw new ArgumentOutOfRangeException(nameof(hourEntries), "Cada registro debe ser mayor o igual a 1.");
            var sum = checked(total + item);
            if (sum > MaxExactInteger) throw new OverflowException("El total de horas excede el rango entero exacto del cliente.");
            return sum;
        });
        return Calculate(hours, hourlyRateClp, retentionPercentage);
    }

    public static IncomeTotals Calculate(long hours, long hourlyRateClp, decimal retentionPercentage)
    {
        if (hours is < 0 or > MaxExactInteger) throw new ArgumentOutOfRangeException(nameof(hours));
        if (hourlyRateClp is < 0 or > MaxExactInteger) throw new ArgumentOutOfRangeException(nameof(hourlyRateClp));
        if (retentionPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(retentionPercentage));

        var gross = checked(hours * hourlyRateClp);
        if (gross > MaxExactInteger) throw new OverflowException("El monto CLP excede el rango entero exacto del cliente.");
        var retention = checked((long)decimal.Round(gross * retentionPercentage / 100m, 0, MidpointRounding.AwayFromZero));
        return new IncomeTotals(hours, gross, retention, checked(gross - retention));
    }
}
