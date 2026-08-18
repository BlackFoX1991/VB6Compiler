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
- 16-bit VB6 `Integer` and 32-bit VB6 `Long`
- Integer-to-Long numeric promotion without incorrectly promoting pure Integer expressions from their assignment target
- `True` and `False`, including VB numeric `True = -1` conversion behavior
- logical operators `Not`, `And`, `Or`, `Xor`, `Eqv`, and `Imp`
- arithmetic operators `+`, `-`, `*`, `/`, `\`, and `Mod`
- string concatenation with `&`
- VB-oriented operator precedence for the implemented expression subset
- `If`, multiline `ElseIf` / `Else`, and single-line `If ... Then ... Else`
- `For ... To ... Step ... Next` with Integer and Long control variables
- `While ... Wend`
- pre-test and post-test `Do While`, `Do Until`, `Loop While`, and `Loop Until`
- unconditional `Do ... Loop`
- `Exit For` and `Exit Do` with bound loop targets for nested-loop correctness
- `Select Case` with value lists, ranges, relational clauses, and `Case Else`
- semantic binder with procedure, parameter, return-value, local-variable and primitive type symbols
- explicit conversion nodes and typed arithmetic promotion
- central `VBCompilation` analysis pipeline for individual source files
- `VBProjectCompilation` for combining standard modules from `.vbp` projects
- primitive `VB6.Runtime` conversion, checked Integer/Long arithmetic, comparisons, Boolean operations, concatenation, and `Debug.Print`
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

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub and Function calls, the current ByRef/ByVal subset, typed Function calls, structured loops, extended If branching, Boolean expressions, `Select Case`, `Mod`, Integer, and Long.

The current ByRef implementation requires a variable argument with an exactly matching type. VB6 edge cases involving parenthesized expressions and temporary ByRef conversions are intentionally left for a later compatibility pass. Class modules, forms, controls, and project references are loaded by the project system but are not compiled into the output yet. A native Windows apphost `.exe` is also a later compiler milestone.

## Next milestones

- add `Single` and complete the first floating-point literal/promotion slice
- broaden `Double` arithmetic compatibility
- add `Byte`, followed by `Currency`, `Date`, and the first `Variant` representation
- expand ByRef coercion and parenthesized-argument edge cases
- introduce a dedicated lowered IR and control flow representation
- add class modules
- begin `.frm` project item parsing
