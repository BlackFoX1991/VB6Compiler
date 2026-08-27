using VB6.Compiler;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ConditionalCompilationDiagnosticTests
{
    [TestMethod]
    [DataRow("VB6CC0001", "#Const = 1")]
    [DataRow("VB6CC0002", "#If Unsupported.Function() Then\n#End If")]
    [DataRow("VB6CC0003", "#ElseIf True Then")]
    [DataRow("VB6CC0004", "#Else")]
    [DataRow("VB6CC0005", "#End If")]
    public void Analyze_ReportsMalformedConditionalDirectives(string code, string source)
    {
        var analysis = VBCompilation.Create(source, "Conditional.bas").Analyze();

        Assert.IsTrue(
            analysis.Diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Expected {code}, got: {string.Join(", ", analysis.Diagnostics.Select(diagnostic => diagnostic.Code))}");
    }

    [TestMethod]
    public void Analyze_ReportsUnsupportedProjectConditionalConstant()
    {
        var analysis = VBCompilation.Create(
            "Sub Main()\nEnd Sub",
            "Conditional.bas",
            new VBCompilationOptions(
                DefinedConstants: new Dictionary<string, string>
                {
                    ["FEATURE"] = "Unsupported.Function()"
                })).Analyze();

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6CC0007"));
    }
}
