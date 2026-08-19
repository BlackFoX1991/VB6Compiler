using System.Diagnostics;

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
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerArrayTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ArrayProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated array application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "42", "99", "42" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
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
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerArrayTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "MultiArrayProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated multidimensional array application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "7", "9" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
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
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerArrayTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ByRefArrayProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated ByRef array application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "42" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
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
