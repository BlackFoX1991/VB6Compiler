# VB6Compiler

VB6-kompatibler Compiler in C#, der bestehende VB6-Projekte nach .NET 10 übersetzt.

## Ziel

1. **Vollständige VB6-Parität.** Alles, was die Original-IDE/Runtime kann, soll der Compiler können.
2. **Legacy-Projekte kompilieren unverändert.** Ein altes `.vbp` wird ohne Quelltextänderung übersetzt. Das ist das Akzeptanzkriterium für jedes Feature.
3. **Moderne Erweiterungen obendrauf** — 64 Bit, breitere Integer-Typen, echtes `Decimal`, bessere Fließkommatypen.
4. **Später:** eigene IDE mit WinForms-Designer.

## Fokus — woran gerade gearbeitet wird

Die Priorisierung ist **.NET-first**. Der Managed-Pfad ist der Zielpfad, an dem Kompatibilität
entschieden wird; alles andere ordnet sich unter.

Aktuelle Arbeitsfront, in dieser Reihenfolge:

1. **COM/ActiveX-Konsum und OCX-Hosting** (M8/M9). `MSComctlLib`, `RichTextLib`, `MSComDlg`
   über den nativen `AxHost`-Pfad, Connection-Point-Events, TypeLib-Import, typisiertes
   ByRef-Marshalling.
2. **Forms/UserControls auf WinForms** (M9). Designer-Werte, Control-Arrays, Event-Mapping,
   Zeichenoperationen.
3. **Managed-Kern nachziehen**, wo COM/Forms es verlangen — Variant-Promotion,
   Default-Property-Regeln, Event-Lifecycle.

**Auf Eis gelegt — nicht ohne ausdrückliche Ansage anfassen:**

- **LLVM/natives Backend** (`VB6.Emit.Llvm`). In der Roadmap ausdrücklich als *optional/deferred*
  geführt: „Dieser Pfad blockiert den Managed/.NET-Abschluss nicht." Der Code bleibt im Build,
  wird aber nicht weitergetrieben. Wichtig zu wissen: er ist **ausschließlich über Textvergleiche
  auf dem erzeugten LLVM-IR abgesichert** — nichts wird assembliert, gelinkt oder ausgeführt.
  Also genau das Prüfmuster, das für das C#-Backend abgeschafft wurde. Aussagen über native
  Korrektheit sind entsprechend schwach gedeckt.
- **IDE und LSP** (`VB6.LanguageServer`, M10). Ein erster Slice steht (Diagnosen, Completion,
  Definition, Dokumentsymbole); bewusst nach dem Compiler-Kern eingeordnet.

**Offene Entscheidung, die die aktuelle Arbeitsfront direkt betrifft:** Die Roadmap hat „x86 als
Default-Ausgabe, x64 opt-in" entschieden — begründet damit, dass der Korpus an 32-Bit-OCX hängt,
die kein 64-Bit-Prozess in-process lädt — und vermerkt, das müsse *vor* M8 endgültig geklärt sein.
Implementiert ist es nicht: CLI, SDK und `ManagedEmitOptions` defaulten durchgängig auf `AnyCpu`,
während der Emitter `AnyCpu` für architekturabhängige Array-Verträge diagnostisch ablehnt. M8
läuft bereits. Diese Lücke schließen, bevor weiterer Marshalling-Code darauf aufsetzt.

## Roadmap und Historie

Zwei getrennte Dokumente — die Trennung bitte halten:

- **`docs/ROADMAP.md`** (~430 Zeilen) ist **Ist-Stand und Offenes**: Produktziel, die beiden
  aktuellen Messwerte, Korpus-Frequenzen, „Entschiedene Weichenstellungen" und die Meilensteine
  0–10 mit `[x]`/`[~]`/`[ ]`-Listen. `[~]` heißt „begonnen, teilweise ausgabefähig" — der
  häufigste Zustand. Hier steht, was zu tun ist.
- **`docs/CHANGELOG.md`** (~2400 Zeilen) ist das **chronologische Arbeitsjournal**, älteste
  Einträge zuerst. Hier steht, was getan wurde.

Nach einem abgeschlossenen Feature: den Meilensteinstatus in der Roadmap fortschreiben und den
Arbeitsschritt **ans Ende** des Changelogs hängen. Keine Verlaufsprosa in die Roadmap
zurückschreiben — genau daran ist sie vorher auf 2800 Zeilen angewachsen, in denen 130
Abschnitte gleichzeitig „Aktueller …-Nachtrag" hießen.

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

Kernpipeline — hier läuft jedes Sprachfeature durch:

```
VB6.Syntax        SourceText, Tokens, Trivia, Syntaxknoten, Diagnostics
VB6.Lexer         case-insensitiv, Trivia-erhaltend
VB6.Parser        fehlertolerant -> ParseResult
VB6.Semantics     Binder: Syntax -> Symbole + Bound Tree (typisiert)
VB6.IR            Lowering: Bound Tree -> Basic Blocks mit expliziten Sprüngen
VB6.Emit.Managed  IR -> CIL + Metadaten + Portable PDB (System.Reflection.Metadata)
VB6.Runtime       VB6-Laufzeitsemantik (Arithmetik, Konvertierung, VBCurrency)
VB6.ProjectSystem .vbp/.vbg laden, Designer-/`.frx`-Parsing
VB6.Compiler      VBCompilation / VBProjectCompilation / VBProjectGroupCompilation,
                  TypeLib-Import, COM-Host-/Manifest-Erzeugung
VB6.Compiler.Cli  vb6c
```

Drumherum — beim Ändern der Pipeline mitdenken, sie hängen daran:

```
VB6.Runtime.WinForms         IVB6Host auf WinForms: Controls, Twips, OLE-Farben, Fonts,
                             Form-Lifecycle, natives OCX-Hosting über AxHost (net10.0-windows)
VB6.Runtime.WinForms.Runner  Startprozess für emittierte Forms-Assemblies
VB6.Compiler.Sdk             MSBuild-SDK: VB6Project/VB6ProjectGroup-Targets, NuGet-Paket
VB6.LanguageServer           LSP-Slice für Visual Studio (auf Eis, siehe Fokus)
VB6.Emit.Llvm                natives x86/x64-Backend (auf Eis, siehe Fokus)
```

13 Testprojekte spiegeln diese Struktur; das Gewicht liegt in `VB6.Compiler.Tests` (E2E).

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

Bei Host-Features (Controls, OCX, Forms) kommt eine Schicht dazu: `VB6.Runtime` definiert den
host-neutralen Vertrag mit deterministischem Headless-Verhalten, `VB6.Runtime.WinForms`
implementiert ihn gegen echte Controls. Headless muss ohne UI-Host durchlaufen — die Suite hat
keinen. Der native OCX-Pfad ist x86-gebunden und lässt sich über `VB6_REQUIRE_NATIVE_OCX=1` von
„überspringen, wenn nicht registriert" auf „hart melden" schalten.

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
Stand der letzten Prüfung (2026-08-25): **1121 Tests in 13 Testprojekten, alle grün.**

Zweite Messung neben der Suite ist die Korpusparität — sie fängt Regressionen, die kein
Unittest sieht:

```
dotnet run --project src/VB6.Compiler.Cli -c Release -- conformance/VISIA/4.8.7.1/prjVisia.vbp --report
```

Stand: **40 von 40 Projektitems, 0 Fehler**; das Gesamtprojekt emittiert auch durch
(`--emit-assembly`). Der Wert darf nicht steigen. VISIA ist Testkorpus, nicht Portierungsziel.

Die Testpyramide steht derzeit auf dem Kopf: die Absicherung liegt fast vollständig in
`VB6.Compiler.Tests` (391 E2E-Tests), während `VB6.IR.Tests` (5) und `VB6.Emit.Managed.Tests`
(10) dünn sind. Folge: ein Lowering- oder Emit-Defekt zeigt sich als falsche Programmausgabe
statt als lokalisierter Fehler. Beim Ergänzen von Tests bevorzugt die untere Ebene bedienen —
`VB6TestIr` für Übersetzungsentscheidungen, E2E zusätzlich, nicht ersatzweise.

CI ist Windows-only (`.github/workflows/build.yml`), .NET 10, Restore/Build/Test auf `main` und
`agent/**`, plus VISIA-Paritätsreport als Artefakt bei jedem Lauf. Die Tests laufen dort
projektweise, nicht solutionweit.

## Fallen

- **`Debug.Print` ist inzwischen VB6-nah formatiert** — führendes Vorzeichen-Leerzeichen über `FormatNumeric`, `G15` für Gleitkomma/Currency, `G29` für den Decimal-Subtype (`Runtime.cs`). Weiterhin gilt: die E2E-Helfer trimmen bewusst, Spalten-/Plattformformat ist damit *nicht* abgedeckt. Beim Anfassen von Zahlenausgabe mitdenken.
- **`VB6.Runtime` konvertiert ausschließlich mit `CultureInfo.InvariantCulture`.** Kompilierte Programme sollen auf jeder Maschine dieselben Werte liefern; mit `CurrentCulture` ergab `"2.5" * 2` unter `de-DE` den Wert 50 statt 5. Klassisches VB6 war hier locale-abhängig — dagegen wurde bewusst entschieden. `Debug.Print` läuft deshalb über `VBConversions.CStr` statt direkt über `Console.WriteLine`. `CultureIndependenceTests` prüft das unter `de-DE`, weil CI auf `en-US` einen Rückfall nicht sehen würde.
- **Von dieser Regel gibt es genau zwei Ausnahmen — keine dritte ohne Entscheidung.** `VBComDispatch` leitet die Dispatch-LCID aus `CurrentCulture` ab (bewusst, siehe Roadmap „culture-aware COM dispatch LCIDs"). `VBStrings.ToFirstDayOfWeek`/`ToCalendarWeekRule` lösen `vbUseSystem` (Wert 0) über `CurrentCulture.DateTimeFormat` auf. Letzteres ist VB6-treu, verletzt aber die Determinismus-Entscheidung: `Weekday(d, vbUseSystemDayOfWeek)` und `Format(d, "ww")` liefern unter `de-DE` andere Werte als unter `en-US`, und **kein Test deckt das ab**. Der Zielkonflikt ist offen — nicht einfach in eine Richtung auflösen.
- **Vergleiche boxen**: `VBOperators.Equal(object?, object?)` für jeden Vergleich, obwohl der Binder beide Seiten bereits auf denselben Typ konvertiert hat.
- **Der Emitter hat genau einen Fehlerkanal.** `NotSupportedException` heißt „diese IR-Form kann das Backend noch nicht" und wird als `VB6E0001` mit der genannten Konstruktion gemeldet; jede andere Ausnahme ist ein Emitter-Defekt und wird als `VB6E0003` samt Typ und Stacktrace gemeldet. Beim Ergänzen von Emit-Code diese Trennung halten — sonst sieht ein NullReference wie eine Sprachlücke aus.
- **Typnamen im IR sind eindeutig, Symbole sind es nicht.** Ein `Private Type` verdeckt ein gleichnamiges `Public Type`; beide sind verschiedene Symbole und brauchen verschiedene Speichernamen (`__vb6_udt_Point`, `__vb6_udt_Point_2`), sonst lehnt die Runtime die Assembly wegen doppelten Typs ab.
- **Eine UDT-Wertkopie kopiert auch ihre Arrays.** Der CLR-Structcopy dupliziert nur die Referenz. `IrLowerer.LowerValueCopy` legt deshalb für jedes feste Array-Member eine eigene Kopie an — an jeder Wertgrenze: Zuweisung, Array-Element, Member, ByVal-Argument, Funktionsergebnis.
- **ByRef ist vollständig, aber typstreng.** Literale, Ausdrücke und Funktionsergebnisse laufen über `VBByRef.Temp` (Rückschreiben verworfen), Klammern erzwingen ByVal. Eine *Variable* falschen Typs bleibt `VB6S0008` — wie in VB6, weil das Rückschreiben dort ein Ziel hätte. Nicht „hilfsbereit" konvertieren.
- **Ein neuer Diagnose-Code braucht einen Test.** Die Diagnostik ist das Sicherheitsnetz der „lieber melden als raten"-Regel — ein ungetesteter Diagnosepfad ist ein Loch darin. Ohne Test bleiben nur noch fünf Codes: `VB6L0002/3/4` (eingefrorener LLVM-Emitter), `VB6E0002` (interner PDB-Fehlerkanal, bräuchte Fehlerinjektion) und `VB6S0068` (verlangt einen Interface-Vertrag aus einem Klassenprojekt). Die semantischen Codes liegen in `UncoveredDiagnosticTests`; dort prüfen die Fälle den **Code, nicht den Meldungstext**, damit die Formulierung frei bleibt.
- **Die CLI implementiert jede Option mehrfach.** `src/VB6.Compiler.Cli/Program.cs` ist Top-Level-Code mit handgeschriebenen Arity-Guards (`args.Length is >= 3 and <= 6`); `--dump-ir`, `--emit-llvm`, `--emit-assembly` und `--report` existieren getrennt im `.vbp`-Zweig, im Einzeldatei-Zweig und in `HandleProjectGroup`. Eine neue Option heißt drei Stellen ändern, und ein vergessener Zweig fällt nur über die langsamen Prozesstests auf.
