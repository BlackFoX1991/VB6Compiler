# Roadmap

Weg von "VB6-Teilmenge kompiliert" zu "beliebiges Legacy-`.vbp` kompiliert unverändert", plus moderne Typerweiterungen, danach die IDE.

## Ehrliche Größeneinschätzung

Der heutige Compiler ist ~4.700 Zeilen und deckt die imperative Sprachbasis ab. Zur vollen Parität fehlen drei Blöcke, von denen **jeder einzelne größer ist als der bisherige Compiler**:

1. die VB6-Standardbibliothek (~200 Funktionen, `Format$` allein ist ein Projekt)
2. Forms + Controls + OCX-Hosting
3. COM-Interop in beide Richtungen

Das ist kein Argument gegen das Ziel, sondern für die Reihenfolge: Jede Phase unten ist für sich nutzbar, und keine erzwingt später einen Umbau der vorherigen.

## Zwei Entscheidungen, die die Architektur bestimmen

### 1. Bitness vs. Legacy-Controls — der zentrale Zielkonflikt

"64 Bit" und "alte Projekte laufen ohne weiteres" widersprechen sich an einer Stelle: **OCX/ActiveX-Controls und die meisten `Declare`-Ziele in Legacy-Projekten sind 32-Bit-COM-DLLs. Ein 64-Bit-Prozess kann sie nicht in-process laden.** Kein Compiler-Trick ändert das.

Mögliche Wege:
- **x86-Ausgabe als Default, x64 als Opt-in.** Maximale Kompatibilität, "64 Bit" gilt dann für Sprache/Typen, nicht für den Prozess.
- **x64-Default mit COM-Surrogat** für 32-Bit-Controls (out-of-process). Funktioniert für Automatisierungsobjekte, nicht für sichtbare Controls im Fenster.
- **Beides emittieren**, Wahl pro Projekt.

Empfehlung: dritter Weg, Default x86, sobald Phase E ansteht. Bis dahin blockiert die Frage nichts — sie muss aber **vor** Phase E entschieden sein, weil Marshalling-Code davon abhängt.

### 2. `Variant` sollte früh kommen, nicht spät

`Variant` ist der Default-Typ in VB6 — jede nicht deklarierte Variable, jeder `Optional`-Parameter ohne Typ, fast jede Bibliotheksfunktionssignatur. Je länger Binder und Generator ohne `Variant` wachsen, desto mehr Code muss beim Nachrüsten angefasst werden. Deshalb steht er hier in Phase A und nicht hinter den Klassen.

### 3. Ohne Konformitätskorpus ist "Parität" nicht messbar

Am wertvollsten früh: ein `conformance/`-Verzeichnis mit echten kleinen VB6-Projekten plus erwarteter Ausgabe (idealerweise unter echtem VB6 einmal aufgezeichnet). Jedes Feature erweitert den Korpus, CI hält ihn grün. Das verwandelt "unterstützt alles" von einer Meinung in eine Zahl.

---

## Phase A — Sprachkern schließen

Reihenfolge innerhalb der Phase ist bewusst.

- [ ] **Bitweise `And`/`Or`/`Xor`/`Not`/`Eqv`/`Imp` auf Numerik** + `&H`/`&O`-Literale — heute per `VB6S0018` abgelehnt, in Legacy-Code allgegenwärtig (`If (flags And &H1) <> 0`)
- [ ] `Const`, `Option Explicit`-Durchsetzung, `Option Base`, `Option Compare`
- [ ] **Arrays** — fest, dynamisch, mehrdimensional, `ReDim`/`Preserve`, `LBound`/`UBound`, `Erase`, `For Each`
- [ ] `String`-Vollständigkeit: Strings fester Länge, `Option Compare Text/Binary` in Vergleichen
- [ ] `Date` + `#...#`-Literale (VB6-Date ist ein Double-Serial, nicht `DateTime`)
- [ ] **`Variant`** — `Empty`, `Null`, `Nothing`, `Missing`, `VarType`, `IsEmpty`/`IsNull`/`IsNumeric`, implizite Konvertierungen, Variant-Arithmetik mit VB6-Promotionsregeln
- [ ] `Type ... End Type` (UDT), `Enum`
- [ ] Zeilennummern und Labels

**Moderne Erweiterungen dieser Phase** (additiv, siehe Invariante in CLAUDE.md): erstklassiges `Decimal` (VB6 kennt es nur als Variant-Subtyp), vorzeichenlose Ganzzahltypen, `LongPtr`.

## Phase B — Prozedur- und Modulmodell

- [ ] `Optional` mit Defaultwerten, `ParamArray`, `Static`-Locals
- [ ] ByRef-Randfälle: geklammerte Argumente, temporäre Konvertierungen
- [ ] `Property Get`/`Let`/`Set`
- [ ] **Klassenmodule** — `New`, `Set`, `Class_Initialize`/`Terminate`, `Implements`, `Event`/`RaiseEvent`/`WithEvents`, Default-Properties
- [ ] Bedingte Kompilierung `#If`/`#Const`
- [ ] `With`-Blöcke

## Phase C — IR und Fehlerbehandlung

**Hier ist der Punkt, an dem das Lowering aus dem Generator raus muss.** `Resume` muss zur Anweisung *nach* der fehlgeschlagenen zurückkehren — das braucht eine explizite Anweisungsnummerierung und einen Zustandsautomaten pro Prozedur, kein Textgenerator.

- [ ] Lowered IR mit explizitem Control Flow (Basic Blocks, Labels, Sprünge)
- [ ] `GoTo`, `On ... GoTo`, `GoSub`/`Return`
- [ ] **`On Error GoTo` / `Resume` / `Resume Next` / `Err`-Objekt** mit VB6-Fehlercodes
- [ ] `End`, `Stop`, `DoEvents`

C# als Backend trägt das (`goto` + `switch`-Automat). Falls stattdessen direkt IL emittiert werden soll, ist **hier** der Entscheidungspunkt, nicht später.

## Phase D — VB6-Standardbibliothek

- [ ] String: `Left`/`Right`/`Mid`/`Len`/`InStr`/`InStrRev`/`Replace`/`Split`/`Join`/`Trim`/`LCase`/`UCase`/`StrComp`/`String`/`Space`/`StrReverse`
- [ ] Math: `Abs`/`Sgn`/`Sqr`/`Int`/`Fix`/`Round`/`Rnd`/`Randomize`/trigonometrisch/`Log`/`Exp`
- [ ] Konvertierung: vollständige `C*`-Familie, `Val`, `Str`, `Hex`, `Oct`, `Asc`, `Chr`
- [ ] **`Format$`/`Format`** — eigenes Teilprojekt, VB6-Formatstrings sind nicht .NET-Formatstrings
- [ ] Datum/Zeit: `Now`/`Date`/`Time`/`DateAdd`/`DateDiff`/`DatePart`/`DateSerial`/`Timer`
- [ ] Datei-I/O-Statements: `Open`/`Close`/`Print #`/`Write #`/`Input #`/`Line Input #`/`Get`/`Put`/`Seek`/`FreeFile`/`Dir`/`Kill`/`Name`
- [ ] `Collection`, `App`, `Screen`, `Printer`, `Clipboard`
- [ ] Finanzfunktionen (`Pmt`, `NPV`, `IRR`, ...)
- [ ] `MsgBox`/`InputBox`

## Phase E — Interop

- [ ] `Declare` -> P/Invoke, inkl. `Alias`, `As Any`, ANSI-String-Marshalling
- [ ] COM-Konsum: Typbibliotheken aus `.vbp`-`Reference=`-Zeilen, `CreateObject`, `GetObject`, Late Binding über `IDispatch`
- [ ] COM-Bereitstellung: Projekttypen ActiveX-DLL/-EXE
- [ ] **Bitness-Entscheidung umgesetzt** (siehe oben)

## Phase F — Forms

Größter Einzelblock. Hier entscheidet sich, ob echte Legacy-Anwendungen laufen.

- [ ] `.frm`/`.frx` parsen -> Formulardefinition
- [ ] VB6-kompatible Forms-Runtime auf WinForms: intrinsische Controls, Property-/Event-Mapping, `Load`/`Unload`/`Show`/`Hide`, Twips-Koordinaten
- [ ] **Control-Arrays** — kein WinForms-Konzept, braucht eigene Nachbildung
- [ ] Zeichnen: `Line`/`Circle`/`PSet`/`Print` auf Form und `PictureBox`
- [ ] OCX-Hosting (ActiveX-Control-Container)
- [ ] MDI-Formulare
- [ ] Nativer Windows-Apphost `.exe` statt DLL + runtimeconfig
- [ ] `.res`-Ressourcendateien

## Phase G — IDE

Erst sinnvoll, wenn Phase F trägt — der Designer braucht dieselbe Formularrepräsentation, die der Compiler liest.

- [ ] Editor mit VB6-Syntax, Projektbaum, Compilerdiagnostik inline
- [ ] WinForms-Designer, der `.frm` liest und schreibt (Roundtrip verlustfrei)
- [ ] Debugger: Haltepunkte, Direktfenster, Überwachungsausdrücke
- [ ] Kompilieren und Starten aus der IDE

---

## Kurzfristig als Nächstes

Unabhängig von der großen Reihenfolge, klein und in sich abgeschlossen:

1. `Debug.Print` auf VB6-Formatierung bringen (führendes Leerzeichen, 15 signifikante Stellen) und die E2E-Tests von `.Trim()` befreien
2. Typisierte Vergleiche direkt emittieren statt über `object`-Boxing
3. `conformance/`-Korpus aufsetzen, solange er noch klein ist
4. `Currency`-Promotionsregeln gegen echtes VB6 verifizieren (`Currency + Double` liefert hier heute `Currency`)
