namespace VB6.Compiler.Tests;

/// <summary>
/// GoTo, labels and the first label-directed error-handler path are lowered. Resume Next and
/// handler targets use per-statement CIL exception regions.
/// </summary>
[TestClass]
public sealed class ControlFlowGuardTests
{
    [TestMethod]
    public void Lower_RepresentsBranchAndLoopEdgesAsExplicitBlocks()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim index As Long
                If index = 0 Then
                    index = 1
                Else
                    index = 2
                End If
                For index = 1 To 2
                    If index = 2 Then Exit For
                Next index
            End Sub
            """);

        var terminators = VB6TestIr.Procedures(program)
            .SelectMany(procedure => procedure.Blocks)
            .Select(block => block.Terminator)
            .ToArray();

        Assert.IsTrue(terminators.OfType<VB6.IR.IrConditionalTerminator>().Count() >= 2);
        Assert.IsTrue(terminators.OfType<VB6.IR.IrGotoTerminator>().Any());
    }

    /// <summary>
    /// GoTo, labels and both supported On Error modes are accepted by semantic analysis.
    /// </summary>
    [TestMethod]
    public void Analyze_AcceptsLabelDirectedErrorHandling()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                On Error GoTo Failed
                Debug.Print 1
                GoTo Done
            Failed:
                Debug.Print 2
            Done:
                On Error GoTo 0
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }

    [TestMethod]
    public void Analyze_AcceptsOnErrorResumeNext()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                On Error Resume Next
                Debug.Print 1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }

    [TestMethod]
    public void Lower_UsesEndProgramHostContract()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                End
            End Sub
            """);

        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.EndProgram);
    }

    [TestMethod]
    public void EmitManagedApplication_EndTerminatesAfterPriorStatements()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print "before"
                End
                Debug.Print "after"
            End Sub
            """);

        Assert.AreEqual("before", output.Trim());
    }

    [TestMethod]
    public void Analyze_AcceptsResumeLabelInAnErrorHandler()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                On Error GoTo Failed
                Debug.Print 1 / 0
                Exit Sub
            Failed:
                Resume Done
            Done:
                Debug.Print "done"
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }

    [TestMethod]
    public void Lower_RepresentsResumeLabelAsAStateClearingResumeOperation()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                On Error GoTo Failed
                Err.Raise 5
            Failed:
                Resume Done
            Done:
            End Sub
            """);

        Assert.IsTrue(
            VB6TestIr.Procedures(program)
                .SelectMany(procedure => procedure.Blocks)
                .SelectMany(block => block.Instructions)
                .OfType<VB6.IR.IrResumeInstruction>()
                .Any(instruction => instruction.Kind == VB6.IR.IrResumeKind.Label));
    }

    [TestMethod]
    public void Lower_EmitsResumeNextRegions()
    {
        var lowering = VBCompilation.Create("""
            Sub Main()
                On Error Resume Next
                Debug.Print 1
            End Sub
            """, "Module1.bas").Lower();

        Assert.IsTrue(lowering.Success, string.Join(Environment.NewLine, lowering.Diagnostics));
        Assert.IsNotNull(lowering.Program);
        Assert.IsTrue(
            lowering.Program.Modules.SelectMany(module => module.Procedures)
                .SelectMany(procedure => procedure.Blocks)
                .SelectMany(block => block.Instructions)
                .Any(instruction => instruction is VB6.IR.IrErrorBoundaryStartInstruction));
    }

    [TestMethod]
    public void EmitManagedApplication_ResumeNextContinuesAfterARecoverableStatementError()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error Resume Next
                Debug.Print 1 / 0
                Debug.Print "continued"
            End Sub
            """);

        Assert.AreEqual("continued", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_JumpsToAnErrorHandler()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error GoTo Failed
                Debug.Print 1 / 0
                Debug.Print "wrong path"
                Exit Sub
            Failed:
                Debug.Print "handled"
            End Sub
            """);

        Assert.AreEqual("handled", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ResumeNextContinuesAtTheFollowingStatement()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error GoTo Failed
                Debug.Print 1 / 0
                Debug.Print "continued"
                Exit Sub
            Failed:
                Resume Next
            End Sub
            """);

        Assert.AreEqual("continued", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ResumeLabelClearsTheHandlerBeforeExecutionContinues()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo Failed
            100
                Err.Raise 5, "unit", "first"
                Exit Sub
            Failed:
                If Err.Number = 5 Then Resume Continue
                Debug.Print Err.Number
                Exit Sub
            Continue:
                Debug.Print Err.Number
                Debug.Print Erl
                Err.Raise 6, "unit", "second"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0", "0", "6" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ResumeLabelWithoutAnActiveErrorRaisesError20()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Resume Done
                Debug.Print Err.Number
            Done:
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "20" }, output);
    }

    /// <summary>
    /// A bare Resume and Resume Next leave the procedure through the resume dispatch switch.
    /// Wrapping them in a per-statement protected region turns that switch into a branch out of
    /// a try block, and the emitted method then fails verification with InvalidProgramException
    /// rather than running. Only Resume &lt;label&gt; may carry the region.
    /// </summary>
    [TestMethod]
    public void Lower_DoesNotWrapABareResumeNextInAProtectedRegion()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                On Error Resume Next
                Resume Next
                Debug.Print 1
            End Sub
            """);

        var starts = VB6TestIr.Procedures(program)
            .SelectMany(procedure => procedure.Blocks)
            .Where(block => block.Instructions
                .OfType<VB6.IR.IrResumeInstruction>()
                .Any(resume => resume.Kind != VB6.IR.IrResumeKind.Label))
            .Select(block => block.Instructions.OfType<VB6.IR.IrErrorBoundaryStartInstruction>().Count())
            .ToArray();

        Assert.AreEqual(1, starts.Length, "genau ein Block traegt das Resume");
        Assert.AreEqual(1, starts[0], $"Regionen um das Resume: {starts[0]}");
    }

    [TestMethod]
    public void EmitManagedApplication_DoesNotReenterAnActiveErrorHandler()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error GoTo Failed
                Inner
                Exit Sub
            Failed:
                Debug.Print Err.Number
            End Sub

            Sub Inner()
                On Error GoTo Failed
                Err.Raise 5, "inner", "first"
                Exit Sub
            Failed:
                Err.Clear
                Err.Raise 6, "inner", "second"
            End Sub
            """);

        Assert.AreEqual("6", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesHandlerAndErlAcrossProcedureCalls()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error GoTo Failed
            100
                Inner
                Exit Sub
            Failed:
                Debug.Print Err.Number
                Debug.Print Erl
            End Sub

            Sub Inner()
                Err.Raise 5, "inner", "failed"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "5", "100" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void Analyze_ReportsIllegalControlFlowWithStableDiagnosticId()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                GoTo Missing
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0061"));
    }
}
