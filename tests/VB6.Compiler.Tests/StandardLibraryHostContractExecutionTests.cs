using System.Globalization;
using VB6.Runtime;

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
    public void EmitManagedApplication_ExecutesRegistryEnumerationAndDeletionContracts()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Dim settings As Variant

                SaveSetting "RegistryCompiler", "General", "Zebra", "last"
                SaveSetting "RegistryCompiler", "General", "Alpha", "first"
                SaveSetting "RegistryCompiler", "Other", "Retained", "yes"
                settings = GetAllSettings("RegistryCompiler", "General")
                Debug.Print settings(0, 0)
                Debug.Print settings(0, 1)
                Debug.Print settings(1, 0)
                Debug.Print settings(1, 1)
                DeleteSetting "RegistryCompiler", "General", "Alpha"
                Debug.Print GetSetting("RegistryCompiler", "General", "Alpha", "missing")
                DeleteSetting "RegistryCompiler", "General"
                Debug.Print IsEmpty(GetAllSettings("RegistryCompiler", "General"))
                DeleteSetting "RegistryCompiler"
                Debug.Print GetSetting("RegistryCompiler", "Other", "Retained", "missing")
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "Alpha", "first", "Zebra", "last", "missing", "True", "missing" },
            lines);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesClipboardFormatAndDataContracts()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Clipboard.Clear
                Clipboard.SetText "plain text"
                Clipboard.SetText "{\rtf1 rich text}", vbCFRTF
                Debug.Print Clipboard.GetFormat(vbCFText)
                Debug.Print Clipboard.GetFormat(vbCFRTF)
                Debug.Print Clipboard.GetText()
                Debug.Print Clipboard.GetText(vbCFRTF)
                Clipboard.SetData "opaque data", 9001
                Debug.Print Clipboard.GetFormat(9001)
                Debug.Print Clipboard.GetData(9001)
                Clipboard.Clear
                Debug.Print Clipboard.GetFormat(vbCFText)
                Debug.Print "[" & Clipboard.GetText() & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "True", "plain text", "{\\rtf1 rich text}", "True", "opaque data", "False", "[]" },
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
    public void Analyze_ResolvesClipboardContract()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim text As String
                text = Clipboard.GetText
                Clipboard.SetText "text", vbCFText
                Clipboard.SetData "opaque", 9001
                Debug.Print Clipboard.GetFormat(vbCFText)
                Debug.Print Clipboard.GetData(9001)
                Clipboard.Clear
            End Sub
            """).Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }

    [TestMethod]
    public void Lower_UsesClipboardRuntimeContracts()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Clipboard.Clear
                Clipboard.SetText "text", vbCFText
                Clipboard.SetData "opaque", 9001
                Debug.Print Clipboard.GetText
                Debug.Print Clipboard.GetFormat(vbCFText)
                Debug.Print Clipboard.GetData(9001)
            End Sub
            """);

        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardGetText);
        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardClear);
        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardSetText);
        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardSetData);
        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardGetFormat);
        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionClipboardGetData);
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
    public void EmitManagedApplication_ErlTracksTheLastNumericLineLabel()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                On Error Resume Next
                GoTo Failure
            100
                Debug.Print "not reached"
            Failure:
            200
                Err.Raise 5, "unit", "message"
                Debug.Print Erl
                Err.Clear
                Debug.Print Erl
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "200", "0" },
            output.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray());
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
    public void Lower_UsesClsHostContract()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Cls
            End Sub
            """);

        CollectionAssert.Contains(
            VB6TestIr.RuntimeCalls(program).ToArray(),
            VB6.IR.IrRuntimeMethod.InteractionCls);
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
