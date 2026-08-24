using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VB6.Runtime;

/// <summary>
/// Small IDispatch ABI bridge used before CLR reflection dispatch. Older OCX collections can
/// expose automation objects whose RCW metadata is unsafe for Type.InvokeMember, while the
/// underlying IDispatch contract remains callable with the native VARIANT layout.
/// </summary>
internal static class VBComDispatch
{
    private const ushort DispatchMethod = 0x1;
    private const ushort DispatchPropertyGet = 0x2;
    private const ushort DispatchPropertyPut = 0x4;
    private const ushort DispatchPropertyPutRef = 0x8;
    private const ushort VariantByRef = 0x4000;
    private const ushort VariantArray = 0x2000;
    private const ushort VariantTypeMask = 0x0FFF;
    private const ushort VariantVariant = 0x000C;
    private const ushort VariantDate = 0x0007;
    private const ushort VariantCurrency = 0x0006;
    private const ushort VariantPointer = 0x001A;
    private const ushort VariantSafeArray = 0x001B;
    private const int DispIdPropertyPut = -3;
    private const int VariantSize = 16;
    private const int VariantDataOffset = 8;

    public static bool TryInvoke(
        object target,
        string memberName,
        object?[] arguments,
        bool setProperty,
        out object? result)
    {
        result = null;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!Marshal.IsComObject(target))
        {
            return false;
        }

        try
        {
            var dispatch = (INativeDispatch)target;
            if (!TryGetDispId(dispatch, memberName, out var dispId))
            {
                return false;
            }

            var flags = setProperty
                ? ShouldUsePropertyPutRef(arguments)
                    ? DispatchPropertyPutRef
                    : DispatchPropertyPut
                : (ushort)(DispatchMethod | DispatchPropertyGet);
            var argumentTypes = TryGetByValArrayArgumentTypes(
                dispatch,
                dispId,
                arguments.Length);
            var byRefArguments = setProperty
                ? null
                : TryGetByRefArgumentTypes(dispatch, dispId, arguments.Length);
            var hr = Invoke(
                dispatch,
                dispId,
                arguments,
                flags,
                argumentTypes,
                byRefArguments,
                out result);
            if (hr < 0 && byRefArguments is not null)
            {
                // A type library can describe an [out] parameter while an individual server
                // still requires its legacy ByVal call shape. Preserve the safe fallback.
                hr = Invoke(
                    dispatch,
                    dispId,
                    arguments,
                    flags,
                    argumentTypes,
                    null,
                    out result);
            }
            if (hr < 0 && setProperty)
            {
                // Automation servers disagree on whether object-valued properties require
                // PROPERTYPUT or PROPERTYPUTREF. Retry with the other contract before the
                // reflection fallback gets a chance to touch an old OCX RCW.
                var fallbackFlags = flags == DispatchPropertyPutRef
                    ? DispatchPropertyPut
                    : DispatchPropertyPutRef;
                hr = Invoke(
                    dispatch,
                    dispId,
                    arguments,
                    fallbackFlags,
                    argumentTypes,
                    null,
                    out result);
            }

            return hr >= 0;
        }
        catch (COMException)
        {
            result = null;
            return false;
        }
        catch (InvalidCastException)
        {
            result = null;
            return false;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
        catch (NotSupportedException)
        {
            result = null;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    internal static bool TryGetComEventIdentity(
        object target,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        var comTarget = target is IVBComObjectProvider provider
            ? provider.ComObject
            : target;
        if (!OperatingSystem.IsWindows() ||
            comTarget is null ||
            !Marshal.IsComObject(comTarget) ||
            string.IsNullOrWhiteSpace(eventName))
        {
            return false;
        }

        try
        {
            var dispatch = (INativeDispatch)comTarget;
            if (TryGetComEventIdentity(dispatch, eventName, out interfaceId, out dispId))
            {
                return true;
            }

            // Older ActiveX controls expose their coclass metadata through IProvideClassInfo
            // while returning no usable ITypeInfo from IDispatch::GetTypeInfo.
            if (TryGetComEventIdentityFromClassInfo(comTarget, eventName, out interfaceId, out dispId))
            {
                return true;
            }

            return target is IVBComTypeInfoProvider typeInfoProvider &&
                TryGetComEventIdentityFromRegisteredTypeLibrary(
                    typeInfoProvider.ComClassId,
                    eventName,
                    out interfaceId,
                    out dispId);
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetComEventIdentityFromClassInfo(
        object target,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        if (target is not IProvideClassInfo provider ||
            provider.GetClassInfo(out var typeInfo) < 0 ||
            typeInfo is null)
        {
            return false;
        }

        try
        {
            return TryFindEventInTypeInfo(typeInfo, eventName, out interfaceId, out dispId);
        }
        finally
        {
            if (Marshal.IsComObject(typeInfo))
            {
                _ = Marshal.ReleaseComObject(typeInfo);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetComEventIdentityFromRegisteredTypeLibrary(
        Guid classId,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        if (classId == Guid.Empty)
        {
            return false;
        }

        using var classKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{classId:B}");
        using var typeLibraryKey = classKey?.OpenSubKey("TypeLib");
        using var versionKey = classKey?.OpenSubKey("Version");
        var typeLibraryValue = typeLibraryKey?.GetValue(null) as string;
        var versionValue = versionKey?.GetValue(null) as string;
        if (!Guid.TryParse(typeLibraryValue, out var typeLibraryId) ||
            !Version.TryParse(versionValue, out var version) ||
            version.Major < 0 ||
            version.Minor < 0 ||
            LoadRegTypeLib(
                ref typeLibraryId,
                (ushort)version.Major,
                (ushort)version.Minor,
                0,
                out var typeLibrary) < 0 ||
            typeLibrary is null)
        {
            return false;
        }

        try
        {
            for (var index = 0; index < typeLibrary.GetTypeInfoCount(); index++)
            {
                typeLibrary.GetTypeInfoType(index, out var typeKind);
                if (typeKind != TYPEKIND.TKIND_COCLASS)
                {
                    continue;
                }

                typeLibrary.GetTypeInfo(index, out var typeInfo);
                try
                {
                    typeInfo.GetTypeAttr(out var typeAttributePointer);
                    try
                    {
                        var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
                        if (typeAttribute.guid == classId &&
                            TryFindEventInTypeInfo(typeInfo, eventName, out interfaceId, out dispId))
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        typeInfo.ReleaseTypeAttr(typeAttributePointer);
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(typeInfo))
                    {
                        _ = Marshal.ReleaseComObject(typeInfo);
                    }
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(typeLibrary))
            {
                _ = Marshal.ReleaseComObject(typeLibrary);
            }
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetComEventIdentity(
        INativeDispatch dispatch,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        if (dispatch.GetTypeInfoCount(out var typeInfoCount) < 0 ||
            typeInfoCount == 0 ||
            dispatch.GetTypeInfo(0, 1033, out var typeInfoPointer) < 0 ||
            typeInfoPointer == IntPtr.Zero)
        {
            return false;
        }

        ITypeInfo? typeInfo = null;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo = (ITypeInfo)Marshal.GetObjectForIUnknown(typeInfoPointer);
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            for (var implementationIndex = 0;
                 implementationIndex < typeAttribute.cImplTypes;
                 implementationIndex++)
            {
                typeInfo.GetImplTypeFlags(implementationIndex, out var flags);
                if ((flags & IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE) == 0)
                {
                    continue;
                }

                typeInfo.GetRefTypeOfImplType(implementationIndex, out var referenceHandle);
                typeInfo.GetRefTypeInfo(referenceHandle, out var sourceTypeInfo);
                try
                {
                    if (TryFindEventInTypeInfo(
                        sourceTypeInfo,
                        eventName,
                        out interfaceId,
                        out dispId))
                    {
                        return true;
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(sourceTypeInfo))
                    {
                        _ = Marshal.ReleaseComObject(sourceTypeInfo);
                    }
                }
            }

            // Some automation servers return the source dispatch interface directly rather
            // than a coclass TYPEATTR. Accept that shape as a final metadata fallback.
            return TryFindEventInTypeInfo(typeInfo, eventName, out interfaceId, out dispId);
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero && typeInfo is not null)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }

            if (typeInfo is not null && Marshal.IsComObject(typeInfo))
            {
                _ = Marshal.ReleaseComObject(typeInfo);
            }

            _ = Marshal.Release(typeInfoPointer);
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryFindEventInTypeInfo(
        ITypeInfo typeInfo,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            if (typeAttribute.guid == Guid.Empty)
            {
                return false;
            }

            for (var implementationIndex = 0;
                 implementationIndex < typeAttribute.cImplTypes;
                 implementationIndex++)
            {
                typeInfo.GetImplTypeFlags(implementationIndex, out var flags);
                if ((flags & IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE) == 0)
                {
                    continue;
                }

                typeInfo.GetRefTypeOfImplType(implementationIndex, out var referenceHandle);
                typeInfo.GetRefTypeInfo(referenceHandle, out var sourceTypeInfo);
                try
                {
                    if (TryFindEventInTypeInfo(
                        sourceTypeInfo,
                        eventName,
                        out interfaceId,
                        out dispId))
                    {
                        return true;
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(sourceTypeInfo))
                    {
                        _ = Marshal.ReleaseComObject(sourceTypeInfo);
                    }
                }
            }

            for (var functionIndex = 0; functionIndex < typeAttribute.cFuncs; functionIndex++)
            {
                typeInfo.GetFuncDesc(functionIndex, out var functionPointer);
                try
                {
                    var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                    var names = new string[1];
                    typeInfo.GetNames(function.memid, names, names.Length, out var nameCount);
                    if (nameCount > 0 &&
                        string.Equals(names[0], eventName, StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceId = typeAttribute.guid;
                        dispId = function.memid;
                        return true;
                    }
                }
                finally
                {
                    typeInfo.ReleaseFuncDesc(functionPointer);
                }
            }
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }
        }

        return false;
    }

    private static bool ShouldUsePropertyPutRef(object?[] arguments) =>
        arguments.Length > 0 &&
        UnwrapComValue(arguments[^1]) is { } value &&
        Marshal.IsComObject(value);

    private static object? UnwrapComValue(object? value) =>
        value is IVBComObjectProvider provider && provider.ComObject is { } comObject
            ? comObject
            : value;

    [SupportedOSPlatform("windows")]
    private static ushort?[]? TryGetByValArrayArgumentTypes(
        INativeDispatch dispatch,
        int dispId,
        int argumentCount)
    {
        if (argumentCount == 0 ||
            dispatch.GetTypeInfoCount(out var typeInfoCount) < 0 ||
            typeInfoCount == 0 ||
            dispatch.GetTypeInfo(0, 1033, out var typeInfoPointer) < 0 ||
            typeInfoPointer == IntPtr.Zero)
        {
            return null;
        }

        ITypeInfo? typeInfo = null;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo = (ITypeInfo)Marshal.GetObjectForIUnknown(typeInfoPointer);
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            var elementSize = Marshal.SizeOf<ELEMDESC>();
            for (var functionIndex = 0; functionIndex < typeAttribute.cFuncs; functionIndex++)
            {
                typeInfo.GetFuncDesc(functionIndex, out var functionPointer);
                try
                {
                    var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                    if (function.memid != dispId ||
                        function.cParams < argumentCount ||
                        function.lprgelemdescParam == IntPtr.Zero)
                    {
                        continue;
                    }

                    var types = new ushort?[argumentCount];
                    for (var parameterIndex = 0; parameterIndex < argumentCount; parameterIndex++)
                    {
                        var elementPointer = IntPtr.Add(
                            function.lprgelemdescParam,
                            parameterIndex * elementSize);
                        var element = Marshal.PtrToStructure<ELEMDESC>(elementPointer);
                        if (!TryGetVariantType(element.tdesc, out var variantType))
                        {
                            continue;
                        }

                        if ((variantType & VariantArray) == 0 ||
                            (element.desc.paramdesc.wParamFlags & PARAMFLAG.PARAMFLAG_FOUT) != 0 ||
                            (element.tdesc.vt & VariantByRef) != 0 ||
                            (element.tdesc.vt & VariantTypeMask) == VariantPointer)
                        {
                            continue;
                        }

                        types[parameterIndex] = variantType;
                    }

                    return types.Any(value => value is not null) ? types : null;
                }
                finally
                {
                    typeInfo.ReleaseFuncDesc(functionPointer);
                }
            }
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero && typeInfo is not null)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }

            if (typeInfo is not null && Marshal.IsComObject(typeInfo))
            {
                _ = Marshal.ReleaseComObject(typeInfo);
            }

            _ = Marshal.Release(typeInfoPointer);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static ushort?[]? TryGetByRefArgumentTypes(
        INativeDispatch dispatch,
        int dispId,
        int argumentCount)
    {
        if (argumentCount == 0 ||
            dispatch.GetTypeInfoCount(out var typeInfoCount) < 0 ||
            typeInfoCount == 0 ||
            dispatch.GetTypeInfo(0, 1033, out var typeInfoPointer) < 0 ||
            typeInfoPointer == IntPtr.Zero)
        {
            return null;
        }

        ITypeInfo? typeInfo = null;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo = (ITypeInfo)Marshal.GetObjectForIUnknown(typeInfoPointer);
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            var elementSize = Marshal.SizeOf<ELEMDESC>();
            for (var functionIndex = 0; functionIndex < typeAttribute.cFuncs; functionIndex++)
            {
                typeInfo.GetFuncDesc(functionIndex, out var functionPointer);
                try
                {
                    var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                    if (function.memid != dispId ||
                        function.cParams < argumentCount ||
                        function.lprgelemdescParam == IntPtr.Zero)
                    {
                        continue;
                    }

                    var types = new ushort?[argumentCount];
                    for (var parameterIndex = 0; parameterIndex < argumentCount; parameterIndex++)
                    {
                        var elementPointer = IntPtr.Add(
                            function.lprgelemdescParam,
                            parameterIndex * elementSize);
                        var element = Marshal.PtrToStructure<ELEMDESC>(elementPointer);
                        if ((element.desc.paramdesc.wParamFlags & PARAMFLAG.PARAMFLAG_FOUT) == 0 ||
                            !TryGetVariantType(element.tdesc, out var variantType))
                        {
                            continue;
                        }

                        types[parameterIndex] = variantType;
                    }

                    return types.Any(value => value is not null) ? types : null;
                }
                finally
                {
                    typeInfo.ReleaseFuncDesc(functionPointer);
                }
            }
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero && typeInfo is not null)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }

            if (typeInfo is not null && Marshal.IsComObject(typeInfo))
            {
                _ = Marshal.ReleaseComObject(typeInfo);
            }

            _ = Marshal.Release(typeInfoPointer);
        }

        return null;
    }

    private static bool TryGetDispId(
        INativeDispatch dispatch,
        string memberName,
        out int dispId)
    {
        var name = Marshal.StringToCoTaskMemUni(memberName);
        var names = Marshal.AllocCoTaskMem(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(names, name);
            var iid = Guid.Empty;
            dispId = 0;
            return dispatch.GetIDsOfNames(
                ref iid,
                names,
                1,
                1033,
                out dispId) >= 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(names);
            Marshal.FreeCoTaskMem(name);
        }
    }

    [SupportedOSPlatform("windows")]
    private static int Invoke(
        INativeDispatch dispatch,
        int dispId,
        object?[] arguments,
        ushort flags,
        ushort?[]? argumentTypes,
        ushort?[]? byRefArguments,
        out object? result)
    {
        result = null;
        var variants = arguments.Length == 0
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        var byRefValues = byRefArguments is null
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        var namedArguments = flags is DispatchPropertyPut or DispatchPropertyPutRef
            ? Marshal.AllocCoTaskMem(sizeof(int))
            : IntPtr.Zero;
        var initialized = new bool[arguments.Length];

        try
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                var variant = IntPtr.Add(
                    variants,
                    index * VariantSize);
                var sourceIndex = arguments.Length - index - 1;
                if (byRefArguments?[sourceIndex] is { } byRefType)
                {
                    var valueVariant = IntPtr.Add(
                        byRefValues,
                        index * VariantSize);
                    if (!TryInitializeVariant(
                            UnwrapComValue(arguments[sourceIndex]),
                            valueVariant,
                            byRefType))
                    {
                        _ = VariantClear(valueVariant);
                        return unchecked((int)0x80070057); // E_INVALIDARG; caller retries ByVal.
                    }

                    Marshal.WriteInt16(
                        variant,
                        (short)(VariantByRef | byRefType));
                    var byRefStorage = byRefType == VariantVariant
                        ? valueVariant
                        : IntPtr.Add(valueVariant, VariantDataOffset);
                    Marshal.WriteIntPtr(variant, VariantDataOffset, byRefStorage);
                }
                else if (argumentTypes?[sourceIndex] is { } argumentType &&
                         (argumentType & VariantArray) != 0)
                {
                    if (!TryInitializeVariant(
                            UnwrapComValue(arguments[sourceIndex]),
                            variant,
                            argumentType))
                    {
                        _ = VariantClear(variant);
                        return unchecked((int)0x80070057); // E_INVALIDARG.
                    }
                }
                else
                {
                    Marshal.GetNativeVariantForObject(
                        UnwrapComValue(arguments[sourceIndex]),
                        variant);
                }

                initialized[index] = true;
            }

            if (namedArguments != IntPtr.Zero)
            {
                Marshal.WriteInt32(namedArguments, DispIdPropertyPut);
            }

            var parameters = new NativeDispParams
            {
                Arguments = variants,
                NamedArguments = namedArguments,
                ArgumentCount = (uint)arguments.Length,
                NamedArgumentCount = namedArguments == IntPtr.Zero ? 0u : 1u
            };
            var exception = default(NativeExcepInfo);
            var iid = Guid.Empty;
            var hr = dispatch.Invoke(
                dispId,
                ref iid,
                1033,
                flags,
                ref parameters,
                out result,
                out exception,
                out _);
            if (hr >= 0 && byRefArguments is not null)
            {
                for (var index = 0; index < arguments.Length; index++)
                {
                    var sourceIndex = arguments.Length - index - 1;
                    if (byRefArguments[sourceIndex] is null)
                    {
                        continue;
                    }

                    var valueVariant = IntPtr.Add(
                        byRefValues,
                        index * VariantSize);
                    var updatedValue = Marshal.GetObjectForNativeVariant(valueVariant);
                    if (!TryCopyArrayBack(
                            arguments[sourceIndex],
                            updatedValue))
                    {
                        arguments[sourceIndex] = updatedValue;
                    }
                }
            }

            return hr;
        }
        finally
        {
            for (var index = 0; index < initialized.Length; index++)
            {
                if (!initialized[index])
                {
                    continue;
                }

                var sourceIndex = arguments.Length - index - 1;
                var values = byRefArguments?[sourceIndex] is not null
                    ? byRefValues
                    : variants;
                _ = VariantClear(IntPtr.Add(values, index * VariantSize));
            }

            if (namedArguments != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(namedArguments);
            }

            if (variants != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(variants);
            }

            if (byRefValues != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(byRefValues);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetVariantType(
        TYPEDESC description,
        out ushort variantType)
    {
        var type = unchecked((ushort)description.vt);
        var baseType = (ushort)(type & VariantTypeMask);
        var arrayFlags = (ushort)(type & VariantArray);
        if (baseType == VariantPointer)
        {
            if (description.lpValue == IntPtr.Zero)
            {
                variantType = 0;
                return false;
            }

            var pointedType = Marshal.PtrToStructure<TYPEDESC>(description.lpValue);
            return TryGetVariantType(pointedType, out variantType);
        }

        if (baseType == VariantSafeArray)
        {
            if (description.lpValue == IntPtr.Zero)
            {
                variantType = 0;
                return false;
            }

            var elementType = Marshal.PtrToStructure<TYPEDESC>(description.lpValue);
            if (!TryGetVariantType(elementType, out var elementVariantType) ||
                (elementVariantType & VariantArray) != 0)
            {
                variantType = 0;
                return false;
            }

            variantType = (ushort)(VariantArray | elementVariantType);
            return true;
        }

        if (baseType == 0 || baseType is 0x000E or 0x001C or 0x001D or 0x0024)
        {
            variantType = 0;
            return false;
        }

        if (!IsSupportedByRefVariantType(baseType))
        {
            variantType = 0;
            return false;
        }

        variantType = (ushort)(arrayFlags | baseType);
        return true;
    }

    private static bool IsSupportedByRefVariantType(ushort baseType) =>
        baseType is 0x0001 or // EMPTY
            0x0002 or // I2
            0x0003 or // I4
            0x0004 or // R4
            0x0005 or // R8
            0x0006 or // CY
            0x0007 or // DATE
            0x0008 or // BSTR
            0x0009 or // DISPATCH
            0x000A or // ERROR
            0x000B or // BOOL
            0x000C or // VARIANT
            0x000D or // UNKNOWN
            0x0010 or // I1
            0x0011 or // UI1
            0x0012 or // UI2
            0x0013 or // UI4
            0x0014 or // I8
            0x0015 or // UI8
            0x0016 or // INT
            0x0017;   // UINT

    [SupportedOSPlatform("windows")]
    internal static bool TryInitializeVariant(
        object? value,
        IntPtr destination,
        ushort expectedType)
    {
        ClearVariantStorage(destination);
        if ((expectedType & VariantArray) != 0)
        {
            if (value is IVBArray vbArray)
            {
                if (!TryCreateAutomationArray(vbArray, expectedType, out var automationArray))
                {
                    return false;
                }

                value = automationArray;
            }
            else if (value is not Array)
            {
                return false;
            }

            try
            {
                Marshal.GetNativeVariantForObject(value, destination);
                var actualType = unchecked((ushort)Marshal.ReadInt16(destination)) &
                                  (ushort)(VariantTypeMask | VariantArray);
                return actualType == expectedType;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        if (expectedType == VariantVariant)
        {
            Marshal.GetNativeVariantForObject(value, destination);
            return true;
        }

        if (value is null || value is DBNull || value is System.Reflection.Missing)
        {
            Marshal.WriteInt16(destination, (short)expectedType);
            return true;
        }

        if (expectedType == VariantDate && value is VBDateValue date)
        {
            Marshal.WriteInt16(destination, (short)VariantDate);
            Marshal.WriteInt64(destination, VariantDataOffset, BitConverter.DoubleToInt64Bits(date.OADate));
            return true;
        }

        if (expectedType == VariantCurrency && value is VBCurrency currency)
        {
            Marshal.WriteInt16(destination, (short)VariantCurrency);
            Marshal.WriteInt64(destination, VariantDataOffset, currency.ScaledValue);
            return true;
        }

        var source = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            ClearVariantStorage(source);
            Marshal.GetNativeVariantForObject(value, source);
            var actualType = unchecked((ushort)Marshal.ReadInt16(source)) &
                              (ushort)(VariantTypeMask | VariantArray);
            if (actualType == expectedType)
            {
                CopyVariant(source, destination);
                return true;
            }

            return VariantChangeType(destination, source, 0, expectedType) >= 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        finally
        {
            _ = VariantClear(source);
            Marshal.FreeCoTaskMem(source);
        }
    }

    internal static bool TryCreateAutomationArray(
        IVBArray source,
        ushort expectedType,
        out Array? result)
    {
        result = null;
        var elementType = GetAutomationElementType(expectedType);
        if (elementType is null || source.Rank < 1)
        {
            return false;
        }

        var lengths = new int[source.Rank];
        var lowerBounds = new int[source.Rank];
        var upperBounds = new int[source.Rank];
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lowerBounds[dimension] = source.LBound(dimension + 1);
            upperBounds[dimension] = source.UBound(dimension + 1);
            var length = (long)upperBounds[dimension] - lowerBounds[dimension] + 1L;
            if (length < 0 || length > int.MaxValue)
            {
                return false;
            }

            lengths[dimension] = (int)length;
        }

        try
        {
            result = Array.CreateInstance(elementType, lengths, lowerBounds);
            if (result.Length == 0)
            {
                return true;
            }

            var indices = lowerBounds.ToArray();
            for (var offset = 0; offset < result.Length; offset++)
            {
                var sourceValue = source.GetObjectValue(indices);
                if (!TryConvertAutomationElement(sourceValue, elementType, out var convertedValue))
                {
                    result = null;
                    return false;
                }

                result.SetValue(convertedValue, indices);
                IncrementIndices(indices, lowerBounds, upperBounds);
            }

            return true;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            result = null;
            return false;
        }
        catch (OverflowException)
        {
            result = null;
            return false;
        }
    }

    private static Type? GetAutomationElementType(ushort expectedType)
    {
        return (ushort)(expectedType & VariantTypeMask) switch
        {
            0x0001 or 0x000C or 0x000D => typeof(object),
            0x0002 => typeof(short),
            0x0003 => typeof(int),
            0x0004 => typeof(float),
            0x0005 => typeof(double),
            0x0006 => typeof(decimal),
            0x0007 => typeof(DateTime),
            0x0008 => typeof(string),
            0x0009 => typeof(object),
            0x000A => typeof(int),
            0x000B => typeof(bool),
            0x0010 => typeof(sbyte),
            0x0011 => typeof(byte),
            0x0012 => typeof(ushort),
            0x0013 => typeof(uint),
            0x0014 => typeof(long),
            0x0015 => typeof(ulong),
            0x0016 => typeof(int),
            0x0017 => typeof(uint),
            _ => null
        };
    }

    private static bool TryConvertAutomationElement(
        object? value,
        Type targetType,
        out object? converted)
    {
        converted = null;
        if (value is null || value is DBNull || value is System.Reflection.Missing)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
            {
                converted = Activator.CreateInstance(targetType);
            }

            return true;
        }

        if (targetType == typeof(object))
        {
            converted = value switch
            {
                VBDateValue date => DateTime.FromOADate(date.OADate),
                VBCurrency currency => currency.ToDecimal(),
                _ => value
            };
            return true;
        }

        if (targetType == typeof(DateTime) && value is VBDateValue dateValue)
        {
            converted = DateTime.FromOADate(dateValue.OADate);
            return true;
        }

        if (targetType == typeof(decimal) && value is VBCurrency currencyValue)
        {
            converted = currencyValue.ToDecimal();
            return true;
        }

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        try
        {
            converted = Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static void IncrementIndices(int[] indices, int[] lowerBounds, int[] upperBounds)
    {
        for (var dimension = indices.Length - 1; dimension >= 0; dimension--)
        {
            if (indices[dimension] < upperBounds[dimension])
            {
                indices[dimension]++;
                return;
            }

            indices[dimension] = lowerBounds[dimension];
        }
    }

    internal static bool TryCopyArrayBack(object? original, object? updated)
    {
        if (original is not IVBArray target || updated is not Array source ||
            target.Rank != source.Rank)
        {
            return false;
        }

        var lowerBounds = new int[target.Rank];
        var upperBounds = new int[target.Rank];
        for (var dimension = 0; dimension < target.Rank; dimension++)
        {
            lowerBounds[dimension] = target.LBound(dimension + 1);
            upperBounds[dimension] = target.UBound(dimension + 1);
            if (lowerBounds[dimension] != source.GetLowerBound(dimension) ||
                upperBounds[dimension] != source.GetUpperBound(dimension))
            {
                return false;
            }
        }

        if (source.Length == 0)
        {
            return true;
        }

        var indices = lowerBounds.ToArray();
        for (var offset = 0; offset < source.Length; offset++)
        {
            target.SetObjectValue(indices, source.GetValue(indices));
            IncrementIndices(indices, lowerBounds, upperBounds);
        }

        return true;
    }

    private static void ClearVariantStorage(IntPtr variant)
    {
        Span<byte> empty = stackalloc byte[VariantSize];
        Marshal.Copy(empty.ToArray(), 0, variant, VariantSize);
    }

    private static void CopyVariant(IntPtr source, IntPtr destination)
    {
        var bytes = new byte[VariantSize];
        Marshal.Copy(source, bytes, 0, VariantSize);
        Marshal.Copy(bytes, 0, destination, VariantSize);
    }

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr variant);

    [DllImport("oleaut32.dll")]
    private static extern int VariantChangeType(
        IntPtr destination,
        IntPtr source,
        ushort flags,
        ushort variantType);

    [DllImport("oleaut32.dll")]
    private static extern int LoadRegTypeLib(
        ref Guid typeLibraryId,
        ushort majorVersion,
        ushort minorVersion,
        int lcid,
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib? typeLibrary);

    [ComImport]
    [Guid("00020400-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeDispatch
    {
        [PreserveSig]
        int GetTypeInfoCount(out uint typeInfoCount);

        [PreserveSig]
        int GetTypeInfo(uint typeInfoIndex, uint lcid, out IntPtr typeInfo);

        [PreserveSig]
        int GetIDsOfNames(
            ref Guid iid,
            IntPtr names,
            uint nameCount,
            uint lcid,
            out int dispId);

        [PreserveSig]
        int Invoke(
            int dispId,
            ref Guid iid,
            uint lcid,
            ushort flags,
            ref NativeDispParams parameters,
            [MarshalAs(UnmanagedType.Struct)] out object? result,
            out NativeExcepInfo exception,
            out uint argumentError);
    }

    [ComImport]
    [Guid("B196B283-BAB4-101A-B69C-00AA00341D07")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProvideClassInfo
    {
        [PreserveSig]
        int GetClassInfo([MarshalAs(UnmanagedType.Interface)] out ITypeInfo typeInfo);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDispParams
    {
        public IntPtr Arguments;
        public IntPtr NamedArguments;
        public uint ArgumentCount;
        public uint NamedArgumentCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeExcepInfo
    {
        public ushort Code;
        public ushort Reserved;
        public IntPtr Source;
        public IntPtr Description;
        public IntPtr HelpFile;
        public uint HelpContext;
        public IntPtr DeferredFillIn;
        public int Scode;
    }
}
