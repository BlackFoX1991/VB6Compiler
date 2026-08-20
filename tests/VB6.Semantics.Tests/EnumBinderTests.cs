using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class EnumBinderTests
{
    [TestMethod]
    public void Bind_ResolvesEnumTypesAndMemberConstants()
    {
        var model = BindSource("""
            Public Enum Alignment
                Left = 0
                Center = 2
                Right
            End Enum

            Private Current As Alignment

            Sub Main()
                Current = Center
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));

        var enumType = model.EnumTypes.Single();
        Assert.AreEqual("Alignment", enumType.Name);
        CollectionAssert.AreEqual(
            new[] { "Left", "Center", "Right" },
            enumType.Members.Select(member => member.Name).ToArray());

        var current = model.ModuleVariables.Single(variable => variable.Symbol.Name == "Current");
        Assert.AreEqual(enumType, current.Symbol.Type);
        Assert.IsTrue(model.ModuleVariables.Any(variable =>
            variable.IsConstant && variable.Symbol.Name == "Center"));

        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements.Single();
        Assert.AreEqual(enumType, assignment.Variable.Type);
        Assert.AreEqual(enumType, assignment.Expression.Type);
    }

    [TestMethod]
    public void Bind_ResolvesOleColorAliasAsLong()
    {
        var model = BindSource("""
            Private BackColor As OLE_COLOR

            Sub Main()
                BackColor = 255
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        Assert.AreEqual(TypeSymbol.Long, model.ModuleVariables.Single().Symbol.Type);
    }

    [TestMethod]
    public void Bind_ResolvesObjectType()
    {
        var model = BindSource("""
            Private Current As Object

            Sub Main()
                Debug.Print 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        Assert.AreEqual(TypeSymbol.Object, model.ModuleVariables.Single().Symbol.Type);
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

    private static string FormatDiagnostics(SemanticModel model) =>
        string.Join(Environment.NewLine, model.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
