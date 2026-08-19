using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class OptionalParameterBinderTests
{
    [TestMethod]
    public void Bind_DiagnosesOmittedOptionalArgumentUntilDefaultSemanticsAreImplemented()
    {
        const string source = """
            Sub Configure(Optional retries As Long = 3)
            End Sub

            Sub Main()
                Configure
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(1, model.Diagnostics.Length);
        Assert.AreEqual("VB6S0006", model.Diagnostics[0].Code);
        StringAssert.Contains(model.Diagnostics[0].Message, "expects 1 argument(s), but 0 were supplied");
    }
}
