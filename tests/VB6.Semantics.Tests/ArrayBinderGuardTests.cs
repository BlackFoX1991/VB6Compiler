using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayBinderGuardTests
{
    [TestMethod]
    public void Bind_LocalArrayProducesDedicatedSemanticDiagnostic()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 10) As Long
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0025" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual(TypeSymbol.Error, model.Procedures.Single().Locals.Single().Type);
    }

    [TestMethod]
    public void Bind_ModuleArrayProducesDedicatedSemanticDiagnostic()
    {
        var model = BindSource("""
            Private values(10) As Integer
            Sub Main()
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0025" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual(TypeSymbol.Error, model.ModuleVariables.Single().Symbol.Type);
    }

    [TestMethod]
    public void Bind_ArrayParameterProducesDedicatedSemanticDiagnostic()
    {
        var model = BindSource("""
            Function Sort(TheArray() As String) As Long
                Sort = 0
            End Function
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0025" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual(TypeSymbol.Error, model.Procedures.Single().Symbol.Parameters.Single().Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            parseResult.Diagnostics.Length,
            string.Join(Environment.NewLine, parseResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}