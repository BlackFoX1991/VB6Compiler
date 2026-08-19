using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ModuleVariableCodeGenTests
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
    public void Generate_EmitsModuleVariablesAsStaticFields()
    {
        var source = Generate("""
            Attribute VB_Name = "Module1"
            Public Total As Long
            Private Label As String

            Public Sub Main()
                Total = 1
                Debug.Print Total
            End Sub
            """);

        StringAssert.Contains(source, "private static int __vb6_Total = 0;");
        StringAssert.Contains(source, "private static string __vb6_Label = string.Empty;");
    }

    [TestMethod]
    public void Generate_SharesModuleVariablesBetweenProcedures()
    {
        var source = Generate("""
            Public Counter As Integer

            Sub Bump()
                Counter = Counter + 1
            End Sub

            Sub Main()
                Bump
                Debug.Print Counter
            End Sub
            """);

        // One field, referenced from both procedures rather than redeclared per procedure.
        Assert.AreEqual(1, CountOccurrences(source, "private static short __vb6_Counter"));
        Assert.IsTrue(CountOccurrences(source, "__vb6_Counter") >= 4);
    }

    [TestMethod]
    public void Generate_EmitsLocalDeclarationWhenALocalShadowsAModuleVariable()
    {
        var source = Generate("""
            Public Value As Long

            Sub Main()
                Dim Value As Integer
                Value = 1
                Debug.Print Value
            End Sub
            """);

        StringAssert.Contains(source, "private static int __vb6_Value = 0;");
        StringAssert.Contains(source, "short __vb6_Value = 0;");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
