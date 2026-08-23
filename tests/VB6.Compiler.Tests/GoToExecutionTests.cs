namespace VB6.Compiler.Tests;

/// <summary>
/// Labels and jumps are lowered to procedure-level IR basic blocks, including targets nested in
/// structured VB6 statements.
/// </summary>
[TestClass]
public sealed class GoToExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_JumpsForwardOverStatements()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim i As Long
                i = 1

                If i = 1 Then
                    GoTo Done
                End If

                Debug.Print 99
            Done:
                Debug.Print i
            End Sub
            """,
            "1");
    }

    [TestMethod]
    public void EmitManagedApplication_JumpsBackwardToFormALoop()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim i As Long
                i = 0
            Again:
                i = i + 1
                Debug.Print i
                If i < 3 Then
                    GoTo Again
                End If
            End Sub
            """,
            "1",
            "2",
            "3");
    }

    [TestMethod]
    public void EmitManagedApplication_ReturnsFromGoSub()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim value As Long
                value = 1
                GoSub AddTwo
                Debug.Print value
                Exit Sub
            AddTwo:
                value = value + 2
                Return
            End Sub
            """,
            "3");
    }

    [TestMethod]
    public void EmitManagedApplication_UsesOnGoToAndNumericLabel()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim selector As Long
                selector = 0
                On selector GoTo First, Second, Third
                Debug.Print 9

                selector = 2
                On selector GoTo First, Second, Third
                Debug.Print 99
                Exit Sub
            First:
                Debug.Print 1
                Exit Sub
            Second:
                Debug.Print 2
                Exit Sub
            Third:
                Debug.Print 3
            End Sub
            """,
            "9",
            "2");
    }

    [TestMethod]
    public void EmitManagedApplication_JumpsToNumericLineLabel()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                GoTo 100
                Debug.Print 99
            100
                Debug.Print 1
            End Sub
            """,
            "1");
    }

    [TestMethod]
    public void EmitManagedApplication_UsesOnGoSubAndReturnsToContinuation()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim selector As Long
                selector = 2
                On selector GoSub First, Second
                Debug.Print 9
                Exit Sub
            First:
                Debug.Print 1
                Return
            Second:
                Debug.Print 2
                Return
            End Sub
            """,
            "2",
            "9");
    }

    [TestMethod]
    public void EmitManagedApplication_JumpsIntoAnIfBlock()
    {
        Run("""
            Sub Main()
                Dim i As Long
                GoTo Inside
                If i = 0 Then
            Inside:
                    Debug.Print 1
                End If
            End Sub
            """,
            "1");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            expectedLines,
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
