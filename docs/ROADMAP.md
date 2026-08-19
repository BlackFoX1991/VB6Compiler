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
| M3 Array-Bindung/Elementzugriff | **2032** | 1571 | 68 | 393 | 0 von 27 |
| M3 `ReDim` / `ReDim Preserve` | **2299** | 1474 | 68 | 757 | 0 von 27 |
| M3 `Erase` / `LBound` / `UBound` | **2294** | 1474 | 68 | 752 | 0 von 27 |
| M3 `Type ... End Type`-Syntax | **2034** | 1214 | 68 | 752 | 0 von 27 |

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
Arraydeklarationen sowie Arrayparameter im Syntaxbaum. Damit sanken die Parserfehler um 114 auf
1644 und die Gesamtzahl auf 2105. `ArrayTypeSymbol` und die bounds-erhaltende `VBArray<T>`-Runtime
bildeten dafür das Fundament.

Die anschließende echte Array-Bindung, feste Initialisierung, `Option Base`, Elementzugriffe und
ByRef-fähige Arrayelemente senkten die Parserdiagnostik weiter auf 1571 und die Gesamtzahl auf
2032. Dynamische Arrays und `values()`-Arrayparameter tragen jetzt bewusst einen unbekannten Rang;
bei festen Deklarationen bleibt der Rang bekannt und wird statisch geprüft. Ganze Arrayparameter
sind wie in VB6 ByRef, während einzelne Elemente dank des `ref`-Indexers auch als echte ByRef-
Argumente weitergereicht werden können.

`ReDim` und `ReDim Preserve` sind nun für explizit typisierte dynamische Arrays von Lexer bis
End-to-End-Ausführung verdrahtet. `Preserve` bewahrt Werte beim Ändern der Obergrenze der letzten
Dimension und lehnt Rang-, frühere Dimensions- und Untergrenzenänderungen ab. Dadurch fallen die
Parserfehler nochmals von 1571 auf 1474. Gleichzeitig steigt die sichtbare Semantik auf 757 und
die rohe Gesamtsumme auf 2299: 97 weitere Parserbarrieren sind verschwunden und deutlich mehr
realer VISIA-Code gelangt nun in Namensauflösung und ByRef-Prüfung. Dieser Anstieg ist daher wie
bei `Optional` ein Übergang von Parserkaskaden zu konkreten späteren Semantiklücken. Actions #793
validiert diesen Stand mit 298 Tests, 0 Warnungen und 0 Buildfehlern sowie erfolgreichen
Compiler-/Runtime-End-to-End-Tests für `ReDim` und `ReDim Preserve`.

`Erase`, `LBound` und `UBound` schließen den nächsten Array-Runtime-/Bibliotheksslice. `Erase`
setzt feste Arrays auf ihre VB6-Initialwerte zurück und bewahrt deren Grenzen; dynamische Arrays
werden deallokiert und können anschließend wieder per `ReDim` angelegt werden. Variable-length
String-Arrayelemente verwenden dabei den VB6-Initialwert `""` statt CLR-`null`. `LBound` und
`UBound` liefern VB6-`Long`, unterstützen die optionale Dimension und verwenden ohne Angabe
Dimension 1. Actions #812 validiert den Slice mit 314 Tests, 0 Warnungen und 0 Buildfehlern. Im
VISIA-Report bleibt der Parser bei 1474, während die Semantik von 757 auf 752 und die Gesamtsumme
von 2299 auf 2294 sinkt.

Die UDT-Syntax für `Type ... End Type` ist jetzt verlustfrei im Syntaxbaum vertreten: optionale
Sichtbarkeit, skalare Felder, feste und mehrdimensionale Arrayfelder, verschachtelte Typnamen und
`String * n` bleiben erhalten. UDT-Feldnamen dürfen dabei wie in klassischem VB auch reservierte
Schlüsselwörter sein. Eine gezielte Recovery bei fehlerhaften/noch nicht unterstützten Feldformen
stellt sicher, dass der Parser im realen Korpus immer Fortschritt macht und nicht in einem
`Type`-Block hängen bleibt. Actions #832 validiert den Implementierungsstand mit 319 Tests,
0 Warnungen und 0 Buildfehlern. Die Parserdiagnostik sinkt um 260 von 1474 auf 1214; die Semantik
bleibt bei 752, die Gesamtsumme fällt entsprechend von 2294 auf 2034. UDT-Typ- und Memberbindung
bleibt bewusst der nächste M3-Slice.

Nur `.bas` wird heute gelesen; `.cls` (3), `.ctl` (4) und `.frm` (6) sind noch außen vor —
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
Deshalb wurde die `Optional`-**Syntax** nach M2 vorgezogen; Default-/Missing-Aufrufsemantik bleibt
weiterhin M5.

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
Deklarator einzeln. Explizit typisierte Listen werden bereits vollständig gebunden und emittiert.
Untypisierte Deklaratoren werden bis M4 als `VB6S0020` diagnostiziert, statt stillschweigend den
Typ des Nachbarn zu erben. Actions #604 verifiziert das mit Parser-, Binder- und End-to-End-Tests.

`Static` verwendet dieselbe Deklaratorstruktur, aber einen eigenen Syntaxknoten. Der Binder macht
die Namen für Folgeausdrücke sichtbar, emittiert sie jedoch **nicht** als normale Locals; bis die
persistente Lebensdauer in M5 implementiert ist, verhindert `VB6S0021` eine falsche Absenkung.
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
| String-Funktionen | 337 | `Optional`/`ParamArray` | 77 (`Optional`-Syntax ✅) |
| `Declare` (Win32) | 234 | Datei-I/O (`For Binary`) | 76 |
| `Property Get/Let/Set` | 209 | `On Error GoTo` / `Resume Next` | 34 / 31 |
| `ReDim`/`Preserve` | 103 ✅ typed arrays | `Type ... End Type` | 52 ✅ Syntax |
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
- [x] `Enum ... End Enum` mit optionaler Sichtbarkeit sowie expliziten/impliziten Memberwerten; Binding bleibt später
- [x] `Optional`-Parametersyntax mit `ByVal`/`ByRef` und optionalem Default-Ausdruck; ausgelassene Argumente/Defaults bleiben M5
- [x] `Option Base 0/1`, `Option Compare Text/Binary`; Auswertung bleibt bei Arrays bzw. Stringvergleichen
- [x] `:` als Anweisungstrenner für den aktuellen Statement-Subset, inklusive Single-Line-`If` und `Case`; Labels bleiben M6
- [x] Mehrfachdeklaratoren wie `Dim a As Integer, b As Long`; `As Type` gilt pro Deklarator, implizites Variant bleibt M4
- [x] `Static`-Local-Syntax; statische Lebensdauer bleibt M5 und wird bis dahin als `VB6S0021` diagnostiziert
- [x] `^` vollständig; `Like`- und `Is`-Syntax mit Semantik-Guards bis M7 bzw. M5

**Nach M3 verschoben:** `With`-Blöcke und `.Feld`-Zugriff (19 Dateien, 629 Vorkommen). Sie
brauchen einen Member-Zugriff, den es ohne UDTs und Objekte nicht sinnvoll gibt.

## Meilenstein 3 — Arrays und UDTs

Zusammen, weil Win32-Strukturen beides brauchen.

- [x] Array-Deklarationssyntax: `Dim x(10)`, `Dim x(1 To 10)`, mehrdimensional und dynamisch `Dim x()`; Grenzen werden verlustfrei im Syntaxbaum bewahrt
- [x] Arrayparameter-Syntax wie `TheArray() As String`; Parameter haben keinen statisch festgelegten Rang und ganze Arrays werden ByRef übergeben
- [x] `ArrayTypeSymbol` / `VBArray<T>` mit bekanntem oder dynamischem Rang, expliziten Unter-/Obergrenzen, Indexprüfung sowie `LBound`/`UBound`-Runtime-Grundlage
- [x] Arrayvariablen/-parameter binden; feste Arrays initialisieren; Arrayelemente lesen/schreiben/emittieren; `Option Base` auf implizite Untergrenzen anwenden; Arrayelemente ByRef weiterreichen
- [x] `ReDim` / `ReDim Preserve` für explizit typisierte dynamische Arrays inklusive Bounds, Codegen, Runtime-Wertbewahrung und End-to-End-Ausführung
- [x] `Erase`, `LBound` und `UBound` für typisierte Arrays inklusive Runtime-/Codegen-/End-to-End-Semantik
- [ ] `For Each` über Arrays — vollständige VB6-Ausführung hängt am Variant-Fundament von M4
- [x] `Type ... End Type`-Syntax mit Sichtbarkeit, skalaren/festen Arrayfeldern, verschachtelten Typnamen, Keyword-Feldnamen und `String * n`
- [ ] `UserDefinedTypeSymbol`, UDT-Memberbindung, Layout, Memberzugriff/-zuweisung und Codegen

## Meilenstein 4 — Variant

- [ ] `VBVariant`: `Empty`, `Null`, `Nothing`, `Missing`, `VarType`, `IsEmpty`/`IsNull`/`IsNumeric`
- [ ] Variant-Arithmetik mit VB6-Promotionsregeln, implizite Konvertierung
- [ ] Untypisierte `Dim`-Deklaratoren und untypisierte `Optional`-Parameter werden Variant; bis dahin `VB6S0020`
- [ ] Erstklassiges `Decimal` als additive Erweiterung

## Meilenstein 5 — Prozeduren und Klassen

- [ ] `Optional`-Aufrufsemantik/Defaults, `ParamArray`, `Static`-Local-Lebensdauer, ByRef-Randfälle
- [ ] `Is`-Objektreferenzidentität auf dem echten Klassen-/Objekttypmodell
- [ ] `Property Get`/`Let`/`Set`
- [ ] Klassenmodule: `New`, `Set`, `Class_Initialize`/`Terminate`, `Implements`
- [ ] `Event`/`RaiseEvent`, `WithEvents`
- [ ] `.cls` als Projektquelle lesen (hebt die Item-Abdeckung von 27 auf 30)

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
