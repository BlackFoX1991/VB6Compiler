using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// The members one generated class publishes to COM, indexed the two ways a client asks for them:
/// by name through <c>GetIDsOfNames</c> and by DISPID through <c>Invoke</c>.
///
/// The numbers are not invented here. The emitter stamps every published member with
/// <see cref="DispIdAttribute"/> and writes the same numbers into the type library, so a client that
/// read the library and a client that asked by name end up at the same member. A member without the
/// attribute is not part of the published surface.
/// </summary>
internal sealed class VBComMemberTable
{
    private static readonly ConcurrentDictionary<Type, VBComMemberTable> Tables = new();

    private readonly Dictionary<string, int> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, VBComMember> _byDispId = new();

    private VBComMemberTable(Type type)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.IsSpecialName || ReadDispId(method) is not { } dispId)
            {
                continue;
            }

            Add(method.Name, dispId, new VBComMember(method.Name, dispId, Method: method));
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (ReadDispId(property) is not { } dispId)
            {
                continue;
            }

            Add(property.Name, dispId, new VBComMember(property.Name, dispId, Property: property));
        }

        // Ein VB6-Public-Feld ist im Emitter ein CLR-Feld mit Assembly-Sichtbarkeit -- dieselbe
        // Regel, nach der VBDynamicDispatch es findet.
        foreach (var field in type.GetFields(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!field.IsAssembly || ReadDispId(field) is not { } dispId)
            {
                continue;
            }

            Add(field.Name, dispId, new VBComMember(field.Name, dispId, Field: field));
        }
    }

    public static VBComMemberTable For(Type type) => Tables.GetOrAdd(type, static key => new VBComMemberTable(key));

    public bool TryGetDispId(string name, out int dispId) => _byName.TryGetValue(name, out dispId);

    public bool TryGetMember(int dispId, out VBComMember? member)
    {
        var found = _byDispId.TryGetValue(dispId, out var value);
        member = value;
        return found;
    }

    private void Add(string name, int dispId, VBComMember member)
    {
        _byName[name] = dispId;
        _byDispId[dispId] = member;
    }

    private static int? ReadDispId(MemberInfo member) =>
        member.GetCustomAttribute<DispIdAttribute>() is { } attribute ? attribute.Value : null;
}

/// <summary>One published member. Exactly one of the three carriers is set.</summary>
internal sealed record VBComMember(
    string Name,
    int DispId,
    MethodInfo? Method = null,
    PropertyInfo? Property = null,
    FieldInfo? Field = null);
