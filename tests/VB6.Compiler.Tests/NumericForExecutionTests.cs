namespace VB6.Compiler.Tests;

[TestClass]
public sealed class NumericForExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesNumericForCounters()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim byteCounter As Byte
                Dim singleCounter As Single
                Dim doubleCounter As Double
                Dim currencyCounter As Currency
                Dim dateCounter As Date

                For byteCounter = 1 To 3
                    Debug.Print byteCounter
                Next byteCounter

                For singleCounter = 0.5 To 1.5 Step 0.5
                    Debug.Print singleCounter
                Next singleCounter

                For doubleCounter = 1 To 2 Step 0.5
                    Debug.Print doubleCounter
                Next doubleCounter

                For currencyCounter = 1@ To 2@ Step 0.5@
                    Debug.Print currencyCounter
                Next currencyCounter

                For dateCounter = CDate(1) To CDate(2) Step CDate(0.5)
                    Debug.Print CDbl(dateCounter)
                Next dateCounter
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "1", "2", "3", "0.5", "1", "1.5", "1", "1.5", "2", "1", "1.5", "2", "1", "1.5", "2" },
            output);
    }
}
