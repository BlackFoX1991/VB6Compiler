using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MemberArrayForEachExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverFixedPrimitiveUdtArrayMember()
    {
        const string source = """
            Type Holder
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim item
                Dim holder As Holder
                holder.Values(1) = 7
                holder.Values(2) = 8
                holder.Values(3) = 9

                For Each item In holder.Values
                    Debug.Print item
                Next item
            End Sub
            """;

        var output = EmitAndRun(source, "MemberArrayForEachProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "7", "8", "9" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverImplicitWithArrayMemberAndExitFor()
    {
        const string source = """
            Type Holder
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim item
                Dim holder As Holder
                holder.Values(1) = 11
                holder.Values(2) = 22
                holder.Values(3) = 33

                With holder
                    For Each item In .Values
                        Debug.Print item
                        Exit For
                    Next item
                End With
            End Sub
            """;

        var output = EmitAndRun(source, "WithMemberArrayForEachProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "11" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void Analyze_ForEachOverScalarUdtMemberRemainsGuarded()
    {
        var analysis = VBCompilation.Create("""
            Type Holder
                Value As Long
            End Type

            Sub Main()
                Dim item
                Dim holder As Holder
                For Each item In holder.Value
                    Debug.Print item
                Next item
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0055");
    }

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerMemberArrayForEachTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, assemblyName);

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
                ?? throw new InvalidOperationException("Failed to start the generated member-array For Each application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            return standardOutput;
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string[] SplitLines(string output) =>
        output.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
}
