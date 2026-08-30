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
    public void EmitManagedApplication_ExecutesRSetForFixedLengthStrings()
    {
        var output = VB6TestProgram.RunLines("""
            Type Strings
                Target As String * 5
            End Type

            Sub Main()
                Dim value As Strings
                Dim source As String
                Dim variable As String

                source = "Hi"
                RSet value.Target = source
                Debug.Print "[" & value.Target & "]"

                source = "ABCDEFGH"
                RSet value.Target = source
                Debug.Print "[" & value.Target & "]"

                RSet variable = source
                Debug.Print "[" & variable & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "[   Hi]", "[ABCDE]", "[ABCDEFGH]" }, output);
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

    [TestMethod]
    public void EmitManagedApplication_ExecutesLSetAcrossSupportedUdtLayouts()
    {
        var output = VB6TestProgram.RunLines("""
            Type SourceRecord
                Prefix As Byte
                Value As Long
            End Type

            Type NarrowRecord
                Value As Long
            End Type

            Type WideRecord
                Value As Long
                Tail As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim narrow As NarrowRecord
                Dim wide As WideRecord

                source.Prefix = 7
                source.Value = 42
                LSet narrow = source
                Debug.Print narrow.Value

                wide.Value = 11
                wide.Tail = 99
                LSet wide = narrow
                Debug.Print wide.Value
                Debug.Print wide.Tail
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "7", "7", "0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLSetAcrossBooleanUdtLayouts()
    {
        var output = VB6TestProgram.RunLines("""
            Type SourceRecord
                Enabled As Boolean
                Value As Long
            End Type

            Type TargetRecord
                Enabled As Boolean
                Result As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim target As TargetRecord

                source.Enabled = True
                source.Value = 42
                LSet target = source

                Debug.Print target.Enabled
                Debug.Print target.Result
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "42" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLSetAcrossLongPtrUdtLayouts()
    {
        var output = VB6TestProgram.RunLines("""
            Type SourceRecord
                Address As LongPtr
                Value As Long
            End Type

            Type TargetRecord
                Address As LongPtr
                Result As Long
            End Type

            Sub Main()
                Dim source As SourceRecord
                Dim target As TargetRecord

                source.Address = CLngPtr(42)
                source.Value = 99
                LSet target = source

                Debug.Print target.Address
                Debug.Print target.Result
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "42", "99" }, output);
    }
}
