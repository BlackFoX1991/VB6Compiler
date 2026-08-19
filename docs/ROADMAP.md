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

`Declare` senkt die Gesamtzahl um 142 und die Parserfehler um 160. `Enum` bringt weitere 222
Parserfehler weg. `Optional` senkt die Parserfehler nochmals um 94. Die rohe Gesamtzahl steigt
dabei von 2100 auf 2216, weil 210 zusätzliche Semantikdiagnosen sichtbar werden: mehr echte
Prozeduren erreichen nun den Binder, statt an ihrer Parameterliste zu entgleisen. Das ist kein
Parser-Rückschritt, sondern genau der gewünschte Übergang von Syntaxkaskaden zu konkreten
semantischen Lücken. `comAssembler.bas` erreicht jetzt als ersten Fehler eine fehlende Prozedur;
`comMemory.bas` beginnt mit einem konkreten ByRef-Randfall. Der VISIA-Report läuft als eigener
Schritt in GitHub Actions nach Build und Tests.

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

Danach, nach betroffenen Dateien sortiert:

| Blocker | Belege |
|---|---|
| `Attribute`-Kopfzeile | 27 von 27 Dateien |
| Deklarationen auf Modulebene (`Public x As Long`) | 22 Dateien |
| `Sub`/`Function` mit `Public`/`Private`-Modifizierer | 20 Dateien |
| `With`-Blöcke (`.Feld`-Zugriff) | 19 Dateien, 629 Vorkommen |
| Bezeichner-Typsuffixe | `Mid$` 110×, `ret&` 26×, `lphKey&` 10× |
| `:` als Anweisungstrenner | `AppType = 0: pError = False` |
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

## Meilenstein 2 — Dateien überhaupt lesbar machen (teilweise)

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
- [ ] `:` als Anweisungstrenner
- [ ] `Dim a, b As Integer` (Mehrfachdeklaratoren), `Static`-Locals
- [ ] `Option Base`, `Option Compare`
- [ ] `^`-Operator, `Like`, `Is`

**Nach M3 verschoben:** `With`-Blöcke und `.Feld`-Zugriff (19 Dateien, 629 Vorkommen). Sie
brauchen einen Member-Zugriff, den es ohne UDTs und Objekte nicht sinnvoll gibt.

## Meilenstein 3 — Arrays und UDTs

Zusammen, weil Win32-Strukturen beides brauchen.

- [ ] `Dim x(10)`, `Dim x(1 To 10)`, mehrdimensional, `Option Base`
- [ ] `ReDim` / `ReDim Preserve`, `Erase`, `LBound`/`UBound`, `For Each`
- [ ] `Type ... End Type`, verschachtelt, mit festen Arrays und `String * n`
- [ ] Neu: `ArrayTypeSymbol`, `UserDefinedTypeSymbol`; `VBArray<T>` in der Runtime

## Meilenstein 4 — Variant

- [ ] `VBVariant`: `Empty`, `Null`, `Nothing`, `Missing`, `VarType`, `IsEmpty`/`IsNull`/`IsNumeric`
- [ ] Variant-Arithmetik mit VB6-Promotionsregeln, implizite Konvertierung
- [ ] Untypisierte `Dim` und untypisierte `Optional`-Parameter werden Variant
- [ ] Erstklassiges `Decimal` als additive Erweiterung

## Meilenstein 5 — Prozeduren und Klassen

- [ ] `Optional`-Aufrufsemantik/Defaults, `ParamArray`, `Static`-Locals, ByRef-Randfälle
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
4. Math, Konvertierung, `Like`
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
