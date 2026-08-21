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
VB6.IR            Lowering: Bound Tree -> Basic Blocks mit expliziten Sprüngen
VB6.Emit.Managed  IR -> CIL + Metadaten + Portable PDB (System.Reflection.Metadata)
VB6.Runtime       VB6-Laufzeitsemantik (Arithmetik, Konvertierung, VBCurrency)
VB6.ProjectSystem .vbp laden
VB6.Compiler      VBCompilation / VBProjectCompilation: Pipeline
VB6.Compiler.Cli  vb6c
```

Es gibt **kein C#-Backend mehr**. Der Weg vom Bound Tree zur Assembly führt ausschließlich über
`VB6.IR` und `VB6.Emit.Managed`; Roslyn ist nicht mehr im Build. `vb6c --dump-ir` zeigt die
Zwischenstufe, die früher der generierte C#-Quelltext war.

Schichtgrenzen sind hart: Der Binder kennt kein IR, der Lowerer keine Syntaxknoten, der Emitter
keinen Bound Tree. Beim Erweitern nicht abkürzen.

`VB6.Runtime` ist die einzige Stelle für VB6-Verhalten zur Laufzeit. Wenn emittierter Code
VB6-Semantik selbst nachbaut statt die Runtime zu rufen, gehört es in die Runtime.

## Arbeitsweise pro Feature

Die Historie folgt einem festen Muster — bitte fortführen, es hält die Codebasis bei dieser Feature-Breite handhabbar. Ein Feature wandert schichtweise durch den Stack, **ein Commit pro Schicht**:

```
Recognize/Add <X> keyword      Lexer-Token, SyntaxKind
Parse <X>                      Parser + Syntaxknoten
Bind <X>                       Binder, Symbole, Konvertierungen
Add <X> runtime operations     VB6.Runtime
Lower <X>                      IrLowerer: Bound Tree -> IR
Emit <X>                       ManagedEmitter: IR -> CIL
Test <X> lexing                \
Test <X> binding                | je ein Commit pro Testschicht
Test <X> runtime semantics      |
Test <X> lowering               |
Test <X> end to end            /
Document <X> support           README
```

Commit-Betreffs: imperativ, kurz, kein Präfix, kein Punkt (`Bind Currency arithmetic`). Die bestehende Historie nutzt keine Co-Authored-By-Trailer.

**Jedes Sprachfeature braucht einen End-to-End-Test**, der ein Programm emittiert, ausführt und die Ausgabe prüft — nicht nur Binder-Assertions. Der Ablauf liegt in `tests/VB6.Compiler.Tests/VB6TestProgram.cs`: `VB6TestProgram.Run(quelltext)` bzw. `RunLines(...)` emittiert, startet und liefert die Ausgabe; `RunProject(pfad)` dasselbe für ein `.vbp`. Nicht wieder pro Testdatei nachbauen.

Wo eine Übersetzungsentscheidung geprüft werden soll statt der Ausgabe, wird gegen das IR assertiert, nicht gegen Text: `VB6TestIr.Lower(quelltext)` liefert das `IrProgram`, `RuntimeCalls`/`ArrayCalls`/`Procedures`/`Expressions` laufen darüber. Textvergleiche auf generiertem Code gibt es nicht mehr — sie waren an das C#-Backend gebunden und konnten zufällig zutreffen.

## Build und Test

```
dotnet build VB6Compiler.sln -c Release
dotnet test VB6Compiler.sln -c Release
```

**Wenn Tests mit `FileLoadException` scheitern, ist es kein Testfehler.** Meldungen wie
`Could not load file or assembly ... Zugriff verweigert` oder `... Falscher Parameter
(E_INVALIDARG)` betreffen die nach `tests/*/bin/` kopierten Projekt-DLLs. Die Dateien sind
dabei nicht beschädigt — sie sind bytegleich mit dem Original und laden außerhalb des
Testhosts problemlos; es ist ein Zustandsproblem der inkrementellen Kopie, typisch nach einem
abgebrochenen Build oder parallelem Visual-Studio-Build.

Behebung: `bin` und `obj` **des betroffenen Testprojekts** löschen und neu bauen. Ein
solutionweites Löschen reicht nicht immer aus:

```
rm -rf tests/VB6.Compiler.Tests/bin tests/VB6.Compiler.Tests/obj
```

Erkennungsmerkmal: Die fehlschlagende Projektmenge wechselt von Lauf zu Lauf, und
langjährig grüne Tests fallen mit aus. Immer erst die Fehlermeldung lesen, bevor Code
angefasst wird.

**Zweite, andere Ursache mit derselben Ausnahme:** `Eine Anwendungssteuerungsrichtlinie hat
diese Datei blockiert. (0x800711C7)`. Das ist **Smart App Control / WDAC**, nicht der Build.
Löschen von `bin`/`obj` hilft hier nicht — die Datei wird unabhängig vom Pfad blockiert, auch
außerhalb des Repos und auch für `vb6c.exe`. Prüfen mit:

```
Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy | Select VerifiedAndReputablePolicyState
Get-WinEvent -LogName Microsoft-Windows-CodeIntegrity/Operational | Where-Object Id -eq 3077
```

`VerifiedAndReputablePolicyState = 1` bedeutet Smart App Control aktiv; Event 3077 nennt die
blockierte Datei. Smart App Control lässt sich nur abschalten, nicht wieder einschalten.

Welche Datei betroffen ist, wechselt. Projekt-DLLs wie `VB6.Semantics.dll` können es treffen;
das gibt sich nach mehreren Builds oft von selbst. **Die E2E-Tests sind dagegen bauartbedingt
anfällig**, denn sie emittieren pro Lauf eine frische, unsignierte DLL nach `%TEMP%` und führen
sie aus — so eine Datei hat per Definition keine Reputation. Sie scheitern dann anders als die
übrigen: mit Exitcode `-532462766` und der Meldung im Ausgabetext des Kindprozesses statt als
Ausnahme im Testhost. Wer danach ohne diesen Hinweis sucht, verdächtigt den Emitter.

Nichts davon ist ein Compilerfehler. Auf einer Maschine mit aktivem Smart App Control sind
lokale Testläufe schlicht nicht aussagekräftig; Devcontainer oder CI als Referenz nehmen. Ist
Smart App Control aus (`VerifiedAndReputablePolicyState = 0`), läuft die Suite vollständig durch.

`TreatWarningsAsErrors` ist an, `Nullable` ist an. Der Build muss warnungsfrei bleiben.
Stand der letzten Prüfung: 523 Tests in 159 Testklassen, alle grün.

CI ist Windows-only (`.github/workflows`), .NET 10, Restore/Build/Test auf `main` und `agent/**`.

## Fallen

- **`Debug.Print` ist noch .NET-Formatierung**, nicht VB6 (kein führendes Vorzeichen-Leerzeichen, .NET-Shortest-Roundtrip statt 15 signifikanter Stellen). Die E2E-Tests vergleichen mit `.Trim()` und verdecken das. Beim Anfassen von Zahlenausgabe mitdenken.
- **`VB6.Runtime` konvertiert ausschließlich mit `CultureInfo.InvariantCulture`.** Kompilierte Programme sollen auf jeder Maschine dieselben Werte liefern; mit `CurrentCulture` ergab `"2.5" * 2` unter `de-DE` den Wert 50 statt 5. Klassisches VB6 war hier locale-abhängig — dagegen wurde bewusst entschieden. `CultureInfo.CurrentCulture` gehört nicht mehr in `VB6.Runtime`; `Debug.Print` läuft deshalb über `VBConversions.CStr` statt direkt über `Console.WriteLine`. `CultureIndependenceTests` prüft das unter `de-DE`, weil CI auf `en-US` einen Rückfall nicht sehen würde.
- **Vergleiche boxen**: `VBOperators.Equal(object?, object?)` für jeden Vergleich, obwohl der Binder beide Seiten bereits auf denselben Typ konvertiert hat.
- **Der Emitter hat genau einen Fehlerkanal.** `NotSupportedException` heißt „diese IR-Form kann das Backend noch nicht" und wird als `VB6E0001` mit der genannten Konstruktion gemeldet; jede andere Ausnahme ist ein Emitter-Defekt und wird als `VB6E0003` samt Typ und Stacktrace gemeldet. Beim Ergänzen von Emit-Code diese Trennung halten — sonst sieht ein NullReference wie eine Sprachlücke aus.
- **Typnamen im IR sind eindeutig, Symbole sind es nicht.** Ein `Private Type` verdeckt ein gleichnamiges `Public Type`; beide sind verschiedene Symbole und brauchen verschiedene Speichernamen (`__vb6_udt_Point`, `__vb6_udt_Point_2`), sonst lehnt die Runtime die Assembly wegen doppelten Typs ab.
- **Eine UDT-Wertkopie kopiert auch ihre Arrays.** Der CLR-Structcopy dupliziert nur die Referenz. `IrLowerer.LowerValueCopy` legt deshalb für jedes feste Array-Member eine eigene Kopie an — an jeder Wertgrenze: Zuweisung, Array-Element, Member, ByVal-Argument, Funktionsergebnis.
- **ByRef ist vollständig, aber typstreng.** Literale, Ausdrücke und Funktionsergebnisse laufen über `VBByRef.Temp` (Rückschreiben verworfen), Klammern erzwingen ByVal. Eine *Variable* falschen Typs bleibt `VB6S0008` — wie in VB6, weil das Rückschreiben dort ein Ziel hätte. Nicht „hilfsbereit" konvertieren.
