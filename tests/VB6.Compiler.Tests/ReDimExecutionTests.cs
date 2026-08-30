namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ReDimExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesReDimAndPreserve()
    {
        const string source = """
            Option Base 1

            Sub Main()
                Dim values() As Long
                ReDim values(2)
                values(1) = 10
                values(2) = 20
                ReDim Preserve values(4)
                Debug.Print values(1)
                Debug.Print values(2)
                Debug.Print values(3)
                values(4) = 40
                Debug.Print values(4)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "10", "20", "0", "40" },
            CompileAndRun(source));
    }

    [TestMethod]
    public void EmitManagedApplication_ReDimWithoutPreserveClearsOldValues()
    {
        const string source = """
            Sub Main()
                Dim values() As Long
                ReDim values(0 To 1)
                values(0) = 99
                ReDim values(0 To 2)
                Debug.Print values(0)
                Debug.Print values(2)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "0", "0" },
            CompileAndRun(source));
    }

    [TestMethod]
    public void EmitManagedApplication_ReDimPreserveKeepsMultidimensionalLayoutAndValues()
    {
        const string source = """
            Sub Main()
                Dim values() As Long
                ReDim values(1 To 2, -1 To 0)
                values(1, -1) = 11
                values(2, 0) = 20

                ReDim Preserve values(1 To 2, -1 To 1)

                Debug.Print LBound(values, 1)
                Debug.Print UBound(values, 1)
                Debug.Print LBound(values, 2)
                Debug.Print UBound(values, 2)
                Debug.Print values(1, -1)
                Debug.Print values(2, 0)
                Debug.Print values(1, 1)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "1", "2", "-1", "1", "11", "20", "0" },
            CompileAndRun(source));
    }

    private static string[] CompileAndRun(string source)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        return standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
    }
}
