namespace VB6.Runtime;

/// <summary>
/// Runtime facade for VB6's selected <c>Printer</c> object. Typed compiler paths dispatch to the
/// same <see cref="VBInteraction"/> operations; the facade keeps Object/late-bound consumers on
/// the explicit host boundary too.
/// </summary>
public sealed class VBPrinter
{
    public string DeviceName { get => VBInteraction.PrinterGetString(nameof(DeviceName)); set => VBInteraction.PrinterSetString(nameof(DeviceName), value); }
    public string DriverName { get => VBInteraction.PrinterGetString(nameof(DriverName)); set => VBInteraction.PrinterSetString(nameof(DriverName), value); }
    public string Port { get => VBInteraction.PrinterGetString(nameof(Port)); set => VBInteraction.PrinterSetString(nameof(Port), value); }
    public string DocumentName { get => VBInteraction.PrinterGetString(nameof(DocumentName)); set => VBInteraction.PrinterSetString(nameof(DocumentName), value); }
    public string OutputFile { get => VBInteraction.PrinterGetString(nameof(OutputFile)); set => VBInteraction.PrinterSetString(nameof(OutputFile), value); }

    public int ColorMode { get => VBInteraction.PrinterGetLong(nameof(ColorMode)); set => VBInteraction.PrinterSetLong(nameof(ColorMode), value); }
    public int Copies { get => VBInteraction.PrinterGetLong(nameof(Copies)); set => VBInteraction.PrinterSetLong(nameof(Copies), value); }
    public int DrawMode { get => VBInteraction.PrinterGetLong(nameof(DrawMode)); set => VBInteraction.PrinterSetLong(nameof(DrawMode), value); }
    public int DrawStyle { get => VBInteraction.PrinterGetLong(nameof(DrawStyle)); set => VBInteraction.PrinterSetLong(nameof(DrawStyle), value); }
    public int DrawWidth { get => VBInteraction.PrinterGetLong(nameof(DrawWidth)); set => VBInteraction.PrinterSetLong(nameof(DrawWidth), value); }
    public int Duplex { get => VBInteraction.PrinterGetLong(nameof(Duplex)); set => VBInteraction.PrinterSetLong(nameof(Duplex), value); }
    public int FillColor { get => VBInteraction.PrinterGetLong(nameof(FillColor)); set => VBInteraction.PrinterSetLong(nameof(FillColor), value); }
    public int FillStyle { get => VBInteraction.PrinterGetLong(nameof(FillStyle)); set => VBInteraction.PrinterSetLong(nameof(FillStyle), value); }
    public int ForeColor { get => VBInteraction.PrinterGetLong(nameof(ForeColor)); set => VBInteraction.PrinterSetLong(nameof(ForeColor), value); }
    public int hDC => VBInteraction.PrinterGetLong(nameof(hDC));
    public int Height { get => VBInteraction.PrinterGetLong(nameof(Height)); set => VBInteraction.PrinterSetLong(nameof(Height), value); }
    public bool IsDefaultPrinter => VBInteraction.PrinterGetBoolean(nameof(IsDefaultPrinter));
    public int Orientation { get => VBInteraction.PrinterGetLong(nameof(Orientation)); set => VBInteraction.PrinterSetLong(nameof(Orientation), value); }
    public int Page => VBInteraction.PrinterGetLong(nameof(Page));
    public int PaperBin { get => VBInteraction.PrinterGetLong(nameof(PaperBin)); set => VBInteraction.PrinterSetLong(nameof(PaperBin), value); }
    public int PaperSize { get => VBInteraction.PrinterGetLong(nameof(PaperSize)); set => VBInteraction.PrinterSetLong(nameof(PaperSize), value); }
    public int PrintQuality { get => VBInteraction.PrinterGetLong(nameof(PrintQuality)); set => VBInteraction.PrinterSetLong(nameof(PrintQuality), value); }
    public int ScaleMode { get => VBInteraction.PrinterGetLong(nameof(ScaleMode)); set => VBInteraction.PrinterSetLong(nameof(ScaleMode), value); }
    public int Width { get => VBInteraction.PrinterGetLong(nameof(Width)); set => VBInteraction.PrinterSetLong(nameof(Width), value); }
    public int Zoom { get => VBInteraction.PrinterGetLong(nameof(Zoom)); set => VBInteraction.PrinterSetLong(nameof(Zoom), value); }

    public float CurrentX { get => VBInteraction.PrinterGetSingle(nameof(CurrentX)); set => VBInteraction.PrinterSetSingle(nameof(CurrentX), value); }
    public float CurrentY { get => VBInteraction.PrinterGetSingle(nameof(CurrentY)); set => VBInteraction.PrinterSetSingle(nameof(CurrentY), value); }
    public float ScaleHeight { get => VBInteraction.PrinterGetSingle(nameof(ScaleHeight)); set => VBInteraction.PrinterSetSingle(nameof(ScaleHeight), value); }
    public float ScaleLeft { get => VBInteraction.PrinterGetSingle(nameof(ScaleLeft)); set => VBInteraction.PrinterSetSingle(nameof(ScaleLeft), value); }
    public float ScaleTop { get => VBInteraction.PrinterGetSingle(nameof(ScaleTop)); set => VBInteraction.PrinterSetSingle(nameof(ScaleTop), value); }
    public float ScaleWidth { get => VBInteraction.PrinterGetSingle(nameof(ScaleWidth)); set => VBInteraction.PrinterSetSingle(nameof(ScaleWidth), value); }
    public float TwipsPerPixelX => VBInteraction.PrinterGetSingle(nameof(TwipsPerPixelX));
    public float TwipsPerPixelY => VBInteraction.PrinterGetSingle(nameof(TwipsPerPixelY));

    public bool TrackDefault { get => VBInteraction.PrinterGetBoolean(nameof(TrackDefault)); set => VBInteraction.PrinterSetBoolean(nameof(TrackDefault), value); }
    public object? Font { get => VBInteraction.PrinterGetObject(nameof(Font)); set => VBInteraction.PrinterSetObject(nameof(Font), value); }

    public void Print(object? value) => VBInteraction.PrinterPrint(value);

    public void NewPage() => VBInteraction.PrinterNewPage();

    public void EndDoc() => VBInteraction.PrinterEndDoc();

    public void KillDoc() => VBInteraction.PrinterKillDoc();

    public float TextWidth(string text) => VBInteraction.PrinterTextWidth(text);

    public float TextHeight(string text) => VBInteraction.PrinterTextHeight(text);

    public float ScaleX(float value, int fromScale) => VBInteraction.PrinterScaleX(value, fromScale, ScaleMode);

    public float ScaleX(float value, int fromScale, int toScale) => VBInteraction.PrinterScaleX(value, fromScale, toScale);

    public float ScaleY(float value, int fromScale) => VBInteraction.PrinterScaleY(value, fromScale, ScaleMode);

    public float ScaleY(float value, int fromScale, int toScale) => VBInteraction.PrinterScaleY(value, fromScale, toScale);

    public void PaintPicture(object? picture, float x, float y, float width = 0f, float height = 0f) =>
        VBInteraction.PrinterPaintPicture(picture, x, y, width, height);
}
