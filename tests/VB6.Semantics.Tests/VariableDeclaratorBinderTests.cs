using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class VariableDeclaratorBinderTests
{
    [TestMethod]
    public void Bind_BindsEachTypedLocalDeclaratorIndependently()
    {
        var model = BindSource("""
            Sub Main()
                Dim small As Integer, wide As Long, label As String
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(3, procedure.Locals.Length);
        Assert.AreEqual(TypeSymbol.Integer, procedure.Locals.Single(local => local.Name == "small").Type);
        Assert.AreEqual(TypeSymbol.Long, procedure.Locals.Single(local => local.Name == "wide").Type);
        Assert.AreEqual(TypeSymbol.String, procedure.Locals.Single(local => local.Name == "label").Type);
        Assert.AreEqual(3, procedure.Body.Statements.OfType<BoundVariableDeclarationStatement>().Count());
    }

    [TestMethod]
    public void Bind_DiagnosesImplicitVariantWithoutBorrowingTrailingType()
    {
        var model = BindSource("""
            Sub Main()
                Dim implicitVariant, typed As Integer
            End Sub
            """);

        Assert.AreEqual(1, model.Diagnostics.Length);
        Assert.AreEqual("VB6S0020", model.Diagnostics[0].Code);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Error, procedure.Locals.Single(local => local.Name == "implicitVariant").Type);
        Assert.AreEqual(TypeSymbol.Integer, procedure.Locals.Single(local => local.Name == "typed").Type);
    }

    [TestMethod]
    public void Bind_BindsModuleDeclaratorsIndependently()
    {
        var model = BindSource("""
            Public Left As Integer, Right As Long
            Dim implicitVariant, Count As Long

            Sub Main()
            End Sub
            """);

        Assert.AreEqual(1, model.Diagnostics.Length);
        Assert.AreEqual("VB6S0020", model.Diagnostics[0].Code);
        Assert.AreEqual(TypeSymbol.Integer, model.ModuleVariables.Single(variable => variable.Symbol.Name == "Left").Symbol.Type);
        Assert.AreEqual(TypeSymbol.Long, model.ModuleVariables.Single(variable => variable.Symbol.Name == "Right").Symbol.Type);
        Assert.AreEqual(TypeSymbol.Error, model.ModuleVariables.Single(variable => variable.Symbol.Name == "implicitVariant").Symbol.Type);
        Assert.AreEqual(TypeSymbol.Long, model.ModuleVariables.Single(variable => variable.Symbol.Name == "Count").Symbol.Type);
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
