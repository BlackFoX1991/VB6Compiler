# VB6Compiler

VB6-kompatibler Compiler in C#, der bestehende VB6-Projekte nach .NET 10 übersetzt.

## Ziel

1. **Vollständige VB6-Parität.** Alles, was die Original-IDE/Runtime kann, soll der Compiler können.
2. **Legacy-Projekte kompilieren unverändert.** Ein altes `.vbp` wird ohne Quelltextänderung übersetzt. Das ist das Akzeptanzkriterium für jedes Feature.
3. **Moderne Erweiterungen obendrauf** — 64 Bit, breitere Integer-Typen, echtes `Decimal`, bessere Fließkommatypen.
4. **Später:** eigene IDE mit WinForms-Designer.

Detaillierte Reihenfolge und offene Architekturentscheidungen: `docs/ROADMAP.md`.

## Die eine Regel, die alles andere schlägt

**Moderne Erweiterungen dürfen VB6-Semantik niemals verändern — sie kommen additiv dazu.**

`LongLong`/`Int64` ist der Präzedenzfall: 64-Bit-Integer wurde ergänzt, ohne dass VB6-`Long` aufhört, 32 Bit zu sein. Genauso läuft jede weitere Erweiterung. Wenn eine Bequemlichkeit für neuen Code die Semantik für alten Code verschiebt, ist sie falsch gebaut.

Daraus folgende Invarianten — nicht ohne ausdrückliche Entscheidung antasten:

- `Integer` ist signed 16 Bit. `Long` ist signed 32 Bit.
- Arithmetik ist `checked`. VB6-Overflow ist beobachtbares Verhalten, kein Bug.
- Reine `Integer`-Ausdrücke werden **nicht** promoted, nur weil das Zuweisungsziel breiter ist (`value = 2000 * 365` überläuft, auch wenn `value As Long`).
- `Currency` ist skalierter Int64 mit vier Nachkommastellen und Banker's Rounding.
- Bezeichner sind case-insensitiv, Trivia bleibt im Lexer erhalten.
- Wo VB6-Verhalten (noch) nicht implementiert ist: **Diagnostic mit Code melden**, nicht stillschweigend etwas Ähnliches tun. Siehe `VB6S0018` für bitweise Operatoren.

## Architektur

```
VB6.Syntax        SourceText, Tokens, Trivia, Syntaxknoten, Diagnostics
VB6.Lexer         case-insensitiv, Trivia-erhaltend
VB6.Parser        fehlertolerant -> ParseResult
VB6.Semantics     Binder: Syntax -> Symbole + Bound Tree (typisiert)
VB6.CodeGen.CSharp Bound Tree -> C#-Quelltext -> Roslyn -> Assembly
VB6.Runtime       VB6-Laufzeitsemantik (Arithmetik, Konvertierung, VBCurrency)
VB6.ProjectSystem .vbp laden
VB6.Compiler      VBCompilation / VBProjectCompilation: Pipeline
VB6.Compiler.Cli  vb6c
```

Schichtgrenzen sind hart: Der Binder kennt kein C#, der Generator keine Syntaxknoten. Beim Erweitern nicht abkürzen.

`VB6.Runtime` ist die einzige Stelle für VB6-Verhalten zur Laufzeit. Wenn generierter C#-Code VB6-Semantik selbst nachbaut statt die Runtime zu rufen, gehört es in die Runtime.

## Arbeitsweise pro Feature

Die Historie folgt einem festen Muster — bitte fortführen, es hält die Codebasis bei dieser Feature-Breite handhabbar. Ein Feature wandert schichtweise durch den Stack, **ein Commit pro Schicht**:

```
Recognize/Add <X> keyword      Lexer-Token, SyntaxKind
Parse <X>                      Parser + Syntaxknoten
Bind <X>                       Binder, Symbole, Konvertierungen
Add <X> runtime operations     VB6.Runtime
Generate/Emit <X>              CSharpGenerator
Test <X> lexing                \
Test <X> binding                | je ein Commit pro Testschicht
Test <X> runtime semantics      |
Test <X> code generation        |
Test <X> end to end            /
Document <X> support           README
```

Commit-Betreffs: imperativ, kurz, kein Präfix, kein Punkt (`Bind Currency arithmetic`). Die bestehende Historie nutzt keine Co-Authored-By-Trailer.

**Jedes Sprachfeature braucht einen End-to-End-Test**, der ein Programm generiert, ausführt und die Ausgabe prüft — nicht nur Binder-Assertions. Vorbild: `tests/VB6.Compiler.Tests/CurrencyExecutionTests.cs`.

## Build und Test

```
dotnet build VB6Compiler.sln -c Release
dotnet test VB6Compiler.sln -c Release
```

**Kein `--no-build` beim Testen.** Auf diesem Rechner scheitern die Testläufe dann sporadisch mit
`FileLoadException ... Zugriff verweigert` auf die nach `tests/*/bin/` kopierten Projekt-DLLs —
mal zwei Projekte, mal alle. Mit regulärem Build ist der Lauf stabil. Die Fehler sehen aus wie
echte Testfehler, sind aber keine; erst die Fehlermeldung prüfen, bevor Code geändert wird.

`TreatWarningsAsErrors` ist an, `Nullable` ist an. Der Build muss warnungsfrei bleiben.
Stand der letzten Prüfung: 160 Tests, alle grün.

CI ist Windows-only (`.github/workflows`), .NET 10, Restore/Build/Test auf `main` und `agent/**`.

## Fallen

- **`Debug.Print` ist noch .NET-Formatierung**, nicht VB6 (kein führendes Vorzeichen-Leerzeichen, .NET-Shortest-Roundtrip statt 15 signifikanter Stellen). Die E2E-Tests vergleichen mit `.Trim()` und verdecken das. Beim Anfassen von Zahlenausgabe mitdenken.
- **Vergleiche boxen**: `VBOperators.Equal(object?, object?)` für jeden Vergleich, obwohl der Binder beide Seiten bereits auf denselben Typ konvertiert hat.
- **Der Generator lowert Control Flow selbst** (`Exit For` -> `goto __vb6_loop_exit_N`). Das trägt nur, solange C# das einzige Backend ist und es kein `On Error`/`GoSub` gibt. Siehe Roadmap-Phase C.
- Die aktuelle ByRef-Implementierung verlangt eine Variable mit exakt passendem Typ. Geklammerte Argumente und temporäre ByRef-Konvertierungen fehlen.
