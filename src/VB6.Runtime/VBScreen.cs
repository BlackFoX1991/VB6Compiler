namespace VB6.Runtime;

/// <summary>
/// Runtime facade for VB6's process-wide <c>Screen</c> object. The compiler recognizes its
/// declared members directly; this facade also preserves the object contract for late-bound and
/// object-typed consumers.
/// </summary>
public sealed class VBScreen
{
    public object? ActiveForm => VBInteraction.ScreenActiveForm();

    public object? ActiveControl => VBInteraction.ScreenActiveControl();

    public float TwipsPerPixelX => VBInteraction.ScreenTwipsPerPixelX();

    public float TwipsPerPixelY => VBInteraction.ScreenTwipsPerPixelY();

    public int MousePointer
    {
        get => VBInteraction.ScreenMousePointer();
        set => VBInteraction.ScreenSetMousePointer(value);
    }
}
