namespace VB6.Compiler.Tests;

[TestClass]
public sealed class Int64ExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesInt64ArithmeticAndForLoop()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Int64
                Dim i As LongLong
                value = 3000000000

                For i = 1 To 3
                    value = value + 1000000000
                Next i

                Debug.Print value
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("6000000000", standardOutput.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLongPtrArithmeticAndConversion()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As LongPtr
                value = CLngPtr(40)
                value = value + 2
                Debug.Print CLng(value)
                Debug.Print value = CLngPtr(42)
                Dim dynamicValue As Variant
                dynamicValue = value
                Debug.Print CLng(dynamicValue + 1)
                Debug.Print CLng(value - 2)
                Debug.Print CLng(value * 2)
                Debug.Print CLng(value \ 2)
                Debug.Print CLng(value Mod 5)
                Debug.Print CLng(-value)
                Debug.Print CLng(Not value)
                Debug.Print CLng(value And 15)
                Debug.Print CLng(value Or 1)
                Debug.Print CLng(value Xor 3)
                Debug.Print CLng(value Eqv 3)
                Debug.Print CLng(value Imp 3)
            End Sub
            """, "Module1.bas");

        CollectionAssert.AreEqual(
            new[] { "42", "True", "43", "40", "84", "21", "2", "-42", "-43", "10", "43", "41", "-42", "-41" },
            VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation)));
    }
}
