# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical.

## Current status

The first end-to-end compiler path is working and is being expanded feature by feature. Milestones M0 through M3 are complete. M4 (Variant) has started with Variant storage, conversions, and a first set of operators.

Implemented so far:

- .NET 10 solution and command-line compiler entry point
- source text and diagnostic infrastructure
- case-insensitive VB6 lexer with trivia preservation
- fault-tolerant parser for the current VB6 language subset
- `Sub` and typed `Function` declarations and calls
- explicit `ByRef`, explicit `ByVal`, and VB6 default-`ByRef` parameters
- `Optional` parameter syntax with explicit `ByVal`/`ByRef` and optional default expressions; omitted-argument/default-value semantics remain a later procedure milestone and are still diagnosed
- `Option Base 0` / `Option Base 1` and `Option Compare Text` / `Option Compare Binary` syntax; `Base` and `Compare` remain ordinary identifiers outside the directive context, while array-bound and string-comparison semantics remain later milestones
- VB6 Function return semantics through assignment to the function name
- cross-module Sub and Function resolution in `.vbp` projects
- typed comma-separated local and module variable declarators; each declarator has its own optional `As Type`, and declarators without one are normalized to Variant before binding
- `Static` local declaration syntax with the same per-declarator typing rules as `Dim`; persistent lifetime semantics remain a later procedure milestone and are explicitly diagnosed instead of lowering as ordinary locals
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
- `Declare Function` and `Declare Sub` syntax with `Lib`, optional `Alias`, `ByVal`/default-`ByRef` parameters, and `As Any`; native binding and P/Invoke emission remain in the interop milestone
- `Enum ... End Enum` with optional visibility plus explicit or implicit member values, bound as Long-backed constants
- `Function` declarations without an `As` clause, which return Variant as they do in VB6
- syntax for the binary file statements `Open ... For Binary As #n`, `Close`, `Get`, `Put` and `Seek`, recognized as statement words only so they stay ordinary identifiers elsewhere; the runtime behind them is not implemented yet and every occurrence is reported as `VB6S0057`
- ByRef arguments that are not variables: a literal, an expression, or a function result is passed through a temporary whose write-back is discarded, and parentheses force an argument to be passed by value, so `Foo (x)` leaves `x` untouched while `Call Foo(x)` does not
- arithmetic operators `+`, `-`, `*`, `/`, `\`, `Mod`, and `^`; exponentiation uses VB precedence/associativity and is implemented through binding, runtime, code generation, and generated-program execution
- string concatenation with `&`
- `Like` and `Is` expression syntax at comparison precedence; pattern matching/`Option Compare` and object-reference identity semantics remain later milestones and currently produce dedicated semantic diagnostics
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
- user-defined type values as locals, parameters, and module variables, including member reads and writes, member arrays, managed value-copy semantics at assignment boundaries, and code generation
- `With` blocks with implicit `.Member` access, bound through a receiver alias
- `Variant` as a semantic type with storage and explicit conversions; multiplication follows the VB6 promotion rules, `&` concatenation and a numeric equality subset are lowered, and every remaining Variant operator is reported as `VB6S0053` instead of being approximated with scalar rules
- VB built-in string constants such as `vbCrLf`, `vbTab`, and `vbNullChar`, which user declarations of the same name still override
- the `Len`, three-argument `Mid`, and ASCII `Chr` intrinsics
- bracketed identifiers such as `[Stop]`
- semantic binder with procedure, parameter, return-value, local-variable and primitive type symbols
- explicit conversion nodes and typed arithmetic promotion
- central `VBCompilation` analysis pipeline for individual source files
- `VBProjectCompilation` for combining standard modules from `.vbp` projects
- primitive `VB6.Runtime` conversion, checked Byte/Integer/Long/LongLong/Currency arithmetic, exponentiation, comparisons, Boolean operations, concatenation, and `Debug.Print`
- C# source generation from the bound scalar program
- Roslyn-based managed assembly emission
- runtime deployment files for emitted managed applications
- end-to-end execution tests for generated single-file and multi-module managed applications
- `.vbp` loading for common project metadata, modules, classes, forms, controls, references, and components
- unit tests for syntax, lexer, parser, semantics, runtime, code generation, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions restore/build/test workflow with a VISIA parity report on every run

The M3 array work was deliberately split into layers, and the guards from that period are gone: declarations, parameters, element access, `ReDim`/`Preserve`, `Erase`, `LBound`/`UBound`, and `For Each` are bound, emitted, and executed against `VBArray<T>`, which keeps VB6 lower bounds instead of normalizing to zero-based CLR arrays. What is still guarded is narrower and each case has its own diagnostic: `For Each` over arrays of user-defined types (`VB6S0056`), `Erase` on an array parameter (`VB6S0036`), and UDT layouts that managed lowering cannot represent yet (`VB6S0046`).

The suite currently holds **478 tests** across 157 test classes, and the Release build is warning-free. The current VISIA parity measurement is **832 total errors** - **218 parser**, **0 lexer**, **614 semantic** - across 27 of 40 VISIA project items. Lexer errors are gone entirely: the 62 reported ones came from `#` in file numbers and had dragged more than 200 parser errors along with them. Parser errors fell from 1214 to 480 with the `With` and member-access work, the largest single drop so far, and to 466 with untyped functions. The total rose in that last step because procedures that used to derail at their header now reach the binder in full, which is progress rather than regression. `docs/ROADMAP.md` keeps the measured history and the current blocker ranking.

Windows CI run #700 validated the array syntax slice on .NET 10 with a warning-free Release build and **258 passing tests**. Its VISIA report measures **2105 total errors**: **1644 parser**, **68 lexer**, and **393 semantic**. The array syntax slice reduces parser errors by 114 from the M2 closeout (1758 → 1644) while keeping semantic diagnostics stable. The project currently analyzes 27 of 40 VISIA project items; `.cls`, `.ctl`, and `.frm` are later milestones.

Windows CI run #662 closes the M2 parser/readability milestone with **243 passing tests** and **2219 total VISIA errors** (1758 parser, 68 lexer, 393 semantic). `Static` syntax, `^`, `Like`, and expression-level `Is` are regression-covered; unsupported `Static` lifetime, `Like` pattern matching/`Option Compare`, and `Is` object identity are guarded rather than approximated.

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

Generate C# source from one source file:

```text
vb6c Module1.bas --emit-csharp Module1.g.cs
```

Generate C# source from the standard modules in a project:

```text
vb6c LegacyApp.vbp --emit-csharp LegacyApp.g.cs
```

Generate a managed application assembly:

```text
vb6c Module1.bas --emit-assembly Module1.dll
vb6c LegacyApp.vbp --emit-assembly LegacyApp.dll
```

The managed application output currently consists of the application DLL, its `.runtimeconfig.json`, and `VB6.Runtime.dll`.

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, typed Function calls, typed comma-separated scalar variable declarators, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, `^`, Byte, Integer, Long, LongLong/Int64, Single, Double, and Currency, plus arrays, user-defined types, `With` blocks, and the current Variant subset.

The current ByRef implementation requires a variable argument with an exactly matching type. VB6 edge cases involving parenthesized expressions and temporary ByRef conversions are intentionally left for a later compatibility pass. Class modules, forms, controls, and project references are loaded by the project system but are not compiled into the output yet. A native Windows apphost `.exe` is also a later compiler milestone.

## Next milestones

The detailed, measured plan lives in `docs/ROADMAP.md`. The immediate order is:

1. finish M4 Variant - `VarType`, `IsEmpty`/`IsNull`/`IsNumeric`, `Empty`/`Null`/`Nothing`/`Missing`, and the full operator promotion matrix in the binder itself rather than in post-binding correction passes
2. M5 procedures and classes - `Optional` defaults, `ParamArray`, `Static` lifetime, `Property Get`/`Let`/`Set`, class modules, and `.cls` as a project source

After that: lowered IR and error handling, the standard library, native and COM interop, Forms/UserControls, and finally the IDE.
