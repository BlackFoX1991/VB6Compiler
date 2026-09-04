using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using VB6.ProjectSystem;
using VB6.Semantics;

namespace VB6.Compiler;

/// <summary>
/// Imports the automation surface, enum constants, and representable record fields of a Windows
/// type library into the semantic project scope. The importer intentionally produces runtime
/// object contracts: activation and the final COM ABI remain runtime concerns, while source
/// binding can use real names/signatures and safe UDT layout metadata.
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
    private const short VtInt = 22;
    private const short VtUInt = 23;
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
    private const short VtSafeArray = 27;
    private const short VtCArray = 28;
    private const short VtPtr = 26;

    public static VBTypeLibraryImportResult Import(
        string filePath,
        string? fallbackLibraryName,
        bool controlLibrary)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(filePath))
        {
            return VBTypeLibraryImportResult.Empty;
        }

        try
        {
            var result = Load(filePath, fallbackLibraryName, controlLibrary);
            return result;
        }
        catch (COMException)
        {
            return VBTypeLibraryImportResult.Empty;
        }
        catch (ExternalException)
        {
            return VBTypeLibraryImportResult.Empty;
        }
        catch (IOException)
        {
            return VBTypeLibraryImportResult.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return VBTypeLibraryImportResult.Empty;
        }
    }

    private static VBTypeLibraryImportResult Load(
        string filePath,
        string? fallbackLibraryName,
        bool controlLibrary)
    {
        var hresult = LoadTypeLibEx(filePath, RegKindNone, out var typeLibrary);
        if (hresult < 0 || typeLibrary is null)
        {
            return VBTypeLibraryImportResult.Empty;
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
            return VBTypeLibraryImportResult.Empty;
        }

        var aliases = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        var recordTypes = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        var qualifiedEnumMembers = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var constants = new Dictionary<string, BoundModuleVariable>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var typeName = QualifiedName(libraryName, record.Name);
            TypeSymbol symbol;
            if (record.Kind == TYPEKIND.TKIND_ENUM)
            {
                // VB6 enums are Long-sized; their named constants are imported below.
                symbol = TypeSymbol.Long;
            }
            else if (record.Kind == TYPEKIND.TKIND_RECORD)
            {
                symbol = new UserDefinedTypeSymbol(typeName);
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

        foreach (var record in records.Where(record => record.Kind == TYPEKIND.TKIND_ALIAS))
        {
            var typeName = QualifiedName(libraryName, record.Name);
            var aliasType = ReadType(record.Attribute.tdescAlias, record.TypeInfo, libraryName, recordTypes);
            recordTypes[typeName] = aliasType;
            aliases[typeName] = aliasType;
            aliases[record.Name] = aliasType;
        }

        foreach (var record in records.Where(record => record.Kind == TYPEKIND.TKIND_ENUM))
        {
            ImportEnumMembers(
                record,
                libraryName,
                qualifiedEnumMembers,
                constants);
        }

        foreach (var record in records)
        {
            if (!recordTypes.TryGetValue(QualifiedName(libraryName, record.Name), out var symbol) ||
                record.Kind is TYPEKIND.TKIND_ENUM or TYPEKIND.TKIND_ALIAS)
            {
                continue;
            }

            if (symbol is UserDefinedTypeSymbol recordType)
            {
                var recordMembers = ImportRecordMembers(record, libraryName, recordTypes);
                recordType.TryDefineImportedMembers(recordMembers, out _);
                continue;
            }

            if (symbol is not ClassTypeSymbol classType)
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

        return new VBTypeLibraryImportResult(
            aliases.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
            qualifiedEnumMembers.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
            constants.Values.ToImmutableArray());
    }

    private static void ImportEnumMembers(
        TypeInfoRecord record,
        string libraryName,
        IDictionary<string, long> qualifiedMembers,
        IDictionary<string, BoundModuleVariable> constants)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var typeInfo = record.TypeInfo;
        var qualifiedTypeName = QualifiedName(libraryName, record.Name);
        for (var index = 0; index < record.Attribute.cVars; index++)
        {
            typeInfo.GetVarDesc(index, out var variablePointer);
            try
            {
                var variable = Marshal.PtrToStructure<VARDESC>(variablePointer);
                if (variable.varkind != VARKIND.VAR_CONST ||
                    variable.desc.lpvarValue == IntPtr.Zero ||
                    !TryReadEnumValue(variable.desc.lpvarValue, out var value))
                {
                    continue;
                }

                var name = GetVariableName(typeInfo, variable.memid);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                qualifiedMembers.TryAdd($"{qualifiedTypeName}.{name}", value);
                qualifiedMembers.TryAdd($"{record.Name}.{name}", value);
                constants.TryAdd(
                    name,
                    new BoundModuleVariable(
                        new ModuleVariableSymbol(name, TypeSymbol.Long)
                        {
                            IsConstant = true
                        },
                        new BoundLiteralExpression(value, TypeSymbol.Long),
                        IsConstant: true));
            }
            finally
            {
                typeInfo.ReleaseVarDesc(variablePointer);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryReadEnumValue(IntPtr variantPointer, out long value)
    {
        try
        {
            var rawValue = Marshal.GetObjectForNativeVariant(variantPointer);
            value = Convert.ToInt64(rawValue, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or OverflowException)
        {
            value = 0;
            return false;
        }
    }

    private static string? GetVariableName(ITypeInfo typeInfo, int memberId)
    {
        var names = new string[1];
        try
        {
            typeInfo.GetNames(memberId, names, names.Length, out var count);
            return count > 0 ? names[0] : null;
        }
        catch (COMException)
        {
            return null;
        }
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

    private static ImmutableArray<UserDefinedTypeMemberSymbol> ImportRecordMembers(
        TypeInfoRecord record,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        var members = ImmutableArray.CreateBuilder<UserDefinedTypeMemberSymbol>();
        var typeInfo = record.TypeInfo;
        for (var index = 0; index < record.Attribute.cVars; index++)
        {
            typeInfo.GetVarDesc(index, out var variablePointer);
            try
            {
                var variable = Marshal.PtrToStructure<VARDESC>(variablePointer);
                if (variable.varkind != VARKIND.VAR_PERINSTANCE &&
                    variable.varkind != VARKIND.VAR_STATIC)
                {
                    continue;
                }

                var name = GetVariableName(typeInfo, variable.memid);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var type = ReadType(variable.elemdescVar.tdesc, typeInfo, libraryName, types);
                members.Add(new UserDefinedTypeMemberSymbol(name, type));
            }
            finally
            {
                typeInfo.ReleaseVarDesc(variablePointer);
            }
        }

        return members.ToImmutable();
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
                                IsLateBound = true,
                                ComDispId = function.memid
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
        var events = ownMembers.Events.ToBuilder();
        var defaultName = ownMembers.DefaultPropertyName;

        for (var index = 0; index < record.Attribute.cImplTypes; index++)
        {
            record.TypeInfo.GetImplTypeFlags(index, out var implementationFlags);
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
            if ((implementationFlags & IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE) != 0)
            {
                var sourceEvents = members.Procedures.Select(procedure =>
                    new EventSymbol(procedure.Name, procedure.Parameters)
                    {
                        ComInterfaceId = implementedRecord.Attribute.guid,
                        ComDispId = procedure.ComDispId
                    });
                events.AddRange(sourceEvents.Where(@event =>
                    events.All(existing =>
                        !string.Equals(existing.Name, @event.Name, StringComparison.OrdinalIgnoreCase))));
                continue;
            }

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
            events.ToImmutable(),
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
            var type = ReadParameterType(element.tdesc, typeInfo, libraryName, types);
            var parameterDescription = Marshal.PtrToStructure<PARAMDESC>(
                IntPtr.Add(pointer, Marshal.SizeOf<TYPEDESC>()));
            var flags = parameterDescription.wParamFlags;
            var passingMode = (flags & PARAMFLAG.PARAMFLAG_FOUT) != 0 ||
                IsParameterByRef(element.tdesc)
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

    private static TypeSymbol ReadParameterType(
        TYPEDESC description,
        ITypeInfo owner,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        if ((description.vt & VariantTypeMask) != VtPtr ||
            description.lpValue == IntPtr.Zero)
        {
            return ReadType(description, owner, libraryName, types);
        }

        // Automation event parameters in classic OCX type libraries are commonly encoded as
        // VT_PTR to the actual scalar type, even when PARAMFLAG_FOUT is absent. A second pointer
        // is an opaque native/COM pointer contract and must not be guessed as a VB scalar.
        var pointedType = Marshal.PtrToStructure<TYPEDESC>(description.lpValue);
        var pointedBaseType = (short)(pointedType.vt & VariantTypeMask);
        if (pointedBaseType is VtPtr or VtEmpty)
        {
            return VBStandardTypes.Object;
        }

        return ReadType(pointedType, owner, libraryName, types);
    }

    private static bool IsParameterByRef(TYPEDESC description) =>
        (description.vt & VariantByRef) != 0 ||
        (description.vt & VariantTypeMask) == VtPtr;

    private static TypeSymbol ReadType(
        TYPEDESC description,
        ITypeInfo owner,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        var vt = description.vt;
        var baseType = (short)(vt & VariantTypeMask);

        if ((vt & VariantArray) != 0)
        {
            // VT_ARRAY keeps its element VARTYPE in the same TYPEDESC. Preserve that
            // information so imported Automation members can use the existing VBArray<T>
            // managed contract instead of degrading every array to Object.
            var elementDescription = new TYPEDESC
            {
                vt = (short)(vt & ~VariantArray),
                lpValue = description.lpValue
            };
            return new ArrayTypeSymbol(
                ReadType(elementDescription, owner, libraryName, types));
        }

        if (baseType == VtSafeArray)
        {
            // VT_SAFEARRAY stores a nested element TYPEDESC in lpValue. C arrays use a
            // different ARRAYDESC layout and remain opaque until their native ABI is modeled.
            if (description.lpValue == IntPtr.Zero)
            {
                return VBStandardTypes.Object;
            }

            var elementDescription = Marshal.PtrToStructure<TYPEDESC>(description.lpValue);
            return new ArrayTypeSymbol(
                ReadType(elementDescription, owner, libraryName, types));
        }

        if (baseType == VtCArray)
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
            VtI4 or VtInt or VtUInt => TypeSymbol.Long,
            VtI8 => TypeSymbol.LongLong,

            // VB6 has no unsigned types, and its own importer maps these to the signed VB6 type.
            // Byte is the exception -- it really is the unsigned 8-bit VB6 type. The wider ones
            // must not arrive as this projects modern extensions: stdole.GUID.Data1 would then
            // answer VarType 20, which a VB6 program reads as vbLongLong, and "value = 2000 * 365"
            // style range checks would shift with it. Extensions are additive; they never change
            // what legacy code sees.
            VtUi1 => TypeSymbol.Byte,
            VtUi2 => TypeSymbol.Integer,
            VtUi4 => TypeSymbol.Long,
            VtUi8 => TypeSymbol.LongLong,
            VtR4 => TypeSymbol.Single,
            VtR8 => TypeSymbol.Double,
            VtBool => TypeSymbol.Boolean,
            VtCurrency => TypeSymbol.Currency,
            VtDate => TypeSymbol.Date,
            VtBstr => TypeSymbol.String,
            VtDispatch or VtUnknown => VBStandardTypes.Object,
            VtUserDefined => ReadReferencedType(description, owner, libraryName, types),
            _ => TypeSymbol.Variant
        };
    }

    private static TypeSymbol ReadReferencedType(
        TYPEDESC description,
        ITypeInfo owner,
        string libraryName,
        IReadOnlyDictionary<string, TypeSymbol> types)
    {
        if (description.lpValue == IntPtr.Zero)
        {
            return VBStandardTypes.Object;
        }

        try
        {
            // TYPEDESC.lpValue stores HREFTYPE directly for VT_USERDEFINED. It is not a
            // pointer to an HREFTYPE, unlike several other TYPEDESC variants.
            var referenceHandle = unchecked((int)description.lpValue.ToInt64());
            owner.GetRefTypeInfo(referenceHandle, out var referencedTypeInfo);
            referencedTypeInfo.GetDocumentation(-1, out var referencedName, out _, out _, out _);
            if (!string.IsNullOrWhiteSpace(referencedName))
            {
                if (types.TryGetValue(QualifiedName(libraryName, referencedName), out var qualifiedType))
                {
                    return qualifiedType;
                }

                if (types.TryGetValue(referencedName, out var unqualifiedType))
                {
                    return unqualifiedType;
                }
            }
        }
        catch (COMException)
        {
            // Unknown referenced records remain object-shaped until their ABI is modeled.
        }

        return VBStandardTypes.Object;
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

internal sealed record VBTypeLibraryImportResult(
    ImmutableDictionary<string, TypeSymbol> Aliases,
    ImmutableDictionary<string, long> QualifiedEnumMembers,
    ImmutableArray<BoundModuleVariable> Constants)
{
    public static VBTypeLibraryImportResult Empty { get; } = new(
        ImmutableDictionary<string, TypeSymbol>.Empty,
        ImmutableDictionary<string, long>.Empty,
        ImmutableArray<BoundModuleVariable>.Empty);
}
