using VB6.IR;

namespace VB6.Compiler.Tests;

/// <summary>
/// The translation decision behind <see cref="ArrayArgumentConversionExecutionTests"/>: the
/// conversion has to be in the IR. Asserting only on the output would keep passing if the element
/// types happened to line up in a later refactoring, and it was exactly the missing IR node that
/// let the emitter push an array of the wrong instantiation.
/// </summary>
[TestClass]
public sealed class ArrayArgumentConversionLoweringTests
{
    [TestMethod]
    public void Lower_EmitsAnArrayConversionForAMismatchedElementType()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim v(0 To 1) As Variant
                v(0) = -100: v(1) = 60
                Debug.Print IRR(v, 0.1)
            End Sub
            """);

        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            IrRuntimeMethod.ArrayFromObject);
    }

    [TestMethod]
    public void Lower_LeavesAMatchingElementTypeAlone()
    {
        // Ein Double-Array trifft den deklarierten Parametertyp -- hier wäre eine Umwandlung
        // reine Kopierarbeit ohne Wirkung.
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim d(0 To 1) As Double
                d(0) = -100: d(1) = 60
                Debug.Print IRR(d, 0.1)
            End Sub
            """);

        CollectionAssert.DoesNotContain(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            IrRuntimeMethod.ArrayFromObject);
    }
}
