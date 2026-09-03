using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using VB6.Compiler;
using VB6.Emit.Managed;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// The out-of-process half of Etappe D: an emitted ActiveX EXE is registered as a local server,
/// activated by COM in its own process, called, and released. Registration goes under HKCU, so the
/// test needs no elevation and leaves nothing behind for other users.
/// </summary>
[TestClass]
public sealed class LocalServerActivationTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ActiveXExe_ServesAClassToAnOutOfProcessClient()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM local servers are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6LocalServer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Guid? registeredClassId = null;

        try
        {
            var projectPath = Path.Combine(directory, "Rechner.vbp");
            File.WriteAllText(projectPath, """
                Type=ActiveX EXE
                Name="Rechner"
                Class=Addierer; Addierer.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Addierer.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Addierer"
                Attribute VB_Creatable = True
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Public Function Summe(ByVal Links As Long, ByVal Rechts As Long) As Long
                    Summe = Links + Rechts
                End Function
                """);

            var exePath = Path.Combine(directory, "bin", "Rechner.exe");
            var result = DirectManagedCompilation.EmitManaged(
                VBProjectCompilation.Create(projectPath),
                exePath,
                new ManagedEmitOptions(exePath) { EnableComHosting = true });
            Assert.IsTrue(
                result.Success,
                string.Join(
                    Environment.NewLine,
                    result.Lowering.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(result.Lowering.Analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))
                        .Concat(result.BackendResult?.Diagnostics.Select(diagnostic =>
                            diagnostic.Code + ": " + diagnostic.Message) ?? Array.Empty<string>())));
            Assert.IsTrue(File.Exists(exePath), exePath);

            var classId = ReadClassId(Path.Combine(directory, "bin", "Rechner.dll"), "Addierer");
            RegisterLocalServer(classId, exePath);
            registeredClassId = classId;

            var comType = Type.GetTypeFromCLSID(classId, throwOnError: true)!;
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(comType);
                Assert.IsNotNull(instance);

                // Der Aufruf geht über die Prozessgrenze: ein echter COM-Proxy, kein In-Process-Objekt.
                Assert.IsTrue(Marshal.IsComObject(instance!));
                // Spät gebunden über IDispatch -- genau der Weg, den ein VB6- oder VBA-Client geht.
                var sum = comType.InvokeMember(
                    "Summe",
                    System.Reflection.BindingFlags.InvokeMethod,
                    binder: null,
                    instance,
                    new object?[] { 20, 22 });
                Assert.AreEqual(42, Convert.ToInt32(sum, System.Globalization.CultureInfo.InvariantCulture));
            }
            finally
            {
                if (instance is not null)
                {
                    Marshal.ReleaseComObject(instance);
                }
            }

            // Nach der Freigabe beendet sich der Server von selbst -- das ist der Teil des
            // Vertrags, an dem ein Local Server sonst als Zombie im Speicher bleibt.
            var stopped = WaitForServerExit(TimeSpan.FromSeconds(30));
            Assert.IsTrue(stopped, "Der Local Server hat sich nach der Freigabe nicht beendet.");
        }
        finally
        {
            if (registeredClassId is { } toRemove)
            {
                UnregisterLocalServer(toRemove);
            }

            TryDeleteDirectory(directory);
        }
    }

    [SupportedOSPlatform("windows")]
    private static Guid ReadClassId(string assemblyPath, string className)
    {
        // Dieselbe Ableitung wie im Emitter; sie steht im GuidAttribute der erzeugten Klasse.
        var name = Path.GetFileNameWithoutExtension(assemblyPath);
        var identity = name + "\0class\0" + className;
        var bytes = System.Security.Cryptography.SHA256
            .HashData(System.Text.Encoding.UTF8.GetBytes(identity))
            .AsSpan(0, 16)
            .ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterLocalServer(Guid classId, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\CLSID\{{{classId:D}}}\LocalServer32");
        key.SetValue(null, "\"" + exePath + "\"");
    }

    [SupportedOSPlatform("windows")]
    private static void UnregisterLocalServer(Guid classId)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\CLSID\{{{classId:D}}}",
                throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool WaitForServerExit(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName("Rechner").Length == 0)
            {
                return true;
            }

            Thread.Sleep(200);
        }

        foreach (var process in Process.GetProcessesByName("Rechner"))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
        }

        return false;
    }

    private static void TryDeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(200);
        }
    }
}
