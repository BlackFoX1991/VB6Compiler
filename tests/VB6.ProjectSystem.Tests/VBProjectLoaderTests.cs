namespace VB6.ProjectSystem.Tests;

[TestClass]
public sealed class VBProjectLoaderTests
{
    [TestMethod]
    public void Parse_LoadsCommonVB6ProjectEntries()
    {
        const string source = """
            Type=Exe
            Form=Form1.frm
            Module=Module1; Module1.bas
            Class=Customer; Customer.cls
            UserControl=Widget.ctl
            Reference=*\G{00025E01-0000-0000-C000-000000000046}#0.0#0#C:\WINDOWS\SYSTEM\DAO2516.DLL#Microsoft DAO 2.5 Object Library
            Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; MSCOMCTL.OCX
            Startup="Sub Main"
            Name="LegacyApp"
            ExeName32="LegacyApp.exe"
            CondComp=UseNew = 1:UseLegacy = 0
            MajorVer=1
            """;

        var projectPath = Path.Combine(Path.GetTempPath(), "LegacyApp", "LegacyApp.vbp");
        var result = new VBProjectLoader().Parse(source, projectPath);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Exe", result.Project.ProjectType);
        Assert.AreEqual("LegacyApp", result.Project.Name);
        Assert.AreEqual("Sub Main", result.Project.StartupObject);
        Assert.AreEqual("LegacyApp.exe", result.Project.ExecutableName);
        Assert.AreEqual("UseNew = 1:UseLegacy = 0", result.Project.ConditionalCompilation);
        Assert.AreEqual(4, result.Project.Items.Length);
        Assert.AreEqual(1, result.Project.References.Length);
        Assert.AreEqual(1, result.Project.Objects.Length);
        var reference = result.Project.References[0];
        Assert.AreEqual(VBProjectReferenceKind.TypeLibrary, reference.Metadata.Kind);
        Assert.AreEqual(Guid.Parse("00025E01-0000-0000-C000-000000000046"), reference.Metadata.LibraryId);
        Assert.AreEqual(0, reference.Metadata.MajorVersion);
        Assert.AreEqual(0, reference.Metadata.MinorVersion);
        Assert.AreEqual(0, reference.Metadata.LocaleId);
        Assert.AreEqual("C:\\WINDOWS\\SYSTEM\\DAO2516.DLL", reference.Metadata.FilePath);
        Assert.AreEqual("Microsoft DAO 2.5 Object Library", reference.Metadata.DisplayName);
        Assert.IsTrue(reference.Metadata.IsWellFormed);
        var @object = result.Project.Objects[0];
        Assert.AreEqual(Guid.Parse("831FDD16-0C5C-11D2-A9FC-0000F8754DA1"), @object.Metadata.ClassId);
        Assert.AreEqual(2, @object.Metadata.MajorVersion);
        Assert.AreEqual(0, @object.Metadata.MinorVersion);
        Assert.AreEqual(0, @object.Metadata.LocaleId);
        Assert.AreEqual("MSCOMCTL.OCX", @object.Metadata.FilePath);
        Assert.IsTrue(@object.Metadata.IsWellFormed);
        Assert.AreEqual(1, result.Project.Modules.Count());
        Assert.AreEqual(1, result.Project.Classes.Count());
        Assert.AreEqual(1, result.Project.Forms.Count());
        Assert.IsTrue(result.Project.Properties.Any(property => property.Name == "MajorVer" && property.Value == "1"));
    }

    [TestMethod]
    public void Load_UsesWindowsAnsiFallbackForLegacyProjectText()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6ProjectEncodingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Legacy.vbp");
            File.WriteAllBytes(
                projectPath,
                System.Text.Encoding.Latin1.GetBytes("Type=Exe\r\nName=\"Müller\"\r\n"));

            var result = new VBProjectLoader().Load(projectPath);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Müller", result.Project.Name);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Parse_PreservesNamedModuleAndClassEntries()
    {
        const string source = """
            Module=Utilities; Source\Utilities.bas
            Class=CustomerRepository; Domain\CustomerRepository.cls
            """;

        var projectPath = Path.Combine(Path.GetTempPath(), "Project1", "Project1.vbp");
        var result = new VBProjectLoader().Parse(source, projectPath);

        var module = result.Project.Modules.Single();
        var classModule = result.Project.Classes.Single();

        Assert.AreEqual("Utilities", module.Name);
        Assert.AreEqual("CustomerRepository", classModule.Name);
        Assert.AreEqual(Path.Combine("Source", "Utilities.bas"), module.RelativePath);
        Assert.AreEqual(Path.Combine("Domain", "CustomerRepository.cls"), classModule.RelativePath);
    }

    [TestMethod]
    public void Parse_ResolvesLegacyDesignerEntryPathAndType()
    {
        var result = new VBProjectLoader().Parse(
            "Designer=MSDataEnvironment; DataEnvironment1.dsr\n",
            Path.Combine(Path.GetTempPath(), "LegacyData", "LegacyData.vbp"));

        var designer = result.Project.Items.Single();
        Assert.AreEqual(VBProjectItemKind.Designer, designer.Kind);
        Assert.AreEqual("DataEnvironment1", designer.Name);
        Assert.AreEqual("DataEnvironment1.dsr", designer.RelativePath);
        Assert.AreEqual("MSDataEnvironment", designer.DesignerType);
    }

    [TestMethod]
    public void Parse_IgnoresStandardSectionHeadersAndKeepsSectionProperties()
    {
        const string source = """
            Type=Exe
            Name="Project1"

            [MS Transaction Server]
            AutoRefresh=1
            """;

        var result = new VBProjectLoader().Parse(source, Path.Combine(Path.GetTempPath(), "Project1.vbp"));

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.IsTrue(result.Project.Properties.Any(property =>
            property.Name == "AutoRefresh" && property.Value == "1"));
    }

    [TestMethod]
    public void Parse_ReportsMalformedProjectLineWithoutDiscardingProject()
    {
        const string source = """
            Type=Exe
            malformed project line
            Name="Project1"
            """;

        var result = new VBProjectLoader().Parse(source, Path.Combine(Path.GetTempPath(), "Project1.vbp"));

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Project1", result.Project.Name);
        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6VBP0001", result.Diagnostics[0].Code);
        Assert.AreEqual(2, result.Diagnostics[0].Line);
    }

    [TestMethod]
    public void Parse_PreservesMalformedBindingEntriesWithoutThrowing()
    {
        const string source = "Reference=legacy-reference\nObject=legacy-object\n";

        var result = new VBProjectLoader().Parse(
            source,
            Path.Combine(Path.GetTempPath(), "MalformedBindings.vbp"));

        Assert.IsTrue(result.Success);
        Assert.AreEqual("legacy-reference", result.Project.References[0].RawValue);
        Assert.IsFalse(result.Project.References[0].Metadata.IsWellFormed);
        Assert.AreEqual("legacy-object", result.Project.Objects[0].RawValue);
        Assert.IsFalse(result.Project.Objects[0].Metadata.IsWellFormed);
    }

    [TestMethod]
    public void Parse_RecognizesProjectReferencePath()
    {
        var result = new VBProjectLoader().Parse(
            "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#..\\Shared\\Shared.vbp#Shared\n",
            Path.Combine(Path.GetTempPath(), "Consumer", "Consumer.vbp"));

        Assert.AreEqual(VBProjectReferenceKind.Project, result.Project.References[0].Metadata.Kind);
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(result.Project.FilePath)!, "..", "Shared", "Shared.vbp")),
            result.Project.References[0].Metadata.GetFullPath(result.Project.ProjectDirectory));
    }

    [TestMethod]
    public void Parse_PreservesVersionAndBinaryCompatibilityMetadataAsProperties()
    {
        var result = new VBProjectLoader().Parse(
            "MajorVer=1\nMinorVer=2\nRevisionVer=3\nAutoIncrementVer=0\n" +
            "CompatibleMode=2\nCompatibleEXE32=bin\\Legacy.exe\n",
            Path.Combine(Path.GetTempPath(), "LegacyCompatibility.vbp"));

        var properties = result.Project.Properties.ToDictionary(
            property => property.Name,
            property => property.Value,
            StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual("1", properties["MajorVer"]);
        Assert.AreEqual("2", properties["MinorVer"]);
        Assert.AreEqual("3", properties["RevisionVer"]);
        Assert.AreEqual("0", properties["AutoIncrementVer"]);
        Assert.AreEqual("2", properties["CompatibleMode"]);
        Assert.AreEqual("bin\\Legacy.exe", properties["CompatibleEXE32"]);
    }
}
