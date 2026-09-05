using VB6.IR;

namespace VB6.Compiler.Tests;

/// <summary>
/// The prologue store that gives an undeclared local its VB6 default.
///
/// The executing test in <see cref="ImplicitTypeDefaultExecutionTests"/> proves the observable
/// result. This one pins the translation decision: the empty string is written in the procedure
/// prologue, before user control flow, rather than being patched in at each use. A fix that
/// happened to satisfy <c>VarType</c> some other way would pass there and fail here.
/// </summary>
[TestClass]
public sealed class ImplicitLocalPrologueIrTests
{
    [TestMethod]
    public void Lower_InitializesAnUndeclaredStringLocalInTheEntryBlock()
    {
        var program = VB6TestIr.Lower("""
            DefStr G

            Public Sub Main()
                Debug.Print gg
            End Sub
            """);

        var entry = VB6TestIr.Procedures(program)
            .Single(procedure => procedure.Name.Equals("Main", StringComparison.OrdinalIgnoreCase))
            .Blocks[0];

        var initializers = entry.Instructions
            .OfType<IrStoreInstruction>()
            .Where(store => store.Target is IrLocalPlace)
            .Where(store => store.Value is IrConstantExpression { Value: "" })
            .ToArray();

        Assert.AreEqual(
            1,
            initializers.Length,
            "Die implizite String-Variable bekommt genau einen Prolog-Store mit dem leeren String.");
    }

    [TestMethod]
    public void Lower_DoesNotInitializeAnUndeclaredNumericLocal()
    {
        // The CLR already zeroes numeric locals, so an extra store would be noise in every
        // procedure that touches an undeclared number -- which, without Option Explicit, is many.
        var program = VB6TestIr.Lower("""
            DefInt G

            Public Sub Main()
                Debug.Print gg
            End Sub
            """);

        var entry = VB6TestIr.Procedures(program)
            .Single(procedure => procedure.Name.Equals("Main", StringComparison.OrdinalIgnoreCase))
            .Blocks[0];

        Assert.AreEqual(
            0,
            entry.Instructions.OfType<IrStoreInstruction>().Count(store => store.Target is IrLocalPlace),
            "Ein numerisches Local braucht keinen eigenen Prolog-Store.");
    }
}
