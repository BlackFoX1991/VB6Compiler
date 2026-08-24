using System.Collections.Immutable;

namespace VB6.Semantics;

/// <summary>
/// Built-in object contracts supplied by the VB6 runtime library. The concrete storage and COM
/// identity are backend concerns; keeping the member signatures in the semantic type system lets
/// normal class/member binding work before either backend is selected.
/// </summary>
public static class VBStandardTypes
{
    public static ClassTypeSymbol Object { get; } = CreateEmpty("Object");
    public static ClassTypeSymbol Collection { get; } = CreateCollection();
    public static ClassTypeSymbol App { get; } = CreateApp();
    public static ClassTypeSymbol Picture { get; } = CreatePicture();
    public static ClassTypeSymbol Font { get; } = CreateFont();
    public static ClassTypeSymbol Control { get; } = CreateControl("Control");
    public static ClassTypeSymbol Form { get; } = CreateControl("Form");
    public static ClassTypeSymbol UserControl { get; } = CreateControl("UserControl");
    public static ClassTypeSymbol Screen { get; } = CreateScreen();
    public static ClassTypeSymbol Ambient { get; } = CreateAmbient();
    public static ClassTypeSymbol PropertyBag { get; } = CreatePropertyBag();
    public static ClassTypeSymbol Clipboard { get; } = CreateClipboard();
    public static ClassTypeSymbol ExternalTreeNode { get; } = CreateExternalTreeNode();
    public static ClassTypeSymbol ExternalTreeNodeCollection { get; } = CreateExternalTreeNodeCollection();
    public static ClassTypeSymbol ExternalTreeView { get; } = CreateExternalTreeView();
    public static ClassTypeSymbol ExternalRichTextBox { get; } = CreateExternalRichTextBox();
    public static ClassTypeSymbol ExternalCommonDialog { get; } = CreateExternalCommonDialog();
    public static ClassTypeSymbol ExternalListImage { get; } = CreateExternalListImage();
    public static ClassTypeSymbol ExternalListImages { get; } = CreateExternalListImages();
    public static ClassTypeSymbol ExternalImageList { get; } = CreateExternalImageList();
    public static ClassTypeSymbol ExternalComboItem { get; } = CreateExternalComboItem();
    public static ClassTypeSymbol ExternalComboItems { get; } = CreateExternalComboItems();
    public static ClassTypeSymbol ExternalImageCombo { get; } = CreateExternalImageCombo();

    private static ClassTypeSymbol CreateCollection()
    {
        var collection = new ClassTypeSymbol("Collection");
        var itemParameter = new ParameterSymbol("Index", TypeSymbol.Variant, ParameterPassingMode.ByVal);
        var count = new PropertySymbol(
            "Count",
            PropertyAccessorKind.Get,
            TypeSymbol.Long,
            ImmutableArray<ParameterSymbol>.Empty);
        var item = new PropertySymbol(
            "Item",
            PropertyAccessorKind.Get,
            TypeSymbol.Variant,
            ImmutableArray.Create(itemParameter));
        var add = new ProcedureSymbol(
            "Add",
            ImmutableArray.Create(
                new ParameterSymbol("Item", TypeSymbol.Variant, ParameterPassingMode.ByVal),
                OptionalVariantParameter("Key"),
                OptionalVariantParameter("Before"),
                OptionalVariantParameter("After")),
            ReturnType: null);
        var remove = new ProcedureSymbol(
            "Remove",
            ImmutableArray.Create(itemParameter),
            ReturnType: null);

        if (!collection.TryDefineMembers(
                new[] { add, remove },
                new[] { count, item },
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in Collection member '{duplicate}' is duplicated.");
        }

        return collection;
    }

    private static ParameterSymbol OptionalVariantParameter(string name) =>
        new(name, TypeSymbol.Variant, ParameterPassingMode.ByVal)
        {
            IsOptional = true
        };

    private static ClassTypeSymbol CreateApp()
    {
        var app = new ClassTypeSymbol("App");
        var properties = new[]
        {
            ReadOnlyProperty("EXEName", TypeSymbol.String),
            ReadOnlyProperty("Path", TypeSymbol.String),
            ReadOnlyProperty("Title", TypeSymbol.String),
            ReadOnlyProperty("hInstance", TypeSymbol.Long),
            ReadOnlyProperty("Major", TypeSymbol.Long),
            ReadOnlyProperty("Minor", TypeSymbol.Long),
            ReadOnlyProperty("Revision", TypeSymbol.Long)
        };
        if (!app.TryDefineMembers(
                Array.Empty<ProcedureSymbol>(),
                properties,
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in App member '{duplicate}' is duplicated.");
        }

        return app;
    }

    private static ClassTypeSymbol CreateScreen()
    {
        var screen = new ClassTypeSymbol("Screen");
        screen.MarkAsRuntimeObjectContract();
        screen.MarkAsLateBoundObject();
        var properties = new List<PropertySymbol>
        {
            ReadOnlyProperty("ActiveForm", Form),
            ReadOnlyProperty("ActiveControl", Control),
            ReadOnlyProperty("TwipsPerPixelX", TypeSymbol.Single),
            ReadOnlyProperty("TwipsPerPixelY", TypeSymbol.Single)
        };
        properties.AddRange(ReadWriteProperties("MousePointer", TypeSymbol.Long));
        if (!screen.TryDefineMembers(
                Array.Empty<ProcedureSymbol>(),
                properties,
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in Screen member '{duplicate}' is duplicated.");
        }

        return screen;
    }

    private static ClassTypeSymbol CreatePicture()
    {
        var picture = new ClassTypeSymbol("Picture");
        picture.MarkAsRuntimeObjectContract();
        picture.MarkAsLateBoundObject();
        var properties = new[]
        {
            ReadOnlyProperty("Width", TypeSymbol.Long),
            ReadOnlyProperty("Height", TypeSymbol.Long),
            ReadOnlyProperty("Type", TypeSymbol.Long)
        };
        if (!picture.TryDefineMembers(
                Array.Empty<ProcedureSymbol>(),
                properties,
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in Picture member '{duplicate}' is duplicated.");
        }

        return picture;
    }

    private static ClassTypeSymbol CreateAmbient()
    {
        var ambient = new ClassTypeSymbol("Ambient");
        ambient.MarkAsRuntimeObjectContract();
        ambient.MarkAsLateBoundObject();
        var properties = new[]
        {
            ReadOnlyProperty("Font", Font),
            ReadOnlyProperty("UserMode", TypeSymbol.Boolean),
            ReadOnlyProperty("DisplayName", TypeSymbol.String)
        };
        if (!ambient.TryDefineMembers(
                Array.Empty<ProcedureSymbol>(),
                properties,
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in Ambient member '{duplicate}' is duplicated.");
        }

        return ambient;
    }

    private static ClassTypeSymbol CreatePropertyBag()
    {
        var bag = new ClassTypeSymbol("PropertyBag");
        bag.MarkAsRuntimeObjectContract();
        bag.MarkAsLateBoundObject();
        var procedures = new[]
        {
            new ProcedureSymbol(
                "ReadProperty",
                ImmutableArray.Create(
                    new ParameterSymbol("Name", TypeSymbol.String, ParameterPassingMode.ByVal),
                    OptionalVariantParameter("DefaultValue")),
                TypeSymbol.Variant),
            new ProcedureSymbol(
                "WriteProperty",
                ImmutableArray.Create(
                    new ParameterSymbol("Name", TypeSymbol.String, ParameterPassingMode.ByVal),
                    new ParameterSymbol("Value", TypeSymbol.Variant, ParameterPassingMode.ByVal),
                    OptionalVariantParameter("DefaultValue")),
                ReturnType: null)
        };
        if (!bag.TryDefineMembers(
                procedures,
                Array.Empty<PropertySymbol>(),
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in PropertyBag member '{duplicate}' is duplicated.");
        }

        return bag;
    }

    private static ClassTypeSymbol CreateClipboard()
    {
        var clipboard = new ClassTypeSymbol("Clipboard");
        clipboard.MarkAsRuntimeObjectContract();
        clipboard.MarkAsLateBoundObject();
        var procedures = new[]
        {
            new ProcedureSymbol("GetText", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.String)
        };
        if (!clipboard.TryDefineMembers(
                procedures,
                Array.Empty<PropertySymbol>(),
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in Clipboard member '{duplicate}' is duplicated.");
        }

        return clipboard;
    }

    private static ClassTypeSymbol CreateExternalTreeNode()
    {
        var node = new ClassTypeSymbol("MSComctlLib.Node");
        node.MarkAsRuntimeObjectContract();
        node.MarkAsLateBoundObject();
        var properties = new[]
        {
            LateBoundReadOnlyProperty("Key", TypeSymbol.String),
            LateBoundReadOnlyProperty("Text", TypeSymbol.String),
            LateBoundReadOnlyProperty("Index", TypeSymbol.Long)
        };
        if (!node.TryDefineMembers(
                Array.Empty<ProcedureSymbol>(),
                properties,
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in TreeView node member '{duplicate}' is duplicated.");
        }

        return node;
    }

    private static ClassTypeSymbol CreateExternalTreeNodeCollection()
    {
        var collection = new ClassTypeSymbol("MSComctlLib.Nodes");
        collection.MarkAsRuntimeObjectContract();
        collection.MarkAsLateBoundObject();
        var item = new PropertySymbol(
            "Item",
            PropertyAccessorKind.Get,
            ExternalTreeNode,
            ImmutableArray.Create(OptionalVariantParameter("Index")))
        {
            IsLateBound = true
        };
        var procedures = new[]
        {
            LateBoundProcedure(
                "Add",
                ImmutableArray.Create(
                    OptionalVariantParameter("Relative"),
                    OptionalVariantParameter("Relationship"),
                    OptionalVariantParameter("Key"),
                    OptionalVariantParameter("Text"),
                    OptionalVariantParameter("Image"),
                    OptionalVariantParameter("SelectedImage")),
                ExternalTreeNode),
            LateBoundProcedure(
                "Remove",
                ImmutableArray.Create(new ParameterSymbol("Index", TypeSymbol.Variant, ParameterPassingMode.ByVal))),
            LateBoundProcedure("Clear", ImmutableArray<ParameterSymbol>.Empty)
        };
        var properties = new[]
        {
            LateBoundReadOnlyProperty("Count", TypeSymbol.Long),
            item
        };
        if (!collection.TryDefineMembers(procedures, properties, Array.Empty<EventSymbol>(), out var duplicate))
        {
            throw new InvalidOperationException($"Built-in TreeView node collection member '{duplicate}' is duplicated.");
        }

        collection.SetDefaultPropertyName("Item");
        return collection;
    }

    private static ClassTypeSymbol CreateExternalTreeView()
    {
        var properties = new List<PropertySymbol>
        {
            LateBoundReadOnlyProperty("Nodes", ExternalTreeNodeCollection),
            LateBoundReadOnlyProperty("SelectedItem", ExternalTreeNode)
        };
        properties.AddRange(LateBoundReadWriteProperties("Style", TypeSymbol.Long));
        properties.AddRange(LateBoundReadWriteProperties("LineStyle", TypeSymbol.Long));
        return CreateExternalControl("MSComctlLib.TreeView", Array.Empty<ProcedureSymbol>(), properties);
    }

    private static ClassTypeSymbol CreateExternalListImage()
    {
        var properties = new List<PropertySymbol>
        {
            LateBoundReadOnlyProperty("Key", TypeSymbol.String),
            LateBoundReadOnlyProperty("Index", TypeSymbol.Long)
        };
        properties.AddRange(LateBoundReadWriteProperties("Picture", Picture));
        return CreateExternalObject("MSComctlLib.ListImage", Array.Empty<ProcedureSymbol>(), properties);
    }

    private static ClassTypeSymbol CreateExternalListImages()
    {
        var item = new PropertySymbol(
            "Item",
            PropertyAccessorKind.Get,
            ExternalListImage,
            ImmutableArray.Create(OptionalVariantParameter("Index")))
        {
            IsLateBound = true
        };
        var procedures = new[]
        {
            LateBoundProcedure(
                "Add",
                ImmutableArray.Create(
                    OptionalVariantParameter("Index"),
                    OptionalVariantParameter("Key"),
                    OptionalVariantParameter("FileName")),
                ExternalListImage),
            LateBoundProcedure(
                "Remove",
                ImmutableArray.Create(new ParameterSymbol("Index", TypeSymbol.Variant, ParameterPassingMode.ByVal))),
            LateBoundProcedure("Clear", ImmutableArray<ParameterSymbol>.Empty)
        };
        var properties = new[]
        {
            LateBoundReadOnlyProperty("Count", TypeSymbol.Long),
            item
        };
        var type = CreateExternalObject("MSComctlLib.ListImages", procedures, properties);
        type.SetDefaultPropertyName("Item");
        return type;
    }

    private static ClassTypeSymbol CreateExternalImageList()
    {
        var properties = new List<PropertySymbol>
        {
            LateBoundReadOnlyProperty("ListImages", ExternalListImages)
        };
        properties.AddRange(LateBoundReadWriteProperties("ImageWidth", TypeSymbol.Long));
        properties.AddRange(LateBoundReadWriteProperties("ImageHeight", TypeSymbol.Long));
        return CreateExternalObject("MSComctlLib.ImageList", Array.Empty<ProcedureSymbol>(), properties);
    }

    private static ClassTypeSymbol CreateExternalComboItem()
    {
        var properties = new List<PropertySymbol>
        {
            LateBoundReadOnlyProperty("Key", TypeSymbol.String),
            LateBoundReadOnlyProperty("Index", TypeSymbol.Long)
        };
        properties.AddRange(LateBoundReadWriteProperties("Text", TypeSymbol.String));
        properties.AddRange(LateBoundReadWriteProperties("Selected", TypeSymbol.Boolean));
        properties.AddRange(LateBoundReadWriteProperties("Image", TypeSymbol.Long));
        return CreateExternalObject("MSComctlLib.ComboItem", Array.Empty<ProcedureSymbol>(), properties);
    }

    private static ClassTypeSymbol CreateExternalComboItems()
    {
        var item = new PropertySymbol(
            "Item",
            PropertyAccessorKind.Get,
            ExternalComboItem,
            ImmutableArray.Create(OptionalVariantParameter("Index")))
        {
            IsLateBound = true
        };
        var procedures = new[]
        {
            LateBoundProcedure(
                "Add",
                ImmutableArray.Create(
                    OptionalVariantParameter("Index"),
                    OptionalVariantParameter("Key"),
                    OptionalVariantParameter("Text"),
                    OptionalVariantParameter("Image")),
                ExternalComboItem),
            LateBoundProcedure(
                "Remove",
                ImmutableArray.Create(new ParameterSymbol("Index", TypeSymbol.Variant, ParameterPassingMode.ByVal))),
            LateBoundProcedure("Clear", ImmutableArray<ParameterSymbol>.Empty)
        };
        var properties = new[]
        {
            LateBoundReadOnlyProperty("Count", TypeSymbol.Long),
            item
        };
        var type = CreateExternalObject("MSComctlLib.ComboItems", procedures, properties);
        type.SetDefaultPropertyName("Item");
        return type;
    }

    private static ClassTypeSymbol CreateExternalImageCombo()
    {
        var properties = new List<PropertySymbol>
        {
            LateBoundReadOnlyProperty("ComboItems", ExternalComboItems),
            LateBoundReadOnlyProperty("SelectedItem", ExternalComboItem)
        };
        properties.AddRange(LateBoundReadWriteProperties("ImageList", VBStandardTypes.Object));
        return CreateExternalControl("MSComctlLib.ImageCombo", Array.Empty<ProcedureSymbol>(), properties);
    }

    private static ClassTypeSymbol CreateExternalRichTextBox()
    {
        var procedures = new[]
        {
            LateBoundProcedure(
                "LoadFile",
                ImmutableArray.Create(new ParameterSymbol("FileName", TypeSymbol.String, ParameterPassingMode.ByVal))),
            LateBoundProcedure(
                "SaveFile",
                ImmutableArray.Create(new ParameterSymbol("FileName", TypeSymbol.String, ParameterPassingMode.ByVal)))
        };
        var properties = new List<PropertySymbol>();
        properties.AddRange(LateBoundReadWriteProperties("SelText", TypeSymbol.String));
        properties.AddRange(LateBoundReadWriteProperties("SelStart", TypeSymbol.Long));
        properties.AddRange(LateBoundReadWriteProperties("SelLength", TypeSymbol.Long));
        properties.AddRange(LateBoundReadWriteProperties("FileName", TypeSymbol.String));
        properties.AddRange(LateBoundReadWriteProperties("Modified", TypeSymbol.Boolean));
        return CreateExternalControl("RichTextLib.RichTextBox", procedures, properties);
    }

    private static ClassTypeSymbol CreateExternalCommonDialog()
    {
        var procedures = new[]
        {
            LateBoundProcedure("ShowOpen", ImmutableArray<ParameterSymbol>.Empty),
            LateBoundProcedure("ShowSave", ImmutableArray<ParameterSymbol>.Empty),
            LateBoundProcedure("ShowColor", ImmutableArray<ParameterSymbol>.Empty),
            LateBoundProcedure("ShowFont", ImmutableArray<ParameterSymbol>.Empty),
            LateBoundProcedure("ShowPrinter", ImmutableArray<ParameterSymbol>.Empty)
        };
        var properties = new List<PropertySymbol>();
        properties.AddRange(LateBoundReadWriteProperties("CancelError", TypeSymbol.Boolean));
        properties.AddRange(LateBoundReadWriteProperties("Filter", TypeSymbol.String));
        properties.AddRange(LateBoundReadWriteProperties("FileName", TypeSymbol.String));
        properties.AddRange(LateBoundReadWriteProperties("DialogTitle", TypeSymbol.String));
        properties.AddRange(LateBoundReadWriteProperties("FilterIndex", TypeSymbol.Long));
        return CreateExternalControl("MSComDlg.CommonDialog", procedures, properties);
    }

    private static ClassTypeSymbol CreateFont()
    {
        var font = new ClassTypeSymbol("Font");
        font.MarkAsRuntimeObjectContract();
        font.MarkAsLateBoundObject();
        var properties = new List<PropertySymbol>();
        properties.AddRange(ReadWriteProperties("Name", TypeSymbol.String));
        properties.AddRange(ReadWriteProperties("Size", TypeSymbol.Single));
        properties.AddRange(ReadWriteProperties("Bold", TypeSymbol.Boolean));
        properties.AddRange(ReadWriteProperties("Italic", TypeSymbol.Boolean));
        properties.AddRange(ReadWriteProperties("Underline", TypeSymbol.Boolean));
        properties.AddRange(ReadWriteProperties("Strikethrough", TypeSymbol.Boolean));
        properties.AddRange(ReadWriteProperties("Weight", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Charset", TypeSymbol.Integer));
        properties.AddRange(ReadWriteProperties("hFont", TypeSymbol.Long));
        if (!font.TryDefineMembers(
                Array.Empty<ProcedureSymbol>(),
                properties,
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in Font member '{duplicate}' is duplicated.");
        }

        return font;
    }

    private static PropertySymbol ReadOnlyProperty(string name, TypeSymbol type) =>
        new(name, PropertyAccessorKind.Get, type, ImmutableArray<ParameterSymbol>.Empty);

    private static ClassTypeSymbol CreateEmpty(string name)
    {
        var type = new ClassTypeSymbol(name);
        type.TryDefineMembers(
            Array.Empty<ProcedureSymbol>(),
            Array.Empty<PropertySymbol>(),
            Array.Empty<EventSymbol>(),
            out _);
        return type;
    }

    private static ClassTypeSymbol CreateControl(string name)
    {
        var type = new ClassTypeSymbol(name);
        type.MarkAsRuntimeObjectContract();
        type.MarkAsLateBoundObject();
        type.MarkAsControlContract();
        var item = new PropertySymbol(
            "Item",
            PropertyAccessorKind.Get,
            TypeSymbol.Variant,
            ImmutableArray.Create(new ParameterSymbol("Index", TypeSymbol.Long, ParameterPassingMode.ByVal)));
        var procedures = new[]
        {
            new ProcedureSymbol("SetFocus"),
            new ProcedureSymbol("Show"),
            new ProcedureSymbol("Hide")
        };
        var properties = new List<PropertySymbol>
        {
            item,
            ReadOnlyProperty("Controls", Object),
            ReadOnlyProperty("hWnd", TypeSymbol.Long),
            ReadOnlyProperty("hInstance", TypeSymbol.Long),
            ReadOnlyProperty("Name", TypeSymbol.String),
            ReadOnlyProperty("Index", TypeSymbol.Long)
        };
        properties.AddRange(ReadWriteProperties("Left", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Top", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Width", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Height", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Visible", TypeSymbol.Boolean));
        properties.AddRange(ReadWriteProperties("Enabled", TypeSymbol.Boolean));
        properties.AddRange(ReadWriteProperties("Caption", TypeSymbol.String));
        properties.AddRange(ReadWriteProperties("Text", TypeSymbol.String));
        properties.AddRange(ReadWriteProperties("BackColor", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("ForeColor", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("BorderStyle", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Appearance", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("MousePointer", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("ScaleHeight", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("ScaleWidth", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("CurrentX", TypeSymbol.Single));
        properties.AddRange(ReadWriteProperties("CurrentY", TypeSymbol.Single));
        properties.AddRange(ReadWriteProperties("FillStyle", TypeSymbol.Long));
        properties.AddRange(ReadWriteProperties("Picture", Picture));
        properties.AddRange(ReadWriteProperties("Image", Picture));
        properties.AddRange(ReadWriteProperties("Font", Font));
        properties.AddRange(ReadWriteProperties("hDC", TypeSymbol.Long));
        if (!type.TryDefineMembers(
                procedures.Select(procedure => procedure with { IsLateBound = true }),
                properties.Select(property => property with { IsLateBound = true }),
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in {name} member '{duplicate}' is duplicated.");
        }

        return type;
    }

    private static ClassTypeSymbol CreateExternalControl(
        string name,
        IEnumerable<ProcedureSymbol> procedures,
        IEnumerable<PropertySymbol> properties)
        => CreateExternalObject(name, procedures, properties, isControl: true);

    private static ClassTypeSymbol CreateExternalObject(
        string name,
        IEnumerable<ProcedureSymbol> procedures,
        IEnumerable<PropertySymbol> properties,
        bool isControl = false)
    {
        var type = new ClassTypeSymbol(name);
        type.MarkAsRuntimeObjectContract();
        type.MarkAsLateBoundObject();
        if (isControl)
        {
            type.MarkAsControlContract();
        }
        var inheritedProcedures = isControl
            ? Control.Procedures.Select(procedure => procedure with { IsLateBound = true })
            : Enumerable.Empty<ProcedureSymbol>();
        var inheritedProperties = isControl
            ? Control.Properties.Select(property => property with { IsLateBound = true })
            : Enumerable.Empty<PropertySymbol>();
        if (!type.TryDefineMembers(
                inheritedProcedures.Concat(procedures),
                inheritedProperties.Concat(properties),
                Array.Empty<EventSymbol>(),
                out var duplicate))
        {
            throw new InvalidOperationException($"Built-in {name} member '{duplicate}' is duplicated.");
        }

        return type;
    }

    private static ProcedureSymbol LateBoundProcedure(
        string name,
        ImmutableArray<ParameterSymbol> parameters,
        TypeSymbol? returnType = null) =>
        new(name, parameters, returnType) { IsLateBound = true };

    private static PropertySymbol LateBoundReadOnlyProperty(string name, TypeSymbol type) =>
        new(name, PropertyAccessorKind.Get, type, ImmutableArray<ParameterSymbol>.Empty)
        {
            IsLateBound = true
        };

    private static IEnumerable<PropertySymbol> LateBoundReadWriteProperties(string name, TypeSymbol type) =>
        new[]
        {
            new PropertySymbol(name, PropertyAccessorKind.Get, type, ImmutableArray<ParameterSymbol>.Empty)
            {
                IsLateBound = true
            },
            new PropertySymbol(name, PropertyAccessorKind.Let, type, ImmutableArray<ParameterSymbol>.Empty)
            {
                IsLateBound = true
            }
        };

    private static IEnumerable<PropertySymbol> ReadWriteProperties(string name, TypeSymbol type) =>
        new[]
        {
            new PropertySymbol(name, PropertyAccessorKind.Get, type, ImmutableArray<ParameterSymbol>.Empty),
            new PropertySymbol(name, PropertyAccessorKind.Let, type, ImmutableArray<ParameterSymbol>.Empty)
        };
}
