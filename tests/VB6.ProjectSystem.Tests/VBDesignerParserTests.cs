namespace VB6.ProjectSystem.Tests;

[TestClass]
public sealed class VBDesignerParserTests
{
    [TestMethod]
    public void Parse_ReadsNestedControlsPropertiesAndControlArrayMetadata()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "VB6Designer", "Main.frm");
        var result = VBDesignerParser.Parse("""
            VERSION 5.00
            Begin VB.Form Main
               Caption = "Legacy form"
               Begin VB.Frame HostFrame
                  Begin VB.CommandButton Buttons
                     Index = 0
                     Caption = "First"
                     BeginProperty Font
                        Name = "MS Sans Serif"
                        Size = 8.25
                     EndProperty
                     Picture = "Main.frx":00000010
                  End
               End
            End
            Attribute VB_Name = "Main"
            Option Explicit
            """, filePath);

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var document = result.Document;
        Assert.IsNotNull(document);
        Assert.AreEqual("VB.Form", document!.Root.TypeName);
        Assert.AreEqual("Main", document.Root.Name);
        var frame = document.Root.Children.Single();
        var button = frame.Children.Single();
        Assert.AreEqual("Buttons", button.Name);
        Assert.IsTrue(button.IsControlArray);
        Assert.AreEqual(0, button.ArrayIndex);
        Assert.AreEqual("MS Sans Serif", button.Properties.Single(property => property.Name == "Font.Name").Value);
        var resource = button.Properties.Single(property => property.Name == "Picture");
        Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath)!, "Main.frx")), resource.ResourcePath);
        Assert.AreEqual(16, resource.ResourceOffset);
    }

    [TestMethod]
    public void Parse_ReportsUnbalancedDesignerBlocks()
    {
        var result = VBDesignerParser.Parse("""
            VERSION 5.00
            Begin VB.Form Main
               BeginProperty Font
                  Name = "MS Sans Serif"
            End
            """, Path.Combine(Path.GetTempPath(), "Broken.frm"));

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6FRM0006"));
    }

    [TestMethod]
    public void Parse_IgnoresEndStatementsAfterDesignerRoot()
    {
        var result = VBDesignerParser.Parse("""
            VERSION 5.00
            Begin VB.Form Main
            End
            Attribute VB_Name = "Main"
            Private Sub Main()
                End
            End Sub
            """, Path.Combine(Path.GetTempPath(), "VB6Designer", "Main.frm"));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.IsNotNull(result.Document);
    }
}
