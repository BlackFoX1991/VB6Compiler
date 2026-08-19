using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class TypeDeclarationParserTests
{
    [TestMethod]
    public void Parse_RecognizesPublicTypeWithScalarMembers()
    {
        var declaration = ParseType("""
            Public Type Point
                X As Long
                Y As Long
            End Type
            """);

        Assert.AreEqual("Public", declaration.VisibilityKeyword?.Text, ignoreCase: true);
        Assert.AreEqual("Point", declaration.Identifier.Text);
        Assert.AreEqual(2, declaration.Members.Length);
        Assert.AreEqual("X", declaration.Members[0].Identifier.Text);
        Assert.AreEqual("Long", declaration.Members[0].TypeToken.Text, ignoreCase: true);
        Assert.IsFalse(declaration.Members[0].IsArray);
        Assert.IsFalse(declaration.Members[0].IsFixedLengthString);
    }

    [TestMethod]
    public void Parse_PreservesArrayBoundsAndNestedTypeName()
    {
        var declaration = ParseType("""
            Private Type Record
                Values(1 To 3, -1 To 1) As Integer
                Position As Point
            End Type
            """);

        Assert.AreEqual("Private", declaration.VisibilityKeyword?.Text, ignoreCase: true);
        Assert.AreEqual(2, declaration.Members.Length);
        Assert.IsTrue(declaration.Members[0].IsArray);
        Assert.AreEqual(2, declaration.Members[0].Dimensions.Length);
        Assert.IsNotNull(declaration.Members[0].Dimensions[0].LowerBound);
        Assert.IsNotNull(declaration.Members[0].Dimensions[1].LowerBound);
        Assert.AreEqual("Point", declaration.Members[1].TypeToken.Text);
    }

    [TestMethod]
    public void Parse_PreservesFixedLengthStringExpression()
    {
        var declaration = ParseType("""
            Type Header
                Name As String * 16
                Labels(0 To 1) As String * 8
            End Type
            """);

        Assert.AreEqual(2, declaration.Members.Length);
        Assert.IsTrue(declaration.Members[0].IsFixedLengthString);
        Assert.IsNotNull(declaration.Members[0].StarToken);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(declaration.Members[0].FixedStringLength);
        Assert.IsTrue(declaration.Members[1].IsArray);
        Assert.IsTrue(declaration.Members[1].IsFixedLengthString);
    }

    [TestMethod]
    public void Parse_AllowsKeywordMemberNames()
    {
        var declaration = ParseType("""
            Type KeywordNames
                Print As Long
                Type As Integer
            End Type
            """);

        Assert.AreEqual(2, declaration.Members.Length);
        Assert.AreEqual("Print", declaration.Members[0].Identifier.Text, ignoreCase: true);
        Assert.AreEqual("Type", declaration.Members[1].Identifier.Text, ignoreCase: true);
    }

    [TestMethod]
    public void Parse_RecoversFromMalformedTypeMember()
    {
        var result = new ParserType(SourceText.From("""
            Type Broken
                , As Long
                Value As Long
            End Type
            """, "test.bas")).ParseCompilationUnit();

        Assert.IsTrue(result.Diagnostics.Length > 0);
        var declaration = (TypeDeclarationSyntax)result.Root.Members.Single();
        Assert.IsTrue(declaration.Members.Any(member => member.Identifier.Text == "Value"));
    }

    private static TypeDeclarationSyntax ParseType(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return (TypeDeclarationSyntax)result.Root.Members.Single();
    }
}
