using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// <c>ReDim Section(0).Bytes(0)</c> redimensions an array inside a UDT element. The bound model
/// still expects a plain variable, so the construct is reported rather than approximated - but the
/// parser has to keep going, because the unreported dot used to derail the whole procedure. This
/// was the first error in four conformance modules.
/// </summary>
[TestClass]
public sealed class ReDimQualifiedTargetParserTests
{
    [TestMethod]
    public void Parse_ReportsQualifiedReDimTargetWithoutDerailingTheProcedure()
    {
        const string source = """
            Sub InitSections()
                ReDim Section(0).Bytes(0)
                Debug.Print 1
                Debug.Print 2
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        var diagnostic = result.Diagnostics.Single();
        Assert.AreEqual("VB6P0002", diagnostic.Code);
        StringAssert.Contains(diagnostic.Message, "ReDim");

        // The statements after the unsupported line still parse, which is the point of the recovery.
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(2, procedure.Statements.OfType<DebugPrintStatementSyntax>().Count());
    }

    [TestMethod]
    public void Parse_PlainReDimTargetStaysDiagnosticFree()
    {
        const string source = """
            Sub InitSections()
                ReDim Section(0) As Long
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
