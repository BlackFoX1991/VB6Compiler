namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 procedure variables have procedure lifetime even when their Dim statement is nested or
/// jumped over. Storage that differs from the CLR zero value must therefore be initialized once
/// in the procedure prologue rather than when control reaches the declaration statement.
/// </summary>
[TestClass]
public sealed class ProcedureInitializationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_DoesNotReinitializeNestedStringDim()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Dim i As Long

                For i = 1 To 2
                    Dim value As String
                    If i = 1 Then
                        value = "kept"
                    End If
                    Debug.Print "[" & value & "]"
                Next i
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "[kept]", "[kept]" }, lines);
    }

    [TestMethod]
    public void EmitManagedApplication_DoesNotReinitializeNestedFixedArrayDim()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Dim i As Long

                For i = 1 To 2
                    Dim values(1 To 1) As Long
                    values(1) = values(1) + 1
                Next i

                Debug.Print values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2" }, lines);
    }

    [TestMethod]
    public void EmitManagedApplication_InitializesFixedArrayBeforeJumpOverDim()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                GoTo AfterDeclaration
                Dim values(1 To 1) As Long
            AfterDeclaration:
                Debug.Print values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0" }, lines);
    }

    [TestMethod]
    public void EmitManagedApplication_StringFunctionDefaultsToEmptyString()
    {
        var lines = VB6TestProgram.RunLines("""
            Function EmptyText() As String
            End Function

            Sub Main()
                Debug.Print "[" & UCase(EmptyText()) & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "[]" }, lines);
    }
}
