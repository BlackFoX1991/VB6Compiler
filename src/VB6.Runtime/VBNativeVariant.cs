namespace VB6.Runtime;

using System.Runtime.InteropServices;

/// <summary>
/// Converts between a VB6 Variant value and the native <c>VARIANT</c> a stored <c>VarPtr</c>
/// hands out.
///
/// This is the one place that decides what a Variant *looks like* to native code, and the whole
/// point of the contract is that the subtype survives. In managed terms Empty, Null and Nothing
/// are all "no value" -- a null reference and two marker singletons -- but they are three
/// different VARIANTs, and a native reader has to be able to tell them apart. So does an Error
/// value, which VB6 keeps distinct from the number it carries.
///
/// The mapping is written out instead of delegated to <see cref="Marshal.GetNativeVariantForObject"/>:
/// that method knows nothing about the runtime's markers and would collapse the three empty-ish
/// states into <c>VT_EMPTY</c>.
///
/// The VARTYPE codes here are the **OLE** ones, which is not always what <c>VarType</c> reports.
/// VB6 has no unsigned 32-bit Variant, so <c>VarType</c> answers 20 for a <see cref="uint"/>; the
/// descriptor still has to say <c>VT_UI4</c> (19), because that is what the payload is. Reading
/// back maps the OLE code to the same CLR type, so <c>VarType</c> is unchanged across the trip.
/// </summary>
internal static class VBNativeVariant
{
    private const ushort VtEmpty = 0;
    private const ushort VtNull = 1;
    private const ushort VtI2 = 2;
    private const ushort VtI4 = 3;
    private const ushort VtR4 = 4;
    private const ushort VtR8 = 5;
    private const ushort VtCy = 6;
    private const ushort VtDate = 7;
    private const ushort VtBstr = 8;
    private const ushort VtDispatch = 9;
    private const ushort VtError = 10;
    private const ushort VtBool = 11;
    private const ushort VtDecimal = 14;
    private const ushort VtI1 = 16;
    private const ushort VtUi1 = 17;
    private const ushort VtUi2 = 18;
    private const ushort VtUi4 = 19;
    private const ushort VtI8 = 20;
    private const ushort VtUi8 = 21;

    /// <summary>The HRESULT VB6 stores for an omitted Optional argument.</summary>
    private const int ParameterNotFound = unchecked((int)0x80020004);

    [DllImport("oleaut32.dll")]
    internal static extern int VariantClear(IntPtr variant);

    internal static void Write(IntPtr variant, int dataOffset, object? value)
    {
        switch (value)
        {
            case null:
                WriteType(variant, VtEmpty);
                return;
            case string text:
                // Die BSTR gehoert ab hier dem VARIANT; VariantClear gibt sie wieder frei.
                WriteType(variant, VtBstr);
                Marshal.WriteIntPtr(variant, dataOffset, Marshal.StringToBSTR(text));
                return;
            case bool flag:
                WriteType(variant, VtBool);
                Marshal.WriteInt16(variant, dataOffset, flag ? (short)-1 : (short)0);
                return;
            case short int16:
                WriteType(variant, VtI2);
                Marshal.WriteInt16(variant, dataOffset, int16);
                return;
            case int int32:
                WriteType(variant, VtI4);
                Marshal.WriteInt32(variant, dataOffset, int32);
                return;
            case long int64:
                WriteType(variant, VtI8);
                Marshal.WriteInt64(variant, dataOffset, int64);
                return;
            case byte value8:
                WriteType(variant, VtUi1);
                Marshal.WriteByte(variant, dataOffset, value8);
                return;
            case sbyte signed8:
                WriteType(variant, VtI1);
                Marshal.WriteByte(variant, dataOffset, unchecked((byte)signed8));
                return;
            case ushort unsigned16:
                WriteType(variant, VtUi2);
                Marshal.WriteInt16(variant, dataOffset, unchecked((short)unsigned16));
                return;
            case uint unsigned32:
                WriteType(variant, VtUi4);
                Marshal.WriteInt32(variant, dataOffset, unchecked((int)unsigned32));
                return;
            case ulong unsigned64:
                WriteType(variant, VtUi8);
                Marshal.WriteInt64(variant, dataOffset, unchecked((long)unsigned64));
                return;
            case float single:
                WriteType(variant, VtR4);
                Marshal.WriteInt32(variant, dataOffset, BitConverter.SingleToInt32Bits(single));
                return;
            case double dbl:
                WriteType(variant, VtR8);
                Marshal.WriteInt64(variant, dataOffset, BitConverter.DoubleToInt64Bits(dbl));
                return;
            case VBCurrency currency:
                WriteType(variant, VtCy);
                Marshal.WriteInt64(variant, dataOffset, currency.ScaledValue);
                return;
            case VBDateValue date:
                WriteType(variant, VtDate);
                Marshal.WriteInt64(variant, dataOffset, BitConverter.DoubleToInt64Bits(date.OADate));
                return;
            case DateTime dateTime:
                WriteType(variant, VtDate);
                Marshal.WriteInt64(variant, dataOffset, BitConverter.DoubleToInt64Bits(dateTime.ToOADate()));
                return;
            case VBErrorValue error:
                WriteType(variant, VtError);
                Marshal.WriteInt32(variant, dataOffset, error.Code);
                return;
            case decimal number:
                WriteDecimal(variant, number);
                return;
            case IntPtr pointer:
                // LongPtr traegt die native Breite und damit auch ihren VARTYPE.
                if (IntPtr.Size == sizeof(long))
                {
                    WriteType(variant, VtI8);
                    Marshal.WriteInt64(variant, dataOffset, pointer.ToInt64());
                }
                else
                {
                    WriteType(variant, VtI4);
                    Marshal.WriteInt32(variant, dataOffset, pointer.ToInt32());
                }

                return;
        }

        if (ReferenceEquals(value, VBVariants.NullValue()))
        {
            WriteType(variant, VtNull);
            return;
        }

        if (ReferenceEquals(value, VBVariants.NothingValue()))
        {
            // Nothing ist ein Objektzeiger, der auf nichts zeigt -- nicht Empty.
            WriteType(variant, VtDispatch);
            Marshal.WriteIntPtr(variant, dataOffset, IntPtr.Zero);
            return;
        }

        if (ReferenceEquals(value, VBVariants.MissingValue()))
        {
            WriteType(variant, VtError);
            Marshal.WriteInt32(variant, dataOffset, ParameterNotFound);
            return;
        }

        // Objekte, Arrays und UDT-Werte besitzen Speicher neben dem VARIANT. Der ist ein eigener
        // Vertrag, und ein halb gefuellter Deskriptor waere schlechter als die Meldung.
        throw new NotSupportedException(
            $"A VB6 Variant holding '{value.GetType().Name}' has no stored native VARIANT layout yet.");
    }

    internal static object? Read(IntPtr variant, int dataOffset)
    {
        var type = unchecked((ushort)Marshal.ReadInt16(variant));
        return type switch
        {
            VtEmpty => null,
            VtNull => VBVariants.NullValue(),
            VtI2 => Marshal.ReadInt16(variant, dataOffset),
            VtI4 => Marshal.ReadInt32(variant, dataOffset),
            VtI8 => Marshal.ReadInt64(variant, dataOffset),
            VtUi1 => Marshal.ReadByte(variant, dataOffset),
            VtI1 => unchecked((sbyte)Marshal.ReadByte(variant, dataOffset)),
            VtUi2 => unchecked((ushort)Marshal.ReadInt16(variant, dataOffset)),
            VtUi4 => unchecked((uint)Marshal.ReadInt32(variant, dataOffset)),
            VtUi8 => unchecked((ulong)Marshal.ReadInt64(variant, dataOffset)),
            VtR4 => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(variant, dataOffset)),
            VtR8 => BitConverter.Int64BitsToDouble(Marshal.ReadInt64(variant, dataOffset)),
            VtCy => VBCurrency.FromScaled(Marshal.ReadInt64(variant, dataOffset)),
            VtDate => new VBDateValue(BitConverter.Int64BitsToDouble(Marshal.ReadInt64(variant, dataOffset))),
            VtBool => Marshal.ReadInt16(variant, dataOffset) != 0,
            VtBstr => ReadBstr(variant, dataOffset),
            VtDispatch or 13 => ReadInterface(variant, dataOffset),
            VtError => ReadError(variant, dataOffset),
            VtDecimal => ReadDecimal(variant),
            _ => throw new NotSupportedException(
                $"A native VARIANT of type {type} has no VB6 Variant representation yet.")
        };
    }

    private static object? ReadBstr(IntPtr variant, int dataOffset)
    {
        var text = Marshal.ReadIntPtr(variant, dataOffset);
        return text == IntPtr.Zero ? string.Empty : Marshal.PtrToStringBSTR(text);
    }

    private static object ReadInterface(IntPtr variant, int dataOffset)
    {
        var pointer = Marshal.ReadIntPtr(variant, dataOffset);
        if (pointer == IntPtr.Zero)
        {
            return VBVariants.NothingValue();
        }

        throw new NotSupportedException(
            "A native VARIANT carrying a live interface pointer has no VB6 Variant representation yet.");
    }

    private static object ReadError(IntPtr variant, int dataOffset)
    {
        var code = Marshal.ReadInt32(variant, dataOffset);
        return code == ParameterNotFound ? VBVariants.MissingValue() : new VBErrorValue(code);
    }

    /// <summary>
    /// A <c>VT_DECIMAL</c> is the one subtype that does not live in the union: the DECIMAL
    /// overlays the whole VARIANT, and its first reserved word is the <c>vt</c> field itself.
    /// </summary>
    private static void WriteDecimal(IntPtr variant, decimal value)
    {
        var parts = decimal.GetBits(value);
        var flags = parts[3];
        WriteType(variant, VtDecimal);
        Marshal.WriteByte(variant, 2, unchecked((byte)((flags >> 16) & 0xFF)));
        Marshal.WriteByte(variant, 3, unchecked((byte)((flags >> 24) & 0xFF)));
        Marshal.WriteInt32(variant, 4, parts[2]);
        Marshal.WriteInt32(variant, 8, parts[0]);
        Marshal.WriteInt32(variant, 12, parts[1]);
    }

    private static decimal ReadDecimal(IntPtr variant)
    {
        var scale = Marshal.ReadByte(variant, 2);
        var sign = Marshal.ReadByte(variant, 3);
        return new decimal(
            Marshal.ReadInt32(variant, 8),
            Marshal.ReadInt32(variant, 12),
            Marshal.ReadInt32(variant, 4),
            (sign & 0x80) != 0,
            scale);
    }

    private static void WriteType(IntPtr variant, ushort type) =>
        Marshal.WriteInt16(variant, unchecked((short)type));
}
