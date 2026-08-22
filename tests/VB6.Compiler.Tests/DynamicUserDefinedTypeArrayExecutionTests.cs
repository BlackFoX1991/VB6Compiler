namespace VB6.Compiler.Tests;

/// <summary>
/// Dynamic UDT array members are allocated by ReDim against the member receiver and retain their
/// element type through managed generic array storage.
/// </summary>
[TestClass]
public sealed class DynamicUserDefinedTypeArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RedimensionsAndUsesDynamicUdtArrayMember()
    {
        var output = VB6TestProgram.Run("""
            Type Child
                Value As Long
            End Type

            Type Record
                Children() As Child
            End Type

            Sub Main()
                Dim value As Record

                ReDim value.Children(1 To 2)
                value.Children(1).Value = 10
                value.Children(2).Value = 20

                Debug.Print LBound(value.Children)
                Debug.Print UBound(value.Children)
                Debug.Print value.Children(1).Value
                Debug.Print value.Children(2).Value
            End Sub
            """, "test.bas");

        CollectionAssert.AreEqual(new[] { "1", "2", "10", "20" }, VB6TestProgram.SplitLines(output), output);
    }
}
