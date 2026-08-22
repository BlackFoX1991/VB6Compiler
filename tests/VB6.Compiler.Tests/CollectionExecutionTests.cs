namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CollectionExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesStandardCollectionOperations()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim items As Collection
                Set items = New Collection
                Debug.Print TypeOf items Is Collection

                items.Add "first", "one"
                items.Add "third", "three"
                items.Add "second", "two", 2

                Debug.Print items.Count
                Debug.Print items.Item(1)
                Debug.Print items(2)
                Debug.Print items("two")

                items.Remove "one"
                Debug.Print items.Count
                Debug.Print items(1)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "3", "first", "second", "second", "2", "second" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverStandardCollection()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim items As Collection
                Dim item As Variant
                Set items = New Collection
                items.Add "first"
                items.Add "second"

                For Each item In items
                    Debug.Print item
                    If item = "second" Then
                        Exit For
                    End If
                Next item

                Dim emptyItems As Collection
                Set emptyItems = New Collection
                For Each item In emptyItems
                    Debug.Print "unexpected"
                Next item
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "first", "second" }, output);
    }
}
