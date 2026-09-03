using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Globalization;
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
    private const ushort VariantEmpty = 0x0000;
    private const ushort VariantNull = 0x0001;
    private const ushort VariantVariant = 0x000C;
    private const ushort VariantDate = 0x0007;
    private const ushort VariantCurrency = 0x0006;
    private const ushort VariantError = 0x000A;
    private const int ParameterNotFound = unchecked((int)0x80020004);
    private const ushort VariantDispatch = 0x0009;
    private const ushort VariantUnknown = 0x000D;
    private const ushort VariantPointer = 0x001A;
    private const ushort VariantSafeArray = 0x001B;
    private const int DispIdPropertyPut = -3;
    private const int DispatchException = unchecked((int)0x80020009);
    private const int FacilityControlMask = unchecked((int)0x800A0000);
    private const int AutomationErrorNumber = 440;
    /// <summary>
    /// sizeof(VARIANT). Sixteen bytes on x86, twenty-four on x64 -- the union carries BRECORD,
    /// which is two pointers. Hard-coding sixteen made every argument after the first overlap the
    /// one before it on x64, so a call with two or more arguments reached the server as garbage
    /// and the standard proxy rejected it with RPC_X_NULL_REF_POINTER before it ever ran. Nothing
    /// looked broken from outside: the reflection fallback answered instead, only without the
    /// server error numbers.
    /// </summary>
    private static readonly int VariantSize = IntPtr.Size == 8 ? 24 : 16;
    private const int VariantDataOffset = 8;
    private const uint InvariantComLocaleId = 1033;

    internal static uint ComLocaleId =>
        CultureInfo.CurrentCulture.LCID is 0 or 127
            ? InvariantComLocaleId
            : unchecked((uint)CultureInfo.CurrentCulture.LCID);

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

            // Named arguments cannot be resolved when the call is compiled, so they arrive
            // wrapped. GetIDsOfNames resolves the member and its parameter names in one call --
            // the same mechanism VB6 uses -- and the result decides both the DISPPARAMS layout
            // and the order the values have to be written in.
            if (!TrySplitNamedArguments(arguments, setProperty, out var callArguments, out var argumentNames))
            {
                return false;
            }

            int dispId;
            int[]? namedDispIds = null;
            if (argumentNames.Length == 0)
            {
                if (!TryGetDispId(dispatch, memberName, out dispId))
                {
                    return false;
                }
            }
            else if (!TryGetDispIds(dispatch, memberName, argumentNames, out dispId, out namedDispIds))
            {
                // VB6 answers a name the target does not know with 448.
                VBErrors.Raise(
                    448,
                    memberName,
                    "Named argument not found",
                    string.Empty,
                    0);
                return false;
            }

            arguments = callArguments;

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
                namedDispIds,
                out result,
                out var error);
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
                    namedDispIds,
                    out result,
                    out error);
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
                    namedDispIds,
                    out result,
                    out error);
            }

            // Reported only once every call shape has been tried. A FACILITY_CONTROL answer is
            // not proof that the server ran: Scripting.Dictionary.Add rejects the ByRef shape its
            // own type library describes with 0x800A0005, and the ByVal retry then succeeds.
            // Reporting on the first attempt would turn that working fallback into error 5.
            RaiseComException(hr, error);

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
            dispatch.GetTypeInfo(0, ComLocaleId, out var typeInfoPointer) < 0 ||
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
            dispatch.GetTypeInfo(0, ComLocaleId, out var typeInfoPointer) < 0 ||
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
            dispatch.GetTypeInfo(0, ComLocaleId, out var typeInfoPointer) < 0 ||
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

    /// <summary>
    /// Separates the named arguments from the positional ones and puts the list into the order
    /// DISPPARAMS wants. <c>Invoke</c> writes rgvarg back to front, and rgvarg has to start with
    /// the named values in the order of rgdispidNamedArgs -- so the named values go last here, in
    /// reverse. A property put keeps its value at the very end, where the existing
    /// DISPID_PROPERTYPUT handling expects it.
    /// </summary>
    private static bool TrySplitNamedArguments(
        object?[] arguments,
        bool setProperty,
        out object?[] callArguments,
        out string[] argumentNames)
    {
        callArguments = arguments;
        argumentNames = Array.Empty<string>();
        var count = setProperty ? arguments.Length - 1 : arguments.Length;
        if (count < 0)
        {
            return true;
        }

        var hasNamed = false;
        for (var index = 0; index < count; index++)
        {
            if (arguments[index] is VBNamedArgument)
            {
                hasNamed = true;
                break;
            }
        }

        if (!hasNamed)
        {
            return true;
        }

        var positional = new List<object?>();
        var named = new List<VBNamedArgument>();
        for (var index = 0; index < count; index++)
        {
            if (arguments[index] is VBNamedArgument namedArgument)
            {
                named.Add(namedArgument);
            }
            else
            {
                positional.Add(arguments[index]);
            }
        }

        var ordered = new List<object?>(positional);
        for (var index = named.Count - 1; index >= 0; index--)
        {
            ordered.Add(named[index].Value);
        }

        if (setProperty)
        {
            ordered.Add(arguments[^1]);
        }

        callArguments = ordered.ToArray();
        argumentNames = named.Select(entry => entry.Name).ToArray();
        return true;
    }

    /// <summary>
    /// Resolves the member and its named parameters in one GetIDsOfNames call. Passing them
    /// together is what lets a server map the names against the member the call actually names,
    /// rather than against whatever else it exposes.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool TryGetDispIds(
        INativeDispatch dispatch,
        string memberName,
        string[] argumentNames,
        out int dispId,
        out int[]? namedDispIds)
    {
        dispId = 0;
        namedDispIds = null;
        var total = argumentNames.Length + 1;
        var pointers = new IntPtr[total];
        var names = Marshal.AllocCoTaskMem(IntPtr.Size * total);
        var ids = Marshal.AllocCoTaskMem(sizeof(int) * total);
        try
        {
            pointers[0] = Marshal.StringToCoTaskMemUni(memberName);
            for (var index = 0; index < argumentNames.Length; index++)
            {
                pointers[index + 1] = Marshal.StringToCoTaskMemUni(argumentNames[index]);
            }

            for (var index = 0; index < total; index++)
            {
                Marshal.WriteIntPtr(names, index * IntPtr.Size, pointers[index]);
            }

            var iid = Guid.Empty;
            if (dispatch.GetIDsOfNames(ref iid, names, (uint)total, ComLocaleId, ids) < 0)
            {
                return false;
            }

            var resolved = new int[total];
            Marshal.Copy(ids, resolved, 0, total);
            dispId = resolved[0];
            namedDispIds = resolved.Skip(1).ToArray();
            return true;
        }
        finally
        {
            foreach (var pointer in pointers)
            {
                if (pointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pointer);
                }
            }

            Marshal.FreeCoTaskMem(names);
            Marshal.FreeCoTaskMem(ids);
        }
    }

    private static bool TryGetDispId(
        INativeDispatch dispatch,
        string memberName,
        out int dispId)
    {
        var name = Marshal.StringToCoTaskMemUni(memberName);
        var names = Marshal.AllocCoTaskMem(IntPtr.Size);
        var ids = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            Marshal.WriteIntPtr(names, name);
            var iid = Guid.Empty;
            dispId = 0;
            if (dispatch.GetIDsOfNames(ref iid, names, 1, ComLocaleId, ids) < 0)
            {
                return false;
            }

            dispId = Marshal.ReadInt32(ids);
            return true;
        }
        finally
        {
            Marshal.FreeCoTaskMem(ids);
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
        int[]? namedDispIds,
        out object? result,
        out ComInvocationError? error)
    {
        result = null;
        error = null;
        var variants = arguments.Length == 0
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        var byRefValues = byRefArguments is null
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        // rgdispidNamedArgs is always allocated, even when there are no named arguments. An
        // in-apartment call tolerates a null there, but the standard IDispatch proxy -- which is
        // what an STA object called from an MTA thread goes through -- rejects it with
        // RPC_X_NULL_REF_POINTER before the server ever sees the call.
        var namedCount = (namedDispIds?.Length ?? 0) +
            (flags is DispatchPropertyPut or DispatchPropertyPutRef ? 1 : 0);
        var namedArguments = Marshal.AllocCoTaskMem(sizeof(int) * Math.Max(1, namedCount));
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
                else if (VBVariants.IsMissing(arguments[sourceIndex]))
                {
                    // VB6 leaves a gap in an argument list -- Foo(a, , c) -- as a VARIANT of type
                    // VT_ERROR carrying DISP_E_PARAMNOTFOUND. That is what tells the server the
                    // argument was not supplied, as opposed to being supplied as Empty. Passing
                    // the runtime marker instead reached the server as an object it could not
                    // read, and a mid-list gap failed where a trailing one worked.
                    Marshal.WriteInt16(variant, unchecked((short)VariantError));
                    Marshal.WriteInt32(variant, VariantDataOffset, ParameterNotFound);
                }
                else
                {
                    Marshal.GetNativeVariantForObject(
                        UnwrapComValue(arguments[sourceIndex]),
                        variant);
                }

                initialized[index] = true;
            }

            // DISPID_PROPERTYPUT comes first when there is one: its value is the last argument,
            // and Invoke writes rgvarg back to front, so it lands in rgvarg[0].
            var isPropertyPut = flags is DispatchPropertyPut or DispatchPropertyPutRef;
            var namedSlot = 0;
            if (isPropertyPut)
            {
                Marshal.WriteInt32(namedArguments, namedSlot++ * sizeof(int), DispIdPropertyPut);
            }

            if (namedDispIds is not null)
            {
                foreach (var namedDispId in namedDispIds)
                {
                    Marshal.WriteInt32(namedArguments, namedSlot++ * sizeof(int), namedDispId);
                }
            }

            if (namedSlot == 0)
            {
                Marshal.WriteInt32(namedArguments, 0);
            }

            var parameters = new NativeDispParams
            {
                Arguments = variants,
                NamedArguments = namedArguments,
                ArgumentCount = (uint)arguments.Length,
                NamedArgumentCount = (uint)namedSlot
            };
            var exception = default(NativeExcepInfo);
            try
            {
                var iid = Guid.Empty;
                var hr = dispatch.Invoke(
                    dispId,
                    ref iid,
                    ComLocaleId,
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
                        var updatedValue = (byRefArguments[sourceIndex] & VariantArray) != 0 &&
                                            (byRefArguments[sourceIndex] & VariantTypeMask) == VariantVariant &&
                                            TryReadVariantArrayFromNativeVariant(valueVariant, out var variantArray)
                            ? variantArray
                            : Marshal.GetObjectForNativeVariant(valueVariant);
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
                // IDispatch owns EXCEPINFO's BSTR fields only until Invoke returns. Read the
                // server's own error out of it before releasing them -- afterwards the strings
                // are gone and only the bare HRESULT would be left. Release happens even when
                // the call fails or ByRef result conversion throws.
                error = ReadComError(ref exception);
                ClearNativeExcepInfo(ref exception);
            }
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

    /// <summary>
    /// Reports a server-side failure as a VB6 error. Two HRESULTs say the server raised something
    /// itself rather than complaining about the call shape: DISP_E_EXCEPTION, and anything in
    /// FACILITY_CONTROL -- the range VB6 and Automation servers use for their own error numbers.
    /// <c>Scripting.Dictionary</c> answers a duplicate key with 0x800A01C9 straight from
    /// <c>Invoke</c> without filling EXCEPINFO at all, so keying only on DISP_E_EXCEPTION lost
    /// that error to the reflection fallback, where it arrived as a bare 5.
    ///
    /// Every other HRESULT describes the call shape and keeps its retries -- a wrong call shape
    /// is what the fallbacks exist to correct. Stopping on a server error matters for a second
    /// reason as well: the call already ran, and a retry would repeat its side effects.
    /// </summary>
    internal static void RaiseComException(int hr, ComInvocationError? error)
    {
        if (hr != DispatchException && !IsServerError(hr))
        {
            return;
        }

        // Without EXCEPINFO the HRESULT is all there is, and its low word carries the number.
        var raised = error ?? (hr == DispatchException
            ? new ComInvocationError(
                AutomationErrorNumber,
                string.Empty,
                "Automation error",
                string.Empty,
                0)
            : MapComException(0, hr, string.Empty, string.Empty, string.Empty, 0));
        VBErrors.Raise(
            raised.Number,
            raised.Source,
            raised.Description,
            raised.HelpFile,
            raised.HelpContext);
    }

    /// <summary>
    /// The server's own error, taken out of EXCEPINFO while its strings are still alive.
    /// </summary>
    internal sealed record ComInvocationError(
        int Number,
        string Source,
        string Description,
        string HelpFile,
        int HelpContext);

    [SupportedOSPlatform("windows")]
    private static ComInvocationError? ReadComError(ref NativeExcepInfo exception)
    {
        var source = ReadNativeBstr(exception.Source);
        var description = ReadNativeBstr(exception.Description);
        var helpFile = ReadNativeBstr(exception.HelpFile);
        if (exception.Code == 0 &&
            exception.Scode == 0 &&
            source.Length == 0 &&
            description.Length == 0 &&
            helpFile.Length == 0)
        {
            return null;
        }

        return MapComException(
            exception.Code,
            exception.Scode,
            source,
            description,
            helpFile,
            exception.HelpContext);
    }

    /// <summary>
    /// Maps an EXCEPINFO onto the VB6 <c>Err</c> object. EXCEPINFO carries the error in exactly
    /// one of two fields, so wCode wins whenever it is set. An scode in the FACILITY_CONTROL
    /// range is a VB6 error number that travelled over COM -- a server raising <c>Err.Raise 9</c>
    /// sends 0x800A0009, and the client has to see 9 again, not the raw HRESULT. Every other
    /// scode stays what it is: VB6 reports an automation error by its full negative HRESULT,
    /// which is what <c>vbObjectError</c> arithmetic in user code expects to find.
    /// </summary>
    internal static ComInvocationError MapComException(
        ushort code,
        int scode,
        string source,
        string description,
        string helpFile,
        uint helpContext)
    {
        var number = code != 0
            ? code
            : (scode & unchecked((int)0xFFFF0000)) == unchecked((int)0x800A0000)
                ? scode & 0xFFFF
                : scode;

        // A server may fail without describing why. VB6 still needs a number, and 440 is its
        // documented catch-all for an automation error.
        if (number == 0)
        {
            number = AutomationErrorNumber;
        }

        // A server that answers through scode alone leaves no description behind. VB6 shows its
        // own text for a number it knows -- Scripting.Dictionary reports 457 without a word, and
        // VB6 still says "This key is already associated with an element of this collection".
        return new ComInvocationError(
            number,
            source,
            description.Length == 0 ? VBErrors.ErrorText(number) : description,
            helpFile,
            unchecked((int)helpContext));
    }

    /// <summary>
    /// FACILITY_CONTROL is where VB6 and Automation servers put their own error numbers. An
    /// HRESULT from that range is never a complaint about how the member was called.
    /// </summary>
    private static bool IsServerError(int hr) =>
        (hr & unchecked((int)0xFFFF0000)) == FacilityControlMask;

    private static string ReadNativeBstr(IntPtr value) =>
        value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringBSTR(value);

    internal static void ClearNativeExcepInfo(ref NativeExcepInfo exception)
    {
        FreeNativeBstr(ref exception.Source);
        FreeNativeBstr(ref exception.Description);
        FreeNativeBstr(ref exception.HelpFile);
        exception.DeferredFillIn = IntPtr.Zero;
    }

    private static void FreeNativeBstr(ref IntPtr value)
    {
        if (value == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeBSTR(value);
        value = IntPtr.Zero;
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
                if ((expectedType & VariantTypeMask) == VariantVariant)
                {
                    return TryInitializeVariantArray(vbArray, destination, expectedType);
                }

                if ((expectedType & VariantTypeMask) == VariantDispatch)
                {
                    return TryInitializeDispatchArray(vbArray, destination, expectedType);
                }

                if ((expectedType & VariantTypeMask) == VariantUnknown)
                {
                    return TryInitializeUnknownArray(vbArray, destination, expectedType);
                }

                if ((expectedType & VariantTypeMask) == VariantCurrency)
                {
                    return TryInitializeCurrencyArray(vbArray, destination, expectedType);
                }

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
            return TryInitializeVariantElement(value, destination);
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

    internal static void ClearNativeVariant(IntPtr variant) => _ = VariantClear(variant);

    [SupportedOSPlatform("windows")]
    private static bool TryInitializeVariantElement(object? value, IntPtr destination)
    {
        ClearVariantStorage(destination);
        if (value is null)
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantEmpty));
            return true;
        }

        if (VBVariants.IsNull(value))
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantNull));
            return true;
        }

        if (VBVariants.IsNothing(value))
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantDispatch));
            return true;
        }

        if (VBVariants.IsMissing(value))
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantError));
            Marshal.WriteInt32(destination, VariantDataOffset, unchecked((int)0x80020004));
            return true;
        }

        if (value is VBErrorValue error)
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantError));
            Marshal.WriteInt32(destination, VariantDataOffset, error.Code);
            return true;
        }

        if (value is VBDateValue date)
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantDate));
            Marshal.WriteInt64(destination, VariantDataOffset, BitConverter.DoubleToInt64Bits(date.OADate));
            return true;
        }

        if (value is VBCurrency currency)
        {
            Marshal.WriteInt16(destination, unchecked((short)VariantCurrency));
            Marshal.WriteInt64(destination, VariantDataOffset, currency.ScaledValue);
            return true;
        }

        try
        {
            Marshal.GetNativeVariantForObject(value, destination);
            return true;
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

    [SupportedOSPlatform("windows")]
    private static bool TryInitializeVariantArray(
        IVBArray source,
        IntPtr destination,
        ushort expectedType)
    {
        if (source.Rank < 1)
        {
            return false;
        }

        var bounds = new NativeSafeArrayBound[source.Rank];
        var lowerBounds = new int[source.Rank];
        var upperBounds = new int[source.Rank];
        var sourceLength = 1L;
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lowerBounds[dimension] = source.LBound(dimension + 1);
            upperBounds[dimension] = source.UBound(dimension + 1);
            var length = (long)upperBounds[dimension] - lowerBounds[dimension] + 1L;
            if (length < 0 || length > uint.MaxValue)
            {
                return false;
            }

            bounds[dimension] = new NativeSafeArrayBound((uint)length, lowerBounds[dimension]);
            if (length == 0)
            {
                sourceLength = 0;
            }
            else if (sourceLength != 0)
            {
                try
                {
                    sourceLength = checked(sourceLength * length);
                }
                catch (OverflowException)
                {
                    return false;
                }

                if (sourceLength > int.MaxValue)
                {
                    return false;
                }
            }
        }

        var safeArray = SafeArrayCreate(VariantVariant, (uint)source.Rank, bounds);
        if (safeArray == IntPtr.Zero)
        {
            return false;
        }

        var elementStorage = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            if (sourceLength != 0)
            {
                var indices = lowerBounds.ToArray();
                for (var offset = 0L; offset < sourceLength; offset++)
                {
                    if (!TryInitializeVariantElement(source.GetObjectValue(indices), elementStorage) ||
                        SafeArrayPutElement(safeArray, indices, elementStorage) < 0)
                    {
                        return false;
                    }

                    IncrementIndices(indices, lowerBounds, upperBounds);
                }
            }

            Marshal.WriteInt16(destination, unchecked((short)expectedType));
            Marshal.WriteIntPtr(destination, VariantDataOffset, safeArray);
            safeArray = IntPtr.Zero;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            _ = VariantClear(elementStorage);
            Marshal.FreeCoTaskMem(elementStorage);
            if (safeArray != IntPtr.Zero)
            {
                _ = SafeArrayDestroy(safeArray);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    internal static bool TryReadVariantArrayFromNativeVariant(
        IntPtr variant,
        out object? value)
    {
        value = null;
        var variantType = unchecked((ushort)Marshal.ReadInt16(variant));
        if ((variantType & (VariantArray | VariantTypeMask)) != (VariantArray | VariantVariant))
        {
            return false;
        }

        var safeArray = Marshal.ReadIntPtr(variant, VariantDataOffset);
        var rank = safeArray == IntPtr.Zero ? 0u : SafeArrayGetDim(safeArray);
        if (rank == 0 || rank > int.MaxValue)
        {
            return false;
        }

        var rankCount = (int)rank;
        var lengths = new int[rankCount];
        var lowerBounds = new int[rankCount];
        var upperBounds = new int[rankCount];
        for (var dimension = 0; dimension < rankCount; dimension++)
        {
            var nativeDimension = (uint)(dimension + 1);
            if (SafeArrayGetLBound(safeArray, nativeDimension, out var lowerBound) < 0 ||
                SafeArrayGetUBound(safeArray, nativeDimension, out var upperBound) < 0)
            {
                return false;
            }

            var length = (long)upperBound - lowerBound + 1L;
            if (length < 0 || length > int.MaxValue)
            {
                return false;
            }

            lengths[dimension] = (int)length;
            lowerBounds[dimension] = lowerBound;
            upperBounds[dimension] = upperBound;
        }

        var result = Array.CreateInstance(typeof(object), lengths, lowerBounds);
        var elementStorage = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            if (result.Length != 0)
            {
                var indices = lowerBounds.ToArray();
                for (var offset = 0; offset < result.Length; offset++)
                {
                    ClearVariantStorage(elementStorage);
                    if (SafeArrayGetElement(safeArray, indices, elementStorage) < 0)
                    {
                        return false;
                    }

                    result.SetValue(ReadVariantElement(elementStorage), indices);
                    IncrementIndices(indices, lowerBounds, upperBounds);
                }
            }

            value = result;
            return true;
        }
        finally
        {
            _ = VariantClear(elementStorage);
            Marshal.FreeCoTaskMem(elementStorage);
        }
    }

    [SupportedOSPlatform("windows")]
    private static object? ReadVariantElement(IntPtr variant)
    {
        var type = unchecked((ushort)Marshal.ReadInt16(variant)) & VariantTypeMask;
        return type switch
        {
            VariantEmpty => null,
            VariantNull => VBVariants.NullValue(),
            VariantError => ReadVariantError(variant),
            VariantDate => new VBDateValue(BitConverter.Int64BitsToDouble(Marshal.ReadInt64(variant, VariantDataOffset))),
            VariantCurrency => VBCurrency.FromScaled(Marshal.ReadInt64(variant, VariantDataOffset)),
            VariantDispatch or VariantUnknown when Marshal.ReadIntPtr(variant, VariantDataOffset) == IntPtr.Zero =>
                VBVariants.NothingValue(),
            _ => Marshal.GetObjectForNativeVariant(variant)
        };
    }

    private static object ReadVariantError(IntPtr variant)
    {
        var code = Marshal.ReadInt32(variant, VariantDataOffset);
        return code == unchecked((int)0x80020004)
            ? VBVariants.MissingValue()
            : new VBErrorValue(code);
    }

    private static bool TryInitializeCurrencyArray(
        IVBArray source,
        IntPtr destination,
        ushort expectedType)
    {
        if (source.Rank < 1)
        {
            return false;
        }

        var bounds = new NativeSafeArrayBound[source.Rank];
        var lowerBounds = new int[source.Rank];
        var upperBounds = new int[source.Rank];
        var sourceLength = 1L;
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lowerBounds[dimension] = source.LBound(dimension + 1);
            upperBounds[dimension] = source.UBound(dimension + 1);
            var length = (long)upperBounds[dimension] - lowerBounds[dimension] + 1L;
            if (length < 0 || length > uint.MaxValue)
            {
                return false;
            }

            bounds[dimension] = new NativeSafeArrayBound((uint)length, lowerBounds[dimension]);
            if (length == 0)
            {
                sourceLength = 0;
            }
            else if (sourceLength != 0)
            {
                sourceLength = checked(sourceLength * length);
                if (sourceLength > int.MaxValue)
                {
                    return false;
                }
            }
        }

        var safeArray = SafeArrayCreate(
            VariantCurrency,
            (uint)source.Rank,
            bounds);
        if (safeArray == IntPtr.Zero)
        {
            return false;
        }

        var valueStorage = Marshal.AllocCoTaskMem(sizeof(long));
        try
        {
            if (sourceLength != 0)
            {
                var indices = lowerBounds.ToArray();
                for (var offset = 0L; offset < sourceLength; offset++)
                {
                    var value = source.GetObjectValue(indices);
                    var currency = value is VBCurrency typed
                        ? typed
                        : VBConversions.CDec(value) is decimal decimalValue
                            ? VBCurrency.FromDecimal(decimalValue)
                            : throw new InvalidCastException("Currency SAFEARRAY element is not numeric.");
                    Marshal.WriteInt64(valueStorage, currency.ScaledValue);
                    if (SafeArrayPutElement(safeArray, indices, valueStorage) < 0)
                    {
                        return false;
                    }

                    IncrementIndices(indices, lowerBounds, upperBounds);
                }
            }

            Marshal.WriteInt16(destination, unchecked((short)expectedType));
            Marshal.WriteIntPtr(destination, VariantDataOffset, safeArray);
            safeArray = IntPtr.Zero;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        finally
        {
            Marshal.FreeCoTaskMem(valueStorage);
            if (safeArray != IntPtr.Zero)
            {
                _ = SafeArrayDestroy(safeArray);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryInitializeDispatchArray(
        IVBArray source,
        IntPtr destination,
        ushort expectedType)
    {
        if (source.Rank < 1)
        {
            return false;
        }

        var bounds = new NativeSafeArrayBound[source.Rank];
        var lowerBounds = new int[source.Rank];
        var upperBounds = new int[source.Rank];
        var sourceLength = 1L;
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lowerBounds[dimension] = source.LBound(dimension + 1);
            upperBounds[dimension] = source.UBound(dimension + 1);
            var length = (long)upperBounds[dimension] - lowerBounds[dimension] + 1L;
            if (length < 0 || length > uint.MaxValue)
            {
                return false;
            }

            bounds[dimension] = new NativeSafeArrayBound((uint)length, lowerBounds[dimension]);
            if (length == 0)
            {
                sourceLength = 0;
            }
            else if (sourceLength != 0)
            {
                sourceLength = checked(sourceLength * length);
                if (sourceLength > int.MaxValue)
                {
                    return false;
                }
            }
        }

        var safeArray = SafeArrayCreate(
            VariantDispatch,
            (uint)source.Rank,
            bounds);
        if (safeArray == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (sourceLength != 0)
            {
                var indices = lowerBounds.ToArray();
                for (var offset = 0L; offset < sourceLength; offset++)
                {
                    var value = source.GetObjectValue(indices);
                    var dispatchPointer = IntPtr.Zero;
                    try
                    {
                        var comValue = UnwrapComValue(value);
                        if (comValue is null ||
                            comValue is DBNull ||
                            comValue is System.Reflection.Missing ||
                            VBVariants.IsNull(comValue) ||
                            VBVariants.IsNothing(comValue))
                        {
                            IncrementIndices(indices, lowerBounds, upperBounds);
                            continue;
                        }
                        dispatchPointer = Marshal.GetIDispatchForObject(comValue);
                        if (dispatchPointer == IntPtr.Zero)
                        {
                            return false;
                        }

                        // VT_DISPATCH elements are passed as the interface pointer itself; the
                        // SafeArray API does not expect an additional pointer indirection.
                        if (SafeArrayPutElement(safeArray, indices, dispatchPointer) < 0)
                        {
                            return false;
                        }
                    }
                    finally
                    {
                        if (dispatchPointer != IntPtr.Zero)
                        {
                            Marshal.Release(dispatchPointer);
                        }
                    }

                    IncrementIndices(indices, lowerBounds, upperBounds);
                }
            }

            Marshal.WriteInt16(destination, unchecked((short)expectedType));
            Marshal.WriteIntPtr(destination, VariantDataOffset, safeArray);
            safeArray = IntPtr.Zero;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        finally
        {
            if (safeArray != IntPtr.Zero)
            {
                _ = SafeArrayDestroy(safeArray);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryInitializeUnknownArray(
        IVBArray source,
        IntPtr destination,
        ushort expectedType)
    {
        if (source.Rank < 1)
        {
            return false;
        }

        var bounds = new NativeSafeArrayBound[source.Rank];
        var lowerBounds = new int[source.Rank];
        var upperBounds = new int[source.Rank];
        var sourceLength = 1L;
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lowerBounds[dimension] = source.LBound(dimension + 1);
            upperBounds[dimension] = source.UBound(dimension + 1);
            var length = (long)upperBounds[dimension] - lowerBounds[dimension] + 1L;
            if (length < 0 || length > uint.MaxValue)
            {
                return false;
            }

            bounds[dimension] = new NativeSafeArrayBound((uint)length, lowerBounds[dimension]);
            if (length == 0)
            {
                sourceLength = 0;
            }
            else if (sourceLength != 0)
            {
                sourceLength = checked(sourceLength * length);
                if (sourceLength > int.MaxValue)
                {
                    return false;
                }
            }
        }

        var safeArray = SafeArrayCreate(
            VariantUnknown,
            (uint)source.Rank,
            bounds);
        if (safeArray == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (sourceLength != 0)
            {
                var indices = lowerBounds.ToArray();
                for (var offset = 0L; offset < sourceLength; offset++)
                {
                    var value = source.GetObjectValue(indices);
                    var unknownPointer = IntPtr.Zero;
                    try
                    {
                        var comValue = UnwrapComValue(value);
                        if (comValue is not null &&
                            comValue is not DBNull &&
                            comValue is not System.Reflection.Missing &&
                            !VBVariants.IsNull(comValue) &&
                            !VBVariants.IsNothing(comValue))
                        {
                            unknownPointer = Marshal.GetIUnknownForObject(comValue);
                            if (unknownPointer == IntPtr.Zero ||
                                SafeArrayPutElement(safeArray, indices, unknownPointer) < 0)
                            {
                                return false;
                            }
                        }
                    }
                    finally
                    {
                        if (unknownPointer != IntPtr.Zero)
                        {
                            Marshal.Release(unknownPointer);
                        }
                    }

                    IncrementIndices(indices, lowerBounds, upperBounds);
                }
            }

            Marshal.WriteInt16(destination, unchecked((short)expectedType));
            Marshal.WriteIntPtr(destination, VariantDataOffset, safeArray);
            safeArray = IntPtr.Zero;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        finally
        {
            if (safeArray != IntPtr.Zero)
            {
                _ = SafeArrayDestroy(safeArray);
            }
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

    internal static object? NormalizeDispatchArray(object? value)
    {
        if (value is not Array source)
        {
            return value;
        }

        var lengths = new int[source.Rank];
        var lowerBounds = new int[source.Rank];
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lengths[dimension] = source.GetLength(dimension);
            lowerBounds[dimension] = source.GetLowerBound(dimension);
        }

        var result = Array.CreateInstance(typeof(object), lengths, lowerBounds);
        if (source.Length == 0)
        {
            return result;
        }

        var upperBounds = new int[source.Rank];
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            upperBounds[dimension] = source.GetUpperBound(dimension);
        }

        var indices = lowerBounds.ToArray();
        for (var offset = 0; offset < source.Length; offset++)
        {
            var element = source.GetValue(indices);
            result.SetValue(element ?? VBVariants.NothingValue(), indices);
            IncrementIndices(indices, lowerBounds, upperBounds);
        }

        return result;
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

        if (targetType == typeof(DateTime) && value is double oleDate)
        {
            converted = DateTime.FromOADate(oleDate);
            return true;
        }

        if (targetType == typeof(int) && value is IntPtr pointer)
        {
            try
            {
                converted = checked((int)pointer.ToInt64());
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        if (targetType == typeof(long) && value is IntPtr longPointer)
        {
            converted = longPointer.ToInt64();
            return true;
        }

        if (targetType == typeof(IntPtr))
        {
            try
            {
                converted = new IntPtr(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
            {
                return false;
            }
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
        var empty = new byte[VariantSize];
        Marshal.Copy(empty, 0, variant, VariantSize);
    }

    private static void CopyVariant(IntPtr source, IntPtr destination)
    {
        var bytes = new byte[VariantSize];
        Marshal.Copy(source, bytes, 0, VariantSize);
        Marshal.Copy(bytes, 0, destination, VariantSize);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSafeArrayBound
    {
        public NativeSafeArrayBound(uint elementCount, int lowerBound)
        {
            ElementCount = elementCount;
            LowerBound = lowerBound;
        }

        public readonly uint ElementCount;
        public readonly int LowerBound;
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
    private static extern IntPtr SafeArrayCreate(
        ushort variantType,
        uint dimensionCount,
        [In] NativeSafeArrayBound[] bounds);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayPutElement(
        IntPtr safeArray,
        int[] indices,
        IntPtr value);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayGetElement(
        IntPtr safeArray,
        int[] indices,
        IntPtr value);

    [DllImport("oleaut32.dll")]
    private static extern uint SafeArrayGetDim(IntPtr safeArray);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayGetLBound(
        IntPtr safeArray,
        uint dimension,
        out int lowerBound);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayGetUBound(
        IntPtr safeArray,
        uint dimension,
        out int upperBound);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayDestroy(IntPtr safeArray);

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

        // rgDispId is an array: one entry for the member plus one per named parameter. Declaring
        // it as a single out int would work for the one-name case and corrupt the stack for any
        // other, so it stays a raw pointer the caller sizes.
        [PreserveSig]
        int GetIDsOfNames(
            ref Guid iid,
            IntPtr names,
            uint nameCount,
            uint lcid,
            IntPtr dispIds);

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

    /// <summary>
    /// EXCEPINFO as OAIDL declares it. The <c>pvReserved</c> slot between the help context and
    /// the deferred fill-in callback is easy to leave out and shifts everything after it: without
    /// it, <c>Scode</c> reads the first half of a function pointer instead of the error code, so
    /// a server that reports through scode rather than wCode looks like it reported nothing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeExcepInfo
    {
        public ushort Code;
        public ushort Reserved;
        public IntPtr Source;
        public IntPtr Description;
        public IntPtr HelpFile;
        public uint HelpContext;
        public IntPtr ReservedPointer;
        public IntPtr DeferredFillIn;
        public int Scode;
    }
}
