using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class StaticBinderTests
{
    [TestMethod]
    public void Bind_PredeclaresStaticLocalButDiagnosesLifetimeSemantics()
    {
        var model = BindSource("""
            Function NextValue() As Long
                Static count As Long
                count = count + 1
                NextValue = count
            End Function
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0021" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var procedure = model.Procedures.Single();
        var local = procedure.Locals.Single();
        Assert.AreEqual("count", local.Name);
        Assert.AreEqual(TypeSymbol.Long, local.Type);
        Assert.IsFalse(procedure.Body.Statements.Any(statement => statement is BoundVariableDeclarationStatement));

        var increment = (BoundAssignmentStatement)procedure.Body.Statements[0];
        Assert.AreEqual(local, increment.Variable);
        var read = (BoundBinaryExpression)increment.Expression;
        Assert.AreEqual(local, ((BoundVariableExpression)read.Left).Variable);
    }

    [TestMethod]
    public void Bind_UntypedStaticAlsoReportsVariantGap()
    {
        var model = BindSource("""
            Sub Main()
                Static value
            End Sub
            """);

        CollectionAssert.AreEquivalent(
            new[] { "VB6S0020", "VB6S0021" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
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