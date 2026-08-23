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
}
