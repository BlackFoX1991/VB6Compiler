namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 has two families of string functions. The Variant form passes Null through untouched; the
/// dollar form is typed String and refuses it with 94. Getting the pair right matters because
/// legacy code reads database fields into Variants and relies on Null surviving the round trip.
/// </summary>
[TestClass]
public sealed class NullPropagationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PassesNullThroughTheVariantStringFunctions()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v As Variant
                v = Null

                ' vbNull ist 1. Jede dieser Funktionen reicht Null weiter, statt zu melden.
                Debug.Print VarType(Left(v, 2))
                Debug.Print VarType(Right(v, 2))
                Debug.Print VarType(Mid(v, 1, 1))
                Debug.Print VarType(Trim(v))
                Debug.Print VarType(LTrim(v))
                Debug.Print VarType(RTrim(v))
                Debug.Print VarType(UCase(v))
                Debug.Print VarType(LCase(v))
                Debug.Print VarType(Len(v))
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "1", "1", "1", "1", "1", "1", "1", "1", "1" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_RefusesNullInTheTypedStringFunctions()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim v As Variant
                Dim s As String
                v = Null

                ' Die Dollar-Form ist String -> String. Null hat dort keinen Platz, und VB6 meldet
                ' 94 statt still eine leere Zeichenkette zu liefern.
                s = Left$(v, 2)
                Debug.Print Err.Number
                Err.Clear
                s = UCase$(v)
                Debug.Print Err.Number
                Err.Clear
                s = Trim$(v)
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "94", "94", "94" }, output);
    }
}
