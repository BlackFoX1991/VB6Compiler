using VB6.ProjectSystem;
using VB6.Semantics;

namespace VB6.Compiler;

/// <summary>
/// Supplies the stable object contracts needed to analyze common VB6 ActiveX project entries.
/// Native control activation and the complete type-library importer remain backend/host work.
/// </summary>
internal static class VBExternalTypeCatalog
{
    public static IReadOnlyDictionary<string, TypeSymbol> Create(VBProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var aliases = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var component in project.Objects)
        {
            var fileName = Path.GetFileName(component.Metadata.FilePath)?.ToUpperInvariant();
            switch (fileName)
            {
                case "MSCOMCTL.OCX":
                    AddControls(aliases, "MSComctlLib", new[]
                    {
                        "ImageCombo",
                        "ImageList",
                        "ListView",
                        "ProgressBar",
                        "Slider",
                        "StatusBar",
                        "TabStrip",
                        "Toolbar",
                        "TreeView"
                    });
                    aliases["MSComctlLib.Node"] = VBStandardTypes.ExternalTreeNode;
                    break;

                case "RICHTX32.OCX":
                    AddControls(aliases, "RichTextLib", new[] { "RichTextBox" });
                    break;

                case "COMDLG32.OCX":
                    AddControls(aliases, "MSComDlg", new[] { "CommonDialog" });
                    break;

                case "MSWINSCK.OCX":
                    AddControls(aliases, "MSWinsockLib", new[] { "Winsock" });
                    break;

                case "MSINET.OCX":
                    AddControls(aliases, "InetCtlsObjects", new[] { "Inet" });
                    break;

                case "MCI32.OCX":
                    AddControls(aliases, "MCI", new[] { "MMControl" });
                    break;
            }
        }

        return aliases;
    }

    private static void AddControls(
        IDictionary<string, TypeSymbol> aliases,
        string libraryName,
        IEnumerable<string> typeNames)
    {
        foreach (var typeName in typeNames)
        {
            aliases[$"{libraryName}.{typeName}"] = VBStandardTypes.Control;
        }
    }
}
