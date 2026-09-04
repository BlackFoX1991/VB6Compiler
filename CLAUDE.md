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

Aktuelle Arbeitsfront ist der verbindliche Managed-Abschlussplan in `docs/ROADMAP.md` (Etappen A–H).
Die offenen Karten und ihre Statusachsen stehen in `docs/vb6-sp6-compatibility-matrix.json`.
Der aktuelle Matrixstand beträgt 118 Erwartungen (88 `implemented`,
14 `partial`, 16 `planned`; 102 `documented-verified`); die nächste offene
Implementierungskarte ist `l1-03-q-typelib-registration-localserver`. `L1-02-A` bleibt als breiter
Familienstatus bewusst `partial`.

**Auf Eis gelegt — nicht ohne ausdrückliche Ansage anfassen:**

- **LLVM/natives Backend** (`VB6.Emit.Llvm`). In der Roadmap ausdrücklich als *optional/deferred*
  geführt: „Dieser Pfad blockiert den Managed/.NET-Abschluss nicht." Der Code bleibt im Build,
  wird aber nicht weitergetrieben. Wichtig zu wissen: er ist **ausschließlich über Textvergleiche
  auf dem erzeugten LLVM-IR abgesichert** — nichts wird assembliert, gelinkt oder ausgeführt.
  Also genau das Prüfmuster, das für das C#-Backend abgeschafft wurde. Aussagen über native
  Korrektheit sind entsprechend schwach gedeckt.
- **IDE und LSP** (`VB6.LanguageServer`, M10). Ein erster Slice steht (Diagnosen, Completion,
  Definition, Dokumentsymbole); bewusst nach dem Compiler-Kern eingeordnet.

Die Plattformentscheidung ist umgesetzt: Legacy-`.vbp`/`.vbg`-Projekte defaulten in CLI und
MSBuild-SDK auf x86; `--x64`/`--anycpu` beziehungsweise `VB6TargetPlatform` sind validierte
Opt-ins. Einzelne Quelldateien und die öffentliche `ManagedEmitOptions`-API behalten AnyCPU als
Default, damit die Projektgrenze die Legacy-Kompatibilität bestimmt.

## Roadmap und Historie

Zwei getrennte Dokumente — die Trennung bitte halten:

- **`docs/ROADMAP.md`** ist **Ist-Stand und Offenes**: Produktziel, die drei
 aktuellen Messwerte, Korpus-Frequenzen, „Entschiedene Weichenstellungen" und die Meilensteine
 0–10 mit `[x]`/`[~]`/`[ ]`-Listen. `[~]` heißt „begonnen, teilweise ausgabefähig" — der
 häufigste Zustand. Hier steht, was zu tun ist.
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
Originalcompiler gesetzt werden. Steht eine Achse auf 100 %,
während die Roadmap offene Punkte führt, ist das ein Fehler und kein Erfolg.

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
Stand der letzten Prüfung (2026-09-01): Der kanonische `build.ps1`-Lauf prüft alle 13 Testprojekte
seriell; die genaue Testzahl steht im Roadmap-/README-Messwert und muss bei jeder Änderung neu
erfasst werden.

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

- **`Debug.Print` ist inzwischen VB6-nah formatiert** — führendes Vorzeichen-Leerzeichen über `FormatNumeric`, **`G7` für Single**, `G15` für Double/Currency, `G29` für den Decimal-Subtype (`Runtime.cs`). Dieselbe Staffelung gilt für `CStr` und für `Format(…, "General Number")`: Ein Single trägt sieben signifikante Stellen, und ihn mit fünfzehn auszugeben zeigt seine Umrechnungsreste als wären sie Werte — `1 / 3` ist in VB6 ein Single. Weiterhin gilt: die E2E-Helfer trimmen bewusst, Spalten-/Plattformformat ist damit *nicht* abgedeckt. Beim Anfassen von Zahlenausgabe mitdenken.
- **`VB6.Runtime` konvertiert ausschließlich mit `CultureInfo.InvariantCulture`.** Kompilierte Programme sollen auf jeder Maschine dieselben Werte liefern; mit `CurrentCulture` ergab `"2.5" * 2` unter `de-DE` den Wert 50 statt 5. Klassisches VB6 war hier locale-abhängig — dagegen wurde bewusst entschieden. `Debug.Print` läuft deshalb über `VBConversions.CStr` statt direkt über `Console.WriteLine`. `CultureIndependenceTests` prüft das unter `de-DE`, weil CI auf `en-US` einen Rückfall nicht sehen würde.
- **Von dieser Regel gibt es genau zwei Ausnahmen — keine dritte ohne Entscheidung.** `VBComDispatch` leitet die Dispatch-LCID aus `CurrentCulture` ab (bewusst, siehe Roadmap „culture-aware COM dispatch LCIDs"). `VBStrings.ToFirstDayOfWeek`/`ToCalendarWeekRule` lösen `vbUseSystem` (Wert 0) über `CurrentCulture.DateTimeFormat` auf. Letzteres ist VB6-treu, verletzt aber die Determinismus-Entscheidung: `Weekday(d, vbUseSystemDayOfWeek)` und `Format(d, "ww")` liefern unter `de-DE` andere Werte als unter `en-US`, und **kein Test deckt das ab**. Der Zielkonflikt ist offen — nicht einfach in eine Richtung auflösen.
- **Vergleiche boxen**: `VBOperators.Equal(object?, object?)` für jeden Vergleich, obwohl der Binder beide Seiten bereits auf denselben Typ konvertiert hat.
- **Der Emitter hat genau einen Fehlerkanal.** `NotSupportedException` heißt „diese IR-Form kann das Backend noch nicht" und wird als `VB6E0001` mit der genannten Konstruktion gemeldet; jede andere Ausnahme ist ein Emitter-Defekt und wird als `VB6E0003` samt Typ und Stacktrace gemeldet. Beim Ergänzen von Emit-Code diese Trennung halten — sonst sieht ein NullReference wie eine Sprachlücke aus.
- **Typnamen im IR sind eindeutig, Symbole sind es nicht.** Ein `Private Type` verdeckt ein gleichnamiges `Public Type`; beide sind verschiedene Symbole und brauchen verschiedene Speichernamen (`__vb6_udt_Point`, `__vb6_udt_Point_2`), sonst lehnt die Runtime die Assembly wegen doppelten Typs ab.
- **Eine UDT-Wertkopie kopiert auch ihre Arrays.** Der CLR-Structcopy dupliziert nur die Referenz. `IrLowerer.LowerValueCopy` legt deshalb für jedes feste Array-Member eine eigene Kopie an — an jeder Wertgrenze: Zuweisung, Array-Element, Member, ByVal-Argument, Funktionsergebnis.
- **ByRef ist vollständig, aber typstreng.** Literale, Ausdrücke und Funktionsergebnisse laufen über `VBByRef.Temp` (Rückschreiben verworfen), Klammern erzwingen ByVal. Eine *Variable* falschen Typs bleibt `VB6S0008` — wie in VB6, weil das Rückschreiben dort ein Ziel hätte. Nicht „hilfsbereit" konvertieren.
- **Ein neuer Diagnose-Code braucht einen Test.** Die Diagnostik ist das Sicherheitsnetz der „lieber melden als raten"-Regel — ein ungetesteter Diagnosepfad ist ein Loch darin. Die aktuelle Abdeckungsmessung findet keinen in `src/` definierten Diagnose-Code ohne Referenz in `tests/`; neue Codes müssen trotzdem mit einer Positivassertion in die zuständige Testsuite aufgenommen werden. Die semantischen Codes liegen in `UncoveredDiagnosticTests`; dort prüfen die Fälle den **Code, nicht den Meldungstext**, damit die Formulierung frei bleibt.
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
  Einzelzuweisung die vollständige Fläche ist. Die `.frx`-Seite (`IPersistStreamInit`, dort hängt
  etwa der RichTextBox-Text) fehlt weiterhin.
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
- **Ein bestehender Test, dessen Name eine Vertragszusage ausspricht, schlägt eine Herleitung
  aus der VB6-Dokumentation.** Ohne installiertes Orakel ist er der bessere Zeuge. Zweimal
  belegt: `CDec(Null)` soll laut Doku 94 melden, liefert aber korrekt Null, weil ein Variant mit
  Decimal-Subtyp Null tragen kann; `CInt(CVErr(5))` soll laut Doku 13 melden, hängt aber über
  `CInt(Missing) = 448` an der Missing-Argument-Mechanik. In beiden Fällen war die Herleitung
  plausibel und falsch. Reißt eine Änderung so einen Test, wird die Änderung zurückgenommen und
  die Frage notiert — nicht der Test angepasst.
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
- **Die CLI-Optionsgrammatik liegt an genau einer Stelle — dort halten.** `CommandLineParser.TryParse` in `src/VB6.Compiler.Cli/CommandLine.cs` parst sie einmal für alle drei Eingabearten; `Program.cs` verzweigt danach nur noch über `CommandLineOptions.Command`. Vorher stand dieselbe Grammatik dreimal da — im `.vbp`-Zweig, im Einzeldatei-Zweig und in `HandleProjectGroup` — mit handgeschriebenen Arity-Guards, und eine neue Option hieß drei Stellen ändern. Wer eine Option ergänzt, tut das im Parser, nicht im Zweig. Welche Befehle eine Eingabeart überhaupt zulässt, entscheidet weiterhin der Zweig — eine `.vbg` nimmt kein `--dump-ir`.
