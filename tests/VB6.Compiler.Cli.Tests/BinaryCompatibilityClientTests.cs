using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace VB6.Compiler.Cli.Tests;

/// <summary>
/// The acceptance VB6 Binary Compatibility actually promises: a client that was built against an
/// earlier version of a component keeps working after a compatible change.
///
/// Such a client holds numbers, not names -- a CLSID to activate and a DISPID to call. So the
/// client here is a separate process that uses exactly those two numbers, both read from the *old*
/// component, against the *new* one. Nothing in it knows a member name.
/// </summary>
[TestClass]
public sealed class BinaryCompatibilityClientTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AClientBuiltAgainstTheOldComponentStillCallsTheNewOne()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM activation is a Windows contract.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "VB6CompatClient", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var old = Build(root, "Rechenwerk", """
                Public Function Verdopple(ByVal Wert As Long) As Long
                    Verdopple = Wert * 2
                End Function
                """);

            var classId = ReadIdentity(old.TypeLibraryPath, "Rechner");
            var dispId = ReadMemberId(old.TypeLibraryPath, "_Rechner", "Verdopple");

            // Eine vertraegliche Aenderung: ein Mitglied kommt dazu, und es steht alphabetisch
            // *vor* dem alten -- ohne Binary Compatibility bekaeme Verdopple damit eine andere
            // DISPID, und der gebaute Client riefe das falsche Mitglied auf.
            var updated = Build(root, "Rechenwerk2", """
                Public Function Addiere(ByVal Wert As Long) As Long
                    Addiere = Wert + 1
                End Function

                Public Function Verdopple(ByVal Wert As Long) As Long
                    Verdopple = Wert * 2
                End Function
                """, compatibleWith: old.AssemblyPath);

            Assert.AreEqual(classId, ReadIdentity(updated.TypeLibraryPath, "Rechner"));

            var probe = RunProbe(updated.ComHostPath, classId, dispId, 21);
            Assert.AreEqual(0, probe.ExitCode, probe.StandardError);
            Assert.AreEqual("42", probe.StandardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private sealed record Built(string AssemblyPath, string ComHostPath, string TypeLibraryPath);

    private static Built Build(string root, string name, string body, string? compatibleWith = null)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        var compatibility = compatibleWith is null
            ? string.Empty
            : "CompatibleMode=2\nCompatibleEXE32=" + compatibleWith + "\n";
        var projectPath = Path.Combine(directory, name + ".vbp");
        File.WriteAllText(
            projectPath,
            "Type=OleDll\nName=" + name + "\nClass=Rechner; Rechner.cls\n" + compatibility);
        File.WriteAllText(
            Path.Combine(directory, "Rechner.cls"),
            "VERSION 1.0 CLASS\nBEGIN\n  MultiUse = -1  'True\nEND\n" +
            "Attribute VB_Name = \"Rechner\"\nAttribute VB_Creatable = True\n" +
            "Attribute VB_PredeclaredId = False\nAttribute VB_Exposed = True\nOption Explicit\n\n" +
            body + "\n");

        var outputPath = Path.Combine(directory, "bin", name + ".dll");
        var result = RunCli(projectPath, "--emit-assembly", outputPath, "--com-host", "--com-manifest", "--x64");
        Assert.AreEqual(0, result.ExitCode, result.StandardError + result.StandardOutput);

        var comHostPath = Path.Combine(directory, "bin", name + ".comhost.dll");
        Assert.IsTrue(File.Exists(comHostPath), "No COM host beside " + outputPath);
        return new Built(outputPath, comHostPath, Path.ChangeExtension(outputPath, ".tlb"));
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProbe(
        string comHostPath,
        Guid classId,
        int dispId,
        int argument)
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
        startInfo.ArgumentList.Add("--dispid");
        startInfo.ArgumentList.Add(comHostPath);
        startInfo.ArgumentList.Add(classId.ToString("D"));
        startInfo.ArgumentList.Add(dispId.ToString());
        startInfo.ArgumentList.Add(argument.ToString());
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

    [SupportedOSPlatform("windows")]
    private static Guid ReadIdentity(string typeLibraryPath, string typeName)
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
