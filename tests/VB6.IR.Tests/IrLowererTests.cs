using VB6.Compiler;

namespace VB6.IR.Tests;

[TestClass]
public sealed class IrLowererTests
{
    [TestMethod]
    public void Lower_IfUsesExplicitBasicBlocks()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Long
                x = 1
                If x = 1 Then
                    Debug.Print 10
                Else
                    Debug.Print 20
                End If
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success);

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsTrue(main.Blocks.Any(block => block.Terminator is IrConditionalTerminator));
        Assert.IsFalse(main.Blocks.SelectMany(block => block.Instructions)
            .Any(instruction => instruction.GetType().Name.Contains("If", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Lower_ByRefExpressionMaterializesTemporaryAddress()
    {
        var analysis = VBCompilation.Create("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Bump 1 + 2
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success);

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsTrue(main.Locals.Any(local => local.IsCompilerGenerated && local.Name.Contains("byref", StringComparison.Ordinal)));
        var call = main.Blocks.SelectMany(block => block.Instructions)
            .OfType<IrEvaluateInstruction>()
            .Select(instruction => instruction.Expression)
            .OfType<IrProcedureCallExpression>()
            .Single();
        Assert.AreEqual(IrCallArgumentKind.Address, call.Arguments.Single().Kind);
        Assert.IsInstanceOfType<IrAddressExpression>(call.Arguments.Single().Expression);
    }

    [TestMethod]
    public void Lower_ForAndExitUseBranchesInsteadOfStructuredLoop()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim i As Long
                For i = 1 To 10
                    If i = 3 Then Exit For
                Next i
            End Sub
            """, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success);

        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
        var main = program.EntryPoint!;

        Assert.IsTrue(main.Blocks.Count(block => block.Terminator is IrGotoTerminator) >= 2);
        Assert.IsTrue(main.Blocks.Any(block => block.Label.Contains("for_exit", StringComparison.Ordinal)));
    }
}
