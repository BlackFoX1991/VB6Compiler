using System.Reflection;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// A COM identity has to survive a rebuild. A client that binds early — VB6, VBA, C++ — stores the
/// CLSID and the interface id of what it referenced; if a fresh build handed out new ones, every
/// such client would break on the next release, and a registration would leave orphaned keys behind.
/// The identities are therefore derived from the names, never generated.
/// </summary>
[TestClass]
public sealed class ComIdentityStabilityTests
{
    [TestMethod]
    public void EmitManagedApplication_DerivesTheSameComIdentityFromTheSameSource()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM identities are emitted for the Windows COM contract.");
            return;
        }

        var root = Path.Combine(
            Path.GetTempPath(),
            "VB6ComIdentity",
            Guid.NewGuid().ToString("N"));

        try
        {
            var first = EmitAndReadIdentity(Path.Combine(root, "erster"));
            var second = EmitAndReadIdentity(Path.Combine(root, "zweiter"));

            Assert.AreNotEqual(Guid.Empty, first.ClassId);
            Assert.AreEqual(first.ClassId, second.ClassId, "Die CLSID haengt am Namen, nicht am Lauf.");
            Assert.AreEqual(first.ProgId, second.ProgId);
            Assert.AreEqual("ComIdent.Rechner", first.ProgId);

            // Und die Bibliothek dahinter ebenso: Coklasse und Dispinterface einer .tlb tragen
            // dieselbe Ableitung, sonst zeigt eine neu gebaute Bibliothek auf eine andere Klasse.
            Assert.AreEqual(first.TypeLibraryBytes.Length > 0, true);
            CollectionAssert.AreEqual(
                first.TypeLibraryBytes,
                second.TypeLibraryBytes,
                "Zwei Uebersetzungen derselben Quelle ergeben dieselbe Typbibliothek.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static (Guid ClassId, string? ProgId, byte[] TypeLibraryBytes) EmitAndReadIdentity(string directory)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "ComIdent.vbp");
        File.WriteAllText(projectPath, """
            Type=OleDll
            Name="ComIdent"
            Class=Rechner; Rechner.cls
            """);
        File.WriteAllText(Path.Combine(directory, "Rechner.cls"), """
            VERSION 1.0 CLASS
            Attribute VB_Name = "Rechner"
            Attribute VB_Creatable = True
            Attribute VB_Exposed = True
            Option Explicit

            Public Function Verdopple(ByVal wert As Long) As Long
                Verdopple = wert * 2
            End Function
            """);

        var outputPath = Path.Combine(directory, "out", "ComIdent.dll");
        var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
            outputPath,
            new ManagedEmitOptions("ComIdent", ManagedOutputKind.Library, ManagedPlatform.X86)
            {
                EnableComHosting = true,
                EnableComManifest = true
            });

        Assert.IsTrue(
            result.Success,
            string.Join(
                Environment.NewLine,
                result.BackendResult?.Diagnostics.Select(diagnostic => diagnostic.Message) ?? []));

        var manifest = File.ReadAllText(result.ComManifestPath!);
        var clsid = ReadAttribute(manifest, "clsid=\"");
        var progId = ReadAttribute(manifest, "progid=\"");
        return (Guid.Parse(clsid), progId, File.ReadAllBytes(result.TypeLibraryPath!));
    }

    private static string ReadAttribute(string manifest, string marker)
    {
        var start = manifest.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, marker + " fehlt im Manifest: " + manifest);
        start += marker.Length;
        var end = manifest.IndexOf('"', start);
        return manifest[start..end];
    }
}
