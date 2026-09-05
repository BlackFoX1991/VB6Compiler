using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Runtime.Versioning;
using VB6.Compiler;
using VB6.Emit.Managed;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// The out-of-process half of Etappe D: an emitted ActiveX EXE starts in its COM embedding role,
/// registers class objects, serves a foreign process through IDispatch, and exits after release.
/// </summary>
[TestClass]
public sealed class LocalServerActivationTests
{
    private const int ClassNotRegistered = unchecked((int)0x80040154);

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
        var applicationName = "Rechner_" + Guid.NewGuid().ToString("N")[..12];
        Directory.CreateDirectory(directory);
        Process? server = null;

        try
        {
            var projectPath = Path.Combine(directory, applicationName + ".vbp");
            File.WriteAllText(
                projectPath,
                "Type=ActiveX EXE" + Environment.NewLine +
                "Name=\"" + applicationName + "\"" + Environment.NewLine +
                "Class=Addierer; Addierer.cls" + Environment.NewLine);
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

            var exePath = Path.Combine(directory, "bin", applicationName + ".exe");
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

            var classId = ReadClassId(Path.Combine(directory, "bin", applicationName + ".dll"), "Addierer");
            server = StartLocalServer(exePath, directory);

            // Ein getrennter Prozess ist Teil des Vertrags: Der Probe spricht den externen
            // Server über rohes IDispatch an, wie ein spät gebundener VB6-/VBA-Client und nicht
            // über einen .NET-spezifischen RCW-Importpfad.
            var activation = WaitForExternalActivation(server, classId, directory);
            Assert.AreEqual(0, activation.ExitCode, activation.StandardError);
            Assert.AreEqual("42", activation.StandardOutput.Trim());

            // Nach der Freigabe beendet sich der Server von selbst -- das ist der Teil des
            // Vertrags, an dem ein Local Server sonst als Zombie im Speicher bleibt.
            var stopped = WaitForServerExit(server, TimeSpan.FromSeconds(30));
            Assert.IsTrue(stopped, "Der Local Server hat sich nach der Freigabe nicht beendet.");
        }
        finally
        {
            if (server is not null)
            {
                try
                {
                    if (!server.HasExited)
                    {
                        server.Kill(entireProcessTree: true);
                        server.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    server.Dispose();
                }
            }

            TryDeleteDirectory(directory);
        }
    }

    private static Process StartLocalServer(string exePath, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/Embedding");
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the ActiveX EXE server.");
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunLocalServerProbe(
        Guid classId,
        string workingDirectory)
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "VB6.ComActivationProbe.exe");
        Assert.IsTrue(File.Exists(probePath), probePath);
        var startInfo = new ProcessStartInfo(probePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--local-server");
        startInfo.ArgumentList.Add(classId.ToString("D"));
        using var probe = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the local-server activation probe.");
        var standardOutput = probe.StandardOutput.ReadToEnd();
        var standardError = probe.StandardError.ReadToEnd();
        probe.WaitForExit();
        return (probe.ExitCode, standardOutput, standardError);
    }

    private static (int ExitCode, string StandardOutput, string StandardError) WaitForExternalActivation(
        Process server,
        Guid classId,
        string workingDirectory)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            var activation = RunLocalServerProbe(classId, workingDirectory);
            if (activation.ExitCode != ClassNotRegistered || server.HasExited || DateTime.UtcNow >= deadline)
            {
                return activation;
            }

            // The executable is already running; this bounded retry only waits for its
            // CoRegisterClassObject call to become visible, not for SCM registry activation.
            Thread.Sleep(100);
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

    private static bool WaitForServerExit(Process server, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (server.HasExited)
            {
                return true;
            }

            Thread.Sleep(200);
        }

        try
        {
            if (!server.HasExited)
            {
                server.Kill(entireProcessTree: true);
                server.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
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
