using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class StaticBinderTests
{
    [TestMethod]
    public void Bind_PredeclaresStaticLocalWithPersistentLifetimeFlag()
    {
        var model = BindSource("""
            Function NextValue() As Long
                Static count As Long
                count = count + 1
                NextValue = count
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);

        var procedure = model.Procedures.Single();
        var local = procedure.Locals.Single();
        Assert.AreEqual("count", local.Name);
        Assert.AreEqual(TypeSymbol.Long, local.Type);
        Assert.IsTrue(local.IsStatic);
        Assert.AreEqual(local, procedure.StaticLocals.Single().Symbol);
        Assert.IsFalse(procedure.Body.Statements.Any(statement => statement is BoundVariableDeclarationStatement));

        var increment = (BoundAssignmentStatement)procedure.Body.Statements[0];
        Assert.AreEqual(local, increment.Variable);
        var read = (BoundBinaryExpression)increment.Expression;
        Assert.AreEqual(local, ((BoundVariableExpression)read.Left).Variable);
    }

    [TestMethod]
    public void Bind_UntypedStaticAsVariant()
    {
        var model = BindSource("""
            Sub Main()
                Static value
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var local = model.Procedures.Single().Locals.Single();
        Assert.AreEqual(TypeSymbol.Variant, local.Type);
        Assert.IsTrue(local.IsStatic);
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
