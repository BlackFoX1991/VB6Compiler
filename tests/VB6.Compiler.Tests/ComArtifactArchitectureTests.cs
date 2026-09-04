using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// The COM artifacts of a legacy project are written for an **x86** assembly, because that is what
/// a <c>.vbp</c> defaults to — while <c>vb6c</c> itself runs as x64. Both writers used to read the
/// emitted assembly by loading it for execution, which fails outright across architectures with
/// "The assembly architecture is not compatible with the current process architecture". Every
/// ActiveX DLL asking for a type library or a manifest ended there, with an unhandled exception
/// instead of a diagnostic.
/// </summary>
[TestClass]
public sealed class ComArtifactArchitectureTests
{
    [TestMethod]
    public void EmitManagedApplication_WritesTypeLibraryAndManifestForAnX86Assembly()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM hosting artifacts are a Windows contract.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ComArtifacts",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ComSdk.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name="ComSdk"
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

            var outputPath = Path.Combine(directory, "out", "ComSdk.dll");
            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                outputPath,
                new ManagedEmitOptions("ComSdk", ManagedOutputKind.Library, ManagedPlatform.X86)
                {
                    EnableComHosting = true,
                    EnableComManifest = true
                });

            Assert.IsTrue(
                result.Success,
                string.Join(
                    Environment.NewLine,
                    result.BackendResult?.Diagnostics.Select(d => d.Message) ?? []));

            Assert.IsNotNull(result.TypeLibraryPath);
            Assert.IsTrue(File.Exists(result.TypeLibraryPath!), "Die .tlb entsteht auch fuer x86.");
            Assert.AreEqual(
                Path.ChangeExtension(outputPath, ".tlb"),
                result.TypeLibraryPath,
                "Der SDK-Zielgraph leitet den Pfad genau so ab.");

            Assert.IsNotNull(result.ComManifestPath);
            var manifest = File.ReadAllText(result.ComManifestPath!);

            // Die Bitness des Manifests folgt der Ausgabe, nicht dem Compilerprozess.
            StringAssert.Contains(manifest, "processorArchitecture=\"x86\"", manifest);
            StringAssert.Contains(manifest, "ComSdk.comhost.dll", manifest);
            StringAssert.Contains(manifest, "progid=\"ComSdk.Rechner\"", manifest);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
