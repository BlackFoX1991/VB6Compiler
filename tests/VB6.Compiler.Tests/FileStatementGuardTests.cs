using VB6.IR;

namespace VB6.Compiler.Tests;

/// <summary>
/// Binding returns null for statements it does not understand, so a file statement that is neither
/// lowered nor reported would vanish from the generated program without a word - a wrong program
/// rather than a reported gap. These tests pin both directions: what is supported reaches the
/// output, and what is not is named.
/// </summary>
[TestClass]
public sealed class FileStatementGuardTests
{
    [TestMethod]
    public void Lower_LowersEverySupportedFileStatement()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim buffer As Long
                Open "data.bin" For Binary As #1
                Put #1, 1, buffer
                Seek #1, 1
                Get #1, 1, buffer
                Close #1
            End Sub
            """);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                IrRuntimeMethod.FileOpenBinary,
                IrRuntimeMethod.FilePut,
                IrRuntimeMethod.FileSeek,
                IrRuntimeMethod.FileGetLong,
                IrRuntimeMethod.FileClose
            },
            VB6TestIr.RuntimeCalls(program).ToArray());
    }

    [TestMethod]
    public void Lower_LowersTextOpenModesAndFilePrint()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim count As Long
                Dim line As String
                Dim first As String
                Dim second As String
                Open "text.txt" For Output As #1
                Print #1, "hello"
                Close #1
                Open "text.txt" For Append As #1
                Print #1, "world"
                Close #1
                Open "text.txt" For Input As #1
                Line Input #1, line
                Input #1, first, second, count
                Close #1
            End Sub
            """);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                IrRuntimeMethod.FileOpenOutput,
                IrRuntimeMethod.FileOpenAppend,
                IrRuntimeMethod.FileOpenInput,
                IrRuntimeMethod.FilePrint,
                IrRuntimeMethod.FileLineInput,
                IrRuntimeMethod.FileInputField
            },
            VB6TestIr.RuntimeCalls(program).ToArray());
    }

    [TestMethod]
    public void Lower_ClosesEveryFileForABareClose()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Close
            End Sub
            """);

        CollectionAssert.Contains(VB6TestIr.RuntimeCalls(program).ToArray(), IrRuntimeMethod.FileCloseAll);
    }

    [TestMethod]
    public void Lower_StopsRatherThanEmittingAProgramMissingAnUnsupportedTransfer()
    {
        var lowering = VBCompilation.Create("""
            Type Record
                Value As Variant
            End Type

            Sub Main()
                Dim record As Record
                Open "a.bin" For Binary As #1
                Put #1, 1, record
                Close #1
            End Sub
            """, "Module1.bas").Lower();

        Assert.IsFalse(lowering.Success);
        Assert.IsNull(lowering.Program);
        Assert.IsTrue(lowering.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0058"));
    }
}
