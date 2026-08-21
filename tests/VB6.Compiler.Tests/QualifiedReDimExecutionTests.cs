using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// <c>ReDim Section(0).Bytes(0 To 4)</c> and the dynamic array member behind it. The shape comes
/// from the conformance corpus, where it was the first error in four modules.
/// </summary>
[TestClass]
public sealed class QualifiedReDimExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RedimensionsAnArrayInsideAUdtElement()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Type TYPE_SECTION
                Name As String
                Bytes() As Byte
            End Type

            Public Sub Main()
                Dim Section(0 To 2) As TYPE_SECTION

                ReDim Section(0).Bytes(0 To 4)
                Section(0).Bytes(2) = 7
                Debug.Print Section(0).Bytes(2)
                Debug.Print UBound(Section(0).Bytes)

                ReDim Preserve Section(0).Bytes(0 To 6) As Byte
                Debug.Print Section(0).Bytes(2)
                Debug.Print UBound(Section(0).Bytes)
            End Sub
            """,
            "7",
            "4",
            "7",
            "6");
    }

    /// <summary>Each element keeps its own array, which is what makes the member dynamic rather than shared.</summary>
    [TestMethod]
    public void EmitManagedApplication_KeepsMemberArraysSeparatePerElement()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Type TYPE_SECTION
                Bytes() As Byte
            End Type

            Public Sub Main()
                Dim Section(0 To 1) As TYPE_SECTION

                ReDim Section(0).Bytes(0 To 1)
                ReDim Section(1).Bytes(0 To 3)

                Section(0).Bytes(0) = 1
                Section(1).Bytes(0) = 2

                Debug.Print Section(0).Bytes(0)
                Debug.Print Section(1).Bytes(0)
                Debug.Print UBound(Section(1).Bytes)
            End Sub
            """,
            "1",
            "2",
            "3");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerQualifiedReDimTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "QualifiedReDimProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(d =>
                    $"{d.Id}: {d.Message}"));
            Assert.IsTrue(result.Success, diagnostics);

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
                ?? throw new InvalidOperationException("Failed to start the generated application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                expectedLines,
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
