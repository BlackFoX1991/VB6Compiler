using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class UserDefinedTypeDeclarationBinderTests
{
    [TestMethod]
    public void Bind_ResolvesForwardUserDefinedTypeReference()
    {
        var result = Bind("""
            Type Container
                Position As Point
            End Type

            Type Point
                X As Long
                Y As Long
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        var container = result.Types["container"];
        var point = result.Types["POINT"];
        Assert.IsTrue(container.TryGetMember("position", out var position));
        Assert.AreSame(point, position.Type);
    }

    [TestMethod]
    public void Bind_PreservesFixedAndDynamicArrayMemberRanks()
    {
        var result = Bind("""
            Type Arrays
                FixedValues(1 To 2, 3 To 4) As Long
                DynamicValues() As Integer
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        var type = result.Types["Arrays"];

        Assert.IsTrue(type.TryGetMember("FixedValues", out var fixedValues));
        var fixedArray = (ArrayTypeSymbol)fixedValues.Type;
        Assert.AreEqual(2, fixedArray.Rank);
        Assert.AreEqual(TypeSymbol.Long, fixedArray.ElementType);

        Assert.IsTrue(type.TryGetMember("DynamicValues", out var dynamicValues));
        var dynamicArray = (ArrayTypeSymbol)dynamicValues.Type;
        Assert.IsNull(dynamicArray.Rank);
        Assert.AreEqual(TypeSymbol.Integer, dynamicArray.ElementType);
    }

    [TestMethod]
    public void Bind_CreatesFixedLengthStringMemberType()
    {
        var result = Bind("""
            Type Header
                Name As String * 16
            End Type
            """);

        Assert.IsTrue(result.Success, FormatDiagnostics(result));
        var type = result.Types["Header"];
        Assert.IsTrue(type.TryGetMember("Name", out var member));
        var fixedString = (FixedLengthStringTypeSymbol)member.Type;
        Assert.AreEqual(16, fixedString.Length);
    }

    [TestMethod]
    public void Bind_DiagnosesDuplicateMembersCaseInsensitively()
    {
        var result = Bind("""
            Type Broken
                Value As Long
                VALUE As Integer
            End Type
            """);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0037"));
    }

    [TestMethod]
    public void Bind_DiagnosesInvalidFixedStringLength()
    {
        var result = Bind("""
            Type Broken
                Name As String * 0
            End Type
            """);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0040"));
    }

    [TestMethod]
    public void Bind_DiagnosesUnknownMemberType()
    {
        var result = Bind("""
            Type Broken
                Value As DoesNotExist
            End Type
            """);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"));
    }

    private static UserDefinedTypeDeclarationResult Bind(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parse = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            parse.Diagnostics.Length,
            string.Join(Environment.NewLine, parse.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return new UserDefinedTypeDeclarationBinder(text).Bind(parse.Root);
    }

    private static string FormatDiagnostics(UserDefinedTypeDeclarationResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
