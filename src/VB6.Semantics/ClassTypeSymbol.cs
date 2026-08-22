using System.Collections.Immutable;

namespace VB6.Semantics;

/// <summary>
/// A reference type declared by a VB6 class module. The type is predeclared before member types
/// are resolved so classes can refer to each other and to themselves.
/// </summary>
public sealed record ClassTypeSymbol : TypeSymbol
{
    private readonly ClassTypeDefinition _definition;

    public ClassTypeSymbol(string name, string? sourcePath = null)
        : this(name, sourcePath, new ClassTypeDefinition())
    {
    }

    private ClassTypeSymbol(string name, string? sourcePath, ClassTypeDefinition definition)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        SourcePath = sourcePath;
        _definition = definition;
    }

    public string? SourcePath { get; }
    public ImmutableArray<ProcedureSymbol> Procedures => _definition.Procedures;
    public ImmutableArray<PropertySymbol> Properties => _definition.Properties;
    public ImmutableArray<EventSymbol> Events => _definition.Events;
    public ImmutableArray<ClassTypeSymbol> ImplementedInterfaces => _definition.ImplementedInterfaces;
    public bool IsInterfaceContract => _definition.IsInterfaceContract;
    public bool MembersDefined => _definition.IsDefined;

    public bool TryGetProcedure(string name, out ProcedureSymbol procedure) =>
        _definition.ProcedureMap.TryGetValue(name, out procedure!);

    public bool TryGetProperty(string name, PropertyAccessorKind accessor, out PropertySymbol property) =>
        _definition.PropertyMap.TryGetValue(new PropertyKey(name, accessor), out property!);

    public bool TryGetDefaultProperty(PropertyAccessorKind accessor, out PropertySymbol property) =>
        TryGetProperty(_definition.DefaultPropertyName ?? "Item", accessor, out property);

    public bool TryGetEvent(string name, out EventSymbol @event) =>
        _definition.EventMap.TryGetValue(name, out @event!);

    public bool TryDefineMembers(
        IEnumerable<ProcedureSymbol> procedures,
        IEnumerable<PropertySymbol> properties,
        IEnumerable<EventSymbol> events,
        out string? duplicateMemberName)
    {
        ArgumentNullException.ThrowIfNull(procedures);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(events);
        if (_definition.IsDefined)
        {
            throw new InvalidOperationException($"Members for class '{Name}' have already been defined.");
        }

        var procedureArray = procedures.ToImmutableArray();
        var propertyArray = properties.ToImmutableArray();
        var eventArray = events.ToImmutableArray();
        var procedureMap = ImmutableDictionary.CreateBuilder<string, ProcedureSymbol>(
            StringComparer.OrdinalIgnoreCase);
        var propertyMap = ImmutableDictionary.CreateBuilder<PropertyKey, PropertySymbol>();
        var eventMap = ImmutableDictionary.CreateBuilder<string, EventSymbol>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var procedure in procedureArray)
        {
            if (!procedureMap.TryAdd(procedure.Name, procedure))
            {
                duplicateMemberName = procedure.Name;
                return false;
            }
        }

        foreach (var property in propertyArray)
        {
            if (!propertyMap.TryAdd(new PropertyKey(property.Name, property.Accessor), property))
            {
                duplicateMemberName = property.Name;
                return false;
            }
        }

        foreach (var @event in eventArray)
        {
            if (!eventMap.TryAdd(@event.Name, @event))
            {
                duplicateMemberName = @event.Name;
                return false;
            }
        }

        _definition.Procedures = procedureArray;
        _definition.Properties = propertyArray;
        _definition.Events = eventArray;
        _definition.ProcedureMap = procedureMap.ToImmutable();
        _definition.PropertyMap = propertyMap.ToImmutable();
        _definition.EventMap = eventMap.ToImmutable();
        _definition.IsDefined = true;
        duplicateMemberName = null;
        return true;
    }

    public void SetImplementedInterfaces(IEnumerable<ClassTypeSymbol> interfaces)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        _definition.ImplementedInterfaces = interfaces
            .Distinct()
            .ToImmutableArray();
    }

    public void SetDefaultPropertyName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _definition.DefaultPropertyName = name;
    }

    public void MarkAsInterfaceContract() => _definition.IsInterfaceContract = true;

    private readonly record struct PropertyKey(string Name, PropertyAccessorKind Accessor);

    private sealed class ClassTypeDefinition
    {
        public ImmutableArray<ProcedureSymbol> Procedures { get; set; } = ImmutableArray<ProcedureSymbol>.Empty;
        public ImmutableArray<PropertySymbol> Properties { get; set; } = ImmutableArray<PropertySymbol>.Empty;
        public ImmutableArray<EventSymbol> Events { get; set; } = ImmutableArray<EventSymbol>.Empty;
        public string? DefaultPropertyName { get; set; }
        public ImmutableArray<ClassTypeSymbol> ImplementedInterfaces { get; set; } =
            ImmutableArray<ClassTypeSymbol>.Empty;
        public bool IsInterfaceContract { get; set; }
        public ImmutableDictionary<string, ProcedureSymbol> ProcedureMap { get; set; } =
            ImmutableDictionary.Create<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        public ImmutableDictionary<PropertyKey, PropertySymbol> PropertyMap { get; set; } =
            ImmutableDictionary<PropertyKey, PropertySymbol>.Empty;
        public ImmutableDictionary<string, EventSymbol> EventMap { get; set; } =
            ImmutableDictionary.Create<string, EventSymbol>(StringComparer.OrdinalIgnoreCase);
        public bool IsDefined { get; set; }
    }
}
