namespace VB6.Compiler.Tests;

/// <summary>
/// Covers the project entry point of the direct managed backend. The acceptance criterion for the
/// compiler is a legacy .vbp that compiles unchanged, so this path - not the single-file one - is
/// what the goal is measured against, and it must be reachable without the C# detour.
/// </summary>
[TestClass]
public sealed class DirectManagedProjectExecutionTests
{
    [TestMethod]
    public void EmitManaged_ExecutesAVbpProjectAndWritesItsDebugInformation()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDirectProjectTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);

        try
        {
            var projectPath = Path.Combine(projectDirectory, "Direct.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Direct"
                Module=First; First.bas
                Module=Second; Second.bas
                """);
            File.WriteAllText(Path.Combine(projectDirectory, "First.bas"), """
                Public Total As Long

                Sub Add(ByVal value As Long)
                    Total = Total + value
                End Sub
                """);
            File.WriteAllText(Path.Combine(projectDirectory, "Second.bas"), """
                Sub Main()
                    Add 40
                    Add 2
                    Debug.Print Total
                End Sub
                """);

            var output = VB6TestProgram.RunEmitted(directory =>
            {
                var result = DirectManagedCompilation.EmitManaged(
                    VBProjectCompilation.Create(projectPath),
                    Path.Combine(directory, "Direct.dll"));

                Assert.IsTrue(
                    result.Success,
                    string.Join(
                        Environment.NewLine,
                        result.Lowering.ProjectDiagnostics
                            .Select(diagnostic => diagnostic.ToString())
                            .Concat(result.Lowering.Analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))
                            .Concat(result.BackendResult?.Diagnostics.Select(diagnostic =>
                                $"{diagnostic.Code}: {diagnostic.Message}") ?? [])));

                // The compatibility facade drops the PDB path, so the project path is the only
                // place where losing debug information would go unnoticed.
                Assert.IsNotNull(result.PdbPath);
                Assert.IsTrue(File.Exists(result.PdbPath!));
                Assert.IsTrue(File.Exists(result.RuntimeAssemblyPath!));
                Assert.IsTrue(File.Exists(result.RuntimeConfigPath!));
                return result.AssemblyPath!;
            });

            Assert.AreEqual("42", output.Trim());
        }
        finally
        {
            if (Directory.Exists(projectDirectory))
            {
                Directory.Delete(projectDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManaged_ReportsAProjectWithoutAnEntryPoint()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDirectProjectTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);

        try
        {
            var projectPath = Path.Combine(projectDirectory, "NoMain.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="NoMain"
                Module=Only; Only.bas
                """);
            File.WriteAllText(Path.Combine(projectDirectory, "Only.bas"), """
                Sub Helper()
                    Debug.Print 1
                End Sub
                """);

            var result = DirectManagedCompilation.EmitManaged(
                VBProjectCompilation.Create(projectPath),
                Path.Combine(projectDirectory, "bin", "NoMain.dll"));

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.AssemblyPath);
            Assert.IsTrue(result.Lowering.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == "VB6PRJ0005"));
        }
        finally
        {
            if (Directory.Exists(projectDirectory))
            {
                Directory.Delete(projectDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManaged_EmitsOleDllProjectsAsLibrariesWithoutSubMain()
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDirectProjectTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDirectory);

        try
        {
            var projectPath = Path.Combine(projectDirectory, "LegacyLibrary.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name="LegacyLibrary"
                Module=Exports; Exports.bas
                """);
            File.WriteAllText(Path.Combine(projectDirectory, "Exports.bas"), """
                Public Sub Register()
                    Debug.Print 1
                End Sub
                """);

            var result = DirectManagedCompilation.EmitManaged(
                VBProjectCompilation.Create(projectPath),
                Path.Combine(projectDirectory, "bin", "LegacyLibrary.dll"));

            Assert.IsTrue(
                result.Success,
                string.Join(
                    Environment.NewLine,
                    result.Lowering.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(result.Lowering.Analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))
                        .Concat(result.BackendResult?.Diagnostics.Select(diagnostic =>
                            $"{diagnostic.Code}: {diagnostic.Message}") ?? [])));
            Assert.IsNotNull(result.BackendResult?.PeImage);

            using var imageStream = new MemoryStream(result.BackendResult!.PeImage!);
            using var peReader = new System.Reflection.PortableExecutable.PEReader(imageStream);
            Assert.AreEqual(
                0,
                peReader.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress);
        }
        finally
        {
            if (Directory.Exists(projectDirectory))
            {
                Directory.Delete(projectDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManaged_GivesAnActiveXExeALocalServerEntryPoint()
    {
        foreach (var projectType in new[] { "OleExe", "ActiveX EXE" })
        {
            var projectDirectory = Path.Combine(
                Path.GetTempPath(),
                "VB6CompilerLocalServer",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDirectory);

            try
            {
                var projectPath = Path.Combine(projectDirectory, "LegacyServer.vbp");
                File.WriteAllText(projectPath, $"""
                    Type={projectType}
                    Name="LegacyServer"
                    Module=Exports; Exports.bas
                    """);
                File.WriteAllText(Path.Combine(projectDirectory, "Exports.bas"), """
                    Public Sub Register()
                    End Sub
                    """);

                var result = DirectManagedCompilation.EmitManaged(
                    VBProjectCompilation.Create(projectPath),
                    Path.Combine(projectDirectory, "bin", "LegacyServer.exe"));

                Assert.IsTrue(result.Success, projectType);
                using var imageStream = new MemoryStream(result.BackendResult!.PeImage!);
                using var peReader = new System.Reflection.PortableExecutable.PEReader(imageStream);

                // Ohne Sub Main -- der Einstiegspunkt ist erzeugt, weil ein ActiveX EXE für COM
                // existiert und nicht für ein Programm, das jemand startet.
                Assert.AreNotEqual(
                    0,
                    peReader.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress,
                    projectType);
            }
            finally
            {
                if (Directory.Exists(projectDirectory))
                {
                    Directory.Delete(projectDirectory, recursive: true);
                }
            }
        }
    }

    [TestMethod]
    public void EmitManaged_EmitsAllSupportedLibraryProjectKindsWithoutSubMain()
    {
        // OleExe und ActiveX EXE stehen bewusst nicht in dieser Liste: VB6 baut daraus eine
        // ausführbare Datei, die COM mit /Embedding startet, und die hat einen Einstiegspunkt.
        // Für sie gilt EmitManaged_GivesAnActiveXExeALocalServerEntryPoint.
        foreach (var projectType in new[]
                 {
                     "OleDll",
                     "Control",
                     "Dll",
                     "ActiveX DLL",
                     "ActiveX Control"
                 })
        {
            var projectDirectory = Path.Combine(
                Path.GetTempPath(),
                "VB6CompilerDirectProjectTypes",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDirectory);

            try
            {
                var projectPath = Path.Combine(projectDirectory, "LegacyServer.vbp");
                File.WriteAllText(projectPath, $"""
                    Type={projectType}
                    Name="LegacyServer"
                    Module=Exports; Exports.bas
                    """);
                File.WriteAllText(Path.Combine(projectDirectory, "Exports.bas"), """
                    Public Sub Register()
                    End Sub
                    """);

                var result = DirectManagedCompilation.EmitManaged(
                    VBProjectCompilation.Create(projectPath),
                    Path.Combine(projectDirectory, "bin", "LegacyServer.dll"));

                Assert.IsTrue(
                    result.Success,
                    string.Join(
                        Environment.NewLine,
                        result.Lowering.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                            .Concat(result.Lowering.Analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))
                            .Concat(result.BackendResult?.Diagnostics.Select(diagnostic =>
                                $"{diagnostic.Code}: {diagnostic.Message}") ?? [])));
                Assert.IsNotNull(result.BackendResult?.PeImage);

                using var imageStream = new MemoryStream(result.BackendResult!.PeImage!);
                using var peReader = new System.Reflection.PortableExecutable.PEReader(imageStream);
                Assert.AreEqual(
                    0,
                    peReader.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress,
                    projectType);
            }
            finally
            {
                if (Directory.Exists(projectDirectory))
                {
                    Directory.Delete(projectDirectory, recursive: true);
                }
            }
        }
    }
}
