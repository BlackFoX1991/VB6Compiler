using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Nodes;

namespace VB6.Compiler;

/// <summary>
/// VB6 language intrinsics made visible to the binder as ordinary procedures. Symbols carry a
/// backend-independent intrinsic identity. The legacy runtime-target string remains populated only
/// while the C# backend is still present during the parity cutover.
/// </summary>
internal static class VBIntrinsicSymbols
{
    private static readonly ImmutableArray<ProcedureSymbol> Intrinsics = ImmutableArray.Create(
        Function("Len", VBIntrinsicKind.Len, "VBStrings.Len", TypeSymbol.Variant, Parameter("Expression", TypeSymbol.Variant)),
        Function("LenB", VBIntrinsicKind.LenB, "VBStrings.LenB", TypeSymbol.Variant, Parameter("Expression", TypeSymbol.Variant)),
        Function(
            "Mid",
            VBIntrinsicKind.Mid,
            "VBStrings.Mid",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Start", TypeSymbol.Long),
            Parameter("Length", TypeSymbol.Long)) with { IntrinsicMinimumArguments = 2 },
        Function(
            "MidB",
            VBIntrinsicKind.MidB,
            "VBStrings.MidB",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Start", TypeSymbol.Long),
            Parameter("Length", TypeSymbol.Long)) with { IntrinsicMinimumArguments = 2 },
        Function("Chr", VBIntrinsicKind.Chr, "VBStrings.Chr", TypeSymbol.String, Parameter("CharCode", TypeSymbol.Long)),
        Function("ChrW", VBIntrinsicKind.ChrW, "VBStrings.ChrW", TypeSymbol.String, Parameter("CharCode", TypeSymbol.Long)),
        Function(
            "Left",
            VBIntrinsicKind.Left,
            "VBStrings.Left",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function(
            "LeftB",
            VBIntrinsicKind.LeftB,
            "VBStrings.LeftB",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function(
            "Right",
            VBIntrinsicKind.Right,
            "VBStrings.Right",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function(
            "RightB",
            VBIntrinsicKind.RightB,
            "VBStrings.RightB",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function("UCase", VBIntrinsicKind.UCase, "VBStrings.UCase", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("LCase", VBIntrinsicKind.LCase, "VBStrings.LCase", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("Trim", VBIntrinsicKind.Trim, "VBStrings.Trim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("LTrim", VBIntrinsicKind.LTrim, "VBStrings.LTrim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("RTrim", VBIntrinsicKind.RTrim, "VBStrings.RTrim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("Asc", VBIntrinsicKind.Asc, "VBStrings.Asc", TypeSymbol.Long, Parameter("Expression", TypeSymbol.String)),
        Function("AscW", VBIntrinsicKind.AscW, "VBStrings.AscW", TypeSymbol.Integer, Parameter("Expression", TypeSymbol.String)),
        Function("Val", VBIntrinsicKind.Val, "VBStrings.Val", TypeSymbol.Double, Parameter("String", TypeSymbol.String)),
        Function("Hex", VBIntrinsicKind.Hex, "VBStrings.Hex", TypeSymbol.String, Parameter("Number", TypeSymbol.Variant)),
        Function("Oct", VBIntrinsicKind.Oct, "VBStrings.Oct", TypeSymbol.Variant, Parameter("Number", TypeSymbol.Variant)),
        Function("Str", VBIntrinsicKind.Str, "VBStrings.Str", TypeSymbol.String, Parameter("Number", TypeSymbol.Variant)),
        Function("String", VBIntrinsicKind.String, "VBStrings.String", TypeSymbol.String, Parameter("Number", TypeSymbol.Long), Parameter("Character", TypeSymbol.Variant)),
        Function(
            "Format",
            VBIntrinsicKind.Format,
            "VBStrings.FormatValue",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.Variant),
            OptionalParameter("Format", TypeSymbol.String, string.Empty),
            OptionalParameter("FirstDayOfWeek", TypeSymbol.Long, 0L),
            OptionalParameter("FirstWeekOfYear", TypeSymbol.Long, 0L)),
        Function("IsNumeric", VBIntrinsicKind.IsNumeric, "VBStrings.IsNumeric", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("IsArray", VBIntrinsicKind.IsArray, "VBVariants.IsArray", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("IsDate", VBIntrinsicKind.IsDate, "VBVariants.IsDate", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("IsObject", VBIntrinsicKind.IsObject, "VBVariants.IsObject", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function(
            "InStr",
            VBIntrinsicKind.InStr,
            "VBStrings.InStr",
            TypeSymbol.Long,
            Parameter("Start", TypeSymbol.Long),
            Parameter("String1", TypeSymbol.String),
            Parameter("String2", TypeSymbol.String),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)) with { IntrinsicMinimumArguments = 2 },
        Function(
            "InStrB",
            VBIntrinsicKind.InStrB,
            "VBStrings.InStrB",
            TypeSymbol.Long,
            Parameter("Start", TypeSymbol.Long),
            Parameter("String1", TypeSymbol.String),
            Parameter("String2", TypeSymbol.String),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)) with { IntrinsicMinimumArguments = 2 },
        Function(
            "InStrRev",
            VBIntrinsicKind.InStrRev,
            "VBStrings.InStrRev",
            TypeSymbol.Long,
            Parameter("StringCheck", TypeSymbol.String),
            Parameter("StringMatch", TypeSymbol.String),
            OptionalParameter("Start", TypeSymbol.Long, -1L),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)),
        Function(
            "StrComp",
            VBIntrinsicKind.StrComp,
            "VBStrings.StrComp",
            TypeSymbol.Integer,
            Parameter("String1", TypeSymbol.String),
            Parameter("String2", TypeSymbol.String),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)),
        Function(
            "Replace",
            VBIntrinsicKind.Replace,
            "VBStrings.Replace",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Find", TypeSymbol.String),
            Parameter("Replace", TypeSymbol.String),
            OptionalParameter("Start", TypeSymbol.Long, 1L),
            OptionalParameter("Count", TypeSymbol.Long, -1L),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)),
        Function("Space", VBIntrinsicKind.Space, "VBStrings.Space", TypeSymbol.String, Parameter("Number", TypeSymbol.Long)),
        Function(
            "Split",
            VBIntrinsicKind.Split,
            "VBStrings.Split",
            new ArrayTypeSymbol(TypeSymbol.String),
            Parameter("Expression", TypeSymbol.String),
            OptionalParameter("Delimiter", TypeSymbol.String, " "),
            OptionalParameter("Limit", TypeSymbol.Long, -1L),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)),
        Function(
            "Join",
            VBIntrinsicKind.Join,
            "VBStrings.Join",
            TypeSymbol.String,
            Parameter("SourceArray", new ArrayTypeSymbol(TypeSymbol.String)),
            OptionalParameter("Delimiter", TypeSymbol.String, " ")),
        Function(
            "Filter",
            VBIntrinsicKind.Filter,
            "VBStrings.Filter",
            new ArrayTypeSymbol(TypeSymbol.String),
            Parameter("SourceArray", new ArrayTypeSymbol(TypeSymbol.String)),
            Parameter("Match", TypeSymbol.String),
            OptionalParameter("Include", TypeSymbol.Boolean, true),
            OptionalParameter("Compare", TypeSymbol.Long, 0L)),
        Function(
            "StrConv",
            VBIntrinsicKind.StrConv,
            "VBStrings.StrConv",
            TypeSymbol.String,
            Parameter("String", TypeSymbol.String),
            Parameter("Conversion", TypeSymbol.Long),
            OptionalParameter("LCID", TypeSymbol.Long, 0L)),
        Function("Abs", VBIntrinsicKind.Abs, "VBMath.Abs", TypeSymbol.Variant, Parameter("Number", TypeSymbol.Variant)),
        Function("Sgn", VBIntrinsicKind.Sgn, "VBMath.Sgn", TypeSymbol.Variant, Parameter("Number", TypeSymbol.Variant)),
        Function("Fix", VBIntrinsicKind.Fix, "VBMath.Fix", TypeSymbol.Variant, Parameter("Number", TypeSymbol.Variant)),
        Function(
            "Round",
            VBIntrinsicKind.Round,
            "VBMath.Round",
            TypeSymbol.Variant,
            Parameter("Number", TypeSymbol.Variant),
            OptionalParameter("NumDigitsAfterDecimal", TypeSymbol.Integer, (short)0)),
        Function("Sqr", VBIntrinsicKind.Sqr, "VBMath.Sqr", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function("Exp", VBIntrinsicKind.Exp, "VBMath.Exp", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function("Log", VBIntrinsicKind.Log, "VBMath.Log", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function("Sin", VBIntrinsicKind.Sin, "VBMath.Sin", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function("Cos", VBIntrinsicKind.Cos, "VBMath.Cos", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function("Tan", VBIntrinsicKind.Tan, "VBMath.Tan", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function("Atn", VBIntrinsicKind.Atn, "VBMath.Atn", TypeSymbol.Double, Parameter("Number", TypeSymbol.Double)),
        Function(
            "FV",
            VBIntrinsicKind.FV,
            "VBFinancial.FV",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            Parameter("NPer", TypeSymbol.Double),
            Parameter("Pmt", TypeSymbol.Double),
            OptionalParameter("PV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d)),
        Function(
            "PV",
            VBIntrinsicKind.PV,
            "VBFinancial.PV",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            Parameter("NPer", TypeSymbol.Double),
            Parameter("Pmt", TypeSymbol.Double),
            OptionalParameter("FV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d)),
        Function(
            "PMT",
            VBIntrinsicKind.PMT,
            "VBFinancial.PMT",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            Parameter("NPer", TypeSymbol.Double),
            Parameter("PV", TypeSymbol.Double),
            OptionalParameter("FV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d)),
        Function(
            "IPMT",
            VBIntrinsicKind.IPMT,
            "VBFinancial.IPMT",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            Parameter("Per", TypeSymbol.Double),
            Parameter("NPer", TypeSymbol.Double),
            Parameter("PV", TypeSymbol.Double),
            OptionalParameter("FV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d)),
        Function(
            "PPMT",
            VBIntrinsicKind.PPMT,
            "VBFinancial.PPMT",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            Parameter("Per", TypeSymbol.Double),
            Parameter("NPer", TypeSymbol.Double),
            Parameter("PV", TypeSymbol.Double),
            OptionalParameter("FV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d)),
        Function(
            "NPER",
            VBIntrinsicKind.NPER,
            "VBFinancial.NPER",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            Parameter("Pmt", TypeSymbol.Double),
            Parameter("PV", TypeSymbol.Double),
            OptionalParameter("FV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d)),
        Function(
            "RATE",
            VBIntrinsicKind.RATE,
            "VBFinancial.RATE",
            TypeSymbol.Double,
            Parameter("NPer", TypeSymbol.Double),
            Parameter("Pmt", TypeSymbol.Double),
            Parameter("PV", TypeSymbol.Double),
            OptionalParameter("FV", TypeSymbol.Double, 0d),
            OptionalParameter("Type", TypeSymbol.Double, 0d),
            OptionalParameter("Guess", TypeSymbol.Double, 0.1d)),
        Function(
            "NPV",
            VBIntrinsicKind.NPV,
            "VBFinancial.NPV",
            TypeSymbol.Double,
            Parameter("Rate", TypeSymbol.Double),
            new ParameterSymbol("Values", new ArrayTypeSymbol(TypeSymbol.Variant), ParameterPassingMode.ByVal)
            {
                IsParamArray = true
            }),
        Function(
            "IRR",
            VBIntrinsicKind.IRR,
            "VBFinancial.IRR",
            TypeSymbol.Double,
            Parameter("Values", new ArrayTypeSymbol(TypeSymbol.Variant)),
            OptionalParameter("Guess", TypeSymbol.Double, 0.1d)),
        Function(
            "MIRR",
            VBIntrinsicKind.MIRR,
            "VBFinancial.MIRR",
            TypeSymbol.Double,
            Parameter("Values", new ArrayTypeSymbol(TypeSymbol.Variant)),
            Parameter("FinanceRate", TypeSymbol.Double),
            Parameter("ReinvestRate", TypeSymbol.Double)),
        Function(
            "SLN",
            VBIntrinsicKind.SLN,
            "VBFinancial.SLN",
            TypeSymbol.Double,
            Parameter("Cost", TypeSymbol.Double),
            Parameter("Salvage", TypeSymbol.Double),
            Parameter("Life", TypeSymbol.Double)),
        Function(
            "SYD",
            VBIntrinsicKind.SYD,
            "VBFinancial.SYD",
            TypeSymbol.Double,
            Parameter("Cost", TypeSymbol.Double),
            Parameter("Salvage", TypeSymbol.Double),
            Parameter("Life", TypeSymbol.Double),
            Parameter("Period", TypeSymbol.Double)),
        Function(
            "DDB",
            VBIntrinsicKind.DDB,
            "VBFinancial.DDB",
            TypeSymbol.Double,
            Parameter("Cost", TypeSymbol.Double),
            Parameter("Salvage", TypeSymbol.Double),
            Parameter("Life", TypeSymbol.Double),
            Parameter("Period", TypeSymbol.Double),
            OptionalParameter("Factor", TypeSymbol.Double, 2d)),
        Function(
            "Rnd",
            VBIntrinsicKind.Rnd,
            "VBMath.Rnd",
            TypeSymbol.Single,
            OptionalParameter("Number", TypeSymbol.Single, 0f)),
        Sub(
            "Randomize",
            VBIntrinsicKind.Randomize,
            "VBMath.Randomize",
            OptionalParameter("Number", TypeSymbol.Variant)),
        Function("Int", VBIntrinsicKind.Int, "VBConversions.Int", TypeSymbol.Variant, Parameter("Number", TypeSymbol.Variant)),
        Function(
            "IIf",
            VBIntrinsicKind.IIf,
            "VBFunctions.IIf",
            TypeSymbol.Variant,
            Parameter("Expression", TypeSymbol.Boolean),
            Parameter("TruePart", TypeSymbol.Variant),
            Parameter("FalsePart", TypeSymbol.Variant)),
        Function(
            "RGB",
            VBIntrinsicKind.RGB,
            "VBFunctions.RGB",
            TypeSymbol.Long,
            Parameter("Red", TypeSymbol.Long),
            Parameter("Green", TypeSymbol.Long),
            Parameter("Blue", TypeSymbol.Long)),
        Sub("DoEvents", VBIntrinsicKind.DoEvents, "VBInteraction.DoEvents"),
        Sub("Cls", VBIntrinsicKind.Cls, "VBInteraction.Cls"),
        Sub("Kill", VBIntrinsicKind.Kill, "VBFiles.Kill", Parameter("Path", TypeSymbol.String)),
        Sub(
            "FileCopy",
            VBIntrinsicKind.FileCopy,
            "VBFiles.FileCopy",
            Parameter("Source", TypeSymbol.String),
            Parameter("Destination", TypeSymbol.String)),
        Sub("MkDir", VBIntrinsicKind.MkDir, "VBFiles.MakeDirectory", Parameter("Path", TypeSymbol.String)),
        Sub("RmDir", VBIntrinsicKind.RmDir, "VBFiles.RemoveDirectory", Parameter("Path", TypeSymbol.String)),
        Sub("ChDir", VBIntrinsicKind.ChDir, "VBFiles.ChangeDirectory", Parameter("Path", TypeSymbol.String)),
        Function(
            "CurDir",
            VBIntrinsicKind.CurDir,
            "VBFiles.CurrentDirectory",
            TypeSymbol.String,
            OptionalParameter("Drive", TypeSymbol.String, string.Empty)),
        Function("GetAttr", VBIntrinsicKind.GetAttr, "VBFiles.GetAttributes", TypeSymbol.Long, Parameter("Path", TypeSymbol.String)),
        Sub(
            "SetAttr",
            VBIntrinsicKind.SetAttr,
            "VBFiles.SetAttributes",
            Parameter("Path", TypeSymbol.String),
            Parameter("Attributes", TypeSymbol.Long)),
        Function("FileDateTime", VBIntrinsicKind.FileDateTime, "VBFiles.FileDateTime", TypeSymbol.Date, Parameter("Path", TypeSymbol.String)),
        Function(
            "Dir",
            VBIntrinsicKind.Dir,
            "VBFiles.Dir",
            TypeSymbol.String,
            OptionalParameter("Path", TypeSymbol.String, string.Empty),
            OptionalParameter("Attributes", TypeSymbol.Long, 0L)),
        Function(
            "MsgBox",
            VBIntrinsicKind.MsgBox,
            "VBInteraction.MsgBox",
            TypeSymbol.Integer,
            Parameter("Prompt", TypeSymbol.String),
            OptionalParameter("Buttons", TypeSymbol.Long, 0L),
            OptionalParameter("Title", TypeSymbol.String, string.Empty)),
        Function(
            "InputBox",
            VBIntrinsicKind.InputBox,
            "VBInteraction.InputBox",
            TypeSymbol.String,
            Parameter("Prompt", TypeSymbol.String),
            OptionalParameter("Title", TypeSymbol.String, string.Empty),
            OptionalParameter("Default", TypeSymbol.String, string.Empty),
            OptionalParameter("XPos", TypeSymbol.Single, 0f),
            OptionalParameter("YPos", TypeSymbol.Single, 0f),
            OptionalParameter("HelpFile", TypeSymbol.String, string.Empty),
            OptionalParameter("Context", TypeSymbol.Long, 0L)),
        Function(
            "GetSetting",
            VBIntrinsicKind.GetSetting,
            "VBInteraction.GetSetting",
            TypeSymbol.String,
            Parameter("AppName", TypeSymbol.String),
            Parameter("Section", TypeSymbol.String),
            Parameter("Key", TypeSymbol.String),
            OptionalParameter("Default", TypeSymbol.String, string.Empty)),
        Sub(
            "SaveSetting",
            VBIntrinsicKind.SaveSetting,
            "VBInteraction.SaveSetting",
            Parameter("AppName", TypeSymbol.String),
            Parameter("Section", TypeSymbol.String),
            Parameter("Key", TypeSymbol.String),
            Parameter("Setting", TypeSymbol.String)),
        Sub(
            "SendKeys",
            VBIntrinsicKind.SendKeys,
            "VBInteraction.SendKeys",
            Parameter("Keys", TypeSymbol.String),
            OptionalParameter("Wait", TypeSymbol.Boolean, false)),
        Sub(
            "PopupMenu",
            VBIntrinsicKind.PopupMenu,
            "VBInteraction.PopupMenu",
            Parameter("Menu", TypeSymbol.Variant),
            OptionalParameter("Flags", TypeSymbol.Long, 0L),
            OptionalParameter("X", TypeSymbol.Single, 0f),
            OptionalParameter("Y", TypeSymbol.Single, 0f)),
        Function(
            "LoadPicture",
            VBIntrinsicKind.LoadPicture,
            "VBInteraction.LoadPicture",
            VBStandardTypes.Picture,
            OptionalParameter("FileName", TypeSymbol.String, string.Empty)),
        Sub(
            "PropertyChanged",
            VBIntrinsicKind.PropertyChanged,
            "VBInteraction.PropertyChanged",
            Parameter("PropertyName", TypeSymbol.String)),
        Function("FileLen", VBIntrinsicKind.FileLen, "VBFiles.FileLength", TypeSymbol.LongLong, Parameter("Path", TypeSymbol.String)),
        Function("Date", VBIntrinsicKind.Date, "VBDateTime.Date", TypeSymbol.Variant),
        Function("Time", VBIntrinsicKind.Time, "VBDateTime.Time", TypeSymbol.Variant),
        Function("Now", VBIntrinsicKind.Now, "VBDateTime.Now", TypeSymbol.Date),
        Function("DateValue", VBIntrinsicKind.DateValue, "VBDateTime.DateValue", TypeSymbol.Date, Parameter("Date", TypeSymbol.Variant)),
        Function("TimeValue", VBIntrinsicKind.TimeValue, "VBDateTime.TimeValue", TypeSymbol.Date, Parameter("Time", TypeSymbol.Variant)),
        Function("Year", VBIntrinsicKind.Year, "VBDateTime.Year", TypeSymbol.Integer, Parameter("Date", TypeSymbol.Date)),
        Function("Month", VBIntrinsicKind.Month, "VBDateTime.Month", TypeSymbol.Integer, Parameter("Date", TypeSymbol.Date)),
        Function("Day", VBIntrinsicKind.Day, "VBDateTime.Day", TypeSymbol.Integer, Parameter("Date", TypeSymbol.Date)),
        Function("Hour", VBIntrinsicKind.Hour, "VBDateTime.Hour", TypeSymbol.Integer, Parameter("Time", TypeSymbol.Date)),
        Function("Minute", VBIntrinsicKind.Minute, "VBDateTime.Minute", TypeSymbol.Integer, Parameter("Time", TypeSymbol.Date)),
        Function("Second", VBIntrinsicKind.Second, "VBDateTime.Second", TypeSymbol.Integer, Parameter("Time", TypeSymbol.Date)),
        Function("Timer", VBIntrinsicKind.Timer, "VBDateTime.Timer", TypeSymbol.Single),
        Function("DateSerial", VBIntrinsicKind.DateSerial, "VBDateTime.DateSerial", TypeSymbol.Date, Parameter("Year", TypeSymbol.Integer), Parameter("Month", TypeSymbol.Integer), Parameter("Day", TypeSymbol.Integer)),
        Function("TimeSerial", VBIntrinsicKind.TimeSerial, "VBDateTime.TimeSerial", TypeSymbol.Date, Parameter("Hour", TypeSymbol.Integer), Parameter("Minute", TypeSymbol.Integer), Parameter("Second", TypeSymbol.Integer)),
        Function("DateAdd", VBIntrinsicKind.DateAdd, "VBDateTime.DateAdd", TypeSymbol.Date, Parameter("Interval", TypeSymbol.String), Parameter("Number", TypeSymbol.Double), Parameter("Date", TypeSymbol.Date)),
        Function(
            "DateDiff",
            VBIntrinsicKind.DateDiff,
            "VBDateTime.DateDiff",
            TypeSymbol.Long,
            Parameter("Interval", TypeSymbol.String),
            Parameter("Date1", TypeSymbol.Date),
            Parameter("Date2", TypeSymbol.Date),
            OptionalParameter("FirstDayOfWeek", TypeSymbol.Long, 1L),
            OptionalParameter("FirstWeekOfYear", TypeSymbol.Long, 1L)),
        Function(
            "DatePart",
            VBIntrinsicKind.DatePart,
            "VBDateTime.DatePart",
            TypeSymbol.Long,
            Parameter("Interval", TypeSymbol.String),
            Parameter("Date", TypeSymbol.Date),
            OptionalParameter("FirstDayOfWeek", TypeSymbol.Long, 1L),
            OptionalParameter("FirstWeekOfYear", TypeSymbol.Long, 1L)),
        Function(
            "Weekday",
            VBIntrinsicKind.Weekday,
            "VBDateTime.Weekday",
            TypeSymbol.Integer,
            Parameter("Date", TypeSymbol.Date),
            OptionalParameter("FirstDayOfWeek", TypeSymbol.Long, 1L)),
        Function(
            "WeekdayName",
            VBIntrinsicKind.WeekdayName,
            "VBDateTime.WeekdayName",
            TypeSymbol.String,
            Parameter("Weekday", TypeSymbol.Integer),
            OptionalParameter("Abbreviate", TypeSymbol.Boolean, false),
            OptionalParameter("FirstDayOfWeek", TypeSymbol.Long, 1L)),
        Function(
            "MonthName",
            VBIntrinsicKind.MonthName,
            "VBDateTime.MonthName",
            TypeSymbol.String,
            Parameter("Month", TypeSymbol.Integer),
            OptionalParameter("Abbreviate", TypeSymbol.Boolean, false)),
        Function("Erl", VBIntrinsicKind.Erl, "VBErrors.LineNumber", TypeSymbol.Long),
        Function("Command", VBIntrinsicKind.Command, "VBInteraction.Command", TypeSymbol.String),
        Function("Environ", VBIntrinsicKind.Environ, "VBInteraction.Environ", TypeSymbol.String, Parameter("Expression", TypeSymbol.Variant)),
        Sub("Load", VBIntrinsicKind.Load, "VBInteraction.Load", Parameter("Object", TypeSymbol.Variant)),
        Sub("Unload", VBIntrinsicKind.Unload, "VBInteraction.Unload", Parameter("Object", TypeSymbol.Variant)),
        Function("VarPtr", VBIntrinsicKind.VarPtr, "VBMemory.VarPtr", TypeSymbol.Long, Parameter("Expression", TypeSymbol.Variant)),
        Function("ObjPtr", VBIntrinsicKind.ObjPtr, "VBMemory.ObjPtr", TypeSymbol.LongPtr, Parameter("Object", TypeSymbol.Variant)),
        Function("StrPtr", VBIntrinsicKind.StrPtr, "VBMemory.StrPtr", TypeSymbol.Long, Parameter("Expression", TypeSymbol.String)),
        Sub("LSet", VBIntrinsicKind.LSet, "VBMemory.LSet", Parameter("Target", TypeSymbol.Variant), Parameter("Source", TypeSymbol.Variant)),
        Sub("RSet", VBIntrinsicKind.RSet, "VBMemory.RSet", Parameter("Target", TypeSymbol.Variant), Parameter("Source", TypeSymbol.Variant)),
        Function(
            "CreateObject",
            VBIntrinsicKind.CreateObject,
            "VBInteraction.CreateObject",
            VBStandardTypes.Object,
            Parameter("Class", TypeSymbol.String),
            OptionalParameter("ServerName", TypeSymbol.String, string.Empty)),
        Function(
            "GetObject",
            VBIntrinsicKind.GetObject,
            "VBInteraction.GetObject",
            VBStandardTypes.Object,
            OptionalParameter("PathName", TypeSymbol.String, string.Empty),
            OptionalParameter("Class", TypeSymbol.String, string.Empty)),
        Function(
            "Shell",
            VBIntrinsicKind.Shell,
            "VBInteraction.Shell",
            TypeSymbol.Long,
            Parameter("PathName", TypeSymbol.String),
            OptionalParameter("WindowStyle", TypeSymbol.Integer, 1L)),
        Function("TypeName", VBIntrinsicKind.TypeName, "VBFunctions.TypeName", TypeSymbol.String, Parameter("Expression", TypeSymbol.Variant)),
        Function(
            "Array",
            VBIntrinsicKind.Array,
            "VBFunctions.Array",
            TypeSymbol.Variant,
            new ParameterSymbol("Arguments", new ArrayTypeSymbol(TypeSymbol.Variant), ParameterPassingMode.ByVal)
            {
                IsParamArray = true
            }),
        Function(
            "Switch",
            VBIntrinsicKind.Switch,
            "VBFunctions.Switch",
            TypeSymbol.Variant,
            new ParameterSymbol("Arguments", new ArrayTypeSymbol(TypeSymbol.Variant), ParameterPassingMode.ByVal)
            {
                IsParamArray = true
            }),
        Function(
            "Choose",
            VBIntrinsicKind.Choose,
            "VBFunctions.Choose",
            TypeSymbol.Variant,
            Parameter("Index", TypeSymbol.Long),
            new ParameterSymbol("Choices", new ArrayTypeSymbol(TypeSymbol.Variant), ParameterPassingMode.ByVal)
            {
                IsParamArray = true
            }),
        Function("IsEmpty", VBIntrinsicKind.IsEmpty, "VBVariants.IsEmpty", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("IsNull", VBIntrinsicKind.IsNull, "VBVariants.IsNull", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("IsMissing", VBIntrinsicKind.IsMissing, "VBVariants.IsMissing", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("IsError", VBIntrinsicKind.IsError, "VBVariants.IsError", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("VarType", VBIntrinsicKind.VarType, "VBVariants.VarType", TypeSymbol.Integer, Parameter("Expression", TypeSymbol.Variant)),
        Function("Empty", VBIntrinsicKind.Empty, "VBVariants.EmptyValue", TypeSymbol.Variant),
        Function("Null", VBIntrinsicKind.Null, "VBVariants.NullValue", TypeSymbol.Variant),
        Function("Nothing", VBIntrinsicKind.Nothing, "VBVariants.NothingValue", TypeSymbol.Variant),
        Function("Missing", VBIntrinsicKind.Missing, "VBVariants.MissingValue", TypeSymbol.Variant),

        Sub("Reset", VBIntrinsicKind.Reset, "VBFiles.Reset"),
        Function("FreeFile", VBIntrinsicKind.FreeFile, "VBFiles.FreeFile", TypeSymbol.Long),
        Function("LOF", VBIntrinsicKind.LOF, "VBFiles.Length", TypeSymbol.LongLong, Parameter("FileNumber", TypeSymbol.Long)),
        Function("EOF", VBIntrinsicKind.EOF, "VBFiles.EndOfFile", TypeSymbol.Boolean, Parameter("FileNumber", TypeSymbol.Long)),
        Function("Loc", VBIntrinsicKind.Loc, "VBFiles.Location", TypeSymbol.LongLong, Parameter("FileNumber", TypeSymbol.Long)),
        Function("Input", VBIntrinsicKind.Input, "VBFiles.Input", TypeSymbol.String, Parameter("NumberOfCharacters", TypeSymbol.LongLong), Parameter("FileNumber", TypeSymbol.Long)),
        Function("Seek", VBIntrinsicKind.Seek, "VBFiles.Position", TypeSymbol.LongLong, Parameter("FileNumber", TypeSymbol.Long)),

        Function("CByte", VBIntrinsicKind.CByte, "VBConversions.CByte", TypeSymbol.Byte, Parameter("Expression", TypeSymbol.Variant)),
        Function("CInt", VBIntrinsicKind.CInt, "VBConversions.CInt", TypeSymbol.Integer, Parameter("Expression", TypeSymbol.Variant)),
        Function("CLng", VBIntrinsicKind.CLng, "VBConversions.CLng", TypeSymbol.Long, Parameter("Expression", TypeSymbol.Variant)),
        Function("CLngPtr", VBIntrinsicKind.CLngPtr, "VBConversions.CLngPtr", TypeSymbol.LongPtr, Parameter("Expression", TypeSymbol.Variant)),
        Function("CUShort", VBIntrinsicKind.CUShort, "VBConversions.CUShort", TypeSymbol.UShort, Parameter("Expression", TypeSymbol.Variant)),
        Function("CUInt", VBIntrinsicKind.CUInt, "VBConversions.CUInt", TypeSymbol.UInteger, Parameter("Expression", TypeSymbol.Variant)),
        Function("CULng", VBIntrinsicKind.CULng, "VBConversions.CULng", TypeSymbol.ULong, Parameter("Expression", TypeSymbol.Variant)),
        Function("CCur", VBIntrinsicKind.CCur, "VBConversions.CCur", TypeSymbol.Currency, Parameter("Expression", TypeSymbol.Variant)),
        Function("CDec", VBIntrinsicKind.CDec, "VBConversions.CDec", TypeSymbol.Variant, Parameter("Expression", TypeSymbol.Variant)),
        Function("CDate", VBIntrinsicKind.CDate, "VBConversions.CDate", TypeSymbol.Date, Parameter("Expression", TypeSymbol.Variant)),
        Function("CVDate", VBIntrinsicKind.CVDate, "VBConversions.CVDate", TypeSymbol.Variant, Parameter("Expression", TypeSymbol.Variant)),
        Function("CSng", VBIntrinsicKind.CSng, "VBConversions.CSng", TypeSymbol.Single, Parameter("Expression", TypeSymbol.Variant)),
        Function("CDbl", VBIntrinsicKind.CDbl, "VBConversions.CDbl", TypeSymbol.Double, Parameter("Expression", TypeSymbol.Variant)),
        Function("CBool", VBIntrinsicKind.CBool, "VBConversions.CBool", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("CStr", VBIntrinsicKind.CStr, "VBConversions.CStr", TypeSymbol.String, Parameter("Expression", TypeSymbol.Variant)),
        Function("CVar", VBIntrinsicKind.CVar, "VBConversions.CVar", TypeSymbol.Variant, Parameter("Expression", TypeSymbol.Variant)),
        Function("CVErr", VBIntrinsicKind.CVErr, "VBConversions.CVErr", TypeSymbol.Variant, Parameter("Expression", TypeSymbol.Variant)));

    private static readonly ImmutableArray<ProcedureSymbol> HostIntrinsics = ImmutableArray.Create(
        Function(
            "ScaleX",
            VBIntrinsicKind.ScaleX,
            "VBInteraction.ScaleX",
            TypeSymbol.Single,
            Parameter("Expression", TypeSymbol.Single),
            OptionalParameter("FromScale", TypeSymbol.Long, 0L),
            OptionalParameter("ToScale", TypeSymbol.Long, 0L)),
        Function(
            "ScaleY",
            VBIntrinsicKind.ScaleY,
            "VBInteraction.ScaleY",
            TypeSymbol.Single,
            Parameter("Expression", TypeSymbol.Single),
            OptionalParameter("FromScale", TypeSymbol.Long, 0L),
            OptionalParameter("ToScale", TypeSymbol.Long, 0L)),
        Function(
            "TextWidth",
            VBIntrinsicKind.TextWidth,
            "VBInteraction.TextWidth",
            TypeSymbol.Single,
            Parameter("Text", TypeSymbol.String)),
        Function(
            "TextHeight",
            VBIntrinsicKind.TextHeight,
            "VBInteraction.TextHeight",
            TypeSymbol.Single,
            Parameter("Text", TypeSymbol.String)),
        Sub(
            "Print",
            VBIntrinsicKind.Print,
            "VBInteraction.Print",
            Parameter("Value", TypeSymbol.Variant)),
        Sub(
            "PaintPicture",
            VBIntrinsicKind.PaintPicture,
            "VBInteraction.PaintPicture",
            Parameter("Picture", TypeSymbol.Variant),
            Parameter("X", TypeSymbol.Single),
            Parameter("Y", TypeSymbol.Single),
            Parameter("Width", TypeSymbol.Single),
            Parameter("Height", TypeSymbol.Single)));

    public static Dictionary<string, ProcedureSymbol> CreateProcedureTable(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in root.Members)
        {
            var symbol = member switch
            {
                SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
                DeclareDeclarationSyntax declare => Binder.CreateDeclareProcedureSymbol(declare),
                _ => null
            };

            if (symbol is not null)
            {
                procedures.TryAdd(symbol.Name, symbol);
            }
        }

        AddTo(procedures);
        return procedures;
    }

    public static void AddTo(IDictionary<string, ProcedureSymbol> procedures)
    {
        ArgumentNullException.ThrowIfNull(procedures);

        foreach (var intrinsic in Intrinsics)
        {
            if (!procedures.ContainsKey(intrinsic.Name))
            {
                procedures.Add(intrinsic.Name, intrinsic);
            }
        }
    }

    public static void AddHostProcedures(IDictionary<string, ProcedureSymbol> procedures)
    {
        ArgumentNullException.ThrowIfNull(procedures);

        foreach (var intrinsic in HostIntrinsics)
        {
            if (!procedures.ContainsKey(intrinsic.Name))
            {
                procedures.Add(intrinsic.Name, intrinsic);
            }
        }
    }

    private static ProcedureSymbol Function(
        string name,
        VBIntrinsicKind intrinsicKind,
        string runtimeTarget,
        TypeSymbol returnType,
        params ParameterSymbol[] parameters) =>
        new(name, parameters.ToImmutableArray(), returnType)
        {
            IntrinsicKind = intrinsicKind,
            IntrinsicTarget = runtimeTarget
        };

    private static ProcedureSymbol Sub(
        string name,
        VBIntrinsicKind intrinsicKind,
        string runtimeTarget,
        params ParameterSymbol[] parameters) =>
        new(name, parameters.ToImmutableArray(), null)
        {
            IntrinsicKind = intrinsicKind,
            IntrinsicTarget = runtimeTarget
        };

    private static ParameterSymbol Parameter(string name, TypeSymbol type) =>
        new(name, type, ParameterPassingMode.ByVal);

    private static ParameterSymbol OptionalParameter(string name, TypeSymbol type, object defaultValue) =>
        new(name, type, ParameterPassingMode.ByVal)
        {
            IsOptional = true,
            DefaultValue = defaultValue
        };

    private static ParameterSymbol OptionalParameter(string name, TypeSymbol type) =>
        new(name, type, ParameterPassingMode.ByVal)
        {
            IsOptional = true
        };
}
