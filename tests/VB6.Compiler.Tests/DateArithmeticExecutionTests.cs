namespace VB6.Compiler.Tests;

/// <summary>
/// A Date is an OLE automation double, so arithmetic has to run on that value. The typed path
/// used to fall through to the numeric fallback and convert the Date to an Integer, which
/// overflows for every real date - a crash where a smaller value would have produced a silently
/// wrong number instead.
///
/// The result subtype follows the rule the Variant path already fixes in
/// <c>EmitManagedApplication_PreservesDateSubtypeThroughVariantArithmetic</c>: adding or
/// subtracting a number keeps the Date, and the difference of two Dates is a Double.
/// </summary>
[TestClass]
public sealed class DateArithmeticExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_AddsDaysToADate()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim start As Date, later As Date
                start = CDate("2001-03-04")
                later = start + 5
                Debug.Print Year(later); Month(later); Day(later)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2001 3 9" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_SubtractsDaysFromADate()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim start As Date, earlier As Date
                start = CDate("2001-03-04")
                earlier = start - 3
                Debug.Print Year(earlier); Month(earlier); Day(earlier)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2001 3 1" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_AddsADateOnEitherSideOfTheOperator()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim start As Date, later As Date
                start = CDate("2001-03-04")
                later = 5 + start
                Debug.Print Day(later)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "9" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsTheFractionalDayWhenAddingADouble()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim start As Date, later As Date
                start = CDate("2001-03-04")
                later = start + 1.5
                Debug.Print Day(later); Hour(later)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "5 12" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReturnsTheDayCountForTheDifferenceOfTwoDates()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim first As Date, second As Date
                first = CDate("2001-03-04")
                second = CDate("2001-03-09")
                Debug.Print second - first
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesDateArithmeticIntoADateIntrinsic()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim start As Date
                start = #3/4/2001#
                Debug.Print DateDiff("d", start, start + 5)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "5" }, output);
    }
}
