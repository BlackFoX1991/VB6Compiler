using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

/// <summary>
/// The compiler's rule is to report a diagnostic rather than quietly do something similar, which
/// makes the diagnostics the safety net of that rule. A diagnostic nothing ever exercises is a
/// hole in it: the message can rot, the condition can stop firing, and no test notices.
///
/// These cases close that gap for the semantic codes that had no test at all. They assert the
/// code, not the message text, so wording stays free to improve.
/// </summary>
[TestClass]
public sealed class UncoveredDiagnosticTests
{
    [TestMethod]
    public void Bind_ReportsADuplicateLocalVariable()
    {
        AssertDiagnostic("VB6S0002", """
            Sub Main()
                Dim value As Long
                Dim value As Long
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsADuplicateParameter()
    {
        AssertDiagnostic("VB6S0009", """
            Sub Main(ByVal value As Long, ByVal value As Long)
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsANonNumericForControlVariable()
    {
        AssertDiagnostic("VB6S0012", """
            Sub Main()
                Dim text As String
                For text = 1 To 10
                Next text
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsAMismatchedNextVariable()
    {
        AssertDiagnostic("VB6S0013", """
            Sub Main()
                Dim index As Long
                Dim other As Long
                For index = 1 To 10
                Next other
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsADoLoopWithTwoConditions()
    {
        AssertDiagnostic("VB6S0014", """
            Sub Main()
                Dim index As Long
                Do While index < 10
                    index = index + 1
                Loop Until index > 5
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsANonNumericNotOperand()
    {
        AssertDiagnostic("VB6S0017", """
            Sub Main()
                Dim text As String
                Dim value As Boolean
                value = Not text
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsADuplicateUserDefinedType()
    {
        AssertTypeDiagnostic("VB6S0040", """
            Type Point
                X As Long
            End Type

            Type Point
                Y As Long
            End Type
            """);
    }

    [TestMethod]
    public void Bind_ReportsAFixedLengthMemberThatIsNotAString()
    {
        AssertTypeDiagnostic("VB6S0042", """
            Type Record
                Value As Long * 8
            End Type
            """);
    }

    [TestMethod]
    public void Bind_ReportsAFixedLengthMemberWithoutAConstantLength()
    {
        AssertTypeDiagnostic("VB6S0043", """
            Type Record
                Value As String * Length
            End Type
            """);
    }

    [TestMethod]
    public void Bind_ReportsAnUnsupportedOpenMode()
    {
        // Binary, Input, Output, Append and Random are bound; anything else is reported rather
        // than approximated with the closest supported mode.
        AssertDiagnostic("VB6S0057", """
            Sub Main()
                Open "data.bin" For Encrypted As #1
                Close #1
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsAGetTargetThatIsNotAssignable()
    {
        AssertDiagnostic("VB6S0059", """
            Sub Main()
                Open "data.bin" For Binary As #1
                Get #1, , 42
                Close #1
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsALineInputTargetThatIsNotAString()
    {
        AssertDiagnostic("VB6S0060", """
            Sub Main()
                Dim value As Long
                Open "data.txt" For Input As #1
                Line Input #1, value
                Close #1
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsAParamArrayWithADefaultValue()
    {
        AssertDiagnostic("VB6S0065", """
            Sub Main(ParamArray values() As Variant = 1)
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsReDimOnAParamArray()
    {
        AssertDiagnostic("VB6S0066", """
            Sub Main(ParamArray values() As Variant)
                ReDim values(3)
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsAJumpToAnUndeclaredLabel()
    {
        AssertDiagnostic("VB6S0061", """
            Sub Main()
                GoTo Missing
            End Sub
            """);
    }

    [TestMethod]
    public void Bind_ReportsAnUnknownNamedArgument()
    {
        AssertDiagnostic("VB6S0069", """
            Sub Target(ByVal value As Long)
            End Sub

            Sub Main()
                Target missing:=1
            End Sub
            """);
    }

    /// <summary>
    /// User-defined type declarations are bound by their own pass, before the procedure binder
    /// runs, so their diagnostics have to be collected there.
    /// </summary>
    private static void AssertTypeDiagnostic(string code, string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parse = new ParserType(text).ParseCompilationUnit();
        var result = new UserDefinedTypeDeclarationBinder(text, null).Bind(parse.Root);
        var diagnostics = parse.Diagnostics.Concat(result.Diagnostics).ToArray();

        Assert.IsTrue(
            diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Expected {code}, got: " +
            (diagnostics.Length == 0
                ? "no diagnostics at all"
                : string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Code))));
    }

    private static void AssertDiagnostic(string code, string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        var model = new Binder(text).BindCompilationUnit(parseResult.Root);
        var diagnostics = parseResult.Diagnostics
            .Concat(model.Diagnostics)
            .ToArray();

        Assert.IsTrue(
            diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Expected {code}, got: " +
            (diagnostics.Length == 0
                ? "no diagnostics at all"
                : string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Code))));
    }
}
