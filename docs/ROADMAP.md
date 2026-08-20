# Roadmap

Weg von "VB6-Teilmenge kompiliert" zu "beliebiges Legacy-`.vbp` kompiliert unverändert", plus
moderne Typerweiterungen, danach die IDE.

Die Reihenfolge stammt aus einer Konstrukt-Frequenzanalyse über echten VB6-Code, nicht aus einer
generischen VB6-Feature-Liste.

## Gemessener Ist-Stand

Erhoben mit `vb6c <projekt.vbp> --report` gegen VISIA 4.8.7.1 (10.152 Zeilen, 42 Quelldateien):

| Stand | Fehler gesamt | Parser | Lexer | Semantik | fehlerfreie Dateien |
|---|---|---|---|---|---|
| Nulllinie (M0) | 3361 | 3183 | 178 | 0 | 0 von 27 |
| nach M2-Grundlagen | **2464** | 2276 | 68 | 120 | 0 von 27 |
| nach `Declare`-Syntax | **2322** | 2116 | 68 | 138 | 0 von 27 |
| nach `Enum`-Syntax | **2100** | 1894 | 68 | 138 | 0 von 27 |
| nach `Optional`-Syntax | **2216** | 1800 | 68 | 348 | 0 von 27 |
| nach `Option Base` / `Option Compare` | **2210** | 1794 | 68 | 348 | 0 von 27 |
| nach Mehrfachdeklaratoren | **2223** | 1762 | 68 | 393 | 0 von 27 |
| M2 abgeschlossen (`Static`, `^`, `Like`, `Is`) | **2219** | 1758 | 68 | 393 | 0 von 27 |
| M3 Array-Syntax/Runtime-Basis | **2105** | 1644 | 68 | 393 | 0 von 27 |

`Declare` senkt die Gesamtzahl um 142 und die Parserfehler um 160. `Enum` bringt weitere 222
Parserfehler weg. `Optional` senkt die Parserfehler nochmals um 94. Die rohe Gesamtzahl steigt
dabei von 2100 auf 2216, weil 210 zusätzliche Semantikdiagnosen sichtbar werden: mehr echte
Prozeduren erreichen nun den Binder, statt an ihrer Parameterliste zu entgleisen. Das ist kein
Parser-Rückschritt, sondern genau der gewünschte Übergang von Syntaxkaskaden zu konkreten
semantischen Lücken. `Option Base` / `Option Compare` entfernen danach weitere 6 Parserfehler.
Mehrfachdeklaratoren senken die Parserfehler anschließend um weitere 32 auf 1762. Die Semantik
steigt dabei von 348 auf 393: unter anderem werden 4 echte implizite-Variant-Deklaratoren jetzt
präzise als `VB6S0020` sichtbar, statt den Typ eines späteren Deklarators zu übernehmen oder im
Parser zu entgleisen. `Static` entfernt weitere 4 Parserfehler und schließt M2 bei 1758
Parserfehlern ab. `^`, `Like` und expression-level `Is` ändern den aktuellen VISIA-Zähler nicht,
sind aber regressionsgesichert.

Der erste M3-Slice bewahrt feste, explizit begrenzte, mehrdimensionale und dynamische
Arraydeklarationen sowie Arrayparameter im Syntaxbaum. Damit sinken die Parserfehler nochmals um
114 auf 1644 und die Gesamtzahl auf 2105. Actions #700 bestätigt diesen Referenzstand mit
258 Tests, 0 Warnungen und 0 Buildfehlern. Der VISIA-Report läuft als eigener Schritt nach
Build und Tests.

Lokaler Folge-Stand: feste und dynamische Arraytypen werden gebunden, feste lokale und Modularrays
werden mit `VBArray<T>` initialisiert, `Option Base` wird auf implizite Untergrenzen angewendet,
Arrayelemente werden gelesen/geschrieben und emittiert, `ReDim`, `ReDim Preserve`, `Erase`,
`LBound`, `UBound` und Array-`For Each` sind von Parser bis CLI-Smoke verdrahtet. Der erste
UDT-Slice ist ebenfalls verdrahtet: `Type ... End Type`, skalare Felder, feste Arrayfelder,
`String * n`-Feldnormalisierung, sequentielle Layout-Metadaten, `ByValTStr`-Metadaten für
konstante feste Stringfelder, UDT-Variablen, verschachtelte Memberzugriffe, UDT-Wertkopien,
UDT-`ByVal` und UDT-`With`-Blöcke. Vollständige Native-/COM-Interop und tiefere
Objekt-/Klassenintegration bleiben offen. Der lokale M4-Stand ergaenzt `VBVariant`-Storage,
Variant-Literale/-Builtins, untypisierte Deklaratoren/Parameter, einen ersten Operatorpfad
fuer arithmetische Operatoren, `&`, Vergleiche, logische/bitweise Operatoren sowie unary `-`/`Not`
ueber gewrappte Primitive, Null-Tri-State fuer Variant-Vergleiche und Boolean-Logik, `CVErr`/
`IsError` als ersten Error-Variant-Slice und `Decimal` als additive Typerweiterung inklusive
`CDec`, Runtime-Operatoren und Variant-Promotion.
Vollstaendige VB6-Promotion, Objektvarianten, tiefere Error-Variant-Kanten und die restliche
Null-Tri-State-Semantik bleiben offen.
Der neue VISIA-Report ist noch nicht neu erhoben.

`.bas` und `.cls` werden heute gelesen; `.ctl` (4) und `.frm` (6) sind noch außen vor —
daher 27 von 40 Items.

Dass zunehmend *semantische* Fehler auftauchen, ist der eigentliche Fortschritt: Dateien kommen
bis zum Binder durch, statt schon im Parser zu entgleisen.

Deshalb bleibt die Zahl fehlerfreier Dateien vorerst bei 0: gebunden wird projektweit, also
kann eine Datei erst sauber sein, wenn auch ihre Abhängigkeiten parsen. Der Sprung kommt
schlagartig, nicht schrittweise.

### Was die Messung an der Planung geändert hat

Die Top-Blocker sind kleinteiliger und billiger als erwartet. Alle 27 Module scheiterten anfangs
an derselben Stelle: **Zeile 1 jeder `.bas`-Datei ist `Attribute VB_Name = "..."`**. Diese
frühen Parserbarrieren werden deshalb zuerst entfernt, auch wenn die vollständige Semantik eines
Konstrukts erst in einem späteren Meilenstein folgt.

Nach `Enum` zeigte die Messung zudem, dass ein großer Teil der verbliebenen `AsKeyword`-Kaskaden
nicht von Mehrfach-`Dim`, sondern von `Optional ... As ...` in realen Prozedurköpfen stammt.
Deshalb wurde die `Optional`-**Syntax** nach M2 vorgezogen. Lokaler M5-Stand: ausgelassene
`ByVal`-Optionals nutzen Default-Ausdruecke oder `Missing`; `ParamArray` packt Restargumente in
zero-based `VBArray<T>`-Instanzen; ausgelassene `ByRef`-Optionals bleiben ein ByRef-Randfall.

`Option Base` und `Option Compare` haben außerdem bestätigt, dass VB6-Kontextwörter nicht
vorschnell global reserviert werden dürfen: `Base` wird im bestehenden Akzeptanzkorpus legal als
Bezeichner verwendet. Beide Direktiven werden deshalb nur direkt hinter `Option` erkannt; die
Wörter bleiben sonst normale Identifier.

Der `:`-Anweisungstrenner war im Parser bereits über die gemeinsame Zeilenabschlusslogik
implementiert. Actions #588 verifiziert ihn ausdrücklich mit Parser- und End-to-End-Tests für
mehrere Statements pro Zeile, Single-Line-`If` und `Case`. Labels wie `LinkFail:` gehören
weiterhin zum späteren Sprung-/IR-Meilenstein und sind von diesem Statement-Separator-Support
getrennt.

Bei Mehrfachdeklarationen gilt die echte VB6-Regel **pro Deklarator**: `Dim a, b As Integer`
macht nur `b` zu Integer; `a` bleibt Variant. Der Syntaxbaum speichert deshalb `As Type` an jedem
Deklarator einzeln. Explizit typisierte Listen und untypisierte Variant-Deklaratoren werden
gebunden und emittiert; untypisierte Namen erben also nicht stillschweigend den Typ des Nachbarn.
Actions #604 verifiziert den historischen Parser-/Binder-Stand; der lokale M4-Stand ersetzt die
frühere `VB6S0020`-Diagnose durch `VBVariant`.

`Static` verwendet dieselbe Deklaratorstruktur, aber einen eigenen Syntaxknoten. Der Binder macht
die Namen fuer Folgeausdruecke sichtbar und markiert sie als statische Locals; der C#-Generator
emittiert prozedurbezogene static Fields, sodass Werte zwischen Aufrufen erhalten bleiben.
`Like` und expression-level `Is` werden analog syntaktisch bewahrt, aber mit `VB6S0023` bzw.
`VB6S0024` gestoppt, bis Pattern-/`Option Compare`- bzw. Objektidentitätssemantik existiert.
`^` ist dagegen bereits vollständig von Lexer bis End-to-End-Ausführung implementiert. Actions
#662 validiert den abgeschlossenen M2-Stand mit 243 Tests.

Der nächste konkrete VISIA-Blocker in `envSort.bas` ist nun ein aufrufseitiges `ByVal`, etwa
`CopyMemory SwpVal, ByVal VarPtr(String1), 4`. Das gehört zu den späteren ByRef-Randfällen; M3
bleibt trotzdem bei Arrays/UDTs, weil dieselbe Datei Arrayparameter und feste lokale Arrays enthält
und Arrays/UDTs der geplante Strukturblock sind.

Danach, nach betroffenen Dateien sortiert:

| Blocker | Belege |
|---|---|
| `Attribute`-Kopfzeile | 27 von 27 Dateien |
| Deklarationen auf Modulebene (`Public x As Long`) | 22 Dateien |
| `Sub`/`Function` mit `Public`/`Private`-Modifizierer | 20 Dateien |
| `With`-Blöcke (`.Feld`-Zugriff) | 19 Dateien, 629 Vorkommen |
| Bezeichner-Typsuffixe | `Mid$` 110×, `ret&` 26×, `lphKey&` 10× |
| `:` als Anweisungstrenner | `AppType = 0: pError = False` ✅ |
| Datei-I/O mit Dateinummern | `Open ... For Binary As #1`, `Put #1`, `Close #1` |

Konsequenz: Diese Punkte sind einzeln klein, betreffen aber viele Dateien und blockieren dadurch
die Messung von allem Übrigen. Sie stehen deshalb vorn.

## Korpus-Frequenzen

Häufig in VISIA — es ist ein Systemprogramm (Assembler, Linker, PE-Erzeugung), kein
Business-Programm:

| | | | |
|---|---|---|---|
| `&H`/`&O`-Literale | 892 ✅ | `Event`/`RaiseEvent` | 97 |
| String-Funktionen | 337 | `Optional`/`ParamArray` | 77 (`Optional` + `ParamArray` basic ✅) |
| `Declare` (Win32) | 234 | Datei-I/O (`For Binary`) | 76 |
| `Property Get/Let/Set` | 209 | `On Error GoTo` / `Resume Next` | 34 / 31 |
| `ReDim`/`Preserve` | 103 | `Type ... End Type` | 52 |
| `With` | 102 | `Enum` | 44 ✅ Syntax |

Kommt **nicht** vor: `Format$` 0, `Date` 0, ADO 0, `#If` 0, `Resume`-Statement 0. Da `Resume`
fehlt, genügt `On Error GoTo` + `On Error Resume Next` + `Err` — kein voller
Resume-Zustandsautomat.

## Entschiedene Weichenstellungen

- **Variant früh**, bewusst gegen die VISIA-Evidenz (dort nur 20 Treffer): der Umbau wird später
  teurer, und die Business-Legacy-Projekte brauchen ihn sehr wohl.
- **x86 als Default-Ausgabe, x64 opt-in.** Bestätigt durch den Korpus: VISIA hängt an 32-Bit-OCX
  (`MSComDlg.CommonDialog`, `MSComctlLib`, `RichTextLib`), die ein 64-Bit-Prozess nicht
  in-process laden kann. „64 Bit" gilt für Sprache und Typen, nicht zwingend für den Prozess.
  Muss vor Meilenstein 8 endgültig entschieden sein, weil Marshalling-Code davon abhängt.
- **VISIA ist Testkorpus, nicht Portierungsziel.** Die IDE entsteht später eigenständig in C#.
  Es liegt versioniert unter `conformance/VISIA/` und wird von `ConformanceCorpusTests` in CI
  mitgemessen. Herkunft und Zweck: `conformance/README.md`.

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
- [x] `Public`/`Private`/`Friend`-Modifizierer an `Sub` und `Function`
- [x] Bezeichner-Typsuffixe `$ % & ! # @`
- [x] Zeilenfortsetzung mit `_`
- [x] `Const`, typisiert und aus dem Wert abgeleitet
- [x] `Exit Sub` und `Exit Function`
- [x] `Declare`-Syntax mit `Lib`, optionalem `Alias` und `As Any`; Binding/PInvoke bleibt M8
- [x] `Enum ... End Enum` mit optionaler Sichtbarkeit, expliziten/impliziten Memberwerten, `EnumTypeSymbol` und Member-Konstanten
- [x] `Optional`-Parametersyntax mit `ByVal`/`ByRef` und optionalem Default-Ausdruck; ausgelassene `ByVal`-Argumente/Defaults sind M5, ausgelassene `ByRef`-Optionals bleiben ByRef-Randfall
- [x] `Option Base 0/1`, `Option Compare Text/Binary`; Auswertung bleibt bei Arrays bzw. Stringvergleichen
- [x] `:` als Anweisungstrenner für den aktuellen Statement-Subset, inklusive Single-Line-`If` und `Case`; Labels bleiben M6
- [x] Mehrfachdeklaratoren wie `Dim a As Integer, b As Long`; `As Type` gilt pro Deklarator, implizites Variant bleibt M4
- [x] `Static`-Local-Syntax; statische Lebensdauer ist im M5-Slice umgesetzt
- [x] `^` vollständig; `Like`- und `Is`-Syntax mit Semantik-Guards bis M7 bzw. M5

`With`-Blöcke und `.Feld`-Zugriff (19 Dateien, 629 Vorkommen) haben jetzt eine erste UDT-Basis.
Objekt-/Klassenmember brauchen weiterhin das spätere Klassenmodell.

## Meilenstein 3 — Arrays und UDTs

Zusammen, weil Win32-Strukturen beides brauchen.

- [x] Array-Deklarationssyntax: `Dim x(10)`, `Dim x(1 To 10)`, mehrdimensional und dynamisch `Dim x()`; Grenzen werden verlustfrei im Syntaxbaum bewahrt
- [x] Arrayparameter-Syntax wie `TheArray() As String`
- [x] `ArrayTypeSymbol` als Typfundament und `VBArray<T>` in der Runtime mit Rang, expliziten Unter-/Obergrenzen, Indexprüfung sowie `LBound`/`UBound`-Grundlage
- [x] Arrayvariablen/-parameter echt binden; feste Arrays initialisieren; Arrayelemente lesen/schreiben und emittieren; `Option Base` semantisch auf implizite Untergrenzen anwenden
- [x] `ReDim`, `ReDim Preserve`, `Erase`, `LBound`/`UBound` als Sprach-/Bibliothekszugriff; Array-`For Each`
- [x] `Type ... End Type` mit skalaren Feldern, festen Arrayfeldern, `String * n`-Truncation/Padding, `UserDefinedTypeSymbol`, Memberzugriff und UDT-`With`
- [x] UDT-Wertkopien, UDT-`ByVal`-Kopien und verschachtelte UDT-Memberzuweisungen
- [x] UDT-Layout-Metadaten: `StructLayout(LayoutKind.Sequential)` und `MarshalAs(ByValTStr)` für konstante `String * n`-Felder
- [ ] Vollständige UDT-Native-/COM-Interop und Objekt-/Klassenmember

## Meilenstein 4 — Variant

- [x] `VBVariant`: `Empty`, `Null`, `Nothing`, `Missing`, `VarType`, `IsEmpty`/`IsNull`/`IsMissing`/`IsNumeric`
- [x] Untypisierte `Dim`-/Modul-/`Static`-Deklaratoren und untypisierte Parameter werden `VBVariant`; Defaultwert `Empty`, Primitive werden gewrappt
- [x] Erste Variant-Operatoren fuer unary `-`/`Not`, `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, `&` und Vergleiche ueber gewrappte Primitive
- [x] Null-Tri-State fuer Variant-Vergleiche; `CBool(Null)` scheitert statt still `False` zu liefern
- [x] Variant-`And`/`Or`/`Xor`/`Eqv`/`Imp`: numerische Bitlogik und Boolean-Null-Tri-State
- [x] Erster Error-Variant-Slice: `CVErr`, `IsError`, `VarType` 10 und gesperrte primitive Konvertierung
- [x] Erstklassiges `Decimal` als additive Erweiterung mit `As Decimal`, `CDec`, Runtime-Arithmetik, Codegen und Variant-`VarType` 14
- [ ] Vollstaendige Variant-Arithmetik mit VB6-Promotionsregeln, Objektvarianten, tieferen Error-Variant-Kanten und restlicher Null-Tri-State-Semantik

## Meilenstein 5 — Prozeduren und Klassen

- [x] Ausgelassene optionale `ByVal`-Argumente: Default-Ausdruck oder `Missing` bei untypisiertem Variant
- [x] `ParamArray`: variable Restargumente werden als zero-based `VBArray<T>` gebunden und emittiert, leere Aufrufe inklusive
- [x] `Static`-Local-Lebensdauer: statische Locals werden pro Prozedur als persistente static Fields emittiert
- [x] Parenthesized ByRef-Argumente in Statement-Calls: temporäre Werte ohne Copyback
- [x] Call-site `ByVal` bei ByRef-Statement-Calls: temporäre Werte ohne Copyback
- [x] Ausgelassene optionale `ByRef`-Argumente in Statement-Calls: Default/Missing als temporäre Werte ohne Copyback
- [x] Function-Expression-Temporaries: ByRef-Temps werden in inline `System.Func<T>`-Ausdruecken gekapselt
- [x] Skalare ByRef-Konvertierung mit Copyback: typabweichende skalare Variablen laufen über konvertierte Temps und schreiben nach dem Call zurück
- [x] Exakte ByRef-Aliase für UDT-Felder, Arrayelemente und UDT-Arrayfelder
- [ ] Weitere ByRef-Randfaelle: Objektmodell-Aliasing und nichtskalare Copyback-Regeln
- [ ] `Is`-Objektreferenzidentität auf dem echten Klassen-/Objekttypmodell
- [x] Projektweite Declared-Type-Auflösung für UDT-/Enum-Namen aus analysierten Modulen und Klassen; `OLE_COLOR` als VB/OLE-Alias auf `Long`
- [x] Klassenmodule als projektweite Typnamen (`ClassTypeSymbol`) binden; neutrale `object?`-Emission bis zur echten Klassen-Runtime
- [x] `.cls`-Designer-Vorspann (`VERSION`/`BEGIN`/`END`) und `Property Get`/`Let`/`Set`-Deklarationen parser-/binderseitig lesen
- [ ] Property-Aufrufe und Property-Dispatch auf dem echten Klassen-/Objekttypmodell
- [ ] Klassenmodule: `New`, `Set`, `Class_Initialize`/`Terminate`, `Implements`
- [x] `Event`-Deklarationen und `RaiseEvent`-Statements parser-/binderseitig lesen
- [ ] Event-Dispatch und `WithEvents`
- [x] `.cls` als Projektquelle lesen (hebt die Item-Abdeckung von 27 auf 30)

## Meilenstein 6 — IR und Fehlerbehandlung

Hier muss das Lowering aus dem Generator heraus. Heute erzeugt `CSharpGenerator` Sprungmarken
direkt beim Emittieren; das trägt nicht mehr, sobald `On Error Resume Next` jede Anweisung
einzeln absichern muss.

- [ ] Lowered IR mit Basic Blocks und expliziten Sprüngen
- [ ] `GoTo`, Labels, Zeilennummern, `On ... GoTo`, `GoSub`/`Return`
- [ ] `On Error GoTo`, `On Error Resume Next`, `On Error GoTo 0`, `Err`-Objekt

## Meilenstein 7 — Standardbibliothek

Nach Korpusbedarf priorisiert:

1. String-Funktionen — `Left`/`Right`/`Mid`/`Len`/`InStr`/`Replace`/`Trim`/`UCase`/`Chr`/`Asc`
2. Datei-I/O — `Open For Binary`/`For Output`, `Get`, `Put`, `Seek`, `LOF`, `FreeFile`, `Close`
3. `MsgBox`/`InputBox`
4. Math, Konvertierung, vollständiges `Like` inklusive `Option Compare`
5. Erst danach `Format$`, Datum/Zeit, Finanzfunktionen — im Korpus unbenutzt

## Meilenstein 8 — Interop

Durch `Declare` (234) deutlich früher als ursprünglich geplant; ab Meilenstein 5 parallel
beginnbar, da weitgehend unabhängig vom Sprachkern.

- [ ] `Declare` → P/Invoke mit `Alias`, `As Any`, ANSI-String-Marshalling
- [ ] COM-Konsum: Typbibliotheken aus `Reference=`/`Object=`, `CreateObject`, `IDispatch`
- [ ] x86-Standardausgabe umgesetzt, nativer Apphost statt DLL + runtimeconfig
- [ ] `LongPtr`, vorzeichenlose Ganzzahltypen

## Meilenstein 9 — Forms

Größter Einzelblock.

- [ ] `.frm`/`.frx` parsen; intrinsische Controls (Menu, Label, Shape, PictureBox, Image, Line,
      CommandButton, TextBox, Frame, Timer)
- [ ] Forms-Runtime auf WinForms: Twips, Property-/Event-Mapping, `Load`/`Unload`/`Show`
- [ ] **Control-Arrays** — kein WinForms-Konzept, eigene Nachbildung
- [ ] Zeichnen auf Form/PictureBox, MDI
- [ ] `UserControl` (ActiveX) — VISIA bringt vier eigene mit
- [ ] OCX-Hosting für `MSComctlLib`, `RichTextLib`, `MSComDlg`

## Meilenstein 10 — IDE

Eigenständig in C#/WinForms, sobald der Compiler trägt: Editor mit VB6-Syntax, Projektbaum,
Inline-Diagnostics, WinForms-Designer mit verlustfreiem `.frm`-Roundtrip, Debugger.

---

## Zusätzlich, klein und unabhängig

1. `Debug.Print` auf VB6-Formatierung (führendes Vorzeichen-Leerzeichen, 15 signifikante
   Stellen); danach `.Trim()` aus den E2E-Tests entfernen
2. Typisierte Vergleiche direkt emittieren statt `VBOperators.Equal(object?, object?)` — der
   Binder hat beide Seiten bereits angeglichen
3. `Currency + Double` liefert heute `Currency`; gegen echtes VB6 verifizieren
