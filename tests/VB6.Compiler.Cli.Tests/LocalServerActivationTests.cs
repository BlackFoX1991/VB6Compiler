using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VB6.Compiler;
using VB6.Emit.Managed;
using VB6.Runtime;

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
            var (exePath, classId) = BuildAdditionServer(directory, applicationName);
            server = StartLocalServer(exePath, directory);

            // Der Runtime-Pfad muss einen fremden Local-Server-RCW so lange behalten, wie noch
            // ein VB6-Slot darauf zeigt. Die Probe läuft dabei gegen einen anderen Prozess;
            // eine vorzeitige ReleaseComObject-Freigabe würde den Server nach dem ersten Clear
            // beenden oder den verbleibenden Alias unbrauchbar machen.
            var activated = WaitForRuntimeActivation(server, classId);
            Assert.IsTrue(Marshal.IsComObject(activated));
            object? primarySlot = VBObjectLifetime.TransferComActivation(null, activated);
            object? aliasSlot = VBObjectLifetime.Replace(null, primarySlot);
            primarySlot = VBObjectLifetime.Transfer(primarySlot, null);

            Assert.IsTrue(
                VBDynamicDispatch.TryInvokeComMember(aliasSlot, "Summe", [20, 22], out var sum));
            Assert.AreEqual(42, Convert.ToInt32(sum));
            Assert.IsFalse(server.HasExited, "Der Server darf mit einem verbleibenden VB6-Alias nicht enden.");

            aliasSlot = VBObjectLifetime.Transfer(aliasSlot, null);
            var stoppedAfterLastSlot = WaitForServerExit(server, TimeSpan.FromSeconds(30));
            Assert.IsTrue(
                stoppedAfterLastSlot,
                "Der Local Server hat sich nach dem letzten Runtime-Slot nicht beendet.");
            server.Dispose();

            // Der bisherige rohe IDispatch-Probe bleibt separat erhalten: Er deckt den
            // unabhängigen Fremdclient-Vertrag der ActiveX-EXE-Emission ab.
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

/// <summary>
    /// Emits the shared one-class ActiveX EXE and returns its path and the class id.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static (string ExePath, Guid ClassId) BuildAdditionServer(string directory, string applicationName)
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

        return (exePath, classId);
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

    private static object WaitForRuntimeActivation(Process server, Guid classId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        VB6RaisedError? lastError = null;
        while (true)
        {
            try
            {
                return VBInteraction.CreateComInstance(classId.ToString("D"), "Addierer");
            }
            catch (VB6RaisedError error) when (error.Number == 429)
            {
                lastError = error;
            }

            if (server.HasExited || DateTime.UtcNow >= deadline)
            {
                throw new AssertFailedException(
                    "Die Runtime konnte den ActiveX-EXE-Server nicht aktivieren. " +
                    (lastError?.Message ?? "Der Server wurde vor der Registrierung beendet."));
            }

            // Der Prozess läuft schon; es wird nur auf seine CoRegisterClassObject-Sichtbarkeit
            // gewartet, nicht auf eine Registrierung über den SCM.
            Thread.Sleep(100);
        }
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

    /// <summary>
    /// The cross-process counterpart to the in-process reference-count cases: a foreign client
    /// holds its own reference while the runtime releases every slot it has.
    ///
    /// In one process a second holder and the runtime share a wrapper, so "the other holder
    /// survived" is partly a statement about the CLR. Here the two references are genuinely
    /// independent, and the server's own lifetime answers the question: if the runtime's release
    /// reached past its own ownership, the server would exit while somebody is still using it.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ActiveXExe_OutlivesTheRuntimeWhileAForeignClientHoldsIt()
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
        Process? holder = null;

        try
        {
            var (exePath, classId) = BuildAdditionServer(directory, applicationName);
            server = StartLocalServer(exePath, directory);

            var activated = WaitForRuntimeActivation(server, classId);
            holder = StartHoldingProbe(classId, directory);

            var announced = ReadProbeUntilHolding(holder);
            Assert.AreEqual("42", announced["SUM"], "Der Fremdclient konnte den Server nicht aufrufen.");

            // Die Zählerbeobachtung von außen: AddRef und das zugehörige Release müssen den Zähler
            // um genau eins bewegen. Es ist der Zähler des Proxys in jenem Prozess, nicht der des
            // Objekts im Server -- mehr kann ein Client nicht sehen, und mehr wird nicht behauptet.
            var afterAddRef = uint.Parse(announced["ADDREF"]);
            var afterRelease = uint.Parse(announced["RELEASE"]);
            Assert.AreEqual(afterAddRef - 1, afterRelease, "AddRef und Release müssen sich um genau eins unterscheiden.");

            object? slot = VBObjectLifetime.TransferComActivation(null, activated);
            slot = VBObjectLifetime.Transfer(slot, null);
            Assert.IsNull(slot);

            // Der eigentliche Nachweis. Die Runtime hat alles losgelassen, was ihr gehört; der
            // Server muss trotzdem laufen, weil ein fremder Prozess noch eine Referenz hält.
            Assert.IsFalse(
                ServerExitsWithin(server, TimeSpan.FromSeconds(3)),
                "Der Server hat sich beendet, obwohl ein fremder Client noch eine Referenz hält.");

            holder.StandardInput.WriteLine();
            holder.StandardInput.Flush();

            var released = ReadProbeToEnd(holder);
            Assert.IsTrue(holder.WaitForExit(30000), "Der Fremdclient ist nicht beendet.");
            Assert.AreEqual(0, holder.ExitCode, holder.StandardError.ReadToEnd());
            Assert.AreEqual("0", released["FINAL"], "Die letzte Freigabe muss den Zähler auf null bringen.");

            Assert.IsTrue(
                ServerExitsWithin(server, TimeSpan.FromSeconds(30)),
                "Der Server hat sich nach der Freigabe durch den letzten Client nicht beendet.");
        }
        finally
        {
            KillIfRunning(holder);
            KillIfRunning(server);
            TryDeleteDirectory(directory);
        }
    }

    private static Process StartHoldingProbe(Guid classId, string workingDirectory)
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "VB6.ComActivationProbe.exe");
        Assert.IsTrue(File.Exists(probePath), probePath);
        var startInfo = new ProcessStartInfo(probePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--local-server-hold");
        startInfo.ArgumentList.Add(classId.ToString("D"));
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the holding activation probe.");
    }

    private static Dictionary<string, string> ReadProbeUntilHolding(Process probe)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? line;
        while ((line = probe.StandardOutput.ReadLine()) is not null)
        {
            if (string.Equals(line, "HOLDING", StringComparison.Ordinal))
            {
                return values;
            }

            AddProbeValue(values, line);
        }

        throw new AssertFailedException(
            "Der Fremdclient hat die Aktivierung nicht gemeldet: " + probe.StandardError.ReadToEnd());
    }

    private static Dictionary<string, string> ReadProbeToEnd(Process probe)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? line;
        while ((line = probe.StandardOutput.ReadLine()) is not null)
        {
            AddProbeValue(values, line);
        }

        return values;
    }

    private static void AddProbeValue(Dictionary<string, string> values, string line)
    {
        var separator = line.IndexOf('=', StringComparison.Ordinal);
        if (separator > 0)
        {
            values[line[..separator]] = line[(separator + 1)..];
        }
    }

    /// <summary>
    /// Whether the server exits within the timeout. Unlike <see cref="WaitForServerExit"/> this
    /// never kills it: it is also used to assert that the server keeps running, and a helper that
    /// tidies up on timeout would destroy the very thing being asserted.
    /// </summary>
    private static bool ServerExitsWithin(Process server, TimeSpan timeout)
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

        return false;
    }

    private static void KillIfRunning(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
