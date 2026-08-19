using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class UserDefinedTypeMemberArrayBindingTests
{
    [TestMethod]
    public void Bind_ResolvesUdtArrayMemberRead()
    {
        var model = Bind("""
            Type Record
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim record As Record
                Dim value As Long
                value = record.Values(2)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var assignment = model.Procedures.Single().Body.Statements
            .OfType<BoundAssignmentStatement>()
            .Single();
        var access = assignment.Expression as BoundElementAccessExpression;
        Assert.IsNotNull(access);
        Assert.AreSame(TypeSymbol.Long, access.Type);
        Assert.AreEqual(1, access.Indices.Length);
        Assert.AreSame(TypeSymbol.Long, access.Indices[0].Type);

        var member = access.Receiver as BoundMemberAccessExpression;
        Assert.IsNotNull(member);
        Assert.AreEqual("Values", member.Member.Name);
        Assert.IsInstanceOfType<ArrayTypeSymbol>(member.Type);
    }

    [TestMethod]
    public void Bind_ResolvesWithUdtArrayMemberRead()
    {
        var model = Bind("""
            Type Record
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim record As Record
                With record
                    Debug.Print .Values(1)
                End With
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var withStatement = model.Procedures.Single().Body.Statements
            .OfType<BoundWithStatement>()
            .Single();
        var debugPrint = withStatement.Body.Statements
            .OfType<BoundDebugPrintStatement>()
            .Single();
        var access = debugPrint.Expression as BoundElementAccessExpression;
        Assert.IsNotNull(access);

        var member = access.Receiver as BoundMemberAccessExpression;
        Assert.IsNotNull(member);
        Assert.IsInstanceOfType<BoundWithReceiverExpression>(member.Receiver);
    }

    [TestMethod]
    public void Bind_AllowsUdtArrayMemberElementAsByRefArgument()
    {
        var model = Bind("""
            Type Record
                Values(1 To 3) As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 10
            End Sub

            Sub Main()
                Dim record As Record
                SetValue record.Values(2)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = main.Body.Statements.OfType<BoundInvocationStatement>().Single();
        Assert.IsInstanceOfType<BoundElementAccessExpression>(invocation.Arguments.Single().Expression);
    }

    [TestMethod]
    public void Bind_ReportsRankMismatchForUdtArrayMember()
    {
        var analysis = Analyze("""
            Type Record
                Values(1 To 3, 4 To 5) As Long
            End Type

            Sub Main()
                Dim record As Record
                Dim value As Long
                value = record.Values(1)
            End Sub
            """);

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0027"));
    }

    private static SemanticModel Bind(string source)
    {
        var analysis = Analyze(source);
        Assert.IsNotNull(analysis.SemanticModel, FormatDiagnostics(analysis));
        return analysis.SemanticModel;
    }

    private static TestAnalysis Analyze(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parse = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parse.Diagnostics.Length, string.Join(Environment.NewLine, parse.Diagnostics));
        var types = new UserDefinedTypeDeclarationBinder(text).Bind(parse.Root);
        using (UserDefinedTypeLookupScope.Push(types.Types))
        {
            var model = new Binder(text).BindCompilationUnit(parse.Root);
            return new TestAnalysis(model, types.Diagnostics.AddRange(model.Diagnostics));
        }
    }

    private static string FormatDiagnostics(SemanticModel model) =>
        string.Join(Environment.NewLine, model.Diagnostics.Select(diagnostic => diagnostic.ToString()));

    private static string FormatDiagnostics(TestAnalysis analysis) =>
        string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()));

    private sealed record TestAnalysis(
        SemanticModel? SemanticModel,
        ImmutableArray<Diagnostic> Diagnostics);
}
