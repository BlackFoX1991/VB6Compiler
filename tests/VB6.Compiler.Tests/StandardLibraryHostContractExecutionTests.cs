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
    public void Analyze_ResolvesScreenAmbientPictureFontAndPropertyBagContracts()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim pictureValue As StdPicture
                Dim fontValue As Font
                Dim bag As PropertyBag
                Set pictureValue = LoadPicture("")
                Set fontValue = Ambient.Font
                Screen.MousePointer = vbHourglass
                Set bag = Nothing
                bag.WriteProperty "Caption", "value"
                Debug.Print bag.ReadProperty("Caption", "fallback")
            End Sub
            """).Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }
}
