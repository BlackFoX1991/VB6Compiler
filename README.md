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
- `ParamArray` procedures with Variant-array collection, empty calls, mixed arguments, and declaration guards
- `Option Base 0` / `Option Base 1` and `Option Compare Text` / `Option Compare Binary` syntax; `Base` and `Compare` remain ordinary identifiers outside the directive context, while array-bound and string-comparison semantics remain later milestones
- VB6 Function return semantics through assignment to the function name
- cross-module Sub and Function resolution in `.vbp` projects
- typed comma-separated local and module variable declarators; each declarator has its own optional `As Type`, and declarators without one are normalized to Variant before binding
- `Static` local storage with persistence across calls, including scalar, String, Variant, and fixed-array initialization
- unsigned 8-bit VB6 `Byte`
- 16-bit VB6 `Integer` and 32-bit VB6 `Long`
- modern signed 64-bit integer extension exposed as `LongLong` and `Int64` while keeping VB6 `Long` 32-bit
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
- `Const` declarations, typed or inferred from the value
- line continuation with a trailing underscore
- identifier type suffixes `$ % & ! # @`
- `Exit Sub` and `Exit Function`
+ `Declare Function` and `Declare Sub` syntax with `Lib`, optional `Alias`, `ByVal`/default-`ByRef` parameters, and `As Any`; scalar signatures now lower to real Managed P/Invoke imports, while ANSI string marshalling and `As Any` remain in the interop milestone
- `Enum ... End Enum` with optional visibility plus explicit or implicit member values, bound as Long-backed constants
- `Function` declarations without an `As` clause, which return Variant as they do in VB6
- file I/O: binary `Open`, `Close`, `Get`, `Put` and `Seek`, plus text `Open For Input/Output/Append` and `Print #`, from lexing the file number through a runtime file-number table to generated programs that read and write real files. Positions are one-based and each supported binary type transfers its exact VB6 storage size; variable-length Strings use a two-byte character-count prefix, and scalar-layout UDT records transfer their fields in declaration order. The statement words are recognized at statement position only, so they stay ordinary identifiers elsewhere. The `Len`/`Random` contract, text input statements, and UDT layouts containing arrays or variable strings remain reported rather than approximated
- call-site passing mode overrides, so `Foo ByVal x` hands over a value against a ByRef parameter just as `Foo (x)` does
- ByRef arguments that are not variables: a literal, an expression, or a function result is passed through a temporary whose write-back is discarded, and parentheses force an argument to be passed by value, so `Foo (x)` leaves `x` untouched while `Call Foo(x)` does not
- arithmetic operators `+`, `-`, `*`, `/`, `\`, `Mod`, and `^`; exponentiation uses VB precedence/associativity and is implemented through binding, runtime, code generation, and generated-program execution
- string concatenation with `&`
- `Like` and `Is` expression syntax at comparison precedence, including the current wildcard/`Option Compare` subset and runtime object-reference identity for Variant/host objects; emitted class-instance identity remains a later milestone
- VB-oriented operator precedence for the implemented expression subset
- `If`, multiline `ElseIf` / `Else`, and single-line `If ... Then ... Else`
- `For ... To ... Step ... Next` with Integer, Long, and LongLong control variables
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
- `For Each` over fixed, multidimensional, and dynamic arrays, including an implicit Variant control variable; arrays of user-defined types are rejected because VB6 rejects them too - the Variant control variable cannot hold a user-defined type declared in a standard module
- `Type ... End Type` with visibility, scalar and fixed array members, nested type names, keyword member names, and `String * n`
- `UserDefinedTypeSymbol` with case-insensitive member lookup, forward references, and Public project-wide versus Private module-local scope
- user-defined type values as locals, parameters, and module variables, including member reads and writes, member arrays, managed value-copy semantics at every value boundary - assignment, array element, member, ByVal argument and function result - including the arrays a copied value owns
- `With` blocks with implicit `.Member` access, bound through a receiver alias
- `Variant` as a semantic type with storage and explicit conversions; the current scalar runtime covers numeric `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, logical operators, comparisons, `&` concatenation, VB6 string/Variant `+`, Empty, Null propagation, and Decimal promotion, while remaining `Missing` edge cases and object/array Variants stay on the roadmap
- VB built-in string constants such as `vbCrLf`, `vbTab`, and `vbNullChar`, which user declarations of the same name still override
- the `Len`, two- and three-argument `Mid`, ASCII `Chr`, `InStr`, `InStrRev`, `Replace`, and current `Abs`/`Sgn`/`Fix`/`Round`/`Sqr` math intrinsics, plus the `CByte`/`CInt`/`CLng`/`CDec`/`CSng`/`CDbl`/`CBool`/`CStr` conversions and the `Left`/`Right`/`UCase`/`LCase`/`Trim`/`LTrim`/`RTrim`/`Asc`/`IsNumeric` string functions. Each intrinsic symbol carries the runtime method the backend calls, so it is resolved and checked like any other procedure and a user declaration of the same name still shadows it
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
- unit tests for syntax, lexer, parser, semantics, runtime, IR lowering, managed emission, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions restore/build/test workflow with a VISIA parity report on every run

The M3 array work was deliberately split into layers, and the guards from that period are gone: declarations, parameters, element access, `ReDim`/`Preserve`, `Erase`, `LBound`/`UBound`, and `For Each` are bound, emitted, and executed against `VBArray<T>`, which keeps VB6 lower bounds instead of normalizing to zero-based CLR arrays. What is still guarded is narrower and each case has its own diagnostic: `For Each` over arrays of user-defined types (`VB6S0056`), `Erase` on an array parameter (`VB6S0036`), and UDT layouts that managed lowering cannot represent yet (`VB6S0046`).

The suite currently holds **612 tests** across the test projects, and the Release build is warning-free. The current VISIA regression measurement is **309 total errors** - **196 parser**, **0 lexer**, **113 semantic** - across all 40 project items (27 modules, 6 forms, 4 user controls and 3 classes), and **16 items analyze without a single error**. VISIA is a regression corpus, not the product target. The total does not fall monotonically: teaching the parser or binder a construct can expose semantic gaps that earlier cascades hid. Cleanly analyzed items can only grow, which makes them the honest corpus metric. `docs/ROADMAP.md` keeps the measured history and current blocker ranking.

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

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, `Optional` and `ParamArray` calls, persistent `Static` locals, typed Function calls, typed comma-separated scalar variable declarators, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, `^`, Byte, Integer, Long, LongLong/Int64, Single, Double, and Currency, plus arrays, user-defined types, `With` blocks, and the current Variant subset.

The current managed project emitter supports standard modules with a single `Sub Main` and emits the managed class core: class instances, instance fields, `New`, `Set`, `TypeOf`, Properties, `Class_Initialize`/`Class_Terminate`, events, simple `WithEvents` sinks with reassignment cleanup, and `Implements` as CLR interfaces with virtual method/property dispatch. COM identity/dispatch, Forms/controls and full default-property semantics remain open. The LLVM backend currently validates x86/x64 target selection and reports unsupported IR operations; native instruction emission and a native Windows apphost remain open. The MSBuild SDK and diagnostic LSP are now available as compiler-facing integration layers.

## Next milestones

The detailed, measured plan lives in `docs/ROADMAP.md`. The immediate compiler order is:

1. finish the Variant promotion matrix and the high-frequency standard library/runtime surface
2. complete class lifecycle, object dispatch, events and COM/ActiveX compatibility beyond the current CLR-interface slice
3. replace the LLVM backend boundary diagnostics with native x86/x64 emission alongside the .NET backend
4. harden the MSBuild SDK and LSP for Visual Studio; build the IDE/designer later
