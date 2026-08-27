namespace VB6.ProjectSystem.Tests;

[TestClass]
public sealed class VBProjectGroupLoaderTests
{
    [TestMethod]
    public void Parse_LoadsProjectsInDeclaredOrderAndResolvesRelativePaths()
    {
        const string source = """
            Type=Group
            Project="App\App.vbp"
            Project=Controls.vbp
            StartupProject="App\App.vbp"
            Name="LegacyGroup"
            """;

        var groupPath = Path.Combine(Path.GetTempPath(), "LegacyGroup", "LegacyGroup.vbg");
        var result = new VBProjectGroupLoader().Parse(source, groupPath);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Group", result.Group.GroupType);
        Assert.AreEqual("App" + Path.DirectorySeparatorChar + "App.vbp", result.Group.Projects[0].RelativePath);
        Assert.AreEqual("Controls.vbp", result.Group.Projects[1].RelativePath);
        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(groupPath)!, "App", "App.vbp")),
            result.Group.Projects[0].GetFullPath(result.Group.ProjectDirectory));
        Assert.AreEqual("App" + Path.DirectorySeparatorChar + "App.vbp", result.Group.StartupProject);
    }

    [TestMethod]
    public void Parse_ReportsMissingProjectEntriesAndWrongGroupType()
    {
        var result = new VBProjectGroupLoader().Parse(
            "Type=Exe\nName=Broken\n",
            Path.Combine(Path.GetTempPath(), "Broken.vbg"));

        CollectionAssert.AreEquivalent(
            new[] { "VB6VBG0003", "VB6VBG0004" },
            result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    [DataRow("VB6VBG0001", "Type=Group\nnot an assignment\nProject=App.vbp")]
    [DataRow("VB6VBG0002", "Type=Group\nProject=\n")]
    public void Parse_ReportsMalformedProjectGroupEntries(string code, string source)
    {
        var result = new VBProjectGroupLoader().Parse(
            source,
            Path.Combine(Path.GetTempPath(), "Broken.vbg"));

        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == code));
    }
}
