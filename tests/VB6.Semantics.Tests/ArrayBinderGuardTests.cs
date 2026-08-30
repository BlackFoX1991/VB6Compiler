using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayBinderGuardTests
{
    [TestMethod]
    public void Bind_FixedLocalArrayPreservesElementTypeAndRankWithoutGuard()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 10, 0 To 4) As Long
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);

        var arrayType = model.Procedures.Single().Locals.Single().Type as ArrayTypeSymbol;
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(TypeSymbol.Long, arrayType.ElementType);
        Assert.AreEqual(2, arrayType.Rank);
    }

    [TestMethod]
    public void Bind_DynamicModuleArrayPreservesElementTypeAndUnknownRank()
    {
        var model = BindSource("""
            Private values() As Integer
            Sub Main()
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);

        var arrayType = model.ModuleVariables.Single().Symbol.Type as ArrayTypeSymbol;
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(TypeSymbol.Integer, arrayType.ElementType);
        Assert.IsNull(arrayType.Rank);
        Assert.IsFalse(arrayType.HasKnownRank);
    }

    [TestMethod]
    public void Bind_ArrayParameterPreservesElementTypeWithUnknownRank()
    {
        var model = BindSource("""
            Function Sort(TheArray() As String) As Long
                Sort = 0
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);

        var arrayType = model.Procedures.Single().Symbol.Parameters.Single().Type as ArrayTypeSymbol;
        Assert.IsNotNull(arrayType);
        Assert.AreEqual(TypeSymbol.String, arrayType.ElementType);
        Assert.IsNull(arrayType.Rank);
        Assert.IsFalse(arrayType.HasKnownRank);
    }

    [TestMethod]
    public void Validate_ReportsUnsupportedUdtArrayElementLayout()
    {
        var text = SourceText.From("""
            Type Record
                Values() As Variant
            End Type

            Sub Main()
                Dim value As Record
            End Sub
            """, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            parseResult.Diagnostics.Length,
            string.Join(Environment.NewLine, parseResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));

        var types = new UserDefinedTypeDeclarationBinder(text).Bind(parseResult.Root);
        var diagnostics = UserDefinedTypeValueGuard.Validate(text, parseResult.Root, types.Types);

        CollectionAssert.AreEqual(
            new[] { "VB6S0046" },
            diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
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
