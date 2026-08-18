using VB6.Syntax.Text;

namespace VB6.Syntax.Tests;

[TestClass]
public sealed class SourceTextTests
{
    [TestMethod]
    public void From_ParsesMixedLineEndings()
    {
        var text = SourceText.From("first\r\nsecond\nthird\rfourth");

        Assert.AreEqual(4, text.Lines.Length);
        Assert.AreEqual("first", text.ToString(text.Lines[0].Span));
        Assert.AreEqual("second", text.ToString(text.Lines[1].Span));
        Assert.AreEqual("third", text.ToString(text.Lines[2].Span));
        Assert.AreEqual("fourth", text.ToString(text.Lines[3].Span));
    }

    [TestMethod]
    public void From_EmptyText_HasSingleEmptyLine()
    {
        var text = SourceText.From(string.Empty);

        Assert.AreEqual(1, text.Lines.Length);
        Assert.AreEqual(0, text.Lines[0].Length);
    }

    [TestMethod]
    public void TextSpan_FromBounds_CalculatesLength()
    {
        var span = TextSpan.FromBounds(3, 9);

        Assert.AreEqual(3, span.Start);
        Assert.AreEqual(6, span.Length);
        Assert.AreEqual(9, span.End);
    }
}
