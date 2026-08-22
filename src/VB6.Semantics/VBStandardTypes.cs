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
    public static ClassTypeSymbol Control { get; } = CreateControl("Control");
    public static ClassTypeSymbol Form { get; } = CreateControl("Form");
    public static ClassTypeSymbol UserControl { get; } = CreateControl("UserControl");

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
