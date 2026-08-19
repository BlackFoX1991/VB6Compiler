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
            MajorVer=1
            """;

        var projectPath = Path.Combine(Path.GetTempPath(), "LegacyApp", "LegacyApp.vbp");
        var result = new VBProjectLoader().Parse(source, projectPath);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Exe", result.Project.ProjectType);
        Assert.AreEqual("LegacyApp", result.Project.Name);
        Assert.AreEqual("Sub Main", result.Project.StartupObject);
        Assert.AreEqual("LegacyApp.exe", result.Project.ExecutableName);
        Assert.AreEqual(4, result.Project.Items.Length);
        Assert.AreEqual(1, result.Project.References.Length);
        Assert.AreEqual(1, result.Project.Objects.Length);
        Assert.AreEqual(1, result.Project.Modules.Count());
        Assert.AreEqual(1, result.Project.Classes.Count());
        Assert.AreEqual(1, result.Project.Forms.Count());
        Assert.IsTrue(result.Project.Properties.Any(property => property.Name == "MajorVer" && property.Value == "1"));
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
}
