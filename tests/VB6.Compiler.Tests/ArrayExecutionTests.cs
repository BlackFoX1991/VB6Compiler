namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedBoundsAndArrayParameters()
    {
        const string source = """
            Option Base 1

            Function First(values() As Long) As Long
                First = values(1)
            End Function

            Sub Main()
                Dim values(3) As Long
                values(1) = 42
                values(3) = 99
                Debug.Print values(1)
                Debug.Print values(3)
                Debug.Print First(values)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "42", "99", "42" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesMultidimensionalExplicitBounds()
    {
        const string source = """
            Sub Main()
                Dim grid(-1 To 1, 2 To 3) As Integer
                grid(-1, 2) = 7
                grid(1, 3) = 9
                Debug.Print grid(-1, 2)
                Debug.Print grid(1, 3)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "7", "9" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesArrayElementByRef()
    {
        const string source = """
            Sub Increment(ByRef value As Long)
                value = value + 1
            End Sub

            Sub Main()
                Dim values(1 To 2) As Long
                values(1) = 41
                Call Increment(values(1))
                Debug.Print values(1)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "42" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
