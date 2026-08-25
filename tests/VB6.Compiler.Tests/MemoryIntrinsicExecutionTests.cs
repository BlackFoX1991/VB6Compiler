using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MemoryIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesNativeWidthObjPtrForNothing()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print ObjPtr(Nothing) = 0
            End Sub
            """);

        Assert.AreEqual("True", output.Trim());
    }

    [TestMethod]
    public void Analyze_ObjPtrReturnsLongPtr()
    {
        var analysis = VBCompilation.Create("""
            Function PointerValue() As LongPtr
                PointerValue = ObjPtr(Nothing)
            End Function
            """, "Memory.bas").Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
        var function = analysis.SemanticModel!.Procedures.Single(
            procedure => procedure.Symbol.Name == "PointerValue");
        Assert.AreSame(TypeSymbol.LongPtr, function.Symbol.ReturnType);
        var assignment = (BoundAssignmentStatement)function.Body.Statements.Single();
        var invocation = assignment.Expression;
        while (invocation is BoundConversionExpression conversion)
        {
            invocation = conversion.Expression;
        }

        var call = (BoundInvocationExpression)invocation;
        Assert.AreSame(TypeSymbol.LongPtr, call.Type);
    }
}
