using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using VB6.ProjectSystem;
using VB6.Semantics;

namespace VB6.Compiler;

/// <summary>
/// Imports the automation surface of a Windows type library into the semantic type alias scope.
/// The importer intentionally produces runtime object contracts: activation and the final COM
/// ABI remain runtime concerns, while source binding can use the real names and signatures.
/// </summary>
internal static class VBTypeLibraryImporter
{
    private const int RegKindNone = 2;
    private const short InvokeFunction = 1;
    private const short InvokePropertyGet = 2;
    private const short InvokePropertyPut = 4;
    private const short InvokePropertyPutRef = 8;

    private const short VariantArray = 0x2000;
    private const short VariantByRef = 0x4000;
    private const short VariantTypeMask = 0x0FFF;

    private const short VtEmpty = 0;
    private const short VtNull = 1;
    private const short VtI1 = 16;
    private const short VtI2 = 2;
    private const short VtI4 = 3;
    private const short VtI8 = 20;
    private const short VtUi1 = 17;
    private const short VtUi2 = 18;
    private const short VtUi4 = 19;
    private const short VtUi8 = 21;
    private const short VtR4 = 4;
    private const short VtR8 = 5;
    private const short VtBool = 11;
    private const short VtVariant = 12;
    private const short VtCurrency = 6;
    private const short VtDate = 7;
    private const short VtBstr = 8;
    private const short VtUserDefined = 29;
    private const short VtDispatch = 9;
    private const short VtUnknown = 13;
    private const short VtPtr = 26;

    public static IReadOnlyDictionary<string, TypeSymbol> Import(
        string filePath,
        string? fallbackLibraryName,
        bool controlLibrary)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(filePath))
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }

        try
        {
            var result = Load(filePath, fallbackLibraryName, controlLibrary);
            return result;
        }
        catch (COMException)
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }
        catch (ExternalException)
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }
        catch (IOException)
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }
    }

    private static IReadOnlyDictionary<string, TypeSymbol> Load(
        string filePath,
        string? fallbackLibraryName,
        bool controlLibrary)
    {
        var hresult = LoadTypeLibEx(filePath, RegKindNone, out var typeLibrary);
        if (hresult < 0 || typeLibrary is null)
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }

        typeLibrary.GetDocumentation(-1, out var documentedName, out _, out _, out _);
        var libraryName = FirstNonEmpty(
            documentedName,
            fallbackLibraryName,
            Path.GetFileNameWithoutExtension(filePath),
            "ImportedTypeLibrary");

        var records = ReadTypeInfos(typeLibrary);
        if (records.IsDefaultOrEmpty)
        {
            return ImmutableDictionary<string, TypeSymbol>.Empty;
        }

        var aliases = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        var recordTypes = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var typeName = QualifiedName(libraryName, record.Name);
            TypeSymbol symbol;
            if (record.Kind == TYPEKIND.TKIND_ENUM)
            {
                // Enum constants are added to the built-in constant scope separately. Treating
                // the enum itself as Long is enough to type parameters and return values here.
                symbol = TypeSymbol.Long;
            }
            else
            {
                var imported = new ClassTypeSymbol(typeName);
                imported.MarkAsRuntimeObjectContract();
                imported.MarkAsLateBoundObject();
                if (controlLibrary || record.IsControl)
                {
                    imported.MarkAsControlContract();
                }

                symbol = imported;
            }

            recordTypes[typeName] = symbol;
            aliases.TryAdd(typeName, symbol);
            aliases.TryAdd(record.Name, symbol);
        }

        foreach (var record in records)
        {
            if (!recordTypes.TryGetValue(QualifiedName(libraryName, record.Name), out var symbol) ||
                symbol is not ClassTypeSymbol classType)
            {
                continue;
            }

            var importedMembers = ImportMembers(record, libraryName, recordTypes);
            if (record.Kind == TYPEKIND.TKIND_COCLASS)
            {
                importedMembers = ImportImplementedMembers(record, importedMembers, libraryName, recordTypes);
            }

            classType.TryDefineMembers(
                importedMembers.Procedures,
                importedMembers.Properties,
                importedMembers.Events,
                out _);
            if (importedMembers.DefaultPropertyName is not null)
            {
                classType.SetDefaultPropertyName(importedMembers.DefaultPropertyName);
            }
        }

        return aliases.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static ImmutableArray<TypeInfoRecord> ReadTypeInfos(ITypeLib typeLibrary)
    {
        var records = ImmutableArray.CreateBuilder<TypeInfoRecord>();
        for (var index = 0; index < typeLibrary.GetTypeInfoCount(); index++)
        {
            typeLibrary.GetTypeInfoType(index, out var kind);
            typeLibrary.GetTypeInfo(index, out var typeInfo);
            typeInfo.GetDocumentation(-1, out var name, out _, out _, out _);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            typeInfo.GetTypeAttr(out var attributePointer);
            try
            {
                var attribute = Marshal.PtrToStructure<TYPEATTR>(attributePointer);
                var isControl = (attribute.wTypeFlags & TYPEFLAGS.TYPEFLAG_FCONTROL) != 0;
                records.Add(new TypeInfoRecord(index, typeInfo, kind, name, attribute, isControl));
            }
            finally
            {
                typeInfo.ReleaseTypeAttr(attributePointer);
            }
        }

        return records.ToImmutable();
    }

    private static ImportedMembers ImportMembers(
        TypeInfoRecord record,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        var properties = new Dictionary<(string Name, PropertyAccessorKind Accessor), PropertySymbol>();
        var defaultPropertyName = (string?)null;
        var typeInfo = record.TypeInfo;

        for (var index = 0; index < record.Attribute.cFuncs; index++)
        {
            typeInfo.GetFuncDesc(index, out var functionPointer);
            try
            {
                var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                var names = GetFunctionNames(typeInfo, function.memid, function.cParams);
                var name = names[0];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var parameters = ReadParameters(
                    typeInfo,
                    function,
                    names,
                    libraryName,
                    types);
                var returnType = ReadType(function.elemdescFunc.tdesc, typeInfo, libraryName, types);
                var isDefault = function.memid == 0;
                if (isDefault)
                {
                    defaultPropertyName = name;
                }

                switch ((short)function.invkind)
                {
                    case InvokeFunction:
                        procedures.TryAdd(
                            name,
                            new ProcedureSymbol(name, parameters, returnType == TypeSymbol.Error ? null : returnType)
                            {
                                IsLateBound = true
                            });
                        break;

                    case InvokePropertyGet:
                        properties.TryAdd(
                            (name, PropertyAccessorKind.Get),
                            new PropertySymbol(name, PropertyAccessorKind.Get, returnType, parameters)
                            {
                                IsLateBound = true
                            });
                        break;

                    case InvokePropertyPut:
                    case InvokePropertyPutRef:
                        if (parameters.IsDefaultOrEmpty)
                        {
                            break;
                        }

                        var valueParameter = parameters[^1];
                        var accessor = (short)function.invkind == InvokePropertyPutRef
                            ? PropertyAccessorKind.Set
                            : PropertyAccessorKind.Let;
                        properties.TryAdd(
                            (name, accessor),
                            new PropertySymbol(
                                name,
                                accessor,
                                valueParameter.Type,
                                parameters.RemoveAt(parameters.Length - 1))
                            {
                                IsLateBound = true
                            });
                        break;
                }
            }
            finally
            {
                typeInfo.ReleaseFuncDesc(functionPointer);
            }
        }

        return new ImportedMembers(
            procedures.Values.ToImmutableArray(),
            properties.Values.ToImmutableArray(),
            ImmutableArray<EventSymbol>.Empty,
            defaultPropertyName);
    }

    private static ImportedMembers ImportImplementedMembers(
        TypeInfoRecord record,
        ImportedMembers ownMembers,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        var procedures = ownMembers.Procedures.ToBuilder();
        var properties = ownMembers.Properties.ToBuilder();
        var defaultName = ownMembers.DefaultPropertyName;

        for (var index = 0; index < record.Attribute.cImplTypes; index++)
        {
            record.TypeInfo.GetRefTypeOfImplType(index, out var referenceHandle);
            record.TypeInfo.GetRefTypeInfo(referenceHandle, out var implementedTypeInfo);
            implementedTypeInfo.GetDocumentation(-1, out var implementedName, out _, out _, out _);
            if (string.IsNullOrWhiteSpace(implementedName))
            {
                continue;
            }

            var implementedRecord = new TypeInfoRecord(
                -1,
                implementedTypeInfo,
                TYPEKIND.TKIND_DISPATCH,
                implementedName,
                ReadTypeAttribute(implementedTypeInfo),
                false);
            var members = ImportMembers(implementedRecord, libraryName, types);
            procedures.AddRange(members.Procedures.Where(procedure =>
                procedures.All(existing => !string.Equals(existing.Name, procedure.Name, StringComparison.OrdinalIgnoreCase))));
            properties.AddRange(members.Properties.Where(property =>
                properties.All(existing =>
                    !string.Equals(existing.Name, property.Name, StringComparison.OrdinalIgnoreCase) ||
                    existing.Accessor != property.Accessor)));
            defaultName ??= members.DefaultPropertyName;
        }

        return new ImportedMembers(
            procedures.ToImmutable(),
            properties.ToImmutable(),
            ownMembers.Events,
            defaultName);
    }

    private static ImmutableArray<ParameterSymbol> ReadParameters(
        ITypeInfo typeInfo,
        FUNCDESC function,
        string[] names,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        if (function.cParams == 0 || function.lprgelemdescParam == IntPtr.Zero)
        {
            return ImmutableArray<ParameterSymbol>.Empty;
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>(function.cParams);
        var elementSize = Marshal.SizeOf<ELEMDESC>();
        for (var index = 0; index < function.cParams; index++)
        {
            var pointer = IntPtr.Add(function.lprgelemdescParam, index * elementSize);
            var element = Marshal.PtrToStructure<ELEMDESC>(pointer);
            var type = ReadType(element.tdesc, typeInfo, libraryName, types);
            var parameterDescription = Marshal.PtrToStructure<PARAMDESC>(
                IntPtr.Add(pointer, Marshal.SizeOf<TYPEDESC>()));
            var flags = parameterDescription.wParamFlags;
            var passingMode = (flags & PARAMFLAG.PARAMFLAG_FOUT) != 0 ||
                (element.tdesc.vt & VariantByRef) != 0
                ? ParameterPassingMode.ByRef
                : ParameterPassingMode.ByVal;
            var parameterName = index + 1 < names.Length && !string.IsNullOrWhiteSpace(names[index + 1])
                ? names[index + 1]
                : $"Parameter{index + 1}";
            parameters.Add(new ParameterSymbol(parameterName, type, passingMode)
            {
                IsOptional = (flags & PARAMFLAG.PARAMFLAG_FOPT) != 0 ||
                    (function.cParamsOpt > 0 && index >= function.cParams - function.cParamsOpt)
            });
        }

        return parameters.ToImmutable();
    }

    private static TypeSymbol ReadType(
        TYPEDESC description,
        ITypeInfo owner,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        var vt = description.vt;
        var baseType = (short)(vt & VariantTypeMask);
        if ((vt & VariantByRef) != 0)
        {
            return TypeSymbol.Variant;
        }

        if ((vt & VariantArray) != 0)
        {
            return VBStandardTypes.Object;
        }

        if (baseType == VtPtr)
        {
            return VBStandardTypes.Object;
        }

        return baseType switch
        {
            VtEmpty or VtNull or VtVariant => TypeSymbol.Variant,
            VtI1 or VtI2 => TypeSymbol.Integer,
            VtI4 => TypeSymbol.Long,
            VtI8 => TypeSymbol.LongLong,
            VtUi1 => TypeSymbol.Byte,
            VtUi2 => TypeSymbol.UShort,
            VtUi4 => TypeSymbol.UInteger,
            VtUi8 => TypeSymbol.ULong,
            VtR4 => TypeSymbol.Single,
            VtR8 => TypeSymbol.Double,
            VtBool => TypeSymbol.Boolean,
            VtCurrency => TypeSymbol.Currency,
            VtDate => TypeSymbol.Date,
            VtBstr => TypeSymbol.String,
            VtDispatch or VtUnknown => VBStandardTypes.Object,
            VtUserDefined => VBStandardTypes.Object,
            _ => TypeSymbol.Variant
        };
    }

    private static string[] GetFunctionNames(ITypeInfo typeInfo, int memberId, short parameterCount)
    {
        var names = new string[Math.Max(1, parameterCount + 1)];
        try
        {
            typeInfo.GetNames(memberId, names, names.Length, out var count);
            if (count < names.Length)
            {
                Array.Resize(ref names, Math.Max(1, count));
            }
        }
        catch (COMException)
        {
            names[0] = $"Member{memberId}";
        }

        return names;
    }

    private static TYPEATTR ReadTypeAttribute(ITypeInfo typeInfo)
    {
        typeInfo.GetTypeAttr(out var pointer);
        try
        {
            return Marshal.PtrToStructure<TYPEATTR>(pointer);
        }
        finally
        {
            typeInfo.ReleaseTypeAttr(pointer);
        }
    }

    private static string QualifiedName(string libraryName, string typeName) =>
        string.IsNullOrWhiteSpace(libraryName) ? typeName : libraryName + "." + typeName;

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int LoadTypeLibEx(
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        int regKind,
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib? typeLibrary);

    private sealed record TypeInfoRecord(
        int Index,
        ITypeInfo TypeInfo,
        TYPEKIND Kind,
        string Name,
        TYPEATTR Attribute,
        bool IsControl);

    private sealed record ImportedMembers(
        ImmutableArray<ProcedureSymbol> Procedures,
        ImmutableArray<PropertySymbol> Properties,
        ImmutableArray<EventSymbol> Events,
        string? DefaultPropertyName);
}
