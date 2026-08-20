using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// A VB6 Function may omit its As clause and then returns Variant. The parser preserves that
/// faithfully - both tokens stay absent - and leaves the defaulting to the implicit Variant
/// lowering, the same split the untyped Dim declarators use.
/// </summary>
[TestClass]
public sealed class UntypedFunctionParserTests
{
    [TestMethod]
    public void Parse_UntypedFunctionKeepsReturnTypeAbsent()
    {
        const string source = """
            Function SetImportUsed(Name As String, Offset As Long)
                SetImportUsed = 0
            End Function
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual("SetImportUsed", function.Identifier.Text);
        Assert.AreEqual(2, function.Parameters.Length);
        Assert.IsNull(function.AsKeyword);
        Assert.IsNull(function.ReturnTypeToken);
    }

    [TestMethod]
    public void Parse_TypedFunctionStillCapturesReturnType()
    {
        const string source = """
            Function Doubled(ByVal Value As Long) As Long
                Doubled = Value * 2
            End Function
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.IsNotNull(function.AsKeyword);
        Assert.AreEqual("Long", function.ReturnTypeToken!.Text);
    }
}
