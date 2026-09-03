using System.Text.Json;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.Runtime;
using VB6.Runtime.WinForms;

namespace VB6.Runtime.WinForms.Tests;

/// <summary>
/// Keeps the stock-control inventory in the compatibility matrix a measurement rather than a
/// claim. Every control the matrix calls "managed-adapter" has to be creatable by the host without
/// any OCX installed; everything else must not pretend to be.
/// </summary>
[STATestClass]
public sealed class StockControlInventoryTests
{
    private sealed record StockControl(string Name, string File, string Library, string Support);

    [STATestMethod]
    public void Inventory_ListsEveryManagedAdapterTheHostCanActuallyCreate()
    {
        var controls = ReadInventory();
        Assert.IsTrue(controls.Count > 20, "Das Inventar ist auffällig kurz.");

        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var index = 0;
        foreach (var control in controls.Where(entry => entry.Support == "managed-adapter"))
        {
            // Der qualifizierte Name ist die Form, in der ein .frm das Control nennt.
            var typeName = control.Library + "." + control.Name;
            var instance = host.CreateControl(owner, "stock" + index++, typeName);
            Assert.IsNotNull(instance, typeName);

            // Ein Platzhalter zaehlt nicht als Umsetzung. Genau das trennt einen managed Adapter
            // von einem Control, das nur mit registriertem OCX laeuft.
            Assert.AreNotEqual(
                typeof(Panel),
                instance!.GetType(),
                typeName + " liefert nur einen Platzhalter, ist aber als managed-adapter gefuehrt.");
        }
    }

    [STATestMethod]
    public void Inventory_KeepsNativeOnlyControlsVisiblyNonNative()
    {
        var controls = ReadInventory();
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var index = 0;
        foreach (var control in controls.Where(entry => entry.Support != "managed-adapter"))
        {
            // Ohne registriertes OCX bleibt ein Platzhalter -- sichtbar, aber kein Fehler. Ist das
            // OCX vorhanden, entsteht ein echtes Control. Beides ist zulaessig; was nicht zulaessig
            // waere, ist ein harter Abbruch, denn ein Formular soll trotzdem laden.
            var instance = host.CreateControl(owner, "native" + index++, control.Library + "." + control.Name);
            Assert.IsNotNull(instance, control.Name);
        }
    }

    private static IReadOnlyList<StockControl> ReadInventory()
    {
        var matrixPath = FindMatrix();
        using var document = JsonDocument.Parse(File.ReadAllText(matrixPath));
        var controls = document.RootElement
            .GetProperty("activeXStockControls")
            .GetProperty("controls");

        var result = new List<StockControl>();
        foreach (var entry in controls.EnumerateArray())
        {
            result.Add(new StockControl(
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("file").GetString()!,
                entry.GetProperty("library").GetString()!,
                entry.GetProperty("support").GetString()!));
        }

        return result;
    }

    private static string FindMatrix()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "vb6-sp6-compatibility-matrix.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The compatibility matrix was not found above the test output.");
    }
}
