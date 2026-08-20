# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical.

## Current status

The first end-to-end compiler path is working and is being expanded feature by feature. Milestones M0 through M2 are complete; the core M3 arrays/UDTs work is wired, and M4 Variant support is in progress.

Implemented so far:

- .NET 10 solution and command-line compiler entry point
- source text and diagnostic infrastructure
- case-insensitive VB6 lexer with trivia preservation
- fault-tolerant parser for the current VB6 language subset
- `Sub` and typed `Function` declarations and calls
- explicit `ByRef`, explicit `ByVal`, and VB6 default-`ByRef` parameters
- parenthesized and call-site `ByVal` ByRef arguments in statement calls lower to temporary variables, preserving VB6 no-copyback behavior; scalar ByRef type mismatches use converted temporaries with copyback; exact UDT fields and array elements alias through `ref`
- `Optional` parameter syntax with explicit `ByVal`/`ByRef` and optional default expressions; omitted `ByVal` optionals use their default expression or `Missing`, and omitted `ByRef` optionals in statement calls use temporary defaults
- `ParamArray` parameters with variable rest arguments lowered to zero-based `VBArray<T>` instances, including empty calls
- `Option Base 0` / `Option Base 1` and `Option Compare Text` / `Option Compare Binary` syntax; `Base` and `Compare` remain ordinary identifiers outside the directive context, while array-bound and string-comparison semantics remain later milestones
- VB6 Function return semantics through assignment to the function name
- cross-module Sub and Function resolution in `.vbp` projects
- typed or implicit-Variant comma-separated local and module variable declarators; each declarator has its own optional `As Type`
- first-class `VBVariant` storage for untyped local/module declarators and untyped parameters, initialized as `Empty` and wrapping assigned primitive values
- Variant literals `Empty`, `Null`, and `Nothing`, plus `VarType`, `IsEmpty`, `IsNull`, `IsMissing`, and `IsNumeric`, which accepts numeric strings, `&H`/`&O` literals, `Boolean` and `Empty`; VB6 has no `Missing` literal, so the word stays an ordinary identifier and the missing state is only reachable through an omitted optional argument
- first Variant operator dispatch for unary `-`/`Not`, arithmetic `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, concatenation `&`, and comparison operators over wrapped primitive values; Variant comparisons preserve `Null`, and `CBool(Null)` fails instead of silently becoming `False`; Variant `+` adds as soon as one operand is numeric and only concatenates when neither is, with `Empty` staying on the string side
- Variant `And`, `Or`, `Xor`, `Eqv`, and `Imp` over numeric bitwise values plus Boolean Null tri-state behavior
- first Error-Variant slice with `CVErr`, `IsError`, `VarType` 10, and blocked primitive conversions from error variants
- additive first-class `Decimal` support via `As Decimal`, `CDec`, checked decimal arithmetic, decimal division, code generation, and Variant `VarType` 14
- `Static` local declarations with the same per-declarator typing rules as `Dim`; generated programs preserve their values across procedure calls by lowering them to procedure-scoped static fields
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
- `Enum ... End Enum` syntax with optional visibility plus explicit or implicit member values, semantic enum types, and enum member constants
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
- `VBArray<T>` runtime storage that preserves explicit lower/upper bounds, rank, indexing checks, `Clear`, and `LBound`/`UBound`
- binding and C# emission for fixed and dynamic arrays, array parameters, `Option Base` implicit lower bounds, array element reads/writes, `ReDim`, `ReDim Preserve`, `Erase`, `LBound`, `UBound`, and array `For Each`
- `Type ... End Type` declarations with reserved words allowed as field names (`Property As Boolean`), scalar fields, fixed `String * n` truncation/padding for UDT fields, generated sequential layout metadata, `ByValTStr` metadata for constant fixed-string fields, fixed array fields, UDT variables, member reads/writes, nested member assignment, member array element access, UDT value copies, UDT `ByVal` copies, and `With ... End With` implicit member access for UDT targets
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

The current M3/M4/M5 work is deliberately split into layers. Arrays are wired from parser through binder, runtime, C# emission, and CLI smokes. The UDT slice now covers `Type ... End Type`, scalar and fixed-array fields, `String * n` field truncation/padding, generated sequential layout metadata, `ByValTStr` metadata for constant fixed-string fields, nested member reads/writes, member array element access, UDT value copies, UDT `ByVal` copies, and UDT `With` blocks. The Variant slice covers storage, literals, builtins, untyped declarations/parameters, primitive unary/binary operator dispatch, Variant comparison and Boolean-logical Null propagation, the first Error-Variant support, and additive Decimal support. The procedure/class-source slice covers omitted `Optional ByVal` defaults, basic `ParamArray` rest-argument lowering, persistent `Static` locals, project-wide UDT/Enum declared-type lookup, project class module type names, the `Object` and `OLE_COLOR` aliases, `.cls` designer metadata, `Property Get`/`Let`/`Set` accessor bodies, event declarations, and `RaiseEvent` statements as analysis-safe placeholders. Full native/COM interop, deeper object/class interaction, property dispatch, event dispatch, and remaining VB6 Variant edge semantics remain later compatibility work.

Windows CI run #700 validates the reference head on .NET 10 with a warning-free Release build and **258 passing tests**. Its 27-item VISIA report measured **2105 total errors**: **1644 parser**, **68 lexer**, and **393 semantic**. The array syntax slice reduced parser errors by 114 from the M2 closeout (1758 → 1644) while keeping semantic diagnostics stable. The current project analysis reads `.bas` and `.cls` sources, covering 30 of 40 VISIA project items; `.ctl` and `.frm` are later milestones.

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

VB6 variable types are attached to individual declarators, not to the whole comma-separated declaration. For example, in `Dim a, b As Integer`, only `b` is Integer; `a` is Variant. VB6Compiler preserves that distinction and emits the untyped `a` as `VBVariant`.

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

These declarations are represented faithfully in syntax and lowered through the dedicated `VBArray<T>` runtime when they are used in generated programs.

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

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, `Optional ByVal` defaults, `ParamArray` rest arguments, persistent `Static` locals, typed Function calls, typed and implicit-Variant comma-separated scalar variable declarators, semantic Enum declarations lowered to integer-backed fields/constants, first Variant operator dispatch, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, `^`, Byte, Integer, Long, LongLong/Int64, Single, Double, Currency, and `VBArray<T>`-backed fixed/dynamic arrays.

The current ByRef implementation accepts exact variable arguments, exact UDT field and array element aliases, parenthesized arguments, call-site `ByVal` arguments, omitted optional `ByRef` arguments, scalar type-mismatch temporaries with copyback, and function-call expression temporaries lowered through scoped temps. Remaining ByRef edge cases focus on object-model aliasing and non-scalar copyback rules. Class modules are read as analysis sources and registered as project-wide type names, including designer metadata, property accessor bodies, events, and `RaiseEvent`; class-typed values currently emit as neutral `object?` placeholders until the real class runtime exists. Forms, controls, and project references are loaded by the project system but are not compiled into the output yet. A native Windows apphost `.exe` is also a later compiler milestone.

## Next milestones

The detailed, measured plan lives in `docs/ROADMAP.md`. The current follow-up is:

1. continue M5 procedure/class semantics: property dispatch, object-model aliasing, `Set`/`New`, and non-scalar ByRef copyback
2. finish M4/M5 object work together: object variants, `Is` identity, and deeper error-variant behavior

After M4: procedure/class semantics, lowered IR and error handling, standard library, native/COM interop, Forms/UserControls, and finally the IDE.
