# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C#.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical.

## Current status

The first compiler frontend slice is under active development.

Implemented so far:

- .NET 10 solution and command-line compiler entry point
- source text and diagnostic infrastructure
- case-insensitive VB6 lexer with trivia preservation
- parser for the initial VB6 language subset
- semantic binder with symbols, local variables, type resolution, and explicit conversion nodes
- central `VBCompilation` analysis pipeline
- unit tests for syntax, lexer, parser, semantics, and compiler orchestration
- Codespaces development configuration
- Windows GitHub Actions build and test workflow

The current acceptance program is:

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

The next major milestone is the first code generation path from the bound program to executable .NET output.
