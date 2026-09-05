using VB6.IR;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FixedUdtArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedUdtArrayMembersWithIndependentCopies()
    {
        var compilation = VBCompilation.Create("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 30
            End Sub

            Sub Main()
                Dim first As Record
                Dim copied As Record
                Dim items(1 To 1) As Record

                Debug.Print first.Values(1)
                first.Values(1) = 10
                first.Values(2) = 20

                copied = first
                copied.Values(1) = 99

                Debug.Print first.Values(1)
                Debug.Print copied.Values(1)

                SetValue copied.Values(2)
                Debug.Print first.Values(2)
                Debug.Print copied.Values(2)

                With copied
                    .Values(1) = 7
                    Debug.Print .Values(1)
                End With

                items(1).Values(1) = 40
                copied = items(1)
                copied.Values(1) = 41
                Debug.Print items(1).Values(1)
                Debug.Print copied.Values(1)
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { "0", "10", "99", "20", "30", "7", "40", "41" },
            lines);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesNestedUdtArrayBoundsDefaultsAndByRefWriteBack()
    {
        var output = VB6TestProgram.RunLines("""
            Type Child
                Amount As Long
            End Type

            Type Container
                Entries(2 To 3, -1 To 0) As Child
            End Type

            Sub SetAmount(ByRef amount As Long)
                amount = 42
            End Sub

            Sub Main()
                Dim value As Container

                Debug.Print value.Entries(2, -1).Amount
                Debug.Print LBound(value.Entries, 1)
                Debug.Print UBound(value.Entries, 1)
                Debug.Print LBound(value.Entries, 2)
                Debug.Print UBound(value.Entries, 2)

                SetAmount value.Entries(3, 0).Amount
                Debug.Print value.Entries(3, 0).Amount
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0", "2", "3", "-1", "0", "42" }, output);
    }

    /// <summary>
    /// Every place VB6 copies a user-defined type by value has to produce an independent value:
    /// assignment, an array element, a member of another type, a ByVal argument and a function
    /// result. Writing into the copy after each of them must leave the source untouched.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_CopiesUdtStorageAtEveryValueBoundary()
    {
        var lines = VB6TestProgram.RunLines("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Type Holder
                Child As Record
            End Type

            Sub Consume(ByVal value As Record)
                value.Values(1) = 91
            End Sub

            Sub Touch(ByRef value As Record)
                value.Values(1) = 92
            End Sub

            Function Copy(ByVal value As Record) As Record
                Copy = value
            End Function

            Sub Main()
                Dim value As Record
                Dim copied As Record
                Dim items(1 To 1) As Record
                Dim holder As Holder

                value.Values(1) = 1

                copied = value
                copied.Values(1) = 2
                Debug.Print value.Values(1)

                items(1) = value
                items(1).Values(1) = 3
                Debug.Print value.Values(1)

                holder.Child = value
                holder.Child.Values(1) = 4
                Debug.Print value.Values(1)

                Consume value
                Debug.Print value.Values(1)

                copied = Copy(value)
                copied.Values(1) = 5
                Debug.Print value.Values(1)

                Touch value
                Debug.Print value.Values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "1", "1", "1", "1", "92" }, lines);
    }

    [TestMethod]
    public void EmitManagedApplication_DeepCopiesFixedUdtArrayElements()
    {
        var lines = VB6TestProgram.RunLines("""
            Type Child
                Values(1 To 1) As Long
            End Type

            Type Parent
                Children(1 To 2) As Child
            End Type

            Sub Main()
                Dim source As Parent
                Dim copied As Parent

                source.Children(1).Values(1) = 10
                copied = source
                copied.Children(1).Values(1) = 20

                Debug.Print source.Children(1).Values(1)
                Debug.Print copied.Children(1).Values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "10", "20" }, lines);
    }

    /// <summary>
    /// A user-defined type without array members is plain value storage, so copying it needs no
    /// fixup at all - the copy must not drag in array work that has nothing to copy.
    /// </summary>
    [TestMethod]
    public void Lower_KeepsPlainUdtCopiesAsPlainValueCopies()
    {
        var program = VB6TestIr.Lower("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim source As Point
                Dim copied As Point
                copied = source
            End Sub
            """);

        Assert.IsFalse(VB6TestIr.Expressions(program).OfType<IrCopyArrayExpression>().Any());
    }

    /// <summary>
    /// Ein UDT hat ein festes Layout, seine Arraygrenzen muessen deshalb zur Uebersetzungszeit
    /// feststehen. Bisher faltete der UDT-Binder ausschliesslich Literale und liess jede andere
    /// Form **ohne Diagnose** fallen; das Member bekam dann gar keine Grenzen, das Array wurde nie
    /// angelegt, und der erste Zugriff riss das Programm mit einer NullReferenceException ab.
    /// Benannte Konstanten und konstante Arithmetik sind in VB6 an dieser Stelle erlaubt.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_FoldsConstantUdtArrayBounds()
    {
        var output = VB6TestProgram.RunLines("""
            Const Breite As Long = 3
            Const Start As Long = 2
            Const Ohne = 4

            Type Rec
                literal(1 To 3) As Long
                rechnung(1 To 2 + 1) As Long
                konstante(1 To Breite) As Long
                gemischt(1 To Breite * 2) As Long
                untere(Start To 4) As Long
                zweidim(1 To 2, 1 To 1 + 1) As Long
                spaeter(1 To Spaet) As Long
                ohneTyp(1 To Ohne) As Long
                abgeleitet(1 To Doppelt) As Long
            End Type

            Const Spaet As Long = 5
            Const Doppelt As Long = Spaet + 1

            Sub Main()
                Dim r As Rec

                Debug.Print LBound(r.literal) & "/" & UBound(r.literal)
                Debug.Print UBound(r.rechnung)
                Debug.Print UBound(r.konstante)
                Debug.Print UBound(r.gemischt)
                Debug.Print LBound(r.untere) & "/" & UBound(r.untere)
                Debug.Print UBound(r.zweidim)
                Debug.Print UBound(r.spaeter)
                Debug.Print UBound(r.ohneTyp)
                Debug.Print UBound(r.abgeleitet)

                r.konstante(2) = 7
                Debug.Print r.konstante(2)
            End Sub
            """);

        // "spaeter" belegt, dass eine Konstante auch nach dem Type stehen darf, "abgeleitet",
        // dass eine Konstante sich auf eine spaeter deklarierte beziehen darf: die Konstanten
        // werden vollstaendig gesammelt, bevor ein Member aufgeloest wird.
        CollectionAssert.AreEqual(
            new[] { "1/3", "3", "3", "6", "2/4", "2", "5", "4", "6", "7" },
            output);
    }

    /// <summary>
    /// Was nicht faltet, wird gemeldet statt verworfen. Vorher entstand in beiden Faellen ein
    /// Member ohne Speicher, und der Fehler zeigte sich erst zur Laufzeit als Absturz.
    /// </summary>
    [TestMethod]
    [DataRow("VB6S0071", "a(1 To n) As Long")]
    [DataRow("VB6S0071", "a(1 To 3 / 0) As Long")]
    [DataRow("VB6S0072", "a(5 To 1) As Long")]
    public void Analyze_RejectsUdtArrayBoundsThatAreNotConstant(string code, string member)
    {
        var compilation = VBCompilation.Create(
            string.Join(
                Environment.NewLine,
                "Type Rec",
                "    " + member,
                "End Type",
                string.Empty,
                "Sub Main()",
                "    Dim r As Rec",
                "End Sub"),
            "Module1.bas");

        Assert.IsTrue(
            compilation.Analyze().Diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Expected {code}.");
    }
}
