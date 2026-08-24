# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is a modern, highly compatible VB6 compiler with one language/runtime contract and multiple backends: native Windows x86/x64 and .NET. COM/ActiveX compatibility, standard VB6 projects and Visual Studio/LSP consumption are compiler requirements; the IDE and WinForms designer are later products.

## Current status

The first end-to-end managed compiler path is working and is being expanded feature by feature. M0 through M3 are complete, the Variant core is implemented with the remaining promotion matrix still open, and the first M5 class/object-model slice is now in place. The backend is no longer a C# code generator: the bound program is lowered to an IR of basic blocks and emitted directly as CIL. LLVM target selection, primitive scalar native emission, an MSBuild SDK boundary and an LSP with diagnostics/navigation now build; native ABI/runtime emission, COM/ActiveX compatibility and the full standard library remain major compiler milestones.

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
- `Choose` with rounded one-based selection, eager Variant choices, and Null for out-of-range indexes
- `Switch` with eager condition/value evaluation and Variant Null when no condition matches
- `Str` with invariant numeric formatting and VB6's leading sign space
- `ChrW` and `AscW` for Unicode UTF-16 code-unit conversion
- `Join` and `Filter` for typed `String()` arrays, including optional delimiters and binary/text comparison
- `Oct` and `CVar` alongside the existing numeric/string conversion intrinsics, including Null and Date Variant preservation
- `CCur` as a Currency conversion intrinsic with VB6-compatible four-decimal-place rounding
- `Date`, `Time`, and `CVDate` as Variant(Date) intrinsics on the shared OLE-Date runtime path
- `Environ` with case-insensitive name lookup and deterministic one-based `NAME=VALUE` index access
- the host-neutral `App` object with executable name/path, title, version metadata, and a deterministic headless instance handle
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
- `LSet target = source` assignment syntax, including Managed execution for fixed-length String targets and same-type UDT copies; cross-UDT raw-layout transfer remains a native ABI task
- filesystem path intrinsics for legacy projects: `FileCopy`, `MkDir`, `RmDir`, `ChDir`, `CurDir`, `GetAttr`, `SetAttr`, and `FileDateTime` through the Managed runtime
- the legacy `Name oldPath As newPath` statement for file and directory renames through the Managed runtime
- `Dir` continuation with VB6 directory, hidden, system and volume attribute filtering on the Managed runtime
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
- late-bound `Variant`/`Object` member dispatch for generated Managed classes, including Property Get/Let/Set and method calls, with optional arguments, `ParamArray`, typed CLR property/indexer conversion, and ByRef write-back for Managed/CLR targets; real COM default access uses `DISPID_VALUE`, COM RCW identity uses `IUnknown`, and EventInfo-backed CLR/COM-RCW events can connect VB6 handlers with ByRef write-back; raw COM `IDispatch`/connection-point ABI edge cases remain open
- VB built-in string and numeric constants such as `vbCrLf`, `vbTab`, `vbWhite`, `vbButtonFace`, `vbRetry`, and `vbPicTypeBitmap`, which user declarations of the same name still override
- the `Len`, two- and three-argument `Mid`, ASCII `Chr`, `InStr`, `InStrRev`, `Replace`, and current `Abs`/`Sgn`/`Fix`/`Round`/`Sqr`/`Exp`/`Log`/`Sin`/`Cos`/`Tan`/`Atn`/`Rnd`/`Randomize` math intrinsics, including `Null`-/`Empty`-semantics for `Abs`, `Sgn`, `Fix`, `Round` and `Int`, plus the `CByte`/`CInt`/`CLng`/`CLngPtr`/`CUShort`/`CUInt`/`CULng`/`CDec`/`CSng`/`CDbl`/`CBool`/`CStr` conversions and the `Left`/`Right`/`UCase`/`LCase`/`Trim`/`LTrim`/`RTrim`/`Asc`/`IsNumeric` string functions. Each intrinsic symbol carries the runtime method the backend calls, so it is resolved and checked like any other procedure and a user declaration of the same name still shadows it
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
- the regression suite currently contains **906 tests**, including typed string-key Variant, Object, compiled COM automation dispatch, and legacy `.dsr` project emission
- `.vbp` loading for common project metadata, modules, classes, forms, controls, property pages, user documents, legacy `Designer=...; file.dsr` sources, references, and components, plus `.vbg` group loading and command-line batch emission of declared projects
- an optional host boundary for compiled Forms/UserControls: `VB6.Runtime` exposes lifecycle, dynamic member, control-creation, enumeration and event hooks, while `VB6.Runtime.WinForms` maps standard VB6 controls, Twips, OLE colors, fonts and `Load`/`Unload`/`Show` to WinForms
- `.cls` project sources: designer metadata stripping, class type registration, `New`, `Set`, `TypeOf`, class Properties, Events, `WithEvents`, `Implements` as CLR interfaces, and class-member binding
- the standard `Collection` object on the managed backend: `New Collection`, one-based and keyed `Item`, `Count`, `Add` with `Key`/`Before`, `Remove`, and `For Each` in insertion order
- unit tests for syntax, lexer, parser, semantics, runtime, IR lowering, managed emission, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions restore/build/test workflow with a VISIA parity report on every run

The M3 array work was deliberately split into layers, and the guards from that period are gone: declarations, parameters, element access, `ReDim`/`Preserve`, `Erase`, `LBound`/`UBound`, and `For Each` are bound, emitted, and executed against `VBArray<T>`, which keeps VB6 lower bounds instead of normalizing to zero-based CLR arrays. What is still guarded is narrower and each case has its own diagnostic: `For Each` over arrays of user-defined types (`VB6S0056`), `Erase` on an array parameter (`VB6S0036`), and UDT layouts that managed lowering cannot represent yet (`VB6S0046`).


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
vb6c LegacyGroup.vbg --report
```

The report lists project items by kind, counts analyzed/error-free sources, and ranks remaining gaps by affected files. `conformance/` holds real third-party VB6 projects used for this measurement; see `conformance/README.md`.

Print the lowered IR - basic blocks, instructions and terminators - for one source file or for
every standard module of a project. Without an output file the dump goes to standard output:

```text
vb6c Module1.bas --dump-ir
vb6c LegacyApp.vbp --dump-ir LegacyApp.ir.txt
```

Generate a native LLVM module from the scalar backend (x64 is the default):

```text
vb6c Module1.bas --emit-llvm Module1.ll
vb6c Module1.bas --emit-llvm Module1-x86.ll --x86
```

Generate a managed application assembly:

```text
vb6c Module1.bas --emit-assembly Module1.dll
vb6c LegacyApp.vbp --emit-assembly LegacyApp.dll
vb6c LegacyGroup.vbg --emit-assembly build
vb6c LegacyApp.vbp --emit-assembly build\LegacyApp.exe --x86
vb6c LegacyGroup.vbg --emit-assembly build --x86
```

The managed application output consists of the emitted `.exe` or `.dll`, its `.runtimeconfig.json`, and `VB6.Runtime.dll`. For a `.vbg` group, the output argument is a directory; referenced `.vbp` library projects are emitted before their consumers, while independent projects retain their declaration order. `StartupProject=` is validated against the declared group projects before emission. Library projects receive `.dll` names based on the project name; executable projects prefer the legacy `ExeName32` filename and fall back to `Name=`.
Managed emission defaults to AnyCPU; `--x86`, `--x64`, and `--anycpu` select the PE target architecture. `--x86` is intended for legacy projects whose OCX-/ActiveX-dependencies are 32-bit.

The legacy project path is verified against `conformance/VISIA/4.8.7.1/prjVisia.vbp`: the CLI report analyzes all 40 project items without errors, and `--emit-assembly` produces a managed executable with its debug and runtime artifacts. The `.vbg` path is covered by dependency-order, library/executable-output, project-group emission tests, and process-level CLI tests. Project, designer, source, and single-file inputs accept UTF-8/UTF-16 BOMs and fall back to the common Windows-1252 ANSI encoding used by older VB6 installations. `--report` writes project/source diagnostics to standard error and returns a non-zero exit code when analysis fails, so legacy builds can be used reliably in CI.

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point or an EXE startup `Form`, cross-module Sub and Function calls, the current ByRef/ByVal subset, `Optional` and `ParamArray` calls, persistent `Static` locals, typed Function calls, typed comma-separated scalar variable declarators, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, `^`, Byte, Integer, Long, LongLong/Int64, LongPtr, UShort/UInt16, UInteger/UInt32, ULong/UInt64, Single, Double, and Currency, plus arrays, user-defined types, `With` blocks, and the current Variant subset.

The current managed project emitter supports standard modules with a single `Sub Main` and emits the managed class core: class instances, instance fields, `New`, `Set`, `TypeOf`, Properties, implicit `Item` and `VB_UserMemId`-named default-property Get/Let dispatch, `Class_Initialize`/`Class_Terminate`, events, simple `WithEvents` sinks with reassignment cleanup, `Implements` as CLR interfaces with virtual method/property dispatch, and the standard `Collection` object with one-based/keyed lookup. EXE projects with a `Startup="FormName"` object now receive a generated entry point that constructs and initializes designer controls on the generated form, preserves nested designer-control containers, then calls the runtime host's `Load` and `Show` hooks. `VB6.Runtime.WinForms` supplies the optional concrete WinForms host; full OCX hosting and the message-loop policy remain host integration work. `Type=OleDll`, `Type=Control`, and equivalent library project types emit DLLs without requiring `Sub Main`; unsupported startup objects still produce a project diagnostic. Imported `FSOURCE` type-library events now retain their source-interface IID and DISPID and use the Windows `ComEventsHelper` bridge for generated `WithEvents` subscriptions, while raw `IDispatch` ABI invocation, custom COM server registration, and native ABI marshalling remain open. The .NET/Managed path is now the primary completion target; LLVM remains an optional native x86/x64 backend and is intentionally deferred while the managed Variant/Object/COM contracts are completed. The MSBuild SDK and diagnostic/navigation LSP are available as compiler-facing integration layers. The SDK tracks `.vbp` source/designer inputs and emitted assembly/runtime outputs for incremental MSBuild builds; package it with `dotnet pack src/VB6.Compiler.Sdk` and point `VB6CompilerPath` at a published `vb6c` executable.

Late-bound Managed/CLR dispatch now also fills optional parameters, packs `ParamArray` arguments, applies VB runtime conversions to indexed properties and property setters, and writes modified ByRef arguments back into the Variant argument array. Windows `.tlb`/`.olb` and TypeLib-bearing `.dll`/`.ocx` references are also imported through `LoadTypeLibEx` into dynamic class, method, property, Enum-constant, scalar alias, representable Record/UDT, and `FSOURCE` event contracts; scalar Record fields and referenced user-defined types flow into Managed Struct emission, while unsafe native pointer/C-array signatures intentionally fall back to `Object` until their ABI is modeled. COM RCWs now support case-insensitive late-bound method/property dispatch and `DISPID_VALUE` default access; COM RCW identity compares `IUnknown` pointers; EventInfo-backed CLR/COM-RCW events now connect conventional VB6 handlers and preserve ByRef event write-back, while raw `IDispatch`/connection-point ABI edge cases remain a separate follow-up. The CLI now accepts `.vbg` groups for `--report` and managed batch emission; each declared `.vbp` is loaded and compiled independently with project-qualified diagnostics, including library projects that do not have `Sub Main` and EXE projects whose startup object is a project Form. Group emission orders explicit project references before their consumers and keeps independent projects in declaration order. `PropertyPage` and `UserDocument` source items are normalized, bound as project classes, and included in Managed library emission. `Reference=` and `Object=` entries retain their raw VB6 spelling and expose parsed GUID/version/locale/path metadata; explicit `.vbp` references are resolved relative to the consumer project, referenced class contracts are visible under project/class aliases during analysis and can be emitted through Managed assembly/member references, and common qualified ActiveX control types are bound from the project object list. `.frm`/`.ctl` designer envelopes now parse nested controls, properties, `BeginProperty` blocks and `.frx` resource offsets; repeated names and `Index` properties bind as typed control arrays. `Begin ...` designer controls in Forms/UserControls retain their qualified object type as class fields; TreeView/Nodes/Node, ImageList/ListImages/ListImage, ImageCombo/ComboItems/ComboItem, RichTextBox, and CommonDialog contracts are available through the Managed late-bound host path, including VB6 Control-hierarchy ByRef compatibility.

Current native LLVM status (optional/deferred): checked integer Add/Subtract/Multiply and Integer narrowing/sign conversions plus Currency Add/Subtract/Negate use pending-error-aware i64 helpers with explicit target-width guards. Currency multiplication now uses a scaled `i128` product with VB6 banker's rounding and an `Int64` range guard. Rounded Single/Double-to-integer conversions now cover typed integer targets through 64-bit using `roundeven` and safe representable range guards; Currency-to-integer conversions use the same scaled ties-to-even helper; exact integer- and Boolean-to-Currency conversions use checked i128 scaling with VB6's `True = -10000` representation; rounded Single/Double-to-Currency conversions use scaled `roundeven`, finite/range guards and checked `fptosi`. Native `On Error Resume Next` and label-directed handler boundaries now consume pending scalar errors, with native `Err.Number`/`Err.Clear` access; `Resume Next` and targetless `Resume` select the recorded boundary continuation/retry labels. String-valued Err fields and complex native ABI contracts remain open. The suite now contains **899 tests**.

## Next milestones

The detailed, measured plan lives in `docs/ROADMAP.md`. The immediate compiler order is:

1. broaden `.vbp`/`.vbg` legacy coverage and project diagnostics beyond the verified CLI baseline
2. finish the Variant promotion matrix and the high-frequency standard library/runtime surface
3. complete class lifecycle, object dispatch, events and COM/ActiveX compatibility for the Managed/.NET target
4. harden the MSBuild SDK and LSP for Visual Studio; build the IDE/designer later
5. resume the optional LLVM backend for native ABI/runtime emission after the .NET target is stable
