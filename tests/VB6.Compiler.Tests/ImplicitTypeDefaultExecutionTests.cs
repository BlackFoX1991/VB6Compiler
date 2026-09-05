namespace VB6.Compiler.Tests;

/// <summary>
/// <c>DefType</c> directives and the variables they type.
///
/// Without <c>Option Explicit</c> a VB6 variable comes into being at its first use, and a
/// <c>DefInt</c>-style directive decides its type by initial letter. The measurement that opened
/// <c>managed-r1-grammar</c> found the directives themselves in good shape and one hole beside
/// them: an implicit variable never passes a declaration statement, so nothing gave it the VB6
/// default value. For every type whose default matches the CLR's that is invisible. For
/// <c>String</c> it was not -- left at null it still concatenated and measured like the empty
/// string, so only <c>VarType</c> and <c>TypeName</c> gave it away.
///
/// The declared twin is asserted beside each implicit variable on purpose: the two must agree,
/// and a test that only checks the implicit side cannot tell a real default from a shared bug.
/// </summary>
[TestClass]
public sealed class ImplicitTypeDefaultExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_GivesAnImplicitStringTheSameDefaultAsADeclaredOne()
    {
        var output = VB6TestProgram.RunLines("""
            DefStr G

            Public Sub Main()
                Dim declared As String
                Debug.Print "declared|" & TypeName(declared) & "|" & VarType(declared) & "|[" & declared & "]"
                Debug.Print "implicit|" & TypeName(gg) & "|" & VarType(gg) & "|[" & gg & "]"
                Debug.Print "len|" & Len(gg)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "declared|String|8|[]", "implicit|String|8|[]", "len|0" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_TypesEveryDefTypeDirectiveByInitialLetter()
    {
        var output = VB6TestProgram.RunLines("""
            DefByte A
            DefInt B
            DefLng C
            DefSng D
            DefDbl E
            DefCur F
            DefStr G
            DefBool H
            DefDate I
            DefObj J
            DefVar K

            Public Sub Main()
                Debug.Print TypeName(aa) & "," & TypeName(bb) & "," & TypeName(cc) & "," & TypeName(dd)
                Debug.Print TypeName(ee) & "," & TypeName(ff) & "," & TypeName(gg) & "," & TypeName(hh)
                Debug.Print TypeName(ii) & "," & TypeName(jj) & "," & TypeName(kk)
            End Sub
            """);

        // DefObj and DefVar are not omissions: an unset object is Nothing and an unset Variant
        // is Empty, exactly as their declared counterparts report.
        CollectionAssert.AreEqual(
            new[]
            {
                "Byte,Integer,Long,Single",
                "Double,Currency,String,Boolean",
                "Date,Nothing,Empty",
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_AppliesDefTypeToRangesListsAndSingleLetters()
    {
        // Ranges must not overlap -- VB6 rejects that, and so does this compiler with VB6S0070,
        // which ImplicitVariantAnalysisTests already covers. Here every letter has one owner.
        var output = VB6TestProgram.RunLines("""
            DefInt A-C
            DefLng N-Z
            DefDbl D, E
            DefSng F

            Public Sub Main()
                Debug.Print "range-first|" & TypeName(alpha)
                Debug.Print "range-second|" & TypeName(nomen)
                Debug.Print "list|" & TypeName(delta) & "," & TypeName(epsilon)
                Debug.Print "single-letter|" & TypeName(factor)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "range-first|Integer",
                "range-second|Long",
                "list|Double,Double",
                "single-letter|Single",
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_LetsATypeSuffixOutrankTheDirective()
    {
        var output = VB6TestProgram.RunLines("""
            DefInt A-Z

            Public Sub Main()
                value$ = "text"
                Debug.Print "suffix|" & TypeName(value$) & "|" & value$
                Debug.Print "directive|" & TypeName(other)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "suffix|String|text", "directive|Integer" }, output);
    }
}
