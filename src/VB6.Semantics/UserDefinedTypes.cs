using System.Collections.Immutable;

namespace VB6.Semantics;

/// <summary>
/// VB6 fixed-length String storage used by Type members declared as <c>String * n</c>.
/// The length participates in the declared type because it is part of the UDT layout.
/// </summary>
public sealed record FixedLengthStringTypeSymbol : TypeSymbol
{
    public FixedLengthStringTypeSymbol(int length)
        : base($"String * {length}")
    {
        if (length is < 1 or > 65526)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                "VB6 fixed-length String members must contain between 1 and 65526 characters.");
        }

        Length = length;
    }

    public int Length { get; }
}

/// <summary>
/// One fixed dimension declared on an array member inside a VB6 <c>Type</c>. Bounds are inclusive
/// and use VB6 Long width so later managed layout/code generation can preserve non-zero lower
/// bounds without consulting syntax again.
/// </summary>
public readonly record struct UserDefinedTypeArrayBound(long Lower, long Upper);

/// <summary>
/// One field declared inside a VB6 <c>Type ... End Type</c>. Array members carry an
/// <see cref="ArrayTypeSymbol"/> plus their concrete declaration bounds when those bounds are
/// statically representable by the current UDT declaration binder.
/// </summary>
public sealed record UserDefinedTypeMemberSymbol(
    string Name,
    TypeSymbol Type,
    ImmutableArray<UserDefinedTypeArrayBound> ArrayBounds) : Symbol(Name)
{
    public UserDefinedTypeMemberSymbol(string name, TypeSymbol type)
        : this(name, type, ImmutableArray<UserDefinedTypeArrayBound>.Empty)
    {
    }

    public bool HasArrayBounds => !ArrayBounds.IsDefaultOrEmpty;
}

/// <summary>
/// A VB6 user-defined type. Type names are predeclared before member types are resolved, so the
/// member list is filled exactly once in a second pass. This supports forward references between
/// UDT declarations without stringly-typed placeholders.
/// </summary>
public sealed record UserDefinedTypeSymbol : TypeSymbol
{
    private readonly UserDefinedTypeDefinition _definition;

    public UserDefinedTypeSymbol(string name)
        : this(name, new UserDefinedTypeDefinition())
    {
    }

    private UserDefinedTypeSymbol(string name, UserDefinedTypeDefinition definition)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _definition = definition;
    }

    public ImmutableArray<UserDefinedTypeMemberSymbol> Members => _definition.Members;
    public bool MembersDefined => _definition.IsDefined;

    public bool TryGetMember(string name, out UserDefinedTypeMemberSymbol member) =>
        _definition.MemberMap.TryGetValue(name, out member!);

    internal bool TryDefineMembers(
        IEnumerable<UserDefinedTypeMemberSymbol> members,
        out string? duplicateMemberName)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (_definition.IsDefined)
        {
            throw new InvalidOperationException($"Members for UDT '{Name}' have already been defined.");
        }

        var memberArray = members.ToImmutableArray();
        var map = ImmutableDictionary.CreateBuilder<string, UserDefinedTypeMemberSymbol>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var member in memberArray)
        {
            if (!map.TryAdd(member.Name, member))
            {
                duplicateMemberName = member.Name;
                return false;
            }
        }

        _definition.Members = memberArray;
        _definition.MemberMap = map.ToImmutable();
        _definition.IsDefined = true;
        duplicateMemberName = null;
        return true;
    }

    private sealed class UserDefinedTypeDefinition
    {
        public ImmutableArray<UserDefinedTypeMemberSymbol> Members { get; set; } =
            ImmutableArray<UserDefinedTypeMemberSymbol>.Empty;

        public ImmutableDictionary<string, UserDefinedTypeMemberSymbol> MemberMap { get; set; } =
            ImmutableDictionary.Create<string, UserDefinedTypeMemberSymbol>(StringComparer.OrdinalIgnoreCase);

        public bool IsDefined { get; set; }
    }
}
