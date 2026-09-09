using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// The event contract as a foreign client sees it: a separate process activates the class
/// registration-free, asks the connection point container for the event source **by the IID the
/// type library names**, advises a sink, and calls a member by DISPID.
///
/// Every identity in this test comes out of the emitted library, never out of the compiler. That is
/// the whole point of the card: a client binds to what the library says, and if the server answers
/// on something else, the events never arrive.
/// </summary>
[TestClass]
public sealed class EventSourceClientTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AForeignClientBindsTheEventSourceThroughTheTypeLibrary()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM activation is a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6EventClient", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Melderei.vbp");
            File.WriteAllText(
                projectPath,
                "Type=OleDll\nName=Melderei\nClass=Melder; Melder.cls\n");
            File.WriteAllText(
                Path.Combine(directory, "Melder.cls"),
                "VERSION 1.0 CLASS\nBEGIN\n  MultiUse = -1  'True\nEND\n" +
                "Attribute VB_Name = \"Melder\"\nAttribute VB_Creatable = True\n" +
                "Attribute VB_PredeclaredId = False\nAttribute VB_Exposed = True\nOption Explicit\n\n" +
                "Public Event Fertig(ByVal Stand As Long)\n\n" +
                "Public Sub Melde()\n    RaiseEvent Fertig(7)\nEnd Sub\n");

            var outputPath = Path.Combine(directory, "bin", "Melderei.dll");
            var build = RunCli(projectPath, "--emit-assembly", outputPath, "--com-host", "--com-manifest", "--x64");
            Assert.AreEqual(0, build.ExitCode, build.StandardError + build.StandardOutput);

            var manifestPath = Path.Combine(directory, "bin", "Melderei.manifest");
            var typeLibraryPath = Path.Combine(directory, "bin", "Melderei.tlb");
            Assert.IsTrue(File.Exists(manifestPath));
            Assert.IsTrue(File.Exists(typeLibraryPath));

            var classId = ReadClassIdFromManifest(manifestPath);

            // VB6 nennt die Ereignisquelle __Klasse; ihre IID und die DISPID des ausloesenden
            // Mitglieds kommen aus der Bibliothek, nicht aus dem Compiler.
            var sourceIid = ReadTypeIdentity(typeLibraryPath, "__Melder");
            var dispId = ReadMemberId(typeLibraryPath, "_Melder", "Melde");

            var probe = RunProbe(manifestPath, classId, sourceIid, dispId, "Fertig:7");
            Assert.AreEqual(0, probe.ExitCode, probe.StandardError);

            var line = probe.StandardOutput.Trim();
            StringAssert.Contains(line, "match=True", line);
            StringAssert.Contains(line, "received=Fertig:7", line);
            StringAssert.Contains(line, "invoke=0x00000000", line);

            // Der Verbindungspunkt meldet genau die Schnittstelle, nach der der Client gefragt hat.
            StringAssert.Contains(line, "iid=" + sourceIid.ToString("D"), line);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProbe(
        string manifestPath,
        Guid classId,
        Guid sourceIid,
        int dispId,
        string expected)
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, "VB6.ComActivationProbe.dll");
        Assert.IsTrue(File.Exists(probePath));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(manifestPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(probePath);
        startInfo.ArgumentList.Add("--events");
        startInfo.ArgumentList.Add(manifestPath);
        startInfo.ArgumentList.Add(classId.ToString("D"));
        startInfo.ArgumentList.Add(sourceIid.ToString("D"));
        startInfo.ArgumentList.Add(dispId.ToString());
        startInfo.ArgumentList.Add(expected);
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

    [SupportedOSPlatform("windows")]
    private static Guid ReadTypeIdentity(string typeLibraryPath, string typeName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
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
                info.GetTypeAttr(out var attributes);
                try
                {
                    return Marshal.PtrToStructure<TYPEATTR>(attributes).guid;
                }
                finally
                {
                    info.ReleaseTypeAttr(attributes);
                    Marshal.ReleaseComObject(info);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }

        Assert.Fail("The type library does not contain " + typeName + ".");
        return Guid.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static int ReadMemberId(string typeLibraryPath, string typeName, string memberName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
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
                info.GetTypeAttr(out var attributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                    for (var function = 0; function < attribute.cFuncs; function++)
                    {
                        info.GetFuncDesc(function, out var descriptor);
                        try
                        {
                            var func = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                            var buffer = new string[1];
                            info.GetNames(func.memid, buffer, 1, out _);
                            if (string.Equals(buffer[0], memberName, StringComparison.Ordinal))
                            {
                                return func.memid;
                            }
                        }
                        finally
                        {
                            info.ReleaseFuncDesc(descriptor);
                        }
                    }
                }
                finally
                {
                    info.ReleaseTypeAttr(attributes);
                    Marshal.ReleaseComObject(info);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }

        Assert.Fail("The type library does not contain " + typeName + "." + memberName + ".");
        return 0;
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

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int LoadTypeLibEx(
        [MarshalAs(UnmanagedType.LPWStr)] string szFile,
        int regKind,
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib typeLibrary);
}
