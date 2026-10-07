using GestionIngresosHonorarios.Domain.Services;
using Xunit;

namespace GestionIngresosHonorarios.UnitTests;

public sealed class IncomeCalculatorTests
{
    [Fact]
    public void CalculatesAcceptanceCaseWithInstitutionalRounding()
    {
        var result = IncomeCalculator.Calculate([6, 6, 6, 6, 6, 6, 6], 31_488, 15.25m);

        Assert.Equal(new IncomeTotals(42, 1_322_496, 201_681, 1_120_815), result);
    }

    [Fact]
    public void RepeatedEntriesRemainIndependentAndSumToTheSameTotal()
    {
        var manyRows = IncomeCalculator.Calculate(Enumerable.Repeat(1, 20), 28_757, 16m);
        var fewRows = IncomeCalculator.Calculate([10, 10], 28_757, 16m);

        Assert.Equal(manyRows, fewRows);
        Assert.Equal(20, manyRows.Hours);
    }

    [Fact]
    public void RoundsHalfAwayFromZeroForEachInstitution()
    {
        var result = IncomeCalculator.Calculate([1], 100, 0.5m);

        Assert.Equal(1, result.RetentionClp);
        Assert.Equal(99, result.NetClp);
    }

    [Fact]
    public void KeepsRetentionRoundedPerInstitutionBeforeConsolidation()
    {
        var first = IncomeCalculator.Calculate([1], 100, 0.5m);
        var second = IncomeCalculator.Calculate([1], 100, 0.5m);

        Assert.Equal(2, first.RetentionClp + second.RetentionClp);
        Assert.NotEqual(1, first.RetentionClp + second.RetentionClp);
    }

    [Fact]
    public void CalculatesPrivateLiquidationRetentionPerPdfFromAuthoritativeGross()
    {
        var services = IncomeCalculator.CalculateFromGross(616_988, 15.25m);
        var clinic = IncomeCalculator.CalculateFromGross(73_460, 15.25m);

        Assert.Equal(new GrossIncomeTotals(616_988, 94_091, 522_897), services);
        Assert.Equal(new GrossIncomeTotals(73_460, 11_203, 62_257), clinic);
        Assert.Equal(105_294, services.RetentionClp + clinic.RetentionClp);
        Assert.Equal(585_154, services.NetClp + clinic.NetClp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsHourEntriesBelowOne(int hours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IncomeCalculator.Calculate([hours], 100, 15m));
    }

    [Fact]
    public void DetectsClpOverflowInsteadOfWrapping()
    {
        Assert.Throws<OverflowException>(() => IncomeCalculator.Calculate([int.MaxValue], IncomeCalculator.MaxExactInteger, 15m));
    }
}
