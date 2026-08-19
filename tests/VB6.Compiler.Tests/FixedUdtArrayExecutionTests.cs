using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FixedUdtArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedUdtArrayMembersWithIndependentCopies()
    {
        var compilation = VBCompilation.Create("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 30
            End Sub

            Sub Main()
                Dim first As Record
                Dim copied As Record
                Dim items(1 To 1) As Record

                Debug.Print first.Values(1)
                first.Values(1) = 10
                first.Values(2) = 20

                copied = first
                copied.Values(1) = 99

                Debug.Print first.Values(1)
                Debug.Print copied.Values(1)

                SetValue copied.Values(2)
                Debug.Print first.Values(2)
                Debug.Print copied.Values(2)

                With copied
                    .Values(1) = 7
                    Debug.Print .Values(1)
                End With

                items(1).Values(1) = 40
                copied = items(1)
                copied.Values(1) = 41
                Debug.Print items(1).Values(1)
                Debug.Print copied.Values(1)
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerFixedUdtArrayTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "FixedUdtArrayProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated fixed UDT array application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(
                new[] { "0", "10", "99", "20", "30", "7", "40", "41" },
                lines);
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
