using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 types a plain decimal literal by its magnitude, so 30000 is an Integer. An expression
/// built only from Integer operands stays Integer arithmetic — a wider assignment target does
/// not promote it. The overflow is therefore observable VB6 behaviour, not a compiler bug.
/// </summary>
[TestClass]
public sealed class IntegerLiteralExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_OverflowsPureIntegerExpressionAssignedToLong()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Long
                value = 30000 + 30000
                Debug.Print value
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerIntegerLiteralTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "IntegerLiteralProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Empty
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
                ?? throw new InvalidOperationException("Failed to start the generated Integer literal application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Unlike every other execution test, the generated program is expected to fail.
            Assert.AreNotEqual(0, process.ExitCode, $"Expected an overflow, got output '{standardOutput.Trim()}'.");
            StringAssert.Contains(standardError, nameof(OverflowException));
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
    public void EmitManagedApplication_KeepsWideningExplicitWhenAnOperandIsLong()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim wide As Long
                Dim value As Long
                wide = 30000
                value = wide + 30000
                Debug.Print value
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerIntegerLiteralTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "LongWideningProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Empty
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
                ?? throw new InvalidOperationException("Failed to start the generated Long widening application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("60000", standardOutput.Trim());
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
