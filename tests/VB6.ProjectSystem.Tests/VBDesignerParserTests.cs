using System.Buffers.Binary;
using System.Text;

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
                     BeginProperty Images {2C247F25-8591-11D1-B16A-00C0F0283628}
                        BeginProperty ListImage1 {2C247F27-8591-11D1-B16A-00C0F0283628}
                           Picture = "Main.frx":00000020
                           Key = "Folder"
                        EndProperty
                     EndProperty
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
        Assert.AreEqual(32, button.Properties.Single(property => property.Name == "ListImage1.Picture").ResourceOffset);
        Assert.AreEqual("Folder", button.Properties.Single(property => property.Name == "ListImage1.Key").Value);
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
    public void Parse_StripsInlineCommentsFromDesignerValuesWithoutTouchingQuotedText()
    {
        var result = VBDesignerParser.Parse("""
            VERSION 5.00
            Begin VB.Form Main
               Caption = "Legacy ' form" ' visible comment
               BorderStyle = 0  'Kein
               AutoRedraw = -1  'True
            End
            """, Path.Combine(Path.GetTempPath(), "VB6Designer", "Main.frm"));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var properties = result.Document!.Root.Properties;
        Assert.AreEqual("Legacy ' form", properties.Single(property => property.Name == "Caption").Value);
        Assert.AreEqual(0L, properties.Single(property => property.Name == "BorderStyle").Value);
        Assert.AreEqual(-1L, properties.Single(property => property.Name == "AutoRedraw").Value);
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

    [TestMethod]
    public void Parse_LoadsLengthPrefixedFrxPayloadAndDollarResourceReferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6Designer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var formPath = Path.Combine(directory, "Main.frm");
        var resourcePath = Path.Combine(directory, "Main.frx");
        var payload = Encoding.ASCII.GetBytes("{\\rtf1\\ansi test}");
        var resourceBytes = new byte[12 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(resourceBytes.AsSpan(8, sizeof(uint)), (uint)payload.Length);
        payload.CopyTo(resourceBytes, 12);
        File.WriteAllBytes(resourcePath, resourceBytes);

        try
        {
            var result = VBDesignerParser.Parse("""
                VERSION 5.00
                Begin RichTextLib.RichTextBox Editor
                   TextRTF = $"Main.frx":00000008
                End
                """, formPath);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            var property = result.Document!.Root.Properties.Single();
            Assert.AreEqual(Path.GetFullPath(resourcePath), property.ResourcePath);
            Assert.AreEqual(8, property.ResourceOffset);
            CollectionAssert.AreEqual(payload, property.ResourceData);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void FrxResourceReader_RejectsTruncatedPayload()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "VB6Designer", Guid.NewGuid() + ".frx");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 10);
        File.WriteAllBytes(filePath, bytes);

        try
        {
            Assert.ThrowsException<InvalidDataException>(() => VBFrxResourceReader.Read(filePath, 0));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
