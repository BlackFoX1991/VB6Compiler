using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// <c>ReDim Section(0).Bytes(0)</c> redimensions an array that lives inside a UDT element. Every
/// parenthesized list except the last selects an element on the way in; the final one carries the
/// new bounds. This was the first error in four conformance modules.
/// </summary>
[TestClass]
public sealed class ReDimQualifiedTargetParserTests
{
    [TestMethod]
    public void Parse_SeparatesTheBoundsFromTheElementSelection()
    {
        var statement = ParseReDim("ReDim Section(0).Bytes(0 To 4)");

        Assert.AreEqual(0, statement.Declarators.Length);
        var target = statement.QualifiedTargets.Single();

        // The bounds are the last list; Section(0) is the element being reached into.
        Assert.AreEqual(1, target.Dimensions.Length);
        var member = (MemberAccessExpressionSyntax)target.Target;
        Assert.AreEqual("Bytes", member.MemberToken.Text);
        Assert.IsInstanceOfType<ElementAccessExpressionSyntax>(member.Receiver);
    }

    [TestMethod]
    public void Parse_PreserveAndRestatedElementType()
    {
        var statement = ParseReDim("ReDim Preserve Section(2).Bytes(UBound(Section(2).Bytes) - 1) As Byte");

        Assert.IsNotNull(statement.PreserveKeyword);
        var target = statement.QualifiedTargets.Single();
        Assert.AreEqual("Byte", target.TypeToken!.Text);
    }

    [TestMethod]
    public void Parse_PlainReDimStillUsesDeclarators()
    {
        var statement = ParseReDim("ReDim Values(1 To 10) As Long");

        Assert.AreEqual(0, statement.QualifiedTargets.Length);
        Assert.AreEqual("Values", statement.Declarators.Single().Identifier.Text);
    }

    private static ReDimStatementSyntax ParseReDim(string statement)
    {
        var source = $"""
            Sub Main()
                {statement}
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(", ", result.Diagnostics.Select(d => d.Message)));

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return (ReDimStatementSyntax)procedure.Statements.Single();
    }
}
