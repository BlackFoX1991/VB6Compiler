# VB6Compiler

VB6Compiler is an experimental Visual Basic 6 compatible compiler written in C# targeting .NET 10.

The long-term goal is to compile existing VB6 projects to modern .NET executables and libraries while preserving VB6 language and runtime behavior as closely as practical. Modern extensions are additive: they must not change the behavior of existing VB6 code.

## Current status

The first end-to-end compiler path is working and the project is now driven primarily by measured compatibility gaps in real VB6 code rather than by a strictly linear milestone sequence.

At the current documentation head, Windows CI validates:

- warning-free Release build on .NET 10
- **494 passing tests, 0 failed, 0 skipped**
- VISIA 4.8.7.1: **27 of 40 project items analyzed**
- VISIA: **1318 total compiler errors**
  - **92 parser**
  - **0 lexer**
  - **1226 semantic**
- the largest remaining diagnostic families are ByRef compatibility, unresolved procedures, and unresolved variables

The current VISIA lexer frontier is therefore closed. Most remaining work is semantic/runtime compatibility, with a much smaller parser tail.

## Implemented so far

### Compiler pipeline

- source text, diagnostics, trivia-preserving case-insensitive lexer, and fault-tolerant parser
- semantic binder with procedure, parameter, return-value, local/module-variable, array, Enum, Variant, and UDT type models
- central `VBCompilation` and project-wide `VBProjectCompilation` analysis pipelines
- C# generation, Roslyn managed assembly emission, runtime deployment files, and generated-program execution tests
- `.vbp` loading for common project metadata, modules, classes, forms, controls, references, components, and standard bracketed project sections
- Windows GitHub Actions restore/build/test workflow with VISIA parity reporting and preserved test artifacts

### VB6 language and scalar runtime

- `Sub` and `Function`, cross-module calls, VB6 Function return assignment, explicit/default `ByRef`, and explicit `ByVal`
- VB6 `Byte`, 16-bit `Integer`, 32-bit `Long`, `Single`, `Double`, and scaled `Currency`
- additive signed 64-bit `LongLong` / `Int64` extension without changing VB6 `Long`
- checked arithmetic and VB-oriented promotion, including preservation of pure-Integer overflow behavior
- `True` / `False`, logical and bitwise `Not`, `And`, `Or`, `Xor`, `Eqv`, and `Imp`
- `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, comparisons, and string concatenation
- `&H` / `&O` literals, numeric and identifier type suffixes, line continuation, and `:` statement separators
- `If` / `ElseIf` / `Else`, `Select Case`, numeric `For`, `While`, all current `Do` / `Loop` forms, `Exit For`, `Exit Do`, `Exit Sub`, and `Exit Function`
- module variables, `Const`, comma-separated declarators with VB6 per-declarator typing, `Attribute` lines, and visibility modifiers
- `Declare Function` / `Declare Sub` syntax with `Lib`, contextual `Alias`, optional `Alias`, `As Any`, and VB6 parameter syntax
- `Option Base 0/1` and `Option Compare Text/Binary` syntax; `Option Base` is wired into array bounds
- `Optional` parameter syntax and `Static` local syntax with explicit diagnostics for semantics that are not implemented yet
- `Like` and expression-level `Is` syntax with dedicated semantic boundaries
- bracketed identifiers such as `[End]`

### Arrays and user-defined types

- fixed, explicitly bounded, multidimensional, and dynamic VB6 arrays
- array parameters and project-wide array binding
- `VBArray<T>` storage preserving rank and lower/upper bounds
- array reads/writes, `Option Base`, ByRef array elements, `ReDim`, `ReDim Preserve`, `Erase`, `LBound`, and `UBound`
- `For Each` over fixed arrays, dynamic/unknown-rank arrays, array parameters, and array-valued members
- `Type ... End Type` with Public/Private scope, nested UDT identities, fixed-length `String * n`, and fixed/dynamic array members in the semantic model
- UDT variables, parameters, function returns, managed storage, member reads/writes, `With`, ByRef members, and value-copy lowering
- fixed primitive UDT array members, including copy-safe generated backing storage

Some UDT layouts remain deliberately guarded, including dynamic array members, fixed-length-String array members, arrays of UDT elements, and recursive by-value layouts.

### Variant and standard environment

- explicit `Variant` storage for locals, arrays, ByVal parameters, and Function returns
- implicit Variant lowering for untyped local/module/Static declarations and untyped Function returns
- array `For Each` control variables using Variant value semantics
- current corpus-reachable Variant multiplication, a limited numeric equality slice, and bound String concatenation through `&`
- Long-backed Enum type aliases and module-level Enum member constants
- built-in VB/VBA String constants including `vbCrLf`, `vbCr`, `vbLf`, `vbNewLine`, `vbTab`, `vbBack`, `vbFormFeed`, `vbVerticalTab`, `vbNullChar`, and `vbNullString`
- corpus-reachable `Len`, three-argument `Mid` / `Mid$`, and ASCII `Chr`

## Parsed but intentionally guarded

VB6Compiler does not silently approximate behavior that is not implemented yet. The current frontend preserves several constructs and then emits a dedicated semantic diagnostic:

- classic file-number and file-I/O syntax (`Open`, `Get`, `Put`, `Close`, `Seek`, `Input`, `Write`, `Kill`, `Print #`) -> `VB6S0057` until runtime semantics are wired
- `TypeOf ... Is ...` -> `VB6S0058` until the object/class model can implement it correctly
- call-site `ByVal` such as `CopyMemory x, ByVal y, 4` -> `VB6S0059` until VB6 temporary/ByRef call semantics are implemented
- `Static` local lifetime, full `Like` / `Option Compare` behavior, and object-reference `Is` remain explicit compatibility boundaries
- the full Variant operator/state matrix (`Null`, `Nothing`, `Missing`, broader promotions/comparisons, `VarType`, and related library behavior) remains incomplete

## Compatibility examples

VB6Compiler keeps VB6 `Integer` as a signed 16-bit type and `Long` as a signed 32-bit type. Pure Integer expressions are not promoted merely because the destination is Long:

```vb
Dim value As Long
value = 2000 * 365
```

The multiplication is still Integer arithmetic before the assignment conversion, preserving VB6 overflow behavior.

VB6 per-declarator typing is also preserved. In:

```vb
Dim a, b As Integer
```

`a` is Variant while only `b` is Integer.

Arrays preserve VB6 lower bounds instead of being normalized to zero-based CLR arrays:

```vb
Option Base 1

Sub Main()
    Dim values(3) As Long
    Dim dynamicValues() As String

    values(1) = 10
    ReDim dynamicValues(2 To 4)
    Debug.Print LBound(values), UBound(values)
End Sub
```

UDTs use managed value semantics rather than reference-like CLR approximations:

```vb
Private Type Point
    X As Long
    Y As Long
End Type

Sub Main()
    Dim p As Point
    With p
        .X = 10
        .Y = 20
    End With
End Sub
```

For modern code that needs a signed 64-bit integer, VB6Compiler provides `LongLong` with `Int64` as an alias. This extension does not change the size or semantics of VB6 `Long`.

## Command line

Analyze a VB6 source file:

```text
vb6c Module1.bas
```

Inspect a VB6 project:

```text
vb6c LegacyApp.vbp
```

Measure project compatibility:

```text
vb6c LegacyApp.vbp --report
```

Generate C#:

```text
vb6c Module1.bas --emit-csharp Module1.g.cs
vb6c LegacyApp.vbp --emit-csharp LegacyApp.g.cs
```

Generate a managed application assembly:

```text
vb6c Module1.bas --emit-assembly Module1.dll
vb6c LegacyApp.vbp --emit-assembly LegacyApp.dll
```

The managed output currently consists of the application DLL, its `.runtimeconfig.json`, and `VB6.Runtime.dll`.

## Current project scope

Project emission currently compiles standard `.bas` modules with a single `Sub Main` entry point and the implemented language/runtime subset above. Class modules, Forms, UserControls, COM references, native P/Invoke lowering, and a native Windows apphost remain later milestones even though the project loader already inventories those project items.

The current ByRef implementation still requires an addressable argument of the expected type for ordinary ByRef binding. Parenthesized/temporary conversion cases and call-site `ByVal` semantics are tracked as explicit procedure-compatibility work rather than being approximated.

## Next work

The measured plan is maintained in `docs/ROADMAP.md`. The immediate frontier is now:

1. reduce project-wide symbol fallout from modules that still contain parser errors
2. implement the high-frequency ByRef/Optional-call compatibility cases
3. finish the remaining parser tail without reintroducing lexer cascades
4. expand the corpus-driven standard library and file-I/O runtime
5. continue classes, error handling/IR, interop, Forms/UserControls, and finally the IDE

`conformance/` contains real third-party VB6 projects used as measurement input; see `conformance/README.md`.