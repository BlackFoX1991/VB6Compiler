using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// A VB6 <c>Type</c> value crossing to a foreign client.
///
/// This is the call the CLR refuses: its class interface answers a record return with
/// <c>0x80131515</c>, and no amount of metadata changes that -- registering the library, registering
/// the class and activating the manifest context were all measured and all made no difference. The
/// server therefore answers <c>IDispatch</c> itself and brings its own <c>IRecordInfo</c>, so the
/// record travels as a real <c>VT_RECORD</c> with nothing registered anywhere.
///
/// The client reads the fields through that interface, which is exactly how a VB6 or C++ client
/// reads a UDT -- and the only way to tell a real record from a variant that merely claims the type.
/// </summary>
[TestClass]
public sealed class RecordDispatchClientTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AForeignClientReadsTheFieldsOfAReturnedRecord()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM activation is a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6RecordClient", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Orte.vbp"),
                "Type=OleDll\nName=Orte\nModule=Typen; Typen.bas\nClass=Geber; Geber.cls\n");
            File.WriteAllText(
                Path.Combine(directory, "Typen.bas"),
                "Attribute VB_Name = \"Typen\"\nOption Explicit\n\n" +
                "Public Type TPunkt\n    X As Long\n    Y As Long\nEnd Type\n");
            File.WriteAllText(
                Path.Combine(directory, "Geber.cls"),
                "VERSION 1.0 CLASS\nBEGIN\n  MultiUse = -1  'True\nEND\n" +
                "Attribute VB_Name = \"Geber\"\nAttribute VB_Creatable = True\n" +
                "Attribute VB_PredeclaredId = False\nAttribute VB_Exposed = True\nOption Explicit\n\n" +
                "Public Function Ort() As TPunkt\n    Ort.X = 3\n    Ort.Y = 4\nEnd Function\n\n" +
                "Public Function Summe(ByVal P As TPunkt) As Long\n    Summe = P.X + P.Y\nEnd Function\n\n" +
                "Public Function Zahl() As Long\n    Zahl = 7\nEnd Function\n");

            var outputPath = Path.Combine(directory, "bin", "Orte.dll");
            var build = RunCli(
                Path.Combine(directory, "Orte.vbp"),
                "--emit-assembly", outputPath, "--com-host", "--com-manifest", "--x64");
            Assert.AreEqual(0, build.ExitCode, build.StandardError + build.StandardOutput);

            var manifestPath = Path.Combine(directory, "bin", "Orte.manifest");
            var comHostPath = Path.Combine(directory, "bin", "Orte.comhost.dll");
            var classId = ReadClassIdFromManifest(manifestPath);

            // Beide Aktivierungswege eines spät gebundenen Clients: über IUnknown mit
            // anschliessendem QueryInterface, und direkt als IDispatch erzeugt. Ein Vertrag, der
            // nur einen der beiden trägt, wäre keiner.
            // Die DISPIDs stehen in der Deklarationsreihenfolge: Ort, Summe, Zahl.
            foreach (var mode in new[] { "--variant", "--variant-direct" })
            {
                var scalar = RunProbe(mode, comHostPath, classId, dispId: 3);
                Assert.AreEqual(0, scalar.ExitCode, scalar.StandardError);
                Assert.AreEqual("vt=3", scalar.StandardOutput.Trim(), mode);

                var record = RunProbe(mode, comHostPath, classId, dispId: 1);
                Assert.AreEqual(0, record.ExitCode, record.StandardError);

                // VT_RECORD ist 36, und der Name kommt aus der IRecordInfo des Servers -- ohne
                // Registrierung, ohne Typbibliothek im Prozess.
                var line = record.StandardOutput.Trim();
                StringAssert.Contains(line, "vt=36", line);
                StringAssert.Contains(line, "name=TPunkt", line);
                StringAssert.Contains(line, "size=8", line);
                StringAssert.Contains(line, "X=3,Y=4", line);
            }

            // Und die andere Richtung: Der Client baut selbst einen Record -- über die
            // IRecordInfo, die der Server ihm zu diesem Typ gegeben hat -- füllt 11 und 22 und
            // gibt ihn als Argument mit. Ein falsches Layout käme hier als falsche Zahl heraus,
            // nicht als Fehlercode.
            var incoming = RunProbe("--record-in", comHostPath, classId, dispId: 1, secondDispId: 2);
            Assert.AreEqual(0, incoming.ExitCode, incoming.StandardError);
            Assert.AreEqual("sum=33", incoming.StandardOutput.Trim(), incoming.StandardOutput);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProbe(
        string mode,
        string comHostPath,
        Guid classId,
        int dispId,
        int? secondDispId = null)
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "VB6.ComActivationProbe.dll");
        Assert.IsTrue(File.Exists(probePath));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(comHostPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(probePath);
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(comHostPath);
        startInfo.ArgumentList.Add(classId.ToString("D"));
        startInfo.ArgumentList.Add(dispId.ToString());
        if (secondDispId is { } second)
        {
            startInfo.ArgumentList.Add(second.ToString());
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the COM activation probe.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, standardOutput, standardError);
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunCli(
        string inputPath,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(inputPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "vb6c.dll"));
        startInfo.ArgumentList.Add(inputPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the vb6c process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, standardOutput, standardError);
    }

    private static Guid ReadClassIdFromManifest(string manifestPath)
    {
        var manifest = File.ReadAllText(manifestPath);
        var match = System.Text.RegularExpressions.Regex.Match(manifest, "clsid=\"(\\{[^}]*\\})\"");
        Assert.IsTrue(match.Success, "The manifest names no comClass.");
        return Guid.Parse(match.Groups[1].Value);
    }

    private static void DeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
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
            Thread.Sleep(100);
        }
    }
}
