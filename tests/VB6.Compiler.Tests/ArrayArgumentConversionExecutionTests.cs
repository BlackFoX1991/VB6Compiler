namespace VB6.Compiler.Tests;

/// <summary>
/// An array argument whose element type differs from the parameter's needs a real, element-by-
/// element conversion. Without one the emitter pushed a <c>VBArray&lt;double&gt;</c> where a
/// <c>VBArray&lt;object&gt;</c> was declared, which is not a cast the CLR can make.
///
/// The failure was not a wrong number. Between two reference element types the shared generic
/// instantiation hid the mistake entirely, so it looked like it worked; over a value type the
/// callee read the wrong storage and answered with zeros, or took the process down with an
/// internal CLR error. <c>IRR</c> with a <c>Double()</c> array — the very signature VB6 documents
/// — was the measured crash.
/// </summary>
[TestClass]
public sealed class ArrayArgumentConversionExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PassesATypedArrayToTheDocumentedDoubleParameter()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim d(0 To 3) As Double
                d(0) = -100: d(1) = 30: d(2) = 40: d(3) = 50
                Debug.Print Round(IRR(d, 0.1), 6)
                Debug.Print Round(NPV(0.1, d), 4)
                Debug.Print Round(MIRR(d, 0.1, 0.12), 6)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0.088963", "-1.9124", "0.098157" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ConvertsAVariantArrayToTheDoubleParameter()
    {
        // Dieselben Werte über einen anderen Weg: Ein Variant-Array trägt dieselben Zahlen, und
        // die Umwandlung muss sie erhalten -- vorher kamen hier Nullen an, und IRR gab schlicht
        // seine Vermutung zurück.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v(0 To 3) As Variant
                v(0) = -100: v(1) = 30: v(2) = 40: v(3) = 50
                Debug.Print Round(IRR(v, 0.1), 6)
                Debug.Print Round(NPV(0.1, v), 4)

                Dim boxed As Variant
                boxed = Array(-100, 30, 40, 50)
                Debug.Print Round(IRR(boxed, 0.1), 6)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0.088963", "-1.9124", "0.088963" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ConvertsBetweenReferenceElementTypes()
    {
        // Der Fall, der vorher stillschweigend richtig aussah: Join erwartet String(), bekommt
        // Variant(). Er soll weiterhin stimmen -- jetzt aber, weil umgewandelt wird.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim s(0 To 2) As String
                s(0) = "a": s(1) = "b": s(2) = "c"
                Debug.Print Join(s, "-")

                Dim v(0 To 2) As Variant
                v(0) = "a": v(1) = "b": v(2) = "c"
                Debug.Print Join(v, "-")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "a-b-c", "a-b-c" }, output);
    }
}
