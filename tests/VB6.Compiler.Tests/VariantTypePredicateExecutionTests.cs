using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantTypePredicateExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesArrayDateAndObjectPredicates()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim values(0 To 1) As Long
                Debug.Print IsArray(values)
                Debug.Print IsArray(Split("a,b", ","))
                Debug.Print IsArray(Empty)
                Debug.Print IsDate("April 28, 2014")
                Debug.Print IsDate("not a date")
                Debug.Print IsObject(Nothing)
                Debug.Print IsObject(Empty)
                Debug.Print IsObject(Null)
            End Sub
            """);

        Assert.AreEqual(
            "True\nTrue\nFalse\nTrue\nFalse\nTrue\nFalse\nFalse",
            output.Trim().Replace("\r", string.Empty));
    }
}
