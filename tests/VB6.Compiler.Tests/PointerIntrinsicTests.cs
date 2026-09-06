using System.Diagnostics;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// The general managed path must not hand out a moving CLR interior pointer. The x86 backend has
/// one narrower exception: a stored <c>VarPtr</c> for a local <c>Long</c> uses a native cell. The
/// default AnyCPU path and every remaining storage family still report the explicit VB6 error 5;
/// immediate <c>ByVal … As Any</c> Declare arguments are lowered as call-scoped addresses.
/// </summary>
[TestClass]
public sealed class PointerIntrinsicTests
{
    [TestMethod]
    public void EmitManagedApplication_ReportsWhyVarPtrCannotAnswer()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim zahl As Long
                Dim zeiger As Long
                zahl = 7
                zeiger = VarPtr(zahl)
                Debug.Print Err.Number
                Debug.Print Err.Description
                Err.Clear
                Dim text As String
                text = "abc"
                zeiger = StrPtr(text)
                Debug.Print Err.Number
            End Sub
            """);

        // Die Nummer bleibt VB6s 5 für einen ungültigen Aufruf -- die Beschreibung sagt jetzt,
        // warum, statt den Sammelwert unerklärt zu lassen.
        Assert.AreEqual("5", output[0]);
        StringAssert.Contains(output[1], "ByVal As Any");
        Assert.AreEqual("5", output[2]);
    }

    [TestMethod]
    public void EmitManagedApplication_AnswersObjPtrForAnObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("ObjPtr is the COM identity pointer and needs Windows.");
            return;
        }

        // ObjPtr ist anders gelagert: Die COM-Identität eines Objekts ist stabil und braucht keine
        // festgehaltene Speicherzelle.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim o As Object
                Set o = New Collection
                Debug.Print (ObjPtr(o) <> 0)
                Debug.Print (ObjPtr(Nothing) = 0)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, output);
    }

    [TestMethod]
    public void EmitX86Application_KeepsAStoredLocalLongVarPtrSynchronizedWithNativeWrites()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The stored VarPtr regression uses the Windows RtlMoveMemory probe.");
            return;
        }

        var dotnetHost = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "dotnet",
            "dotnet.exe");
        if (!File.Exists(dotnetHost))
        {
            Assert.Inconclusive("The x86 .NET host required by the x86 VarPtr contract is unavailable.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerStoredVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "StoredVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Sub Main()
                    Dim source As Long
                    Dim destination As Long
                    Dim pointer As Long

                    source = 16909060
                    pointer = VarPtr(source)
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    source = 123
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    destination = 84281096
                    CopyMemory ByVal pointer, destination, 4
                    Debug.Print source
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("StoredVarPtr", Platform: ManagedPlatform.X86));
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

            var startInfo = new ProcessStartInfo(dotnetHost)
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(assemblyPath);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The x86 stored-VarPtr probe could not start.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "16909060", "123", "84281096" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(200);
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(200);
            }
        }
    }
}
