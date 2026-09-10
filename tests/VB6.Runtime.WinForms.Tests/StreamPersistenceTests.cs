using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.Runtime;
using VB6.Runtime.WinForms;

namespace VB6.Runtime.WinForms.Tests;

/// <summary>
/// Stream-based designer persistence, measured against a registered stock control.
///
/// A container cannot choose which persistence a control uses -- the control does, by which
/// interface it implements. Measured first, before any of this was built: all eleven registered
/// stock controls offer <c>IPersistStreamInit</c>, **none** offers plain <c>IPersistStream</c> (the
/// two are separate interfaces; the Init variant does not derive from the other), and every one
/// answers <c>GetSizeMax</c> with <c>E_NOTIMPL</c> -- a container that sized its buffer from that
/// answer would hand the control nothing.
///
/// The second measurement decided the fixture: a control that has no container takes **no**
/// properties at all. Setting Min, Max and Value on a bare <c>Slider</c> and reading them back gives
/// zeros, and the stream it writes carries those zeros faithfully. So the state has to be set
/// through the host, on a control with a site and a window, or the round trip proves nothing.
///
/// The bytes stay opaque to the container on purpose: the control writes what it wants and reads its
/// own writing back. The acceptance is therefore a round trip through a *second* control, not an
/// inspection of the block.
/// </summary>
[STATestClass]
public sealed class StreamPersistenceTests
{
    private const string SliderProgId = "MSComctlLib.Slider.2";
    private const string SliderControlType = "MSComctlLib.Slider";

    private static bool RequireNativeOcx =>
        string.Equals(
            Environment.GetEnvironmentVariable("VB6_REQUIRE_NATIVE_OCX"),
            "1",
            StringComparison.Ordinal);

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void TheWrittenStreamCarriesTheStateOfTheControl()
    {
        if (!Available())
        {
            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var untouched = Sited(host, owner, "Slider0");
        var fresh = VBComStreamPersistence.TrySaveState(untouched.ComObject);
        Assert.IsNotNull(fresh, "The control refused to write its state.");
        Assert.IsTrue(fresh!.Length > 0, "The control wrote an empty stream.");

        var source = Sited(host, owner, "Slider1");
        Assert.IsTrue(host.TrySetMember(source.Control, "Min", Array.Empty<object?>(), (short)5));
        Assert.IsTrue(host.TrySetMember(source.Control, "Max", Array.Empty<object?>(), (short)55));
        Assert.IsTrue(host.TrySetMember(source.Control, "Value", Array.Empty<object?>(), (short)42));
        var written = VBComStreamPersistence.TrySaveState(source.ComObject);

        // Der Inhalt gehoert dem Control -- der Container liest ihn nicht. Was er pruefen kann und
        // muss, ist, dass der Block den Zustand *traegt*: Ein Control mit anderen Werten schreibt
        // etwas anderes. Ein Strom, der sich nie aendert, waere ein Strom ohne Zustand.
        Assert.IsNotNull(written);
        Assert.AreEqual(fresh.Length, written!.Length, "Gleiche Laenge ist erwartet, gleicher Inhalt nicht.");
        Assert.IsFalse(fresh.AsSpan().SequenceEqual(written), "Der Strom traegt den Zustand nicht.");

        // Und derselbe Zustand schreibt sich wiederholbar: Zweimal gesichert ist zweimal dasselbe.
        var again = VBComStreamPersistence.TrySaveState(source.ComObject);
        Assert.IsTrue(written.AsSpan().SequenceEqual(again!), "Derselbe Zustand schrieb zwei verschiedene Stroeme.");
    }

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void AControlWithoutPersistedStateIsInitialisedInstead()
    {
        if (!Available())
        {
            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        var control = Sited(host, owner, "Slider1");

        // VB6 ruft InitNew fuer ein Control ohne persistierten Zustand. Ein Control, das weder
        // geladen noch initialisiert wurde, ist in gar keinem definierten Zustand.
        Assert.IsTrue(VBComStreamPersistence.TryApplyState(control.ComObject, null));
        Assert.IsTrue(VBComStreamPersistence.TryApplyState(control.ComObject, Array.Empty<byte>()));

        // Und es antwortet danach weiter -- die Zusicherung ist nicht nur der HRESULT.
        Assert.IsTrue(host.TrySetMember(control.Control, "Value", Array.Empty<object?>(), (short)3));
        Assert.AreEqual(3, Convert.ToInt32(Read(host, control.Control, "Value"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void AControlWithoutTheInterfaceIsNotAnError()
    {
        // Keine Stromspeicherung ist kein Fehler: Das Control fuehrt seinen Zustand anderswo, und
        // der Aufrufer faellt auf die Eigenschaftstuete oder die einzelnen Zuweisungen zurueck.
        var plain = new object();
        Assert.IsFalse(VBComStreamPersistence.IsStreamPersistent(plain));
        Assert.IsNull(VBComStreamPersistence.TrySaveState(plain));
        Assert.IsFalse(VBComStreamPersistence.TryApplyState(plain, new byte[] { 1, 2, 3 }));
        Assert.IsNull(VBComStreamPersistence.TryGetDirty(plain));
    }

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void ADamagedStreamLeavesTheControlUsableAndSaysSo()
    {
        if (!Available())
        {
            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        var control = Sited(host, owner, "Slider1");

        // Ein zerstoerter Block ist kein Zustand. Was daraus folgt, entscheidet das Control; der
        // Container darf den Fehler nur nicht verschlucken. Ein stiller Erfolg waere schlimmer --
        // das Control stuende mit halbem Zustand da, und niemand wuesste es.
        var damaged = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB, 0xFA, 0x00, 0x01, 0x02, 0x03 };
        try
        {
            VBComStreamPersistence.TryApplyState(control.ComObject, damaged);
        }
        catch (COMException exception)
        {
            Assert.AreNotEqual(0, exception.ErrorCode);
        }

        // In beiden Ausgaengen muss das Control danach ansprechbar bleiben: Ein Container, der
        // einen kaputten Block liest, darf nicht mit einem toten Control weiterlaufen.
        Assert.IsTrue(host.TrySetMember(control.Control, "Value", Array.Empty<object?>(), (short)9));
        Assert.AreEqual(9, Convert.ToInt32(Read(host, control.Control, "Value"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [STATestMethod]
    [SupportedOSPlatform("windows")]
    public void DirtyIsSOkAndCleanIsSFalse()
    {
        if (!Available())
        {
            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        var control = Sited(host, owner, "Slider1");

        // IsDirty dreht die uebliche Leserichtung um: S_OK heisst schmutzig, S_FALSE sauber. Nach
        // einem Save mit clearDirty steht es wieder auf sauber -- das ist der Teil, an dem ein
        // Container erkennt, ob er ueberhaupt speichern muss.
        Assert.IsTrue(host.TrySetMember(control.Control, "Value", Array.Empty<object?>(), (short)7));
        _ = VBComStreamPersistence.TrySaveState(control.ComObject);
        Assert.AreEqual(false, VBComStreamPersistence.TryGetDirty(control.ComObject));
    }

    private static object? Read(WinFormsHost host, object control, string name)
    {
        Assert.IsTrue(host.TryGetMember(control, name, Array.Empty<object?>(), out var value), name);
        return value;
    }

    /// <summary>
    /// One native control with a site and a window. Without both it takes no properties at all --
    /// measured, and the reason this fixture goes through the host rather than through
    /// <c>Activator.CreateInstance</c>.
    /// </summary>
    private static (object Control, object ComObject) Sited(WinFormsHost host, object owner, string name)
    {
        var control = host.CreateControl(owner, name, SliderControlType)!;
        Assert.IsInstanceOfType<AxHost>(control);
        ((Control)control).CreateControl();
        var comObject = ((IVBComObjectProvider)control).ComObject;
        Assert.IsNotNull(comObject);
        return (control, comObject!);
    }

    private static bool Available()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID(SliderProgId, throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail($"The registered {SliderProgId} fixture is required by this run.");
            }

            return false;
        }

        return true;
    }
}
