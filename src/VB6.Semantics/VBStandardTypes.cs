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
    public static ClassTypeSymbol Picture { get; } = CreateEmpty("Picture");
    public static ClassTypeSymbol Font { get; } = CreateFont();
    public static ClassTypeSymbol Control { get; } = CreateControl("Control");
    public static ClassTypeSymbol Form { get; } = CreateControl("Form");
    public static ClassTypeSymbol UserControl { get; } = CreateControl("UserControl");
    public static ClassTypeSymbol Screen { get; } = CreateScreen();
    public static ClassTypeSymbol Ambient { get; } = CreateAmbient();
    public static ClassTypeSymbol PropertyBag { get; } = CreatePropertyBag();

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
        var properties = new List<PropertySymbol>
        {
            ReadOnlyProperty("ActiveForm", Form),
            ReadOnlyProperty("ActiveControl", Control)
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

    private static ClassTypeSymbol CreateAmbient()
    {
        var ambient = new ClassTypeSymbol("Ambient");
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

    private static ClassTypeSymbol CreateFont()
    {
        var font = new ClassTypeSymbol("Font");
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
        properties.AddRange(ReadWriteProperties("Picture", Picture));
        properties.AddRange(ReadWriteProperties("Image", Picture));
        properties.AddRange(ReadWriteProperties("Font", Font));
        properties.AddRange(ReadWriteProperties("hDC", TypeSymbol.Long));
        if (!type.TryDefineMembers(procedures, properties, Array.Empty<EventSymbol>(), out var duplicate))
        {
            throw new InvalidOperationException($"Built-in {name} member '{duplicate}' is duplicated.");
        }

        return type;
    }

    private static IEnumerable<PropertySymbol> ReadWriteProperties(string name, TypeSymbol type) =>
        new[]
        {
            new PropertySymbol(name, PropertyAccessorKind.Get, type, ImmutableArray<ParameterSymbol>.Empty),
            new PropertySymbol(name, PropertyAccessorKind.Let, type, ImmutableArray<ParameterSymbol>.Empty)
        };
}
