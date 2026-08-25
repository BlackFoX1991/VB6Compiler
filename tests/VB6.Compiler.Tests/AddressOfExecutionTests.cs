using VB6.IR;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class AddressOfExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_StoresAddressOfAsLongPtr()
    {
        var output = VB6TestProgram.Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Function Callback(ByVal value As Long) As Long
                Callback = value + 1
            End Function

            Public Sub Main()
                Dim callbackAddress As LongPtr
                callbackAddress = AddressOf Callback
                Debug.Print callbackAddress <> 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void Lower_ConvertsAddressOfToLongForLegacyCallbackDeclare()
    {
        var lowering = VBCompilation.Create("""
            Private Declare Function SetWindowLong Lib "user32" Alias "SetWindowLongA" (ByVal hwnd As Long, ByVal index As Long, ByVal callback As Long) As Long

            Private Function Callback(ByVal hwnd As Long, ByVal message As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
                Callback = 0
            End Function

            Sub Main()
                SetWindowLong 0, 0, AddressOf Callback
            End Sub
            """, "Module1.bas").Lower();

        Assert.IsTrue(lowering.Success, string.Join(Environment.NewLine, lowering.Diagnostics));
        var call = lowering.Program!.Modules
            .SelectMany(module => module.Procedures)
            .Single(procedure => procedure.Name == "Main")
            .Blocks
            .SelectMany(block => block.Instructions)
            .SelectMany(instruction => instruction is IrEvaluateInstruction evaluate
                ? new[] { evaluate.Expression }
                : Array.Empty<IrExpression>())
            .OfType<IrProcedureCallExpression>()
            .Single(callExpression => callExpression.Procedure.Name == "SetWindowLong");

        Assert.AreEqual(TypeSymbol.Long, call.Arguments[2].Expression.Type);
        Assert.IsInstanceOfType<IrAddressOfExpression>(call.Arguments[2].Expression);
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesNativeDeclareCallback()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native callback test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function EnumSystemLocalesA Lib "kernel32" Alias "EnumSystemLocalesA" (ByVal callback As LongPtr, ByVal flags As Long) As Long
            Private callbackCount As Long

            Private Function Callback(ByVal localeName As LongPtr) As Long
                callbackCount = callbackCount + 1
                Callback = 1
            End Function

            Sub Main()
                Dim status As Long
                status = EnumSystemLocalesA(AddressOf Callback, 0)
                Debug.Print status <> 0
                Debug.Print callbackCount > 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_MarshalsAnsiStringAndBooleanNativeCallback()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native ANSI callback test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function EnumSystemLocalesA Lib "kernel32" Alias "EnumSystemLocalesA" (ByVal callback As LongPtr, ByVal flags As Long) As Long
            Private callbackCount As Long
            Private callbackNameValid As Boolean

            Private Function Callback(ByVal localeName As String) As Boolean
                callbackCount = callbackCount + 1
                callbackNameValid = Len(localeName) > 0
                Callback = True
            End Function

            Sub Main()
                Dim status As Long
                status = EnumSystemLocalesA(AddressOf Callback, 0)
                Debug.Print status <> 0
                Debug.Print callbackCount > 0
                Debug.Print callbackNameValid
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "True", "True" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesNativeDeclareByRefUdtCallback()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native ByRef callback test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Type RECT
                Left As Long
                Top As Long
                Right As Long
                Bottom As Long
            End Type

            Private Declare Function EnumDisplayMonitors Lib "user32" (ByVal hdc As LongPtr, ByVal clipRect As LongPtr, ByVal callback As LongPtr, ByVal data As LongPtr) As Long
            Private callbackCount As Long
            Private callbackShapeValid As Boolean

            Private Function Callback(ByVal monitor As LongPtr, ByVal hdc As LongPtr, ByRef monitorRect As RECT, ByVal data As LongPtr) As Long
                callbackCount = callbackCount + 1
                callbackShapeValid = monitorRect.Right > monitorRect.Left And monitorRect.Bottom > monitorRect.Top
                Callback = 1
            End Function

            Sub Main()
                Dim status As Long
                status = EnumDisplayMonitors(0, 0, AddressOf Callback, 0)
                Debug.Print status <> 0
                Debug.Print callbackCount > 0
                Debug.Print callbackShapeValid
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }
}
