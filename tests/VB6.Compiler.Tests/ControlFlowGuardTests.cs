namespace VB6.Compiler.Tests;

/// <summary>
/// GoTo, labels and the first label-directed error-handler path are lowered. Resume Next and
/// handler targets use per-statement CIL exception regions.
/// </summary>
[TestClass]
public sealed class ControlFlowGuardTests
{
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
}
