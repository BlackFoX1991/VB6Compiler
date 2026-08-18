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
- semantic binder with symbols, local variables, type resolution, and explicit conversion nodes
- central `VBCompilation` analysis pipeline
- primitive VB6 runtime helpers for conversions, integer arithmetic, comparisons, concatenation, and `Debug.Print`
- C# source generation from the bound program
- Roslyn-based managed assembly emission
- runtime deployment files for emitted managed applications
- end-to-end execution tests for generated managed applications
- `.vbp` project loading for common project metadata, modules, classes, forms, controls, references, and components
- unit tests for syntax, lexer, parser, semantics, runtime, code generation, project loading, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions build and test workflow

## Current acceptance program

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

## Command line

Analyze a VB6 source file:

```text
vb6c Module1.bas
```

Inspect a VB6 project file:

```text
vb6c LegacyApp.vbp
```

Generate C# source:

```text
vb6c Module1.bas --emit-csharp Module1.g.cs
```

Generate a managed application assembly and its runtime support files:

```text
vb6c Module1.bas --emit-assembly Module1.dll
```

The managed application output currently consists of:

```text
Module1.dll
Module1.runtimeconfig.json
VB6.Runtime.dll
```

Project-level emission from `.vbp` files and a native Windows apphost `.exe` are not generated yet. These are later compiler milestones.

## Next milestones

- compile standard modules from a loaded `.vbp` project as one compilation
- introduce a dedicated lowered IR and control flow representation
- expand the VB6 type system and runtime behavior
- add class modules and cross-file symbol resolution
- begin `.frm` project item parsing
