using System.Linq;
using VB6.IR;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

/// <summary>
/// A form or control property is resolved through the dynamic dispatch, which returns
/// <c>object</c>, while the bound tree already knows the member is numeric. Leaving the call
/// typed as that numeric type makes the backend read the boxed reference instead of its content:
/// <c>Me.ScaleWidth</c> then answered a plausible but wrong number that changed with every
/// allocation, and no diagnostic ever fired.
///
/// The lowered call therefore stays a Variant and carries an explicit conversion.
/// </summary>
[TestClass]
public sealed class LateBoundNumericMemberLoweringTests
{
    [TestMethod]
    public void Lower_ConvertsANumericLateBoundMemberInsteadOfRetypingTheCall()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6LateBoundNumericTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "frmTest.frm"), """
                VERSION 5.00
                Begin VB.Form frmTest
                   Caption         =   "Probe"
                   ClientHeight    =   5280
                   ClientLeft      =   0
                   ClientTop       =   0
                   ClientWidth     =   8160
                End
                Attribute VB_Name = "frmTest"
                Private Sub Form_Load()
                    Dim value As Long
                    value = Me.ScaleWidth
                    Debug.Print value
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

            var dynamicReads = VB6TestIr.Expressions(program)
                .OfType<IrRuntimeCallExpression>()
                .Where(call => call.Method is IrRuntimeMethod.DynamicGetMember
                    or IrRuntimeMethod.DynamicGetIndexedMember
                    or IrRuntimeMethod.DynamicInvokeMember)
                .ToArray();

            Assert.IsTrue(dynamicReads.Length > 0, "Die Probe erzeugt keinen dynamischen Memberzugriff.");
            foreach (var read in dynamicReads)
            {
                Assert.AreNotEqual(
                    TypeSymbol.Long,
                    read.ResultType,
                    "Ein dynamischer Memberzugriff darf nicht als Zahl typisiert bleiben.");
            }

            Assert.IsTrue(
                VB6TestIr.RuntimeCalls(program).Contains(IrRuntimeMethod.ConvertCLng),
                "Der numerische Zugriff braucht eine ausdrueckliche Konvertierung.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
