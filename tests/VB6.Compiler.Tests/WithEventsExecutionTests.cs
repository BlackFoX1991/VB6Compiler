namespace VB6.Compiler.Tests;

/// <summary>
/// <c>WithEvents</c> plus <c>RaiseEvent</c> — the VB6 way one object listens to another. The parser
/// accepted the declaration and the binder resolved the handler, but nothing measured that an event
/// actually arrives, or that assigning a new source rewires the handler and <c>Nothing</c>
/// disconnects it.
/// </summary>
[TestClass]
public sealed class WithEventsExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_DeliversRaisedEventsToAWithEventsSink()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6WithEvents",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Ereignisse.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Ereignisse"
                Class=Quelle; Quelle.cls
                Class=Senke; Senke.cls
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Quelle.cls"), """
                VERSION 1.0 CLASS
                Attribute VB_Name = "Quelle"
                Option Explicit

                Public Event Fertig(ByVal wert As Long)

                Public Sub Ausloesen(ByVal wert As Long)
                    RaiseEvent Fertig(wert)
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Senke.cls"), """
                VERSION 1.0 CLASS
                Attribute VB_Name = "Senke"
                Option Explicit

                Private WithEvents m_quelle As Quelle
                Public Protokoll As String

                Public Sub Verbinde(ByVal q As Quelle)
                    Set m_quelle = q
                End Sub

                Private Sub m_quelle_Fertig(ByVal wert As Long)
                    Protokoll = Protokoll & CStr(wert) & " "
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim q As Quelle
                    Set q = New Quelle
                    Dim s As Senke
                    Set s = New Senke

                    s.Verbinde q
                    q.Ausloesen 7
                    q.Ausloesen 9
                    Debug.Print Trim$(s.Protokoll)

                    ' Set auf Nothing trennt die Verbindung -- danach erreicht nichts mehr den Sink.
                    s.Verbinde Nothing
                    q.Ausloesen 11
                    Debug.Print Trim$(s.Protokoll)

                    ' Und eine neue Quelle wird neu verdrahtet.
                    Dim r As Quelle
                    Set r = New Quelle
                    s.Verbinde r
                    r.Ausloesen 13
                    Debug.Print Trim$(s.Protokoll)
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "7 9", "7 9", "7 9 13" },
                VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
