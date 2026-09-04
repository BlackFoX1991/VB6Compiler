using System.Runtime.Versioning;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

/// <summary>
/// The container half of the OLE property-bag contract. VB6 does not assign an ActiveX control's
/// persisted properties one by one — it hands the control a bag and lets the control read what it
/// knows. A control that keeps its state in its own blob has no other way to get it back, so a
/// container that only assigns the properties it recognises loses the rest, silently.
///
/// The control half is exercised here by a managed double. That is deliberate: the container is
/// what this repository owns, and it is testable without a registered third-party OCX.
/// </summary>
[TestClass]
public sealed class ComPersistenceRuntimeTests
{
    /// <summary>Reads its properties the way a real control does — by name, with an expected type.</summary>
    [SupportedOSPlatform("windows")]
    private sealed class RecordingControl : IVBPersistPropertyBag
    {
        public bool Initialized { get; private set; }

        public List<string> Requested { get; } = new();

        public string Caption { get; private set; } = string.Empty;

        public int Value { get; private set; }

        public bool Enabled { get; private set; }

        public string Missing { get; private set; } = "unberührt";

        public void GetClassID(out Guid classId) => classId = Guid.Empty;

        public void InitNew() => Initialized = true;

        public void Load(IVBPropertyBag propertyBag, IntPtr errorLog)
        {
            Caption = Read(propertyBag, "Caption", string.Empty) ?? Caption;
            Value = Read(propertyBag, "Value", 0);
            Enabled = Read(propertyBag, "Enabled", false);
            Missing = Read(propertyBag, "NieGeschrieben", string.Empty) ?? Missing;
        }

        public void Save(IVBPropertyBag propertyBag, bool clearDirty, bool saveAllProperties) =>
            throw new NotSupportedException();

        /// <summary>
        /// A control announces the type it wants by putting it into the variant it passes in --
        /// that is what makes the bag typed, and the prototype here stands for it.
        /// </summary>
        private T? Read<T>(IVBPropertyBag propertyBag, string name, T prototype)
        {
            Requested.Add(name);
            object? value = prototype;
            return propertyBag.Read(name, ref value, IntPtr.Zero) == 0 && value is T typed
                ? typed
                : default;
        }
    }

    private sealed class PlainControl
    {
    }

    /// <summary>
    /// A control with a nested collection. It creates the collection itself and passes it into the
    /// read, which is how an ImageList gets its images and a Toolbar its buttons.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private sealed class CollectionControl : IVBPersistPropertyBag
    {
        public EntryCollection Images { get; } = new();

        public void GetClassID(out Guid classId) => classId = Guid.Empty;

        public void InitNew()
        {
        }

        public void Load(IVBPropertyBag propertyBag, IntPtr errorLog)
        {
            object? images = Images;
            propertyBag.Read("Images", ref images, IntPtr.Zero);
        }

        public void Save(IVBPropertyBag propertyBag, bool clearDirty, bool saveAllProperties) =>
            throw new NotSupportedException();
    }

    [SupportedOSPlatform("windows")]
    private sealed class EntryCollection : IVBPersistPropertyBag
    {
        public int Count { get; private set; }

        public List<string> Keys { get; } = new();

        public void GetClassID(out Guid classId) => classId = Guid.Empty;

        public void InitNew()
        {
        }

        public void Load(IVBPropertyBag propertyBag, IntPtr errorLog)
        {
            object? count = 0;
            if (propertyBag.Read("NumListImages", ref count, IntPtr.Zero) != 0)
            {
                return;
            }

            Count = Convert.ToInt32(count, System.Globalization.CultureInfo.InvariantCulture);
            for (var index = 1; index <= Count; index++)
            {
                object? entry = new Entry();
                if (propertyBag.Read("ListImage" + index, ref entry, IntPtr.Zero) == 0 &&
                    entry is Entry loaded)
                {
                    Keys.Add(loaded.Key);
                }
            }
        }

        public void Save(IVBPropertyBag propertyBag, bool clearDirty, bool saveAllProperties) =>
            throw new NotSupportedException();
    }

    [SupportedOSPlatform("windows")]
    private sealed class Entry : IVBPersistPropertyBag
    {
        public string Key { get; private set; } = string.Empty;

        public void GetClassID(out Guid classId) => classId = Guid.Empty;

        public void InitNew()
        {
        }

        public void Load(IVBPropertyBag propertyBag, IntPtr errorLog)
        {
            object? key = string.Empty;
            if (propertyBag.Read("Key", ref key, IntPtr.Zero) == 0)
            {
                Key = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        public void Save(IVBPropertyBag propertyBag, bool clearDirty, bool saveAllProperties) =>
            throw new NotSupportedException();
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_HandsANestedGroupToTheObjectTheControlPassesIn()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        // A BeginProperty group is read as an object, not as a value. The dotted path is how the
        // nesting survives the flat designer envelope; here it becomes sub-objects again.
        var control = new CollectionControl();

        VBComPersistence.TryApplyDesignerState(
            control,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Images.NumListImages"] = 2,
                ["Images.ListImage1.Key"] = "Ordner",
                ["Images.ListImage2.Key"] = "Datei"
            });

        Assert.AreEqual(2, control.Images.Count);
        CollectionAssert.AreEqual(new[] { "Ordner", "Datei" }, control.Images.Keys);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_HandsEveryDesignerValueToTheControl()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        var control = new RecordingControl();

        var applied = VBComPersistence.TryApplyDesignerState(
            control,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Caption"] = "Kopfzeile",
                ["Value"] = 7,
                ["Enabled"] = true
            });

        Assert.IsTrue(applied);
        Assert.IsTrue(control.Initialized, "InitNew comes before Load, as it does in VB6.");
        Assert.AreEqual("Kopfzeile", control.Caption);
        Assert.AreEqual(7, control.Value);
        Assert.IsTrue(control.Enabled);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_ConvertsToTheTypeTheControlAsksFor()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        // The designer envelope of a .frm carries text and whole numbers; the control decides what
        // it wants them to be. Converting to the requested type is the container's job -- it is the
        // reason the bag is typed at all.
        var control = new RecordingControl();

        VBComPersistence.TryApplyDesignerState(
            control,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Caption"] = 42,
                ["Value"] = "13",
                ["Enabled"] = -1
            });

        Assert.AreEqual("42", control.Caption);
        Assert.AreEqual(13, control.Value);
        Assert.IsTrue(control.Enabled);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_LeavesAPropertyTheDesignerNeverWroteAlone()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        // A control asks for everything it might have saved. A name the designer never wrote is not
        // an error; answering it with an empty value would overwrite the control's own default.
        var control = new RecordingControl();

        VBComPersistence.TryApplyDesignerState(
            control,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Caption"] = "nur das"
            });

        Assert.AreEqual("nur das", control.Caption);
        Assert.AreEqual("unberührt", control.Missing);
        CollectionAssert.Contains(control.Requested, "NieGeschrieben");
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_InitializesAControlWithoutAnyDesignerValues()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        // VB6 calls InitNew for a control that has nothing persisted -- without it the control is
        // never told to build its default state at all.
        var control = new RecordingControl();

        var applied = VBComPersistence.TryApplyDesignerState(
            control,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase));

        Assert.IsTrue(applied);
        Assert.IsTrue(control.Initialized);
        Assert.AreEqual(0, control.Requested.Count, "Nothing was persisted, so nothing is loaded.");
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_KeepsReadingAfterAValueThatDoesNotConvert()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        // A designer value the control cannot use is its own decision to make. Failing the read
        // would abort the entire Load and cost every other property with it -- so the value is
        // handed over as it stands and the control decides.
        var control = new RecordingControl();

        VBComPersistence.TryApplyDesignerState(
            control,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Value"] = "keine Zahl",
                ["Caption"] = "danach"
            });

        Assert.AreEqual(0, control.Value, "Der unbrauchbare Wert bleibt beim Vorgabewert.");
        Assert.AreEqual("danach", control.Caption, "Die folgenden Eigenschaften kommen trotzdem an.");
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TryApplyDesignerState_ReportsThatAControlDoesNotUseTheContract()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Property-bag persistence is a Windows contract.");
            return;
        }

        // Not every control persists this way, and that is not a failure -- the caller falls back
        // to the properties it assigned one by one.
        Assert.IsFalse(VBComPersistence.TryApplyDesignerState(
            new PlainControl(),
            new Dictionary<string, object?> { ["Caption"] = "x" }));
    }
}
