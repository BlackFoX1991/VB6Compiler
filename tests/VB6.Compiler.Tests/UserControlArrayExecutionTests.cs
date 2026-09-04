namespace VB6.Compiler.Tests;

/// <summary>
/// A UserControl placed twice in a container with an <c>Index</c> is a control array like any other.
/// The designer envelope keys on the <c>Index</c> property rather than on the kind of control, so
/// the same machinery carries an intrinsic control, a menu and a <c>.ctl</c> alike — which the
/// roadmap doubted and this test settles.
/// </summary>
[TestClass]
public sealed class UserControlArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_BindsAUserControlArrayFromTheDesigner()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6UserControlArray",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Probe.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Probe"
                Form=frmHost.frm
                UserControl=ucTest.ctl
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "ucTest.ctl"), """
                VERSION 5.00
                Begin VB.UserControl ucTest
                   ClientHeight    =   600
                   ClientWidth     =   1200
                End
                Attribute VB_Name = "ucTest"
                Option Explicit

                Public Function Kennung() As String
                    Kennung = "uc"
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "frmHost.frm"), """
                VERSION 5.00
                Begin VB.Form frmHost
                   Caption         =   "Host"
                   Begin Probe.ucTest ucEins
                      Index           =   0
                      Left            =   100
                   End
                   Begin Probe.ucTest ucEins
                      Index           =   1
                      Left            =   200
                   End
                End
                Attribute VB_Name = "frmHost"
                Option Explicit

                Public Function Untere() As Long
                    Untere = LBound(ucEins)
                End Function

                Public Function Obere() As Long
                    Obere = UBound(ucEins)
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim f As frmHost
                    Set f = New frmHost
                    Debug.Print f.Untere
                    Debug.Print f.Obere
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "0", "1" },
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
