namespace VB6.Runtime.Tests;

[TestClass]
public sealed class FinancialIntrinsicTests
{
    [TestMethod]
    public void CoreAnnuityFunctions_HandleZeroRateAndBeginningOfPeriod()
    {
        Assert.AreEqual(-300d, VBFinancial.PMT(0d, 3d, 600d, 300d, 0d), 1e-12);
        Assert.AreEqual(-900d, VBFinancial.PV(0d, 3d, 100d, 600d, 0d), 1e-12);
        Assert.AreEqual(-600d, VBFinancial.FV(0d, 3d, 100d, 300d, 0d), 1e-12);

        var endPayment = VBFinancial.PMT(0.1d / 12d, 36d, 10000d, 0d, 0d);
        var beginningPayment = VBFinancial.PMT(0.1d / 12d, 36d, 10000d, 0d, 1d);
        Assert.AreEqual(-322.67d, endPayment, 0.005d);
        Assert.AreEqual(-320.00516225293467d, beginningPayment, 1e-9);
        Assert.IsTrue(Math.Abs(beginningPayment) < Math.Abs(endPayment));
    }

    [TestMethod]
    public void CoreAnnuityFunctionsRoundTripPresentAndFutureValue()
    {
        const double rate = 0.08d / 12d;
        const double periods = 60d;
        const double presentValue = 15000d;
        const double payment = -304.15d;

        var futureValue = VBFinancial.FV(rate, periods, payment, presentValue, 0d);
        var recoveredPresentValue = VBFinancial.PV(rate, periods, payment, futureValue, 0d);

        Assert.AreEqual(presentValue, recoveredPresentValue, 1e-8);
    }

    [TestMethod]
    public void PaymentBreakdownAndPeriodFunctionsRoundTripAnnuity()
    {
        const double rate = 0.1d / 12d;
        const double periods = 36d;
        const double presentValue = 10000d;
        var payment = VBFinancial.PMT(rate, periods, presentValue, 0d, 0d);

        var interest = VBFinancial.IPMT(rate, 1d, periods, presentValue, 0d, 0d);
        var principal = VBFinancial.PPMT(rate, 1d, periods, presentValue, 0d, 0d);
        Assert.AreEqual(-83.33333333333333d, interest, 1e-12);
        Assert.AreEqual(payment, interest + principal, 1e-12);
        Assert.AreEqual(0d, VBFinancial.IPMT(rate, 1d, periods, presentValue, 0d, 1d), 1e-12);

        Assert.AreEqual(periods, VBFinancial.NPER(rate, payment, presentValue, 0d, 0d), 1e-9);
        Assert.AreEqual(rate, VBFinancial.RATE(periods, payment, presentValue, 0d, 0d, 0.1d), 1e-10);
        Assert.AreEqual(10d, VBFinancial.NPER(0d, -100d, 1000d, 0d, 0d), 1e-12);
        Assert.AreEqual(0d, VBFinancial.RATE(10d, -100d, 1000d, 0d, 0d, 0.1d), 1e-10);
    }

    [TestMethod]
    public void NetPresentValueDiscountsParamArrayFromTheFirstPeriod()
    {
        var values = new VBArray<double>(new VBArrayBound(0, 1));
        values[0] = 100d;
        values[1] = 100d;

        Assert.AreEqual(173.55371900826447d, VBFinancial.NPV(0.1d, values), 1e-12);
    }

    [TestMethod]
    public void InternalRateOfReturnSolvesCashFlowRoot()
    {
        var values = new VBArray<double>(new VBArrayBound(0, 2));
        values[0] = -100d;
        values[1] = 60d;
        values[2] = 60d;

        Assert.AreEqual(0.1306623862918075d, VBFinancial.IRR(values, 0.1d), 1e-12);
    }

    [TestMethod]
    public void ModifiedInternalRateOfReturnUsesSeparateFinanceAndReinvestmentRates()
    {
        var values = new VBArray<double>(new VBArrayBound(0, 2));
        values[0] = -100d;
        values[1] = 60d;
        values[2] = 60d;

        Assert.AreEqual(0.127829774389735d, VBFinancial.MIRR(values, 0.1d, 0.12d), 1e-12);
    }

    [TestMethod]
    public void InvalidFinancialArgumentsAreRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFinancial.PMT(0.1d, 0d, 1d, 0d, 0d));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFinancial.PV(0.1d, 1d, 1d, 0d, 2d));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFinancial.IPMT(0.1d, 0d, 12d, 1d, 0d, 0d));
        Assert.ThrowsException<DivideByZeroException>(() => VBFinancial.NPER(0d, 0d, 1d, 0d, 0d));

        var oneSidedValues = new VBArray<double>(new VBArrayBound(0, 1));
        oneSidedValues[0] = 100d;
        oneSidedValues[1] = 200d;
        Assert.ThrowsException<ArgumentException>(() => VBFinancial.MIRR(oneSidedValues, 0.1d, 0.1d));
    }

    [TestMethod]
    public void DepreciationFunctionsFollowDocumentedSchedules()
    {
        Assert.AreEqual(180d, VBFinancial.SLN(1000d, 100d, 5d), 1e-12);
        Assert.AreEqual(300d, VBFinancial.SYD(1000d, 100d, 5d, 1d), 1e-12);
        Assert.AreEqual(60d, VBFinancial.SYD(1000d, 100d, 5d, 5d), 1e-12);
        Assert.AreEqual(400d, VBFinancial.DDB(1000d, 100d, 5d, 1d, 2d), 1e-12);
        Assert.AreEqual(240d, VBFinancial.DDB(1000d, 100d, 5d, 2d, 2d), 1e-12);
        Assert.AreEqual(144d, VBFinancial.DDB(1000d, 100d, 5d, 3d, 2d), 1e-12);
    }
}
