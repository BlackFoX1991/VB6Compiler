namespace VB6.Compiler.Tests;

/// <summary>
/// <c>VarPtr</c> and <c>StrPtr</c> hand out the address of managed storage. That address is valid
/// only while the storage is held in place, and a returned pointer does not survive that — the
/// collector may move the cell right after. Supported is therefore exactly the position where VB6
/// passes the pointer straight on: a <c>ByVal … As Any</c> argument of a Declare, which the
/// lowerer turns into an address. Everywhere else the compiler says so instead of handing back a
/// number that points somewhere else after the next collection.
/// </summary>
[TestClass]
public sealed class PointerIntrinsicTests
{
    [TestMethod]
    public void EmitManagedApplication_ReportsWhyVarPtrCannotAnswer()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim zahl As Long
                Dim zeiger As Long
                zahl = 7
                zeiger = VarPtr(zahl)
                Debug.Print Err.Number
                Debug.Print Err.Description
                Err.Clear
                Dim text As String
                text = "abc"
                zeiger = StrPtr(text)
                Debug.Print Err.Number
            End Sub
            """);

        // Die Nummer bleibt VB6s 5 für einen ungültigen Aufruf -- die Beschreibung sagt jetzt,
        // warum, statt den Sammelwert unerklärt zu lassen.
        Assert.AreEqual("5", output[0]);
        StringAssert.Contains(output[1], "ByVal As Any");
        Assert.AreEqual("5", output[2]);
    }

    [TestMethod]
    public void EmitManagedApplication_AnswersObjPtrForAnObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("ObjPtr is the COM identity pointer and needs Windows.");
            return;
        }

        // ObjPtr ist anders gelagert: Die COM-Identität eines Objekts ist stabil und braucht keine
        // festgehaltene Speicherzelle.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim o As Object
                Set o = New Collection
                Debug.Print (ObjPtr(o) <> 0)
                Debug.Print (ObjPtr(Nothing) = 0)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, output);
    }
}
