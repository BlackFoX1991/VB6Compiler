# Roadmap

Weg von "VB6-Teilmenge kompiliert" zu "beliebiges Legacy-`.vbp` kompiliert unverändert", plus moderne Typerweiterungen und danach die IDE.

Die Reihenfolge ist corpus-getrieben. VISIA 4.8.7.1 dient als realer Akzeptanzkorpus; dadurch werden Milestones inzwischen bewusst überlappend bearbeitet, wenn ein kleiner späterer Slice einen großen Parser-/Semantik-Blocker entfernt.

## Gemessener Ist-Stand

Erhoben mit `vb6c <projekt.vbp> --report` gegen VISIA 4.8.7.1 (10.152 Zeilen, 42 Quelldateien):

| Stand | Fehler gesamt | Parser | Lexer | Semantik | fehlerfreie Dateien |
|---|---:|---:|---:|---:|---:|
| Nulllinie (M0) | 3361 | 3183 | 178 | 0 | 0 von 27 |
| M2 abgeschlossen | 2219 | 1758 | 68 | 393 | 0 von 27 |
| M3 Array-Syntax/Runtime-Basis | 2105 | 1644 | 68 | 393 | 0 von 27 |
| M3 Array-Bindung/Elementzugriff | 2032 | 1571 | 68 | 393 | 0 von 27 |
| M3 `ReDim` / `ReDim Preserve` | 2299 | 1474 | 68 | 757 | 0 von 27 |
| M3 `Erase` / `LBound` / `UBound` | 2294 | 1474 | 68 | 752 | 0 von 27 |
| M3 `Type ... End Type` + UDT-Typraum | 2034 | 1214 | 68 | 752 | 0 von 27 |
| UDT-Memberzugriff | 1671 | 630 | 68 | 973 | 0 von 27 |
| `With` | 1632 | 591 | 68 | 973 | 0 von 27 |
| M3/M4 `For Each` + Variant-Basis | 1535 | 494 | 68 | 973 | 0 von 27 |
| `Len` / Kontext-`Alias` | 1499 | 480 | 68 | 951 | 0 von 27 |
| `Mid` / `Chr` / Variant-Stringverkettung | 1425 | 480 | 68 | 877 | 0 von 27 |
| Enum-Binding + bracketed identifiers | 1350 | 480 | 62 | 808 | 0 von 27 |
| aktuelles `main` vor dem File-I/O-Slice | **1339** | **480** | **62** | **797** | **0 von 27** |
| aktueller Dokumentations-Head | **1318** | **92** | **0** | **1226** | **0 von 27** |

Der aktuelle Referenz-Head wird in Windows CI mit **494 Tests, 0 Fehlern, 0 Skips**, einem warnungsfreien Release-Build und erfolgreichem CLI-Publish validiert.

Der starke Sprung von 480 auf 92 Parserfehler und von 62 auf 0 Lexerfehler ist absichtlich mit einem Anstieg sichtbarer Semantik verbunden. File-I/O, `TypeOf`, call-site `ByVal` und implizite Variant-Function-Returns werden jetzt syntaktisch sauber bewahrt; dadurch erreichen deutlich mehr echte VISIA-Prozeduren die Namensauflösung und ByRef-Prüfung statt vorher in Parserkaskaden zu verschwinden.

### Aktuelle Fehlerfront

| Code | Anzahl | Bedeutung |
|---|---:|---|
| `VB6S0007` | **515** | ByRef-Argument ist im aktuellen Modell nicht als passende Variable/Adresse bindbar |
| `VB6S0005` | **417** | aufgerufene Prozedur nicht aufgelöst |
| `VB6S0001` | **177** | Variable nicht aufgelöst |
| `VB6P0001` | **92** | verbleibende Parserfehler |
| `VB6S0006` | **65** | Argumentanzahl passt nicht zur bekannten Signatur |
| `VB6S0059` | **22** | call-site `ByVal` syntaktisch erkannt, Semantik noch offen |
| `VB6S0057` | **18** | Datei-I/O syntaktisch erkannt, Runtime noch offen |

Die drei größten semantischen Familien (`VB6S0007`, `VB6S0005`, `VB6S0001`) machen den Großteil der aktuellen Fehler aus. Der nächste Hebel liegt deshalb nicht mehr primär im Lexer.

Nur `.bas` wird heute kompiliert; `.cls` (3), `.ctl` (4) und `.frm` (6) werden vom Projektloader inventarisiert, aber noch nicht in den Compilerpfad aufgenommen. Daher bleiben 27 von 40 VISIA-Projektitems analysiert.

## Was die Messung an der Planung geändert hat

Frühe Parserbarrieren werden weiterhin vorgezogen, wenn sie viele Dateien gleichzeitig entsperren. Dieses Prinzip hat sich mehrfach bestätigt: `Attribute`, Sichtbarkeitsmodifizierer, UDT-Memberzugriff, `With`, Kontext-`Alias`, bracketed identifiers und zuletzt File-I/O-Syntax haben deutlich mehr realen Code in die Semantik gebracht.

Der aktuelle Lexer-Frontier ist im VISIA-Pfad bei null. Die verbleibenden 92 Parserfehler werden daher gezielt nach tatsächlicher Korpusfrequenz bearbeitet, während parallel die nun dominanten ByRef-/Symbolauflösungsfehler reduziert werden.

Ein zusätzlicher, noch nicht gemergter Kandidat existiert auf `agent/m4-partial-module-symbols`: gültige Enum-/UDT-/Prozedur-/Modulvariablendeklarationen sollen projektweit erhalten bleiben, auch wenn später im selben Modul Parserfehler auftreten. Diese Änderung ist nicht Teil des aktuellen File-I/O-Branches und sollte als eigener kleiner Slice validiert werden, weil sie direkt auf `VB6S0005`/`VB6S0001`-Folgefehler wirken kann.

## Korpus-Frequenzen

VISIA ist ein Systemprogramm (Assembler, Linker, PE-Erzeugung), kein typisches Business-Programm:

| Konstrukt | Häufigkeit/Stand | Konstrukt | Häufigkeit/Stand |
|---|---|---|---|
| `&H`/`&O`-Literale | 892 ✅ | `Event`/`RaiseEvent` | 97 |
| String-Funktionen | 337, teilweise ✅ | `Optional`/`ParamArray` | 77, Syntax teilweise ✅ |
| `Declare` (Win32) | 234, Syntax ✅ | Datei-I/O (`For Binary`) | 76, Syntax ✅ / Runtime offen |
| `Property Get/Let/Set` | 209 | `On Error GoTo` / `Resume Next` | 34 / 31 |
| `ReDim`/`Preserve` | 103 ✅ typisierte Arrays | `Type ... End Type` | 52 ✅ Kernpfad |
| `With` | 102 ✅ UDT-Pfad | `Enum` | 44 ✅ Binding |

Nicht bzw. praktisch nicht im Korpus: `Format$`, Datum/Zeit, ADO, `#If`, vollständiges `Resume`-Statement. Das beeinflusst die Reihenfolge, nicht das langfristige Paritätsziel.

## Entschiedene Weichenstellungen

- **VB6-Semantik vor Bequemlichkeit.** Moderne Erweiterungen dürfen bestehende VB6-Semantik nicht verändern.
- **Variant früh und schrittweise.** Der Umbau ist strukturell wichtig, auch wenn VISIA relativ wenig explizites Variant enthält.
- **x86 als Default-Ausgabe, x64 opt-in.** VISIA hängt an 32-Bit-OCX; die Sprachunterstützung für breitere Typen ist davon unabhängig.
- **VISIA ist Testkorpus, nicht Portierungsziel.** Die spätere IDE entsteht eigenständig in C#/WinForms.
- **Nicht implementierte Semantik wird diagnostiziert.** Syntax wird lieber verlustfrei bewahrt und mit einem dedizierten Guard gestoppt, statt stillschweigend CLR-/C#-Semantik zu approximieren.

---

## Meilenstein 0 — Paritätsmessung ✅

`vb6c <projekt.vbp> --report` liefert Item-Inventar, Anteil fehlerfrei analysierter Dateien, Fehlercodes, betroffene Dateien und die größten verbleibenden Lücken.

## Meilenstein 1 — Bitweise Semantik und Zahlliterale ✅

`&H`/`&O`-Literale mit VB6-Wrapping, `&`/`%`-Typsuffixe an Literalen sowie bitweise `And`/`Or`/`Xor`/`Eqv`/`Imp`/`Not` auf Numerik sind implementiert und End-to-End getestet.

## Meilenstein 2 — Dateien überhaupt lesbar machen ✅

- [x] `Attribute`-Zeilen auf Modulebene
- [x] Moduldeklarationen mit `Public`/`Private`/`Global`/`Dim`
- [x] `Public`/`Private`/`Friend` an `Sub` und `Function`
- [x] Bezeichner-Typsuffixe `$ % & ! # @`
- [x] Zeilenfortsetzung mit `_`
- [x] `Const`, typisiert und aus dem Wert abgeleitet
- [x] `Exit Sub` / `Exit Function`
- [x] `Declare`-Syntax mit `Lib`, `Alias`, `As Any`; P/Invoke bleibt M8
- [x] `Enum ... End Enum`-Syntax; Binding wurde später ergänzt
- [x] `Optional`-Parametersyntax; ausgelassene Argumente/Defaults bleiben M5
- [x] `Option Base 0/1`, `Option Compare Text/Binary`
- [x] `:` als Statement-Separator; Labels bleiben M6
- [x] Mehrfachdeklaratoren mit VB6-Typregel pro Deklarator
- [x] `Static`-Local-Syntax; persistente Lebensdauer bleibt M5
- [x] `^` vollständig; `Like` und expression-level `Is` syntaktisch bewahrt und guarded
- [x] kontextabhängiges `Alias` außerhalb echter `Declare ... Alias`-Klauseln
- [x] bracketed identifiers wie `[End]`

## Meilenstein 3 — Arrays und UDTs

Der Kernpfad ist weitgehend implementiert; offen sind nur noch bestimmte Layout-/Objektgrenzen.

- [x] feste, explizit begrenzte, mehrdimensionale und dynamische Arrays
- [x] Arrayparameter mit unbekanntem Rang und VB6-ByRef-Grundregel
- [x] `ArrayTypeSymbol` / `VBArray<T>` mit Bounds, Rang und Indexprüfung
- [x] Arraybindung, feste Initialisierung, Elementzugriff/-zuweisung, `Option Base`, ByRef-Arrayelemente
- [x] `ReDim` / `ReDim Preserve`
- [x] `Erase`, `LBound`, `UBound`
- [x] `For Each` über feste Arrays
- [x] `For Each` über dynamische/unknown-rank Arrays und Arrayparameter
- [x] `For Each` über array-valued UDT-Member und implizite `With`-Member
- [x] `Type ... End Type`-Syntax und stabiler Public-/Private-UDT-Typraum
- [x] UDT-Werte in Parametern, Returns, Locals und Modulvariablen
- [x] managed UDT-Storage, Member Reads/Writes und `With`
- [x] `String * n`-Member mit VB6-Padding/Truncation
- [x] ByRef-Zugriff auf UDT-Member und UDT-Member-Arrayelemente
- [x] Wertkopie/Clone-Lowering für UDTs mit verwaltetem Array-Backing
- [x] feste primitive UDT-Arraymember
- [ ] dynamische UDT-Arraymember vollständig ausführen
- [ ] Arraymember aus `String * n`
- [ ] Arrays von UDT-Elementen vollständig freigeben
- [ ] rekursive by-value UDT-Layouts

## Meilenstein 4 — Variant

Der Variant-Unterbau ist vorhanden, die vollständige VB6-Zustands- und Operatormatrix noch nicht.

- [x] explizites `As Variant` für Locals, Arrays, ByVal-Parameter und Function-Returns
- [x] implizite Variant-Locals, Modulvariablen und `Static`-Deklarationen ohne `As`
- [x] untypisierte Function-Returns werden Variant
- [x] Projektpipeline verwendet dieselbe implizite-Variant-Normalisierung wie Single-File-Kompilierung
- [x] Variant als `For Each`-Kontrollvariable mit Value-Semantik
- [x] corpus-reachable Variant-Multiplikation
- [x] begrenzte numerische Variant-Gleichheit gegen integrale Werte
- [x] gebundene Variant-origin Stringverkettung über `&`
- [ ] vollständige arithmetische/logische/vergleichende Variant-Operatormatrix
- [ ] `Null`, `Nothing`, `Missing`, vollständige `Empty`-Semantik
- [ ] `VarType`, `TypeName`, `IsEmpty`, `IsNull`, `IsNumeric`
- [ ] untypisierte `Optional`-Parameter und vollständige Missing/Default-Integration
- [ ] erstklassiges `Decimal` als additive Erweiterung

## Meilenstein 5 — Prozeduren und Klassen

- [ ] `Optional`-Aufrufsemantik/Defaults
- [ ] `ParamArray`
- [ ] `Static`-Local-Lebensdauer
- [ ] vollständige ByRef-Randfälle und temporäre Konvertierungen
- [x] call-site `ByVal` wird syntaktisch korrekt erkannt; Semantik bleibt mit `VB6S0059` guarded
- [x] `TypeOf ... Is ...` wird syntaktisch korrekt erkannt; Objektsemantik bleibt mit `VB6S0058` guarded
- [ ] `Is`-Objektreferenzidentität auf echtem Klassen-/Objektmodell
- [ ] `Property Get`/`Let`/`Set`
- [ ] Klassenmodule: `New`, `Set`, `Class_Initialize`/`Terminate`, `Implements`
- [ ] `Event`/`RaiseEvent`, `WithEvents`
- [ ] `.cls` in den Compilerpfad aufnehmen (27 -> 30 analysierte Items)

## Meilenstein 6 — IR und Fehlerbehandlung

Hier muss langfristig Control-Flow-Lowering aus dem C#-Generator heraus. `On Error Resume Next` benötigt eine feinere, explizite IR als die heutige direkte Emission.

- [ ] Lowered IR mit Basic Blocks und expliziten Sprüngen
- [ ] `GoTo`, Labels, Zeilennummern, `On ... GoTo`, `GoSub`/`Return`
- [ ] `On Error GoTo`, `On Error Resume Next`, `On Error GoTo 0`, `Err`-Objekt

## Meilenstein 7 — Standardbibliothek

Corpus-getrieben priorisiert:

- [x] `Len`
- [x] dreiargumentiges `Mid` / `Mid$`
- [x] `Chr` für den aktuell erreichten ASCII-Bereich
- [x] globale Stringkonstanten (`vbCrLf`, `vbTab`, usw.)
- [ ] `Left`, `Right`, `InStr`, `Replace`, `Trim`, `UCase`, `Asc`, `Split` und weitere Stringfunktionen
- [x] Datei-I/O-Lexer/Parser für Dateinummern und `Open`/`Get`/`Put`/`Close`/`Seek`/`Input`/`Write`/`Kill`/`Print #`
- [ ] Datei-I/O-Runtime/Codegen (`VB6S0057` ist die aktuelle Grenze)
- [ ] `LOF`, `FreeFile` und weitere Dateifunktionen
- [ ] `MsgBox` / `InputBox`
- [ ] Math-/Konvertierungsbibliothek vervollständigen
- [ ] vollständiges `Like` inkl. `Option Compare`
- [ ] danach erst Format/Datum/Zeit/Finanzfunktionen, die im VISIA-Korpus kaum vorkommen

## Meilenstein 8 — Interop

Durch die 234 `Declare`s im Korpus früh relevant und weitgehend parallel zum Sprachkern entwickelbar.

- [ ] `Declare` -> P/Invoke mit `Alias`, `As Any`, ANSI-String-Marshalling
- [ ] COM-Konsum aus `Reference=` / `Object=`, `CreateObject`, `IDispatch`
- [ ] x86-Standardausgabe und nativer Apphost statt DLL + runtimeconfig
- [ ] `LongPtr` und weitere additive moderne Ganzzahltypen

## Meilenstein 9 — Forms und UserControls

- [ ] `.frm` / `.frx` in den Compiler-/Designerpfad aufnehmen
- [ ] intrinsische Controls und Property-/Event-Mapping
- [ ] Twips, `Load` / `Unload` / `Show`, Zeichnen, MDI
- [ ] Control-Arrays
- [ ] `UserControl` / `.ctl` und die vier VISIA-Controls
- [ ] OCX-Hosting für `MSComctlLib`, `RichTextLib`, `MSComDlg`

## Meilenstein 10 — IDE

Eigenständig in C#/WinForms, sobald der Compiler trägt: Editor, Projektbaum, Inline-Diagnostics, Designer mit verlustfreiem `.frm`-Roundtrip und Debugger.

---

## Unmittelbare Reihenfolge

1. `agent/m4-partial-module-symbols` als eigenen Slice gegen den aktuellen Head rebasen/übertragen und messen; Ziel ist weniger symbolbedingter Fallout aus teilweise parsebaren Modulen.
2. Danach die dominante ByRef-Familie (`VB6S0007`) corpus-getrieben aufspalten: echte temporäre ByRef-Konvertierungen, call-site `ByVal`, parenthesized arguments und fehlende Optional-Call-Semantik nicht vermischen.
3. Die verbleibenden 92 Parserfehler nach konkreten Syntaxformen gruppieren und nur die hochwirksamen Familien öffnen.
4. Standardbibliothek nach realer Häufigkeit ergänzen (`Left`, `UCase`, `InStr`, Konvertierungen usw.), ohne User-Prozeduren fälschlich als Built-ins zu behandeln.
5. File-I/O-Runtime auf die bereits stabile Syntaxschicht setzen.
6. Danach Klassen/Properties, IR/Error Handling und Interop weiterziehen.

## Zusätzlich, klein und unabhängig

1. `Debug.Print` auf echte VB6-Formatierung bringen; die heutigen E2E-Tests verdecken Unterschiede teilweise mit `.Trim()`.
2. Typisierte Vergleiche nach Möglichkeit ohne unnötiges Boxing emittieren.
3. Grenzfälle bei gemischter `Currency`-/Floating-Arithmetik gegen echtes VB6 verifizieren.
4. Historische Agent-Branches nach Merge/Superseding löschen, sobald sichergestellt ist, dass keine einzigartige Änderung wie bei `m4-partial-module-symbols` mehr enthalten ist.