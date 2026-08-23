# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is a modern, highly compatible VB6 compiler with one language/runtime contract and multiple backends: native Windows x86/x64 and .NET. COM/ActiveX compatibility, standard VB6 projects and Visual Studio/LSP consumption are compiler requirements; the IDE and WinForms designer are later products.

## Current status

The first end-to-end managed compiler path is working and is being expanded feature by feature. M0 through M3 are complete, the Variant core is implemented with the remaining promotion matrix still open, and the first M5 class/object-model slice is now in place. The backend is no longer a C# code generator: the bound program is lowered to an IR of basic blocks and emitted directly as CIL. LLVM target selection, an MSBuild SDK boundary and a diagnostic LSP now build; native instruction emission, COM/ActiveX ABI compatibility and the full standard library remain major compiler milestones.

Implemented so far:

- .NET 10 solution and command-line compiler entry point
- source text and diagnostic infrastructure
- case-insensitive VB6 lexer with trivia preservation
- fault-tolerant parser for the current VB6 language subset
- `Sub` and typed `Function` declarations and calls
- explicit `ByRef`, explicit `ByVal`, and VB6 default-`ByRef` parameters
- `Optional` parameters with explicit `ByVal`/`ByRef`, defaults, and `Missing` for omitted untyped Variant arguments
- named arguments with `name:=value`, case-insensitive parameter binding, signature-order normalization, and optional defaults
- `Array(...)` as a zero-based Variant-array intrinsic, including empty calls and mixed values
- `ParamArray` procedures with Variant-array collection, empty calls, mixed arguments, and declaration guards
- `Option Base 0` / `Option Base 1` and `Option Compare Text` / `Option Compare Binary` syntax; `Base` and `Compare` remain ordinary identifiers outside the directive context, while array-bound and string-comparison semantics remain later milestones
- VB6 Function return semantics through assignment to the function name
- cross-module Sub and Function resolution in `.vbp` projects
- typed comma-separated local and module variable declarators; each declarator has its own optional `As Type`, and declarators without one are normalized to Variant before binding
- `Static` local storage with persistence across calls, including scalar, String, Variant, and fixed-array initialization
- unsigned 8-bit VB6 `Byte`
- 16-bit VB6 `Integer` and 32-bit VB6 `Long`
- modern signed 64-bit integer extension exposed as `LongLong` and `Int64` while keeping VB6 `Long` 32-bit
- native-width `LongPtr` with `CLngPtr`, pointer-sized managed storage, arithmetic, and `Declare`/P/Invoke signatures
- unsigned 32-bit `UInteger` with `UInt32` alias, `CUInt`, checked arithmetic, bitwise operations, and P/Invoke signatures
- Error Variants through `CVErr`, `IsError`, `VarType`, and `TypeName`
- Variant type predicates through `IsArray`, `IsDate`, and `IsObject`, including `Nothing` object identity and array/object separation
- unsigned 16-bit `UShort`/`UInt16` and unsigned 64-bit `ULong`/`UInt64` with `CUShort`/`CULng`, checked arithmetic, bitwise operations, Variant values, and P/Invoke signatures
- Integer-to-Long numeric promotion without incorrectly promoting pure Integer expressions from their assignment target
- 64-bit integer literal inference beyond the signed 32-bit range and promotion into `LongLong`
- `Single` and `Double` floating-point types with floating literals and numeric promotion
- 64-bit scaled VB6 `Currency`, including `@` literals, four decimal places, Banker's rounding, and checked arithmetic
- `True` and `False`, including VB numeric conversion behavior
- logical operators `Not`, `And`, `Or`, `Xor`, `Eqv`, and `Imp` on Boolean operands
- bitwise `Not`, `And`, `Or`, `Xor`, `Eqv`, and `Imp` on numeric operands
- `&H` hexadecimal and `&O` octal literals with VB6 wrapping, plus the `&` and `%` integer type suffixes
- `Attribute` metadata lines
- `Public`, `Private`, `Friend` and `Global` on procedures and module-level declarations
- module-level variables, shared across the standard modules of a project
- module- and procedure-level `Const` declarations, typed or inferred from the value
- line continuation with a trailing underscore
- identifier type suffixes `$ % & ! # @`
- `Exit Sub` and `Exit Function`
- `Declare Function` and `Declare Sub` syntax with `Lib`, optional `Alias`, `ByVal`/default-`ByRef` parameters, and `As Any`; scalar signatures now lower to real Managed P/Invoke imports, and direct `AddressOf` procedure targets lower to function addresses, while full callback ABI and complex marshalling remain in the interop milestone
- `Enum ... End Enum` with optional visibility plus explicit or implicit member values, bound as Long-backed constants
- `Function` declarations without an `As` clause, which return Variant as they do in VB6
- file I/O: binary `Open`, `Close`, `Get`, `Put` and `Seek`, plus text `Open For Input/Output/Append`, `Print #`, `Line Input #` and basic string-field `Input #` CSV parsing, from lexing the file number through a runtime file-number table to generated programs that read and write real files. Positions are one-based and each supported binary type transfers its exact VB6 storage size; variable-length Strings use a two-byte character-count prefix, scalar-layout UDT records transfer their fields in declaration order, including variable `String` fields, scalar and fixed-array `String * n` UDT fields transfer exactly `n` bytes without a descriptor, fixed UDT array fields support scalar and nested non-recursive elements, and scalar Random records honor one-based record positions, fixed `Len` boundaries, padding and the VB6 default length. The current managed fixed-string profile uses one Latin-1 byte per character; host code-page selection, typed numeric/date conversion for `Input #`, and the remaining Random/Len layout rules are still reported rather than approximated
- call-site passing mode overrides, so `Foo ByVal x` hands over a value against a ByRef parameter just as `Foo (x)` does
- ByRef arguments that are not variables: a literal, an expression, or a function result is passed through a temporary whose write-back is discarded, and parentheses force an argument to be passed by value, so `Foo (x)` leaves `x` untouched while `Call Foo(x)` does not
- arithmetic operators `+`, `-`, `*`, `/`, `\`, `Mod`, and `^`; exponentiation uses VB precedence/associativity and is implemented through binding, runtime, code generation, and generated-program execution
- string concatenation with `&`
- `Like` and `Is` expression syntax at comparison precedence, including the current wildcard/`Option Compare` subset and runtime object-reference identity for Variant/host objects; emitted class-instance identity remains a later milestone
- VB-oriented operator precedence for the implemented expression subset
- `If`, multiline `ElseIf` / `Else`, and single-line `If ... Then ... Else`
- `For ... To ... Step ... Next` with numeric control variables (`Byte`, `Integer`, `Long`, `LongLong`, `LongPtr`, `UShort`, `UInteger`, `ULong`, `Single`, `Double`, `Currency`, and `Date`)
- `While ... Wend`
- pre-test and post-test `Do While`, `Do Until`, `Loop While`, and `Loop Until`
- unconditional `Do ... Loop`
- `Exit For` and `Exit Do` with bound loop targets for nested-loop correctness
- `Select Case` with value lists, ranges, relational clauses, and `Case Else`
- VB6 array declaration syntax for fixed upper bounds, explicit `lower To upper` bounds, multidimensional arrays, and dynamic `()` declarations
- array parameter syntax such as `values() As Long`
- `ArrayTypeSymbol` as the semantic type-system foundation for element type and rank
- `VBArray<T>` runtime storage that preserves explicit lower/upper bounds, rank, indexing checks, and the foundation for `LBound`/`UBound`
- array variable, parameter, and element binding, with `Option Base` applied only to dimensions that have no explicit lower bound; single array elements can be passed as real ByRef arguments
- `ReDim` and `ReDim Preserve` for dynamic arrays, including bounds checks, value preservation when the last dimension grows, and generated-program execution
- `Erase`, `LBound`, and `UBound`; `Erase` resets fixed arrays to their VB6 initial values and deallocates dynamic ones
- `For Each` over fixed, multidimensional, and dynamic arrays plus the standard `Collection`, including an implicit Variant control variable; arrays of user-defined types are rejected because VB6 rejects them too - the Variant control variable cannot hold a user-defined type declared in a standard module
- `Type ... End Type` with visibility, scalar and fixed array members, nested type names, keyword member names, and `String * n`
- `UserDefinedTypeSymbol` with case-insensitive member lookup, forward references, and Public project-wide versus Private module-local scope
- user-defined type values as locals, parameters, and module variables, including member reads and writes, member arrays, managed value-copy semantics at every value boundary - assignment, array element, member, ByVal argument and function result - including the arrays a copied value owns
- `With` blocks with implicit `.Member` access, bound through a receiver alias
- `Variant` as a semantic type with storage and explicit conversions; the current scalar runtime covers numeric `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, logical operators, comparisons, `&` concatenation, VB6 string/Variant `+`, Date-Subtype-Arithmetik, Empty, Null propagation, and Decimal promotion, while remaining `Missing` edge cases and object/array Variants stay on the roadmap
- late-bound `Variant`/`Object` member dispatch for generated Managed classes, including Property Get/Let/Set and method calls, with CLR-property fallback for host objects; COM/IDispatch identity and full ByRef write-back remain open
- VB built-in string and numeric constants such as `vbCrLf`, `vbTab`, `vbWhite`, `vbButtonFace`, `vbRetry`, and `vbPicTypeBitmap`, which user declarations of the same name still override
- the `Len`, two- and three-argument `Mid`, ASCII `Chr`, `InStr`, `InStrRev`, `Replace`, and current `Abs`/`Sgn`/`Fix`/`Round`/`Sqr`/`Exp`/`Log`/`Sin`/`Cos`/`Tan`/`Atn` math intrinsics, including `Null`-/`Empty`-semantics for `Abs`, `Fix` and `Round`, plus the `CByte`/`CInt`/`CLng`/`CLngPtr`/`CUShort`/`CUInt`/`CULng`/`CDec`/`CSng`/`CDbl`/`CBool`/`CStr` conversions and the `Left`/`Right`/`UCase`/`LCase`/`Trim`/`LTrim`/`RTrim`/`Asc`/`IsNumeric` string functions. Each intrinsic symbol carries the runtime method the backend calls, so it is resolved and checked like any other procedure and a user declaration of the same name still shadows it
- the deterministic `Format`/`Format$` subset for numeric masks (`0`, `#`, grouping, decimals, percent and sections), standard numeric names, string case masks, common date/time tokens, scalar `Year`/`Month`/`Day`/`Hour`/`Minute`/`Second`/`Timer` intrinsics, `DateValue`/`TimeValue`, and `DateSerial`/`TimeSerial`/`DateAdd`/`DateDiff` for the supported interval subset; week-number, locale-specific formatting and further placeholders remain explicit follow-up work
- bracketed identifiers such as `[Stop]`
- semantic binder with procedure, parameter, return-value, local-variable and primitive type symbols
- explicit conversion nodes and typed arithmetic promotion
- central `VBCompilation` analysis pipeline for individual source files
- `VBProjectCompilation` for combining standard modules from `.vbp` projects
- primitive `VB6.Runtime` conversion, checked Byte/Integer/Long/LongLong/Currency arithmetic, exponentiation, comparisons, Boolean operations, concatenation, VB6-near `Debug.Print`, and host-neutral `MsgBox`/`InputBox` contracts
- lowering of the bound program to an IR of basic blocks with explicit jumps (`VB6.IR`), inspectable with `--dump-ir`
- direct managed emission from that IR: CIL, metadata and a Portable PDB written by `VB6.Emit.Managed`, with no C# or Roslyn in between
- debug information that maps back to VB6 source: documents, user-visible locals, and a sequence point per statement, carried referentially from the binder through the IR into the PDB
- runtime deployment files for emitted managed applications
- end-to-end execution tests for generated single-file and multi-module managed applications
- `.vbp` loading for common project metadata, modules, classes, forms, controls, references, and components
- `.cls` project sources: designer metadata stripping, class type registration, `New`, `Set`, `TypeOf`, class Properties, Events, `WithEvents`, `Implements` as CLR interfaces, and class-member binding
- the standard `Collection` object on the managed backend: `New Collection`, one-based and keyed `Item`, `Count`, `Add` with `Key`/`Before`, `Remove`, and `For Each` in insertion order
- unit tests for syntax, lexer, parser, semantics, runtime, IR lowering, managed emission, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions restore/build/test workflow with a VISIA parity report on every run

The M3 array work was deliberately split into layers, and the guards from that period are gone: declarations, parameters, element access, `ReDim`/`Preserve`, `Erase`, `LBound`/`UBound`, and `For Each` are bound, emitted, and executed against `VBArray<T>`, which keeps VB6 lower bounds instead of normalizing to zero-based CLR arrays. What is still guarded is narrower and each case has its own diagnostic: `For Each` over arrays of user-defined types (`VB6S0056`), `Erase` on an array parameter (`VB6S0036`), and UDT layouts that managed lowering cannot represent yet (`VB6S0046`).

The suite currently holds **758 tests** across the test projects, and the Release build is warning-free. The current VISIA regression measurement is **1 total error** - **0 parser**, **0 lexer**, **1 semantic** - across all 40 project items (27 modules, 6 forms, 4 user controls and 3 classes), and **39 items analyze without a single error**. `LongPtr` and `CLngPtr` now use native-width `System.IntPtr` storage and real `Declare`/P/Invoke signatures while retaining checked arithmetic and bitwise operations; direct `AddressOf` procedure targets now lower to managed function addresses, while full native callback ABI and delegate-lifetime contracts remain open. Error Variants now use `CVErr`, `IsError`, `VarType = 10`, and `TypeName = "Error"`; `CVErr(Null)` preserves Null semantics. `IsArray`, `IsDate`, and `IsObject` now use the same Variant runtime state contract, including `Nothing` as an object and arrays as a distinct non-object category. `Array(...)` now returns a zero-based Variant array and supports empty calls and mixed values. Named arguments use `name:=value`, resolve case-insensitively to procedure parameters, reorder into signature order, and fill optional defaults. `UShort`/`UInt16`, `UInteger`/`UInt32`, and `ULong`/`UInt64` now cover the unsigned widths through `CUShort`, `CUInt`, and `CULng`, checked arithmetic/bitwise operations, Variant numeric conversion, `For` counters, and scalar P/Invoke signatures. `LBound`/`UBound` now also accept the VB6 spelling with empty array parentheses, such as `UBound(values())`, without confusing the array with an element value. The RichTextBox OCX file-type constants `rtfRTF = 0` and `rtfText = 1` are available through the compiler's built-in constant contract. `Format`/`Format$` now cover deterministic numeric masks, standard numeric names, `<`/`>` string case masks, and common date/time tokens including `yyyy`, `yy`, `m`/`mm`/`mmm`/`mmmm`, `d`/`dd`/`ddd`/`dddd`, `h`/`hh`, `n`/`nn`, `s`/`ss`, and `AM/PM`; the scalar `Year`/`Month`/`Day`/`Hour`/`Minute`/`Second`/`Timer` contracts now use the same OLE-Date runtime representation. `DateValue`/`TimeValue` normalize date and time parts on that same representation. `DateSerial`/`TimeSerial` normalize date and time parts on that same representation, while `DateAdd`/`DateDiff` cover the `yyyy`, `q`, `m`, `y`, `d`, `h`, `n`, `s`, `w`, and `ww` intervals; `DateDiff` also accepts the optional first-day and first-week arguments. `DatePart` now exposes calendar, time, weekday, and week-of-year values with the same optional week settings, and the corresponding `vbSunday`/`vbMonday`/`vbFirst...` constants are built in. `Weekday`, `WeekdayName`, and `MonthName` complete the current portable date-name slice with configured weekday bases and invariant-stable names. Variant `Date + Zahl` und `Date - Zahl` behalten nun den Date-Subtype; `Date - Date` liefert einen numerischen Abstand. Variant comparisons now retain Decimal precision when comparing Decimal values with `Single` or `Double`. Locale-specific formatting and further placeholders remain explicit follow-up work. `With` over an indexed class Property now evaluates the receiver once into a managed alias, preserving object member reads and writes without requiring a CLR address for a property result. `For Each` now accepts host-provided `Form`/`UserControl` control collections and object-valued `Controls` properties through a host-neutral enumeration callback; object-typed loop variables are accepted while numeric array control variables remain guarded. `Err.Source` now binds through the same semantic, IR, Managed-emitter and runtime contract as the other `Err` members, preserving the explicit source passed to `Err.Raise`. `As New` class declarators now bind and initialize through the same IR `New` path as explicit construction, including `Class_Initialize`. Standard class Property keys now follow VB6 case-insensitive lookup, so unqualified UserControl host members such as `hdc`/`hwnd` bind correctly under `Option Explicit`. The standard-library/host slice now covers `Val`, `Hex`, repeated-character `String`, expression-level `Input`, `TextHeight`, unqualified control `Print`, and the five-argument `PaintPicture` contract through symbols, IR, Managed emission, and headless runtime tests. Built-in `Picture` now exposes readable `Width`, `Height`, and `Type` members, while `Screen` exposes `TwipsPerPixelX/Y` with deterministic host metadata defaults. Graphics `Line` statements lower through a host-neutral runtime contract carrying typed coordinates, color, `Step`, and the `B`/`F` options. Labels are resolved across nested `If`, loop, and `Select Case` blocks and lower to executable IR basic-block targets. `End` lowers through a host-neutral process-termination contract that IDE and test hosts can intercept. Qualified member statements now preserve VB6 whitespace-separated argument forms and dispatch Variant receivers through the existing late-bound contract. Functions called in statement form now execute while discarding their return value, as VB6 permits. `Erase` also accepts UDT member arrays, including implicit `.Member` targets inside `With`. Constants passed to ByRef parameters use the same typed temporary semantics as literals. Identifier type suffixes (`$`, `%`, `&`, `!`, `#`, `@`) now survive lexing and infer the corresponding VB6 type for declarations, parameters, functions, and implicit variables. VISIA is a regression corpus, not the product target. The total does not fall monotonically: teaching the parser or binder a construct can expose semantic gaps that earlier cascades hid. Cleanly analyzed items can only grow, which makes them the honest corpus metric. `docs/ROADMAP.md` keeps the measured history and current blocker ranking.

Windows CI run #700 validated the array syntax slice on .NET 10 with a warning-free Release build and **258 passing tests**. Its VISIA report measures **2105 total errors**: **1644 parser**, **68 lexer**, and **393 semantic**. The array syntax slice reduces parser errors by 114 from the M2 closeout (1758 → 1644) while keeping semantic diagnostics stable. The project currently analyzes 27 of 40 VISIA project items; `.cls`, `.ctl`, and `.frm` are later milestones.

Windows CI run #662 closes the M2 parser/readability milestone with **243 passing tests** and **2219 total VISIA errors** (1758 parser, 68 lexer, 393 semantic). `Static` syntax, `^`, `Like`, and expression-level `Is` are regression-covered; at that historical snapshot `Static` lifetime, `Like` pattern matching/`Option Compare`, and `Is` object identity were guarded rather than approximated.

## Compatibility examples

The compiler keeps VB6 `Integer` as a signed 16-bit type and `Long` as a signed 32-bit type:

```vb
Sub Main()
    Dim value As Long
    Dim i As Long

    value = 40000 + 20000

    For i = 1 To 3
        value = value + 1
    Next i

    Debug.Print value
End Sub
```

The generated application prints `60003`.

Pure Integer expressions are intentionally not promoted merely because the destination is Long. For example, the multiplication in the following statement remains Integer arithmetic before the assignment conversion:

```vb
Dim value As Long
value = 2000 * 365
```

That distinction is important for preserving VB6 overflow behavior.

VB6 variable types are attached to individual declarators, not to the whole comma-separated declaration. For example, in `Dim a, b As Integer`, only `b` is Integer; `a` is Variant. VB6Compiler preserves that distinction now: the untyped `a` becomes a Variant instead of silently inheriting `Integer` from its neighbour.

Array syntax likewise preserves the distinction between implicit and explicit bounds instead of normalizing immediately to a zero-based CLR array:

```vb
Option Base 1

Sub Main()
    Dim implicitBase(10) As Long
    Dim explicitBounds(-2 To 5) As Long
    Dim grid(1 To 4, 0 To 7) As Integer
    Dim dynamicValues() As String
End Sub
```

These bounds survive into the generated program: `VBArray<T>` stores them, `LBound`/`UBound` report them, and index checks use them.

Exponentiation is kept distinct from integer arithmetic. The compiler preserves VB precedence and evaluates repeated powers from left to right, so the generated acceptance program verifies `-2 ^ 2 = -4`, `3 ^ 3 ^ 3 = 19683`, and `2 ^ -3 = 0.125`.

For modern code that needs a signed 64-bit integer, VB6Compiler provides `LongLong` with `Int64` as an alias. This is a compiler extension and does not change the size of VB6 `Long`.

For pointer-sized native handles and interop declarations, VB6Compiler provides `LongPtr` and the
`CLngPtr` conversion. It is emitted as `System.IntPtr`, so generated `Declare` signatures use the
host process pointer width while ordinary arithmetic and bitwise operations retain checked signed
semantics.

`UInteger` with `UInt32` as an alias is the first unsigned extension. It is emitted as `System.UInt32`,
uses `CUInt`, preserves the full 0 through 4,294,967,295 range, and is available in arithmetic,
bitwise expressions, `For` loops, and scalar `Declare` signatures. Additional unsigned widths remain
separate follow-up contracts.

`Byte` is emitted as .NET `System.Byte` and keeps the unsigned VB range from 0 through 255. Conversions and Byte arithmetic use checked runtime helpers so out-of-range results fail instead of silently wrapping.

`Currency` uses a dedicated scaled 64-bit runtime value instead of binary floating point. Currency literals use the VB `@` suffix, arithmetic keeps four decimal places, and assignment/conversion paths use Banker's rounding.

Conversions between strings and numbers use the invariant culture, which is a deliberate deviation from VB6. Classic VB6 resolved `CDbl("2.5")` against the active locale, so the same source produced 2.5 on one machine and 25 on another. A compiler is held to determinism instead: the compiled program behaves the same everywhere. Locale-aware output belongs to the later `Format$` work, where the locale is an explicit argument rather than ambient thread state. `CultureIndependenceTests` pins this down under a comma-decimal culture, because CI runs on `en-US` and would not notice a regression on its own.

## Command line

Analyze a VB6 source file:

```text
vb6c Module1.bas
```

Inspect a VB6 project file:

```text
vb6c LegacyApp.vbp
```

Measure how much of a project the compiler currently understands:

```text
vb6c LegacyApp.vbp --report
```

The report lists project items by kind, counts analyzed/error-free sources, and ranks remaining gaps by affected files. `conformance/` holds real third-party VB6 projects used for this measurement; see `conformance/README.md`.

Print the lowered IR - basic blocks, instructions and terminators - for one source file or for
every standard module of a project. Without an output file the dump goes to standard output:

```text
vb6c Module1.bas --dump-ir
vb6c LegacyApp.vbp --dump-ir LegacyApp.ir.txt
```

Generate a managed application assembly:

```text
vb6c Module1.bas --emit-assembly Module1.dll
vb6c LegacyApp.vbp --emit-assembly LegacyApp.dll
```

The managed application output currently consists of the application DLL, its `.runtimeconfig.json`, and `VB6.Runtime.dll`.

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, `Optional` and `ParamArray` calls, persistent `Static` locals, typed Function calls, typed comma-separated scalar variable declarators, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, `^`, Byte, Integer, Long, LongLong/Int64, LongPtr, UShort/UInt16, UInteger/UInt32, ULong/UInt64, Single, Double, and Currency, plus arrays, user-defined types, `With` blocks, and the current Variant subset.

The current managed project emitter supports standard modules with a single `Sub Main` and emits the managed class core: class instances, instance fields, `New`, `Set`, `TypeOf`, Properties, implicit `Item` and `VB_UserMemId`-named default-property Get/Let dispatch, `Class_Initialize`/`Class_Terminate`, events, simple `WithEvents` sinks with reassignment cleanup, `Implements` as CLR interfaces with virtual method/property dispatch, and the standard `Collection` object with one-based/keyed lookup. COM identity/dispatch, Forms/controls and the remaining full default-property rules remain open. The LLVM backend currently validates x86/x64 target selection and reports unsupported IR operations; native instruction emission and a native Windows apphost remain open. The MSBuild SDK and diagnostic LSP are now available as compiler-facing integration layers.

## Next milestones

The detailed, measured plan lives in `docs/ROADMAP.md`. The immediate compiler order is:

1. finish the Variant promotion matrix and the high-frequency standard library/runtime surface
2. complete class lifecycle, object dispatch, events and COM/ActiveX compatibility beyond the current CLR-interface slice
3. replace the LLVM backend boundary diagnostics with native x86/x64 emission alongside the .NET backend
4. harden the MSBuild SDK and LSP for Visual Studio; build the IDE/designer later
