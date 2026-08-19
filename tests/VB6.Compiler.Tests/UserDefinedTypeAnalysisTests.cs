using VB6.Semantics;

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
    public void Analyze_BindsUdtIdentityIntoValuesAndArrays()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Y As Long
            End Type

            Public Current As Point

            Function Echo(ByRef value As Point) As Point
                Dim local As Point
                Echo = value
            End Function

            Sub Main()
                Dim points(1 To 2) As Point
            End Sub
            """, "test.bas").Analyze();

        Assert.IsNotNull(analysis.UserDefinedTypes);
        Assert.IsNotNull(analysis.SemanticModel);
        var point = analysis.UserDefinedTypes.Types["Point"];

        var current = analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "Current");
        Assert.AreSame(point, current.Symbol.Type);

        var echo = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Echo");
        Assert.AreSame(point, echo.Symbol.ReturnType);
        Assert.AreSame(point, echo.Symbol.Parameters.Single().Type);
        Assert.AreSame(point, echo.Locals.Single(local => local.Name == "local").Type);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var points = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "points").Type;
        Assert.AreSame(point, points.ElementType);
        Assert.AreEqual(1, points.Rank);

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }

    [TestMethod]
    public void GenerateCSharp_EmitsManagedUdtStorage()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim value As Point
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "private struct __vb6_udt_Point");
        StringAssert.Contains(generation.Source, "public int __vb6_member_X;");
        StringAssert.Contains(generation.Source, "__vb6_udt_Point __vb6_value = default;");
        Assert.IsFalse(generation.Source.Contains("object?", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GenerateCSharp_StopsOnArrayMemberLayout()
    {
        var generation = VBCompilation.Create("""
            Type Buffer
                Values(0 To 3) As Long
            End Type

            Sub Main()
                Dim value As Buffer
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }

    [TestMethod]
    public void GenerateCSharp_EmitsFixedStringMemberLayout()
    {
        var generation = VBCompilation.Create("""
            Type Record
                Name As String * 16
            End Type

            Sub Main()
                Dim value As Record
                value.Name = "hello"
                Debug.Print value.Name
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "private string? __vb6_fixed_Name;");
        StringAssert.Contains(generation.Source, "public string __vb6_member_Name");
        StringAssert.Contains(generation.Source, "new string(' ', 16)");
        StringAssert.Contains(generation.Source, "__vb6_value[..16]");
        StringAssert.Contains(generation.Source, "__vb6_value.PadRight(16)");
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
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