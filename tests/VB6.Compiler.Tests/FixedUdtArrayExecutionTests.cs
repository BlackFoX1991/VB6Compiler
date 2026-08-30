using VB6.IR;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FixedUdtArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedUdtArrayMembersWithIndependentCopies()
    {
        var compilation = VBCompilation.Create("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 30
            End Sub

            Sub Main()
                Dim first As Record
                Dim copied As Record
                Dim items(1 To 1) As Record

                Debug.Print first.Values(1)
                first.Values(1) = 10
                first.Values(2) = 20

                copied = first
                copied.Values(1) = 99

                Debug.Print first.Values(1)
                Debug.Print copied.Values(1)

                SetValue copied.Values(2)
                Debug.Print first.Values(2)
                Debug.Print copied.Values(2)

                With copied
                    .Values(1) = 7
                    Debug.Print .Values(1)
                End With

                items(1).Values(1) = 40
                copied = items(1)
                copied.Values(1) = 41
                Debug.Print items(1).Values(1)
                Debug.Print copied.Values(1)
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { "0", "10", "99", "20", "30", "7", "40", "41" },
            lines);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesNestedUdtArrayBoundsDefaultsAndByRefWriteBack()
    {
        var output = VB6TestProgram.RunLines("""
            Type Child
                Amount As Long
            End Type

            Type Container
                Entries(2 To 3, -1 To 0) As Child
            End Type

            Sub SetAmount(ByRef amount As Long)
                amount = 42
            End Sub

            Sub Main()
                Dim value As Container

                Debug.Print value.Entries(2, -1).Amount
                Debug.Print LBound(value.Entries, 1)
                Debug.Print UBound(value.Entries, 1)
                Debug.Print LBound(value.Entries, 2)
                Debug.Print UBound(value.Entries, 2)

                SetAmount value.Entries(3, 0).Amount
                Debug.Print value.Entries(3, 0).Amount
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0", "2", "3", "-1", "0", "42" }, output);
    }

    /// <summary>
    /// Every place VB6 copies a user-defined type by value has to produce an independent value:
    /// assignment, an array element, a member of another type, a ByVal argument and a function
    /// result. Writing into the copy after each of them must leave the source untouched.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_CopiesUdtStorageAtEveryValueBoundary()
    {
        var lines = VB6TestProgram.RunLines("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Type Holder
                Child As Record
            End Type

            Sub Consume(ByVal value As Record)
                value.Values(1) = 91
            End Sub

            Sub Touch(ByRef value As Record)
                value.Values(1) = 92
            End Sub

            Function Copy(ByVal value As Record) As Record
                Copy = value
            End Function

            Sub Main()
                Dim value As Record
                Dim copied As Record
                Dim items(1 To 1) As Record
                Dim holder As Holder

                value.Values(1) = 1

                copied = value
                copied.Values(1) = 2
                Debug.Print value.Values(1)

                items(1) = value
                items(1).Values(1) = 3
                Debug.Print value.Values(1)

                holder.Child = value
                holder.Child.Values(1) = 4
                Debug.Print value.Values(1)

                Consume value
                Debug.Print value.Values(1)

                copied = Copy(value)
                copied.Values(1) = 5
                Debug.Print value.Values(1)

                Touch value
                Debug.Print value.Values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "1", "1", "1", "1", "92" }, lines);
    }

    /// <summary>
    /// A user-defined type without array members is plain value storage, so copying it needs no
    /// fixup at all - the copy must not drag in array work that has nothing to copy.
    /// </summary>
    [TestMethod]
    public void Lower_KeepsPlainUdtCopiesAsPlainValueCopies()
    {
        var program = VB6TestIr.Lower("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim source As Point
                Dim copied As Point
                copied = source
            End Sub
            """);

        Assert.IsFalse(VB6TestIr.Expressions(program).OfType<IrCopyArrayExpression>().Any());
    }
}
