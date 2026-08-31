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

    /// <summary>
    /// <c>String * n</c> war bisher nur als UDT-Member zugelassen; als lokale, Modul- oder
    /// Klassenvariable war es ein Parserfehler. Jetzt tragen alle vier Deklarationsformen
    /// dieselbe feste Breite: Anfangswert sind n Leerzeichen, ein zu langer Wert wird
    /// abgeschnitten, ein zu kurzer aufgefuellt.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedLengthStringVariables()
    {
        var output = VB6TestProgram.RunLines("""
            Public Modul As String * 4

            Type Record
                Feld As String * 4
            End Type

            Sub Main()
                Dim lokal As String * 4
                Dim satz As Record

                Debug.Print "[" & lokal & "]" & Len(lokal)
                Debug.Print "[" & Modul & "]" & Len(Modul)
                Debug.Print "[" & satz.Feld & "]" & Len(satz.Feld)

                lokal = "ab"
                Modul = "ab"
                Debug.Print "[" & lokal & "]" & Len(lokal)
                Debug.Print "[" & Modul & "]" & Len(Modul)

                lokal = "abcdefg"
                Debug.Print "[" & lokal & "]" & Len(lokal)

                Debug.Print (lokal = "abcd")
                Debug.Print "[" & lokal & "x]"
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "[    ]4", "[    ]4", "[    ]4",
                "[ab  ]4", "[ab  ]4",
                "[abcd]4",
                "True",
                "[abcdx]"
            },
            output);
    }

    /// <summary>
    /// Dieselbe Breite gilt fuer ein Feld einer Klasse - gelesen und geschrieben von aussen wie
    /// von innen - und fuer ein Array solcher Felder.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedLengthStringClassFields()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerFixedStringTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "FixedStrings.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="FixedStrings"
                Class=Bag; Bag.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bag.cls"), """
                Option Explicit

                Public Tag As String * 4
                Public Tags(1 To 2) As String * 4
                Private hidden As String * 4

                Public Function Poke() As String
                    hidden = "z"
                    Poke = "[" & hidden & "]" & Len(hidden)
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim c As Bag
                    Set c = New Bag

                    Debug.Print "[" & c.Tag & "]" & Len(c.Tag)

                    c.Tag = "ab"
                    Debug.Print "[" & c.Tag & "]" & Len(c.Tag)

                    c.Tag = "abcdefg"
                    Debug.Print "[" & c.Tag & "]" & Len(c.Tag)

                    c.Tags(1) = "q"
                    Debug.Print "[" & c.Tags(1) & "]" & Len(c.Tags(1))

                    Debug.Print c.Poke()
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "[    ]4", "[ab  ]4", "[abcd]4", "[q   ]4", "[z   ]4" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Die Laengenpruefung ist dieselbe wie fuer ein UDT-Member, damit beide Deklarationsformen
    /// auf dieselbe Eingabe dieselbe Diagnose melden. Eine benannte Konstante als Laenge bleibt
    /// in beiden Formen ausserhalb der aktuellen Teilmenge.
    /// </summary>
    [TestMethod]
    [DataRow("VB6S0042", "Dim value As Long * 5")]
    [DataRow("VB6S0044", "Dim value As String * 0")]
    [DataRow("VB6S0044", "Dim value As String * 70000")]
    [DataRow("VB6S0043", "Const Breite As Long = 4" + "\n" + "    Dim value As String * Breite")]
    public void Analyze_RejectsInvalidFixedLengthStringVariables(string code, string declaration)
    {
        var compilation = VBCompilation.Create(
            string.Join(
                Environment.NewLine,
                "Sub Main()",
                "    " + declaration.Replace("\n", Environment.NewLine, StringComparison.Ordinal),
                "End Sub"),
            "Module1.bas");

        Assert.IsTrue(
            compilation.Analyze().Diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Expected {code}.");
    }
}
