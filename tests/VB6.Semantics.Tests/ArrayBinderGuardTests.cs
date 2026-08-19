using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayBinderGuardTests
{
    [TestMethod]
    public void Bind_LocalArrayPreservesElementTypeAndRankWhileExecutionRemainsGuarded()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 10, 0 To 4) As Long
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0025" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var arrayType = model.Procedures.Single().Locals.Single().Type as ArrayTypeSymbol;
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(TypeSymbol.Long, arrayType.ElementType);
        Assert.AreEqual(2, arrayType.Rank);
    }

    [TestMethod]
    public void Bind_DynamicModuleArrayPreservesElementTypeWhileExecutionRemainsGuarded()
    {
        var model = BindSource("""
            Private values() As Integer
            Sub Main()
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0025" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var arrayType = model.ModuleVariables.Single().Symbol.Type as ArrayTypeSymbol;
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(TypeSymbol.Integer, arrayType.ElementType);
        Assert.AreEqual(1, arrayType.Rank);
    }

    [TestMethod]
    public void Bind_ArrayParameterPreservesElementTypeWhileInvocationRemainsGuarded()
    {
        var model = BindSource("""
            Function Sort(TheArray() As String) As Long
                Sort = 0
            End Function
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0025" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var arrayType = model.Procedures.Single().Symbol.Parameters.Single().Type as ArrayTypeSymbol;
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(TypeSymbol.String, arrayType.ElementType);
        Assert.AreEqual(1, arrayType.Rank);
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
