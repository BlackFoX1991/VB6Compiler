# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical.

## Current status

The first end-to-end compiler slice is under active development.

Implemented so far:

- .NET 10 solution and command-line compiler entry point
- source text and diagnostic infrastructure
- case-insensitive VB6 lexer with trivia preservation
- parser for the initial VB6 language subset
- Sub calls using bare-call and `Call ...(...)` syntax with argument lists
- Sub parameters with explicit `ByRef`, explicit `ByVal`, and VB6 default-`ByRef` behavior
- typed `Function ... As Type` declarations with parameters and `End Function`
- Function invocation expressions such as `result = Add(5, 7)`
- VB6 Function return semantics through assignment to the function name
- semantic binder with procedure, parameter, return-value, local-variable and type symbols, explicit conversion nodes, and invocation binding
- ByRef argument validation for variable arguments and exact type matching in the current compiler subset
- ByVal argument conversion through the VB6 conversion layer
- central `VBCompilation` analysis pipeline for individual source files
- `VBProjectCompilation` for combining standard modules from `.vbp` projects
- project-wide Sub and Function declaration with case-insensitive cross-module symbol resolution
- project-level duplicate procedure detection across standard modules
- primitive VB6 runtime helpers for conversions, integer arithmetic, comparisons, concatenation, and `Debug.Print`
- C# source generation from the bound program, including `ref` parameters, Function return slots, and invocation expressions
- Roslyn-based managed assembly emission
- runtime deployment files for emitted managed applications
- end-to-end execution tests for generated single-file and multi-module managed applications
- `.vbp` project loading for common project metadata, modules, classes, forms, controls, references, and components
- unit tests for syntax, lexer, parser, semantics, runtime, code generation, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions build and test workflow

Windows CI run #278 validates the current Function and parameter implementation on .NET 10. The end-to-end project test compiles multiple standard modules into one assembly, applies default-ByRef and explicit-ByVal semantics, calls a Function from another module, executes the generated assembly, and verifies the final output.

## Current acceptance program

Single-file source:

```vb
Option Explicit

Sub Main()
    Dim x As Integer
    x = 10

    If x > 5 Then
        Debug.Print x
    End If
End Sub
```

Multi-module project:

```vb
' MainModule.bas
Sub Main()
    Dim x As Integer
    x = 5
    Call Update(x)
    Call Observe(x)
    x = Add(x, 2)
    Debug.Print x
End Sub
```

```vb
' HelperModule.bas
Sub Update(value As Integer)
    value = 10
End Sub

Sub Observe(ByVal value As Integer)
    value = 20
End Sub

Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
    Add = left + right
End Function
```

The output is `12`: `Update` receives `value` ByRef by default and changes the caller to 10, `Observe` receives a ByVal copy, and the cross-module `Add` Function returns 12.

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

Project emission currently supports standard `.bas` modules with a single `Sub Main` entry point, cross-module Sub calls, the first ByRef/ByVal parameter subset, and typed Function calls. The current ByRef implementation requires a variable argument with an exactly matching type; VB6 edge cases involving parenthesized expressions and temporary ByRef conversions are intentionally left for a later compatibility pass. Class modules, forms, controls, and project references are loaded by the project system but are not compiled into the output yet. A native Windows apphost `.exe` is also a later compiler milestone.

## Next milestones

- add `For`, `While`, and `Do` control-flow statements
- add `Select Case`
- expand ByRef coercion and parenthesized-argument edge cases
- introduce a dedicated lowered IR and control flow representation
- expand the VB6 type system and runtime behavior
- add class modules
- begin `.frm` project item parsing
