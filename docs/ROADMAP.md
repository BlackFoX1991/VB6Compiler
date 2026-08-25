# Roadmap

Dieses Dokument beschreibt den **Ist-Stand und das Offene**. Die chronologische Historie — was
wann implementiert und gemessen wurde — steht in `CHANGELOG.md` und gehört nicht hierher.

## Produktziel

Das Hauptprodukt ist ein moderner, hochkompatibler VB6-Compiler, nicht die VISIA-Portierung und
nicht zuerst die IDE. Der Compiler soll die vollständige VB6-Sprache und Standardbibliothek mit
einem eigenen Runtime-/Objektmodell abbilden, COM/ActiveX konsumieren und emittieren, sowie
native Windows-Ziele (x86 und x64 über LLVM) und .NET bedienen. `.vbp`/`.vbg` plus MSBuild SDK sind die
Projektverträge. Visual Studio wird später über LSP angebunden; Forms- und WinForms-Designer
folgen erst nach dem Compiler-Kern.

Der historische Plan wird auf das eigentliche Produktziel eingeordnet: ein moderner, hochkompatibler
VB6-Compiler mit eigenem Runtime-/Objektmodell, COM/ActiveX-Kompatibilität, .NET- und nativen
Windows-Backends. VISIA ist Regressionstestkorpus; Visual Studio/LSP, IDE und Designer folgen später.

Die aktuelle Priorisierung ist bewusst **.NET-first**: Der Managed-Emitter, die Runtime, Variant-/Object-
Semantik, COM-/ActiveX-Konsum und die Visual-Studio-/MSBuild-Buildverträge werden zuerst bis zu einem
stabilen Kompatibilitätsziel geführt. LLVM bleibt als optionaler nativer x86/x64-Backendpfad im Projekt,
wird aber bis zum Abschluss dieses Managed-Ziels nicht als Blocker behandelt.

Die Reihenfolge stammt aus einer Konstrukt-Frequenzanalyse über echten VB6-Code, nicht aus einer
generischen VB6-Feature-Liste.

## Gemessener Ist-Stand

Zwei Messungen definieren den Stand. Beide sind reproduzierbar und dürfen sich nicht
verschlechtern.

**Korpusparität** — `vb6c conformance/VISIA/4.8.7.1/prjVisia.vbp --report`:

| Messpunkt | Fehler gesamt | Parser | Lexer | Semantik | fehlerfreie Dateien |
|---|---|---|---|---|---|
| 2026-08-25 | **0** | **0** | **0** | **0** | **40 von 40** |

Alle 40 `.bas`-, `.cls`-, `.frm`- und `.ctl`-Quellen werden gelesen, Designer-Metadaten
offsettreu ausgeblendet, typisiert und gebunden; das Gesamtprojekt emittiert auch durch
(`--emit-assembly`). Zum Vergleich die Nulllinie: 3361 Fehler, 0 von 27 Dateien. Der Weg
dorthin steht als Messreihe in `CHANGELOG.md`.

**Regressionssuite** — `dotnet test VB6Compiler.sln -c Release`: **1099 Tests, alle grün**
(Stand 2026-08-25).

Als Compiler-Kern vorhanden: `Property Get/Let/Set`, Events, `WithEvents`, `New`, `Set`,
`TypeOf`, Variant-Arrays, Standard-`Collection`, late-bound Object-/Control-Mitglieder sowie
`On Error` mit `Err` und `Resume Next`. Managed-Klasseninstanzen haben eigenen Feldspeicher,
Konstruktor-/Terminator-Lifecycle, Property-Dispatch, `RaiseEvent`/`WithEvents`-Emission und
echte Referenzidentität; `Implements` wird als CLR-Interface emittiert und über `callvirt`
inklusive Property-Accessors dispatcht.

Offen sind die Blöcke, die die Meilensteine unten führen: vollständige COM-/IDispatch-Identität,
OCX-Komposition, Forms-Vollständigkeit sowie native ABI-Emission.

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
3. **MDI und `DrawMode` kommen im Korpus nicht ein einziges Mal vor.** Beide stehen in M9 als
   offene Punkte und werden deshalb zurückgestellt, nicht gebaut.

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
einen fehlerstellenspezifischen Fortsetzungsdispatcher. Der native Resume-/ABI-Vertrag bleibt offen.

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
- **Zahlkonvertierung ist invariant, nicht locale-abhängig.** `VB6.Runtime` konvertiert zwischen
  Strings und Zahlen ausschließlich mit `CultureInfo.InvariantCulture`. Klassisches VB6 wertete
  `CDbl("2.5")` gegen die aktive Locale aus, sodass derselbe Quelltext je nach Maschine 2,5 oder
  25 ergab. Für einen Compiler wiegt Determinismus schwerer als diese Treue: das Kompilat soll
  überall dasselbe tun. Echte locale-abhängige Ausgabe gehört später zu `Format$`, wo die Locale
  ein expliziter Parameter ist statt ambienter Thread-Zustand. Dies ist eine der wenigen
  Stellen, an denen bewusst von VB6 abgewichen wird.
- **`vbUseSystem` ist die eine erlaubte Ausnahme davon.** Wo VB6 den Wert 0 als „frag das System"
  definiert — `FirstDayOfWeek` und `FirstWeekOfYear` in `Weekday`, `WeekdayName`, `Format` und den
  Datumsfunktionen — löst die Runtime bewusst über `CultureInfo.CurrentCulture` auf. Das ist keine
  versehentliche Locale-Abhängigkeit, sondern genau der angeforderte Wert: Der Aufrufer verlangt
  ausdrücklich die Systemeinstellung, und ihn auf Sonntag festzunageln wäre die Abweichung. Die
  Ausnahme gilt eng für diesen einen Parameterwert; mit explizitem `vbSunday`/`vbMonday` ist das
  Ergebnis kulturunabhängig. Ein Test hält beide Seiten fest. Ebenfalls bewusst kulturabhängig:
  die LCID des COM-Dispatch. Jede weitere `CurrentCulture`-Verwendung in `VB6.Runtime` ist ein
  Fehler, kein Präzedenzfall.
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
- [x] `Enum ... End Enum` mit optionaler Sichtbarkeit sowie expliziten/impliziten Memberwerten; inzwischen auch als Long-basierte Konstanten gebunden
- [x] `Optional`-Parametersyntax mit `ByVal`/`ByRef` und optionalem Default-Ausdruck; ausgelassene Argumente/Defaults sind umgesetzt
- [x] `Option Base 0/1`, `Option Compare Text/Binary`; Auswertung bleibt bei Arrays bzw. Stringvergleichen
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
- [ ] Vollständige Variant-Arithmetik mit VB6-Promotionsregeln und impliziter Konvertierung. Numerische `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, logische Operatoren, Vergleiche, `&` und die String/Variant-Sonderregeln von `+` sind für die aktuelle Scalar-Variantmenge implementiert; `CDec` sowie Decimal-aware `+`, `-`, `*`, `/`, `Mod`, `\`, `^`, logische Operatoren, unäres `-` und Vergleiche sind ergänzt. Empty-Operanden, Null-Vergleiche, Null-Arithmetik, Null-If-Verzweigungen, Null bei `&` inklusive `Null & Null` sowie Currency-/Single-Vergleichspromotionen sind regressionsgesichert. Offen bleiben weitere `Null`/`Missing`-Sonderfälle, Objekt- und Array-Varianten sowie die abschließende Prüfung aller VB6-Promotionstabellen.
- [ ] Erstklassiges `Decimal` als additive Erweiterung. `CDec` liefert den Variant-Subtype 14, die zentralen skalaren Rechenpfade erhalten Decimal-Werte und die aktuelle Operator-/Konvertierungsmenge ist abgedeckt; offen bleiben die vollständige Promotionstabelle und noch nicht unterstützte Variant-Subtypen.

## Meilenstein 5 — Prozeduren und Klassen

- [x] `Optional`-Aufrufsemantik/Defaults **vorgezogen**: ausgelassene Argumente erhalten den deklarierten Default oder den Typdefault
- [x] `ParamArray` als letztes `Variant`-Array-Argument mit leerem Aufruf und gemischten Werten
- [x] `Static`-Local-Lebensdauer ueber compiler-generierten Modul-Storage inklusive String-/Array-Initialisierung
- [x] ByRef-Randfälle **vorgezogen**: Temporaries für Literale/Ausdrücke/Funktionsergebnisse,
      Klammern erzwingen ByVal, Typmismatch bleibt `VB6S0008`
- [~] `Is`-Objektreferenzidentität für Variant-/Hostobjekte und emittierte Klasseninstanzen steht; COM-RCW-Identität wird über `IUnknown` verglichen, die übrige COM-Interop bleibt offen
- [~] `Property Get`/`Let`/`Set`: typisierte Managed-Instanz-Dispatch-Emission sowie implizites `Item`-Default-Property-Get/Let und `VB_UserMemId`-benannte Default-Properties stehen; numerische Variant-Objektindizes fallen auf das Managed-Default-`Item` zurück; vollständige benannte Default-Property- und COM-Dispatch-Regeln bleiben offen
- [~] Klassenmodule: `.cls`, Klassentypen, `New`, `Set`, `TypeOf`, Instanzspeicher sowie `Class_Initialize`/`Terminate` sind emittiert; `Implements` wird als CLR-Interface mit MethodImpl-/Property-Dispatch emittiert, COM-Dispatch und Forms bleiben offen
- [~] Standard-`Collection`: semantischer Vertrag sowie Managed-`New`/`Count`/`Item`/`Add`/`Remove`/`For Each` mit one-based, keyed lookup und Einfügereihenfolge stehen; vollständige Fehlercodes und COM-Collection-Dispatch bleiben offen
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
- [x] `On Error GoTo`, `On Error Resume Next`, `On Error GoTo 0`, `Err`-Objekt und fehlerstellenspezifischer
      `Resume Next`-Dispatcher im Managed-Backend; native ABI- und vollständige Handlerzustände offen
- [x] Quellpositionen: der Binder hängt `SourceLocation` referenziell an jede gebundene Anweisung,
      `IrLowerer` stempelt sie auf die entstehenden Instruktionen, der Emitter merkt sich die
      IL-Offsets und `PortablePdbEmitter` schreibt daraus Sequenzpunkte. Die PDB trägt damit
      Dokumente, Locals, Anweisungsgrenzen und procedure-wide Scopes mit Start/Length. Die
      verbleibende native Debug-ABI ist offen.

## Meilenstein 7 — Standardbibliothek

Nach Korpusbedarf priorisiert:

1. String-Funktionen — `Left`/`Right`/`Mid`/`Len`/`InStr`/`Replace`/`Trim`/`UCase`/`Chr`/`Asc`/`Val`/`Hex`/`String`.
    `Len`/`LenB`, dreiargumentiges `Mid` und ASCII-`Chr` existieren. `ProcedureSymbol.IntrinsicKind`
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
    `InStr`, `InStrRev` und zweiargumentiges `Mid` sind über die Intrinsic-Tabelle und
    End-to-End-Tests verdrahtet.
1d. Host- und Kontrollintrinsics — `IIf`/`RGB`, `GetSetting`/`SaveSetting`, `SendKeys`,
    `PopupMenu`, `LoadPicture`, `PropertyChanged`, `TextWidth`/`TextHeight`, `Print` und
    `PaintPicture` — ✅ als headless-fähige Runtime-Verträge;
    echte UI-/Registry-Hostadapter folgen in M8/M9.
1e. `LSet` — die kontextuelle `LSet target = source`-Syntax sowie Managed-Ausführung für feste
    String-Ziele und gleichartige UDT-Werte sind ✅; unterschiedliche UDT-Layouts benötigen
    weiterhin den nativen ABI-/Padding-Vertrag.
1f. Dateisystem-Pfad-Intrinsics — `FileCopy`, `MkDir`, `RmDir`, `ChDir`, `CurDir`, `GetAttr`,
    `SetAttr` und `FileDateTime` sind ✅ über Symboltabelle, IR, Managed-Emitter und Runtime
    verdrahtet und durch direkte Runtime- sowie generierte Programmtests abgesichert.
1g. `Name oldPath As newPath` — Datei- und Verzeichnisumbenennung ist ✅ als eigene Syntax und
    Managed-Runtime-Operation implementiert und generiert keine untypisierten Restaufrufe.
1h. `Dir`-Attribute — die Fortsetzungsabfrage berücksichtigt ✅ `vbDirectory`, `vbHidden`,
    `vbSystem` und `vbVolume` (ohne portable Volume-Labels) und liefert Dateien sowie
    Verzeichnisse passend zum angeforderten Filter.
2. Datei-I/O — `Open For Binary/Input/Output/Append`, `Get`, `Put`, `Print`, `Input`, `Seek`, `LOF`,
   `FreeFile`, `Close` ✅ für die numerischen Binärformen, skalare UDT-Records sowie skalare und feste
   String-Arrayfelder mit `String * n` und grundlegende
   Textzeilen: Lexer, Syntax, Parser, Runtime, Bindung und Emission stehen, und E2E-Tests schreiben
   und lesen echte Dateien. Variable `String`-Transfers, `Line Input`, grundlegende Stringfelder und
   typisierte numerische, Boolean- und Currency-Ziele für `Input #` sowie skalare Random-Records mit
   `Len`-Klausel und Defaultlänge 128 sind ergänzt; dynamische UDT-Arraymember in Records tragen
   ihren Descriptor und werden elementweise übertragen, eigenständige Arrays unterstützter UDT-
   Elemente übertragen ihre Payload ohne äußeren Descriptor, variable Stringfelder tragen ihr
   2-Byte-Längenpräfix, und Date-Ziele werden bei `Input #` in OLE-Automation-Doubles konvertiert.
   Weitere zusammengesetzte Random-Record-Layouts bleiben offen.
3. `MsgBox`/`InputBox` als hostfähige Verträge ✅; `MsgBox` liefert deterministische Buttonwerte und
   `InputBox` im headless Runtime-Profil den Defaultwert
4. Math: `Abs`, `Sgn`, `Fix`, `Round`, `Sqr`, `Exp`, `Log`, `Sin`, `Cos`, `Tan` und `Atn` sind als
   Scalar-Slice ergänzt, einschließlich `Null`-/`Empty`-Semantik für `Abs`, `Fix` und `Round`; `LongPtr`
   ist als native-width Integer inklusive Pointerarithmetik und `CLngPtr` ergänzt; weitere
   Funktionen und vollständige Variant-Promotion bleiben offen. `Like`/`Option Compare` sind
   für den aktuellen String-/Variant-Subset implementiert.
5. [~] `Format$` — deterministische numerische Masken, Standardnamen, gängige Datums-/Zeit-Token
   und `<`/`>`-Stringmasken sind ergänzt; `w`/`ww`/`q` berücksichtigen nun die übergebenen
   `FirstDayOfWeek`-/`FirstWeekOfYear`-Regeln. Locale-Auswahl, weitere String-Platzhalter und
   Finanzfunktionen bleiben offen und sind im Korpus unbenutzt

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
      rohe Pointer und nicht unterstützte SAFEARRAY-Elemente sowie der native LLVM-Pfad bleiben offen.
      Diese drei ABI-Lücken bleiben bewusst als Diagnose stehen, solange kein Korpusbeleg sie
      fordert — die Arbeit geht zuerst in die fünf tatsächlich verwendeten Controltypen (siehe M9).
      Wenn eine historische `Reference=`-/`Object=`-Zeile nur den Dateinamen trägt, versucht der
      Managed-Importer zusätzlich die registrierten `HKCR\TypeLib`-/`HKCR\CLSID`-Pfade in der
      passenden Version, LCID und Prozessbitness aufzulösen.
      Der Managed/.NET-Konsum wird vor dem nativen LLVM-Backend vervollständigt
- [~] eigener COM-Server-/ClassFactory-/IUnknown-Vertrag für emittierte VB6-Klassen — `--com-host` versieht emittierte Klassen mit stabilen CLSIDs, `ProgID`, `ComVisible` und Automation-Metadaten und erzeugt für Bibliotheken einen nativen .NET-`comhost.dll`. `DllGetClassObject`/`IClassFactory`/`IDispatch`-Aktivierung ist regressionsgesichert; die CLI kann den erzeugten Host über `--register-com`/`--unregister-com` mit dem passenden x86/x64-`regsvr32` installieren oder entfernen. Reg-Free-Manifest-/Typbibliotheks-Emission und der vollständige eigene Raw-`IUnknown`-/`IDispatch`-Vertrag bleiben offen
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
- [x] MSBuild SDK-Grundvertrag — `VB6Project`, `VB6CompilerPath` und `CompileVB6Project`-Target; NuGet-Packaging und inkrementelle Input-/Output-Verfolgung sind mit `VB6.Compiler.Sdk.1.0.0.nupkg` verifiziert
- [~] MSBuild-SDK für VB6-Projektgruppen — `VB6ProjectGroup` verfolgt `.vbg`-, `.vbp`-, Quell- und Designerinputs, ruft die vorhandene CLI-Gruppenemission auf und verwendet einen eigenen inkrementellen Compile-Stempel; `.vbg`-Analysen diagnostizieren jetzt auch projektbezogene `Reference=`-Einträge, die nicht als `Project=` deklariert sind (`VB6VBG0008`), bevor unvollständige Artefakte entstehen; vollständige Visual-Studio-Projektmodellintegration und Design-Time-Build-Verträge bleiben offen
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
danach, unbelegte Konstrukte gar nicht.

- [x] **`Paint`-Event und `AutoRedraw`-Semantik** — `Paint` ist für Designer-Controls
      (einschließlich Control-Array-Index), Forms und UserControls verdrahtet und wird wie in VB6
      nur bei abgeschaltetem `AutoRedraw` ausgelöst. `BeginDrawing` entscheidet pro
      Zeichenoperation über das Ziel: innerhalb eines `Paint`-Handlers dessen Zeichenkontext, bei
      `AutoRedraw` die persistente Fläche, sonst direkt die sichtbare Fläche. Das Abschalten von
      `AutoRedraw` verwirft die Bitmap. Offen bleibt `Cls` als eigene Operation.
- [~] `.frm`/`.frx` parsen; die Designer-Hülle wird mit verschachtelten Controls, Eigenschaften,
      `BeginProperty`-Blöcken und hexadezimalen `.frx`-Ressourcenoffsets erfasst. Intrinsische
      Designer-Controltypen (u. a. `CommandButton`, `TextBox`, `Frame`, `PictureBox`, `Image`,
      `Label`, `Shape`, `Line`, `Timer` und `Menu`) werden als typisierte Klassenfelder gebunden;
      skalare Designerwerte für Controls und das Root-Form (einschließlich Fensterrahmen,
      ControlBox, Min-/Max-Button, Taskbar, Startposition und WindowState) werden nach der
      Erzeugung über den Host gesetzt; `TextRTF`
      kann seine Nutzdaten aus `.frx` beziehen. Vollständige Ressourcendekodierung und WinForms-
      Erzeugung bleiben offen.
- [~] Forms-Runtime auf WinForms: Der portable `IVB6Host`-Vertrag deckt Message-Pump, Form-Lifecycle,
      dynamischen Member-/Control-Dispatch, Control-Erzeugung und Enumeration ab; `VB6.Runtime.WinForms`
      mappt Standardcontrols, Twips, OLE-Farben und Fonts und regressionstestet `Load`/`Unload`/`Show`.
      Automatische Designer-Registrierung, vollständiges Event-Mapping und die vollständige OCX-
      Komposition bleiben offen; der geprüfte native OCX-Pfad ist separat regressiongesichert.
- [~] **Control-Arrays** — Designer-`Index`-Eigenschaften und wiederholte Controlnamen werden
      als typisierte VB6-Arrays gebunden und im generierten Form-Konstruktor als Host-Controls
      initialisiert; die vollständige Laufzeit-/WinForms-Nachbildung bleibt offen.
- [~] Zeichnen auf Form/PictureBox — persistentes `GraphicsLine`-Rendering auf der aktiven
      Formoberfläche mit Twips-/Pixel-Skalierung und Linien-/Rechteckfüllung steht; ein unterstütztes
      `PaintPicture`-Subset zeichnet `Bitmap`-/FRX-/`VBPicture`-Quellen persistent mit; qualifizierte
      `PictureBox.PaintPicture`- und `PictureBox.Line`-Aufrufe lösen ihr eigenes Ziel auf.
      `ScaleMode` ist vollständig: Twip, Point, Pixel, Character, Inch, Millimeter und Zentimeter
      rechnen exakt und pro Achse — Character ist mit 120 zu 240 Twips die einzige asymmetrische
      Einheit. `User` (0) bleibt Twips, bis ein eigener Maßstab über `ScaleWidth`/`ScaleHeight`
      existiert; ein Wert außerhalb 0–7 meldet wie in VB6 Fehler 380. `AutoRedraw` gehört zum
      `Paint`-Punkt oben.
- [ ] **`DrawMode` — zurückgestellt, mangels Korpusbeleg.** Die Rasteroperationen (`Xor Pen`,
      `Invert` und die übrigen 14) kommen in den 40 Quellen nicht vor; die drei früheren Treffer
      der Messung waren ein gleichnamiges Enum, ein Kommentar und ein `SetROP2`-P/Invoke-Parameter.
      Wie MDI erst bauen, wenn ein Korpusprojekt es fordert.
- [ ] **MDI — zurückgestellt, mangels Korpusbeleg.** Weder `MDIForm` noch `MDIChild` kommt in den
      40 VISIA-Quellen vor. Der Punkt bleibt für die VB6-Vollständigkeit stehen, wird aber erst
      angefasst, wenn ein Korpusprojekt ihn fordert.
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
      **Priorität sind die fünf im Korpus tatsächlich verwendeten Typen**: `MSComDlg.CommonDialog`
      (4 Instanzen), `MSComctlLib.ImageList` (3), `RichTextLib.RichTextBox` (2),
      `MSComctlLib.TreeView` (2), `MSComctlLib.ImageCombo` (2) — alle fünf haben bereits einen
      managed Late-Binding-Vertrag, offen sind vor allem die vollständigen Event-Signaturen
      (belegt: `NodeClick`). Dabei gilt: **nativer `AxHost`-Pfad und managed Adapter müssen
      dieselbe Signatur liefern.** Der native Pfad ist an registrierte 32-Bit-OCX gebunden und
      wird über `VB6_REQUIRE_NATIVE_OCX=1` erzwungen; ohne Registrierung — etwa auf einem
      CI-Runner — muss der managed Pfad grün bleiben.

## Meilenstein 10 — IDE

**Auf Eis gelegt, wird nicht weitergetrieben** — bewusst nach dem Compiler-Kern eingeordnet.

Der erste LSP-Slice für Visual Studio steht: JSON-RPC, Initialize, Dokument-Synchronisation,
Lexer-/Parser-/Semantik-Diagnosen und leere Completion-/Symbol-/Definition-Antworten. Als Nächstes
folgen echte Symbolsuche, Completion, Go-to-definition und Buildintegration. Danach eigenständige IDE-/WinForms-Designer-Funktionen mit verlustfreiem
`.frm`-Roundtrip und Debugger. Diese Schicht ist bewusst nach dem Compiler-Kern eingeordnet.

---

## Zusätzlich, klein und unabhängig

1. [x] `Debug.Print` auf VB6-nahe Formatierung (führendes Vorzeichen-Leerzeichen, 15
   signifikante Stellen für Gleitkomma-/Currencywerte und vollständige Decimal-Präzision);
   die E2E-Helfer trimmen weiterhin bewusst Plattform-/Spaltenformat
2. Typisierte Vergleiche direkt emittieren statt `VBOperators.Equal(object?, object?)` — der
   Binder hat beide Seiten bereits angeglichen
3. `Currency + Double` folgt nun der VB6-Promotionsreihenfolge und liefert `Double`, während
   `Currency * Double` die separate Multiplikationsreihenfolge beibehält und `Currency` liefert;
   Vergleichspromotionen behalten weiterhin die separate Currency-Präzisionsregel
4. `Debug.Print` formatiert Zahlen invariant und mit VB6-nahem Vorzeichen-/Signifikanzformat
   unverändert unter Punkt 1
5. [x] `Debug.Assert` wird als kompiliertes VB6-Statement akzeptiert und im Managed-Emit
   vollständig elidiert.
