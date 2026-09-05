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

Aktuelle Arbeitsfront ist die einzige aktive Managed-Roadmap R0–R7 in `docs/ROADMAP.md`.
<!-- verification:claude-matrix:begin -->
Die Matrix enthält 157 Erwartungen: 131 `implemented`, 0 `partial`, 26 `planned`;
131 `documented-verified`, 26 `not-yet-verified`, 0 `oracle-verified`.
<!-- verification:claude-matrix:end -->
Offene Karten tragen `milestone` und `dependsOn`; sie schließen ausdrücklich
Objektlebensdauer, gespeicherte Zeiger und externe COM-/ActiveX-Verträge ein.
Sprachsemantische Korrekturen gelten in beiden Profilen. Locale, Plattformvorgaben und
erlaubte Erweiterungen bleiben profilabhängig.

R0 ist geschlossen: `build.ps1` wertet Standardlauf, nativen x86-Lauf und Wiederholungen getrennt
aus und schreibt `artifacts/verification-report.json`; die Statusregeln der Matrix prüfen Tests
statt Leser; `-UpdateVerificationDocs` schreibt die markierten Messwertblöcke. Nächste Karte ist
`managed-r1-grammar`.

**Auf Eis gelegt — nicht ohne ausdrückliche Ansage anfassen:**

- **LLVM/natives Backend** (`VB6.Emit.Llvm`). In der Roadmap ausdrücklich als *optional/deferred*
  geführt: „Dieser Pfad blockiert den Managed/.NET-Abschluss nicht." Der Code bleibt im Build,
  wird aber nicht weitergetrieben. Wichtig zu wissen: er ist **ausschließlich über Textvergleiche
  auf dem erzeugten LLVM-IR abgesichert** — nichts wird assembliert, gelinkt oder ausgeführt.
  Also genau das Prüfmuster, das für das C#-Backend abgeschafft wurde. Aussagen über native
  Korrektheit sind entsprechend schwach gedeckt.
- **IDE und LSP** (`VB6.LanguageServer`, nach R7). Ein erster Slice steht (Diagnosen, Completion,
  Definition, Dokumentsymbole); bewusst nach dem Compiler-Kern eingeordnet.

Die Plattformentscheidung ist umgesetzt: Legacy-`.vbp`/`.vbg`-Projekte defaulten in CLI und
MSBuild-SDK auf x86; `--x64`/`--anycpu` beziehungsweise `VB6TargetPlatform` sind validierte
Opt-ins. Einzelne Quelldateien und die öffentliche `ManagedEmitOptions`-API behalten AnyCPU als
Default, damit die Projektgrenze die Legacy-Kompatibilität bestimmt.

## Roadmap und Historie

Zwei getrennte Dokumente — die Trennung bitte halten:

- **`docs/ROADMAP.md`** ist **Ist-Stand und Offenes**: gemessene Baseline,
  verbindliche Entscheidungen und R0–R7 mit Karten und Abnahmebedingungen. Alte A–H-/M0–M10-
  Verweise werden durch die Zuordnungstabelle aufgelöst; keine zweite aktive Restliste führen.
- **`docs/CHANGELOG.md`** ist das **chronologische Arbeitsjournal**, älteste
 Einträge zuerst. Hier steht, was getan wurde.

Nach einem abgeschlossenen Feature: den Meilensteinstatus in der Roadmap fortschreiben und den
Arbeitsschritt **ans Ende** des Changelogs hängen. Keine Verlaufsprosa in die Roadmap
zurückschreiben — genau daran ist sie vorher auf 2800 Zeilen angewachsen, in denen 130
Abschnitte gleichzeitig „Aktueller …-Nachtrag" hießen.

Die Kompatibilitätsmatrix `docs/vb6-sp6-compatibility-matrix.json` hat **zwei unabhängige
Statusachsen** — `implementation` (`planned`/`partial`/`implemented`) und `verification`
(`not-yet-verified`/`documented-verified`/`oracle-verified`). Sie werden nie vermischt und nie
optimistisch gefüllt. `oracle-verified` darf nie ohne echten Lauf gegen einen VB6-SP6-
Originalcompiler gesetzt werden. Geplante Karten bleiben `not-yet-verified`; ihre
`testRefs` nennen vorhandene Baseline-Dateien und sind kein Nachweis des geplanten Vertrags.
Bereichsstatus werden aus den Erwartungen abgeleitet: alle umgesetzt = `implemented`,
alle geplant = `planned`, sonst `partial`. Nicht verifizierte Kinder verhindern eine
Verifikationszusage für den Gesamtbereich. Offene `gap`-Texte nennen konkrete Karten.
Diese Regeln prüfen `CompatibilityMatrixStatusTests` und `CompatibilityMatrixTests`, nicht mehr das Lesen.

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
keinen. Der native OCX-Pfad ist x86-gebunden. Die Fälle überspringen sich selbst, solange der Testhost
64 Bit ist oder die OCX fehlen — sie sind also **im normalen Lauf wertlos**. Der echte Lauf:

```
$env:VB6_REQUIRE_NATIVE_OCX = '1'
dotnet test tests/VB6.Runtime.WinForms.Tests -c Release -- RunConfiguration.TargetPlatform=x86
```

Der Schalter macht aus „überspringen" ein „hart melden"; ohne ihn sagt ein grüner Lauf nichts.
Gegenprobe zum Absichern: dasselbe mit `TargetPlatform=x64` muss fehlschlagen.

Commit-Betreffs: imperativ, kurz, kein Präfix, kein Punkt (`Bind Currency arithmetic`). Die bestehende Historie nutzt keine Co-Authored-By-Trailer.

**Jedes Sprachfeature braucht einen End-to-End-Test**, der ein Programm emittiert, ausführt und die Ausgabe prüft — nicht nur Binder-Assertions. Der Ablauf liegt in `tests/VB6.Compiler.Tests/VB6TestProgram.cs`: `VB6TestProgram.Run(quelltext)` bzw. `RunLines(...)` emittiert, startet und liefert die Ausgabe; `RunProject(pfad)` dasselbe für ein `.vbp`. Nicht wieder pro Testdatei nachbauen.

Wo eine Übersetzungsentscheidung geprüft werden soll statt der Ausgabe, wird gegen das IR assertiert, nicht gegen Text: `VB6TestIr.Lower(quelltext)` liefert das `IrProgram`, `RuntimeCalls`/`ArrayCalls`/`Procedures`/`Expressions` laufen darüber. Textvergleiche auf generiertem Code gibt es nicht mehr — sie waren an das C#-Backend gebunden und konnten zufällig zutreffen.

## Build und Test

```
.\build.ps1 -Configuration Release
```

Der Skriptpfad baut seriell und testet die 13 Projekte einzeln; ein solutionweiter `dotnet test`
startet die E2E-Projekte parallel und ist deshalb kein kanonischer Messlauf.

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
<!-- verification:claude-measurements:begin -->
Stand der Prüfung 2026-09-05 auf `b14e2b9`: 1668 Standardfälle in 13 Projekten,
1668 bestanden, 0 fehlgeschlagen. Nativer x86-Lauf: 81/81 bestanden, 0 übersprungen.
VISIA: 40/40 Projektitems, 0 Diagnosen.
Vollständiges Gate: True. Laufbericht: `artifacts/verification-report.json`.
<!-- verification:claude-measurements:end -->

Standardlauf, x86-Lauf und Wiederholungen werden nie addiert — die früher genannte 1698 war genau
so eine Summe und wurde als Testzahl gelesen. Diese Messwertblöcke schreibt
`build.ps1 -UpdateVerificationDocs` aus `artifacts/verification-report.json`; ein gewöhnlicher
Build fasst kein Dokument an. Zahlen hier nicht von Hand fortschreiben.

Zweite Messung neben der Suite ist die Korpusparität — sie fängt Regressionen, die kein
Unittest sieht:

```
dotnet run --project src/VB6.Compiler.Cli -c Release -- conformance/VISIA/4.8.7.1/prjVisia.vbp --report
```

Stand: **40 von 40 Projektitems, 0 Fehler**; das Gesamtprojekt emittiert auch durch
(`--emit-assembly`). Der Wert darf nicht steigen. VISIA ist Testkorpus, nicht Portierungsziel.

Die Absicherung lag lange fast vollständig in `VB6.Compiler.Tests`; der kanonische Lauf ergänzt
jetzt gezielte IR-/Emitter-, Plattform- und Diagnoseabdeckungen. Beim Ergänzen von Tests weiterhin
die untere Ebene bedienen — `VB6TestIr` für Übersetzungsentscheidungen, E2E zusätzlich, nicht
ersatzweise.

CI ist Windows-only (`.github/workflows/build.yml`), .NET 10, und ruft für Restore, seriellen
Build, projektweise Tests und VISIA-Paritätsreport ausschließlich `build.ps1` auf. Die Tests
laufen dort projektweise, nicht solutionweit; der native OCX-Pfad bleibt ein expliziter x86-Opt-in.

## Fallen

- **`build.ps1` braucht sein BOM.** Die Datei enthält deutsche Zeichenketten, und Windows
  PowerShell 5.1 liest ein `.ps1` **ohne** Byte Order Mark als Windows-1252: aus `Fälle` wird
  `FÃ¤lle` — und zwar erst in der *erzeugten* Datei, nicht im Skript. Weil die handgeschriebene
  Prosa daneben intakt bleibt, sieht das nach einem Tippfehler im Dokument aus. Umgekehrt schreibt
  `Set-Content -Encoding UTF8` unter 5.1 ein BOM, das die Markdown-Dateien nie hatten. Wer eine
  Datei erzeugt, die beide Shells gleich behandeln sollen, nimmt
  `[IO.File]::WriteAllText(..., (New-Object Text.UTF8Encoding($false)))`. CI läuft auf pwsh 7 und
  sieht keinen der beiden Effekte — lokal aufgefallen, nicht im Build.
- **PowerShell-Variablennamen sind case-insensitiv, und ein typisierter Parameter zwingt jede
  Zuweisung in seinen Typ.** In `build.ps1` war die Schleifenvariable `$project` dieselbe wie der
  Parameter `[string[]] $Project`; die Zuweisung eines `FileInfo` machte daraus ein String-Array,
  `$project.Name` wurde `$null`, jede Ergebnisdatei hieß `.trx`, und jeder der dreizehn Durchläufe
  testete die ganze Solution. Die Frischeprüfung des Laufberichts schlug **nicht** an — die Datei
  existierte und war frisch. Verraten hat es allein der Dateiname.
- **`Debug.Print` ist inzwischen VB6-nah formatiert** — führendes Vorzeichen-Leerzeichen über `FormatNumeric`, **`G7` für Single**, `G15` für Double/Currency, `G29` für den Decimal-Subtype (`Runtime.cs`). Dieselbe Staffelung gilt für `CStr` und für `Format(…, "General Number")`: Ein Single trägt sieben signifikante Stellen, und ihn mit fünfzehn auszugeben zeigt seine Umrechnungsreste als wären sie Werte — `1 / 3` ist in VB6 ein Single. Weiterhin gilt: die E2E-Helfer trimmen bewusst, Spalten-/Plattformformat ist damit *nicht* abgedeckt. Beim Anfassen von Zahlenausgabe mitdenken.
- **Locale-Verträge sind profilabhängig.** Bestehende deterministische Signaturen bleiben invariant; `VB6Sp6` verwendet an den implementierten Grenzen System-LCID und ANSI-Codepage. Profilzustand reist über IR/Assembly und explizite Runtime-Verträge, nicht über einen globalen Schalter. Weitere Locale-/DBCS- und Ausgabeabnahme gehört zu R1.
- **`vbUseSystem` bleibt in beiden Profilen systemabhängig.** Kalenderparameter mit Wert 0 verwenden `CurrentCulture`; das ist eine entschiedene Ausnahme. Die COM-Dispatch-LCID folgt ebenfalls bewusst `CurrentCulture`. Diese Entscheidung nicht erneut als offenen Determinismuskonflikt führen.
- **Skalare Vergleiche verwenden typisierte Helfer.** Variant-/Objektvergleiche behalten ihren dynamischen Runtime-Vertrag; die frühere Behauptung, jeder Vergleich boxe, ist überholt.
- **Der Emitter hat genau einen Fehlerkanal.** `NotSupportedException` heißt „diese IR-Form kann das Backend noch nicht" und wird als `VB6E0001` mit der genannten Konstruktion gemeldet; jede andere Ausnahme ist ein Emitter-Defekt und wird als `VB6E0003` samt Typ und Stacktrace gemeldet. Beim Ergänzen von Emit-Code diese Trennung halten — sonst sieht ein NullReference wie eine Sprachlücke aus.
- **Typnamen im IR sind eindeutig, Symbole sind es nicht.** Ein `Private Type` verdeckt ein gleichnamiges `Public Type`; beide sind verschiedene Symbole und brauchen verschiedene Speichernamen (`__vb6_udt_Point`, `__vb6_udt_Point_2`), sonst lehnt die Runtime die Assembly wegen doppelten Typs ab.
- **Eine UDT-Wertkopie kopiert auch ihre Arrays.** Der CLR-Structcopy dupliziert nur die Referenz. `IrLowerer.LowerValueCopy` legt deshalb für jedes feste Array-Member eine eigene Kopie an — an jeder Wertgrenze: Zuweisung, Array-Element, Member, ByVal-Argument, Funktionsergebnis.
- **ByRef ist vollständig, aber typstreng.** Literale, Ausdrücke und Funktionsergebnisse laufen über `VBByRef.Temp` (Rückschreiben verworfen), Klammern erzwingen ByVal. Eine *Variable* falschen Typs bleibt `VB6S0008` — wie in VB6, weil das Rückschreiben dort ein Ziel hätte. Nicht „hilfsbereit" konvertieren.
- **Ein neuer Diagnose-Code braucht einen Test.** Die Diagnostik ist das Sicherheitsnetz der „lieber melden als raten"-Regel — ein ungetesteter Diagnosepfad ist ein Loch darin. Die aktuelle Abdeckungsmessung findet keinen in `src/` definierten Diagnose-Code ohne Referenz in `tests/`; neue Codes müssen trotzdem mit einer Positivassertion in die zuständige Testsuite aufgenommen werden. Die semantischen Codes liegen in `UncoveredDiagnosticTests`; dort prüfen die Fälle den **Code, nicht den Meldungstext**, damit die Formulierung frei bleibt.
- **Zwischen `BeginDesignerInitialization` und `CompleteDesignerInitialization` läuft kein
  VB6-Ereignis.** VB6 legt eine Form zuerst aus und lässt das Programm danach laufen; WinForms
  meldet `Resize`, sobald eine Größe zugewiesen wird. Im Korpus rief das `conInTab_Resize` auf,
  während das `Line`-Control zwei Zeilen weiter unten noch nicht existierte — Absturz auf
  `Nothing`. Beide Enden der Hülle sind **ausdrücklicher Vertrag** des erzeugten Programms, nicht
  implizit: Der erste Entwurf öffnete sie beim ersten `CreateControl`, und jeder Hostkonsument,
  der Controls selbst anlegt, verlor damit für immer jedes Ereignis (sechs Tests). Wer eine neue
  Designer-Fläche ergänzt, emittiert beide Aufrufe.
- **Eine Form hat eine Default-Instanz, ein UserControl nicht.** `frmMain.Show` ohne `New` ist die
  übliche VB6-Art, ein zweites Fenster zu öffnen — die Form trägt `VB_PredeclaredId`, ihr Name ist
  eine Instanz. Im Compiler ist das ein globales `As New` (`VBProjectCompilation`), genau wie bei
  einer `.cls` mit demselben Attribut. Die Startform bleibt davon eine **eigene** Instanz; wer
  `frmStart.Show` nach `Unload Me` schreibt, bekommt hier eine zweite. Offene Abweichung.
- **Der Kopf einer Kontrollflussanweisung hat seine eigene Fehlerregion.**
  `CanProtectForErrorHandling` nimmt `If`, `For`, `For Each`, `While`, `Do`, `With` und
  `Select Case` von der Absicherung *als Anweisung* aus, weil eine geschützte Region keinen
  Basisblock überqueren darf. Das heißt **nicht**, dass ihr Kopf ungeschützt ist: dafür gibt es
  `LowerProtectedHeader`, das die Kopfinstruktionen einzeln umschließt und im Fehlerfall bei
  `Resume Next` am Schleifenausgang fortsetzt. Wer eine neue Kontrollflussform ergänzt, muss ihn
  benutzen — `For Each` tat es lange nicht, und ein 438 aus der Aufzählungsquelle beendete
  deshalb das Programm, während der Handler danebenstand. Der erste Reparaturversuch, eine
  generische Umklammerung in `LowerStatement`, verschachtelte sich mit genau diesem Helfer und
  riss den VISIA-Korpus mit „Nested error handling regions are not supported".
- **Ein Switch-Ausdruck mit lauter Zahlarmen nimmt `double` als natürlichen Typ — auch wenn das
  Ziel `object` ist.** Seit .NET 7 ist `IntPtr` gleich `nint` und hat eine **implizite**
  Umwandlung nach `double`. In `VBComVTable.DefaultOf` wurde `IntPtr.Zero` dadurch still zu
  `0.0`, und der vtable-Aufruf scheiterte mit „Object of type System.Double cannot be converted to
  type System.IntPtr&". Ein natürlicher Typ schlägt die Zielzuweisung; wer Arme unterschiedlicher
  Zahlbreiten in ein `object` gibt, boxt jeden Arm ausdrücklich.
- **`ELEMDESC` endet in einer Union — `wParamFlags` nicht über die marshallte Struktur lesen.**
  Und Vorsicht bei der Bedeutung: `stdole.IFont.Clone` trägt `PARAMFLAG_FOUT`, **nicht**
  `FRETVAL`. Der letzte Parameter ist damit kein Rückgabewert, sondern ein ByRef-Argument — die
  VB6-Form ist `f.Clone g`, nicht `Set g = f.Clone`. Wer die beiden verwechselt, ruft den Server
  mit einem Null-Zeiger und bekommt `E_POINTER`.
- **Die GUID auf einer `Object=`-Zeile ist eine TypeLib-Id, keine CLSID.** Ein installiertes OCX
  registriert sie unter `HKCR\TypeLib\{…}`; unter `HKCR\CLSID\{…}` steht sie nicht. Dazu kommt: die
  in der `.vbp` gepinnte Nebenversion muss nicht die installierte sein (`#2.0#` gegen registriertes
  `2.1` bei MSCOMCTL) — eine Nebenversion ist in COM aufwärtskompatibel. Beides zusammen ließ die
  Auflösung immer `null` liefern, und `VBExternalTypeCatalog` fiel auf eine **von Hand gepflegte
  Liste von neun Controlnamen** zurück. Weil die band, sah der Import funktionsfähig aus. Wer hier
  etwas ändert, prüft immer mit einem Namen gegen, den die Bibliothek *nicht* definiert — sonst
  wird aus dem Gewinn ein Bibliothekspräfix, das jeden Tippfehler durchwinkt.
- **Ein Teil des Designer-Zustands eines OCX ist über IDispatch gar nicht erreichbar.** `_ExtentX`,
  `_ExtentY` und `_Version` stehen für jedes ActiveX-Control in der `.frm`, und jedes gemessene
  Stock-Control weist sie beim Einzelzugriff mit einer `COMException` ab — `TrySetMember` liefert
  `False`, und `VBInteraction.SetMember` verwirft das. Der Weg dorthin ist ausschließlich
  `IPersistPropertyBag`, den VB6 ohnehin für den ganzen persistierten Zustand benutzt
  (`VBComPersistence`, geschlossen durch `CompleteDesignerInitialization` am Ende der Designer-
  Hülle). Wer Designer-Eigenschaften anfasst, darf deshalb nicht davon ausgehen, dass die
  Einzelzuweisung die vollständige Fläche ist. Umgekehrt gilt: Eine `.frx`-Nutzlast ist **keine**
  Automationswert. Ein Bild muss vor der Übergabe zu einem `IPictureDisp` werden — auf
  ListImage-Ebene fordert das Control für `Picture`, `Key` und `Tag` gleichermaßen `null` an, die
  Tüte kann sie also nicht unterscheiden, und eine durchgereichte Zeichenkette wird als
  Schnittstellenzeiger gelesen: `0xC0000005`. `IPersistStreamInit` braucht keines der gemessenen
  Stock-Controls — `TextRTF` und `Buttons` bietet die Tüte selbst an.
- **Ein VB6-Event auf einem ActiveX-Control hat zwei mögliche Quellen.** Die Events des OCX kommen über den COM-Connection-Point und verlangen den **VB6-Namen** — ein WinForms-Name wie `TextChanged` sagt einem OCX nichts, und die Übersetzung in `FindEvent` gilt nur dem managed Adapter. Fokus-Events dagegen sind in VB6 **Extender-Events**: Sie stammen vom Container, fehlen im Event-Interface des Controls und kommen nur über das `AxHost`-Wrapper-Event. Wer nur einen der beiden Wege bedient, bekommt einen Pfad, der stillschweigend nie feuert. Beim Ergänzen von Events immer beide durchdenken und nativ nachmessen, nicht herleiten — für `GotFocus` war die Namensregel schlicht die falsche Erklärung.
- **Die Umsetzung ist hier meist weiter als ihre Absicherung — erst messen, dann bauen.** Bei
  `l1-02-f` und `l1-02-g` lautete der Befund zweimal hintereinander „das Verhalten war bereits
  richtig, nur ungetestet"; bei der Variant-Promotionstabelle waren alle 49 gemessenen
  Operandenpaare korrekt. Die echten Lücken (`Err.Number` 5 statt 94 bei Null-Konvertierungen,
  5 statt 13 bei `CDate("kein Datum")`) waren beim Lesen des Quelltexts **nicht** sichtbar —
  Binder, Lowerer und Runtime haben zusammen zu viele Pfade. Ein Wegwerfprogramm über
  `VB6TestProgram.RunLines`, das `VarType`, `Err.Number` und Ergebniswert über die ganze
  Vertragsfläche ausgibt, kostet Minuten und verhindert, dass funktionierender Code umgebaut
  wird. Das ist verbindlich: erst messen, dann bauen.
- **Bestehende Tests sind Regressionsnachweise, kein Original-VB6-Orakel.** Widersprüche zwischen
  dokumentiertem Vertrag und Testwert erst gezielt messen und mit Quellen/Begründung festhalten.
  Ein Test darf weder allein aufgrund einer Vermutung geändert noch allein aufgrund seines
  Namens als endgültiger VB6-Beleg behandelt werden. Strittige Konvertierungsfälle bleiben R1.
- **Ein `Public`-Feld einer Klasse ist keine Variable, sondern eine Property.** `Binder.cs`
  löst `c.N` über `classType.TryGetProperty(...)` auf. Diese Modellierung hat vier Symptome
  erzeugt: `Bump c.N` mit `ByRef` verlor **still** das Rückschreiben, `Set c.ObjFeld = …` meldete
  `VB6S0064`, `c.Nums(1)` meldete `VB6S0006`, und `Public S As String * 5` ist ein Parserfehler.
  Die ersten drei sind behoben — `PropertySymbol.IsFieldBacked` unterscheidet die synthetisierte
  Feld-Property jetzt von einem echten `Property Get`, und der Binder verzweigt darauf. **Der
  Marker ist die einzige Unterscheidung; die synthetisierte Property bleibt bewusst
  parameterlos.** Wer ihr Parameter gäbe, um Indizierung zu ermöglichen, macht sie von einer
  echten indizierten Property ununterscheidbar. Alle vier Symptome sind behoben.
  Gegenprobe: ByRef funktioniert über Locals, Globals, UDT-Member und Array-Elemente. Wer hier
  etwas anfasst, prüft alle vier Symptome.
- **Zur Laufzeit ist dasselbe `Public`-Feld wieder ein Feld, keine Property.** Der Binder
  modelliert es als Get/Let-Property, der Emitter bildet es auf ein **CLR-Feld** ab. Wer im
  Laufzeitdispatch nach Mitgliedern sucht, muss deshalb Methoden, Properties **und** Felder
  abdecken — `VBDynamicDispatch` tat Letzteres nicht, weshalb ein spät gebundenes `o.N` 438
  meldete, obwohl das Feld direkt danebenlag. Die VB6-Sichtbarkeit steckt dabei im
  CLR-Attribut: `Public` wird `FieldAttributes.Assembly`, `Private` wird `FieldAttributes.
  Private`. Die Feldsuche prüft `!IsPrivate` — es gibt keine zweite Sichtbarkeitsquelle, und es
  soll auch keine geben.
- **Ein UDT-Member hat ein festes Layout — und der UDT-Binder hat seinen eigenen, schwächeren
  Konstantenfalter.** `UserDefinedTypeDeclarationBinder` faltet Arraygrenzen und `String * n`-
  Breiten selbst, weil der Speicher zur Übersetzungszeit feststehen muss; ein gewöhnliches `Dim`
  wertet seine Grenzen dagegen zur Laufzeit aus und kommt deshalb ohne Falter aus. Bis 08/2026
  faltete er **nur Literale** und gab bei allem anderen eine **leere Grenzenliste ohne Diagnose**
  zurück — das Member bekam keinen Speicher, und der erste Zugriff riss das Programm mit einer
  `NullReferenceException` ab. Auch `a(5 To 1)` kam so als Absturz statt als Meldung heraus. Wer
  hier etwas ergänzt: **eine Faltung, die fehlschlägt, muss melden**, nie still etwas Leeres
  liefern. Und die `String * n`-Breite hängt am selben Falter, hat aber noch ihre eigene
  Literal-only-Prüfung in **zwei** Pfaden (`BindFixedStringLength` und
  `ResolveFixedLengthStringType`) — wer einen davon öffnet, öffnet beide.
- **`String * n` hat vier Deklarationsformen und drei Stellen, die es tragen müssen.** Lokal,
  Modulvariable, Klassenfeld und UDT-Member gehen durch `ParseVariableDeclarators` bzw.
  `ResolveVariableDeclaratorType` — bis auf das UDT-Member, das seinen eigenen Pfad hat. Die
  Breite ist erst vollständig, wenn **alle drei** Verhaltensweisen stimmen: Anfangswert *n*
  Leerzeichen, Abschneiden beim Überschreiten, Auffüllen beim Unterschreiten. Genau daran ist
  A4 zweimal hintereinander vorbeigelaufen: Nachdem der Parser die Deklaration annahm, fehlte
  das Auffüllen bei einfacher Zuweisung, und danach fehlte noch der Anfangswert für alles außer
  dem UDT-Member. Wer hier etwas anfasst, misst alle drei gegen das UDT-Member als Referenz.
- **Fehlernummer 5 ist der Sammelwert für „nicht zugeordnet".** `VBErrors.Set` bildet jede
  unbekannte Ausnahme darauf ab, deshalb sieht ein falsches 5 wie ein Ergebnis aus. Beim
  Breitendurchgang waren fünf gemessene 5 falsch (richtig wären 6, 9, 13, 91, 94) und vier
  richtig. Eine gemessene 5 ist ein Verdacht, bis sie gegen die Dokumentation geprüft ist.
- **Beim Nachmessen von Fehlernummern gehört die 0 in die Fälle.** Der Sammelwert 5 verdeckt
  eine falsche Nummer, aber .NET verdeckt manchmal den *Fehler selbst*: `File.Delete` löscht
  eine fehlende Datei geräuschlos, `File.GetLastWriteTime` liefert für sie einen 1601er-Platz-
  halter. `Kill` und `FileDateTime` meldeten deshalb gar nichts, während `Open` und `FileLen`
  immerhin die falsche 5 lieferten — die schwereren Befunde standen also gerade **nicht** in
  der Liste der falschen 5. Eine Fehlernummernmessung, die nur bekannte Fehlerfälle abfragt,
  findet diese Klasse nie; jeder Fall braucht auch die Frage „meldet er überhaupt?".
- **Ein funktionierender Rückfall verdeckt einen toten Hauptpfad.** `VBComDispatch.TryInvoke`
  meldete bei jedem Problem `false`, und `VBDynamicDispatch` beantwortete den Aufruf per
  Reflection. Das Programm lief weiter — nur ohne die Fehlernummern des Servers. Auf x64 war der
  schnelle IDispatch-Pfad dadurch für **jeden Aufruf mit mehr als einem Argument** tot, jahrelang
  unbemerkt, weil kein Test die Nummer geprüft hat. Ursache war `VariantSize = 16`: ein `VARIANT`
  ist auf x64 **24** Bytes, die Union trägt `BRECORD` mit zwei Zeigern. Wer an COM-Marshalling
  etwas ändert, misst gegen einen echten Fremdserver (`Scripting.Dictionary` aus `scrrun.dll`,
  siehe `ComInteropRuntimeTests`) und prüft `Err.Number`, nicht nur, ob der Aufruf gelingt.
  Zwei verwandte Stolpersteine derselben Familie: `rgdispidNamedArgs` darf nicht null sein, sonst
  weist der Standard-Proxy den Aufruf ab (ein STA-Objekt vom MTA-Thread aus), und `EXCEPINFO` hat
  zwischen `dwHelpContext` und `pfnDeferredFillIn` ein `pvReserved`, ohne das `Scode` ins
  Leere liest.
- **Ein FACILITY_CONTROL-HRESULT ist nicht automatisch ein Serverfehler.**
  `Scripting.Dictionary.Add` lehnt die ByRef-Aufrufform ab, die seine eigene Typbibliothek
  beschreibt, mit `0x800A0005`; erst der ByVal-Rückfall gelingt. Ein COM-Fehler wird deshalb erst
  gemeldet, wenn **jede** Aufrufform durch ist — nicht beim ersten misslungenen `Invoke`.
- **Ein Array-Argument mit falschem Elementtyp war lange ein Typloch, kein Rechenfehler.** Zwischen zwei
  Referenz-Elementtypen teilen sich `VBArray<object>` und `VBArray<string>` über `__Canon` den Code — ein
  fehlender Cast fällt dort **gar nicht** auf, und `Join(variantArray, "-")` sah jahrelang richtig aus.
  Über einen Werttyp las der Aufgerufene denselben Fehler als falschen Speicher: `IRR` mit einem
  `Double`-Array riss den Prozess mit `Internal CLR error (0x80131506)` ab. Die Konvertierung sitzt
  deshalb im **Lowerer** (`ArrayFromObject` über `VBArrayOperations.FromObject<T>`), wo beide Typsymbole
  bekannt sind — nicht im Emitter, der nur noch CLR-Typen sieht. Wer eine Intrinsic-Deklaration mit
  Array-Parameter anfasst, prüft **beide** Elementarten; ein grüner Test über die Referenzseite sagt über
  die Wertseite nichts.
- **Bei `Format` entscheidet das Muster, nicht der Speichertyp des Ausdrucks.** `FormatValue` schickte
  jeden String in den Zeichenkettenformatierer, weshalb `Format("12", "0.00")` den Wert verlor (`0.00`)
  und `Format("abc", "#,##0")` das *Muster* zurückgab. VB6 wählt umgekehrt: numerisches Muster →
  Zahl, Datumsmuster → Datum, `@`/`&`/`<`/`>`/`!` → Zeichen. Ein String ohne Zahl kommt unverändert
  zurück — eine Null zu erfinden wäre schlimmer als nichts zu tun. `IsStringFormat` überspringt dabei
  literale Läufe, damit ein gequotetes `@` die Frage nicht falsch entscheidet.
- **Die CLI-Optionsgrammatik liegt an genau einer Stelle — dort halten.** `CommandLineParser.TryParse` in `src/VB6.Compiler.Cli/CommandLine.cs` parst sie einmal für alle drei Eingabearten; `Program.cs` verzweigt danach nur noch über `CommandLineOptions.Command`. Vorher stand dieselbe Grammatik dreimal da — im `.vbp`-Zweig, im Einzeldatei-Zweig und in `HandleProjectGroup` — mit handgeschriebenen Arity-Guards, und eine neue Option hieß drei Stellen ändern. Wer eine Option ergänzt, tut das im Parser, nicht im Zweig. Welche Befehle eine Eingabeart überhaupt zulässt, entscheidet weiterhin der Zweig — eine `.vbg` nimmt kein `--dump-ir`.
