using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// A compiled <c>.ctl</c> in a container that owns a window.
///
/// Every other COM test in this repo is a *client*: it activates a class and calls it. A container
/// is a different thing, and four parts of a control's contract can only be measured from one --
/// it hands over a client site, activates the control in place inside its own window, draws it into
/// a device context of its own, and receives its events. That is what
/// <c>VB6.OleContainerProbe</c> is, and it deliberately has no reference to the compiler's runtime.
///
/// The first measurement of this card was a failure and it decided the shape of everything else:
/// the component would not activate at all in the existing client probe.
/// <c>CoCreateInstance</c> answered <c>0x800080A5</c>, and the host trace said
/// <c>The specified framework 'Microsoft.WindowsDesktop.App' is not present in the previously
/// loaded runtime</c>. An in-process .NET component that needs the desktop framework cannot load
/// into a process whose runtime was already initialised without it -- so the container had to be a
/// WinForms process, and a plain .NET client of such a component fails by construction. A native
/// container has no pre-loaded runtime and is not affected.
/// </summary>
[TestClass]
public sealed class UserControlPresentationTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AForeignContainerActivatesDrawsAndHearsACompiledUserControl()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("OLE in-place activation is a Windows contract.");
            return;
        }

        var directory = CreateControlProject();
        try
        {
            var outputPath = Path.Combine(directory, "bin", "Widgets.dll");
            var build = RunCli(
                Path.Combine(directory, "Widgets.vbp"),
                "--emit-assembly", outputPath, "--com-host", "--com-manifest", "--x64");
            Assert.AreEqual(0, build.ExitCode, build.StandardError + build.StandardOutput);

            // Ohne diese Datei kann das Control nichts zeigen, und das war der zweite gemessene
            // Befund der Karte: Neben einer erzeugten Control-Komponente lag ausschliesslich
            // VB6.Runtime.dll. Der Host wurde gar nicht mitgeliefert.
            var hostPath = Path.Combine(directory, "bin", "VB6.Runtime.WinForms.dll");
            Assert.IsTrue(File.Exists(hostPath), "Der WinForms-Host liegt nicht neben der Komponente.");

            var manifestPath = Path.Combine(directory, "bin", "Widgets.manifest");
            var typeLibraryPath = Path.Combine(directory, "bin", "Widgets.tlb");
            var classId = ReadClassIdFromManifest(manifestPath);
            var sourceIid = ReadTypeIdentity(typeLibraryPath, "__Widget");
            var bumpDispId = ReadMemberId(typeLibraryPath, "_Widget", "Bump");

            var probe = RunContainer(directory, manifestPath, classId, sourceIid, bumpDispId);
            Assert.AreEqual(0, probe.ExitCode, probe.StandardError);
            var answers = ParseAnswers(probe.StandardOutput);

            Assert.AreEqual("0x00000000", answers["cocreate"], probe.StandardOutput);
            Assert.AreEqual("0x00000000", answers["setclientsite"]);
            Assert.AreEqual("0x00000000", answers["initnew"]);

            // Aktivieren an seinem Platz. Ein Erfolgscode allein sagt hier nichts -- die Aussage
            // ist das Elternfenster: Das Fenster des Controls haengt im Fenster des Containers.
            // Ein Control, das ein eigenes Popup aufmacht, kaeme mit demselben HRESULT zurueck.
            Assert.AreEqual("0x00000000", answers["doverb"]);
            Assert.AreEqual("0x00000000", answers["window"]);
            Assert.AreEqual("container", answers["parent"], probe.StandardOutput);

            // Aus dem Positionsrechteck (10,20)-(210,140). Die beiden hinteren Werte eines RECT
            // sind Kanten, keine Groesse: Wer sie als Breite liest, bekommt ein Fenster, das mit
            // seiner Position waechst -- hier waere es 210x140.
            Assert.AreEqual("200x120", answers["childsize"], probe.StandardOutput);

            // Und der Container verschiebt es an seinem Platz: (5,5)-(105,55).
            Assert.AreEqual("0x00000000", answers["setobjectrects"]);
            Assert.AreEqual("100x50", answers["movedsize"], probe.StandardOutput);

            // Zeichnen in einen Kontext, den nur der Container besitzt. Der HRESULT allein wuerde
            // fuer ein Control durchgehen, das S_OK meldet und nichts malt -- deshalb zaehlt die
            // Sonde die Bildpunkte, die sich gegenueber ihrer Fuellfarbe geaendert haben.
            Assert.AreEqual("0x00000000", answers["draw"]);
            Assert.IsTrue(
                int.Parse(answers["drawnpixels"], System.Globalization.CultureInfo.InvariantCulture) > 0,
                $"Das Control meldete Erfolg und malte nichts: {probe.StandardOutput}");

            // Die Haelfte des Vertrags, die ein Client nicht messen kann: Ereignisse sind das,
            // wofuer ein Container da ist.
            Assert.AreEqual("0x00000000", answers["invoke"]);
            Assert.AreEqual("1", answers["eventcalls"], probe.StandardOutput);

            Assert.AreEqual("0x00000000", answers["close"]);
            Assert.AreEqual("0", answers["release"]);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateControlProject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "VB6ControlContainer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "Widgets.vbp"),
            "Type=OleDll\nName=Widgets\nUserControl=Widget.ctl\nStartup=\"(None)\"\n");

        // Ein Label im Designer ist hier kein Beiwerk: Ohne ein Kind malt das Control eine leere
        // Flaeche, und der Bildpunktvergleich der Sonde koennte nicht zwischen "gezeichnet" und
        // "nichts getan" unterscheiden. Es ist zugleich der Nachweis, dass der Designer-Umschlag
        // im Konstruktor einen Host gefunden hat.
        File.WriteAllText(
            Path.Combine(directory, "Widget.ctl"),
            """
            VERSION 5.00
            Begin VB.UserControl Widget
               ClientHeight    =   1200
               ClientLeft      =   0
               ClientTop       =   0
               ClientWidth     =   1800
               Begin VB.Label lblCaption
                  Caption         =   "Widget"
                  Height          =   255
                  Left            =   120
                  Top             =   120
                  Width           =   1500
               End
            End
            Attribute VB_Name = "Widget"
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = False
            Attribute VB_Exposed = True
            Option Explicit

            Public Event Clicked(ByVal Times As Long)

            Private mCount As Long

            Public Sub Bump()
                mCount = mCount + 1
                RaiseEvent Clicked(mCount)
            End Sub

            Private Sub UserControl_InitProperties()
                mCount = 0
            End Sub

            """);

        return directory;
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

    private static (int ExitCode, string StandardOutput, string StandardError) RunContainer(
        string directory,
        string manifestPath,
        Guid classId,
        Guid sourceIid,
        int bumpDispId)
    {
        var probePath = Path.Combine(
            AppContext.BaseDirectory, "ole-container", "VB6.OleContainerProbe.exe");
        Assert.IsTrue(File.Exists(probePath), $"Die Containersonde fehlt: {probePath}");

        // Das Arbeitsverzeichnis ist das der Komponente. Der comhost loest von dort auf, und ein
        // anderer Start findet die Assembly nicht -- was wie ein Aktivierungsfehler aussieht.
        var startInfo = new ProcessStartInfo(probePath)
        {
            WorkingDirectory = Path.Combine(directory, "bin"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--presentation");
        startInfo.ArgumentList.Add(manifestPath);
        startInfo.ArgumentList.Add(classId.ToString("B"));
        startInfo.ArgumentList.Add(sourceIid.ToString("B"));
        startInfo.ArgumentList.Add(bumpDispId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the OLE container probe.");
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

    [SupportedOSPlatform("windows")]
    private static Guid ReadTypeIdentity(string typeLibraryPath, string typeName)
    {
        LoadTypeLibEx(typeLibraryPath, 2, out var library);
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var info);
                var attributesPointer = IntPtr.Zero;
                info.GetTypeAttr(out attributesPointer);
                try
                {
                    var attributes = Marshal.PtrToStructure<System.Runtime.InteropServices.ComTypes.TYPEATTR>(
                        attributesPointer);
                    return attributes.guid;
                }
                finally
                {
                    info.ReleaseTypeAttr(attributesPointer);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }

        Assert.Fail($"Die Typbibliothek nennt '{typeName}' nicht.");
        return Guid.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static int ReadMemberId(string typeLibraryPath, string typeName, string memberName)
    {
        LoadTypeLibEx(typeLibraryPath, 2, out var library);
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var info);
                var names = new[] { memberName };
                var identifiers = new int[1];
                info.GetIDsOfNames(names, 1, identifiers);
                return identifiers[0];
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }

        Assert.Fail($"Die Typbibliothek nennt '{typeName}.{memberName}' nicht.");
        return 0;
    }

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void LoadTypeLibEx(
        string fileName,
        int registerKind,
        out System.Runtime.InteropServices.ComTypes.ITypeLib library);

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
