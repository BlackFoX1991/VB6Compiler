using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ModuleVariableBinderTests
{
    [TestMethod]
    public void Bind_MakesModuleVariablesVisibleInsideProcedures()
    {
        var model = BindSource("""
            Public Total As Long

            Sub Main()
                Total = 5
                Debug.Print Total
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        Assert.AreEqual(1, model.ModuleVariables.Length);
        Assert.AreEqual("Total", model.ModuleVariables[0].Name);
        Assert.AreEqual(TypeSymbol.Long, model.ModuleVariables[0].Type);

        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[0];
        Assert.IsInstanceOfType<ModuleVariableSymbol>(assignment.Variable);
    }

    [TestMethod]
    public void Bind_LetsLocalsShadowModuleVariables()
    {
        var model = BindSource("""
            Public Value As Long

            Sub Main()
                Dim Value As Integer
                Value = 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        Assert.IsInstanceOfType<LocalVariableSymbol>(assignment.Variable);
        Assert.AreEqual(TypeSymbol.Integer, assignment.Variable.Type);
    }

    [TestMethod]
    public void Bind_LetsParametersShadowModuleVariables()
    {
        var model = BindSource("""
            Public Value As Long

            Sub Show(ByVal Value As Integer)
                Debug.Print Value
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var print = (BoundDebugPrintStatement)model.Procedures.Single().Body.Statements[0];
        var variable = (BoundVariableExpression)print.Expression;
        Assert.IsInstanceOfType<ParameterSymbol>(variable.Variable);
    }

    [TestMethod]
    public void Bind_ReportsUnknownModuleVariableType()
    {
        var model = BindSource("""
            Public Thing As Widget

            Sub Main()
                Debug.Print 1
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
    }

    [TestMethod]
    public void Bind_ReportsDuplicateModuleVariable()
    {
        var model = BindSource("""
            Public Value As Long
            Private Value As Integer

            Sub Main()
                Debug.Print 1
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0019"));
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            parseResult.Diagnostics.Length,
            string.Join(Environment.NewLine, parseResult.Diagnostics.Select(d => d.ToString())));
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
