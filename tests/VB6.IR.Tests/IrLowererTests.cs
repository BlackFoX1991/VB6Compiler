using VB6.Compiler;
using VB6.Semantics;

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

    [TestMethod]
    public void Lower_CrossUdtLSetUsesManagedDestinationAddressForScalarLayouts()
    {
        var program = Lower("""
            Type SourceRecord
                Prefix As Byte
                Value As Long
            End Type

            Type TargetRecord
                Value As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim target As TargetRecord
                LSet target = source
            End Sub
            """);

        var lsetCalls = RuntimeCalls(program)
            .Where(call => call.Method == IrRuntimeMethod.MemoryLSet)
            .ToArray();

        Assert.AreEqual(1, lsetCalls.Length);
        var transfer = lsetCalls.Single();
        Assert.AreEqual(TypeSymbol.Error, transfer.ResultType);
        var descriptor = transfer.Arguments[0].Expression;
        Assert.IsInstanceOfType<IrAddressExpression>(
            descriptor);
        Assert.AreEqual(
            IrCallArgumentKind.Address,
            transfer.Arguments[0].Kind);
    }

    [TestMethod]
    public void Lower_CrossUdtLSetKeepsReferenceLayoutsOnGuardedRuntimePath()
    {
        var program = Lower("""
            Type SourceRecord
                Value As String
            End Type

            Type TargetRecord
                Value As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim target As TargetRecord
                LSet target = source
            End Sub
            """);

        var call = RuntimeCalls(program)
            .Single(runtime => runtime.Method == IrRuntimeMethod.MemoryLSet);
        Assert.AreEqual(TypeSymbol.Error, call.ResultType);
        Assert.IsInstanceOfType<IrLoadExpression>(call.Arguments[0].Expression);
        Assert.IsInstanceOfType<IrLoadExpression>(call.Arguments[1].Expression);
    }

    private static IrProgram Lower(string source)
    {
        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
        return IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
    }

    private static IEnumerable<IrRuntimeCallExpression> RuntimeCalls(IrProgram program)
    {
        foreach (var expression in program.EntryPoint!.Blocks
                     .SelectMany(block => block.Instructions)
                     .OfType<IrEvaluateInstruction>()
                     .Select(instruction => instruction.Expression))
        {
            foreach (var call in RuntimeCalls(expression))
            {
                yield return call;
            }
        }
    }

    private static IEnumerable<IrRuntimeCallExpression> RuntimeCalls(IrExpression expression)
    {
        if (expression is IrRuntimeCallExpression call)
        {
            yield return call;
            foreach (var argument in call.Arguments)
            {
                foreach (var nested in RuntimeCalls(argument.Expression))
                {
                    yield return nested;
                }
            }
        }
    }
}
