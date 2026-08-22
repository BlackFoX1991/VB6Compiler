namespace VB6.Compiler.Tests;

[TestClass]
public sealed class LikeAndObjectIdentityExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesLikeWithBinaryAndTextCompare()
    {
        var output = VB6TestProgram.Run("""
            Option Compare Binary

            Sub Main()
                Debug.Print "abc" Like "a*"
                Debug.Print "ABC" Like "a*"
                Debug.Print "a5" Like "a#"
                Debug.Print "abc" Like "a[!d]c"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "False", "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesOptionCompareTextAndObjectIdentity()
    {
        var output = VB6TestProgram.Run("""
            Option Compare Text

            Sub Main()
                Dim first As Variant
                Dim second As Variant
                first = CreateObject("Example.Item")
                second = first
                Debug.Print "ABC" Like "a*"
                Debug.Print first Is second
                Debug.Print first Is Nothing
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "False" }, VB6TestProgram.SplitLines(output), output);
    }
}
