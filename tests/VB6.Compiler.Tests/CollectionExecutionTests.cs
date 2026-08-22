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
}
