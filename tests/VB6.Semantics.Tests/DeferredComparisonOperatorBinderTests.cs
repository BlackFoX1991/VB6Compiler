using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class DeferredComparisonOperatorBinderTests
{
    [TestMethod]
    public void Bind_LikeProducesDedicatedSemanticDiagnostic()
    {
        var model = BindSource("""
            Sub Main()
                Dim result As Boolean
                Dim value As String
                result = value Like "A*"
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0023" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Bind_IsProducesDedicatedSemanticDiagnostic()
    {
        var model = BindSource("""
            Sub Main()
                Dim result As Boolean
                Dim left As String
                Dim right As String
                result = left Is right
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0024" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    /// <summary>
    /// Event dispatch does not exist yet. Without a diagnostic the statement would vanish from
    /// the bound tree and the generated program would run and quietly raise nothing.
    /// </summary>
    [TestMethod]
    public void Bind_RaiseEventProducesDedicatedSemanticDiagnostic()
    {
        var model = BindSource("""
            Public Event CaptionChanged(ByVal OldValue As String)

            Sub Main()
                Dim value As String
                RaiseEvent CaptionChanged(value)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0049" },
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