using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.Runtime;
using VB6.Runtime.WinForms;

namespace VB6.Runtime.WinForms.Tests;

/// <summary>
/// The WinForms half of a generated control, measured without a container.
///
/// The end-to-end proof lives in a real foreign container, because that is the only place where
/// re-parenting and drawing into someone else's device context mean anything. What is worth
/// measuring here is the seam and the geometry: that the companion installs itself under the name
/// the runtime looks for, that the window is created at all, and that a position rectangle is read
/// as edges rather than as a size.
/// </summary>
[STATestClass]
public sealed class ControlPresentationTests
{
    [TestCleanup]
    public void ForgetTheFactory() => VBControlPresentationHost.Reset();

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void TheCompanionInstallsItselfUnderTheNameTheRuntimeLooksFor()
    {
        VBControlPresentationHost.Reset();
        Assert.IsFalse(VBControlPresentationHost.IsAvailable);

        // Der Name ist der Vertrag. Die Runtime darf diese Assembly nicht referenzieren, sucht sie
        // deshalb nach Namen, und ein Umbenennen von Typ oder Methode bricht die Naht, ohne den
        // Build zu brechen -- weshalb beide Namen hier noch einmal ausgeschrieben stehen.
        var installer = typeof(WinFormsControlPresentationFactory);
        Assert.AreEqual("VB6.Runtime.WinForms.WinFormsControlPresentationFactory", installer.FullName);
        Assert.IsNotNull(installer.GetMethod("Install", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));

        WinFormsControlPresentationFactory.Install();
        Assert.IsTrue(VBControlPresentationHost.IsAvailable);
    }

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void AControlWithAHostGetsAWindowAndTakesItsBoundsFromThePositionRectangle()
    {
        WinFormsControlPresentationFactory.Install();
        var previousHost = VBInteraction.Host;
        try
        {
            VBInteraction.Host = null;
            var control = new HostedWidget();

            // Der Designer-Umschlag eines erzeugten Controls steht in seinem Konstruktor, und der
            // Host muss davor stehen. Die Presentation wird aus dem Basiskonstruktor aufgeloest --
            // einen Schritt frueher -- und legt ihn an, wenn keiner da ist.
            Assert.IsInstanceOfType<WinFormsHost>(VBInteraction.Host);
            Assert.IsNotNull(control.Window, "Der Host hat kein Fenster fuer das Control gebaut.");

            var rectangle = Marshal.AllocCoTaskMem(sizeof(int) * 4);
            try
            {
                // Links 10, oben 20, *rechts* 210, *unten* 140. Die beiden hinteren Werte eines
                // RECT sind Kanten. Wer sie als Breite und Hoehe liest, bekommt ein Fenster, das
                // mit seiner Position waechst -- hier waere es 210 x 140 statt 200 x 120.
                Marshal.WriteInt32(rectangle, 0, 10);
                Marshal.WriteInt32(rectangle, 4, 20);
                Marshal.WriteInt32(rectangle, 8, 210);
                Marshal.WriteInt32(rectangle, 12, 140);

                Assert.IsTrue(control.ActivateInPlace(IntPtr.Zero, rectangle));
                Assert.AreEqual(200, control.Window!.Width);
                Assert.AreEqual(120, control.Window.Height);
                Assert.AreEqual(10, control.Window.Left);
                Assert.AreEqual(20, control.Window.Top);
            }
            finally
            {
                Marshal.FreeCoTaskMem(rectangle);
            }
        }
        finally
        {
            (VBInteraction.Host as IDisposable)?.Dispose();
            VBInteraction.Host = previousHost;
        }
    }

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void AnExtentInHiMetricBecomesPixels()
    {
        WinFormsControlPresentationFactory.Install();
        var previousHost = VBInteraction.Host;
        try
        {
            VBInteraction.Host = null;
            var control = new HostedWidget();
            Assert.IsNotNull(control.Window);

            // 2540 HIMETRIC ist genau ein Zoll, also 96 Bildpunkte. Ein Container, dem Twips oder
            // HIMETRIC unumgerechnet durchgereicht werden, legt das Control um Faktoren falsch aus,
            // und niemand bekommt einen Fehler zu sehen.
            control.ResizeTo(new VBOleSize { Width = 2540, Height = 1270 });
            Assert.AreEqual(96, control.Window!.Width);
            Assert.AreEqual(48, control.Window.Height);
        }
        finally
        {
            (VBInteraction.Host as IDisposable)?.Dispose();
            VBInteraction.Host = previousHost;
        }
    }

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void AControlThatTheHostNeverBuiltAWindowForRefusesToActivate()
    {
        WinFormsControlPresentationFactory.Install();
        var previousHost = VBInteraction.Host;
        try
        {
            VBInteraction.Host = null;

            // Kein Load, also kein Fenster. Die Presentation darf hier keines erfinden: Ein
            // Fenster ohne die Kinder des Designers waere eine leere Flaeche, die ein Container
            // fuer das Control haelt.
            var control = new SilentWidget();
            Assert.IsFalse(control.ActivateInPlace(IntPtr.Zero, IntPtr.Zero));
            Assert.AreEqual(IntPtr.Zero, control.WindowHandleForTest);
        }
        finally
        {
            (VBInteraction.Host as IDisposable)?.Dispose();
            VBInteraction.Host = previousHost;
        }
    }

    /// <summary>
    /// A stand-in for what the emitter produces: the control base plus a constructor that runs the
    /// designer envelope. <c>VBInteraction.Load</c> is what an emitted control's envelope reaches
    /// the host through, so the stand-in uses the same call.
    /// </summary>
    [ComVisible(true)]
    [Guid("1D6E0B4E-3C21-4B77-9A0E-7F2C4D5E6A7B")]
    private sealed class HostedWidget : VBComUserControl
    {
        public HostedWidget()
        {
            SetDesignExtentFromTwips(1800, 1200);
            VBInteraction.Load(this);
        }

        public Form? Window => (VBInteraction.Host as WinFormsHost)?.TryGetWindow(this);

        public bool ActivateInPlace(IntPtr parent, IntPtr rectangle) =>
            ((IVBOleObject)this).DoVerb(-4, IntPtr.Zero, IntPtr.Zero, 0, parent, rectangle) == 0;

        public void ResizeTo(VBOleSize extent)
        {
            var size = extent;
            _ = ((IVBOleObject)this).SetExtent(1, ref size);
        }
    }

    /// <summary>The same control, but nothing ever loaded it -- so no window was built for it.</summary>
    [ComVisible(true)]
    [Guid("2E7F1C5F-4D32-4C88-8B1F-8A3D5E6F7B8C")]
    private sealed class SilentWidget : VBComUserControl
    {
        public bool ActivateInPlace(IntPtr parent, IntPtr rectangle) =>
            ((IVBOleObject)this).DoVerb(-4, IntPtr.Zero, IntPtr.Zero, 0, parent, rectangle) == 0;

        public IntPtr WindowHandleForTest
        {
            get
            {
                _ = ((IVBOleWindow)this).GetWindow(out var window);
                return window;
            }
        }
    }
}
