namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FixedLengthStringUdtExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedLengthStringMembers()
    {
        var compilation = VBCompilation.Create("""
            Type Record
                Name As String * 5
            End Type

            Sub Main()
                Dim value As Record
                Dim copied As Record
                Dim values(1 To 1) As Record

                Debug.Print "[" & value.Name & "]"
                value.Name = "Hi"
                copied = value
                value.Name = "ABCDEFG"
                values(1).Name = "X"

                Debug.Print "[" & value.Name & "]"
                Debug.Print "[" & copied.Name & "]"
                Debug.Print "[" & values(1).Name & "]"
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');
        CollectionAssert.AreEqual(
            new[] { "[     ]", "[ABCDE]", "[Hi   ]", "[X    ]" },
            lines);
    }
}