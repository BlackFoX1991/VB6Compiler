using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ArrayLibraryExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_EraseClearsFixedArrayAndKeepsBounds()
    {
        const string source = """
            Sub Main()
                Dim values(1 To 2) As Long
                values(1) = 99
                values(2) = 42
                Erase values
                Debug.Print values(1)
                Debug.Print LBound(values)
                Debug.Print UBound(values)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "0", "1", "2" },
            CompileAndRun(source));
    }

    [TestMethod]
    public void EmitManagedApplication_EraseDeallocatesDynamicArrayBeforeLaterReDim()
    {
        const string source = """
            Sub Main()
                Dim values() As Long
                ReDim values(-1 To 1)
                values(0) = 99
                Erase values
                ReDim values(5 To 6)
                Debug.Print LBound(values)
                Debug.Print UBound(values)
                Debug.Print values(5)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "5", "6", "0" },
            CompileAndRun(source));
    }

    [TestMethod]
    public void EmitManagedApplication_EraseRestoresEmptyStringElements()
    {
        const string source = """
            Sub Main()
                Dim names(1 To 1) As String
                names(1) = "filled"
                Erase names
                Debug.Print "[" & names(1) & "]"
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "[]" },
            CompileAndRun(source));
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsMultidimensionalBounds()
    {
        const string source = """
            Sub Main()
                Dim grid(-2 To 2, 4 To 6) As Integer
                Debug.Print LBound(grid, 1)
                Debug.Print UBound(grid, 1)
                Debug.Print LBound(grid, 2)
                Debug.Print UBound(grid, 2)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "-2", "2", "4", "6" },
            CompileAndRun(source));
    }

    private static string[] CompileAndRun(string source)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerArrayLibraryTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ArrayLibraryProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.Message}"));
            Assert.IsTrue(result.Success, diagnostics);
            Assert.IsNotNull(result.AssemblyPath);

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(result.AssemblyPath!);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the generated array-library application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            return standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
