# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical.

## Current status

The first end-to-end compiler path is working and is being expanded feature by feature.

Implemented so far:

- .NET 10 solution and command-line compiler entry point
- source text and diagnostic infrastructure
- case-insensitive VB6 lexer with trivia preservation
- fault-tolerant parser for the current VB6 language subset
- `Sub` and typed `Function` declarations and calls
- explicit `ByRef`, explicit `ByVal`, and VB6 default-`ByRef` parameters
- VB6 Function return semantics through assignment to the function name
- cross-module Sub and Function resolution in `.vbp` projects
- `Dim` locals for the current primitive type subset
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
- arithmetic operators `+`, `-`, `*`, `/`, `\`, and `Mod`
- string concatenation with `&`
- VB-oriented operator precedence for the implemented expression subset
- `If`, multiline `ElseIf` / `Else`, and single-line `If ... Then ... Else`
- `For ... To ... Step ... Next` with Integer, Long, and LongLong control variables
- `While ... Wend`
- pre-test and post-test `Do While`, `Do Until`, `Loop While`, and `Loop Until`
- unconditional `Do ... Loop`
- `Exit For` and `Exit Do` with bound loop targets for nested-loop correctness
- `Select Case` with value lists, ranges, relational clauses, and `Case Else`
- semantic binder with procedure, parameter, return-value, local-variable and primitive type symbols
- explicit conversion nodes and typed arithmetic promotion
- central `VBCompilation` analysis pipeline for individual source files
- `VBProjectCompilation` for combining standard modules from `.vbp` projects
- primitive `VB6.Runtime` conversion, checked Byte/Integer/Long/LongLong/Currency arithmetic, comparisons, Boolean operations, concatenation, and `Debug.Print`
- C# source generation from the bound program
- Roslyn-based managed assembly emission
- runtime deployment files for emitted managed applications
- end-to-end execution tests for generated single-file and multi-module managed applications
- `.vbp` loading for common project metadata, modules, classes, forms, controls, references, and components
- unit tests for syntax, lexer, parser, semantics, runtime, code generation, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions restore/build/test workflow

Windows CI run #422 validates the current `Long` implementation on .NET 10. It builds the complete solution, runs the full regression suite, verifies Integer/Long promotion rules, and executes a generated managed application whose Long arithmetic and Long `For` loop produce `60003`.

`Mod` is also end-to-end verified by Windows CI run #394, including parser precedence, binding, runtime behavior, C# generation, and execution of a generated application.

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

For modern code that needs a signed 64-bit integer, VB6Compiler provides `LongLong` with `Int64` as an alias. This is a compiler extension and does not change the size of VB6 `Long`:

```vb
Sub Main()
    Dim value As Int64
    Dim i As LongLong

    value = 3000000000

    For i = 1 To 3
        value = value + 1000000000
    Next i

    Debug.Print value
End Sub
```

The generated application uses .NET `System.Int64` and prints `6000000000`.

`Byte` is emitted as .NET `System.Byte` and keeps the unsigned VB range from 0 through 255. Conversions and Byte arithmetic use checked runtime helpers so out-of-range results fail instead of silently wrapping.

`Currency` uses a dedicated scaled 64-bit runtime value instead of binary floating point. Currency literals use the VB `@` suffix, arithmetic keeps four decimal places, and assignment/conversion paths use Banker's rounding:

```vb
Sub Main()
    Dim amount As Currency
    amount = 1.2345@
    amount = amount * 1.2345@
    Debug.Print amount
End Sub
```

The current structured-control-flow and cross-module acceptance project also exercises loops, `Select Case`, extended `If`, Boolean expressions, default ByRef, explicit ByVal, and typed Function calls.

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

The report lists the project items by kind and marks the kinds that are not read yet, counts
how many source files analyze without errors, and ranks the remaining gaps by the number of
files each one affects. Raw diagnostic counts are deliberately not the headline number: one
unsupported construct derails the parser for the rest of the file, so the cascade would drown
out the gaps that matter.

Generate C# source from one source file:

```text
vb6c Module1.bas --emit-csharp Module1.g.cs
```

Generate C# source from the standard modules in a project:

```text
vb6c LegacyApp.vbp --emit-csharp LegacyApp.g.cs
```

Generate a managed application assembly from one source file:

```text
vb6c Module1.bas --emit-assembly Module1.dll
```

Generate one managed application assembly from the standard modules in a project:

```text
vb6c LegacyApp.vbp --emit-assembly LegacyApp.dll
```

The managed application output currently consists of:

```text
LegacyApp.dll
LegacyApp.runtimeconfig.json
VB6.Runtime.dll
```

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, typed Function calls, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, Byte, Integer, Long, LongLong/Int64, Single, Double, and Currency.

The current ByRef implementation requires a variable argument with an exactly matching type. VB6 edge cases involving parenthesized expressions and temporary ByRef conversions are intentionally left for a later compatibility pass. Class modules, forms, controls, and project references are loaded by the project system but are not compiled into the output yet. A native Windows apphost `.exe` is also a later compiler milestone.

## Next milestones

The order below is derived from a construct-frequency analysis over a real VB6
codebase rather than from a generic VB6 feature list. See `docs/ROADMAP.md`.

- `Const`, `Option Explicit` enforcement, multi-declarator `Dim`, and `Enum`
- arrays and `Type ... End Type`, including `ReDim Preserve`
- the first `Variant` representation
- `Optional`, `ParamArray`, `Property Get`/`Let`/`Set`, and class modules
- a dedicated lowered IR, then `GoTo`, `On Error`, and the `Err` object
- the string and binary file I/O parts of the VB6 standard library
- `Declare` marshalling and COM consumption
- `.frm` project item parsing and a VB6-compatible forms runtime
