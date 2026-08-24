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

    [TestMethod]
    public void EmitManagedApplication_ExecutesLSetForFixedLengthStrings()
    {
        var compilation = VBCompilation.Create("""
            Type Strings
                Target As String * 5
                Source As String * 8
            End Type

            Sub Main()
                Dim value As Strings

                value.Source = "ABCDEFGH"
                LSet value.Target = value.Source
                Debug.Print "[" & value.Target & "]"
            End Sub
            """, "Module1.bas");

        var standardOutput = VB6TestProgram.Run(compilation);

        Assert.AreEqual("[ABCDE]", standardOutput.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLSetForSameTypeUdts()
    {
        var compilation = VBCompilation.Create("""
            Type Record
                Name As String * 5
            End Type

            Sub Main()
                Dim source As Record
                Dim target As Record

                source.Name = "Hi"
                LSet target = source
                Debug.Print "[" & target.Name & "]"
            End Sub
            """, "Module1.bas");

        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "[Hi   ]" },
            standardOutput.Trim().Split(Environment.NewLine));
    }
}
