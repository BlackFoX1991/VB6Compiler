using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Runtime.Tests;

/// <summary>
/// The OLE control surface of a generated UserControl, measured the way a container reaches it:
/// through the CCW, by interface id, not by calling the C# methods directly.
///
/// That distinction is the point of the first case. A managed class can implement all the right
/// interfaces and still hand a container nothing -- <c>IID_IDispatch</c> is exactly that, refused
/// by the CLR and needing a hand-built vtable. So the surface is asked for the same way a
/// container asks, and the answer is what the container would get.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class OleControlSurfaceTests
{
    private const int SOk = 0;
    private const int SFalse = 1;
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int OleEBlank = unchecked((int)0x80040007);
    private const int OleObjSCannotDoVerbNow = 0x00040181;
    private const int DvAspectContent = 1;

    /// <summary>
    /// These cases measure what a control answers **without** a host, so the absence of one has to
    /// be stated rather than assumed. A generated control looks for its presentation companion by
    /// name on first need, and whether that succeeds would otherwise depend on which files happen
    /// to sit in this project's output directory.
    /// </summary>
    [TestInitialize]
    public void SuppressThePresentationCompanion() =>
        VBControlPresentationHost.SuppressForThisProcess();

    [TestCleanup]
    public void ForgetTheSuppression() => VBControlPresentationHost.Reset();

    [TestMethod]
    public void EveryDeclaredControlInterfaceIsHandedOutThroughTheWrapper()
    {
        var control = new Widget();
        var unknown = Marshal.GetIUnknownForObject(control);
        try
        {
            foreach (var (name, iid) in new (string, string)[]
                     {
                         ("IOleObject", "00000112-0000-0000-C000-000000000046"),
                         ("IOleControl", "B196B288-BAB4-101A-B69C-00AA00341D07"),
                         ("IOleWindow", "00000114-0000-0000-C000-000000000046"),
                         ("IOleInPlaceObject", "00000113-0000-0000-C000-000000000046"),
                         ("IOleInPlaceActiveObject", "00000117-0000-0000-C000-000000000046"),
                         ("IViewObject2", "00000127-0000-0000-C000-000000000046"),
                         ("IPersistStreamInit", "7FD52380-4E07-101B-AE2D-08002B2EC713")
                     })
            {
                var id = Guid.Parse(iid);
                var hresult = Marshal.QueryInterface(unknown, in id, out var candidate);
                Assert.AreEqual(SOk, hresult, $"{name} wurde nicht herausgegeben: 0x{hresult:X8}");
                Marshal.Release(candidate);
            }

            // Gegenprobe: Eine Schnittstelle, die dieses Control nicht traegt, wird abgelehnt. Ohne
            // sie beweist die Liste darueber nur, dass QueryInterface immer ja sagt.
            var absent = Guid.Parse("0000010E-0000-0000-C000-000000000046");   // IDataObject
            Assert.AreNotEqual(SOk, Marshal.QueryInterface(unknown, in absent, out _));
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    [TestMethod]
    public void TheContainerLearnsHowThisControlWantsToBeTreated()
    {
        var control = (IVBOleObject)new Widget();

        // Genau die Kombination, die ein VB6-UserControl meldet. SETCLIENTSITEFIRST ist die
        // wichtigste davon: ohne sie laedt der Container den Zustand, bevor das Control seine
        // Site hat, und jede Ambient-Eigenschaft, die es beim Wiederherstellen liest, fehlt.
        Assert.AreEqual(SOk, control.GetMiscStatus(DvAspectContent, out var status));
        Assert.AreEqual(0x00000001, status & 0x00000001, "RECOMPOSEONRESIZE fehlt.");
        Assert.AreEqual(0x00000010, status & 0x00000010, "CANTLINKINSIDE fehlt.");
        Assert.AreEqual(0x00000080, status & 0x00000080, "INSIDEOUT fehlt.");
        Assert.AreEqual(0x00000100, status & 0x00000100, "ACTIVATEWHENVISIBLE fehlt.");
        Assert.AreEqual(0x00020000, status & 0x00020000, "SETCLIENTSITEFIRST fehlt.");
    }

    [TestMethod]
    public void TheControlNamesItselfWithItsVb6NameAndItsOwnClassId()
    {
        var control = (IVBOleObject)new Widget();

        Assert.AreEqual(SOk, control.GetUserClassID(out var classId));
        Assert.AreEqual(typeof(Widget).GUID, classId);

        Assert.AreEqual(SOk, control.GetUserType(1, out var pointer));
        try
        {
            Assert.AreEqual("Widget", Marshal.PtrToStringUni(pointer));
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [TestMethod]
    public void AnExtentTheDesignerNeverGaveIsReportedAsMissingRatherThanInvented()
    {
        var control = (IVBOleObject)new Widget();
        var size = default(VBOleSize);

        // Eine erfundene Groesse waere der schlimmere Ausgang: Der Container legt darum herum aus,
        // und der Fehler taucht als Layoutdefekt an einer ganz anderen Stelle auf.
        Assert.AreEqual(EFail, control.GetExtent(DvAspectContent, ref size));
    }

    [TestMethod]
    public void AResizeFromTheContainerReachesTheControlsOwnHandler()
    {
        var widget = new Widget();
        var control = (IVBOleObject)widget;
        var requested = new VBOleSize { Width = 4233, Height = 2646 };

        Assert.AreEqual(SOk, control.SetExtent(DvAspectContent, ref requested));
        Assert.AreEqual(1, widget.Resizes, "UserControl_Resize wurde nicht gerufen.");

        var read = default(VBOleSize);
        Assert.AreEqual(SOk, control.GetExtent(DvAspectContent, ref read));
        Assert.AreEqual(4233, read.Width);
        Assert.AreEqual(2646, read.Height);

        // Ein anderer Aspekt ist kein Inhalt. Ein Control, das jede Anfrage mit seiner Inhaltsgroesse
        // beantwortet, gibt einem Container fuer das Symbol die Groesse der Flaeche.
        var icon = default(VBOleSize);
        Assert.AreEqual(EInvalidArg, control.GetExtent(4, ref icon));
        Assert.AreEqual(EInvalidArg, control.SetExtent(4, ref requested));

        // Und eine leere Groesse ist keine: sie wuerde die vorhandene ueberschreiben.
        var empty = default(VBOleSize);
        Assert.AreEqual(EInvalidArg, control.SetExtent(DvAspectContent, ref empty));
    }

    [TestMethod]
    public void ANewControlIsInitialisedAndASavedOneIsRestored()
    {
        var fresh = new Widget();
        ((IVBPersistStreamInit)fresh).InitNew();
        Assert.AreEqual(1, fresh.Initialisations, "UserControl_InitProperties wurde nicht gerufen.");
        Assert.AreEqual(0, fresh.Restores);

        var source = new Widget { Caption = "Zaehler", Count = 42, Rate = VBCurrency.FromScaled(12_3400) };
        var stream = new VBMemoryStream();
        ((IVBPersistStreamInit)source).Save(stream, clearDirty: true);
        var block = stream.ToArray();
        Assert.IsTrue(block.Length > 0);
        Assert.AreEqual(1, source.Writes);

        var target = new Widget();
        ((IVBPersistStreamInit)target).Load(new VBMemoryStream(block));
        Assert.AreEqual(1, target.Restores, "UserControl_ReadProperties wurde nicht gerufen.");
        Assert.AreEqual(0, target.Initialisations);

        // Der Wert *und* sein Typ muessen den Rundlauf ueberleben. Eine Tuete, die 42 als String
        // zurueckgibt, macht aus ReadProperty eine Konvertierung, und VB6-Code, der auf den Typ
        // verzweigt, nimmt danach den falschen Zweig.
        Assert.AreEqual("Zaehler", target.Caption);
        Assert.AreEqual(42, target.Count);
        Assert.AreEqual(VBCurrency.FromScaled(12_3400), target.Rate);
        Assert.IsInstanceOfType<long>(target.RawCount);
        Assert.IsInstanceOfType<VBCurrency>(target.RawRate);
    }

    [TestMethod]
    public void AnEmptyStreamMeansANewControlNotARestoredOne()
    {
        var control = new Widget();

        // VB6 entscheidet an genau dieser Stelle: Eine Tuete mit Inhalt ist ein Wiederherstellen,
        // eine leere ein neues Control. ReadProperties mit einer leeren Tuete zu rufen waere ein
        // Wiederherstellen, das nichts wiederherstellt.
        ((IVBPersistStreamInit)control).Load(new VBMemoryStream(Array.Empty<byte>()));
        Assert.AreEqual(1, control.Initialisations);
        Assert.AreEqual(0, control.Restores);
    }

    [TestMethod]
    public void ABlockThisControlDidNotWriteIsRefused()
    {
        var control = new Widget();

        // Ein fremder Block ist kein Zustand. Ihn halb zu lesen liefert ein Control, das teils auf
        // seinen Vorgaben und teils auf Fremddaten steht -- und niemand erfaehrt es.
        var exception = Assert.ThrowsExactly<COMException>(
            () => ((IVBPersistStreamInit)control).Load(
                new VBMemoryStream([0xFF, 0xFE, 0xFD, 0xFC, 0x01, 0x02, 0x03, 0x04])));
        Assert.AreNotEqual(0, exception.ErrorCode);
        Assert.AreEqual(0, control.Restores);
    }

    [TestMethod]
    public void DirtyFollowsTheStateAndSaveClearsIt()
    {
        var control = new Widget();
        var persist = (IVBPersistStreamInit)control;

        Assert.AreEqual(SFalse, persist.IsDirty(), "Ein unveraendertes Control ist sauber.");
        control.MarkDirty();
        Assert.AreEqual(SOk, persist.IsDirty(), "S_OK heisst schmutzig -- die umgekehrte Leserichtung.");

        persist.Save(new VBMemoryStream(), clearDirty: true);
        Assert.AreEqual(SFalse, persist.IsDirty());

        // Und ein Save ohne clearDirty laesst den Zustand schmutzig: Der Container speichert dann
        // in eine Kopie und will danach noch wissen, dass das Original ungesichert ist.
        control.MarkDirty();
        persist.Save(new VBMemoryStream(), clearDirty: false);
        Assert.AreEqual(SOk, persist.IsDirty());
    }

    [TestMethod]
    public void AnAmbientChangeArrivesUnderTheNameTheProgrammerWrote()
    {
        var widget = new Widget();
        var control = (IVBOleControl)widget;

        Assert.AreEqual(SOk, control.OnAmbientPropertyChange(-501));
        CollectionAssert.AreEqual(new[] { "BackColor" }, widget.AmbientChanges);

        widget.AmbientChanges.Clear();
        Assert.AreEqual(SOk, control.OnAmbientPropertyChange(-709));
        CollectionAssert.AreEqual(new[] { "UserMode" }, widget.AmbientChanges);

        // DISPID_UNKNOWN heisst "alle". Es einmal mit leerem Namen zu melden waere einfacher und
        // wuerde jeden Handler brechen, der auf den Namen verzweigt.
        widget.AmbientChanges.Clear();
        Assert.AreEqual(SOk, control.OnAmbientPropertyChange(-1));
        Assert.IsTrue(widget.AmbientChanges.Count > 1);
        CollectionAssert.Contains(widget.AmbientChanges, "UserMode");
        CollectionAssert.Contains(widget.AmbientChanges, "BackColor");

        // Eine unbekannte DISPID ist kein Fehler -- ein Container darf Ambients haben, die dieses
        // Control nicht kennt -- aber sie darf auch keinen Namen erfinden.
        widget.AmbientChanges.Clear();
        Assert.AreEqual(SOk, control.OnAmbientPropertyChange(-999));
        Assert.AreEqual(0, widget.AmbientChanges.Count);
    }

    [TestMethod]
    public void FrozenEventsNestBecauseTheContainersCallsNest()
    {
        var widget = new Widget();
        var control = (IVBOleControl)widget;

        Assert.IsFalse(widget.Frozen);
        control.FreezeEvents(true);
        control.FreezeEvents(true);
        Assert.IsTrue(widget.Frozen);

        // Ein Flag statt eines Zaehlers taut hier auf, waehrend der aeussere Aufrufer noch
        // eingefroren zu sein glaubt.
        control.FreezeEvents(false);
        Assert.IsTrue(widget.Frozen);
        control.FreezeEvents(false);
        Assert.IsFalse(widget.Frozen);

        // Und ein Auftauen zu viel darf nicht unter null laufen und die naechste Sperre schlucken.
        control.FreezeEvents(false);
        control.FreezeEvents(true);
        Assert.IsTrue(widget.Frozen);
    }

    [TestMethod]
    public void TheAdviseListKeepsItsConnectionNumbers()
    {
        var control = (IVBOleObject)new Widget();
        var sink = Marshal.GetIUnknownForObject(new object());
        try
        {
            Assert.AreEqual(SOk, control.Advise(sink, out var first));
            Assert.AreEqual(SOk, control.Advise(sink, out var second));
            Assert.AreNotEqual(first, second, "Zwei Senken bekamen dieselbe Nummer.");

            Assert.AreEqual(SOk, control.Unadvise(first));
            Assert.AreEqual(EInvalidArg, control.Unadvise(first), "Dieselbe Nummer wurde zweimal angenommen.");
            Assert.AreEqual(EInvalidArg, control.Unadvise(4711));
            Assert.AreEqual(SOk, control.Unadvise(second));

            Assert.AreEqual(EInvalidArg, control.Advise(IntPtr.Zero, out _));
        }
        finally
        {
            Marshal.Release(sink);
        }
    }

    [TestMethod]
    public void WithoutAHostTheControlSaysSoInsteadOfPretending()
    {
        var widget = new Widget();

        // Ohne Fenster gibt es kein Fenster. Ein Container, dem gesagt wird, das Control sei
        // aktiviert, legt um ein Fenster herum aus, das nie erscheint.
        Assert.AreEqual(EFail, ((IVBOleWindow)widget).GetWindow(out var window));
        Assert.AreEqual(IntPtr.Zero, window);

        Assert.AreEqual(
            OleObjSCannotDoVerbNow,
            ((IVBOleObject)widget).DoVerb(0, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero));

        Assert.AreEqual(
            OleEBlank,
            ((IVBViewObject2)widget).Draw(
                DvAspectContent, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
    }

    [TestMethod]
    public void WithAHostTheVerbsAndTheDrawingReachIt()
    {
        var widget = new Widget();
        var presentation = new RecordingPresentation();
        widget.AttachPresentation(presentation);

        Assert.AreEqual(SOk, ((IVBOleWindow)widget).GetWindow(out var window));
        Assert.AreEqual(new IntPtr(0x1234), window);

        // Das Primaerverb eines Controls ist die Aktivierung an seinem Platz; -5 ist die ohne
        // Tastaturfokus. Wer die beiden zusammenlegt, gibt einem Control den Fokus, das der
        // Container nur sichtbar machen wollte.
        Assert.AreEqual(SOk, ((IVBOleObject)widget).DoVerb(0, IntPtr.Zero, IntPtr.Zero, 0, new IntPtr(7), IntPtr.Zero));
        Assert.AreEqual(true, presentation.LastUiActive);
        Assert.AreEqual(new IntPtr(7), presentation.LastParent);

        Assert.AreEqual(SOk, ((IVBOleObject)widget).DoVerb(-5, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero));
        Assert.AreEqual(false, presentation.LastUiActive);

        Assert.AreEqual(SOk, ((IVBOleObject)widget).DoVerb(-3, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero));
        Assert.AreEqual(1, presentation.Hides);

        Assert.AreEqual(
            SOk,
            ((IVBViewObject2)widget).Draw(
                DvAspectContent, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                new IntPtr(99), new IntPtr(88), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
        Assert.AreEqual(new IntPtr(99), presentation.LastDeviceContext);

        Assert.AreEqual(SOk, ((IVBOleInPlaceObject)widget).InPlaceDeactivate());
        Assert.AreEqual(1, presentation.Deactivations);
    }

    [TestMethod]
    public void AKeystrokeIsNotClaimedUnlessTheControlWantsIt()
    {
        // S_FALSE, nicht S_OK: Der Container fragt, ob das Control die Taste verbraucht hat. Ein
        // Control, das jede Taste beansprucht, schluckt die Akzeleratoren des Containers.
        Assert.AreEqual(
            SFalse,
            ((IVBOleInPlaceActiveObject)new Widget()).TranslateAccelerator(IntPtr.Zero));
    }

    [TestMethod]
    public void TheClientSiteIsHeldAndHandedBack()
    {
        var control = (IVBOleObject)new Widget();
        var site = Marshal.GetIUnknownForObject(new object());
        try
        {
            Assert.AreEqual(SOk, control.SetClientSite(site));
            Assert.AreEqual(SOk, control.GetClientSite(out var returned));
            try
            {
                Assert.AreEqual(site, returned, "Der Container bekam eine andere Site zurueck.");
            }
            finally
            {
                Marshal.Release(returned);
            }

            // Null ist, wie ein Container sich abmeldet, und muss das Gehaltene freigeben.
            Assert.AreEqual(SOk, control.SetClientSite(IntPtr.Zero));
            Assert.AreEqual(SOk, control.GetClientSite(out var empty));
            Assert.AreEqual(IntPtr.Zero, empty);
        }
        finally
        {
            Marshal.Release(site);
        }
    }

    [TestMethod]
    public void ClosingRunsTheControlsTerminationOnce()
    {
        var widget = new Widget();
        Assert.AreEqual(SOk, ((IVBOleObject)widget).Close(1));
        Assert.AreEqual(1, widget.Terminations);
    }

    /// <summary>
    /// A stand-in for what the compiler emits: the class the OLE surface hangs on, with the
    /// lifecycle handlers a VB6 programmer would have written as private subs. They are private
    /// here for the same reason -- the surface finds them by name, and a public stand-in would
    /// prove less than the emitted code needs.
    /// </summary>
    [ComVisible(true)]
    [Guid("6B0E3B4E-1D2A-4C5F-9E77-2F3A1B4C5D6E")]
    private sealed class Widget : VBComUserControl
    {
        public string? Caption { get; set; }
        public long Count { get; set; }
        public VBCurrency Rate { get; set; }
        public object? RawCount { get; private set; }
        public object? RawRate { get; private set; }
        public int Initialisations { get; private set; }
        public int Restores { get; private set; }
        public int Writes { get; private set; }
        public int Resizes { get; private set; }
        public int Terminations { get; private set; }
        public List<string> AmbientChanges { get; } = new();
        public bool Frozen => EventsAreFrozen;

        private void UserControl_InitProperties() => Initialisations++;

        private void UserControl_ReadProperties(VBPropertyBag bag)
        {
            Restores++;
            Caption = (string?)bag.ReadProperty("Caption", string.Empty);
            RawCount = bag.ReadProperty("Count", 0L);
            RawRate = bag.ReadProperty("Rate", VBCurrency.FromScaled(0));
            Count = (long)RawCount!;
            Rate = (VBCurrency)RawRate!;
        }

        private void UserControl_WriteProperties(VBPropertyBag bag)
        {
            Writes++;
            bag.WriteProperty("Caption", Caption, string.Empty);
            bag.WriteProperty("Count", Count, 0L);
            bag.WriteProperty("Rate", Rate, VBCurrency.FromScaled(0));
        }

        private void UserControl_Resize() => Resizes++;

        private void UserControl_Terminate() => Terminations++;

        private void UserControl_AmbientChanged(string propertyName) =>
            AmbientChanges.Add(propertyName);
    }

    private sealed class RecordingPresentation : IVBControlPresentation
    {
        public IntPtr WindowHandle => new(0x1234);
        public IntPtr LastParent { get; private set; }
        public bool? LastUiActive { get; private set; }
        public IntPtr LastDeviceContext { get; private set; }
        public int Hides { get; private set; }
        public int Deactivations { get; private set; }

        public bool Activate(IntPtr parentWindow, IntPtr positionRectangle, bool uiActive)
        {
            LastParent = parentWindow;
            LastUiActive = uiActive;
            return true;
        }

        public void Hide() => Hides++;

        public void Deactivate() => Deactivations++;

        public void DeactivateUi() { }

        public void SetObjectRects(IntPtr positionRectangle, IntPtr clipRectangle) { }

        public void Resize(VBOleSize extent) { }

        public bool Draw(IntPtr deviceContext, IntPtr bounds)
        {
            LastDeviceContext = deviceContext;
            return true;
        }
    }
}
