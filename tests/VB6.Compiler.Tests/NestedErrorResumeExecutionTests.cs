namespace VB6.Compiler.Tests;

/// <summary>
/// The contract surface of card <c>l1-02-j</c>: nested handlers, every Resume form, and the
/// Err/Erl state around them.
///
/// The resume dispatch is a switch into the per-statement continuations, which sit outside every
/// protected region. A Resume with nothing to return from therefore cannot raise there - the IL
/// would not verify - so it records the documented error 20 instead and falls through, which is
/// what an enclosing On Error Resume Next observes. Only a procedure without any protected
/// region at all still raises it.
/// </summary>
[TestClass]
public sealed class NestedErrorResumeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ResumeNextWithoutAnActiveErrorRecordsError20AndContinues()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Resume Next
                Debug.Print "after " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "after 20" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_BareResumeWithoutAnActiveErrorRecordsError20AndContinues()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Resume
                Debug.Print "after " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "after 20" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_BareResumeRetriesTheFailingStatement()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim tries As Long
                On Error GoTo Failed
                Err.Raise 5
                Debug.Print "done " & tries
                Exit Sub
            Failed:
                tries = tries + 1
                If tries >= 3 Then Resume Next
                Resume
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "done 3" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LetsAnInnerHandlerShadowTheOuterOne()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "back"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Debug.Print "INNER " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "INNER 5", "back" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PropagatesAnErrorWithoutAnInnerHandlerToTheCaller()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "unreached"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                Err.Raise 9
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "OUTER 9" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RestoresTheOuterHandlerAfterTheInnerCallReturns()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Err.Raise 11
                Debug.Print "unreached"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Debug.Print "INNER " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "INNER 5", "OUTER 11" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_OnErrorGotoZeroDisablesOnlyTheCurrentProcedure()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "unreached"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                On Error GoTo 0
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Debug.Print "INNER"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "OUTER 5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ResumeNextInsideTheInnerProcedureContinuesThere()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "back " & Err.Number
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Debug.Print "inner continues"
                Exit Sub
            InnerFailed:
                Resume Next
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "inner continues", "back 0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RaisingFromInsideAHandlerReachesTheCaller()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Err.Raise 6
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "OUTER 6" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsEveryErrFieldInsideTheHandler()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo Failed
            100
                Err.Raise 13, "srcname", "descr"
                Exit Sub
            Failed:
                Debug.Print Err.Number & "|" & Err.Source & "|" & Err.Description & "|" & Erl
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "13|srcname|descr|100" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ClearsEveryErrFieldOnErrClear()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Err.Raise 13, "srcname", "descr"
                Err.Clear
                Debug.Print Err.Number & "|" & Err.Source & "|" & Err.Description & "|" & Erl
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0|||0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExitSubFromAnActiveHandlerClearsEveryErrField()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Inner
                Debug.Print Err.Number & "|" & Err.Source & "|" & Err.Description & "|" & Erl
            End Sub

            Sub Inner()
                On Error GoTo Failed
            100
                Err.Raise 13, "srcname", "descr"
                Exit Sub
            Failed:
                Exit Sub
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0|||0" }, output);
    }

    [TestMethod]
    public void Lower_MarksOnlyExplicitProcedureExitForActiveHandlerCleanup()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Exit Sub
            End Sub
            """);

        var returns = VB6TestIr.Procedures(program)
            .SelectMany(procedure => procedure.Blocks)
            .Select(block => block.Terminator)
            .OfType<VB6.IR.IrReturnTerminator>()
            .ToArray();

        Assert.AreEqual(1, returns.Count(terminator => terminator.ClearsActiveErrorHandler));
        Assert.IsTrue(returns.Any(terminator => !terminator.ClearsActiveErrorHandler));
    }

    [TestMethod]
    public void EmitManagedApplication_HandlesErrorsRaisedInsideControlFlowConditions()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim a As Variant
                Dim s As String
                Dim n As Long

                On Error Resume Next
                a = Array(1, 2)

                s = "unberuehrt"
                If a(99) = 1 Then s = "dann" Else s = "sonst"
                Debug.Print Err.Number & " " & s

                Err.Clear
                s = "unberuehrt"
                If False Then
                    s = "dann"
                ElseIf a(98) = 1 Then
                    s = "elseif"
                Else
                    s = "sonst"
                End If
                Debug.Print Err.Number & " " & s

                Err.Clear
                Do While a(97) = 1
                    Debug.Print "Rumpf"
                Loop
                Debug.Print Err.Number

                Err.Clear
                While a(96) = 1
                    Debug.Print "Rumpf"
                Wend
                Debug.Print Err.Number

                Err.Clear
                Select Case a(95)
                    Case 1
                        Debug.Print "eins"
                    Case Else
                        Debug.Print "sonst"
                End Select
                Debug.Print Err.Number

                Err.Clear
                For n = 1 To a(94)
                    Debug.Print "Rumpf"
                Next n
                Debug.Print Err.Number

                ' Gegenproben: fehlerfreie Kopfteile laufen unveraendert.
                Err.Clear
                If a(0) = 1 Then n = 10 Else n = 20
                Debug.Print Err.Number & " " & n

                n = 0
                For n = 1 To 3
                Next n
                Debug.Print Err.Number & " " & n
            End Sub
            """);

        // Ein Fehler im Kopf einer Kontrollflussanweisung wurde vorher gar nicht abgefangen: Die
        // Anweisung selbst kann nicht geschuetzt werden, weil ihr Rumpf mehrere Basisbloecke
        // umfasst, aber ihr Kopf laeuft im aktuellen Block. Ohne Schutz beendete die Ausnahme den
        // Prozess. Jetzt vermerkt Resume Next die 9 und setzt hinter der Anweisung fort -- kein
        // Zweig und kein Schleifenrumpf laeuft.
        CollectionAssert.AreEqual(
            new[]
            {
                "9 unberuehrt",
                "9 unberuehrt",
                "9",
                "9",
                "9",
                "9",
                "0 10",
                "0 4"
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_TransfersArrayRecordsUnderAnActiveErrorHandler()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim f As Integer
                Dim typisiert(0 To 1) As Long
                Dim varianten(0 To 1) As Variant
                Dim gelesen(0 To 1) As Long

                On Error Resume Next

                f = FreeFile
                Open "records.dat" For Binary As #f
                typisiert(0) = 7
                typisiert(1) = 9
                Put #f, 1, typisiert
                Debug.Print Err.Number
                Get #f, 1, gelesen
                Debug.Print Err.Number & " " & gelesen(0) & " " & gelesen(1)

                varianten(0) = 1
                varianten(1) = "zwei"
                Put #f, 20, varianten
                Debug.Print Err.Number
                Close #f
                Kill "records.dat"
            End Sub
            """);

        // Ein Put oder Get eines Arrays expandiert in eine Elementschleife und verlaesst damit
        // seinen Basisblock. Weil die Schutzregion vorher schon geoeffnet war, spannte sich die
        // Try-Region ueber die Spruenge und die CLR lehnte die ganze Methode ab -- das Programm
        // startete nicht. Ohne aktiven Handler lief derselbe Code.
        CollectionAssert.AreEqual(new[] { "0", "0 7 9", "0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RoutesAConditionErrorToAnOnErrorGoToHandler()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim a As Variant
                Dim s As String

                On Error GoTo Handler
                a = Array(1, 2)
                s = "vorher"
                If a(99) = 1 Then s = "dann" Else s = "sonst"
                Debug.Print "nicht erreicht"
                Exit Sub

            Handler:
                Debug.Print Err.Number & " " & s
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "9 vorher" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsTheLineNumberOfTheFailingStatementThroughErl()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim i As Long
                On Error Resume Next
            10  Debug.Print "eins"
            20  i = 7
            30  Debug.Print "zwei " & i
                Debug.Print Erl
            40  Err.Raise 5
                Debug.Print Erl & " " & Err.Number
                Err.Clear
                GoTo 60
            50  Debug.Print "nicht erreicht"
            60  Debug.Print "gesprungen"
            End Sub
            """);

        // Erl meldet die Nummer der zuletzt fehlgeschlagenen Anweisung. Die Laufzeitkette dafuer
        // war vollstaendig -- der Lowerer senkt fuer ein Label ErrorSetLineNumber -- aber die
        // Syntax war unerreichbar: beide Labelformen verlangten eine eigene Zeile, wodurch
        // 10 Debug.Print "x" ein Parserfehler war und Erl strukturell 0 blieb. Ohne Fehler
        // bleibt Erl 0, auch wenn nummerierte Anweisungen gelaufen sind.
        CollectionAssert.AreEqual(
            new[] { "eins", "zwei 7", "0", "40 5", "gesprungen" },
            output);
    }
}
