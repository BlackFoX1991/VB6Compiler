using System.Linq;
using VB6.IR;

namespace VB6.Compiler.Tests;

/// <summary>
/// A designer envelope is emitted as a sequence: create each control, then assign the properties
/// the designer wrote. That sequence has no end marker, and an ActiveX control needs one — VB6
/// hands such a control its persisted state as a whole once every control exists, not property by
/// property. The lowered constructor therefore closes the envelope explicitly.
///
/// A class without designer controls has no envelope and must not carry the call.
/// </summary>
[TestClass]
public sealed class DesignerEnvelopeLoweringTests
{
    [TestMethod]
    public void Lower_ClosesTheDesignerEnvelopeOfAFormThatPlacesControls()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6DesignerEnvelope",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "frmTest.frm"), """
                VERSION 5.00
                Begin VB.Form frmTest
                   Caption         =   "Probe"
                   Begin VB.CommandButton cmdOk
                      Caption         =   "OK"
                   End
                End
                Attribute VB_Name = "frmTest"
                Private Sub Form_Load()
                    Debug.Print cmdOk.Caption
                End Sub
                """);
            var projectPath = Path.Combine(directory, "Probe.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="frmTest"
                Name="Probe"
                Form=frmTest.frm
                """);

            var program = VB6TestIr.LowerProject(projectPath);
            var constructor = VB6TestIr.Procedures(program)
                .Single(procedure => procedure.Name == ".ctor" && procedure.DeclaringClass?.Name == "frmTest");

            var calls = constructor.Blocks
                .SelectMany(block => block.Instructions)
                .OfType<IrEvaluateInstruction>()
                .Select(instruction => instruction.Expression)
                .OfType<IrRuntimeCallExpression>()
                .Select(call => call.Method)
                .ToArray();

            Assert.IsTrue(
                calls.Contains(IrRuntimeMethod.InteractionCompleteDesignerInitialization),
                string.Join(", ", calls));

            // The order is the whole point: the envelope closes after the last designer property,
            // and before any user code can look at a control.
            var lastSet = Array.LastIndexOf(calls, IrRuntimeMethod.InteractionSetMember);
            var completion = Array.IndexOf(calls, IrRuntimeMethod.InteractionCompleteDesignerInitialization);
            Assert.IsTrue(lastSet >= 0, "Die Probe schreibt keine Designer-Eigenschaft.");
            Assert.IsTrue(completion > lastSet, string.Join(", ", calls));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Lower_LeavesAClassWithoutDesignerControlsUntouched()
    {
        var program = VB6TestIr.Lower("""
            Option Explicit

            Sub Main()
                Debug.Print 1
            End Sub
            """);

        Assert.IsFalse(
            VB6TestIr.RuntimeCalls(program).Contains(IrRuntimeMethod.InteractionCompleteDesignerInitialization));
    }
}
