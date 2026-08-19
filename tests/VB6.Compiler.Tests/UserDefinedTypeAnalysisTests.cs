namespace VB6.Compiler.Tests;

[TestClass]
public sealed class UserDefinedTypeAnalysisTests
{
    [TestMethod]
    public void Analyze_ExposesBoundUserDefinedTypes()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Y As Long
            End Type
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.UserDefinedTypes);
        Assert.IsTrue(analysis.UserDefinedTypes.Types.ContainsKey("point"));
        Assert.AreEqual(2, analysis.UserDefinedTypes.Types["Point"].Members.Length);
    }

    [TestMethod]
    public void GenerateCSharp_StopsOnInvalidUserDefinedTypeMember()
    {
        var generation = VBCompilation.Create("""
            Type Broken
                Value As MissingType
            End Type
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
    }
}
