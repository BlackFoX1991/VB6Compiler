using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// A compiled <c>.ctl</c> as a foreign container sees it.
///
/// Measured before any of this existed, and the reason the card was opened this way: the compiled
/// control activated reg-free out of its own manifest and answered <c>IDispatch</c>,
/// <c>IProvideClassInfo</c> and <c>IConnectionPointContainer</c> -- all three from the CLR -- and
/// <c>E_NOINTERFACE</c> to every OLE control interface there is. It was an Automation object that
/// happened to come from a <c>.ctl</c>.
///
/// The acceptance is deliberately not "the interfaces are there". A container creates a control,
/// asks it what it is, gives it a state, takes the state back and hands it to a second instance. So
/// that is what runs here, in a separate process, with nothing registered.
///
/// Every call in the probe goes through the raw vtable at its published slot rather than through a
/// managed interface declaration -- the probe links the same runtime the server does, so a wrongly
/// ordered declaration would be wrong identically on both sides and the round trip would still
/// pass. A slot number is an independent statement.
/// </summary>
[TestClass]
public sealed class UserControlContainerClientTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AForeignContainerSeesTheOleControlSurfaceOfACompiledUserControl()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM activation is a Windows contract.");
            return;
        }

        var directory = CreateControlProject();
        try
        {
            var manifestPath = Build(directory);
            var classId = ReadClassIdFromManifest(manifestPath);
            var probe = RunProbe(directory, "--olecontrol", manifestPath, classId.ToString("B"));
            Assert.AreEqual(0, probe.ExitCode, probe.StandardError);

            var answers = ParseAnswers(probe.StandardOutput);

            // Der Satz, den ein Container braucht, um ein Control zu *platzieren* statt es nur zu
            // rufen: Identitaet und Klasseninfo, die Ereignisquelle, beide Persistenzwege, das
            // OLE-Objekt samt Control- und In-Place-Haelfte, und das Zeichnen unter *beiden*
            // IViewObject-Ids -- ein Container, der nur die aeltere kennt, fragt die aeltere und
            // faellt nicht zurueck.
            foreach (var name in new[]
                     {
                         "IDispatch", "IProvideClassInfo", "IConnectionPointContainer",
                         "IPersistStreamInit", "IPersistPropertyBag",
                         "IOleObject", "IOleControl", "IOleWindow",
                         "IOleInPlaceObject", "IOleInPlaceActiveObject",
                         "IViewObject", "IViewObject2"
                     })
            {
                Assert.IsTrue(answers.TryGetValue(name, out var answer), $"{name} fehlt in der Messung.");
                Assert.AreEqual("ja", answer, $"{name} wurde abgelehnt: {answer}");
            }

            // Gegenprobe. Ohne sie belegt die Liste darueber nur, dass QueryInterface immer ja
            // sagt. IPersistStream bietet keines der elf gemessenen Stock-Controls an, und dieses
            // auch nicht -- die Init-Variante leitet nicht davon ab.
            Assert.AreNotEqual("ja", answers["IPersistStream"]);
            Assert.AreNotEqual("ja", answers["IDataObject"]);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TheStateOfACompiledUserControlTravelsBetweenTwoActivationsInAForeignProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM activation is a Windows contract.");
            return;
        }

        var directory = CreateControlProject();
        try
        {
            var manifestPath = Build(directory);
            var classId = ReadClassIdFromManifest(manifestPath);
            var probe = RunProbe(
                directory, "--olecontrol-run", manifestPath, classId.ToString("B"), "Zaehler");
            Assert.AreEqual(0, probe.ExitCode, probe.StandardError);

            var answers = ParseAnswers(probe.StandardOutput);

            // Genau die Kombination eines VB6-UserControls, aus Slot 22 gelesen.
            // SETCLIENTSITEFIRST ist die wichtigste: ohne sie laedt der Container den Zustand,
            // bevor das Control seine Site hat, und jede Ambient-Eigenschaft, die es beim
            // Wiederherstellen liest, fehlt.
            Assert.AreEqual("0x20191", answers["miscstatus"]);

            // Die Entwurfsgroesse der .ctl -- 1800 x 1200 Twips -- in HIMETRIC. Die Umrechnung ist
            // der Grund, warum sie ueberhaupt geprueft wird: ein Container, dem Twips gereicht
            // werden, legt das Control rund 1,76-fach zu klein aus, und niemand bekommt einen
            // Fehler zu sehen.
            Assert.AreEqual("3175x2116", answers["extent"]);

            // Der Name, den ein Container anzeigt, ist der VB6-Name -- nicht der CLR-Name mit dem
            // Compilerpraefix.
            Assert.AreEqual("Widget", answers["usertype"]);
            Assert.AreEqual(classId.ToString("B").ToUpperInvariant(), answers["userclsid"]);

            Assert.AreEqual("0x00000000", answers["setclientsite"]);
            Assert.AreEqual("0x00000000", answers["initnew"]);
            Assert.AreEqual("0x00000000", answers["save"]);
            Assert.AreEqual("0x00000000", answers["load"]);

            // Der eigentliche Nachweis: Der Zustand ist durch zwei unabhaengig aktivierte
            // Instanzen gereist. Ein Marshalling-Fehler kaeme hier als falscher Wert heraus,
            // nicht als HRESULT.
            Assert.AreEqual("Zaehler", answers["caption"]);

            // Und das Ende: Close laeuft durch, und die letzte Referenz gibt das Objekt frei.
            Assert.AreEqual("0x00000000", answers["close"]);
            Assert.AreEqual("0", answers["release"]);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// One ActiveX control project. The designer block matters as much as the code: its
    /// <c>ClientWidth</c> and <c>ClientHeight</c> are the extent the container reads back, and
    /// they are the only place that size exists once the control runs without a host.
    /// </summary>
    private static string CreateControlProject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "VB6ControlClient", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "Widgets.vbp"),
            "Type=OleDll\nName=Widgets\nUserControl=Widget.ctl\nStartup=\"(None)\"\n");
        File.WriteAllText(
            Path.Combine(directory, "Widget.ctl"),
            """
            VERSION 5.00
            Begin VB.UserControl Widget
               ClientHeight    =   1200
               ClientLeft      =   0
               ClientTop       =   0
               ClientWidth     =   1800
            End
            Attribute VB_Name = "Widget"
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = False
            Attribute VB_Exposed = True
            Option Explicit

            Public Event Clicked(ByVal Times As Long)

            Private mCaption As String
            Private mCount As Long

            Public Property Get Caption() As String
                Caption = mCaption
            End Property

            Public Property Let Caption(ByVal Value As String)
                mCaption = Value
            End Property

            Public Sub Bump()
                mCount = mCount + 1
                RaiseEvent Clicked(mCount)
            End Sub

            Private Sub UserControl_InitProperties()
                mCaption = ""
            End Sub

            Private Sub UserControl_ReadProperties(PropBag As PropertyBag)
                mCaption = PropBag.ReadProperty("Caption", "")
            End Sub

            Private Sub UserControl_WriteProperties(PropBag As PropertyBag)
                PropBag.WriteProperty "Caption", mCaption, ""
            End Sub

            """);

        return directory;
    }

    private static string Build(string directory)
    {
        var outputPath = Path.Combine(directory, "bin", "Widgets.dll");
        var build = RunCli(
            Path.Combine(directory, "Widgets.vbp"),
            "--emit-assembly", outputPath, "--com-host", "--com-manifest", "--x64");
        Assert.AreEqual(0, build.ExitCode, build.StandardError + build.StandardOutput);
        return Path.Combine(directory, "bin", "Widgets.manifest");
    }

    private static Dictionary<string, string> ParseAnswers(string output)
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                answers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        Assert.AreNotEqual(0, answers.Count, $"Die Sonde meldete nichts Verwertbares: {output}");
        return answers;
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProbe(
        string workingDirectory,
        params string[] arguments)
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "VB6.ComActivationProbe.dll");
        Assert.IsTrue(File.Exists(probePath));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(workingDirectory, "bin"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(probePath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
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
