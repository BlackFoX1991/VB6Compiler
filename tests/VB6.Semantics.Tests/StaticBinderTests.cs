using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class StaticBinderTests
{
    [TestMethod]
    public void Bind_ModelsStaticLocalAsPersistentStorage()
    {
        var model = BindSource("""
            Function NextValue() As Long
                Static count As Long
                count = count + 1
                NextValue = count
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, string.Join(Environment.NewLine, model.Diagnostics));

        var procedure = model.Procedures.Single();
        Assert.AreEqual(0, procedure.Locals.Length);
        var storage = model.StaticVariables.Single();
        Assert.AreEqual(TypeSymbol.Long, storage.Symbol.Type);

        var increment = (BoundAssignmentStatement)procedure.Body.Statements[0];
        Assert.AreEqual(storage.Symbol, increment.Variable);
        var read = (BoundBinaryExpression)increment.Expression;
        Assert.AreEqual(storage.Symbol, ((BoundVariableExpression)read.Left).Variable);
    }

    [TestMethod]
    public void Bind_UntypedStaticDefaultsToVariant()
    {
        var model = BindSource("""
            Sub Main()
                Static value
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0020" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual(TypeSymbol.Error, model.StaticVariables.Single().Symbol.Type);
        Assert.AreEqual(0, model.Procedures.Single().Locals.Length);
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
