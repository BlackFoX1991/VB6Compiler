# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical.

## Current status

The first end-to-end compiler path is working and is being expanded feature by feature. Milestones M0 through M2 are complete; M3 (arrays and UDTs) is in progress.

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
- typed comma-separated local and module variable declarators; each declarator has its own optional `As Type`, while omitted types remain implicit Variant and diagnose until the Variant milestone
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
- `Enum ... End Enum` syntax with optional visibility plus explicit or implicit member values; enum type binding remains a later language milestone
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

The current M3 array work is deliberately split into layers. Array declarations and array parameters are parsed without losing rank or bounds, and `ArrayTypeSymbol` plus `VBArray<T>` are tested foundations. **Array variables and parameters are still stopped in the binder with `VB6S0025`** until fixed/dynamic array binding, `Option Base`, array element access/assignment, and code generation are connected. This prevents a CLR-array approximation from silently changing VB6 lower-bound behavior.

Windows CI run #700 validates the current reference head on .NET 10 with a warning-free Release build and **258 passing tests**. Its VISIA report measures **2105 total errors**: **1644 parser**, **68 lexer**, and **393 semantic**. The array syntax slice reduces parser errors by 114 from the M2 closeout (1758 → 1644) while keeping semantic diagnostics stable. The project currently analyzes 27 of 40 VISIA project items; `.cls`, `.ctl`, and `.frm` are later milestones.

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

VB6 variable types are attached to individual declarators, not to the whole comma-separated declaration. For example, in `Dim a, b As Integer`, only `b` is Integer; `a` is Variant. VB6Compiler preserves that distinction now. Until Variant is implemented, the untyped `a` is diagnosed instead of being silently treated as Integer.

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

These declarations are currently represented faithfully in syntax, and the dedicated `VBArray<T>` runtime can represent their bounds. Binding and generated-program use of arrays is the next M3 slice.

Exponentiation is kept distinct from integer arithmetic. The compiler preserves VB precedence and evaluates repeated powers from left to right, so the generated acceptance program verifies `-2 ^ 2 = -4`, `3 ^ 3 ^ 3 = 19683`, and `2 ^ -3 = 0.125`.

For modern code that needs a signed 64-bit integer, VB6Compiler provides `LongLong` with `Int64` as an alias. This is a compiler extension and does not change the size of VB6 `Long`.

`Byte` is emitted as .NET `System.Byte` and keeps the unsigned VB range from 0 through 255. Conversions and Byte arithmetic use checked runtime helpers so out-of-range results fail instead of silently wrapping.

`Currency` uses a dedicated scaled 64-bit runtime value instead of binary floating point. Currency literals use the VB `@` suffix, arithmetic keeps four decimal places, and assignment/conversion paths use Banker's rounding.

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

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, typed Function calls, typed comma-separated scalar variable declarators, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, `^`, Byte, Integer, Long, LongLong/Int64, Single, Double, and Currency. Array syntax is accepted but array binding/emission is not yet enabled.

The current ByRef implementation requires a variable argument with an exactly matching type. VB6 edge cases involving parenthesized expressions and temporary ByRef conversions are intentionally left for a later compatibility pass. Class modules, forms, controls, and project references are loaded by the project system but are not compiled into the output yet. A native Windows apphost `.exe` is also a later compiler milestone.

## Next milestones

The detailed, measured plan lives in `docs/ROADMAP.md`. The immediate M3 order is:

1. bind fixed/dynamic arrays and array parameters using `ArrayTypeSymbol`
2. apply `Option Base` only to dimensions without an explicit lower bound
3. bind and emit array element reads/writes against `VBArray<T>`
4. add `ReDim` / `ReDim Preserve`, `Erase`, `LBound`/`UBound`, and `For Each`
5. add `Type ... End Type`, then member access / `With`

After M3: Variant, procedure/class semantics, lowered IR and error handling, standard library, native/COM interop, Forms/UserControls, and finally the IDE.
