using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class UserDefinedTypeBinderTests
{
    [TestMethod]
    public void Bind_UserDefinedTypeFieldsAndMemberAccess()
    {
        var model = BindSource("""
            Type Point
                X As Long
                Name As String * 16
                Values(1 To 2) As Integer
            End Type

            Sub Main()
                Dim point As Point
                point.X = 10
                point.Values(1) = 20
                Debug.Print point.X
                Debug.Print point.Values(1)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var type = model.UserDefinedTypes.Single();
        Assert.AreEqual("Point", type.Name);
        Assert.AreEqual(TypeSymbol.Long, type.FindField("X")!.Type);
        Assert.AreEqual(TypeSymbol.String, type.FindField("Name")!.Type);
        Assert.IsNotNull(type.FindField("Name")!.FixedStringLength);
        Assert.IsInstanceOfType<ArrayTypeSymbol>(type.FindField("Values")!.Type);

        var procedure = model.Procedures.Single();
        Assert.AreEqual(type, procedure.Locals.Single().Type);
        var assignment = (BoundMemberAssignmentStatement)procedure.Body.Statements[1];
        Assert.AreEqual("X", assignment.Field.Name);
        var arrayAssignment = (BoundMemberArrayElementAssignmentStatement)procedure.Body.Statements[2];
        Assert.AreEqual("Values", arrayAssignment.Field.Name);
        Assert.AreEqual(TypeSymbol.Integer, arrayAssignment.Expression.Type);
        var print = (BoundDebugPrintStatement)procedure.Body.Statements[3];
        Assert.IsInstanceOfType<BoundMemberAccessExpression>(print.Expression);
        var arrayPrint = (BoundDebugPrintStatement)procedure.Body.Statements[4];
        Assert.IsInstanceOfType<BoundMemberArrayElementExpression>(arrayPrint.Expression);
    }

    [TestMethod]
    public void Bind_WithBlockBindsImplicitMemberAccess()
    {
        var model = BindSource("""
            Type Point
                X As Long
                Values(1 To 2) As Integer
            End Type

            Sub Main()
                Dim point As Point
                With point
                    .X = 10
                    .Values(1) = 20
                    Debug.Print .X
                    Debug.Print .Values(1)
                End With
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var procedure = model.Procedures.Single();
        var withStatement = (BoundWithStatement)procedure.Body.Statements[1];
        Assert.IsInstanceOfType<BoundVariableExpression>(withStatement.Target);
        Assert.AreEqual(4, withStatement.Body.Statements.Length);

        var assignment = (BoundMemberAssignmentStatement)withStatement.Body.Statements[0];
        Assert.AreEqual("X", assignment.Field.Name);
        var arrayAssignment = (BoundMemberArrayElementAssignmentStatement)withStatement.Body.Statements[1];
        Assert.AreEqual("Values", arrayAssignment.Field.Name);
        var print = (BoundDebugPrintStatement)withStatement.Body.Statements[2];
        Assert.IsInstanceOfType<BoundMemberAccessExpression>(print.Expression);
        var arrayPrint = (BoundDebugPrintStatement)withStatement.Body.Statements[3];
        Assert.IsInstanceOfType<BoundMemberArrayElementExpression>(arrayPrint.Expression);
    }

    [TestMethod]
    public void Bind_NestedUserDefinedTypeMemberAssignmentAndValueCopy()
    {
        var model = BindSource("""
            Type Inner
                Value As Long
            End Type

            Type Outer
                Inner As Inner
            End Type

            Sub Main()
                Dim first As Outer
                Dim second As Outer
                first.Inner.Value = 10
                second = first
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var outer = model.UserDefinedTypes.Single(type => type.Name == "Outer");
        Assert.AreEqual("Inner", outer.FindField("Inner")!.Type.Name);

        var procedure = model.Procedures.Single();
        var nestedAssignment = (BoundMemberAssignmentStatement)procedure.Body.Statements[2];
        Assert.AreEqual("Value", nestedAssignment.Field.Name);
        Assert.IsInstanceOfType<BoundMemberAccessExpression>(nestedAssignment.Target);

        var copyAssignment = (BoundAssignmentStatement)procedure.Body.Statements[3];
        Assert.AreEqual("second", copyAssignment.Variable.Name);
        Assert.AreEqual(outer, copyAssignment.Variable.Type);
        Assert.AreEqual(outer, copyAssignment.Expression.Type);
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
