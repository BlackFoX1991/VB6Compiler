using System.Globalization;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class StandardLibraryHostContractExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesIIfRgbAndHeadlessInteractionContracts()
    {
        var lines = VB6TestProgram.RunLines("""
            Function Mark(ByVal Value As Long) As Long
                Debug.Print Value
                Mark = Value
            End Function

            Sub Main()
                Debug.Print IIf(True, Mark(1), Mark(2))
                Debug.Print RGB(1, 2, 3)
                SaveSetting "CompilerTests", "Settings", "Answer", "saved"
                Debug.Print GetSetting("CompilerTests", "Settings", "Answer", "fallback")
                Debug.Print GetSetting("CompilerTests", "Settings", "Missing", "fallback")
                SendKeys "{DOWN}", True
                PopupMenu Empty
                PropertyChanged "Caption"
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "1", "2", "1", "197121", "saved", "fallback" },
            lines);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesEnvironNameAndIndexContracts()
    {
        var name = "VB6COMPILER_ENV_EXEC_" + Guid.NewGuid().ToString("N");
        var previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "compiled");

            var lines = VB6TestProgram.RunLines($"""
                Sub Main()
                    Debug.Print Environ("{name}")
                    Debug.Print Len(Environ(1))
                    Debug.Print Len(Environ("{name}_MISSING"))
                End Sub
                """);

            Assert.AreEqual("compiled", lines[0]);
            Assert.IsTrue(int.Parse(lines[1], CultureInfo.InvariantCulture) > 0);
            Assert.AreEqual("0", lines[2]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesApplicationObjectContract()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Len(App.EXEName)
                Debug.Print Len(App.Path)
                Debug.Print App.hInstance
                Debug.Print App.Major
            End Sub
            """);

        Assert.IsTrue(int.Parse(lines[0], CultureInfo.InvariantCulture) > 0);
        Assert.IsTrue(int.Parse(lines[1], CultureInfo.InvariantCulture) > 0);
        Assert.AreEqual("0", lines[2]);
        Assert.IsTrue(int.Parse(lines[3], CultureInfo.InvariantCulture) >= 0);
    }

    [TestMethod]
    public void Analyze_ResolvesScreenAmbientPictureFontAndPropertyBagContracts()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim pictureValue As StdPicture
                Dim fontValue As Font
                Dim bag As PropertyBag
                Set pictureValue = LoadPicture("")
                Debug.Print pictureValue.Width
                Debug.Print pictureValue.Height
                Debug.Print pictureValue.Type
                Set fontValue = Ambient.Font
                Screen.MousePointer = vbHourglass
                Debug.Print Screen.TwipsPerPixelX
                Debug.Print Screen.TwipsPerPixelY
                Set bag = Nothing
                Call bag.WriteProperty("Caption", "value")
                Debug.Print bag.ReadProperty("Caption", "fallback")
                Debug.Print Command()
                Debug.Print Command$
                Debug.Print StrPtr("value")
                Debug.Print Erl
            End Sub
            """).Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }

    [TestMethod]
    public void EmitManagedApplication_CreatesAndUsesStdFontHostObject()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Dim fontValue As New StdFont
                fontValue.Name = "Compiler Font"
                fontValue.Bold = True
                Debug.Print fontValue.Name
                Debug.Print fontValue.Bold
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Compiler Font", "True" }, lines);
    }

    [TestMethod]
    public void Analyze_ResolvesClipboardGetTextContract()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim text As String
                text = Clipboard.GetText
            End Sub
            """).Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }

    [TestMethod]
    public void Lower_UsesClipboardGetTextRuntimeContract()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Debug.Print Clipboard.GetText
            End Sub
            """);

        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardGetText);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesErlRuntimeContract()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print Erl
            End Sub
            """);

        Assert.AreEqual("0", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_UsesErrSourceRuntimeContract()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error Resume Next
                Err.Raise 5, "unit", "message"
                Debug.Print Err.Source
            End Sub
            """);

        Assert.AreEqual("unit", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_UsesErrHelpAndLastDllErrorRuntimeContracts()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error Resume Next
                Err.Raise 5, "unit", "message", "help.chm", 42
                Debug.Print Err.HelpFile
                Debug.Print Err.HelpContext
                Debug.Print Err.LastDllError
                Err.Clear
                Debug.Print Err.HelpFile
                Debug.Print Err.HelpContext
            End Sub
            """);

        var lines = VB6TestProgram.SplitLines(output);
        CollectionAssert.AreEqual(new[] { "help.chm", "42", "0", "", "0" }, lines);
    }

    [TestMethod]
    public void Lower_UsesGraphicsLineHostContract()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim x As Long
                x = 10
                Line (x, 2)-(x + 3, 4), vbRed, B, F
                Line Step (1, 2)-(3, 4), vbBlue
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { VB6.IR.IrRuntimeMethod.GraphicsLine, VB6.IR.IrRuntimeMethod.GraphicsLine },
            VB6TestIr.RuntimeCalls(program).Where(method => method == VB6.IR.IrRuntimeMethod.GraphicsLine).ToArray());
    }

    [TestMethod]
    public void Lower_UsesTargetGraphicsLineHostContract()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim picture As Control
                picture.Line (0, 0)-(1, 1), vbRed
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { VB6.IR.IrRuntimeMethod.GraphicsLineOnTarget },
            VB6TestIr.RuntimeCalls(program)
                .Where(method => method == VB6.IR.IrRuntimeMethod.GraphicsLineOnTarget)
                .ToArray());
    }

    [TestMethod]
    public void Analyze_ResolvesExternalControlAndComTypeAliases()
    {
        var analysis = VBCompilation.Create("""
            Function ReadNode(ByVal node As MSComctlLib.Node) As VbMsgBoxResult
                Dim editor As RichTextBox
                Dim picture As IPicture
                Dim pointer As MousePointerConstants
                Dim comparison As VbCompareMethod

                Set editor = Nothing
                Set picture = Nothing
                Debug.Print node.Key
                Debug.Print node.Index
                ReadNode = comparison
            End Function
            """).Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }
}
