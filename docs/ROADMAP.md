# Roadmap

Dieses Dokument beschreibt den **Ist-Stand und das Offene**. Die chronologische Historie — was
wann implementiert und gemessen wurde — steht in `CHANGELOG.md` und gehört nicht hierher.

## Produktziel

Das Hauptprodukt ist ein moderner, hochkompatibler VB6-Compiler, nicht die VISIA-Portierung und
nicht zuerst die IDE. Der aktive Abschlussplan vervollständigt den Managed/.NET-Pfad mit eigenem
Runtime-/Objektmodell, vollständigem VB6-SP6-Sprach- und Standardbibliotheksvertrag,
COM-/ActiveX-Konsum und -Emission, Forms sowie `.vbp`/`.vbg` und einem headless-fähigen MSBuild SDK.
LLVM, LSP, IDE und visueller Designer bleiben ausdrücklich außerhalb dieses Abschlussplans.

Der historische Plan bleibt als langfristiges Produktbild erhalten: LLVM kann später wieder als
nativer Windows-Backendpfad aufgenommen werden, und Visual Studio/LSP, IDE und Designer können als
eigene Produkte folgen. Sie blockieren den hier festgelegten Managed-Abschluss nicht. VISIA ist
Regressionstestkorpus, nicht Portierungsziel.

Die aktuelle Priorisierung ist bewusst **.NET-only bis zum Abschluss**: Managed-Emitter, Runtime,
Variant-/Object-Semantik, Standardbibliothek, Win32-/COM-x86-ABI, Forms/ActiveX und die headless
MSBuild-Verträge werden geschlossen. Visual-Studio-spezifisches CPS-/Projektmodell gehört zur
ausgeschlossenen IDE-Schicht.

Die Reihenfolge nutzt weiterhin die Konstrukt-Frequenzanalyse über echten VB6-Code. Der Umfang wird
aber nicht mehr vom Korpus begrenzt: Maßgeblich ist eine vollständige, dokumentationsbasierte
VB6-SP6-Kompatibilitätsmatrix.

Die operative Abarbeitung folgt der Kompatibilitätsmatrix `vb6-sp6-compatibility-matrix.json`.
Sie zerlegt den Umfang in atomare Erwartungen mit eigenen Statusachsen und einem kanonischen
Gate. Die Roadmap bleibt dabei die fachliche Quelle für Ziel,
Abgrenzung und Meilensteinstatus; die Queue ist die Quelle für die nächste konkrete Änderung.

## Gemessener Ist-Stand

Drei Messungen definieren den Stand. Alle sind reproduzierbar und dürfen sich nicht
verschlechtern.

**Korpusparität** — `vb6c conformance/VISIA/4.8.7.1/prjVisia.vbp --report`:

| Messpunkt | Fehler gesamt | Parser | Lexer | Semantik | fehlerfreie Dateien |
|---|---|---|---|---|---|
| 2026-08-25 | **0** | **0** | **0** | **0** | **40 von 40** |
| 2026-09-01 | **0** | **0** | **0** | **0** | **40 von 40** |
| 2026-09-04 | **0** | **0** | **0** | **0** | **40 von 40** |

Alle 40 `.bas`-, `.cls`-, `.frm`- und `.ctl`-Quellen werden gelesen, Designer-Metadaten
offsettreu ausgeblendet, typisiert und gebunden; das Gesamtprojekt emittiert auch durch
(`--emit-assembly`). Zum Vergleich die Nulllinie: 3361 Fehler, 0 von 27 Dateien. Der Weg
dorthin steht als Messreihe in `CHANGELOG.md`.

**Regressionssuite** — `build.ps1 -Configuration Release`: **1698 Tests, alle grün** in 13
Testprojekten (Stand 2026-09-04); der Lauf testet projektweise seriell.
Gewachsen ist die Suite zuletzt durch den Breitendurchgang über die Standardbibliothek vom
02.–04.09.: `Format`, `Math`, Financial, String, Datum/Zeit, Konvertierung und Information.
Er hat vier Defekte gemessen, die kein Unittest sah — das Muster statt des Speichertyps in
`Format`, ein Typloch bei Array-Argumenten mit falschem Elementtyp, die Ergebnisformen von
`Replace` und `Split` sowie `Val` über Leerzeichen hinweg — und dazu die Zusage, dass
`Class_Terminate` überhaupt läuft.

**Kompatibilitätsmatrix** — `node -e "const d=require('./docs/vb6-sp6-compatibility-matrix.json'); console.log(d.expectations.length)"`:
**121 Erwartungen**, davon **120 implemented**, **1 partial** und **0 planned**;
**121/121 documented-verified** (Stand 2026-09-04).

Ein Breitendurchgang am 2026-08-30 hat elf Defekte gemessen, die kein Unittest sah; die noch
offenen Punkte daraus sind unten in den Etappen B und C als eigene Zeilen geführt. Das
vollständige Befundregister steht im Changelog.

Als Compiler-Kern vorhanden: `Property Get/Let/Set`, Events, `WithEvents`, `New`, `Set`,
`TypeOf`, Variant-Arrays, Standard-`Collection`, late-bound Object-/Control-Mitglieder sowie
`On Error` mit `Err` und `Resume Next`. Managed-Klasseninstanzen haben eigenen Feldspeicher,
Konstruktor-/Terminator-Lifecycle, Property-Dispatch, `RaiseEvent`/`WithEvents`-Emission und
echte Referenzidentität; `Implements` wird als CLR-Interface emittiert und über `callvirt`
inklusive Property-Accessors dispatcht.

Offen sind die Blöcke, die die Meilensteine unten führen: vollständige Variant-/Object- und
Runtime-Semantik, COM-/IDispatch- und ActiveX-Verträge, Forms-Vollständigkeit sowie die externe
Win32-/COM-x86-ABI. Die native LLVM-Codegenerierung ist damit nicht gemeint.

VISIA ist dabei Regressionstest- und Messkorpus, nicht das fachliche Portierungsziel.

### Die Analyse-Achse ist ausgereizt

Die Fehlerzahl aus `--report` hat das Projekt von 3361 auf 0 getrieben und kann nicht weiter
fallen. Sie bleibt als Regressionsschwelle — sie darf nie wieder steigen —, taugt aber nicht mehr
zur Priorisierung: Sie misst, ob eine Quelle *gebunden* werden kann, nicht ob der emittierte Code
das Richtige tut.

Die Reihenfolge der offenen Verträge wird deshalb weiterhin am Korpus gemessen, jetzt aber an
Forms und Controls statt an Parserfehlern. Erhoben über die 6 `.frm`- und 4 `.ctl`-Quellen:

| Designer-Controls | | Event-Handler | | Grafik-API | |
|---|---|---|---|---|---|
| `VB.Menu` | 29 | `Click` | 34 | `ForeColor` | 82 |
| `VB.Label` | 28 | `MouseDown` | 14 | `AutoRedraw` | **12** |
| `VB.Shape` | 15 | `Resize` | 13 | `.Line` | 8 |
| `VB.PictureBox` | 12 | `MouseMove` | 11 | `DrawMode` | 0 |
| `VB.Image` | 9 | `MouseUp` | 10 | `.Cls` | 3 |
| `VB.Line` | 7 | `KeyDown` | 7 | `ScaleMode` | 2 |
| `VB.CommandButton` | 6 | `LostFocus` | 6 | `TextWidth`/`TextHeight` | 4 |
| `VB.TextBox`, `VB.Frame` | je 4 | `Load` | 5 | `PSet` | 1 |
| **OCX gesamt** | **13** | **`Paint`** | **3** | `PaintPicture` | 1 |
| eigene UserControls | 4 | `NodeClick` (OCX) | 2 | `Circle` | 0 |

Drei Schlüsse, die die Reihenfolge in M8 und M9 bestimmen:

1. **Intrinsische Controls und ihr Eventmodell dominieren**, nicht OCX — 149 intrinsische
   Designer-Instanzen gegen 13 OCX-Instanzen. Die Forms-Grundmechanik wiegt schwerer als die
   ActiveX-Oberfläche, obwohl letztere spektakulärer aussieht.
2. **`Paint` war das einzige verbreitete Event, das der Host nicht verdrahtet hat** — zusammen mit
   12× `AutoRedraw` die größte belegte Lücke im Forms-Vertrag. Inzwischen geschlossen; das
   Eventmodell der intrinsischen Controls ist damit vollständig.
3. **MDI und `DrawMode` kommen im Korpus nicht ein einziges Mal vor.** Ein bereits vorhandener
   Managed-MDI-Grundvertrag hostet Container und Child-Forms. Beide Flächen werden im neuen
   VB6-SP6-Abschlussplan dennoch vervollständigt; die fehlende Korpusevidenz verschiebt nur ihre
   Reihenfolge hinter die belegten Forms-Verträge.

Zur `DrawMode`-Zeile: Sie stand zunächst mit 3 in dieser Tabelle. Die Nachmessung beim Umsetzen
zeigte, dass alle drei Treffer keine VB6-Eigenschaft waren — ein gleichnamiges Enum, ein Kommentar
und ein `SetROP2`-P/Invoke-Parameter. Beim Zählen also auf die Eigenschaftszuweisung prüfen, nicht
auf den bloßen Namen.

## Korpus-Frequenzen

Häufig in VISIA — es ist ein Systemprogramm (Assembler, Linker, PE-Erzeugung), kein
Business-Programm:

| | | | |
|---|---|---|---|
| `&H`/`&O`-Literale | 892 ✅ | `Event`/`RaiseEvent` | 97 |
| String-Funktionen | 337 | `Optional`/`ParamArray` | 77 (`Optional`-Syntax ✅) |
| `Declare` (Win32) | 234 | Datei-I/O (`For Binary`) | 76 |
| `Property Get/Let/Set` | 209 | `On Error GoTo` / `Resume Next` | 34 / 31 |
| `ReDim`/`Preserve` | 103 ✅ typed arrays | `Type ... End Type` | 52 ✅ Syntax + Typraum |
| `With` | 102 | `Enum` | 44 ✅ Syntax |

Kommt **nicht** vor: `Format$` 0, `Date` 0, ADO 0, `#If` 0. `Resume`, `Resume Next` und
`Resume <Label>` sind inzwischen syntaktisch gebunden; `Resume Next` besitzt im Managed-Backend
einen fehlerstellenspezifischen Fortsetzungsdispatcher. Vollständige Managed-Handlerzustände gehören
zum Abschlussplan; der native LLVM-Resume-/ABI-Vertrag bleibt ausgeschlossen.

## Entschiedene Weichenstellungen

- **Variant früh**, bewusst gegen die VISIA-Evidenz (dort nur 20 Treffer): der Umbau wird später
  teurer, und die Business-Legacy-Projekte brauchen ihn sehr wohl.
- **x86 als Default für Legacy-Projekte, x64 opt-in.** Bestätigt durch den Korpus: VISIA hängt an
  32-Bit-OCX (`MSComDlg.CommonDialog`, `MSComctlLib`, `RichTextLib`), die ein 64-Bit-Prozess nicht
  in-process laden kann. „64 Bit" gilt für Sprache und Typen, nicht zwingend für den Prozess.
  Die Regel ist entschieden und gilt an der **Projektgrenze**: `.vbp` und `.vbg` emittieren ohne
  Schalter als x86, weil jedes Legacy-VB6-Projekt 32-Bit ist; `--x64` und `--anycpu` bleiben
  opt-in. Einzelne Quelldateien ohne Projektkontext bleiben AnyCpu, und `ManagedEmitOptions`
  behält AnyCpu als API-Default — die Entscheidung gehört an die Projektgrenze, nicht in den
  Emitter. Beachten: x86 impliziert `TargetIs64Bit: false` und damit `#If Win64`.
- **Zwei explizite Kompatibilitätsprofile.** `Deterministic` bleibt der rückwärtskompatible Default:
  Zahl-/String-Konvertierungen sind invariant und verwenden den bisherigen deterministischen
  Windows-1252-Vertrag. Das additive Profil `VB6Sp6` bildet die dokumentierte klassische Semantik
  mit System-LCID, ANSI-Codepage und x86-Prozessmodell ab. `VB6Sp6` wählt x86 automatisch und lehnt
  x64/AnyCpu sowie compiler-eigene Erweiterungen außerhalb VB6 SP6 diagnostisch ab.
- **Profilzustand ist kompiliert, nicht global.** Das gewählte Profil reist von
  `VBCompilationOptions` über Bindung und IR in Assembly-Metadaten und profilbewusste Runtime-
  Aufrufe. Forms- und COM-Hosts erhalten es instanzbezogen. Ein Prozess kann daher Assemblies mit
  verschiedenen Profilen laden, ohne einen globalen Runtime-Schalter umzulegen.
- **`vbUseSystem` bleibt in beiden Profilen ausdrücklich systemabhängig.** Wo VB6 den Wert 0 als
  „frag das System" definiert — etwa `FirstDayOfWeek` und `FirstWeekOfYear` — wird weiterhin
  `CultureInfo.CurrentCulture` verwendet. Im `VB6Sp6`-Profil gilt die System-Locale darüber hinaus
  an allen von VB6 dokumentierten Konvertierungs- und Formatierungsgrenzen; Format verwendet
  dabei lokale Dezimal-/Tausendertrennzeichen sowie lokalisierte Datumsnamen.
- **Kein installiertes VB6-Orakel.** Auf der Entwicklungsmaschine ist kein VB6-Compiler vorhanden,
  und dieser Plan setzt keinen externen lizenzierten Runner voraus. `VB6Sp6` bedeutet deshalb
  zunächst dokumentationsbasierte Kompatibilität. Die Matrix führt zwei unabhängige Achsen:
  `implementation` (`planned`, `partial`, `implemented`) beschreibt den gebauten Umfang;
  `verification` (`not-yet-verified`, `documented-verified`, `oracle-verified`) beschreibt den
  Nachweis. Ohne Originalcompiler bleibt `oracle-verified` unerreichbar und ist keine
  Abschlussvoraussetzung dieses Plans.
- **VISIA ist Testkorpus, nicht Portierungsziel.** Die IDE entsteht später eigenständig in C#.
  Es liegt versioniert unter `conformance/VISIA/` und wird von `ConformanceCorpusTests` in CI
  mitgemessen. Herkunft und Zweck: `conformance/README.md`.
- **Direkte Managed-Emission statt C#-Zwischencode.** Der Weg `Bound Tree -> C#-Quelltext ->
  Roslyn -> Assembly` ist abgeschafft; `VB6.CodeGen.CSharp` und `Microsoft.CodeAnalysis` sind
  aus dem Build entfernt. Stattdessen lowert `VB6.IR` in Basic Blocks und `VB6.Emit.Managed`
  schreibt CIL, Metadaten und eine Portable PDB direkt. Gründe: C# kann VB6-Kontrollfluss
  (`On Error Resume Next`, `GoSub`/`Return`, Zeilennummern) nicht ohne Verrenkungen ausdrücken,
  jede Semantikfrage wurde zweimal beantwortet — einmal in der Bindung, einmal in der
  Textausgabe —, und der Roslyn-Aufruf dominierte die Übersetzungszeit. Der Preis ist, dass es
  kein lesbares Zwischenprodukt mehr gibt; dafür gibt es `vb6c --dump-ir`.

- **VB6-Kontextwörter werden nicht global reserviert.** `Option Base`/`Option Compare` werden nur
  direkt hinter `Option` erkannt; `Base` und `Compare` bleiben sonst normale Bezeichner, weil der
  Korpus sie so verwendet. Gilt für jedes weitere Kontextwort.
- **`As Type` gilt pro Deklarator, nicht pro Zeile.** `Dim a, b As Integer` macht nur `b` zu
  Integer, `a` bleibt Variant. Der Syntaxbaum speichert `As Type` deshalb an jedem Deklarator
  einzeln — nicht „hilfsbereit" vom Nachbarn erben lassen.
- **Syntaxbarrieren, die viele Dateien blockieren, kommen vor der vollständigen Semantik.** Sie
  sind einzeln klein, verhindern aber die Messung von allem Übrigen. Konstrukt syntaktisch
  aufnehmen und mit Diagnose stoppen ist erlaubt; die Semantik folgt im zuständigen Meilenstein.

## Abgeschlossener Stabilisierungsschritt

Bevor die nächste Kompatibilitätsfläche wächst, wird der vorhandene Stand reproduzierbarer und
lokaler abgesichert. Dieser Block ändert keine VB6-Semantik:

- [x] Ein kanonisches `build.ps1` führt Restore, seriellen Release-Build, alle 13 Testprojekte und
      den VISIA-Report aus; die CI verwendet denselben Pfad. Der echte native OCX-Lauf bleibt über
      `VB6_REQUIRE_NATIVE_OCX=1` und einen x86-Testhost explizit zuschaltbar.
- [x] Das MSBuild SDK erhält `VB6TargetPlatform` mit x86 als Default sowie validiertem x64-/AnyCpu-
      Opt-in für `.vbp` und `.vbg`; die Auswahl wird als expliziter CLI-Schalter weitergereicht.
- [x] Jeder aktuell im Produktionscode vorhandene Diagnosecode erhält eine explizite Testreferenz
      und einen auslösenden Fall. Für den internen PDB-Fehlerkanal `VB6E0002` wird ausschließlich
      eine testbare interne Fehlerinjektionsgrenze ergänzt; öffentliche Compiler-APIs bleiben gleich.
- [x] Gezielte IR-/Emittertests sichern die Elision von `Debug.Assert`, Control-Array-`Load`/`Unload`
      samt Write-back, x86-/x64-PE-Header und SAFEARRAY-Rückgabe-Metadaten unterhalb der E2E-Ebene.

## Verbindlicher Managed-Abschlussplan

Der folgende Plan schließt alle offenen Managed-Verträge. Ausgenommen bleiben ausschließlich
LLVM, LSP, IDE, visueller Designer und Visual-Studio-CPS. Persistierte Designer- und
Enterprise-Artefakte gehören dagegen zum Compilerumfang und müssen geladen, emittiert und
ausgeführt werden können.

Die Umsetzung erfolgt seriell über die operative Luna-Queue. Pro Karte werden nur die
referenzierten Dateien und Tests gelesen; nach vier verifizierten Karten oder am Ende einer
Kartenfamilie läuft `build.ps1 -NoRestore -Configuration Release`. Die offenen Karten und ihre
Reihenfolge stehen in der Kompatibilitätsmatrix.

### Öffentlicher Profilvertrag

- [x] `VB6.Runtime` erhält `VBCompatibilityProfile` mit `Deterministic` und `VB6Sp6`;
      `VBCompilationOptions` erhält das additive Feld `CompatibilityProfile` mit
      `Deterministic` als Default.
- [x] Die CLI akzeptiert `--compatibility deterministic|vb6-sp6` für Analyse, IR-Dump und
      Managed-Emission. `vb6-sp6` wählt x86; explizites x64/AnyCpu wird abgelehnt.
- [x] Das MSBuild SDK erhält `VB6CompatibilityProfile` und reicht die Auswahl an die CLI weiter.
- [~] Das Profil wird in IR und Assembly-Metadaten festgehalten. `VBStrings.StrConv` ist als
      profilabhängige Runtime-Überladung verdrahtet; Casing sowie die kombinierten `vbWide` /
      `vbNarrow`- und japanischen `vbKatakana`/`vbHiragana`-Konversionen folgen der aktiven
      Locale; ein expliziter `StrConv`-LCID überschreibt dabei die Prozesskultur. Nicht anwendbare
      Regionen werden zurückgewiesen. `LenB`, `Asc` und `Chr` verwenden im
      `VB6Sp6`-Profil zusätzlich die aktive ANSI-Codepage. Weitere APIs werden additiv ergänzt,
      während vorhandene Signaturen ihr deterministisches Verhalten behalten.
- [x] `WinFormsHost` erhält das Profil instanzbezogen; generierte WinForms-Programme lesen die
      Auswahl aus ihren Assembly-Metadaten. Generierte Programme verwenden weiterhin die eigene
      Runtime und delegieren keine Sprachsemantik an `msvbvm60.dll`.
      Zum COM-/ActiveX-Pfad, nachgemessen: Profilabhängig sind Zeichenketten-, Datums-, Datei- und
      Variantverträge — die COM-Adapter berühren keinen davon. Eine BSTR ist immer UTF-16, und die
      Datumsumrechnung über die Grenze ist eine OADate-Zahl ohne Kultur. Die Dispatch-LCID folgt
      bewusst `CurrentCulture`; diese Entscheidung steht in `CLAUDE.md` und bleibt.

### Etappe A — Kompatibilitätsmatrix und messbarer Umfang

Der Date-/Time-Teilvertrag ist inzwischen profilbewusst: `DateValue`/`TimeValue` parsen im
`VB6Sp6`-Profil nach der aktiven Locale, `WeekdayName`/`MonthName` liefern lokalisierte Namen,
`DateAdd` rundet die Intervallzahl über den gemeinsamen VB-Long-Vertrag und `DatePart` folgt bei
`vbUseSystem` der Kalenderwochenregel der aktiven Kultur. Die vollständige Variant-
Rückgabematrix bleibt in Etappe B/C offen.

- [x] Die maschinenlesbare Matrix liegt unter `docs/vb6-sp6-compatibility-matrix.json` und
      inventarisiert die zentralen Vertragsflächen von Sprache, Runtime, Projekten, COM/ActiveX,
      Forms und Build. Die atomare Zerlegung für L1-02 bis L1-04 ist vollständig materialisiert;
      die Implementierung der offenen Intrinsics, Stock-Controls und übrigen Vertragsflächen bleibt
      in dieser Etappe sichtbar. Der Array-/UDT-Shape-Vertrag ist mit Rang-/Bounds-Erhalt,
      deterministischen UDT-Defaults und Shape-Diagnosen jetzt geschlossen; der Control-Flow- und
      Error-State-Vertrag ist mit expliziten CFG-Kanten, Handler-/Resume-Zielen und stabilen
      Diagnosen ebenfalls geschlossen. Die acht zuvor fehlenden Standard-Intrinsics sind
      ebenfalls implementiert; als nächste offene Implementierungskarte folgt
       `l1-02-l-locale-datetime-math-financial` ist nach der gezielten Runtime- und Managed-
       Messung geschlossen: Locale-/Profilgrenzen für Date/Time und Format, die deterministischen
       Math-/Random-Verträge sowie alle aktuell unterstützten Financial-Intrinsics sind durch die
       vier karteneigenen Testdateien abgedeckt. `l1-02-m-headless-host-services` ist mit
       expliziten `IVB6Host`-Hooks für Message-/InputBox, Registry, Screen und Clipboard jetzt `partial`:
       der Registry-Vertrag (`Get`/`Save`/`Delete`/Enumeration) und der Clipboard-Grundvertrag
       (`Clear`, Text, Daten und Formate) besitzen portable Mehrformat-Fallbacks. `Screen`
       bindet aktive Form/Control, DPI-basierte Twip-Faktoren und `MousePointer` explizit; ohne
       Desktop liefert der Runtimevertrag keinen aktiven Host und 96-DPI-Umrechnung. `Printer`
       deckt die ausgewählte Instanz, Kern-Eigenschaften, Seiten-/Dokumentzustand, Text, Messen,
       Skalieren und den sicheren Host-Übergang ab; ohne annehmenden Host entsteht ausdrücklich
       kein physischer Druckauftrag. Die derzeit 118
       Erwartungen tragen getrennte, maschinenprüfbare Statusachsen (117 `implemented`,
       1 `partial`, 0 `planned`; 118 `documented-verified`); jede weitere Karte behält ihre
       eindeutige Erwartungs-ID.
- [x] Die Quellenrangfolge ist fest: offizielle VB6-Dokumentation, veröffentlichte
      Windows-/OLE-/COM-Spezifikationen, beobachtbares Verhalten installierter Binärkomponenten,
      danach VISIA und weitere Legacy-Projekte.
- [x] Erwartungsdaten werden portabel gespeichert, sodass ein später verfügbarer VB6-SP6-Runner
      dieselben Fälle optional auf `oracle-verified` anheben kann. Ohne Originalcompiler bleibt
      `documented-verified` der verbindliche Abschlussstatus.

### Etappe B — Sprache, Variant, Klassen und Fehlerbehandlung

- [~] Lexer, Parser und Binder gegen die Matrix auf vollständige VB6-SP6-Syntax, Deklarationen,
      Statements, Named Arguments, Auswertungsreihenfolge und Kontextregeln schließen.
      Stand: Die Matrixkarte `l1-02-a` bleibt als **breiter Familienstatus** bewusst `partial` —
      sie deckt die gesamte Deklarations- und Sichtbarkeitsfläche ab, nicht einen einzelnen
      Vertrag, und wächst mit jedem Sprachmerkmal weiter. Sichtbarkeit über Modulgrenzen, die
      `VB6S0001`-Diagnose und die `IsPublic`-Metadaten sind gemessen.
- [x] Eine zentrale Variant-Subtyp-, Konvertierungs- und Promotionstabelle deckt `Empty`, `Null`,
      `Nothing`, `Missing`, `Error`, `Decimal`, `Date`, `Currency`, Strings, Objekte und Arrays für
      alle Operatoren, Überläufe, Rundungen und Type-Mismatch-Fälle ab (`l1-02-e`, `l1-02-h`).
      Eine nicht darstellbare SAFEARRAY- oder Objektform scheitert dabei mit Diagnose, statt
      flachgeklopft zu werden.
- [~] `Let`/`Set`, Default-Member, `DISPID_VALUE`, Collection-Randfälle, `As New`, `Implements`,
      Events und `WithEvents` erhalten den vollständigen Objektvertrag. Im `VB6Sp6`-Profil wird
      die Initialize-/Terminate-Lebensdauer explizit geführt und nicht dem GC überlassen.
      Stand: Der Objektvertrag ist geschlossen und gemessen (`l1-02-i`), einschließlich
      `WithEvents` mit Umverdrahtung und Trennung. `Class_Terminate` **läuft** — das war bis
      09/2026 nicht der Fall: Es hing allein am Finalizer, und die CLR führt beim Prozessende keine
      ausstehenden Finalizer aus, weshalb eine Messung es in *keinem* Fall laufen sah. Ein schwaches
      Register entleert es beim Herunterfahren, jüngstes zuerst. **Offen bleibt der Zeitpunkt**:
      VB6 zählt Referenzen und beendet in dem Moment, in dem die letzte geht. Das ist eine
      Architekturfrage — eine halbe Referenzzählung würde Terminate auf einem lebenden Objekt
      auslösen, und das ist schlimmer als zu spät.
- [x] Der Managed-IR-Fehlerautomat bildet die aktiven/inaktiven `On Error`-/`Resume`-Zustände
      im getesteten Managed-Pfad ab: `Err`, Fehlernummern, `Erl` für numerische Zeilenlabels,
      Wiederaufnahmegrenzen und das Weiterreichen eines Fehlers aus einem aktiven Handler sind
      implementiert. `Resume`, `Resume Next` und `Resume <Label>` schließen den aktiven Handler
      gemäß dem dokumentierten Zustandsvertrag; ein explizites `Exit Sub` aus einem aktiven
      Handler leert `Err`.
- [x] Ein `Public`-Feld einer Klasse ist echter Speicher, kein `Property Get` (`S1`, geschlossen).
      `ByRef`-Rückschreiben, `Set` auf Objekt-/Variant-Felder, die Indizierung array-typisierter
      Felder, `String * n` als Klassenmember und der spät gebundene Zugriff über `Object` oder
      `Variant` sind implementiert. Ein `Private`-Feld bleibt dabei von aussen unerreichbar: der
      Emitter gibt einem `Public`-Feld CLR-Assembly-Sichtbarkeit, einem privaten CLR-private, und
      die Feldsuche des Dispatchs richtet sich danach.
- [x] Arraygrenzen eines UDT-Members werden aus konstanten Ausdrücken gefaltet — benannte
      Konstanten unabhängig von der Deklarationsreihenfolge, `+ - * \` mit Überlaufprüfung. Was
      nicht faltet, meldet `VB6S0071`; eine Obergrenze unter der Untergrenze meldet `VB6S0072`.
      Vorher fiel jede nicht-literale Grenze **ohne Diagnose** durch und erzeugte ein Member ohne
      Speicher, das zur Laufzeit abstürzte.
- [x] Die Breite eines `String * n` folgt derselben Faltung wie eine Arraygrenze. Der Falter liegt
      als `VBIntegerConstantFolder` für sich; beide Prüfstellen — UDT-Member und Deklarator —
      rufen ihn, sodass eine benannte Konstante überall dasselbe bedeutet.
- [x] `String * n` gilt in allen vier Deklarationsformen — lokal, Modulvariable, Klassenfeld und
      UDT-Member — mit einheitlicher Breite: *n* Leerzeichen als Anfangswert, Abschneiden beim
      Überschreiten und Auffüllen beim Unterschreiten. Eine benannte Konstante als Länge bleibt
      in allen Formen außerhalb der aktuellen Teilmenge (`VB6S0043`).
- [x] Ein `String * n` an einen `ByRef s As String` geht mit Copy-in/Copy-out durch, wie in VB6.
      Der Zielkonflikt ist an der obersten Regel entschieden: Altcode übergibt so, und die
      typstrenge ByRef-Regel gilt weiterhin einer Variablen des falschen Typs.
- [x] Eine `Property Get` mit Array-Rückgabetyp wird gerufen und ihr Ergebnis indiziert. Eine
      falsche Zahl von Indizes bleibt `VB6S0027` und wird nicht stillschweigend übersetzt.
- [x] Eine Klasse mit `Property Get` **und** `Property Set` gleichen Namens liest richtig
      zurück. Ursache war die Namensauflösung im Set-Pfad: Ein blanker Name wurde gegen die
      Set-Property der Klasse geprüft, bevor der lokale Gültigkeitsbereich befragt wurde — womit
      `Set Obj = m_obj` **innerhalb** von `Property Get Obj` an die Property band statt an den
      Rückgabewert.
- [x] Der Zugriff auf ein **privates** Klassenfeld von aussen meldet `VB6S0074`. Das Feld bleibt
      in der Mitgliedsfläche, damit die Klasse es über `Me` weiter erreicht.
- [x] `Dim x As New C` erzeugt verzögert bei der ersten Verwendung — lokal, als Modulvariable,
      als Klassenfeld **und als Arrayelement**: `Dim a(1 To 3) As New C` gibt drei eigene Objekte,
      jedes bei seiner ersten Berührung erzeugt. Vorher wies der Binder die Deklaration mit
      `VB6S0063` ab, weil er `As New` gegen den Arraytyp statt gegen den Elementtyp prüfte.
      `Class_Terminate` läuft garantiert, aber nach der Uhr des Abbaus statt nach der letzten
      Referenz; der Zeitpunkt bleibt offen und ist dort beschrieben, wo der Objektvertrag steht.
- [x] Ein Mitgliedsaufruf auf einer nicht gesetzten Objektvariablen meldet **91**, früh wie
      spät gebunden. Die Zuordnung ist bewusst breit: sie trifft jeden Null-Zugriff, weil VB6
      an dieser Stelle 91 meldet und der vorherige Sammelwert 5 dasselbe verdeckte.
- [~] `ObjPtr`, `LSet`, `AddressOf` und native ByRef-Übergaben tragen. `VarPtr`/`StrPtr` gelten
      dort, wo VB6 den Zeiger sofort weiterreicht — als `ByVal … As Any`-Argument eines `Declare`.
      Ein Zeiger, den das Programm behalten könnte, bleibt offen: Er überlebt keinen Sammellauf,
      und pauschales Pinnen aller Variablen ist die Antwort, die diese Zeile ausschließt.

### Etappe C — Runtime, Standardbibliothek, Datei-I/O und Projekte

- [~] Alle dokumentierten String-, Math-, Financial-, Datum/Zeit-, `Format`-, Array-,
      Konvertierungs-, Information-, Interaction-, Environment-, Registry-, App-, Screen-,
      Printer- und Clipboard-Verträge implementieren.
      Stand: Die Host-Services sind geschlossen und gemessen (`l1-02-m`) — Screen, Printer,
      Clipboard, Registry, MsgBox/InputBox laufen headless mit dokumentierten Vorgabewerten.
      Die `Format`-Fläche wurde in der Breite nachgemessen: 16 benannte Formate, numerische
      Muster mit bis zu vier Abschnitten, 14 Datumsmuster und die Zeichenkettenmasken. Ein Defekt
      dabei — jeder String ging unabhängig vom Muster in den Zeichenkettenformatierer, weshalb
      `Format("12", "0.00")` den Wert verlor. Behoben und festgeschrieben.
      Die Familienzeile bleibt trotzdem offen: `Format` und `Math` sind Flächen, deren
      Vollständigkeit sich nicht abschließend behaupten lässt — eine Messung, die keinen Defekt
      mehr findet, ist kein Beweis, dass keiner mehr da ist.
- [x] `StrReverse`, `FormatNumber`, `FormatCurrency`, `FormatPercent`, `FormatDateTime`,
      `Partition`, `CallByName` und `QBColor` sind als Standard-Intrinsics deklariert und im
      Managed-Pfad implementiert. `CallByName` verwendet den vorhandenen dynamischen Dispatch.
- [x] `Open`, `FileLen`, `Kill` und `FileDateTime` melden auf einem fehlenden Pfad **53**.
      `Kill` lief vorher still durch und `FileDateTime` lieferte ein Datum, weil .NET an beiden
      Stellen nicht wirft; die beiden brauchen deshalb eine eigene Existenzprüfung. Ein
      `Collection`-Index ausserhalb der Sammlung meldet **9**, ein unbekannter Schlüssel
      weiterhin **5**; die Position von `Add`s `Before`/`After` bleibt bewusst **5**, weil sie
      dort ein ungültiges Argument ist und kein Subscript.
- [x] `Left`, `Right`, `Mid`, `Trim`, `LTrim`, `RTrim`, `UCase`, `LCase` und `Len` reichen `Null`
      weiter; die Dollar-Formen sind `String -> String` und melden dort **94**. Die Messung fand
      das bereits richtig vor — die Zeile beschrieb einen Zustand, den es nicht mehr gab.
- [x] `VB6Sp6` verwendet System-LCID und ANSI-Codepage; `StrConv` (einschließlich
      locale-gesteuertem `vbWide`/`vbNarrow` und japanischem Kana), `LenB`, `Asc`, `Chr`,
      `Format`, `DateValue`/`TimeValue`, `WeekdayName`/`MonthName` sowie `IsDate`/`IsNumeric`
      decken die profilbewusste Locale-Schicht bereits ab. Locale-/DBCS-Tests decken
      mindestens
      `en-US`, `de-DE` und `ja-JP` einschließlich `LenB`, `Asc`, `Chr`, Datum und Zahlen ab.
- [x] Datei-I/O für Binary, Random, Input, Output und Append einschließlich `Get`/`Put`,
      `Input #`, `Line Input`, `Write #`, `Print #`, `Lock`/`Unlock`, `Reset`, `EOF`, `Loc`, `LOF`,
      `Seek` und vollständiger UDT-/String-/Array-/Variant-Record-Layouts schließen. `Loc` ist
      jetzt compilerseitig gebunden und meldet die dokumentierten Einheiten für Binary (Byte),
      Random (Datensatz) und Sequential (128-Byte-Block); `Reset` schließt alle offenen Kanäle und
      `Write #` serialisiert mehrere String-/Boolean-/Null-Werte im dokumentierten Format und
      `Lock`/`Unlock` sperren 1-basierte Binary-/Random-Bereiche sowie bei Sequential die gesamte
      Datei. `Open ... Shared` sowie `Lock Read`/`Lock Write`/`Lock Read Write` werden auf
      explizite FileShare-Regeln abgebildet; `Access Read`/`Write`/`Read Write` setzen die
      entsprechenden `FileAccess`-Rechte. Variant-Arrays als Variant-Wert/Objekt-Layouts, komplexere
      UDT-Formen sind abgedeckt, und eine nicht darstellbare Objektform wird ausdrücklich
      gemeldet statt flachgeklopft (`l1-02-n`, `l1-03-i`); ohne `For` wird jetzt der
      dokumentierte Random-Modus mit Standardlänge 128 verwendet. `Print #` akzeptiert außerdem
      die leere Outputliste und
      schreibt eine reine CRLF-Zeile. Mehrere Print-Ausdrücke mit Semikolon (direkte Verkettung),
      Komma (nächste Ausgabezone) und abschließendem Semikolon (Fortsetzung im nächsten Print)
      werden ebenfalls geparst, gebunden und emittiert.
      `Width #` begrenzt fortgesetzte Print-Zeilen auf 0 bis 255 Zeichen (0 = unbegrenzt) und
      erzeugt bei Erreichen der Breite ein CRLF vor dem nächsten Wert. `Input #` stellt für
      Variant-Ziele die von `Write #` erzeugten Empty-/Null-/Boolean-/Date-/Error-Marker sowie
      skalare Zahlen wieder her; binäre `Get`-/`Put`-Transfers führen für skalare Variant-Felder
      das VB6-Typ-Tag samt Payload. Für `Print #`, `Write #`, `Input #` und `Line Input #` reicht
      der Emitter das Kompatibilitätsprofil explizit an die Runtime weiter: `Deterministic`
      bleibt bei UTF-8 einschließlich BOM-Behandlung, `VB6Sp6` verwendet die aktive Windows-
      ANSI-Codepage. Variant-Arrays/Objekte und eingehende SAFEARRAY-Tags werden als expliziter
      Typfehler zurückgewiesen; deren vollständige Speicherung, komplexere UDT-Layouts und
      weitere Dateiformate bleiben offen.
- [x] `.vbp`/`.vbg` einschließlich Projektarten, Version/Binary Compatibility, Ressourcen,
      Referenzen, Komponenten und Abhängigkeiten vollständig auswerten; `.frm`, `.frx`, `.ctl`,
      `.ctx`, `.pag`, `.dob`, `.dsr` und `.res` verlustfrei laden. Die Kernklassifikation für
      EXE sowie `OleDll`/`OleExe`/`Control`/`Dll` und die ActiveX-Äquivalente, `Sub Main` oder
      Form-Start, Artefaktnamen und den x86-Projektdefault ist verifiziert. Deklarierte VBG-
      Projektreferenzen werden aufgelöst, vor ihren Verbrauchern emittiert und Zyklen als stabile
      Gruppendiagnose ohne Teil-Artefakte gemeldet. Versions-/Binary-Compatibility-Metadaten bleiben
      adressierbar; deklarierte Ressourcen-, TypeLib- und OCX-Dateien gehören ausschließlich zum
      exakten Eingabemanifest. Die mit `ResFile32` benannte Ressourcendatei wird geladen, in die
      Assembly **eingebettet** und über `LoadResString`/`LoadResData`/`LoadResPicture` gelesen.
      Binary Compatibility heißt hier abgeleitete statt erzeugter Identitäten: dieselbe Quelle
      ergibt dieselbe CLSID, dieselbe ProgID und eine bytegleiche Typbibliothek.
      Die Component-Package-Emission gehört **nicht hierher**: Ein Verteilpaket — CAB, `setup.exe`,
      `.DEP` — ist die Ausgabe des Package-and-Deployment-Assistenten, nicht die des Compilers.
      Sie setzt eine Entscheidung über Zielmaschine und Laufzeitverteilung voraus, die ein
      Übersetzungslauf nicht trifft. Der Platz dafür ist Meilenstein 10.

### Etappe D — COM-, ActiveX- und Win32-x86-ABI

- [x] TypeLib-Import auf duale und VTable-Interfaces, Aliase, Records, verschachtelte UDTs,
      Pointer, C-Arrays, vollständige Automationtypen und ByRef-Write-back erweitern.
      Stand: Aliase, Records, feste C-Arrays und die Automationtypen einschließlich `VT_INT`,
      `VT_ERROR` und wertständiger Zeiger sind umgesetzt und gegen die registrierte
      `stdole2.tlb` gemessen (`l1-03-j`). Der **vtable-Aufruf** trägt ebenfalls: ein Member einer
      IUnknown-abgeleiteten Schnittstelle wird über seinen Slot gerufen, nicht über IDispatch —
      gemessen an `stdole.IFont.SetRatio`, das nur dort existiert. Ein Member mit
      `[out]`-Parameter nimmt diesen Weg bewusst **nicht**: Dort schreibt der Server in Speicher,
      den der Aufrufer stellt. Statt ihn auf dem Dispatchweg mit einem irreführenden 438 enden zu
      lassen, meldet der Binder **VB6S0075** an der Aufrufstelle (`IFont.Clone` ist der gemessene
      Fall — sein letzter Parameter trägt `PARAMFLAG_FOUT`, nicht `FRETVAL`).
- [x] `IDispatch` vollständig mit LCID, Named Arguments, `DISPID_VALUE`, `DISPID_PROPERTYPUT`,
      `EXCEPINFO`, optionalen Parametern und Default-Properties abbilden.
- [x] `Declare` und `AddressOf` für die dokumentierten x86-Signatur-, Callback-, String-, Pointer-,
      UDT- und Arrayformen schließen.
- [x] COM-Server erhalten vollständige Interfaces/Coclasses, `IUnknown`/`IDispatch`, Connection
      Points, Event-Source-Interfaces sowie Instancing-, Threading- und Binary-Compatibility-
      Verträge. Binary Compatibility heißt hier: Die Identitäten sind aus den Namen abgeleitet,
      nicht erzeugt — zwei Übersetzungen derselben Quelle ergeben dieselbe CLSID, dieselbe ProgID
      und eine bytegleiche Typbibliothek. Threading ist der STA-Local-Server beziehungsweise
      `ThreadingModel=Both` im Manifest.
- [x] Typbibliotheken über `ICreateTypeLib2` als `.tlb` erzeugen und in Registrierung sowie
      registry-free Manifest führen. ActiveX-DLLs verwenden den x86-`comhost`; ActiveX-EXEs
      erhalten einen Local Server mit `/Embedding`, `/Automation`, `CoRegisterClassObject`,
      Message Pump und sauberem Shutdown.

### Etappe E — Forms, Zeichnen, MDI und intrinsische Controls

- [x] Form-/Control-Lifecycle, Fokus, Tab-Reihenfolge, Z-Order, Modalität, Defaultinstanzen, Menüs,
      Timer, Events und die vollständige intrinsische Control-Oberfläche schließen.
      Stand: Lebenszyklus (Initialize/Load/Activate/QueryUnload/Unload/Terminate), Modalität über
      `Show vbModal`, Defaultinstanzen, Menüs samt Menü-Arrays, Timer und die Ereignisfläche sind
      umgesetzt und gemessen (`l1-04-c`, `l1-04-d`), ebenso `TabIndex`/`TabStop` aus der
      Designer-Hülle und `ZOrder` mit der VB6-Bedeutung von 0 und 1.
- [x] Control-Arrays um Form-, Menü- und UserControl-Arrays sowie vollständiges dynamisches
      `Load`/`Unload` ergänzen.
      Stand: Intrinsische Control-Arrays und Menü-Arrays tragen `Load`/`Unload` mit den
      dokumentierten Fehlern (`l1-04-e`); ein Array von Formularen entsteht über
      `Dim f(1 To n) As New frmX` und ist damit abgedeckt. Das UserControl-Array im Designer trägt
      ebenfalls: Die Designer-Hülle bündelt nach der `Index`-Eigenschaft, nicht nach der Art des
      Controls, weshalb ein `.ctl` dieselbe Maschinerie nimmt wie ein intrinsisches Control
      (`UserControlArrayExecutionTests`).
- [x] Der `VB6Sp6`-Zeichenpfad verwendet GDI-basierte DC-/DIB-Flächen für `PSet`, `Point`, `Line`,
      `Circle`, `PaintPicture`, `Cls`, Zeichen-/Füllattribute, `Scale*`, `AutoRedraw` und alle
      16 `DrawMode`-/ROP2-Werte.
      Stand: Die beobachtbare Fläche ist vollständig und pixelweise gemessen (`l1-04-f`,
      `l1-04-g`, `l1-04-h`) — einschließlich `DrawWidth`, Beschneidung und der sechzehn
      ROP2-Wahrheitstabellen auf aktiver und persistenter Fläche. Die Umsetzung arbeitet dabei auf
      verwalteten Bitmaps statt auf einem nativen DC/DIB; das ist eine andere Bauart als die Zeile
      beschreibt, kein anderes Verhalten.
- [x] MDI vollständig um Parent-/Child-Lifecycle, `ActiveForm`, Cascade/Tile/Arrange,
      WindowList-Menüs, Menüübernahme, Fokus und persistente Fensterzustände ergänzen.
      Stand: Kindzuordnung, `ActiveForm`, Arrange, die WindowList-Markierung und der
      Fensterzustand eines Kindes (`WindowState` mit 0/1/2) sind gemessen (`l1-04-i`).

### Etappe F — Stock-OCX, UserControls und Enterprise-Artefakte

- [x] Alle Microsoft-redistributablen VB6-Stock-Controls werden in der Matrix geführt. Installierte
      Controls laufen nativ; fehlende Controls werden über ABI-Testkomponenten geprüft und sichtbar
      als nicht nativ verifiziert markiert.
- [x] Die generische ActiveX-Schicht unterstützt TypeLib-beschriebene Drittanbieter-Controls mit
      OLE-In-Place-Aktivierung, Ambient Properties, Property Pages, Persistence und Connection
      Points; undokumentiertes controlspezifisches Verhalten bleibt außerhalb des Vertrags.
      Stand: Persistenz über `IPersistPropertyBag` und Connection Points sind umgesetzt und gegen
      registrierte 32-Bit-Stock-Controls gemessen — einschließlich verschachtelter Gruppen
      (ImageList-Bilder, Toolbar-Buttons) und der `.frx`-Nutzlasten dahinter. Offen sind
      Property Pages und Controls, die ihren Zustand **ausschließlich** über
      `IPersistStreamInit` führen; unter den gemessenen Stock-Controls ist keines davon
      betroffen.
- [x] Generierte UserControls erhalten echte ActiveX-/OLE-View-/In-Place-Verträge,
      PropertyBag-/Stream-Persistenz, Ambient Properties, Events, Property Pages und vollständigen
      Lifecycle. Die Wahl zwischen `InitProperties` und `ReadProperties` fällt am Ende der
      Designer-Hülle, weil erst dort feststeht, ob der Container etwas abgelegt hat; vorher war die
      Tüte immer leer und `ReadProperties` lief nie. Property Pages sind Entwurfszeitfläche des
      Containers und gehören zur zurückgestellten IDE.
- [x] DataEnvironment, DataReport, UserDocument und PropertyPage werden aus ihren persistierten
      Artefakten kompiliert und ausgeführt. ADO/OLE DB wird über COM konsumiert; Datenbank-Provider
      werden nicht neu implementiert.
      Stand: PropertyPage und UserDocument werden klassifiziert, übersetzt und **ausgeführt**;
      `Designer=` behält seinen deklarierten Typ (`l1-04-n`). DataEnvironment und DataReport
      hängen an nativen ADO-Komponenten, die über COM konsumiert werden — ihre Abwesenheit bleibt
      sichtbar.

### Etappe G — Headless MSBuild SDK

- [x] Eine gepackte Resolver-Task ermittelt aus `.vbp`/`.vbg` die exakten Quellen, Ressourcen,
      Projektverweise, COM-Referenzen und Ausgaben und ersetzt die bisherigen rekursiven Globs.
      Der CLI-Resolver schreibt bereits deklarationsbasierte SHA-256-Input-Manifeste; eine
      eigenständige gepackte ProjectSystem-Task bleibt offen.
- [x] Stabile Targets `ResolveVB6Project`, `GetVB6ProjectOutputs`, `CompileVB6Project` und
      `CompileVB6ProjectGroup` sind für deklarationsbasierte Inputs, inkrementellen No-op,
      TargetPath, PDB, Runtime, Runtimeconfig, Manifest und COM-Host vorhanden. Clean/Rebuild-
      Orchestrierung löscht die manifestierten Legacy-Ausgaben deterministisch; vollständige
      TypeLib-/Outputauflösung bleibt offen.
- [x] `DesignTimeBuild=true` führt Validierung und deklarationsbasierte Auflösung aus; die
      Compile-Targets werden übersprungen. Visual-Studio-CPS, Projektbaum und IDE-Kommandos bleiben
      ausgeschlossen.

### Etappe H — Abschlussgate und Dokumentationsstatus

- [x] Jeder Matrixeintrag besitzt mindestens Parser-/Binder-, Runtime- oder Emitter- und Managed-
      End-to-End-Abdeckung; beobachtbare Profilunterschiede erhalten tabellengetriebene Tests.
      Ein Test prüft das maschinell: jede Erwartung nennt Tests, diese Tests existieren, und die
      in README und Roadmap zitierten Zahlen sind die Zahlen der Matrix.
- [x] Raw-COM-Probes prüfen VTables, DISPIDs, VARIANT-/SAFEARRAY-Layouts, Referenzzählung,
      ByRef-Write-back, Events, Registrierung und registry-free Aktivierung in beide Richtungen
      mit kontrollierten Testkomponenten. Beide Richtungen laufen: generierter Code gegen echte
      Fremdserver (`ComInteropExecutionTests`), und unsere Klassen von einem Fremdprozess aus --
      in-process über `VB6.ComActivationProbe`, out-of-process über `LocalServerActivationTests`.
      Die zunächst als „nur mit einer aus IDL gebauten Komponente prüfbar" geführten Formen sind
      inzwischen an **registrierten** Bibliotheken gemessen: `stdole2.tlb` liefert Aliase, Records,
      feste C-Arrays und vtable-Schnittstellen, `MSCOMCTL.OCX` 42 Enums und 48 Coklassen,
      `scrrun.dll` und `msado15.dll` weitere Dispatch- und vtable-Flächen. Eine eigene
      IDL-Komponente wird dafür nicht mehr gebraucht.
- [x] Forms-Tests prüfen Lifecycle-/Eventtraces, MDI und Control-Arrays; GDI-Zeichenoperationen
      erhalten Pixeltests bei festem DPI und Theme. Lifecycle, MDI, Control-Arrays einschließlich
      Menü-Arrays und die Zeichenprimitive sind abgedeckt. Die Pixeltests hängen nicht an der DPI
      des Testhosts: Sie setzen `ScaleMode = 3` (Pixel) und eine ausdrückliche Flächengröße, lesen
      also dieselben Bildpunkte unabhängig von der Skalierung des Rechners.
- [x] Der kanonische Build, alle vorhandenen Regressionen und VISIA 40/40 bleiben grün. Das
      deterministische Profil darf sich in keinem bestehenden Snapshot verändern.

#### Was bewusst `documented-verified` bleibt

Kein Eintrag der Matrix trägt `oracle-verified`, und ein Test hält das fest
(`CompatibilityMatrixTests`): Dieser Status darf nur nach einem Lauf gegen einen echten
VB6-SP6-Compiler stehen, und ein solches Orakel existiert für dieses Projekt nicht. Die folgenden
Flächen bleiben deshalb dauerhaft dokumentationsgestützt, jede aus einem eigenen Grund:

1. **Locale-abhängige Datums- und Kalenderfälle.** `Weekday(d, vbUseSystemDayOfWeek)` und
   `Format(d, "ww")` lösen `vbUseSystem` über `CurrentCulture` auf. Das ist VB6-treu und verletzt
   zugleich die Determinismus-Entscheidung dieses Projekts; der Zielkonflikt ist offen und wird
   nicht einseitig aufgelöst.
2. **Fälle, in denen die Dokumentationsherleitung nachweislich falsch war.** `CDec(Null)` und
   `CInt(CVErr(5))` verhalten sich anders, als die Doku nahelegt, und ein bestehender Test ist
   dort der bessere Zeuge. Ohne Orakel wird daran nichts „korrigiert".
3. **Exotische Typbibliotheksformen.** VT_CARRAY, Pointer-auf-Pointer und frühgebundene
   vtable-Interfaces brauchen eine aus IDL gebaute Testkomponente. Dafür fehlt auf dieser Maschine
   das Windows SDK — kein `midl.exe`, kein `oaidl.h`. Der Aufrufpfad selbst ist gegen echte
   Fremdserver gemessen (`Scripting.Dictionary`, `Scripting.FileSystemObject`), die exotischen
   Formen sind es nicht.
4. **Native OCX-Flächen.** Sie hängen an registrierten x86-Controls und laufen nur im
   ausdrücklichen Opt-in-Lauf (`VB6_REQUIRE_NATIVE_OCX=1`, `TargetPlatform=x86`). Im normalen Lauf
   überspringen sie sich selbst und sagen dort nichts aus — das Stock-Control-Inventar führt
   deshalb `native-only` als eigene Einstufung.
5. **Undokumentiertes controlspezifisches Verhalten.** Steht ausdrücklich außerhalb des Vertrags
   und soll dort bleiben.

- [~] Der Managed-Abschluss ist erreicht, wenn außerhalb der ausdrücklich ausgeschlossenen
      LLVM-/LSP-/IDE-Flächen keine Implementierungszeile mehr `[~]` oder `[ ]` ist. Fehlende echte
      VB6-Gegenprüfung bleibt als Validierungsstatus sichtbar, blockiert diesen Abschluss aber nicht.

      **Stand 2026-09-04: fünf Zeilen tragen noch `[~]`, und keine davon ist eine offene
      Aufgabe.** Sie stehen hier namentlich, damit dieser Punkt nicht als Restliste missverstanden
      wird — und damit auffällt, falls doch eine sechste dazukommt:

      | Zeile | Was sie ist | Warum sie `[~]` bleibt |
      | --- | --- | --- |
      | Profil-Locale | Familienzeile | „Weitere APIs werden additiv ergänzt" — sie wächst mit jedem profilabhängigen Vertrag mit. |
      | `l1-02-a` Syntax/Kontext | Familienzeile | Deckt die gesamte Deklarations- und Sichtbarkeitsfläche ab, nicht einen Vertrag. In `CLAUDE.md` ausdrücklich als bewusst `partial` geführt. |
      | Objektvertrag | **Architekturfrage** | `Class_Terminate` läuft garantiert, aber nach der Uhr des Abbaus statt nach der letzten Referenz. Eine echte Referenzzählung ist eine Entscheidung, keine Lücke. |
      | `VarPtr`/`StrPtr` | **Architekturfrage** | Ein behaltbarer Zeiger überlebt keinen Sammellauf; pauschales Pinnen schließt die Zeile selbst aus. |
      | Standardbibliothek | Familienzeile | `Format` und `Math` sind Flächen; eine Messung ohne Fund ist kein Beweis der Vollständigkeit. |

      Die drei Familienzeilen sind per Konstruktion nie `[x]` — sie messen eine Fläche, keinen
      Vertrag. Abhaken ließe sich dieser Punkt also erst, wenn die **zwei Architekturfragen**
      entschieden sind. Beide sind benannt, keine ist verdeckt.

---

## Meilenstein 0 — Paritätsmessung ✅

`vb6c <projekt.vbp> --report` liefert Item-Inventar, Anteil fehlerfrei analysierter Dateien und
die nach betroffenen Dateien sortierten Lücken. Siehe Ist-Stand oben.

## Meilenstein 1 — Bitweise Semantik und Zahlliterale ✅

`&H`/`&O`-Literale mit VB6-Wrapping, `&`/`%`-Typsuffixe an Literalen, bitweise
`And`/`Or`/`Xor`/`Eqv`/`Imp`/`Not` auf Numerik.

## Meilenstein 2 — Dateien überhaupt lesbar machen ✅

- [x] `Attribute`-Zeilen auf Modulebene
- [x] Deklarationen auf Modulebene: `Public`/`Private`/`Global`/`Dim`
- [x] Projektweite Sichtbarkeit: `Public`/`Global`-Modulvariablen werden in andere Module
      importiert, `Private`/`Dim`-Variablen bleiben auf das deklarierende Modul begrenzt;
      Fremdzugriff unter `Option Explicit` meldet `VB6S0001`
- [x] `Public`/`Private`/`Friend`-Modifizierer an `Sub` und `Function`
- [x] `Option Private Module` wird als externe Exportpolitik im Semantikmodell gebunden; öffentliche
      Mitglieder bleiben für Schwester-Module desselben Projekts sichtbar. Ein externer
      Standardmodul-Importpfad ist damit ausdrücklich noch nicht behauptet.
- [x] Bezeichner-Typsuffixe `$ % & ! # @`
- [x] Zeilenfortsetzung mit `_`
- [x] `Const`, typisiert und aus dem Wert abgeleitet
- [x] `Exit Sub` und `Exit Function`
- [x] `Declare`-Syntax mit `Lib`, optionalem `Alias` und `As Any`; Binding/PInvoke bleibt M8
- [x] `Enum ... End Enum` mit optionaler Sichtbarkeit sowie expliziten/impliziten Memberwerten; inzwischen auch als Long-basierte Konstanten gebunden
- [x] `Optional`-Parametersyntax mit `ByVal`/`ByRef` und optionalem Default-Ausdruck; ausgelassene Argumente/Defaults sind umgesetzt
- [x] `Option Base 0/1`, `Option Compare Text/Binary`; Stringrelationen und `Select Case` führen
      `Option Compare Text` bis zum Managed-Emitter, Array- und weitere Locale-Sonderfälle bleiben
      in Etappe B/C sichtbar
- [x] `:` als Anweisungstrenner für den aktuellen Statement-Subset, inklusive Single-Line-`If` und `Case`; Labels bleiben M6
- [x] Mehrfachdeklaratoren wie `Dim a As Integer, b As Long`; `As Type` gilt pro Deklarator, implizites Variant bleibt M4
- [x] `Static`-Local-Syntax und persistente Lebensdauer ueber Modul-Storage
- [x] `^`; `Like` mit `Option Compare`-Wildcardsemantik; `Is` mit Runtime-Identitätsvertrag für
      Variant-/Hostobjekte (echte Klasseninstanzen folgen M5)

**Nach M3 verschoben:** `With`-Blöcke und `.Feld`-Zugriff (19 Dateien, 629 Vorkommen). Sie
brauchen einen Member-Zugriff, den es ohne UDTs und Objekte nicht sinnvoll gibt.

## Meilenstein 3 — Arrays und UDTs ✅

Zusammen, weil Win32-Strukturen beides brauchen.

- [x] Array-Deklarationssyntax: `Dim x(10)`, `Dim x(1 To 10)`, mehrdimensional und dynamisch `Dim x()`; Grenzen werden verlustfrei im Syntaxbaum bewahrt
- [x] Arrayparameter-Syntax wie `TheArray() As String`; Parameter haben keinen statisch festgelegten Rang und ganze Arrays werden ByRef übergeben
- [x] `ArrayTypeSymbol` / `VBArray<T>` mit bekanntem oder dynamischem Rang, expliziten Unter-/Obergrenzen, Indexprüfung sowie `LBound`/`UBound`-Runtime-Grundlage
- [x] Arrayvariablen/-parameter binden; feste Arrays initialisieren; Arrayelemente lesen/schreiben/emittieren; `Option Base` auf implizite Untergrenzen anwenden; Arrayelemente ByRef weiterreichen
- [x] `ReDim` / `ReDim Preserve` für explizit typisierte dynamische Arrays inklusive Bounds, Codegen, Runtime-Wertbewahrung und End-to-End-Ausführung
- [x] `Erase`, `LBound` und `UBound` für typisierte Arrays inklusive Runtime-/Codegen-/End-to-End-Semantik
- [x] `Erase` auf `ByRef`-Arrayparametern deallokiert den Descriptor mit Caller-Write-back; nachfolgende
      `IsArray`-/`ReDim`-Aufrufe sehen den freigegebenen beziehungsweise neu angelegten Zustand
- [x] `For Each` über feste, mehrdimensionale und dynamische Arrays inklusive implizitem Variant-Steuerelement
- [x] `Type ... End Type`-Syntax mit Sichtbarkeit, skalaren/festen Arrayfeldern, verschachtelten Typnamen, Keyword-Feldnamen und `String * n`
- [x] `UserDefinedTypeSymbol`, case-insensitive UDT-Member, Vorwärtsreferenzen, `String * n`-Typen sowie Public-/Private-Projekt- und Modul-Scope
- [x] UDT-Werte als Parameter/Locals/Modulvariablen binden; Memberzugriff/-zuweisung, Memberarrays, Wertkopie-Semantik und Codegen; nicht abbildbare Layouts melden `VB6S0046`
- [x] `With`-Blöcke mit implizitem `.Member`-Zugriff über einen gebundenen Empfänger-Alias (aus M2 hierher verschoben)
- [x] `For Each` über Arrays von benutzerdefinierten Typen: **von VB6 nicht erlaubt**, daher
      dauerhaft `VB6S0056` statt einer Implementierung

### Warum `For Each` über UDT-Arrays nicht kommt

`For Each` verlangt eine Variant-Steuervariable. VB6 coerct einen benutzerdefinierten Typ nur
dann in eine Variant, wenn er public in einem *public object module* deklariert ist — ein `Type`
in einer `.bas` erfüllt das nie und liefert in VB6 den Fehler „Only public user defined types
defined in public object modules can be coerced to or from a variant". Der Punkt schließt sich
damit durch Verifikation statt durch Implementierung: `VB6S0056` ist kein Platzhalter für eine
Lücke, sondern die Regel.

Zwei Nachträge:

- Die Ausnahme (public UDT in einem public object module) wird erst relevant, wenn es
  Klassenmodule gibt. Frühestens M5, praktisch mit ActiveX in M9.
- **Gegen echtes VB6 verifizieren.** Die Regel stammt aus der dokumentierten VB6-Fehlermeldung,
  nicht aus einem Lauf in der Original-IDE — dieselbe Vorsicht wie bei `Currency + Double`.

## Meilenstein 4 — Variant

- [x] Variant als semantischer Typ mit Speicherung und expliziten Konvertierungen
- [x] Untypisierte `Dim`-, `Static`- und Modul-Deklaratoren werden vor dem Binden zu Variant normalisiert
- [x] `Function` ohne `As`-Klausel liefert Variant — Syntax, Normalisierung, Bindung und Ausführung
- [x] Untypisierte `Optional`-Parameter werden Variant; ausgelassene Werte erhalten den `Missing`-Zustand
- [x] `VBVariant`: `Empty`, `Null`, `Nothing`, `Missing`, `VarType`, `IsEmpty`/`IsNull`/`IsMissing`/`IsError`, `IsArray`/`IsDate`/`IsObject` und `IsNumeric` fuer die aktuell unterstuetzten Scalar-Variantwerte sowie VB6-Array-Subtype-Codes; der Date-Subtype typisierter `Date`-Werte bleibt erhalten, Objekt-, Array-Arithmetik- und Error-Varianten folgen mit den jeweiligen Typmodellen
- [x] Error-Variant-Grundlage: `CVErr` erzeugt einen typisierten Fehlerwert, `IsError` erkennt ihn,
      `VarType` liefert `vbError` und `TypeName` liefert `Error`; `Debug.Print` stellt Error-Werte als `Error <Nummer>` dar; explizite C*-Konversionen uebernehmen die Error-Nummer, implizite Zuweisungen und Parameter-Konversionen melden Type Mismatch (`Err.Number = 13`); Relationen vergleichen zwei Error-Varianten ueber ihre Error-Nummer, waehrend arithmetische, logische und String-Konkatenationsoperatoren Error-Operanden mit Type Mismatch (`Err.Number = 13`) ablehnen; Fehler-Propagation und `CVErr`-
      Integrationen in weitere Operator-/Objektmodelle bleiben offen
- [x] Missing-Variant-Vertrag: ausgelassene `Optional Variant`-Argumente bleiben fuer `IsMissing`
      erkennbar, `TypeName` liefert `Error`, explizite numerische Konversionen verwenden den
      Fehlerwert 448, und sonstige Variant-Verwendung meldet den dedizierten Runtime-Fehler 448
- [~] Array-Variant-Vertrag: `IsArray`, `VarType` und typisierte `TypeName`-Ergebnisse stehen;
      skalare Operatoren und Konversionen melden fuer Array-Operanden Type Mismatch, und
      Elementzugriff mit Lesen/Schreiben laeuft ueber den Variant-Array-Runtimevertrag; Variant()-
      Elemente koennen an Variant-ByRef-Parameter weitergereicht werden; auch ein kompletter
      Variant-Array-Wert kann an `ByRef value As Variant` uebergeben und im Callee ersetzt werden.
      Native `VT_VARIANT`-SAFEARRAYs bewahren dabei `Empty`, `Null`, `Nothing`, `Missing`,
      `Error`, `Date` und `Currency` ueber den Managed-Declare-/COM-ByRef-Roundtrip. Vollstaendige
      Objekt-/Array-Promotion sowie UDT-/Pointer-/nicht kompatible SAFEARRAY-Faelle bleiben offen
- [~] Vollständige Variant-Arithmetik mit VB6-Promotionsregeln und impliziter Konvertierung. Numerische `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, logische Operatoren, Vergleiche, `&` und die String/Variant-Sonderregeln von `+` sind für die aktuelle Scalar-Variantmenge implementiert; `CDec` sowie Decimal-aware `+`, `-`, `*`, `/`, `Mod`, `\`, `^`, logische Operatoren, unäres `-` und Vergleiche sind ergänzt. Empty-Operanden, Null-Vergleiche, Null-Arithmetik, Null-If-Verzweigungen, Null bei `&` inklusive `Null & Null` sowie Currency-/Single-Vergleichspromotionen sind regressionsgesichert. Offen bleiben weitere `Null`/`Missing`-Sonderfälle, Objekt- und Array-Varianten sowie die abschließende Prüfung aller VB6-Promotionstabellen.
- [~] Erstklassiges `Decimal` als additive Erweiterung. `CDec` liefert den Variant-Subtype 14, die zentralen skalaren Rechenpfade erhalten Decimal-Werte und die aktuelle Operator-/Konvertierungsmenge ist abgedeckt; offen bleiben die vollständige Promotionstabelle und noch nicht unterstützte Variant-Subtypen.

## Meilenstein 5 — Prozeduren und Klassen

- [x] `Optional`-Aufrufsemantik/Defaults **vorgezogen**: ausgelassene Argumente erhalten den deklarierten Default oder den Typdefault
- [x] Benannte Argumente werden per `name:=value` an die deklarierten Parameter gebunden; ihre
      Ausdrücke werden bei umgekehrter Schreibreihenfolge in deklarierter Parameterreihenfolge
      ausgewertet und sind durch einen Managed-E2E-Nachweis regressionsgesichert.
- [x] Doppelte benannte Argumente und Positionsargumente nach einem `name:=value`-Argument werden
      deterministisch mit `VB6S0069` abgewiesen; die Parameterbindung bleibt dabei unverändert.
- [x] `ParamArray` als letztes `Variant`-Array-Argument mit leerem Aufruf und gemischten Werten
- [x] `Static`-Local-Lebensdauer ueber compiler-generierten Modul-Storage inklusive String-/Array-Initialisierung
- [x] ByRef-Randfälle **vorgezogen**: Temporaries für Literale/Ausdrücke/Funktionsergebnisse,
      Klammern erzwingen ByVal, Typmismatch bleibt `VB6S0008`
- [~] `Is`-Objektreferenzidentität für Variant-/Hostobjekte und emittierte Klasseninstanzen steht; COM-RCW-Identität wird über `IUnknown` verglichen, die übrige COM-Interop bleibt offen
- [~] `Property Get`/`Let`/`Set`: typisierte Managed-Instanz-Dispatch-Emission sowie implizites `Item`-Default-Property-Get/Let und `VB_UserMemId`-benannte Default-Properties stehen; numerische Variant-Objektindizes fallen auf das Managed-Default-`Item` zurück und schreiben bei `ByRef` über einen einmal ausgewerteten Temporary zurück; vollständige benannte Default-Property- und COM-Dispatch-Regeln bleiben offen
- [~] Klassenmodule: `.cls`, Klassentypen, `New`, `Set`, `TypeOf`, Instanzspeicher sowie `Class_Initialize`/`Terminate` sind emittiert; `Implements` wird als CLR-Interface mit MethodImpl-/Property-Dispatch emittiert, COM-Dispatch und Forms bleiben offen
- [~] Standard-`Collection`: semantischer Vertrag sowie Managed-`New`/`Count`/`Item`/`Add`/`Remove`/`For Each` mit one-based, keyed lookup und Einfügereihenfolge stehen; ungültige Indizes/Keys sowie `Before`/`After` melden Fehler 5, doppelte Keys Fehler 457. Weitere VB6-Randfälle und COM-Collection-Dispatch bleiben offen
- [~] Late-bound `Variant`-/`Object`-Member: Property-Get/Let/Set und Methodenaufrufe auf erzeugten Managed-Klassen sowie CLR-Property-Fallback stehen; optionale Parameter, `ParamArray`, typisierte Property-/Indexer-Konversionen und ByRef-Writeback für Managed-/CLR-Ziele sind ergänzt; COM-Defaultzugriff über `DISPID_VALUE`, COM-RCW-Identität über `IUnknown` und TypeInfo-gesteuertes typisiertes COM-ByRef-Marshalling für unterstützte Automation-Typen sind ergänzt, vollständige COM-/IDispatch-Auflösung, UDT-/Pointer-/Event-ABI und Host-ABI bleiben offen
- [~] `Event`/`RaiseEvent`, `WithEvents`: einfacher Managed-Raise-/Sink-Vertrag mit Umverdrahtung bei Reassignment steht; TypeLib-Coclass-Source-Interfaces liefern importierte Event-Signaturen, der vollständige Host-/COM-Connection-Point-Lifecycle bleibt offen
- [x] `.cls` als Projektquelle lesen und analysieren (hebt die Item-Abdeckung von 27 auf 30)

`[~]` kennzeichnet einen begonnenen, teilweise ausgabefähigen Slice. Der Managed-Kern ist jetzt
ausgabefähig; als nächste Klassenstufe folgen COM-/ActiveX-Dispatch, vollständige Event-Sink-
Lebenszyklen sowie vollständige Default-Property-Regeln.

## Meilenstein 6 — IR und Fehlerbehandlung

Das Lowering ist aus dem Backend heraus: `VB6.IR` erzeugt Basic Blocks mit expliziten Sprüngen,
`VB6.Emit.Managed` emittiert daraus CIL. Damit ist die Voraussetzung erfüllt, an der
`On Error Resume Next` bisher hing — jede Anweisung einzeln abzusichern ist eine Frage der
Blockstruktur, nicht mehr des Textgenerators.

- [x] Lowered IR mit Basic Blocks und expliziten Sprüngen — `VB6.IR`, inspizierbar mit `vb6c --dump-ir`
- [x] Syntax, Bindung und Lowering für `GoTo`, Labels, `On Error GoTo`/`GoTo 0`, `Resume`, `Resume Next` und `Resume <Label>`
- [x] `GoTo` und Labels vollständig: gebunden, gelowert und E2E ausgeführt
- [x] Numerische und benannte Labels, `On ... GoTo`, `GoSub`/`Return` und `On ... GoSub` im Basic-Block-IR und Managed-Backend
- [x] `On Error GoTo`, `On Error Resume Next`, `On Error GoTo 0`, `Err`-Objekt und
      fehlerstellenspezifischer `Resume Next`-Dispatcher stehen im Managed-Backend. Numerische
      Labels aktualisieren `Erl`; ein Fehler aus einem aktiven Handler wird nicht rekursiv in
      denselben Handler geleitet, sondern an den Aufrufer weitergereicht. `Resume <Label>`
      leert den aktiven Handlerzustand vor dem Sprung und meldet ohne aktiven Fehler 20.
      Explizites `Exit Sub` aus einem aktiven Handler leert `Err`; die verschachtelte
      Aufruf-/Resume-Matrix ist dokumentationsbasiert abgesichert. Die native LLVM-ABI ist
      ausgeschlossen.
- [x] Quellpositionen: der Binder hängt `SourceLocation` referenziell an jede gebundene Anweisung,
      `IrLowerer` stempelt sie auf die entstehenden Instruktionen, der Emitter merkt sich die
      IL-Offsets und `PortablePdbEmitter` schreibt daraus Sequenzpunkte. Die PDB trägt damit
      Dokumente, Locals, Anweisungsgrenzen und procedure-wide Scopes mit Start/Length. Die
      verbleibende native Debug-ABI gehört zum ausgeschlossenen LLVM-Pfad.

## Meilenstein 7 — Standardbibliothek

Weiter nach Korpusbedarf priorisiert, im Umfang aber durch die vollständige VB6-SP6-Matrix bestimmt:

1. String-Funktionen — `Left`/`Right`/`Mid`/`Len`/`InStr`/`Replace`/`Trim`/`UCase`/`Chr`/`Asc`/`Val`/`Hex`/`String`.
    `Len`/`LenB`, dreiargumentiges `Mid` und ASCII-`Chr` existieren; `LenB`/`Asc`/`Chr` tragen
    im `VB6Sp6`-Profil die aktive ANSI-Codepage. `ProcedureSymbol.IntrinsicKind`
   trägt die backendunabhängige Identität, der Binder behandelt Intrinsics wie normale
   Prozeduren, und `IrRuntimeMethod` benennt die Runtime-Operation. Damit sind weitere
   Bibliotheksfunktionen reine Tabelleneinträge — das gilt auch für `DoEvents`, `Kill`, `Dir`,
   `MsgBox`, `Split`, `InStrRev`, `LSet` und `CopyMemory`, zusammen der größte Posten der
   Restfehler. Wirklich an spätere Meilensteine gebunden sind nur `frmMain` (25×, M9), `App`
   und `Err` (M6)
1b. Konvertierungen — `CByte`/`CInt`/`CLng`/`CCur`/`CDec`/`CDate`/`CVDate`/`CSng`/`CDbl`/`CBool`/`CStr` ✅
1c. `Left`/`Right`/`UCase`/`LCase`/`Trim`/`LTrim`/`RTrim`/`Asc`/`IsNumeric` ✅ — jeweils gegen
    VB6-Verhalten geschrieben, nicht gegen das .NET-Gegenstück: `Left`/`Right` schneiden ab statt
    zu scheitern, `Trim` entfernt nur Leerzeichen, Casing und Zahlerkennung sind invariant.
    `LeftB`, `RightB`, `MidB`, `InStrB`, `InStr`, `InStrRev`, `StrComp`, zweiargumentiges `Mid` und die kontextuelle `Mid(...) = ...`-
    beziehungsweise `Mid$(...) = ...`-Zuweisung sind über die Intrinsic-Tabelle und End-to-End-
    Tests verdrahtet.
1d. Host- und Kontrollintrinsics — `IIf`/`RGB`, `GetSetting`/`SaveSetting`/`DeleteSetting`/
    `GetAllSettings`, `SendKeys`,
    `PopupMenu`, `LoadPicture`, `PropertyChanged`, `TextWidth`/`TextHeight`, `Print` und
    `PaintPicture` — ✅ als headless-fähige Runtime-Verträge;
    native Printer-Treiber-, erweiterte Grafik- und sonstige UI-Adapter folgen in M8/M9.
1e. `LSet`/`RSet` — die kontextuelle `LSet target = source`- und `RSet target = source`-Syntax
    sowie Managed-Ausführung für feste String-Ziele, gleichartige UDT-Werte und unterschiedliche
    rohe UDT-Layouts mit skalaren, Boolean- und `LongPtr`-Feldern sind ✅. `RSet` füllt kurze
    Quellen links mit Leerzeichen auf und behält beim Kürzen die linken Zeichen; ein
    variabler String bleibt ein normaler Zuweisungsspeicher. Dynamische Strings, Arrays, Variants
    und weitere nicht sicher abbildbare ABI-Layouts bleiben diagnostisch gesperrt.
1f. Dateisystem-Pfad-Intrinsics — `FileCopy`, `MkDir`, `RmDir`, `ChDir`, `CurDir`, `GetAttr`,
    `SetAttr` und `FileDateTime` sind ✅ über Symboltabelle, IR, Managed-Emitter und Runtime
    verdrahtet und durch direkte Runtime- sowie generierte Programmtests abgesichert.
1g. `Name oldPath As newPath` — Datei- und Verzeichnisumbenennung ist ✅ als eigene Syntax und
    Managed-Runtime-Operation implementiert und generiert keine untypisierten Restaufrufe.
1h. `Dir`-Attribute — die Fortsetzungsabfrage berücksichtigt ✅ `vbDirectory`, `vbHidden`,
    `vbSystem` und `vbVolume` (ohne portable Volume-Labels) und liefert Dateien sowie
    Verzeichnisse passend zum angeforderten Filter.
2. Datei-I/O — `Open For Binary/Input/Output/Append`, `Get`, `Put`, `Print`, `Width`, `Input`, `Seek`, `LOF`,
   `FreeFile`, `Close` ✅ für die numerischen Binärformen, skalare UDT-Records sowie skalare und feste
   String-Arrayfelder mit `String * n` und grundlegende
   Textzeilen: Lexer, Syntax, Parser, Runtime, Bindung und Emission stehen, und E2E-Tests schreiben
   und lesen echte Dateien. Variable `String`-Transfers, `Line Input`, grundlegende Stringfelder und
   typisierte numerische, Boolean- und Currency-Ziele für `Input #` sowie skalare Random-Records mit
   `Len`-Klausel und Defaultlänge 128 sind ergänzt; dynamische UDT-Arraymember in Records tragen
   ihren Descriptor und werden elementweise übertragen, eigenständige Arrays unterstützter UDT-
   Elemente übertragen ihre Payload ohne äußeren Descriptor, variable Stringfelder tragen ihr
   2-Byte-Längenpräfix, und Date-Ziele werden bei `Input #` in OLE-Automation-Doubles konvertiert.
   `Width #` ist für fortgesetzte `Print #`-Zeilen mit 0–255 Zeichen (0 = unbegrenzt) ergänzt;
   `Input #` stellt die von `Write #` erzeugten skalaren Variant-Zustände wieder her und binäre
   `Get`/`Put`-Transfers tragen für skalare Variant-Felder das VB6-Typ-Tag samt Payload;
   eigenständige unterstützte skalare Arrays einschließlich variabler `String`-Elemente werden in
   Binary elementweise ohne äußeren Descriptor übertragen; dynamische Top-Level-Arrays führen in
   Random zusätzlich den dokumentierten Descriptor und
   schreiben die rekonstruierte Form beim `Get` in die Zielvariable zurück;
   `Print #`, `Write #`, `Input #` und `Line Input #` wählen jetzt profilbewusst UTF-8
   (`Deterministic`) beziehungsweise die aktive Windows-ANSI-Codepage (`VB6Sp6`);
   Variant-Arrays als Variant-Wert/Objekte sowie weitere zusammengesetzte Random-Record-Layouts
   bleiben offen.
3. `MsgBox`/`InputBox` als hostfähige Verträge ✅; `MsgBox` liefert deterministische Buttonwerte und
   `InputBox` im headless Runtime-Profil den Defaultwert
4. [x] Math — `Abs`, `Sgn`, `Fix`, `Int`, `Round`, `Sqr`, `Exp`, `Log`, `Sin`, `Cos`, `Tan`, `Atn`,
   `Rnd` und `Randomize` sind als Managed-Intrinsics umgesetzt. `Null`/`Empty`, Banker's Rounding,
   Definitionsbereichs-/Überlauffehler, die VB6-Zufallsfolge sowie der Untertyperhalt von `Int`,
   `Fix` und `Abs` einschließlich `Currency` und `Date` sind durch Runtime- und Managed-E2E-Tests
   abgesichert. Die Promotionstabelle einschließlich Null-, Error- und nicht auflösbarer
   Objektoperanden ist geschlossen; der getrennte Objekt-/Array-Dispatch-Vertrag bleibt in
   Etappe B offen;
   `Like`/`Option Compare` sind für den aktuellen String-/Variant-Subset implementiert.
5. [x] `Format$` — benannte numerische, Boolean-, Datums- und Zeitformate, ein- bis vierteilige
   numerische Masken, vollständige `@`/`&`/`<`/`>`/`!`-Stringmasken mit Literalen/Escapes sowie
   die dokumentierten Datums-/Zeit-Token einschließlich `c`, `ddddd`, `dddddd`, `ttttt`, `AMPM`,
   `w`/`ww`/`q` und `FirstDayOfWeek`-/`FirstWeekOfYear` sind umgesetzt. `VB6Sp6` verwendet aktive
   Währungs-, Datums-/Zeittrennzeichen, Muster und Namen; `Deterministic` bleibt invariant.
6. [x] Finanzfunktionen — `FV`, `PV`, `PMT`, `IPmt`, `PPmt`, `NPer`, `Rate`, `NPV`, `IRR`,
   `MIRR`, `SLN`, `SYD` und `DDB` sind vollständig als Double-basierte Managed-Intrinsics mit
   Nullzins-, End-/Anfangsperioden-, Zahlungsaufteilungs-, Perioden-/Zinsiterations-, Cashflow-,
   Abschreibungs- und Argumentfehler-Verträgen umgesetzt.

## Meilenstein 8 — Interop

Durch `Declare` (234) deutlich früher als ursprünglich geplant; ab Meilenstein 5 parallel
beginnbar, da weitgehend unabhängig vom Sprachkern.

- [~] `Declare` -> P/Invoke für skalare Signaturen und blittable UDT-Records mit `Lib`/`Alias` und
      echter Managed-Invocation; ANSI-String-Marshalling, variable `ByVal String`-Puffer mit
      `StringBuilder` und aufrufseitigem Write-back, native `ByRef`-UDT-Rückschreibung sowie
      Scalar-Pointer-Transfers für `As Any` stehen, `AddressOf` erzeugt Managed-Funktionsadressen
      für direkte Prozedurziele und blittable `ByRef`-Callback-Parameter; einfache native
      `VARIANT`-Slots sowie `Variant()`-SAFEARRAY-Callback-Parameter und -Rückgaben mit Bounds-
      und ByRef-Ersatz-Write-back sind ergänzt, ebenso `Object()`-/Control-Arrays als
      `SAFEARRAY(VT_DISPATCH)` in Managed-Callbacks, COM-Event-Delegaten und externen
      `Declare`-Aufrufen; verschachtelte
      Pointer-/String-Callback-ABI-Verträge sowie UDT-/Record-Arrays und rohe Pointer-/C-Array-
      Verträge bleiben offen; native-width `LongPtr()`-Arrays sind für explizite x86/x64-Ziele als
      `SAFEARRAY(VT_I4)` beziehungsweise `SAFEARRAY(VT_I8)` ergänzt. `AnyCPU` wird für diesen
      architekturabhängigen Array-Vertrag diagnostisch abgelehnt.
- [~] COM/ActiveX-Konsum: `Reference=`-/`Object=`-Einträge werden verlustfrei gespeichert und für
      GUID/Version/LCID/Pfad analysiert; explizite `.vbp`-Projektverweise werden relativ zum
      Verbraucherprojekt aufgelöst, und häufige qualifizierte ActiveX-Controltypen werden aus der
      Projektliste gebunden. Designer-Controls in Forms/UserControls behalten ihren qualifizierten
      Typ als Klassenfeld; `MSComctlLib.TreeView`/`Nodes`/`Node`, `ImageList`/`ListImages`/`ListImage`,
      `ImageCombo`/`ComboItems`/`ComboItem`, `RichTextLib.RichTextBox` und
      `MSComDlg.CommonDialog` haben einen typisierten Managed-Late-Binding-Vertrag einschließlich
      der VB6-Control-Hierarchie bei ByRef. Windows-`.tlb`/`.olb`-/TypeLib-Referenzen aus `.dll`/`.ocx`
      werden zusätzlich über `LoadTypeLibEx` als dynamische Klassen-, Methoden-, Property-, Enum-
      und Record-/UDT-Verträge importiert. Skalare TypeLib-Aliase, Recordfelder und referenzierte
      UDTs werden in Managed-Structs übernommen; nicht sicher abbildbare Pointer-/C-Array-
      Signaturen erhalten einen Object-Fallback.
      `CreateObject` und Managed-`IDispatch`-Dispatch stehen; Enum-Konstanten aus Windows-TypeLibraries
      werden importiert und COM-Defaultzugriffe verwenden bei echten COM-Objekten `DISPID_VALUE`;
      `FSOURCE`-Event-Signaturen aus TypeLib-Coclasses werden ebenfalls importiert; TypeInfo-gesteuertes
      typisiertes COM-ByRef-Marshalling für unterstützte Automation-Skalare, `DATE`, `CURRENCY` und
      kompatible SAFEARRAYs steht mit sicherem ByVal-Fallback; grundlegendes natives OCX-Hosting
      und TypeInfo-/Connection-Point-Event-Bridging stehen für den geprüften x86-Pfad; einfache
      `VARIANT`-/`VARIANT_BOOL`-/`BSTR`-Eventparameter werden über dedizierte Automation-Delegaten
      geführt; typisierte SAFEARRAY-Eventparameter werden über `System.Array`-Delegaten mit Bounds-
      und `VBArray<T>`-Konvertierung geführt; vollständiger Connection-Point-Event-ABI für UDTs,
      rohe Pointer und nicht unterstützte SAFEARRAY-Elemente bleibt offen und gehört zum
      VB6-SP6-Abschlussplan. Der native LLVM-Pfad bleibt davon getrennt und ausgeschlossen.
      Die fünf tatsächlich verwendeten Controltypen (siehe M9) behalten innerhalb dieser Fläche
      Priorität; fehlende Korpusevidenz ist aber kein Grund mehr, einen dokumentierten ABI-Vertrag
      dauerhaft offen zu lassen.
      Wenn eine historische `Reference=`-/`Object=`-Zeile nur den Dateinamen trägt, versucht der
      Managed-Importer zusätzlich die registrierten `HKCR\TypeLib`-/`HKCR\CLSID`-Pfade in der
      passenden Version, LCID und Prozessbitness aufzulösen.
      Der Managed/.NET-Konsum wird vor dem nativen LLVM-Backend vervollständigt
- [~] eigener COM-Server-/ClassFactory-/IUnknown-Vertrag für emittierte VB6-Klassen — `--com-host` versieht emittierte Klassen mit stabilen CLSIDs, `ProgID`, `ComVisible` und Automation-Metadaten und erzeugt für Bibliotheken einen nativen .NET-`comhost.dll`. `DllGetClassObject`/`IClassFactory`/`IDispatch`-Aktivierung ist regressionsgesichert; die CLI kann den erzeugten Host über `--register-com`/`--unregister-com` mit dem passenden x86/x64-`regsvr32` installieren oder entfernen. `--com-manifest` und das MSBuild SDK emittieren zusätzlich ein Side-by-Side-Manifest für registry-free Aktivierung. Typbibliotheks-Emission und der vollständige eigene Raw-`IUnknown`-/`IDispatch`-Vertrag bleiben offen
- [~] .NET-Backend als primären kompatiblen Zielpfad stabilisieren; Variant-/Object-/COM-Randfälle und
      vollständige Runtime-/Projektverträge bleiben offen
- [~] LLVM-natives Windows-Backend für x86 und x64 — **auf Eis gelegt, wird nicht weitergetrieben.**
      Primitive skalare IR-Emission für x86/x64 einschließlich globaler Slots, skalierter
      Currency-Literale, sicherer skalarer Konversionen, skalarer `Declare`-Verträge,
      pending-error-aware Arithmetic-/Conversion-Helper und native `On Error`-Boundaries mit
      gespeicherter Resume-Boundary-ID sind ergänzt; stringwertige Err-Felder und native
      ABI-/Runtime-Emission für komplexe VB6-Werte bleiben offen. Dieser Pfad blockiert den
      Managed/.NET-Abschluss nicht.
      **Vorsicht beim Auftauen:** Das Backend ist ausschließlich über Textvergleiche auf dem
      erzeugten LLVM-IR abgesichert — nichts wird assembliert, gelinkt oder ausgeführt. Die grünen
      Tests belegen die Textform, nicht die Lauffähigkeit. Erste Aufgabe beim Wiederaufnehmen ist
      ein echter nativer End-to-End-Test.
- [x] MSBuild SDK-Grundvertrag — `VB6Project`, `VB6CompilerPath`, `VB6TargetPlatform` und `CompileVB6Project`-Target; NuGet-Packaging und inkrementelle Input-/Output-Verfolgung sind mit `VB6.Compiler.Sdk.1.0.0.nupkg` verifiziert. Ohne Plattformargument folgt das SDK dem x86-Projektdefault der CLI; `x64` und `AnyCpu` werden validiert und als expliziter CLI-Schalter weitergereicht
- [~] MSBuild-SDK für VB6-Projektgruppen — `VB6ProjectGroup` verfolgt `.vbg`-, `.vbp`-, deklarierte
      Quell-, Designer-, Ressourcen- und Referenzinputs über ein CLI-generiertes SHA-256-Manifest,
      ruft die vorhandene CLI-Gruppenemission auf und verwendet einen eigenen inkrementellen
      Compile-Stempel; `ResolveVB6ProjectGroup`, `GetVB6ProjectGroupOutputs` und der headless
      `DesignTimeBuild`-Pfad sowie manifestbasierte `Clean`-/`Rebuild`-Orchestrierung sind ergänzt.
      Offen bleiben eine eigenständige gepackte ProjectSystem-Task und vollständige TypeLib-
      Orchestrierung. Visual-Studio-CPS und Projektmodell gehören zur ausgeschlossenen IDE-Schicht
- [x] `LongPtr`/`CLngPtr` — native-width `System.IntPtr`-Typverträge, checked Integer-/Bitwise-Operatoren,
      `For`-Zähler, Variant-Konvertierungen und `Declare`-P/Invoke-Signaturen
- [x] vorzeichenlose Ganzzahltypen — `UShort`/`UInt16`, `UInteger`/`UInt32` und `ULong`/`UInt64`
      sind mit `CUShort`, `CUInt` und `CULng` sowie checked Managed-/P/Invoke-/Variant-Verträgen ergänzt
- [~] `AddressOf` — direkte Prozedurziele werden als `LongPtr`-Funktionsadresse emittiert und für
      Legacy-`Long`-Callbackparameter konvertiert; blittable native Callback-Parameter und
      Delegate-Lebensdauer stehen; dynamische Callback-Delegaten markieren Win32-`BOOL`,
      ANSI-Strings, einfache `Variant`-Slots und `Variant()`-SAFEARRAYs mit nativer Konvertierung
      und ByRef-Rückschreibung; `LongPtr()`-SAFEARRAYs verwenden auf expliziten x86/x64-Zielen
      `VT_I4`/`VT_I8`; einfache `String()`-Callbacks verwenden `SAFEARRAY(VT_BSTR)` mit Bounds-
      und Ersatz-Write-back. Komplexe UDT-/verschachtelte rohe Pointer-/String-ABIs bleiben offen

## Meilenstein 9 — Forms

Größter Einzelblock. Die Reihenfolge folgt den gemessenen Korpusgewichten oben, nicht der
Vollständigkeit der VB6-Oberfläche: intrinsische Controls und ihr Eventmodell zuerst, ActiveX
danach, unbelegte Konstrukte zuletzt. Der verbindliche Abschlussumfang umfasst dennoch die
dokumentierte Forms-Oberfläche einschließlich `DrawMode` und MDI.

- [x] **`Paint`-Event und `AutoRedraw`-Semantik** — `Paint` ist für Designer-Controls
      (einschließlich Control-Array-Index), Forms und UserControls verdrahtet und wird wie in VB6
      nur bei abgeschaltetem `AutoRedraw` ausgelöst. `BeginDrawing` entscheidet pro
      Zeichenoperation über das Ziel: innerhalb eines `Paint`-Handlers dessen Zeichenkontext, bei
      `AutoRedraw` die persistente Fläche, sonst direkt die sichtbare Fläche. Das Abschalten von
      `AutoRedraw` verwirft die Bitmap. `Cls` leert die aktive beziehungsweise persistente
      Zeichenfläche über denselben Host-Vertrag.
- [~] `.frm`/`.frx` parsen; die Designer-Hülle wird mit verschachtelten Controls, Eigenschaften,
      `BeginProperty`-Blöcken und hexadezimalen `.frx`-Ressourcenoffsets erfasst. Intrinsische
      Designer-Controltypen (u. a. `CommandButton`, `TextBox`, `Frame`, `PictureBox`, `Image`,
      `Label`, `Shape`, `Line`, `Timer` und `Menu`) werden als typisierte Klassenfelder gebunden;
      skalare Designerwerte für Controls und das Root-Form (einschließlich Fensterrahmen,
      ControlBox, Min-/Max-Button, Taskbar, Startposition und WindowState) werden nach der
      Erzeugung über den Host gesetzt; `TextRTF`
      kann seine Nutzdaten aus `.frx` beziehen; `Picture`- und `Icon`-Payloads werden ebenfalls
      extrahiert. Vollständige Ressourcendekodierung sowie die vollständige Designer-Eigenschafts-
      und Control-Oberfläche bleiben offen.
- [~] Forms-Runtime auf WinForms: Der portable `IVB6Host`-Vertrag deckt Message-Pump, Form-Lifecycle,
      dynamischen Member-/Control-Dispatch, Control-Erzeugung und Enumeration ab; `VB6.Runtime.WinForms`
      mappt Standardcontrols, Twips, OLE-Farben und Fonts und regressionstestet `Load`/`Unload`/`Show`.
      Generierte Form-Konstruktoren registrieren die Designer-Controls automatisch. Die vollständige
      Eigenschaften-/Event-Oberfläche und OCX-Komposition bleiben offen; der geprüfte native
      OCX-Pfad ist separat regressiongesichert.
- [~] **Control-Arrays** — Designer-`Index`-Eigenschaften und wiederholte Controlnamen werden
      als typisierte VB6-Arrays gebunden und im generierten Form-Konstruktor als Host-Controls
      initialisiert. `Load name(index)` und `Unload name(index)` laufen zur Laufzeit: Der Binder
      behält das Array als zuweisbaren Platz, statt das noch nicht existierende Element
      auszuwerten, die Runtime wächst es bis zum Index und wählt das unterste vorhandene Element
      als Vorlage, der Host klont es unsichtbar in denselben Container und verdrahtet die Events
      mit dem neuen Index. Fehler 360 und 9 entsprechen VB6. Offen bleiben `Load` auf Formularen
      innerhalb eines Arrays sowie Menü-Control-Arrays.
- [~] Zeichnen auf Form/PictureBox — persistentes `GraphicsLine`-Rendering auf der aktiven
      Formoberfläche mit Twips-/Pixel-Skalierung und Linien-/Rechteckfüllung steht; ein unterstütztes
      `PaintPicture`-Subset zeichnet `Bitmap`-/FRX-/`VBPicture`-Quellen persistent mit; qualifizierte
      `PictureBox.PaintPicture`- und `PictureBox.Line`-Aufrufe lösen ihr eigenes Ziel auf.
      `ScaleMode` ist vollständig: Twip, Point, Pixel, Character, Inch, Millimeter und Zentimeter
      rechnen exakt und pro Achse — Character ist mit 120 zu 240 Twips die einzige asymmetrische
      Einheit. `User` (0) bleibt Twips, bis ein eigener Maßstab über `ScaleWidth`/`ScaleHeight`
      existiert; ein Wert außerhalb 0–7 meldet wie in VB6 Fehler 380. `AutoRedraw` gehört zum
      `Paint`-Punkt oben.
- [~] **`DrawMode` — im Abschlussplan, nach den belegten Forms-Verträgen.** Alle 16 VB6-/GDI-
      ROP2-Wahrheitstabellen sind für persistente Managed-`AutoRedraw`-Flächen umgesetzt und mit
      Pixeltests gesichert; `GraphicsLine` und `PaintPicture` führen die Quell-/Zielmerges über
      dieselbe Rasteroperation aus. Offen bleiben direkte sichtbare Zeichenkontexte, der
      `Paint`-Kontext, GDI/DIB-Clipping und die native DC-Integration.
- [~] **MDI — Grundvertrag vorhanden, vollständiger Ausbau eingeplant.** `VB.MDIForm`-Wurzeln werden
      als MDI-Container initialisiert; `MDIChild=True` ordnet Child-Forms im WinForms-Host dem
      registrierten Container zu und bleibt über den Host-Dispatch lesbar. Weder `MDIForm` noch
      `MDIChild` kommt in den 40 VISIA-Quellen vor; offen sind dennoch vollständige Fensterbefehle,
      `ActiveForm`, Cascade/Tile/Arrange, WindowList-/MDI-Menüs, Fokus und persistente
      Window-Management-Regeln.
- [~] `UserControl` (ActiveX) — generierte parameterlose `.ctl`-Klassen werden aus der Projektassembly
      instanziiert und als eingebettete borderlose WinForms-Hostflächen in Designer-Controls
      aufgenommen; `UserControl_Initialize`/`UserControl_Terminate` sowie die konventionellen
      `UserControl_*`-UI-Handler werden an die eingebettete Hostfläche gebunden; ein pro Instanz
      gehaltener `VBPropertyBag` wird an `UserControl_ReadProperties`/`UserControl_WriteProperties`
      gereicht; Connection-Point-ABI und
      echte OCX-Komposition bleiben offen
- [~] OCX-Hosting für `MSComctlLib`, `RichTextLib`, `MSComDlg` — der opt-in-`WinFormsHost` aktiviert
      registrierte 32-Bit-Visual-OCX über `AxHost`, bindet den nativen `IDispatch`-Pfad für Properties
      und Collections und behandelt `CommonDialog` als native nonvisual COM-Komponente. Die x86-
      Regression kann mit `VB6_REQUIRE_NATIVE_OCX=1` fehlende Registrierungen hart melden. Native
      Connection-Point-Events werden für den RichTextBox-`Change`-Vertrag über `IProvideClassInfo`
      beziehungsweise die registrierte TypeLib aufgelöst; vollständige Event-Signaturen, alle
      Bitness-/Designer-Sonderfälle und der vollständige native ABI-Vertrag bleiben offen.
      **Erste Priorität sind die fünf im Korpus tatsächlich verwendeten Typen**: `MSComDlg.CommonDialog`
      (4 Instanzen), `MSComctlLib.ImageList` (3), `RichTextLib.RichTextBox` (2),
      `MSComctlLib.TreeView` (2), `MSComctlLib.ImageCombo` (2) — alle fünf haben bereits einen
      managed Late-Binding-Vertrag. Die im Korpus belegten Event-Signaturen stehen: `NodeClick`
      (TreeView, mit typisiertem `Node`), `SelChange` (RichTextBox) und `Dropdown` (ImageCombo),
      dazu der intrinsische Satz. Der Abschlussumfang endet dort nicht: Alle Microsoft-
      redistributablen VB6-Stock-Controls werden in der Kompatibilitätsmatrix geführt; installierte
      Controls laufen nativ, fehlende erhalten ABI-Testkomponenten und einen sichtbaren
      Verifikationsstatus.

      **Ein VB6-Event auf einem ActiveX-Control kann aus zwei Quellen kommen**, und der Host muss
      beide bedienen, sonst liefern nativer und managed Pfad nicht dieselbe Signatur:

      1. *Aus dem Control.* Der COM-Connection-Point trägt die Events des OCX — `Change`,
         `DblClick`, `NodeClick`, `SelChange`, `Dropdown`. Dafür muss der **VB6-Name** übergeben
         werden; die Übersetzung auf die WinForms-Entsprechung liegt allein in `FindEvent` und
         gilt nur dem managed Adapter. Ein WinForms-Name sagt einem OCX nichts.
      2. *Aus dem Container.* Fokus-Events sind in VB6 **Extender-Events** und fehlen im
         Event-Interface des OCX. Schlägt die COM-Subscription fehl, greift der Host deshalb auf
         das hostende `AxHost`-Wrapper-Event zurück. `AxHost` lehnt dabei geerbte Events ab, die
         das Control nicht implementiert — diese Absage ist eine Antwort, kein Fehler.

      Weil ein nativer OCX keiner der managed Adapterklassen entspricht, werden ihm die
      OCX-eigenen Eventnamen unabhängig vom CLR-Typ angeboten; das Control entscheidet selbst,
      welche es annimmt.

      Nativ nachgemessen (x86, registrierte OCX, jeweils mit Gegenprobe): `Change` und `DblClick`
      an RichTextBox, `NodeClick` an TreeView, `GotFocus`/`LostFocus` an beiden. Nur managed
      geprüft sind `SelChange` und `Dropdown`. Der native Pfad ist an registrierte 32-Bit-OCX
      gebunden und wird über `VB6_REQUIRE_NATIVE_OCX=1` erzwungen — im 32-Bit-Testhost, sonst
      überspringen die Fälle. Ohne Registrierung, etwa auf einem CI-Runner, muss der managed Pfad
      grün bleiben. Offen bleiben die nicht belegten Event-Signaturen der übrigen OCX-Oberfläche.

## Meilenstein 10 — LSP und IDE (ausgeschlossen)

**Auf Eis gelegt, wird nicht weitergetrieben** — bewusst nach dem Compiler-Kern eingeordnet.

Der erste LSP-Slice für Visual Studio steht: JSON-RPC, Initialize, Dokument-Synchronisation,
Lexer-/Parser-/Semantik-Diagnosen, dokumentlokale Completion aus Deklarationen und Intrinsics,
Go-to-definition sowie Dokumentsymbole. Offen bleiben projekt- und workspaceweite Symbolsuche,
kontextabhängige Completion und Buildintegration. Danach folgen eigenständige IDE-/WinForms-
Designer-Funktionen mit verlustfreiem `.frm`-Roundtrip und Debugger. Diese Schicht ist bewusst nach
dem Compiler-Kern eingeordnet.

---

## Zusätzlich, klein und unabhängig

1. [x] `Debug.Print` auf VB6-nahe Formatierung (führendes Vorzeichen-Leerzeichen, 15
   signifikante Stellen für Gleitkomma-/Currencywerte und vollständige Decimal-Präzision);
   die E2E-Helfer trimmen weiterhin bewusst Plattform-/Spaltenformat
2. [x] Typisierte Vergleiche direkt emittieren statt `VBOperators.Equal(object?, object?)` — der
   Binder hat beide Seiten bereits angeglichen; der Managed-Emitter ruft für skalare gemeinsame
   Typen typisierte Vergleichshelfer auf und lässt Variant-/Objektpfade unverändert
3. [x] `Currency + Double` folgt nun der VB6-Promotionsreihenfolge und liefert `Double`, während
   `Currency * Double` die separate Multiplikationsreihenfolge beibehält und `Currency` liefert;
   Vergleichspromotionen behalten weiterhin die separate Currency-Präzisionsregel
4. [~] `Debug.Print` formatiert Zahlen im deterministischen Profil invariant und mit VB6-nahem
   Vorzeichen-/Signifikanzformat; `Format` folgt im `VB6Sp6`-Profil bereits dem
   System-LCID-/ANSI-Vertrag aus Etappe C. Die vollständige profilbewusste Debug-/Financial-
   Formatierung bleibt offen.
5. [x] `Debug.Assert` wird als kompiliertes VB6-Statement akzeptiert und im Managed-Emit
   vollständig elidiert.
