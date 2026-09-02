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
                Debug.Print Not (first Is Nothing)
                Debug.Print (first Is Nothing) Or (first Is second)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "False", "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_AssignsNothingAsANullTypedObjectReference()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim value As Object
                Set value = CreateObject("Example.Item")
                Set value = Nothing
                Debug.Print value Is Nothing
            End Sub
            """);

        Assert.AreEqual("True", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_UsesOptionCompareTextForStringRelations()
    {
        var output = VB6TestProgram.Run("""
            Option Compare Text

            Sub Main()
                Debug.Print "ABC" = "abc"
                Debug.Print "ABC" <> "abc"

                Select Case "ABC"
                    Case "abc"
                        Debug.Print "matched"
                    Case Else
                        Debug.Print "missed"
                End Select
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "False", "matched" }, VB6TestProgram.SplitLines(output), output);
    }
}
