using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class BitwiseExpressionCodeGenTests
{
    private static string Generate(string source)
    {
        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();
        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        return new CSharpGenerator().Generate(analysis.SemanticModel!);
    }

    [TestMethod]
    public void Generate_EmitsIntegerBitwiseRuntimeCalls()
    {
        var source = Generate("""
            Sub Main()
                Dim value As Integer
                value = 12 And 10
                Debug.Print value
            End Sub
            """);

        StringAssert.Contains(source, "VBOperators.AndInteger(");
    }

    [TestMethod]
    public void Generate_EmitsLongBitwiseRuntimeCalls()
    {
        var source = Generate("""
            Sub Main()
                Dim wide As Long
                Dim value As Long
                wide = 70000
                value = wide Or 1
                Debug.Print value
            End Sub
            """);

        StringAssert.Contains(source, "VBOperators.OrLong(");
    }

    [TestMethod]
    public void Generate_EmitsNumericNotRuntimeCall()
    {
        var source = Generate("""
            Sub Main()
                Dim value As Integer
                value = Not 1
                Debug.Print value
            End Sub
            """);

        StringAssert.Contains(source, "VBOperators.NotInteger(");
    }

    [TestMethod]
    public void Generate_KeepsBooleanOperatorsOnTheBooleanOverloads()
    {
        var source = Generate("""
            Sub Main()
                Dim flag As Boolean
                flag = True And Not False
                Debug.Print flag
            End Sub
            """);

        StringAssert.Contains(source, "VBOperators.AndBoolean(");
        StringAssert.Contains(source, "VBOperators.NotBoolean(");
    }

    [TestMethod]
    public void Generate_EmitsHexadecimalLiteralsWithTheirWrappedValue()
    {
        var source = Generate("""
            Sub Main()
                Dim value As Integer
                value = &HFFFF
                Debug.Print value
            End Sub
            """);

        StringAssert.Contains(source, "VBConversions.CInt(-1L)");
    }
}
