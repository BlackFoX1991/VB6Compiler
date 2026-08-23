using System.Collections.Immutable;
using VB6.ProjectSystem;
using VB6.Semantics;

namespace VB6.Compiler;

/// <summary>
/// Supplies the stable object contracts needed to analyze common VB6 ActiveX project entries.
/// Native control activation and the complete type-library importer remain backend/host work.
/// </summary>
internal static class VBExternalTypeCatalog
{
    public static VBExternalTypeCatalogResult Create(VBProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var aliases = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        var qualifiedEnumMembers = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var constants = new Dictionary<string, BoundModuleVariable>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in project.References.Where(reference =>
                     reference.Metadata.Kind == VBProjectReferenceKind.TypeLibrary))
        {
            var path = reference.Metadata.GetFullPath(project.ProjectDirectory);
            if (path is null)
            {
                continue;
            }

            MergeImportedTypeLibrary(
                aliases,
                qualifiedEnumMembers,
                constants,
                VBTypeLibraryImporter.Import(path, reference.Metadata.DisplayName, controlLibrary: false));
        }

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
                    aliases["MSComctlLib.TreeView"] = VBStandardTypes.ExternalTreeView;
                    aliases["MSComctlLib.ImageList"] = VBStandardTypes.ExternalImageList;
                    aliases["MSComctlLib.ListImages"] = VBStandardTypes.ExternalListImages;
                    aliases["MSComctlLib.ListImage"] = VBStandardTypes.ExternalListImage;
                    aliases["MSComctlLib.ImageCombo"] = VBStandardTypes.ExternalImageCombo;
                    aliases["MSComctlLib.ComboItems"] = VBStandardTypes.ExternalComboItems;
                    aliases["MSComctlLib.ComboItem"] = VBStandardTypes.ExternalComboItem;
                    break;

                case "RICHTX32.OCX":
                    aliases["RichTextLib.RichTextBox"] = VBStandardTypes.ExternalRichTextBox;
                    break;

                case "COMDLG32.OCX":
                    aliases["MSComDlg.CommonDialog"] = VBStandardTypes.ExternalCommonDialog;
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

            var path = component.Metadata.GetFullPath(project.ProjectDirectory);
            if (path is not null)
            {
                MergeImportedTypeLibrary(
                    aliases,
                    qualifiedEnumMembers,
                    constants,
                    VBTypeLibraryImporter.Import(path, component.Metadata.DisplayName, controlLibrary: true));
            }
        }

        return new VBExternalTypeCatalogResult(
            aliases,
            qualifiedEnumMembers,
            constants.Values.ToImmutableArray());
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

    private static void MergeImportedTypeLibrary(
        IDictionary<string, TypeSymbol> aliases,
        IDictionary<string, long> qualifiedEnumMembers,
        IDictionary<string, BoundModuleVariable> constants,
        VBTypeLibraryImportResult imported)
    {
        foreach (var entry in imported.Aliases)
        {
            // Explicit contracts above are more precise for the common VB6 controls than the
            // generic automation signatures exposed by an installed OCX.
            aliases.TryAdd(entry.Key, entry.Value);
        }

        foreach (var entry in imported.QualifiedEnumMembers)
        {
            qualifiedEnumMembers.TryAdd(entry.Key, entry.Value);
        }

        foreach (var constant in imported.Constants)
        {
            constants.TryAdd(constant.Symbol.Name, constant);
        }
    }
}

internal sealed record VBExternalTypeCatalogResult(
    IReadOnlyDictionary<string, TypeSymbol> Aliases,
    IReadOnlyDictionary<string, long> QualifiedEnumMembers,
    ImmutableArray<BoundModuleVariable> Constants)
{
    public ImmutableArray<BoundModuleVariable> AddMemberSymbols(
        IDictionary<string, ModuleVariableSymbol> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var visible = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        foreach (var constant in Constants)
        {
            if (variables.ContainsKey(constant.Symbol.Name))
            {
                continue;
            }

            variables.Add(constant.Symbol.Name, constant.Symbol);
            visible.Add(constant);
        }

        return visible.ToImmutable();
    }
}
