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
- unit tests for syntax, lexer, parser, semantics, runtime, code generation, and compiler orchestration
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

A native Windows apphost `.exe` is not generated yet. That is a later backend milestone.

## Next milestones

- validate the complete solution in Windows CI and Codespaces
- execute generated managed assemblies in end-to-end tests
- introduce a dedicated lowered IR and control flow representation
- expand the VB6 type system and runtime behavior
- add `.vbp` project loading
