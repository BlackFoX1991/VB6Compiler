# Roadmap

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
| M3 UDT-Typraum / Scope-Bindung | **2034** | 1214 | 68 | 752 | 0 von 27 |
| M3 UDT-Werte, `With`, `For Each`; M4-Grundlage | **1339** | 480 | 62 | 797 | 0 von 27 |
| M4 untypisierte Functions | **1473** | 466 | 62 | 945 | 0 von 27 |
| M5 ByRef-Randfälle vorgezogen | **1064** | 466 | 62 | 536 | 0 von 27 |
| ReDim-Recovery bei qualifizierten Zielen | **1052** | 454 | 62 | 536 | 0 von 27 |
| M7 Datei-I/O-Syntax vorgezogen | **832** | 218 | 0 | 614 | 0 von 27 |
| M7 Datei-I/O-Runtime (numerisch) | **822** | 218 | 0 | 604 | 0 von 27 |
| `TypeOf ... Is`-Syntax | **726** | 110 | 0 | 604 | 0 von 27 |
| Aufrufseitiges `ByVal` | **724** | 71 | 0 | 641 | 0 von 27 |
| Intrinsics umgebaut, Konvertierungen ergänzt | **717** | 71 | 0 | 634 | 0 von 27 |
| String-Funktionen | **692** | 71 | 0 | 609 | 0 von 27 |
| M6 Kontrollfluss-Syntax vorgezogen | **752** | 37 | 0 | 703 | 0 von 27 |
| Qualifizierte Aufrufe | **784** | 12 | 0 | 760 | 0 von 27 |
| Sichtbare Deklarationen aus fehlerhaften Modulen | **489** | 12 | 0 | 465 | 0 von 27 |
| Sichtbare Typen aus fehlerhaften Modulen | **416** | 12 | 0 | 392 | **1 von 27** |
| Qualifiziertes `ReDim`, `UBound` auf Ausdrücken, dynamische UDT-Member | **459** | 12 | 0 | 447 | 1 von 27 |
| M5 `Optional`-Aufrufsemantik vorgezogen | **367** | 12 | 0 | 355 | **3 von 27** |
| Datei-Funktionen, nackte Funktionsnamen | **322** | 12 | 0 | 310 | **4 von 27** |
| Backend-Cutover auf direkte Managed-Emission | **304** | 12 | 0 | 292 | **5 von 27** |
| Klassenquellen, Property/Event-Grundlage | **377** | 12 | 0 | 365 | **5 von 30** |
| Klassen/Form-/Control-Analyse, Variant-/Objektverträge, Fehlerdispatcher und String-I/O | **779** | **0** | **0** | **779** | **21 von 40** |
| Standard-VB-Konstanten und Host-unabhängige Numeric-Verträge | **680** | **0** | **0** | **680** | **22 von 40** |
| Standardbibliotheks- und hostfähige Interaktionsverträge | **515** | **0** | **0** | **515** | **22 von 40** |
| `Call`-qualifizierte Objektaufrufe | **335** | **0** | **0** | **335** | **22 von 40** |
| Modulbezogene UDT-Scope-Auflösung in Klassen/Forms/Controls | **289** | **0** | **0** | **289** | **22 von 40** |
| Kontextuelle `Set`-Zuweisung auf indizierte Member | **286** | **0** | **0** | **286** | **22 von 40** |
| `Command`-/`StrPtr`-Standardverträge | **278** | **0** | **0** | **278** | **22 von 40** |
| Implizite UserControl-Host-Intrinsics | **276** | **0** | **0** | **276** | **22 von 40** |
| Kontextuelle `LSet`-Zuweisungssyntax | **272** | **0** | **0** | **272** | **23 von 40** |
| Variant-Guard für boolesche Vergleichsoperatoren | **268** | **0** | **0** | **268** | **23 von 40** |
| Modulbezogener Designer-Control-Scope | **221** | **0** | **0** | **221** | **27 von 40** |
| Implizite Form-/UserControl-Host-Properties | **205** | **0** | **0** | **205** | **27 von 40** |
| Qualifizierte Enum-Memberauflösung | **258** | **0** | **0** | **258** | **23 von 40** |
| Modulkonstanten mit projektweiten Enum-Symbolen | **202** | **0** | **0** | **202** | **27 von 40** |
| Externe VB6-/Win32-Konstantenverträge | **172** | **0** | **0** | **172** | **27 von 40** |
| `Erl`- und `Clipboard.GetText`-Hostverträge | **169** | **0** | **0** | **169** | **27 von 40** |
| Externe Control-/COM-Typaliase und TreeView-Node-Vertrag | **134** | **0** | **0** | **134** | **27 von 40** |
| Graphics-`Line`-Runtimevertrag | **120** | **0** | **0** | **120** | **27 von 40** |
| Verschachtelte Label-/`GoTo`-Auflösung | **80** | **0** | **0** | **80** | **31 von 40** |
| `End`-Prozessbeendigungsvertrag | **77** | **0** | **0** | **77** | **31 von 40** |
| Whitespace-/Variant-Auflösung qualifizierter Member-Aufrufe | **73** | **0** | **0** | **73** | **31 von 40** |
| `Erase` auf UDT-Memberarrays | **71** | **0** | **0** | **71** | **31 von 40** |
| ByRef-Konstanten als typisierte Temporaries | **65** | **0** | **0** | **65** | **33 von 40** |
| Identifier-Typensuffixe in Bindung und impliziten Variablen | **55** | **0** | **0** | **55** | **34 von 40** |
| Statement-Aufrufe von Functions mit verworfenem Rückgabewert | **50** | **0** | **0** | **50** | **34 von 40** |
| Standardbibliotheks- und Host-Intrinsics (`Val`, `Hex`, `String`, `Input`, `TextHeight`, `Print`, `PaintPicture`) | **43** | **0** | **0** | **43** | **34 von 40** |
| Standardtypen `Picture`-/`Screen`-Properties | **36** | **0** | **0** | **36** | **34 von 40** |
| Case-insensitive Standard-Property-Bindung für UserControl-Hosts | **20** | **0** | **0** | **20** | **34 von 40** |
| `As New`-Klassendeklaratoren | **16** | **0** | **0** | **16** | **34 von 40** |
| `Err.Source`-Runtimevertrag | **15** | **0** | **0** | **15** | **34 von 40** |
| `For Each` über Host-/Control-Sammlungen | **9** | **0** | **0** | **9** | **36 von 40** |
| Klassen-Property-Targets in `With`-Blöcken | **6** | **0** | **0** | **6** | **37 von 40** |
| `LBound`/`UBound` mit leeren Arrayklammern | **3** | **0** | **0** | **3** | **38 von 40** |
| RichTextBox-Dateityp-Konstanten (`rtfRTF`, `rtfText`) | **2** | **0** | **0** | **2** | **38 von 40** |
| `Format`/`Format$`-Subset für deterministische Zahlen-, Datums-/Zeit- und Stringmasken | **2** | **0** | **0** | **2** | **38 von 40** |
| Skalare Date-Part-/Timer-Intrinsics | **2** | **0** | **0** | **2** | **38 von 40** |
| DateSerial/TimeSerial sowie DateAdd/DateDiff-Intervalle inklusive `w`/`ww` | **2** | **0** | **0** | **2** | **38 von 40** |
| `DatePart` mit Kalender-, Zeit- und Wochenanteilen | **2** | **0** | **0** | **2** | **38 von 40** |
| `Weekday`/`WeekdayName`/`MonthName` | **2** | **0** | **0** | **2** | **38 von 40** |
| Variant-Date-Arithmetik mit Date-Subtype-Erhalt | **2** | **0** | **0** | **2** | **38 von 40** |
| `DateValue`/`TimeValue`-Normalisierung | **2** | **0** | **0** | **2** | **38 von 40** |
| Skalare Mathematik-Intrinsics `Exp`/`Log`/`Sin`/`Cos`/`Tan`/`Atn` | **2** | **0** | **0** | **2** | **38 von 40** |
| Variant-Mathematik mit `Null`-/`Empty`-Semantik | **2** | **0** | **0** | **2** | **38 von 40** |
| Decimal-Promotion bei Variant-Vergleichen | **2** | **0** | **0** | **2** | **38 von 40** |

Die aktuelle Zeile ist der neue Messpunkt: alle 40 `.bas`, `.cls`, `.frm` und `.ctl`-Quellen werden
gelesen, Designer-Metadaten werden offsettreu ausgeblendet, typisiert und gebunden. `Property
Get/Let/Set`, Events, `WithEvents`, `New`, `Set`, `TypeOf`, Variant-Arrays, Standard-Collection,
late-bound Object-/Control-Mitglieder sowie `On Error` mit `Err` und `Resume Next` sind als
Compiler-Kern vorhanden. Managed-Klasseninstanzen besitzen jetzt eigenen Feldspeicher, Konstruktor-
und Terminator-Lifecycle, Property-Dispatch, `RaiseEvent`/`WithEvents`-Emission sowie echte
Referenzidentitaet. `Implements`-Vertraege werden als CLR-Interfaces emittiert und ueber
`callvirt` inklusive Property-Accessors dispatcht. COM-Identitaet/Dispatch, native ABI-Emission
und viele Parser-/UDT-/Forms-Faelle stehen noch aus.
VISIA bleibt dabei ein Regressionstest- und Messkorpus, nicht das fachliche Portierungsziel.

### Aktueller Compiler-Kern-Nachtrag

Seit dem Messpunkt sind mehrere bisher offene, backendunabhängige Kernpfade implementiert und
regressionsgesichert: `Like` mit `Option Compare Binary/Text` (Wildcard-, Zeichenlisten- und
Bereichsmuster), `Is` für Variant-/Hostobjektreferenzidentität, variable String-Transfers bei
binärem `Get`/`Put` mit Zwei-Byte-Längenpräfix sowie `Debug.Print` mit VB6-naher numerischer
Formatierung. `InStr`, `InStrRev`, zweiargumentiges `Mid`, `MsgBox`/`InputBox` als hostfähige headless
Vertrag und der mathematische Kern `Abs`/`Sgn`/`Fix`/`Round`/`Sqr` sind ebenfalls über Symbol,
IR, Managed-Emitter und Runtime verdrahtet. Skalare `Declare`-Signaturen werden als echte
Managed-P/Invoke-Methoden mit `Lib`/`Alias`-Importmetadaten emittiert. ANSI-String-Marshalling in
`Declare` ist über `CharSetAnsi` und echte Windows-E2E-Aufrufe abgedeckt. Skalare UDT-Records werden
bei binärem `Get`/`Put` feldweise in Deklarationsreihenfolge übertragen; skalare feste `String * n`-
UDT-Felder werden ohne Descriptor mit exakt ihrer Bytebreite geschrieben; feste UDT-Arrayfelder mit
skalaren oder verschachtelten nicht-rekursiven Elementen sowie skalare Random-Records respektieren
`Len`, Recordgrenzen und die Defaultlänge 128. Der Managed-Fixed-String-Pfad verwendet aktuell eine
deterministische Latin-1-Abbildung; hostabhängige ANSI-Codepages und nicht unterstützte
zusammengesetzte Layouts bleiben offen. Eigenständige Arrays von unterstützten UDT-Elementen
übertragen außerhalb eines UDT nur ihre elementweise Payload ohne äußeren Descriptor. Dynamische
UDT-Arraymember mit unterstützten nicht-rekursiven Elementtypen laufen sowohl im Managed-Wertepfad
als auch in UDT-Dateirecords über den `2 + 8 * Dimensionen`-Descriptor und elementweise Payload.
`Len(udt)` verwendet bei emittierten `VB6.Generated`-Records nun den tatsächlichen nativen
4-Byte-gepackten Struct-Umfang, einschließlich `String * n`-Feldern; nicht repräsentierbare
benutzerdefinierte CLR-Structs werden weiterhin nicht implizit als VB6-UDT akzeptiert.
Date-Werte werden als OLE-Automation-Doubles übertragen und bei `Input #` konvertiert; beim
Ablegen typisierter Date-Werte in Variant bleibt der Date-Subtype `VarType = 7` erhalten.
`For ... Next` akzeptiert jetzt alle numerischen Zählerformen des Sprachvertrags: `Byte`,
`Integer`, `Long`, `LongLong`, `Single`, `Double`, `Currency` und `Date`. Default-`Step`-Werte,
Richtungstests und die Date-OLE-Darstellung laufen dabei typisiert durch Binder, IR und Managed-
Emitter. Scalar-
Pointer-Transfers für `Declare ... As Any` inklusive `ByVal VarPtr(...)` sind über `IntPtr`
abgedeckt. Die semantisch vorhandene Standard-`Collection` besitzt jetzt ebenfalls eine echte
Managed-Runtime: `New Collection`, one-based und schlüsselbasierter `Item`-Zugriff, `Count`,
`Add` mit `Key`/`Before`, `Remove` sowie `For Each` in Einfügereihenfolge laufen über eigene
IR-Runtime-IDs und werden im Managed-Emitter typkorrekt auf `VBCollection` abgebildet. `For Each`
arbeitet dabei mit einem Variant-Snapshot, sodass leere Collections und `Exit For` denselben
kontrollflussstabilen Pfad wie Array-Iteration verwenden. Weitere zusammengesetzte String-/Random-Layouts,
komplexes `As Any`-Marshalling, COM, native LLVM-Emission und Forms bleiben bewusst offen.
Late-bound `Variant`-/`Object`-Memberzugriffe sind jetzt ebenfalls als eigener Runtime-Vertrag
verdrahtet: Property-Get/Let/Set und Methoden werden auf erzeugten Klassen über `__vb6_`-Reflection
aufgelöst, während gewöhnliche CLR-Properties als Host-Fallback bestehen bleiben. Dadurch fallen
17 bisherige `VB6S0047`-Diagnosen weg; der Messpunkt sinkt auf 275 Fehler und 21 von 40 fehlerfreie
Dateien. Vollständige COM-/IDispatch-Identität, ByRef-Writeback und Host-ABI-Regeln bleiben offen.
Der Managed-Kern fuer Klassen, Ereignisse und den erweiterten Kontrollfluss
ist inzwischen regressionsgesichert.

Seit dem letzten Messpunkt verarbeitet der Parser führende Punktaufrufe in `With`-Blöcken auch
ohne Argumentliste, etwa `.ShowOpen` oder `.Cls`. Außerdem überspringt `Select Case` Leer- und
Kommentarzeilen vor dem ersten `Case`, wie sie im realen VB6-Quelltext häufig vorkommen. Die
beiden Syntax-Slices sind mit Parser-Regressionstests abgesichert; dadurch sinkt der VISIA-
Messpunkt auf **205 Gesamtfehler**, davon **124 Parser**, **0 Lexer** und **81 Semantik**. Die
Zahl der fehlerfrei analysierten Dateien bleibt bei **21 von 40**, weil die verbleibenden Blocker
überwiegend in Binder- und Objektverträgen liegen.

Zusätzlich akzeptiert und bindet der Compiler nun procedure-level `Const`-Deklarationen mit
explizitem oder inferiertem Typ. Ihre Werte werden als lokale Initializer in den Managed-IR-Prolog
überführt, sodass reale Muster wie `Const ProcName = "..."` nicht nur syntaktisch, sondern auch
bei der Ausführung korrekt bleiben. Damit fällt der Parserzähler auf **104** und der Gesamtstand
auf **185** Fehler bei weiterhin **21 von 40** fehlerfreien Dateien.

Der Aufrufparser akzeptiert nun auch `Foo(arg1, arg2)` ohne das optionale `Call` sowie
qualifizierte Aufrufe wie `object.Method(value)`. Die VB6-Unterscheidung zu `Foo (value)` bleibt
erhalten: ein Leerzeichen vor der Klammer markiert weiterhin den ByVal-Ausdruck. Damit sinkt die
Parserdiagnostik auf **99** und der Gesamtstand auf **180** Fehler; die acht neuen Regressionstests
heben die Suite auf **652 Tests**.

Qualifizierte Deklarationstypen wie `MSComctlLib.Node` werden jetzt als vollständige Typnamen im
Syntaxbaum erhalten, statt nach dem ersten Identifier abzubrechen. Das gilt für Parameter,
Variablen, Konstanten, Rückgabetypen, `Declare`, `TypeOf`, `New`, `ReDim` und UDT-Felder; der
Binder verwendet für Meldungen und Auflösung ebenfalls den vollständigen Namen. Zwei Parser-
Regressionstests sichern Tokenfolge und Text ab. Der VISIA-Stand sinkt dadurch auf **170
Gesamtfehler**, davon **89 Parser**, **0 Lexer** und **81 Semantik**, bei weiterhin **21 von 40**
fehlerfreien Dateien; die Suite umfasst **654 Tests**.

`AddressOf ProcedureName` wird nun als eigener Ausdruck bis zur semantischen Grenze erhalten. Der
Binder meldet den noch offenen Callback-/Funktionszeigervertrag explizit, statt die nachfolgenden
Argumente als Parserfehler zu behandeln. Dadurch sinkt der VISIA-Parserstand auf **84** und der
Gesamtstand auf **167** Fehler bei weiterhin **21 von 40** fehlerfreien Dateien; die Suite umfasst
**655 Tests**.

Deklaratoren mit `As New TypeName` werden nun vollständig als Deklaratorsyntax erhalten, inklusive
des kontextuellen `New`-Tokens und qualifizierten Typnamens. Damit verschwinden die vier
Kaskadenfehler aus den betroffenen VISIA-Quellen; die implizite Objektinstanziierung bleibt als
separater Semantikbaustein offen. Der Stand sinkt auf **163 Gesamtfehler**, davon **80 Parser**,
**0 Lexer** und **83 Semantik**, bei weiterhin **21 von 40** fehlerfreien Dateien. Die Suite umfasst
**656 Tests**.

Der nächste Syntaxabschluss verarbeitet nun die restliche VISIA-Parseroberfläche: numerische `!`-/`#`-
Suffixe, Grafik-`Line` mit Koordinaten, `On Local Error`, `End` in Einzeilenbedingungen, Array-
Rückgabetypen, `#Const`, `Erase .Member`, Graphics-`Print` sowie kommentierte Fortsetzungszeilen.
Damit analysiert der Parser alle 40 VISIA-Projektdateien ohne Parserdiagnose. Die Semantikzahl steigt
bewusst auf **779**, weil zuvor versteckte Bindungs- und Objektmodelllücken jetzt sichtbar werden; die
fehlerfreien Dateien bleiben bei **21 von 40**. Die nächste Arbeit liegt daher im Binder-/Runtime-
Vertrag für Controls, Late Binding, Standardbibliothek und COM, nicht mehr in der VISIA-Syntax-
Wiederherstellung.

Darauf baut der Standardkonstanten-Slice auf: Der Compiler stellt nun die numerischen VB6-Konstanten
für Farben, Dialogschaltflächen, Variant-Typcodes, Cursor, Fensterzustände, Tastaturmasken,
Picture-Typen und grundlegende Grafik-/Dateiverträge bereit. Sie werden wie die vorhandenen
Stringkonstanten projektweit sichtbar, bleiben typisiert und werden weiterhin von gleichnamigen
Benutzerdeklarationen überschattet. Dadurch sinkt der VISIA-Stand auf **680 semantische Fehler**;
**22 von 40** Dateien analysieren fehlerfrei. Controls, `PropertyChanged`, `IIf`, `RGB`,
`PropertyBag` und COM-/Forms-Objektmodelle bleiben getrennte Compilerkern-Slices.

Der anschließende Standardbibliotheks-Slice verdrahtet `IIf` und `RGB` backendunabhängig durch
Binder, IR, Managed-Emitter und Runtime. `IIf` behält die VB6-eager Auswertung beider Wertzweige;
`RGB` erzeugt geklemmte Windows-`OLE_COLOR`-Werte. `GetSetting`/`SaveSetting` besitzen einen
deterministischen, case-insensitiven Prozessspeicher für headless Hosts; `SendKeys`, `PopupMenu`
und `PropertyChanged` sind explizite hostfähige No-op-Verträge. `LoadPicture` liefert einen
hostneutralen Picture-Platzhalter. `Screen`, `Ambient`, `Picture`, `Font` und `PropertyBag`
stehen als typisierte Standardobjekte im Typraum; `PropertyBag` kann Werte über einen
case-insensitiven Runtime-Speicher lesen und schreiben. Der VISIA-Stand sinkt damit auf **515
semantische Fehler**, weiterhin **22 von 40** fehlerfreie Dateien. Controls, COM-Dispatch,
Forms-Lifecycle und komplexes Host-Marshalling bleiben die nächsten separaten Blöcke.

Der anschließende Parserabschluss behandelt `Call receiver.Member(...)` und die VB6-Variante ohne
Klammern als denselben qualifizierten Aufrufpfad. Zuvor wurde `Call PropBag.ReadProperty(...)`
fälschlich als globaler Aufruf der Prozedur `PropBag` zerlegt; außerdem verloren führende
Punktzugriffe in verschachtelten `With`-Blöcken ihren Empfängerkontext. Der korrigierte Parserpfad
bewahrt den vollständigen Empfänger und bindet die vorhandene Late-Binding-/PropertyBag-Semantik.
Damit verschwinden **180** semantische Kaskadenfehler; der VISIA-Stand beträgt **335 semantische
Fehler**, bei weiterhin **22 von 40** fehlerfreien Dateien.

Die UDT-Auflösung wird nun auch beim Aufbau von Member- und Prozedursymbolen für Klassen, Forms und
UserControls unter dem jeweiligen Modul-Scope ausgeführt. Zuvor wurden private Typen wie `POINTAPI`
bei der frühen Signaturerzeugung als unbekannt markiert; dadurch zerfielen dynamische UDT-Arrays und
`With`-Zugriffe in Folgefehler. Der Scope-Fix entfernt **46** semantische Kaskaden, darunter alle
`DstPoint`-/`POINTAPI`-Fehler in `GpTabs.ctl`. Der aktuelle VISIA-Stand beträgt **289 semantische
Fehler**, weiterhin **22 von 40** fehlerfreien Dateien.

Die kontextuelle `Set`-Erkennung scannt nun auch indizierte Empfänger wie
`Set m_ButtonItem(index).TB_Icon = value` bis zum Gleichheitszeichen. Zuvor wurde diese Form als
Aufruf einer nicht vorhandenen Prozedur `Set` klassifiziert. Der Parser-Fix entfernt drei weitere
semantische Kaskaden; der VISIA-Stand beträgt **286 semantische Fehler**, weiterhin **22 von 40**
fehlerfreien Dateien.

Die Standardverträge umfassen nun auch `Command()`/`Command$` für headless Hosts sowie `StrPtr()`
als explizit typisierten Native-ABI-Vertrag. `Command` liefert im headless Runtime einen stabilen
leeren Wert; `StrPtr` bleibt bis zum nativen Backend bewusst geschützt. Der VISIA-Stand sinkt damit
um **8** auf **278 semantische Fehler**, weiterhin **22 von 40** fehlerfreien Dateien.

`ScaleX`, `ScaleY` und `TextWidth` werden nun als implizite Host-Intrinsics nur für Form- und
UserControl-Module ergänzt. Der headless Runtime-Vertrag nutzt Identitätsskalierung und eine
deterministische Zeichenbreiten-Näherung; ein UI-Host kann diese Verträge später ersetzen. Der
VISIA-Stand sinkt um **2** auf **276 semantische Fehler**, weiterhin **22 von 40** fehlerfreien
Dateien.

Die Parserbehandlung von `LSet target = source` nutzt nun die tatsächliche VB6-Zuweisungsschreibweise
und führt sie mit zwei Argumenten in den bestehenden `LSet`-Vertrag. Dadurch verschwinden die vier
Arity-Kaskaden in `comMath.bas`; der VISIA-Stand sinkt auf **272 semantische Fehler**, und **23 von
40** Dateien analysieren fehlerfrei. Der Managed-Pfad bewahrt nun die konkreten Operandentypen und führt
feste String-Ziele sowie gleichartige UDT-Kopien aus; unterschiedliche UDT-Layouts bleiben wegen ihrer nativen
Feld-/Paddingsemantik bis zum nativen Backend bewusst separat offen.

Der Variant-Operations-Guard akzeptiert nun auch `Not` und boolesche logische Operatoren über
Vergleichsergebnisse mit Variant-/Objektursprung. Diese Ausdrücke werden bereits typkorrekt als
`NotBoolean` beziehungsweise boolesche Logik gelowert und waren zuvor nur von einer zu engen
Diagnosebedingung blockiert. Der VISIA-Stand sinkt auf **268 semantische Fehler**, bei weiterhin
**23 von 40** fehlerfreien Dateien.

Qualifizierte Enum-Ausdrücke wie `eMsgWhen.MSG_BEFORE` werden nun als Long-Konstanten aus dem
jeweiligen Enum-Scope gebunden. Damit bleiben auch private Modul-Enums in Forms und UserControls
für ihre qualifizierten Memberzugriffe sichtbar; der VISIA-Stand sinkt um **10** auf **258
semantische Fehler**, bei weiterhin **23 von 40** fehlerfreien Dateien.

Designer-Controls werden nun nur noch im eigenen Form- oder UserControl-Modul als implizite
Mitglieder gebunden. Dadurch überschattet ein Control wie `frmMain.Code` nicht mehr das gleichnamige
öffentliche Enum-Mitglied `ENUM_SECTION_TYPE.Code` in Standardmodulen. Der VISIA-Stand sinkt um
**37** auf **221 semantische Fehler**, und die Zahl der fehlerfreien Dateien steigt auf **27 von 40**.

Form- und UserControl-Module binden nun auch bare Host-Properties wie Height, ScaleWidth, hWnd,
CurrentX und FillStyle gegen Me. Der bestehende Host-Vertrag wird damit nicht nur für ScaleX,
ScaleY und TextWidth, sondern auch für die häufige Property-Syntax sichtbar. Der VISIA-Stand sinkt
um **16** auf **205 semantische Fehler**, bei weiterhin **27 von 40** fehlerfreien Dateien.

Modulvariablen und Konstanten binden ihre Initializer nun gegen bereits bekannte projektweite
Symbole und zuvor deklarierte Modulvariablen. Dadurch können beispielsweise Enum-Member in
typisierten Modulkonstanten verwendet werden, ohne die Duplikatprüfung eigener Deklarationen zu
umgehen. Der VISIA-Stand sinkt um **3** auf **202 semantische Fehler**, bei weiterhin **27 von 40**
fehlerfreien Dateien; die Suite umfasst **682 Tests**.

Der projektweite Konstantenvertrag enthält nun auch die nachgewiesenen externen Werte für
TreeView-Kindknoten, Win32-Rahmenflags, `vbGrayText` und `vbSrcCopy`. Die Konstanten bleiben
typisierte Long-Werte und können weiterhin durch eigene Moduldeklarationen überschattet werden.
Dadurch sinkt der VISIA-Stand um **30** auf **172 semantische Fehler**, bei weiterhin **27 von 40**
fehlerfreien Dateien; die Suite umfasst **683 Tests**.

`Erl` ist nun als nullargumentiger Intrinsic bis in den Managed-Runtime-Vertrag verdrahtet und
`Clipboard.GetText` besitzt einen typisierten Objektvertrag. Ohne weitergereichte Quellzeile
liefert `Erl` im aktuellen Headless-Backend bewusst **0**; die Zeilenverfolgung bei abgefangenen
Fehlern bleibt ein eigener Runtime-Slice. Der VISIA-Stand sinkt um **3** auf **169 semantische
Fehler**, bei weiterhin **27 von 40** fehlerfreien Dateien; die Suite umfasst **685 Tests**.

Die Typauflösung kennt nun die im Korpus verwendeten VB6-Standard- und ActiveX-Controlnamen als
late-bound `Control`-Verträge, Long-basierte Konstantentypen sowie `IPicture`. `MSComctlLib.Node`
ist als eigener Minimalvertrag mit `Key`, `Text` und `Index` modelliert. Damit verschwinden **35**
Typ-/Folgefehler; der VISIA-Stand beträgt **134 semantische Fehler**, weiterhin **27 von 40**
fehlerfreien Dateien, bei **686 Tests**. COM-Identität, echtes OCX-Hosting und vollständige
Memberbibliotheken bleiben bewusst spätere Interop-Slices.

Graphics-`Line`-Anweisungen werden nun semantisch gebunden, nach `Single` konvertiert und über
einen host-neutralen IR-/Managed-Runtimevertrag emittiert. Der Vertrag trägt Farbwert, `Step`
sowie die Box-/Fill-Optionen `B` und `F`; ein UI-Host kann die strukturierte Operation über den
`GraphicsLineSink` übernehmen, während Headless-Läufe ohne Sink deterministisch bleiben. Dadurch
fallen **14** bisherige VISIA-Diagnosen weg: Der Stand sinkt auf **120 semantische Fehler** bei
weiterhin **27 von 40** fehlerfreien Dateien; die Suite umfasst **688 Tests**. Die verbleibenden
Objektmodellfälle wie `End`, `Erase` auf Objektmembern und echte Control-/COM-Methoden bleiben
separate Compiler-/Interop-Slices.

Die Labelauflösung ist nun prozedurweit statt nur auf der äußersten Statementebene aktiv. Dadurch
werden auch Sprünge in Labels innerhalb von `If`, Schleifen und `Select Case` als IR-Basic-Block-
Ziele gebunden und im Managed-Backend ausgeführt. Die 40 bisherigen Label-/`GoTo`-Diagnosen
entfallen; der VISIA-Stand sinkt auf **80 semantische Fehler** bei **31 von 40** fehlerfreien
Dateien.

`End` wird nun als host-neutraler `EndProgram`-Runtimevertrag gebunden und im Managed-Backend
prozessweit beendet. IDE- und Test-Hosts können den Vorgang über `EndProgramSink` übernehmen.
Damit sind die drei verbliebenen `End`-Diagnosen entfernt: Der VISIA-Stand beträgt **77
semantische Fehler** bei weiterhin **31 von 40** fehlerfreien Dateien; die Suite umfasst **691
Tests**.

Qualified Member-Aufrufe bewahren nun auch die VB6-Form mit Leerzeichen vor der Argumentklammer,
ohne die mehrteilige `PSet (X, Y), Farbe`-Schreibweise als Parserfehler zu behandeln. Der Binder
dispatcht Variant-Empfänger bei Statement-Aufrufen über denselben Late-Bound-Vertrag wie
Ausdrucksaufrufe. Damit entfallen vier weitere VISIA-Objektmodellfehler; der Stand sinkt auf
**73 semantische Fehler** bei weiterhin **31 von 40** fehlerfreien Dateien. Die Suite umfasst
nun **694 Tests**.

`Erase .Member` in einem `With`-Block bindet nun über denselben adressierbaren UDT-Memberpfad
wie `ReDim` und Memberzuweisungen. Die IR speichert dynamische Memberarrays über ihr Feld- bzw.
With-Place zurück, statt nur lokale Variablensymbole zu akzeptieren. Damit entfallen die beiden
verbliebenen `VB6S0062`-Diagnosen in `mcToolBar.ctl`; der Stand sinkt auf **71 semantische Fehler**
bei weiterhin **31 von 40** fehlerfreien Dateien. Die Suite umfasst nun **696 Tests**.

Konstanten werden bei ByRef-Aufrufen nun wie Literale als typisierte Temporaries übergeben. Das
entfernt die sechs falschen Typmismatch-Diagnosen für die `EX_*`- und `REG_SZ`-Konstanten in
VISIA; echte beschreibbare ByRef-/Interop-Mismatches bleiben sichtbar. Der Stand sinkt auf
**65 semantische Fehler**, und **33 von 40** Dateien analysieren fehlerfrei. Die Suite umfasst
nun **697 Tests**.

Identifier-Typensuffixe werden nun vom Lexer bis zur Semantik erhalten. `&`, `%`, `$`, `!`, `#`
und `@` typisieren deklarierte und implizite Variablen, Parameter sowie Funktionsrückgaben nach
VB6-Regeln; dadurch werden unter anderem die `lphKey&`-/`ret&`-Pfade in `envAssociation.bas`
korrekt als `Long` gebunden. Der VISIA-Stand sinkt auf **55 semantische Fehler**, bei **34 von
40** fehlerfreien Dateien. Die Suite umfasst nun **699 Tests**.

Statementartige Aufrufe von Klassen-Functions dürfen ihren Rückgabewert nun wie in VB6 verwerfen;
der Aufruf bleibt als ausgewertete IR-Anweisung erhalten, damit Seiteneffekte ausgeführt werden.
Damit verschwinden die fünf `Append`-Diagnosen aus `CodeEdit.ctl`. Der VISIA-Stand sinkt auf
**50 semantische Fehler**, bei weiterhin **34 von 40** fehlerfreien Dateien. Die verbliebene
`String`-zu-`Variant`-ByRef-Diagnose bleibt bewusst sichtbar, weil VB6 bei einem typisierten
ByRef-Argument den exakten Parametertyp verlangt.

Der anschließende Standardbibliotheks- und Host-Slice ergänzt `Val`, `Hex`, die wiederholende
`String`-Funktion, die Ausdrucksform `Input`, sowie `TextHeight`, unqualifiziertes Control-`Print`
und den beobachteten fünfargumentigen `PaintPicture`-Vertrag. Alle sieben Pfade laufen durch
Intrinsic-Symbol, IR, Managed-Emitter und headless Runtime-Tests. Dadurch sinkt der VISIA-Stand
auf **43 semantische Fehler**, weiterhin **34 von 40** fehlerfreien Dateien; die Suite umfasst nun
**705 Tests**. Die verbleibende `String`-zu-`Variant`-ByRef-Diagnose sowie Host-/COM-/Forms-Lücken
bleiben bewusst sichtbar.

Der folgende Host-Object-Slice ergänzt die lesbaren Standardmitglieder `Picture.Width`,
`Picture.Height`, `Picture.Type` sowie `Screen.TwipsPerPixelX` und `Screen.TwipsPerPixelY`.
`LoadPicture` liefert dafür deterministische headless Defaults. Die sieben `VB6S0064`-Diagnosen
entfallen; der VISIA-Stand sinkt auf **36 semantische Fehler**, weiterhin **34 von 40** fehlerfreien
Dateien. Die Suite umfasst nun **706 Tests**.

Die Standard-Property-Schlüssel werden nun case-insensitiv verglichen, wie es VB6 für Namen
verlangt. Dadurch binden unqualifizierte Host-Mitglieder wie `hdc` und `hwnd` an die vorhandenen
`hDC`-/`hWnd`-Verträge, auch unter `Option Explicit`; die 16 entsprechenden `VB6S0001`-Diagnosen
entfallen. Der VISIA-Stand sinkt auf **20 semantische Fehler**, weiterhin **34 von 40** fehlerfreien
Dateien.

`As New`-Deklaratoren werden nun als Objektinitialisierer gebunden und über denselben IR-/Managed-
Konstruktorpfad wie explizites `New` ausgeführt; `Class_Initialize` wird dabei erreicht. Dadurch
entfallen die vier verbliebenen impliziten Objektkonstruktionsdiagnosen aus `clsFont` und `CodeEdit`.
Der VISIA-Stand sinkt auf **16 semantische Fehler**, weiterhin **34 von 40** fehlerfreien Dateien;
die Suite umfasst nun **707 Tests**.

`Err.Source` wird nun als lesbares Standardmitglied gebunden und über IR, Managed-Emitter und
Runtime aufgelöst. Der Fehlerzustand bewahrt dabei auch den explizit an `Err.Raise` übergebenen
Quelltext, statt ihn beim Fehlerhandler durch den CLR-Fehlertyp zu ersetzen. Damit entfällt die
letzte `Err.Source`-Diagnose; der VISIA-Stand sinkt auf **15 semantische Fehler**, weiterhin
**34 von 40** fehlerfreien Dateien. Die Suite umfasst nun **709 Tests**.

`For Each` unterstützt nun auch hostbereitgestellte Form-/UserControl-Sammlungen und late-bound
`Object`-Werte über einen host-neutralen Enumeration-Callback. Objektvariablen sind als Schleifen-
steuerung zulässig, während numerische Array-Steuerungen weiterhin diagnostiziert werden; implizite
Schleifenvariablen ohne `Option Explicit` werden als Variant angelegt. Damit entfallen die sechs
`For Each`-Diagnosen aus `frmDesign.frm` und `envBorders.bas`; der VISIA-Stand sinkt auf **9
semantische Fehler**, bei **36 von 40** fehlerfreien Dateien. Die Suite umfasst nun **711 Tests**.

Klassen-Property-Ergebnisse sind nun ebenfalls gültige `With`-Targets, wenn sie einen Objektwert
liefern. Die IR-Absenkung wertet den indizierten Property-Get einmal aus und bindet den resultierenden
Klassenverweis lokal, während UDT-Targets weiterhin über echte Adressen laufen. Damit entfallen die
drei `With`-Diagnosen aus `GpTabs.ctl`; der VISIA-Stand sinkt auf **6 semantische Fehler**, bei
**37 von 40** fehlerfreien Dateien. Die Suite umfasst nun **712 Tests**.

`LBound` und `UBound` akzeptieren nun auch die VB6-Schreibweise mit leeren Arrayklammern, etwa
`UBound(values())`. Der Binder bewahrt in diesem Kontext die Arrayreferenz, statt den Ausdruck als
elementlosen Zugriff mit dem Elementtyp zu behandeln. Damit entfallen die drei `UBound`-Diagnosen
aus `mcToolBar.ctl`; der VISIA-Stand sinkt auf **3 semantische Fehler**, bei **38 von 40**
fehlerfreien Dateien. Die Suite umfasst nun **714 Tests**.

Die globalen RichTextBox-OCX-Konstanten `rtfRTF = 0` und `rtfText = 1` sind nun als Built-in-
Konstanten verfügbar und respektieren weiterhin die Überschreibung durch Benutzerdeklarationen.
Damit entfällt die `rtfText`-Diagnose aus `CodeEdit.ctl`; der VISIA-Stand sinkt auf **2 semantische
Fehler**, bei weiterhin **38 von 40** fehlerfreien Dateien. Die Suite umfasst nun **715 Tests**.

`Format` und `Format$` sind nun als Intrinsics bis zum Managed-Backend verdrahtet. Der
deterministische Teilumfang formatiert numerische Masken mit `0`, `#`, Gruppierung, Dezimalstellen,
Prozent und Abschnitten sowie die Standardnamen `General Number`, `Currency`, `Fixed`, `Standard`,
`Percent` und `Scientific`; bei Strings sind die `<`- und `>`-Fallmasken abgedeckt. Für
`VBDateValue` kommen die gebräuchlichen Jahres-, Monats-, Tages-, Stunden-, Minuten-, Sekunden-
und `AM/PM`-Token sowie die Standardnamen `General Date`, `Short Date`, `Long Date`, `Short Time`
und `Long Time` hinzu. Wochenmasken, Locale-Auswahl und weitere String-Platzhalter bleiben bewusst
offen, statt über eine unklare Annäherung als kompatibel zu gelten. Der VISIA-Stand bleibt
unverändert bei **2 semantischen Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst
nun **720 Tests**.

Die skalaren Date-Part-/Timer-Intrinsics `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second` und
`Timer` sind nun als backendunabhängige Runtime-Verträge ergänzt. Die Date-Part-Funktionen lesen
typisierte `Date`-Ausdrücke über die bestehende OLE-Automation-Darstellung; `Timer` liefert die
Sekunden seit lokaler Mitternacht im VB6-Tagesbereich. Der VISIA-Stand bleibt bei **2 semantischen
Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **722 Tests**.

`DateSerial` und `TimeSerial` normalisieren nun Date-/Zeitbestandteile auf der bestehenden
OLE-Automation-Darstellung. `DateAdd` und `DateDiff` unterstützen die Intervalle `yyyy`, `q`, `m`,
`y`, `d`, `h`, `n` und `s`; Wochenintervalle bleiben wegen der zusätzlichen Wochentagsparameter
ein eigener späterer Vertrag. Der VISIA-Stand bleibt bei **2 semantischen Fehlern** und **38 von 40**
fehlerfreien Dateien; die Suite umfasst nun **723 Tests**.

`DateAdd` und `DateDiff` unterstützen nun auch die VB6-Wochenintervalle `w` und `ww`. `DateDiff`
akzeptiert zusätzlich `firstdayofweek` und `firstweekofyear` mit den portablen VB6-Konstantwerten;
die Wochengrenzen werden auf dem OLE-Datewert ausgewertet. Der VISIA-Stand bleibt bei **2
semantischen Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst weiterhin **733 Tests**.

`DatePart` ist nun als eigener Intrinsic-Vertrag ergänzt und liefert Kalender-, Zeit-, Wochentags-
und Kalenderwochenanteile mit den portablen `firstdayofweek`-/`firstweekofyear`-Regeln. Die
Standardkonstanten `vbSunday` bis `vbSaturday` sowie `vbFirstJan1`, `vbFirstFourDays` und
`vbFirstFullWeek` sind projektweit verfügbar. Der VISIA-Stand bleibt bei **2 semantischen Fehlern**
und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **735 Tests**.

`Weekday`, `WeekdayName` und `MonthName` ergänzen den Date-/Time-Intrinsic-Slice mit konfigurierbarer
Wochenbasis und invariant-stabilen Namen für das portable Compilerprofil. Der VISIA-Stand bleibt bei
**2 semantischen Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **737 Tests**.

Variant-Arithmetik erhält bei `Date + Zahl` und `Date - Zahl` den Date-Subtype; `Date - Date`
liefert weiterhin einen numerischen Abstand. Damit bleibt die typisierte OLE-Automation-Darstellung
auch nach dynamischer Variant-Arithmetik erhalten. Der VISIA-Stand bleibt bei **2 semantischen
Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **725 Tests**.

`DateValue` und `TimeValue` normalisieren nun den Tages- beziehungsweise Zeitanteil beliebiger
Date-Ausdrücke auf die bestehende OLE-Automation-Darstellung. Der VISIA-Stand bleibt bei **2
semantischen Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **727 Tests**.

Die skalaren Mathematik-Intrinsics `Exp`, `Log`, `Sin`, `Cos`, `Tan` und `Atn` sind nun als
portable Runtime-Verträge ergänzt. Winkel werden im bestehenden Radiant-Vertrag verarbeitet,
`Log` ist der natürliche Logarithmus; ungültige Log-Eingaben und `Exp`-Überläufe bleiben explizite
Laufzeitfehler. Der VISIA-Stand bleibt bei **2 semantischen Fehlern** und **38 von 40** fehlerfreien
Dateien; die Suite umfasst nun **729 Tests**.

`Abs`, `Fix` und `Round` geben bei einem `Null`-Variant wieder `Null` zurück und behandeln
`Empty` als numerische 0. Damit folgt der bestehende Math-Slice auch bei uninitialisierten und
explizit ungültigen Variant-Zuständen dem VB6-Vertrag. Der VISIA-Stand bleibt bei **2 semantischen
Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **731 Tests**.

`LongPtr` und `CLngPtr` sind nun als native-width `System.IntPtr`-Verträge ergänzt. Der Typ läuft
durch Binder, IR, Managed-Emitter und Runtime mit checked Integer-/Bitwise-Operatoren, kann als
`For`-Zähler verwendet werden, wird in Variants numerisch konvertiert und erscheint in `Declare`
als echte pointergroße P/Invoke-Signatur. Der VISIA-Stand bleibt bei **2 semantischen Fehlern** und
**38 von 40** fehlerfreien Dateien; die Suite umfasst nun **741 Tests**.

`UInteger` mit dem Alias `UInt32` ergänzt die unsigned Integer-Basis. `CUInt`, checked Arithmetic
und Bitwise-Operationen, `For`-Zähler, boxed-Variant-Konvertierung sowie skalare `Declare`-/P/Invoke-
Signaturen nutzen den vollständigen Bereich 0 bis 4.294.967.295. `UShort`/`UInt16` und `ULong`/`UInt64`
ergänzen nun dieselben checked Managed-, Variant-, `For`- und skalaren `Declare`-/P/Invoke-Verträge
für 16 und 64 Bit mit `CUShort` und `CULng`. Der VISIA-Stand bleibt bei **2 semantischen Fehlern**
und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **749 Tests**.

`AddressOf` löst jetzt direkte Prozedurziele semantisch auf und senkt sie über IR und Managed-Emitter
zu einer echten Funktionsadresse. `LongPtr` verwendet dabei `ldftn` und native-width `System.IntPtr`,
während Legacy-Callback-Deklarationen mit `Long` explizit auf den 32-Bit-Vertrag konvertiert werden.
Signaturprüfung, native Callback-ABI und die Lebensdauer von Callback-Delegates bleiben als eigener
Interop-Schritt offen. Der VISIA-Stand verbessert sich dadurch auf **1 semantischen Fehler** und
**39 von 40** fehlerfreien Dateien; die Suite umfasst nun **751 Tests**.

Error-Varianten sind jetzt als eigener Runtime-Zustand ergänzt: `CVErr`/`IsError`, `VarType = 10`
und `TypeName = "Error"` laufen durch Symbolik, IR, Managed-Emitter und End-to-End-Ausführung.
`CVErr(Null)` bewahrt dabei die bestehende Null-Semantik. Der VISIA-Stand bleibt bei **1 semantischen
Fehler** und **39 von 40** fehlerfreien Dateien; die Suite umfasst nun **752 Tests**.

Benannte Argumente mit `name:=value` werden jetzt im Parser bewahrt und im Binder case-insensitiv
aufgelöst. Die Argumente werden in Signaturreihenfolge gebracht, optionale Lücken erhalten ihre
Defaults, und die Reihenfolge kann damit unabhängig von der Deklaration verwendet werden. Der
VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die Suite
umfasst nun **754 Tests**.

`IsArray`, `IsDate` und `IsObject` sind jetzt als eigene Variant-Typprädikate durch Symbolik, IR,
Managed-Emitter und Runtime geführt. Arrays bleiben gegenüber Objekten getrennt, `Nothing` wird
als Objekt erkannt, und Datumserkennung umfasst den erhaltenen Date-Subtype sowie parsebare
invariante Datums-/Zeitstrings. Der VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von
40** fehlerfreien Dateien; die Suite umfasst nun **756 Tests**.

Die Standardfunktion `Array(...)` erzeugt jetzt über denselben ParamArray-/Array-Emitter ein
nullbasiertes Variant-Array. Leere Aufrufe und gemischte Werte sind durch Runtime- und End-to-End-
Tests abgedeckt; der VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien
Dateien, die Suite umfasst nun **758 Tests**.

`Join` und `Filter` verarbeiten jetzt typisierte `String()`-Arrays über eigene Intrinsic-, IR-,
Emitter- und Runtime-Verträge. `Join` unterstützt das optionale Trennzeichen; `Filter` bewahrt die
Reihenfolge und unterstützt Include-/Binary-/Text-Vergleiche, auch bei leeren Ergebnissen. Der
VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die Suite
umfasst nun **760 Tests**.

`Oct` ergänzt `Hex` als Variant-String-Konversion mit bis zu elf Long-Oktalziffern und erhaltener
`Null`-Semantik. `CVar` führt die Typkonversionsfamilie als expliziter Variant-Vertrag fort und
bewahrt Date-Subtype und Variant-Zustände. Der VISIA-Stand bleibt bei **1 semantischen Fehler** und
**39 von 40** fehlerfreien Dateien; die Suite umfasst nun **762 Tests**.

`Choose` ergänzt die Variant-ParamArray-Familie um eine 1-basierte Auswahl mit gerundetem Index,
eager Auswertung aller Auswahlargumente und `Null` für Indizes außerhalb des gültigen Bereichs.
Der VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die
Suite umfasst nun **764 Tests**.

`Switch` bewahrt bei vollständig ausgewerteten Bedingungs-/Wertpaaren nun auch den korrekten
Variant-`Null`-Zustand, wenn keine Bedingung wahr ist. Der VISIA-Stand bleibt bei **1 semantischen
Fehler** und **39 von 40** fehlerfreien Dateien; die Suite umfasst nun **766 Tests**.

`Str` ergänzt die numerischen Konversionen um invariant-stabile VB6-Ausgabe mit führendem
Vorzeichen-Leerzeichen für nichtnegative Werte. Der VISIA-Stand bleibt bei **1 semantischen Fehler**
und **39 von 40** fehlerfreien Dateien; die Suite umfasst nun **767 Tests**.

`ChrW` und `AscW` ergänzen die String-Intrinsics um Unicode-UTF-16-Codeunit-Konvertierung mit
signiertem `AscW`-Integer-Vertrag. Der VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von
40** fehlerfreien Dateien; die Suite umfasst nun **769 Tests**.

`CCur` ist nun als vollständiger Compiler-Intrinsic an die vorhandene Currency-Runtime angebunden.
Die Managed-Ausführung prüft die VB6-kompatible Vier-Dezimalstellen-Rundung, boolesche Werte und
den erhaltenen `Currency`-Variant-Subtype. Der VISIA-Stand bleibt bei **1 semantischen Fehler** und
**39 von 40** fehlerfreien Dateien; die Suite umfasst nun **770 Tests**.

`Date` und `Time` liefern nun wie in VB6 `Variant(Date)`-Werte über `VBDateValue`; `CVDate` wandelt
beliebige kompatible Ausdrücke in denselben Date-Subtype um. `VarType`, `IsDate` und bestehende
Date-Part-Intrinsics bleiben dadurch auf einem gemeinsamen OLE-Automation-Pfad. Der VISIA-Stand
bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die Suite umfasst nun
**772 Tests**.

`CStr` behandelt die verbliebenen Error-/Null-Variantfälle nun explizit: Error-Werte werden als
`Error <Nummer>` formatiert, während `Null` einen kontrollierten Konversionsfehler auslöst. Der
VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die Suite
umfasst nun **774 Tests**.

`Environ` ist nun als hostneutraler Interaktions-Intrinsic verdrahtet. Der Compiler unterstützt
case-insensitiven Namenszugriff sowie den numerischen, gerundeten 1-basierten Zugriff mit stabil
sortierten `NAME=VALUE`-Einträgen; unbekannte Namen und Positionen liefern den leeren String. Der
VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die Suite
umfasst nun **776 Tests**.

Das globale `App`-Objekt besitzt nun einen hostneutralen Managed-Vertrag. `EXEName`, `Path`,
`Title`, `Major`, `Minor` und `Revision` werden aus der Entry-Assembly abgeleitet; `hInstance`
liefert im Headless-Profil deterministisch `0`. Die Properties werden mit typisierten IR-/Runtime-
Aufrufen emittiert, damit numerische Werte nicht über den objektbasierten Late-Bound-Pfad laufen.
Der VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die
Suite umfasst nun **778 Tests**.

`Rnd` und `Randomize` sind nun als vollständiger mathematischer Runtime-Slice verdrahtet. Der
Managed-Kern nutzt die dokumentierte VB6-24-Bit-LCG, unterscheidet negative, null, positive und
ausgelassene `Rnd`-Argumente und unterstützt timerbasierte sowie reproduzierbare numerische Seeds.
Der VISIA-Stand bleibt bei **1 semantischen Fehler** und **39 von 40** fehlerfreien Dateien; die
Suite umfasst nun **781 Tests**.

Der native LLVM-Emitter trägt nun den ersten ausgabefähigen Skalar-Slice: `Byte`, `Integer`,
`Long`, `LongLong`, `LongPtr`, die unsigned Ganzzahlbreiten, `Single`, `Double`, `Date`,
`Currency` und `Boolean` werden mit x86-/x64-breiten Typen, lokalen, globalen und Parameter-Slots,
arithmetischen/vergleichenden Runtime-Operationen, Returns und Basic-Block-Verzweigungen als
LLVM-Text emittiert. Direkte interne skalare Prozeduraufrufe werden mit Wert- und Pointer-Argumenten
ebenfalls als native Calls emittiert. Skalare externe `Declare`-Prozeduren werden mit x86-/x64-
Signaturen als LLVM-`declare`-Verträge ausgegeben und von generiertem Code aufgerufen. Skalare
ByRef-Parameter werden als native Pointer-Slots gelesen und geschrieben. Currency-Literale
werden als skalierte `i64`-Slots mit vier Nachkommastellen emittiert. Sichere skalare
Konversionen wie native Integer-Erweiterungen, Integer-zu-Floating und Bool-Tests werden
direkt emittiert; geprüfte Integer-Verengungen und Vorzeichenwechsel werden nun über
trap-geschützte i64-Helper emittiert. Currency-Multiplikation wird nun über ein skaliertes
`i128`-Produkt mit Banker's Rounding und `Int64`-Range-Guard emittiert. Komplexe
Variant-/String-/Objekt-/ByRef-Werte und Klassen bleiben
explizit diagnostiziert. `vb6c --emit-llvm` macht diesen Backend-Slice für Einzeldateien und
`.vbp`-Projekte mit x64-Default sowie explizitem x86/x64-Target erreichbar. Die Suite umfasst nun
**823 Tests**. Gerundete Single-/Double-zu-Integer-Konversionen bis 64 Bit verwenden nun
`llvm.roundeven.f64`, NaN-/Range-Guards und sichere `fptosi`/`fptoui`-Konversionen mit darstellbaren
Grenzwerten. Currency-zu-Integer-Konversionen verwenden nun denselben skalierten
Ties-to-even-Helper; exakte Integer- und Boolean-zu-Currency-Konversionen
skalieren nun mit i128, prüfen den Int64-Currency-Bereich und bilden `True`
als `-10000` ab. Gerundete Single-/Double-zu-Currency-Konversionen skalieren
nun mit `roundeven`, prüfen NaN-/Range-Fälle und verwenden eine geprüfte
`fptosi`-Konversion.
Typisierte
Integer-Division und Restbildung werden über sign-/zero-erweiterte
i64-Helper mit expliziten Guards für den Divisor `0` und signedem `MinValue / -1` als LLVM-
Operationen ausgegeben; die Guard-Pfade schreiben nun Fehlernummer `11` bzw. `6` in einen
thread-lokalen pending-Status. `On Error Resume Next` und label-directed Handler-Boundaries
verzweigen auf dieser Basis, `Err.Number` und `Err.Clear` sind nativ lesbar bzw. nutzbar.
`Resume Next` und targetloses `Resume` verwenden die gespeicherte Boundary-ID für Fortsetzung bzw.
Wiederholung. Stringwertige Err-Felder bleiben offen. Single-Arithmetik und -Negation
sowie Single-/Double-Division verwenden nun ebenfalls pending-error-aware LLVM-Helper für
Single-Overflow und Division durch `0`.
Integer-Addieren/-Subtrahieren/-Multiplizieren und Currency-Addieren/-Subtrahieren/-Negieren
verwenden nun Overflow-Intrinsics mit expliziten Zielbreiten-Guards; Currency-Multiplikation ist
mit der skalierten, gerundeten Zwischenrechnung nun ebenfalls nativ umgesetzt.

Der Missing-Variant-Slice ist nun ebenfalls geschlossen: ausgelassene `Optional Variant`-Argumente
bleiben fuer `IsMissing` erkennbar, erscheinen ueber `TypeName` als `Error`, loesen bei expliziten
numerischen Konversionen den stabil dokumentierten Fehlerwert **448** auf und melden bei sonstiger
Verwendung (Variant-Operatoren, String-Konversion, Bool-Kontext und `Debug.Print`) `Err.Number =
448`. Die Runtime-, Error-Handling- und Managed-End-to-End-Regressionen decken diese Trennung vom
normalen `CVErr`-Error-Variant ab.

Array-Varianten bilden nun ebenfalls einen expliziten Grenzvertrag: `TypeName` liefert fuer
`VBArray<T>` die VB6-Form `T()`, und skalare arithmetische, logische, relationale, String- und
Konversionspfade melden bei Array-Operanden `Err.Number = 13` statt einer generischen CLR-Ausnahme.
Elementzugriff mit Lesen und Schreiben laeuft nun ueber den Variant-Array-Runtimevertrag; Variant()-
Elemente koennen an Variant-ByRef-Parameter weitergereicht werden. UDT-Arrays, Default-Properties
und vollstaendige Objekt-/Array-Promotion bleiben separate offene M4-/M5-Vertraege. Die Suite
umfasst nun **817 Tests**.

Variant-Vergleiche konvertieren `Single`- und `Double`-Operanden nun in den bestehenden Decimal-
Promotionspfad, sobald der Gegenwert als Decimal vergleichbar ist. Dadurch bleibt die Decimal-
Präzision gegenüber binären Gleitkommawerten erhalten; der VISIA-Stand bleibt bei **2 semantischen
Fehlern** und **38 von 40** fehlerfreien Dateien; die Suite umfasst nun **733 Tests**.

Seit diesem Messpunkt sind Klasseninstanzen als eigener Managed-Typ mit Instanzfeldern,
`Class_Initialize`, `Class_Terminate`, `New`, `Set`, `Is`, `TypeOf`, Properties und einfachen
Events/`WithEvents` emittierbar. Der M6-Kontrollfluss ist fuer numerische und benannte Labels,
`GoTo`, `On ... GoTo`, `GoSub`/`Return` und `On ... GoSub` im Basic-Block-IR und Managed-Backend
verifiziert.

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
bleibt bei 752, die Gesamtsumme fällt entsprechend von 2294 auf 2034.

Darauf baut jetzt ein eigener zweipassiger UDT-Typraum auf. `UserDefinedTypeSymbol` erzeugt vor
der Memberauflösung stabile Typidentitäten, sodass Vorwärtsreferenzen zwischen UDTs ohne
String-Platzhalter möglich sind. Membernamen werden case-insensitiv gebunden; feste und
dynamische Arraymember behalten ihren Rang, `String * n` erhält einen eigenen
`FixedLengthStringTypeSymbol`. Öffentliche Typen teilen projektweit dieselbe Identität, private
Typen bleiben modullokal und dürfen in anderen Modulen denselben Namen tragen. Sowohl
`VBCompilation` als auch `VBProjectCompilation` geben diese Typmodelle und ihre Diagnosen jetzt
im Analyseergebnis zurück; ungültige UDT-Deklarationen stoppen die Codegenerierung statt später als
`object?` approximiert zu werden. Actions #866 validiert diesen Scope-/Analyse-Slice mit
**337 Tests**, 0 Warnungen und 0 Buildfehlern. Der VISIA-Zähler bleibt erwartungsgemäß bei
2034 / 1214 Parser / 68 Lexer / 752 Semantik, weil UDT-Werte in Variablen und Parametern erst im
nächsten Slice in den bestehenden Haupt-Binder integriert werden.

Danach sind UDT-Werte in Locals/Parametern/Modulvariablen, `With`-Blöcke, `For Each` über feste,
mehrdimensionale und dynamische Arrays, die Variant-Grundlage (Speicherung, Konvertierung,
Multiplikation, `&`-Verkettung, eine Gleichheits-Teilmenge), das Enum-Binding, die eingebauten
VB-String-Konstanten und die ersten String-Intrinsics (`Len`, `Mid`, `Chr`) gelandet. Die Messung
dazu ergibt **1339** Gesamtfehler: **480 Parser**, **62 Lexer**, **797 Semantik**.

Der Parserzähler fällt damit von 1214 auf 480 — mit −734 der mit Abstand größte Einzelsprung der
bisherigen Historie, und der erste, bei dem die Gesamtsumme trotz steigender Semantik deutlich
mitfällt (2034 → 1339). Der Grund steht seit M0 in der Blockertabelle: `With` kommt in 19 Dateien
629-mal vor, und ohne Memberzugriff entgleiste dort jede Folgezeile. Die Semantik steigt
erwartungsgemäß von 752 auf 797.

Die Rangfolge der Blocker hat sich dadurch verschoben:

| Code | Vorkommen | Dateien | Bedeutung |
|---|---|---|---|
| `VB6P0001` | 480 | 15 | verbleibende Parserlücken |
| `VB6S0005` | 342 | 10 | Prozedur nicht deklariert — überwiegend Folge nicht parsender Module und fehlender Bibliotheksfunktionen |
| `VB6S0007` | 290 | 6 | ByRef-Argument muss eine Variable sein |
| `VB6S0001` | 147 | 9 | Variable nicht deklariert |
| `VB6L0001` | 62 | 6 | `#` — Dateinummern der Datei-I/O |
| `VB6S0006` | 16 | 4 | falsche Argumentanzahl |

**`VB6S0007` ist die Überraschung dieser Messung.** Die ByRef-Randfälle stehen bisher als kleiner
Punkt in M5, sind mit 290 Vorkommen in 6 Dateien aber der zweitgrößte semantische Blocker. Der
Auslöser ist die in `CLAUDE.md` notierte Einschränkung: ByRef verlangt heute eine Variable mit
exakt passendem Typ, also scheitern geklammerte Argumente und temporäre Konvertierungen. Das
sollte vor dem Rest von M5 gezogen werden.

Untypisierte `Function`-Deklarationen senken die Parserfehler danach von 480 auf 466. Die
Gesamtsumme steigt dabei von 1339 auf 1473, weil 14 weitere Prozeduren nicht mehr an ihrem Kopf
entgleisen und komplett in den Binder gelangen: `VB6S0007` springt von 290 auf 409, `VB6S0006`
von 16 auf 36. Derselbe Übergang wie bei `Optional` — Parserkaskade raus, konkrete Semantiklücke
rein. Er unterstreicht zugleich, wie dominant die ByRef-Randfälle inzwischen sind.

Danach wurden die ByRef-Randfälle aus M5 vorgezogen — nach demselben Kriterium wie damals
`Optional`: gemessene Blockerbreite schlägt Meilensteinreihenfolge. `VB6S0007` verschwindet
vollständig, alle 409 Vorkommen, und die Gesamtsumme fällt von 1473 auf **1064**. Das war kein
implementierter Sonderfall, sondern eine falsche Annahme: VB6 akzeptiert Literale, Ausdrücke und
Funktionsergebnisse an ByRef-Parametern, indem es einen Temporary übergibt und das Rückschreiben
verwirft. Nur eine *Variable* falschen Typs bleibt ein Fehler (`VB6S0008`), weil das
Rückschreiben dort ein Ziel hätte.

Im selben Zug fiel eine stille Abweichung: `Foo (x)` hat `x` verändert. In VB6 erzwingen
Klammern Auswertung zum Wert, der Aufgerufene kann also nicht zurückschreiben. Ursache war der
Parser, der `Foo (x)` und `Call Foo(x)` beide als Argumentliste las — nur ein `Call`-Statement
hat aber eine geklammerte Argumentliste. Genau die Sorte Fehler, die die Projektregeln als
schlimmer einstufen als eine Diagnose: falsches Ergebnis statt gemeldeter Lücke.

`ReDim Section(0).Bytes(0)` — ein `ReDim` auf ein Arraymember innerhalb eines UDT-Elements — war
danach der Ersterfehler in vier Modulen. Das gebundene Modell nimmt dort weiterhin ein einfaches
`VariableSymbol`, die Konstruktion ist also noch nicht absenkbar; der unbehandelte Punkt riss
aber die ganze restliche Prozedur mit. `VB6P0002` benennt sie jetzt und verwirft nur die Zeile,
nach demselben Recovery-Muster wie bei den `Type`-Membern. 24 Kaskadenfehler weichen 12 präzisen,
Parser 466 → 454.

**Offen und bewusst nicht halb gebaut:** volle Unterstützung verlangt ein Zielausdruck statt
eines Symbols in `BoundReDimStatement`, also Syntax, Binder und Codegen. Das ist der nächste
große Array-Slice.

Danach wurde die Datei-I/O-**Syntax** aus M7 vorgezogen, weil der Lexer an `#` scheiterte und
ein kaputter Tokenstrom die teuerste Sorte Barriere ist. Die Wirkung ist entsprechend groß:
**Lexerfehler verschwinden vollständig** (62 → 0) und die Parserfehler fallen von 454 auf 218 —
die 62 gemeldeten Lexerfehler hatten also weit mehr als 200 Parserfehler nach sich gezogen.
Gesamtsumme 1052 → 832.

`Open`, `Close`, `Get`, `Put` und `Seek` werden **kontextuell** am Statement-Anfang erkannt, nicht
global reserviert — dieselbe Lehre wie bei `Option Base`. Eine Zuweisung an eine Variable namens
`Get` bleibt eine Zuweisung.

Der Binder gibt für unbekannte Statements `null` zurück; ohne Guard wären die Anweisungen also
kommentarlos aus dem erzeugten Programm gefallen — ein falsches Programm statt einer gemeldeten
Lücke.

Der Folgeslice hat Runtime, Bindung und Codegen nachgezogen: `VB6Files` bildet die prozessweite
Dateinummerntabelle nach, Positionen sind einsbasiert, jeder Typ liest und schreibt seine exakte
VB6-Speichergröße, und `Currency` geht als skalierter Int64 auf die Platte. Damit kompilieren 11
der 17 I/O-Anweisungen im Korpus. **Offen bleiben Transfers von `String` und UDT-Werten**
(`VB6S0058`, 6 Vorkommen): der eine braucht das Zwei-Byte-Längenpräfix, der andere ein
Record-Layout — beides eigene Regeln, die hier nicht geraten werden.

`TypeOf x Is T` wurde als zwei benachbarte Bezeichner gelesen, und ein einziges Vorkommen riss
den Rest der Datei mit: allein `envBorders.bas` verlor 72 Parserfehler daran. Die Syntax wird
jetzt bewahrt, die Semantik als `VB6S0060` gemeldet — sie braucht das Objektmodell aus M5/M9.
Parser 218 → 110, Gesamtsumme 822 → 726.

Aufrufseitiges `ByVal` — in der Blockertabelle seit M0 als `CopyMemory SwpVal, ByVal
VarPtr(String1), 4` notiert — senkt die Parserfehler von 110 auf 71. Die Bindung fiel dabei
billig aus: explizites `ByVal` überschreibt einen ByRef-Parameter genau wie Klammern und nutzt
denselben Temporary. Die Semantik steigt von 604 auf 641, weil wieder mehr Code den Binder
erreicht.

`On Error GoTo` blieb der Ersterfehler in sieben Modulen, deshalb wurde die **M6-Syntax**
vorgezogen: `On Error GoTo <Label>`, `On Error GoTo 0`, `On Error Resume Next`, `GoTo` und
Labels. Parser 71 → 37. Die Gesamtsumme steigt dabei von 692 auf 752 — der inzwischen vertraute
Übergang, hier besonders deutlich: `VB6S0060` (TypeOf) taucht mit 24 Vorkommen überhaupt erst
auf, weil `envBorders.bas` zum ersten Mal den Binder erreicht.

Ein Label wird nur erkannt, wenn es allein auf seiner Zeile steht. `Foo: Bar` ist in VB6 ein
parameterloser Aufruf plus Anweisungstrenner; es als Label zu lesen würde den Aufruf still
verschlucken. Alle 21 Labels im Korpus stehen auf eigenen Zeilen.

Qualifizierte Aufrufe (`frmMain.SelectObjectObject "Frames"`) und ausgelassene Argumente
(`List.Add , , "General"`) waren die letzte breite Parserlücke. Damit fällt der Parserzähler auf
**12** — von 480 zu Beginn dieser Arbeit, also 97 %.

Zwei Regeln halten den Lookahead ehrlich, beide von Tests erzwungen, die zuerst brachen: der
Punkt muss direkt auf den Empfänger folgen, sonst wird `Consume record.Value` als Memberaufruf
gelesen; und das Leerzeichen entscheidet den Rest, weil `Consume .Value` innerhalb eines `With`
das With-Member als Argument übergibt. VB6 zieht dieselbe Grenze — und der trivia-erhaltende
Lexer macht sie überhaupt erst sichtbar.

Der größte Sprung dieser Reihe kam dann nicht aus einem Sprachfeature, sondern aus der
Projekt-Pipeline. Sie sammelte Deklarationen — Prozeduren, Modulvariablen, Enums, UDTs —
ausschließlich aus Modulen **ohne** Parserfehler. Ein Modul mit einem einzigen Syntaxfehler war
damit projektweit unsichtbar, und jeder Aufruf hinein wurde „nicht deklariert".

Das widersprach dem eigenen Entwurf: der Parser ist ausdrücklich fehlertolerant, damit er trotz
Fehlern einen brauchbaren Baum liefert. `comSummary.bas` hat genau einen Parserfehler und
beherbergt `ErrMessage` (30 Aufrufe aus sieben Dateien); `comLinker.bas` hat drei und deklariert
`ENUM_APP_TYPE` und `ENUM_SECTION_TYPE`. Acht der 27 Module waren betroffen.

Gesamtsumme 784 → 489 → **416**, `VB6S0005` von 364 auf 94, `VB6S0001` von 179 auf 119. Vor
allem aber: **die erste Datei analysiert fehlerfrei** (`envVirtualFiles.bas`). Genau wie oben
vorhergesagt kam dieser Sprung schlagartig, nicht schrittweise — weil projektweit gebunden wird
und eine Datei erst sauber sein kann, wenn ihre Abhängigkeiten es sind. Der Ratchet steht
entsprechend auf 1.

`ReDim Section(0).Bytes(0)` ist damit echt implementiert statt abgefangen; `VB6P0002` entfällt.
Die Modellgrenze war, dass `BoundReDimStatement` und `BoundArrayBoundExpression` ein
`VariableSymbol` trugen — ein Array in einem UDT-Element hat aber kein eigenes Symbol. Beide
nehmen jetzt den Ausdruck, der es lokalisiert.

Zwei Folgearbeiten fielen dabei an, beide von der Messung erzwungen: die wiederholte
Elementtypangabe (`... As Byte`), und `UBound` auf einem Arrayausdruck statt nur auf einem Namen
— letzteres allein 48 Diagnosen. Zuletzt konnte der Layout-Guard für **dynamische Arraymember**
gelöst werden: der Generator konnte sie längst, er wurde nur nicht gelassen.

Die `Optional`-Aufrufsemantik aus M5 war danach der größte Einzelposten hinter `VB6S0006`:
`AddSymbol` deklariert fünf Parameter, zwei davon `Optional`, und jeder Aufruf liefert vier.
Ein ausgelassenes Argument trägt jetzt seinen deklarierten Default — oder den Default seines
Typs, wenn die Deklaration keinen nennt. Ein ausgelassener ByRef-`Optional` bekommt einen
Temporary und hat damit kein Ziel zum Zurückschreiben, wie in VB6.

Gesamtsumme 459 → **367**, und die Zahl fehlerfreier Dateien steigt von 1 auf **3**.

`FileNum = FreeFile` ist in VB6 ein Funktionsaufruf; ein nackter Name suchte aber nur nach einer
Variablen. Zusammen mit `FreeFile`, `LOF`, `EOF` und der `Seek`-Funktion — die Runtime hatte alle
bis auf `EOF` bereits, sie waren nur nicht freigegeben — sinkt die Summe auf **322** bei
**4 von 27** fehlerfreien Dateien.

**Die billigen Hebel sind dünner geworden, aber nicht erschöpft.** Stand nach dem
Backend-Cutover: **304 Fehler**, davon 12 Parser, 0 Lexer, 292 Semantik, bei **5 von 27**
fehlerfreien Dateien.

| Code | Anzahl | wartet auf |
|---|---|---|
| `VB6S0005` / `VB6S0001` | 148 / 137 | Standardbibliotheksfunktionen und fehlende Projekt-/Objektbezeichner; davon sind `CopyMemory`, `DoEvents`, `VarPtr`, `RaiseEvent`, `frmMain`, `App` und `Err` die breiten Blocker |
| `VB6S0003` | 31 | fehlende externe Typen wie `Collection`, `Control` und `OLE_COLOR`; COM-/Forms-Typraum folgt |
| `VB6S0061` | 27 | `On Error` und Handler-Semantik; das lowered IR aus M6 steht inzwischen |
| `VB6P0001` | 12 | verstreute Parserreste |
| `VB6S0012` | 8 | verbliebene Typkonvertierungen |
| `VB6S0058` | 6 | Datei-I/O-Formen jenseits der numerischen Binärtransfers |

Zum damaligen Messstand wurden `.bas` und `.cls` gelesen und analysiert; `.ctl` (4) und `.frm`
(6) lagen noch außerhalb des Compiler-Kerns. Der aktuelle Stand liest zusätzlich die Designer-
Hüllen dieser Module, ohne die historische Messreihe nachträglich umzuschreiben.

Dass zunehmend *semantische* Fehler auftauchen, ist der eigentliche Fortschritt: Dateien kommen
bis zum Binder durch, statt schon im Parser zu entgleisen.

Die Zahl fehlerfreier Dateien blieb lange bei 0: gebunden wird projektweit, also kann eine Datei
erst sauber sein, wenn auch ihre Abhängigkeiten parsen. Der Sprung kam schlagartig, wie
erwartet — siehe die Zeile mit 1 von 27 oben.

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
die Namen für Folgeausdrücke sichtbar und registriert prozedurbezogenen Modul-Storage; der bestehende
Modulinitialisierer setzt String- und Array-Defaults einmalig, während skalare Defaults aus dem CLR-Nullwert kommen.
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
| Datei-I/O mit Dateinummern | `Open ... For Binary/Input/Output/Append As #1`, `Get #1`, `Put #1`, `Print #1`, `Close #1` |

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
| `ReDim`/`Preserve` | 103 ✅ typed arrays | `Type ... End Type` | 52 ✅ Syntax + Typraum |
| `With` | 102 | `Enum` | 44 ✅ Syntax |

Kommt **nicht** vor: `Format$` 0, `Date` 0, ADO 0, `#If` 0. `Resume`, `Resume Next` und
`Resume <Label>` sind inzwischen syntaktisch gebunden; `Resume Next` besitzt im Managed-Backend
einen fehlerstellenspezifischen Fortsetzungsdispatcher. Der native Resume-/ABI-Vertrag bleibt offen.

## Entschiedene Weichenstellungen

- **Variant früh**, bewusst gegen die VISIA-Evidenz (dort nur 20 Treffer): der Umbau wird später
  teurer, und die Business-Legacy-Projekte brauchen ihn sehr wohl.
- **x86 als Default-Ausgabe, x64 opt-in.** Bestätigt durch den Korpus: VISIA hängt an 32-Bit-OCX
  (`MSComDlg.CommonDialog`, `MSComctlLib`, `RichTextLib`), die ein 64-Bit-Prozess nicht
  in-process laden kann. „64 Bit" gilt für Sprache und Typen, nicht zwingend für den Prozess.
  Muss vor Meilenstein 8 endgültig entschieden sein, weil Marshalling-Code davon abhängt.
- **Zahlkonvertierung ist invariant, nicht locale-abhängig.** `VB6.Runtime` konvertiert zwischen
  Strings und Zahlen ausschließlich mit `CultureInfo.InvariantCulture`. Klassisches VB6 wertete
  `CDbl("2.5")` gegen die aktive Locale aus, sodass derselbe Quelltext je nach Maschine 2,5 oder
  25 ergab. Für einen Compiler wiegt Determinismus schwerer als diese Treue: das Kompilat soll
  überall dasselbe tun. Echte locale-abhängige Ausgabe gehört später zu `Format$`, wo die Locale
  ein expliziter Parameter ist statt ambienter Thread-Zustand. Dies ist eine der wenigen
  Stellen, an denen bewusst von VB6 abgewichen wird.
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
      Elemente koennen an Variant-ByRef-Parameter weitergereicht werden. Vollstaendige
      Objekt-/Array-Promotion und weitere Variant-ByRef-Faelle bleiben offen
- [ ] Vollständige Variant-Arithmetik mit VB6-Promotionsregeln und impliziter Konvertierung. Numerische `+`, `-`, `*`, `/`, `\`, `Mod`, `^`, logische Operatoren, Vergleiche, `&` und die String/Variant-Sonderregeln von `+` sind für die aktuelle Scalar-Variantmenge implementiert; `CDec` sowie Decimal-aware `+`, `-`, `*`, `/`, `Mod`, `\`, `^`, logische Operatoren, unäres `-` und Vergleiche sind ergänzt. Empty-Operanden, Null-Vergleiche, Null-Arithmetik, Null-If-Verzweigungen, Null bei `&` sowie Currency-/Single-Vergleichspromotionen sind regressionsgesichert. Offen bleiben weitere `Null`/`Missing`-Sonderfälle, Objekt- und Array-Varianten sowie die abschließende Prüfung aller VB6-Promotionstabellen.
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
- [~] Late-bound `Variant`-/`Object`-Member: Property-Get/Let/Set und Methodenaufrufe auf erzeugten Managed-Klassen sowie CLR-Property-Fallback stehen; optionale Parameter, `ParamArray`, typisierte Property-/Indexer-Konversionen und ByRef-Writeback für Managed-/CLR-Ziele sind ergänzt; COM-Defaultzugriff über `DISPID_VALUE` und COM-RCW-Identität über `IUnknown` sind ergänzt, vollständige COM-/IDispatch-Auflösung, COM-ByRef-/Event-ABI und Host-ABI bleiben offen
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
      Dokumente, Locals und Anweisungsgrenzen. **Offen:** Prozedurgrenzen und `Debug.Assert`

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
      für direkte Prozedurziele, komplexes Array-Marshalling sowie vollständige
      Callback-ABI-/Delegate-Verträge bleiben offen
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
      `FSOURCE`-Event-Signaturen aus TypeLib-Coclasses werden ebenfalls importiert; vollständiger
      COM-ByRef-/Connection-Point-Event-ABI, natives OCX-Hosting und der native
      LLVM-Pfad bleiben offen. Der Managed/.NET-Konsum wird vor dem nativen LLVM-Backend vervollständigt
- [ ] eigener COM-Server-/ClassFactory-/IUnknown-Vertrag für emittierte VB6-Klassen
- [~] .NET-Backend als primären kompatiblen Zielpfad stabilisieren; Variant-/Object-/COM-Randfälle und
      vollständige Runtime-/Projektverträge bleiben offen
- [~] LLVM-natives Windows-Backend für x86 und x64 (**optional/deferred**) — primitive skalare IR-Emission
      für x86/x64 einschließlich globaler Slots, skalierter Currency-Literale, sicherer skalare
      Konversionen, skalarer `Declare`-Verträge, pending-error-aware Arithmetic-/Conversion-Helper und
      native `On Error`-Boundaries mit gespeicherter Resume-Boundary-ID sind ergänzt; stringwertige
      Err-Felder und native ABI-/Runtime-Emission für komplexe VB6-Werte bleiben offen. Dieser Pfad
      blockiert den Managed/.NET-Abschluss nicht.
- [x] MSBuild SDK-Grundvertrag — `VB6Project`, `VB6CompilerPath` und `CompileVB6Project`-Target; NuGet-Packaging und inkrementelle Input-/Output-Verfolgung sind mit `VB6.Compiler.Sdk.1.0.0.nupkg` verifiziert
- [x] `LongPtr`/`CLngPtr` — native-width `System.IntPtr`-Typverträge, checked Integer-/Bitwise-Operatoren,
      `For`-Zähler, Variant-Konvertierungen und `Declare`-P/Invoke-Signaturen
- [x] vorzeichenlose Ganzzahltypen — `UShort`/`UInt16`, `UInteger`/`UInt32` und `ULong`/`UInt64`
      sind mit `CUShort`, `CUInt` und `CULng` sowie checked Managed-/P/Invoke-/Variant-Verträgen ergänzt
- [x] `AddressOf` — direkte Prozedurziele werden als `LongPtr`-Funktionsadresse emittiert und für
      Legacy-`Long`-Callbackparameter konvertiert; native Callback-ABI und Delegate-Lebensdauer offen

## Meilenstein 9 — Forms

Größter Einzelblock.

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
      Automatische Designer-Registrierung, vollständiges Event-Mapping und OCX-Hosting bleiben offen.
- [~] **Control-Arrays** — Designer-`Index`-Eigenschaften und wiederholte Controlnamen werden
      als typisierte VB6-Arrays gebunden und im generierten Form-Konstruktor als Host-Controls
      initialisiert; die vollständige Laufzeit-/WinForms-Nachbildung bleibt offen.
- [~] Zeichnen auf Form/PictureBox, MDI — persistentes `GraphicsLine`-Rendering auf der aktiven
      Formoberfläche mit Twips-/Pixel-Skalierung und Linien-/Rechteckfüllung steht; ein unterstütztes
      `PaintPicture`-Subset zeichnet `Bitmap`-/FRX-/`VBPicture`-Quellen persistent mit; qualifizierte
      `PictureBox.PaintPicture`- und `PictureBox.Line`-Aufrufe lösen ihr eigenes Ziel auf. MDI und vollständige
      DrawMode-/AutoRedraw-/ScaleMode-Semantik bleiben offen
- [~] `UserControl` (ActiveX) — generierte parameterlose `.ctl`-Klassen werden aus der Projektassembly
      instanziiert und als eingebettete borderlose WinForms-Hostflächen in Designer-Controls
      aufgenommen; `UserControl_Initialize`/`UserControl_Terminate` sowie die konventionellen
      `UserControl_*`-UI-Handler werden an die eingebettete Hostfläche gebunden; ein pro Instanz
      gehaltener `VBPropertyBag` wird an `UserControl_ReadProperties`/`UserControl_WriteProperties`
      gereicht; Connection-Point-ABI und
      echte OCX-Komposition bleiben offen
- [ ] OCX-Hosting für `MSComctlLib`, `RichTextLib`, `MSComDlg`

## Meilenstein 10 — IDE

Der erste LSP-Slice für Visual Studio steht: JSON-RPC, Initialize, Dokument-Synchronisation,
Lexer-/Parser-/Semantik-Diagnosen und leere Completion-/Symbol-/Definition-Antworten. Als Nächstes
folgen echte Symbolsuche, Completion, Go-to-definition und Buildintegration. Danach eigenständige IDE-/WinForms-Designer-Funktionen mit verlustfreiem
`.frm`-Roundtrip und Debugger. Diese Schicht ist bewusst nach dem Compiler-Kern eingeordnet.

---

## Zusätzlich, klein und unabhängig

1. [x] `Debug.Print` auf VB6-nahe Formatierung (führendes Vorzeichen-Leerzeichen, 15
   signifikante Stellen); die E2E-Helfer trimmen weiterhin bewusst Plattform-/Spaltenformat
2. Typisierte Vergleiche direkt emittieren statt `VBOperators.Equal(object?, object?)` — der
   Binder hat beide Seiten bereits angeglichen
3. `Currency + Double` folgt nun der VB6-Promotionsreihenfolge und liefert `Double`, während
   `Currency * Double` die separate Multiplikationsreihenfolge beibehält und `Currency` liefert;
   Vergleichspromotionen behalten weiterhin die separate Currency-Präzisionsregel
4. `Debug.Print` formatiert Zahlen invariant und mit VB6-nahem Vorzeichen-/Signifikanzformat
   unverändert unter Punkt 1

## Aktueller .NET-Nachtrag

Der late-bound Managed-/CLR-Dispatch füllt nun optionale Parameter auf, bündelt `ParamArray`-
Argumente, konvertiert Property- und Indexerargumente über die VB-Runtime und schreibt geänderte
ByRef-Argumente in die Variant-Argumentliste zurück. Die drei Runtime-Regressionstests erhöhen die
Suite auf **826 Tests**. COM-/IDispatch-spezifische Identität, Event-Sinks und Host-ABI bleiben als
separater Interop-Schritt offen.

Die Projektintegration unterstützt nun zusätzlich `.vbg`-Gruppen: deklarierte `.vbp`-Pfade werden
relativ zum Gruppenverzeichnis in abhängigkeitssicherer Reihenfolge aufgelöst (referenzierte
Bibliotheken vor ihren Verbrauchern, unabhängige Projekte in Deklarationsreihenfolge), einzeln analysiert und über
`vb6c <gruppe.vbg> --emit-assembly <ausgabeverzeichnis>` als Managed-Artefakte ausgegeben. Gruppen-
und projektbezogene Fehler behalten den aufgelösten Pfad. `Reference=`/`Object=` werden mit
GUID-, Versions-, LCID- und Pfadmetadaten erfasst; fehlende explizite `.vbp`-Verweise erzeugen
aufgelöste Compilerdiagnosen; vorhandene Referenzprojekte liefern ihre Klassenverträge unter
Projekt- und Klassennamen in die semantische Sicht und in Managed-IL-Assembly-/Member-References,
und die verbreiteten `MSComctlLib`-/
`RichTextLib`-/`MSComDlg`-Controltypen werden projektlokal erkannt. Designer-Controlfelder werden
mit diesen Typverträgen als Klassenfelder gebunden; der Managed-Pfad nutzt dafür Late Binding und
emittiert keine falschen CLR-Typreferenzen auf OCX-Dateien. Die Projektgruppenregressionen
erhöhen die Suite auf
**848 Tests**. `.frm`-/`.ctl`-Designerhüllen werden jetzt offsettreu geparst, verschachtelte
Controls und Control-Arrays werden in die Klassenfelder übernommen; die Legacy-Schreibweise
`controls.LBound`/`controls.UBound` wird als Array-Bound gebunden, und ein späteres `End` im
Quellcode wird nicht mehr als Designerabschluss diagnostiziert. `.frx`-Verweise behalten
ihren aufgelösten Pfad sowie Hex-Offset. `Type=OleDll`, `Type=OleExe`, `Type=ActiveX EXE` und `Type=Control` werden dabei als
Managed-Libraries ohne `Sub Main`
emittiert. EXE-Projekte mit `Startup="FormName"` erhalten einen generierten Einstiegspunkt, der
die erzeugte Formklasse konstruiert; Fenstererzeugung, Message Loop und OCX-Hosting bleiben
Host-/Interop-Aufgaben. `PropertyPage`- und `UserDocument`-Quellen werden ebenfalls als
Projektklassen analysiert und in Managed-Libraries aufgenommen; vollständiger
ActiveX-/COM-Server- und Typbibliotheksimport bleiben separate Kompatibilitätsstufen.

Variant-Objektindizes verwenden nun den bestehenden Managed-Dispatch auch dann, wenn der
Empfänger erst zur Laufzeit als Objekt bekannt ist: `value(index)` bleibt für echte `IVBArray`-
Werte ein Arrayzugriff und fällt für Objekte auf `Item`-Get/Let zurück. Die Suite umfasst damit
**855 Tests**; COM-Default-Member werden für echte COM-Objekte nun über `DISPID_VALUE` aufgelöst,
Windows-TypeLib-Records wie `GUID` und `EXCEPINFO` werden mit skalaren Feldern in den Managed-
UDT-Pfad übernommen, und COM-RCW-Identität wird über `IUnknown` verglichen; die vollständige
Dispatch-ABI bleibt offen.

`.vbg`-Gruppen schreiben ihre Managed-Artefakte jetzt mit dem passenden Zieltyp: `Type=Exe`-
Projekte erhalten `.exe`, Bibliotheksprojekte `.dll`. Die Abhängigkeitsreihenfolge und die
expliziten Einzelprojekt-Ausgabepfade bleiben unverändert.

Variant-Indizes behalten nun ihren ursprünglichen Ausdruckstyp: echte Variant-Arrays konvertieren
ihre Subscripte weiterhin nach `Long`, während Objekt-Default-Properties auch String-Schlüssel
über den Managed-Dispatch erhalten. Echte COM-Objekte verwenden für Defaultzugriffe zuerst den
leeren Dispatch-Namen (`DISPID_VALUE`); der ByRef-Writeback und die übrige COM-ABI bleiben offen.

Statisch deklarierte `Object`-Empfänger nutzen denselben dynamischen `Item`-Default-Property-
Vertrag wie `Variant`: String-Indizes werden gebunden, an den Managed-Dispatch weitergereicht
und können gelesen sowie geschrieben werden. Die direkte COM-Aktivierung und vollständige
`IDispatch`-Default-Member-Ermittlung für COM-Objekte ist über `DISPID_VALUE` angebunden; vollständige
COM-ByRef-/Event- und Aktivierungsregeln bleiben offen.

VB6-`VB_UserMemId = 0`-Namen werden für erzeugte Klassen nun als CLR-
`DefaultMemberAttribute` emittiert. Dadurch verwenden late-bound `Variant`- und `Object`-
Zugriffe auch benannte Default-Properties wie `Text(...)`; die vollständige COM-Dispatch-ABI
bleibt separat offen.

## Aktueller CLI-Legacy-Nachtrag

Der Managed-CLI-Pfad kompiliert nun reale Legacy-Projekte über beide Projektcontainer: Für
`conformance/VISIA/4.8.7.1/prjVisia.vbp` liefert `vb6c ... --report` 40 von 40 fehlerfrei
analysierte Projektitems, und `vb6c ... --emit-assembly` erzeugt erfolgreich die Managed-
Anwendung samt PDB, Runtime-DLL und Runtime-Konfiguration. Dabei werden unter anderem lokale
Konstanten in `Static`-Arraygrenzen, Klassen-/Formfelder, scoped `Declare`/P/Invoke-Verträge,
UDT- und Hosttypen in nativen Signaturen sowie `Font`/`StdFont`-Erzeugung berücksichtigt.
Projekt-, Designer- und Quelltextdateien akzeptieren UTF-8/UTF-16-BOMs und verwenden für ältere
VB6-ANSI-Dateien einen Windows-1252-Fallback. `.vbg`-Batch-Emission bleibt über die bestehende
Abhängigkeitsreihenfolge und die getrennte
Ausgabe von `.exe`-/`.dll`-Projekten regression-getestet; ausführbare Projekte verwenden dabei
bevorzugt den Legacy-Namen aus `ExeName32` und fallen auf `Name=` zurück. Die Gesamtsuite umfasst
**886 Tests**.
`--report` gibt Projekt- und Quelldiagnosen bei Fehlern auf `stderr` aus und liefert dann einen
Fehler-Exitcode statt eines erfolgreichen Status. Zwei Prozessregressionen prüfen sowohl ein
fehlerhaftes `.vbp` als auch die echte `.vbg`-Batch-Emission über den CLI-Prozess. Die Gesamtsuite
umfasst **886 Tests**.

Die Managed-CLI akzeptiert für Einzelprojekte und `.vbg`-Batch-Emission zusätzlich `--x86`,
`--x64` und `--anycpu`. Die Auswahl wird bis in den PE-Header durchgereicht; `--x86` setzt für
Legacy-OCX-/ActiveX-Projekte `Machine.I386` und `Requires32Bit`, ohne die projektabhängige
Ausgabeentscheidung zwischen `.exe` und `.dll` zu überschreiben. Der CLI-Prozesspfad ist dafür
regression-getestet. Die Gesamtsuite umfasst **889 Tests**.

Legacy-Designerprojekte werden jetzt ebenfalls über den CLI-Projektpfad kompiliert: `Designer=`-
Einträge mit der historischen `DesignerType; Datei.dsr`-Form werden in Designer-Typ und echten
relativen Quellpfad aufgeteilt. `.dsr`-Quellen werden als klassenartige Projektquellen normalisiert,
analysiert und in Managed-Libraries emittiert. Das ergänzt die vorhandene Unterstützung für
`.frm`, `.ctl`, `.pag` und `.dob`; die eigentliche Designer-/OCX-Laufzeit bleibt davon getrennt.
Die drei Regressionen erhöhen die Gesamtsuite auf **902 Tests**.

Der gleiche Architekturvertrag gilt jetzt auch für direkte `.bas`-Emission; `--x64` erzeugt
einen PE-Header mit `Machine.Amd64`, während die Projekt- und Gruppenpfade weiterhin ihre
projektabhängige `.exe`-/`.dll`-Ausgabe beibehalten. Die Gesamtsuite umfasst **890 Tests**.

Variant-Exponentiation verwirft nun ebenfalls nichtdarstellbare `Double`-Ergebnisse als
Overflow, statt `Infinity` in ein laufendes VB6-Programm durchsickern zu lassen. Die Regression
ist direkt im Runtime-Vertrag abgesichert; die Gesamtsuite umfasst **891 Tests**.

Offen bleiben die vollständige Forms-/OCX-Hostlaufzeit, COM-ByRef-/Event-ABI und die weitere
Abdeckung von Legacy-Projektsonderfällen.

## Aktueller Forms-Host-Nachtrag

Der Managed-Form-Startup erzeugt Designer-Controls jetzt über einen expliziten portablen
`IVB6Host`-Vertrag und ruft für die gehaltene Startup-Instanz `Load` sowie `Show` auf. Ohne
Host bleibt der Compiler headless lauffähig und verwendet `VBControlProxy`-Objekte. Der optionale
`VB6.Runtime.WinForms`-Adapter erzeugt Standard-WinForms-Controls, löst Designer-Namen auf,
überträgt `Caption`/`Text`, `Visible`, `Enabled`, Position und Größe in VB6-Twips, OLE-Farben,
Fonts und Handles und führt `Unload`/`DoEvents` aus. Konventionelle Handlernamen wie
`Text1_Change` werden an WinForms-Events angebunden; explizite `VBEvents.SubscribeMethod`-
Abonnements werden bei Reassignment wieder entfernt. Portable Runtime-, Compiler-E2E- und STA-
WinForms-Regressionen sichern diesen Umfang ab. Der häufige Standard-Event-Satz für Controls und
Forms (`MouseDown`/`MouseUp`/`MouseMove`, `KeyDown`/`KeyPress`/`KeyUp`, `Resize`, `Click`,
`Change`, Fokus und Doppelklick) wird auf VB6-Button-/Shift-/Key-/Twips-Argumente abgebildet.
Vollständige `.frx`-Ressourcendekodierung, vollständige MDI-Fenster-/Menüverwaltung,
UserControl-/OCX-Hosting und COM-Connection-Points bleiben nachgelagerte Roadmap-Blöcke.
Verschachtelte Designer-Controls werden über qualifizierte Namen nun in ihre Parent-Container
registriert; die Regression deckt sowohl IR-Erzeugung als auch die konkrete WinForms-Hierarchie ab.

Der erweiterte Event-Adapter ist mit echten WinForms-Events für Maus-, Tastatur- und Form-Resize-
Argumente regressionsgesichert. Die Gesamtsuite umfasst **892 Tests**.

## Aktueller Variant-Nachtrag

`Sgn` ist als Variant-Intrinsic typisiert und bewahrt nun `Null`, während `Empty` weiterhin als
numerische Null behandelt wird. `Int` prüft Missing-/Array-Zustände, bewahrt `Null` und nutzt die
zentrale Variant-Konversion für Date-/Currency-/Boolean-Werte. Die Verträge laufen durch Symbolik,
Managed-Emission und Runtime-Regressionen. Variant-`/` liefert für Byte-/Integer-/Single-Paare
nun `Single`, für Decimal-Beteiligung `Decimal` und sonst `Double`; überlaufende `Single`-
Ergebnisse aus `+`, `-`, `*` und `/` werden auf `Double` hochgestuft, Integer-/Long-Negationen
wechseln bei `MinValue`-Überlauf ebenfalls auf die nächste darstellbare Breite, und Variant-
`Double`-Überläufe werden bei `+`, `-`, `*` und `/` als Fehler abgelehnt. Die vollständige VB6-
Promotionstabelle sowie Objekt- und Array-Varianten bleiben offen. Die Gesamtsuite umfasst
**887 Tests**.

## Aktueller VBG-Diagnostik-Nachtrag

`StartupProject=` wird nun gegen die tatsächlich deklarierten `.vbp`-Einträge aufgelöst.
Fehlende oder falsch geschriebene Startprojekte erzeugen `VB6VBG0007`, verhindern die Batch-
Emission und liefern über den CLI-Report einen Fehler-Exitcode. Der Prozesspfad ist mit einer
echten `.vbg`-Regression abgesichert. Die Gesamtsuite umfasst **888 Tests**.

## Aktueller LSP-Navigations-Nachtrag

Der LSP liefert neben Compilerdiagnosen nun echte Completion-, Go-to-definition- und
Dokument-Symbol-Antworten. Die Antworten werden direkt aus dem bestehenden Syntaxbaum erzeugt,
berücksichtigen modulare Sub-/Function-/Property-/Event-/Declare-/Enum-/Type-/Const- und
Variablendeklarationen und ergänzen eine kleine Liste häufig genutzter VB6-Intrinsics. Wortpräfixe
und Cursorpositionen werden als LSP-Zeilen-/Spaltenpositionen aufgelöst; `didClose` entfernt
Dokumente wieder aus dem Serverzustand. Der vollständige JSON-RPC-Pfad ist mit einer Regression
für Completion, Definition und Dokument-Symbole abgesichert. Die Gesamtsuite umfasst
**897 Tests**. Vollständige Typermittlung, projektübergreifende Definitionen und semantisch
kontextabhängige Completion bleiben nachgelagerte Visual-Studio-Integrationsschritte.

## Aktueller COM-Event-Nachtrag

`VBEvents.SubscribeMethod` verbindet neben dem portablen Host-Hook nun auch CLR-Events und COM-
RCWs. Importierte `FSOURCE`-Events tragen ihre Source-Interface-IID und DISPID aus der TypeLib
bis in die generierte `WithEvents`-Subscription; auf Windows nutzt die Runtime dafür
`ComEventsHelper`, bildet die Handlerparameter dynamisch ab und entfernt die Verbindung bei
Reassignment. Ein dynamischer Delegate-Adapter packt die Eventargumente in den bestehenden
VB6-Handlervertrag und schreibt geänderte `ByRef`-Argumente zurück. Der Umfang ist mit normalen
CLR-Events, einem echten `ByRef`-Event und dem Windows-`stdole2.tlb`-Import regressionsgesichert.
Raw-`IDispatch`-ABI-Aufrufe, COM-Server-Registrierung und native ABI-Marshalling bleiben offen.

Der Windows-RCW-Pfad deckt nun zusätzlich case-insensitive Automation-Methoden und Properties
sowie Default-`Item`-Get/Let über `DISPID_VALUE` ab. Der Umfang ist mit `Scripting.Dictionary`
gegen einen realen COM-Server regressionsgesichert; rohe `IDispatch::Invoke`-Strukturen,
COM-ByRef-Variant-Marshalling und Server-Registrierung bleiben separate ABI-Schritte. Der
kompilierte VB6-`CreateObject`-Pfad ist mit Methoden-, Property- und Default-Indexer-Zugriff auf
`Scripting.Dictionary` end-to-end abgesichert. Die Gesamtsuite umfasst **904 Tests**.

## Aktueller VB6-Variant-Mod-Nachtrag

Der `Mod`-Operator folgt für `Single`, `Double` und `Decimal` nun der klassischen
VB6/VBA-Regel: Fließkommawerte werden vor der Restbildung zu Ganzzahlen gerundet, und das
Ergebnis bleibt ein Long-artiger Variant-Wert. Die Regression deckt die historischen Beispiele
`12 Mod 4.3 = 0`, `12.6 Mod 5 = 3` sowie den kompilierten Variant-Ausführungspfad ab. Die
Gesamtsuite umfasst **906 Tests**.

## Aktueller VBG-Referenznachtrag

Die `.vbg`-Emission validiert nun auch den tatsächlichen Lauf eines Consumers gegen eine zuvor
emittierte referenzierte VB6-Klassenbibliothek. Externe Klassenmember verwenden dabei denselben
Managed-Namen wie ihre Library-Definitionen (`__vb6_...`), sodass Projektgruppen mit
`Reference=...; Shared.vbp; ...` nicht nur in Dependency-Reihenfolge gebaut werden, sondern auch
zur Laufzeit aufgelöst werden. Der vollständige CLI-Pfad ist mit einem gestarteten Consumer
regressionsgesichert. Die Gesamtsuite umfasst **908 Tests**.

## Aktueller MSBuild-SDK-Nachtrag

Der SDK-Targetvertrag arbeitet nun inkrementell: Neben der `.vbp` werden die Legacy-Quellen
`.bas`, `.cls`, `.frm`, `.ctl`, `.pag`, `.dob` sowie `.frx`-/`.res`-Designerressourcen als
Inputs registriert. Assembly, PDB, Runtimeconfig und `VB6.Runtime.dll` werden als Outputs verfolgt;
unveränderte Projekte werden von MSBuild übersprungen, während eine geänderte Quell- oder
Designerdatei eine neue CLI-Emission auslöst. Der SDK bleibt ein dünner Buildadapter und ersetzt
nicht das `.vbp`-Projektmodell oder den späteren Visual-Studio-Designer. Direkte CLI-Aufrufe über
`vb6c <projekt.vbp> --emit-assembly <ausgabe>` bleiben der primäre, unabhängig nutzbare Vertrag.
Das Release-Paket wurde mit `dotnet pack src/VB6.Compiler.Sdk/VB6.Compiler.Sdk.csproj -c Release
--no-restore` erzeugt und enthält `Sdk/Sdk.props`, `Sdk/Sdk.targets`, README, Nuspec und die
`net10.0`-SDK-Assembly.

## Aktueller Standard-Control-Nachtrag

Der WinForms-Host deckt nun auch häufige Legacy-Controlmember ab: `ListBox` und `ComboBox`
unterstützen `AddItem`, `RemoveItem`, `Clear`, die indizierte `List`-Property sowie `ListCount`
und `ListIndex`; `TextBox` unterstützt `SelStart`, `SelLength` und `SelText`; `CheckBox` und
`OptionButton` stellen `Value` bereit. Die Verträge laufen durch den bestehenden Twips-/Late-
Bound-Hostpfad und sind mit einer STA-Regression für Einfügen, Ersetzen, Entfernen, Auswahl und
Textselektion abgesichert. Vollständige OCX-Memberbibliotheken, MDI und UserControl-Hosting
bleiben separate Forms-/Interop-Schritte. `Timer` wird als eigener unsichtbarer WinForms-Host-
Control mit `Interval`, `Enabled` und konventionellem `TimerName_Timer`-Handler verdrahtet.
Die Gesamtsuite umfasst **899 Tests**.

## Aktueller Conditional-Compilation-Nachtrag

Die Managed-Compilation wertet jetzt `#Const`, verschachtelte `#If`-/`#ElseIf`-/`#Else`-/
`#End If`-Blöcke und die gängigen `VBA6`-/`VBA7`-/`VBA`-/`Win16`-/`Win32`-/`Win64`- sowie
Mac-Plattformkonstanten vor Parser und Binder aus. Inaktive Zeilen bleiben durch
positionsstabile Leerzeichen und Zeilenumbrüche im Quelltext erhalten; fehlerhafte oder nicht
abgeschlossene Blöcke liefern datei- und zeilenbezogene `VB6CC`-Diagnosen. Der gleiche Vertrag
gilt für direkte `.bas`-Emission und echte `.vbp`-Projektquellen, einschließlich Designer-
Klassenmodulen. Die expliziten CLI-Ziele `--x86` und `--x64` werden bis in die
Conditional-Compilation-Konstanten durchgereicht, sodass `Win64` nicht mehr versehentlich aus
der Breite des Compilerprozesses gewählt wird; `Win32` bleibt dabei auch auf Win64 wahr. Die
vollständige Release-Suite umfasst **914 Tests**; der VISIA-CLI-Report analysiert weiterhin 40 von
40 Projektitems ohne Fehler.

Projektweite `CondComp=`-Einträge aus `.vbp`-Dateien werden zusätzlich verlustfrei geladen und
als globale Conditional-Compilation-Konstanten vor den moduleigenen `#Const`-Definitionen in
den jeweiligen Projektquellen ausgewertet. Ungültige Projektwerte erzeugen `VB6CC0007`; die
Abhängigkeit wird auch bei referenzierten `.vbp`-Projekten separat pro Projekt angewendet.

Der `Format$`-Stringmaskenpfad unterstützt nun neben `<`/`>` auch `@`- und `&`-Platzhalter,
`!`-gesteuertes Füllen von links nach rechts, das klassische VB6-Füllen von rechts nach links
und die zweite Maskensektion für leere beziehungsweise `Null`-Strings. Die direkte Runtime-
und kompilierte Ausführung ist regressionsgesichert; locale-abhängige Named-Formate und weitere
Datum-/Finanzmasken bleiben separat offen. Die Gesamtsuite umfasst **915 Tests**.

## Aktueller Declare-UDT-Nachtrag

Blittable `Type`-Records werden im Managed-Emitter jetzt als sequenzielle Structs in nativen
`Declare`-Signaturen verwendet und erhalten explizit das für den VB6-UDT-Pfad erforderliche
4-Byte-Packing. Echte Windows-Aufrufe von `GetSystemTime`, `GetVersionExA` und
`RtlMoveMemory` regressionssichern den vollständigen `ByRef`-Pfad einschließlich Feld-Write-back,
`Byte`-/`Double`-Alignment sowie feste `String * n`-Felder über `BYVALTSTR`/`SizeConst`. Variable
Stringfelder, Arrays, nicht-blittable UDTs und Callback-Delegates bleiben separate ABI-Schritte.
Die Gesamtsuite umfasst **919 Tests**.

## Aktueller UDT-Len-Nachtrag

`Len` erkennt emittierte VB6-UDTs über ihren Managed-Namespace und fragt ihren nativen
Struct-Umfang über `Marshal.SizeOf` ab. Dadurch liefert ein `Byte`-/`Double`-Record mit VB6-
4-Byte-Packing `12` statt der CLR-defaulteten Ausrichtung; feste `String * n`-Felder werden
über ihre `BYVALTSTR`-Metadaten ebenfalls korrekt berücksichtigt. Die direkte Managed-Ausführung
ist mit zwei End-to-End-Tests regressionsgesichert. Die Gesamtsuite umfasst **921 Tests**.

## Aktueller Declare-Stringpuffer-Nachtrag

Variable `ByVal String`-Parameter werden im Managed-P/Invoke als ANSI-`StringBuilder` emittiert.
Aufrufseitig addressierbare VB6-Strings werden nach dem nativen Aufruf per `ToString()` in ihr
ursprüngliches Ziel zurückgeschrieben; Rückgabewerte von Funktionen mit gleichzeitigem Puffer-
Write-back bleiben über Compiler-Temporaries erhalten. `GetComputerNameA` ist als echter Windows-
End-to-End-Aufruf regressionsgesichert. Array-Marshalling, nicht-blittable UDTs und Callback-
Delegates bleiben separate ABI-Schritte. Die Gesamtsuite umfasst **918 Tests**.

## Aktueller LenB-Nachtrag

`Len` und `LenB` verwenden jetzt Variant-Rückgaben, sodass `Null` gemäß dem VB6-Vertrag erhalten
bleibt. `LenB` ist als eigene Intrinsic-Signatur durch Binder, IR, Managed-Emitter und Runtime
verdrahtet: Unicode-Strings liefern zwei Bytes je UTF-16-Codeeinheit, Scalar-Varianten behalten
ihre VB6-Speicherbreite, und emittierte UDTs verwenden den nativen In-Memory-Umfang einschließlich
Padding. Die direkte Ausführung ist mit String-, Scalar-, `Null`- und UDT-Fällen regressions-
gesichert. Die Gesamtsuite umfasst **924 Tests**.

## Aktueller CommonDialog-Nachtrag

Der WinForms-Host behandelt `MSComDlg.CommonDialog` jetzt als nichtvisuelle Komponente statt als
unbekanntes `Panel`. `FileName`, `Filter`, `DialogTitle`, `FilterIndex`, `CancelError` und
`DefaultExt` werden über einen Managed-Adapter bereitgestellt; `ShowOpen` und `ShowSave` nutzen
die nativen WinForms-Dateidialoge und übernehmen den ausgewählten Dateinamen zurück in den
VB6-Objektvertrag. Die Komponente bleibt aus der visuellen Control-Hierarchie heraus, ist aber
über die bestehende Form-/Control-Namensauflösung und den Late-Bound-Dispatch erreichbar.
Vollständiges ActiveX-OCX-Hosting, insbesondere die echte `MSComDlg`-Typbibliothek und deren
gesamte Ereignis-/ABI-Oberfläche, bleibt separat offen. Die Gesamtsuite umfasst **925 Tests**.

## Aktueller TreeView-Nachtrag

Der WinForms-Host stellt `MSComctlLib.TreeView.Nodes` jetzt als Managed-Adapter bereit. Der
Adapter unterstützt den VB6-Aufruf `Nodes.Add` mit `Relative`, `Relationship`, `Key`, `Text`,
`Image` und `SelectedImage`, einsbasierte numerische oder schlüsselbasierte `Item`-Auflösung,
`Remove`, `Clear`, `Count` sowie `Node`-Properties für `Key`, `Text`, `Index`, `Expanded`,
`Image`, `SelectedImage`, `Selected` und `Parent`. `Style` und `LineStyle` werden am TreeView
hostseitig gespeichert, ohne die native WinForms-Control-Hierarchie zu verfälschen. Die Regression
läuft durch den bestehenden Late-Bound-Dispatch und prüft Parent/Child-Aufbau, Bilder,
einsbasierte Indizes, Text-Writeback und Entfernen. Vollständiges ActiveX-Hosting, ImageList-
Ressourcendekodierung und die übrige COM-Connection-Point-ABI bleiben offen. Die Gesamtsuite
umfasst **926 Tests**.

## Aktueller ImageList-/ImageCombo-Nachtrag

Der WinForms-Host behandelt `MSComctlLib.ImageList` nun als nichtvisuelle Komponente mit
`ListImages`, einsbasierter beziehungsweise schlüsselbasierter `Item`-Auflösung, `Add`, `Remove`,
`Clear`, `Count`, `Key`, `Index`, `Picture`, `ImageWidth`, `ImageHeight` und `hImageList`.
`MSComctlLib.ImageCombo` verwendet eine echte WinForms-ComboBox mit einem Managed-
`ComboItems`-Adapter für `Add`, `Remove`, `Clear`, `Count`, `Item`, `Key`, `Index`, `Text`,
`Selected` und `Image`; die `ImageList`-Verknüpfung bleibt als Objektbeziehung erhalten. Die
Regression prüft den Late-Bound-Collection-Pfad, Dateibild-Metadaten, einsbasierte Indizes,
Auswahl und die Verknüpfung beider Controls. Native OCX-Rendering, `.frx`-Dekodierung und
vollständiges ActiveX-/Connection-Point-Hosting bleiben offen. Die Gesamtsuite umfasst
**927 Tests**.

## Aktueller Generated-Assembly-Runner-Nachtrag

`VB6.Runtime.WinForms.Runner` ergänzt den Compiler um einen separaten Startvertrag für erzeugte
Form-Assemblies. `GeneratedApplicationRunner` lädt den Entry-Point auf einem STA-Thread,
installiert den `WinFormsHost` nur für diesen Prozess und startet nach `Load`/`Show` die
WinForms-Message-Pump. Reine `Sub Main`-Assemblies können denselben Runner verwenden und kehren
ohne Formularschleife zurück. Dadurch bleibt die Compiler-Assembly headless und von Visual Studio
oder einem anderen Host aufrufbar, während eine erzeugte Form-Anwendung direkt mit
`dotnet run --project src/VB6.Runtime.WinForms.Runner -- <assembly.exe>` gestartet werden kann.
Die Regression prüft den Launcher-Fehlervertrag für fehlende Assemblies; vollständige Form-
End-to-End-Läufe mit echten OCX-Abhängigkeiten bleiben separat. Die Gesamtsuite umfasst
**928 Tests**.

## Aktueller RichTextBox-Host-Nachtrag

Der Managed-WinForms-Host bildet für `RichTextLib.RichTextBox` nun den häufigen VB6-Vertrag
für `TextRTF`, `SelStart`, `SelLength`, `SelText`, `SelColor`, `SelBold`, `SelItalic` und
`SelUnderline` ab. `FileName`, `Modified`, `RightMargin`, `HideSelection` und
`GetLineFromChar` sind ebenfalls verdrahtet; `LoadFile` und `SaveFile` akzeptieren den
optionalen `rtfRTF`-/`rtfText`-Dateityp und führen PlainText-Zeilenenden am Host auf VB6-`CRLF`
zurück. Die Regression nutzt den echten Late-Bound-Hostpfad und prüft Auswahlformatierung,
RTF-Roundtrip, Zeilenauflösung sowie Textdatei-Laden/Speichern. Vollständige RichTextLib-OCX-
ABI- und native Connection-Point-Kompatibilität bleiben offen. Die Gesamtsuite umfasst
**929 Tests**.

## Aktueller FRX-Ressourcen-Nachtrag

`VBDesignerParser` erkennt nun auch die VB6-Designerform `TextRTF = $"file.frx":offset`.
`VBFrxResourceReader` validiert den little-endian 32-Bit-Längenpräfix am Offset, prüft die
Dateigrenze und stellt die folgenden Nutzdaten als `VBDesignerProperty.ResourceData` bereit.
Die Bytes bleiben bewusst opaque: RTF-, Bild-, Icon- und OCX-spezifische Interpretation gehört
in den jeweiligen Hostadapter und wird nicht durch eine unsichere Universaldecodierung ersetzt.
Fehlerhafte vorhandene Ressourcen erzeugen `VB6FRX0001` als Warnung, während fehlende optionale
Designerdateien für reine Analysepfade weiterhin diagnostikfrei bleiben. Die Gesamtsuite umfasst
**931 Tests**.

## Aktueller Designer-Initialisierungs-Nachtrag

Designerwerte für `Caption`, `Text`, Sichtbarkeit, Aktivierung, Position, Größe, Farben,
`RichTextBox`-Auswahl und `Timer.Interval` werden nun beim generierten Form-Konstruktor nach der
Control-Erzeugung als explizite `InteractionSetMember`-Aufrufe emittiert. Der portable Runtime-
Vertrag reicht diese Werte an den konfigurierten Host weiter; der WinForms-Host setzt sie über
Twips-, OLE-Farb- und RichTextBox-Konvertierungen. Nicht skalare oder noch opaque Ressourcenwerte
bleiben bewusst beim jeweiligen Hostadapter. Die IR-Regression prüft den Designer-Property-
Namen und den emittierten Wert; die Gesamtsuite bleibt bei **931 Tests**.

## Aktueller Forms-Designerwert-Nachtrag

Der generierte Form-Konstruktor setzt nun zusätzlich häufige skalare Designerwerte für das
Root-Form und Standardcontrols. Dazu gehören Form-Rahmen, `ControlBox`, Min-/Max-Button,
`ShowInTaskbar`, `StartUpPosition`, `WindowState`, `BorderStyle`, `Appearance`, `Tag`,
`ToolTipText` sowie die hostseitig gespeicherten VB6-Zustände `AutoRedraw`, `FillStyle`,
`MousePointer` und `ScaleMode`. `ImageList.ImageWidth`/`ImageHeight` und die skalaren
`CommonDialog`-Eigenschaften werden ebenfalls über den Managed-Host abgebildet. Der Parser
ignoriert dabei Inline-Kommentare außerhalb von Zeichenketten, wie sie in älteren `.frm`-Dateien
häufig vorkommen. Die VISIA-Emission und der native WinForms-Runner laufen damit ohne Analyse-
oder Startfehler; direkte Ausführung der erzeugten Managed-PE bleibt bis zur separaten AppHost-
Erzeugung auf `dotnet` beziehungsweise den Runner angewiesen. Die Gesamtsuite umfasst
**932 Tests**.

## Aktueller FRX-Bild-Nachtrag

`.frx`-Ressourcen für Form-/Control-`Picture` und Form-`Icon` werden nun als transportierbare
Werte in den generierten Form-Konstruktor übernommen. Der WinForms-Host entpackt die historische
VB6-StdPicture-Hülle und dekodiert BMP-/ICO-Payloads für `PictureBox`, `Image` und Form-Hintergrund
bzw. Form-Icon. Der Pfad bleibt absichtlich auf die intrinsischen Bildmember begrenzt; die
ressourcenbasierte `ImageList`-Einträge werden nun ebenfalls in den Managed-Adapter übernommen;
OCX-eigenes Rendering und vollständige OLE-Picture-Konvertierung folgen in separaten Host-/
ActiveX-Slices. Die VISIA-Emission wurde erneut erzeugt
und im STA-Runner ohne Ausnahme oder Messagebox gestartet. Die Gesamtsuite umfasst **935 Tests**.

## Aktueller ImageList-FRX-Nachtrag

Verschachtelte `BeginProperty Images`-/`ListImageN`-Blöcke werden nun als Designer-Initialisierer
für `MSComctlLib.ImageList` erkannt. `ListImageN.Picture` dekodiert die eingebettete BMP-/ICO-
StdPicture-Payload, `ListImageN.Key` erhält den Legacy-Schlüssel, und fehlende Zwischenindizes
werden einsbasiert im Managed-Collection-Adapter angelegt. Die Bildobjekte bleiben bewusst im
Managed-Vertrag; eine echte native `ImageList`-Zuordnung zu OCX-Controls und deren Rendering
bleibt ein separater ActiveX-Host-Schritt. Die Regression deckt sowohl den verschachtelten
Designerpfad als auch den bestehenden `ListImages`-Late-Bound-Vertrag ab. Die Gesamtsuite
umfasst **935 Tests**.

## Aktueller Shape-/Line-Forms-Nachtrag

`VB.Shape` und `VB.Line` werden im Managed-WinForms-Host nicht mehr als generische Panels
angelegt. `Shape` rendert Rechteck, Quadrat, Oval, Kreis und abgerundete Varianten mit
`BackColor`, `FillColor`, `FillStyle`, `BackStyle`, `BorderColor` und `BorderWidth`; `Line`
zeichnet seine Endpunkte über die VB6-Twips-Konvertierung aus `X1`, `Y1`, `X2` und `Y2`.
Die Designer-Allowlist übernimmt diese Werte in den generierten Formkonstruktor, und die
Regression prüft sowohl die IR-Emission als auch gerenderte Pixel im STA-Host. Native
Zeichen-APIs wie `PaintPicture`, vollständige AutoRedraw-/DrawMode-Semantik und MDI bleiben
separate Forms-Schritte. Die Gesamtsuite umfasst **938 Tests**.

## Aktueller Menu-Forms-Nachtrag

Verschachtelte `VB.Menu`-Designerobjekte werden jetzt mit ihrem ursprünglichen Typnamen bis zur
IR-Emission erhalten und im WinForms-Host als echter `MenuStrip`-/`ToolStripMenuItem`-Baum
angelegt. `Caption`/`Text`, `Visible`, `Enabled`, `Checked`, `Index`, `Tag` und `Shortcut`
laufen über den bestehenden Late-Bound-Hostvertrag; Parent-Menüs werden anhand des qualifizierten
Designerpfads verbunden, und `MenuName_Click`-Handler werden an `ToolStripMenuItem.Click`
angeschlossen. Die Regression deckt Designer-Emission, Hierarchie und Event-Auslösung ab.
Separator-Semantik, vollständige VB6-Shortcut-Konvertierung, `PopupMenu` und MDI-Menüs bleiben
separate Forms-Schritte. Die Gesamtsuite umfasst **938 Tests**.

## Aktueller Managed-AppHost-Nachtrag

Windows-Anwendungen, die mit `--emit-assembly <name>.exe` ausgegeben werden, erhalten nun neben
der Managed-Companion-Assembly `<name>.dll` einen echten nativen .NET-AppHost. Die Ausgabe kann
dadurch direkt gestartet werden; die frühere `System.Private.CoreLib, Version=10.0.0.0`-
Ladeexception durch eine Managed-Assembly mit `.exe`-Endung entfällt. Der WinForms-Runner erkennt
beide Ausgabeteile und lädt automatisch die Managed-Assembly, sodass bestehende Runner-Aufrufe
weiter funktionieren. Der direkte AppHost bleibt headless; sichtbare Formulare laufen weiterhin
über den optionalen `VB6.Runtime.WinForms.Runner`. Die CLI-Regression startet `.vbp`-/`.vbg`-
Ausgaben direkt und prüft die Architektur der Companion-Assembly. Die AppHost-Auswahl bevorzugt
jetzt die zur erzeugenden Runtime passende Major-/Minor-Version und sortiert verbleibende Packs
numerisch statt lexikografisch, damit ein installiertes `10.x` nicht versehentlich mit einem
`8.x`-AppHost ausgegeben wird. Die Gesamtsuite umfasst **946 Tests**.

## Aktueller PopupMenu-Forms-Nachtrag

`VBInteraction.PopupMenu` delegiert nun an den konfigurierten `IVB6Host`. Der WinForms-Host baut
für ein `VB.Menu` einen separaten `ContextMenuStrip`-Snapshot auf, sodass der vorhandene
`MenuStrip`-Baum an Ort und Stelle bleibt. Verschachtelte Items, Separatoren, Sichtbarkeit,
Aktivierung, Checkzustand und Tags werden in den Snapshot übernommen; Popup-Klicks werden auf die
bereits am Original-Menü verdrahteten VB6-Handler weitergeleitet. Flags, vollständige
VB6-Shortcut-Konvertierung und MDI-Popup-Menüs bleiben weitere Kompatibilitätsschritte. Die
Regression prüft Delegation, Snapshot-Verhalten, Originalhierarchie und Handlerauslösung. Die
Gesamtsuite umfasst **939 Tests**.

## Aktueller GraphicsLine-Forms-Nachtrag

Der portable `IVB6Host`-Vertrag übernimmt nun `VBGraphicsLine`-Operationen vom Runtime-Pfad. Der
WinForms-Host zeichnet Linien sowie B-/F-Rechtecke persistent auf einer Bitmap-Oberfläche der
aktiven Form, übernimmt vorhandene Hintergrundbilder, interpretiert `Step`-Koordinaten und
skaliert VB6-Twips, Punkte und Pixel in die aktuelle DPI-Auflösung. Die Regression prüft echte
gerenderte Pixel und die Füll-/Rahmensemantik. Ein `PaintPicture`-Subset verarbeitet `Bitmap`-,
FRX- und dateibasierte `VBPicture`-Quellen auf derselben persistenten Oberfläche. Qualifizierte
`PictureBox.PaintPicture`- und `PictureBox.Line`-Aufrufe werden über den bestehenden late-bound
Control-Dispatch auf die PictureBox-Bitmap gerendert. MDI, DrawMode sowie vollständige
AutoRedraw-/ScaleMode-Regeln bleiben weitere Graphics-/Forms-Slices. Die Gesamtsuite umfasst
**945 Tests**.

## Aktueller UserControl-Host-Nachtrag

Der WinForms-Host erkennt bei einem unbekannten Designer-Controltyp eine parameterlose generierte
CLR-Klasse aus derselben Projektassembly, instanziiert sie und hostet ihre eigene Runtime-Bindung
als borderlose, eingebettete Formfläche. Dadurch können `.ctl`-Klassen als verschachtelte Managed-
Designerkomponenten geladen werden, ohne den Compilerkern an WinForms zu koppeln. Vollständige
UserControl-Ereignis-/PropertyBag-Semantik, ActiveX-Connection-Points und natives OCX-Hosting
bleiben bewusst separate Kompatibilitätsschritte. Der Host hält pro eingebetteter Instanz einen
`VBPropertyBag` und ruft `UserControl_ReadProperties` beim Einfügen sowie
`UserControl_WriteProperties` vor `UserControl_Terminate` auf. Die Regression umfasst den
Instanzierungs-, Komponenten-, PropertyBag- und Initialize-/Terminate-/Unload-Lifecycle. Die
Gesamtsuite umfasst **950 Tests**.

## Aktueller Form-Lifecycle-Nachtrag

Der WinForms-Host bindet nun `Form_Initialize`/`Form_Terminate` sowie
`Form_Activate`, `Form_Deactivate`, `Form_QueryUnload` und `Form_Unload` an den Managed-
Form-Lifecycle und die WinForms-Ereignisse `Activated`, `Deactivate`, `FormClosing` und
`FormClosed`. Die Initialisierung erfolgt pro Bindung genau einmal; der Terminate-Aufruf wird
auch beim Host-Dispose ausgeführt. Für
`Form_QueryUnload` werden `Cancel` und `UnloadMode` in den VB6-Argumentvertrag übersetzt; ein
geänderter `Cancel`-ByRef-Wert wird in `FormClosingEventArgs.Cancel` zurückgeschrieben. Die
Regression löst die geschützten WinForms-Ereignisse direkt aus und prüft Aktivierung,
Deaktivierung, Unload-Modus und Abbruchsemantik. MDI und vollständige OCX-/Connection-Point-
Integration bleiben weitere Forms-/Interop-Schritte. Die Gesamtsuite umfasst **950 Tests**.

## Aktueller MDI-Forms-Nachtrag

`VB.MDIForm`-Designerwurzeln werden als MDI-Containerinitialisierung in die Managed-IR übernommen.
`MDIChild=True` wird als Form-Designerwert gebunden; der WinForms-Host ordnet solche Child-Forms
automatisch dem registrierten MDI-Container zu und hält den Wert über den Host-Dispatch lesbar.
Die Regression deckt sowohl Designer-Emission als auch die konkrete Parent-/Child-Hierarchie ab.
Vollständige MDI-Fensterbefehle, MDI-Menüs und persistente Window-Management-Regeln bleiben offen.
Die Gesamtsuite umfasst **950 Tests**.

## Aktueller Native-OCX-/AppHost-Nachtrag

Der optionale `VB6.Runtime.WinForms.Runner` läuft standardmäßig als x86-Prozess, damit die auf
Legacy-Systemen üblichen 32-Bit-OCX-Dateien aus `SysWOW64` überhaupt geladen werden können. Für
registrierte `MSComctlLib`-Visual-Controls ohne bestehenden Managed-Adapter versucht der Host nun
eine echte `AxHost`-Aktivierung; fehlt die OCX oder ist sie für die andere Prozessarchitektur
registriert, bleibt der Managed-Fallback aktiv. TreeView, ImageList, ImageCombo, RichTextBox und
CommonDialog behalten ihre vorhandenen Managed-Adapter, weil diese bereits die benötigten VB6-
Objekt- und Collection-Verträge abbilden. Die Ausgabeerzeugung verweigert außerdem einen
scheinbaren `.exe`-Output, wenn kein passender nativer .NET-AppHost erstellt werden kann; eine
Managed-DLL wird nicht mehr als startbare `.exe` kopiert, wodurch die bekannte
`System.Private.CoreLib`-Ladeexception vermieden wird. VISIA wurde frisch mit `--x86` emittiert
und über den x86-Runner ohne unbehandelte Ausnahme gestartet. Die Gesamtsuite umfasst
**950 Tests**.

## Aktueller COM-Wrapper-Interop-Nachtrag

Native `AxHost`-Controls stellen nun über `IVBComObjectProvider` ihr zugrunde liegendes RCW für
den portablen Runtime-Kern bereit. `VBDynamicDispatch` leitet dadurch Late-Bound Methoden,
Properties und `DISPID_VALUE`-Defaultzugriffe auf das echte COM-Objekt weiter, ohne die
WinForms-Geometrie des Wrappers zu verlieren. Der bestehende `ComEventsHelper`-Pfad verwendet
für `WithEvents`-Subscriptions und deren Abmeldung ebenfalls das entpackte RCW; damit können
TypeLib-importierte Source-IIDs/DISPIDs auch für native OCX-Wrapper verwendet werden. Der
Regressionstest deckt diesen Vertrag mit einem realen `Scripting.Dictionary`-RCW ab. Die Runtime
stellt außerdem `VBEvents.UnsubscribeMethod` als explizite, quellenbezogene Abmeldung bereit;
ein `null`-Quellobjekt entfernt alle passenden Verbindungen. Raw-
`IDispatch`-ABI-Marshalling, vollständige OCX-Event-Signaturabdeckung und COM-Server-Emission
bleiben separate Interop-Schritte. Die Gesamtsuite umfasst **952 Tests**.

## Aktueller Native-OCX-Dispatch-Nachtrag

Der WinForms-Host leitet Memberzugriffe auf native `AxHost`-Controls jetzt nach den normalen
VB6-/WinForms-Sonderregeln direkt an das zugrunde liegende COM-RCW weiter. Damit funktionieren
auch COM-Properties und Methoden, die der CLR-Wrapper selbst nicht als Managed-Property anbietet.
Der x86-Test aktiviert die auf diesem Rechner registrierte `MSCOMCTL.OCX` als echtes
`MSComctlLib.ListViewCtrl.2`, setzt `View` und liest den Automation-Wert wieder aus. Die
64-Bit-Fallback-Regel bleibt aktiv, weil die 32-Bit-OCX dort trotz sichtbarer ProgID nicht
aktivierbar ist. Vollständiges `IDispatch`-ABI-Marshalling, native OCX-Events und die weiteren
MSComctl-/RichText-/CommonDialog-Oberflächen bleiben offen.

## Aktueller nativer RichText-Nachtrag

Der opt-in-Native-Pfad hostet `RichTextLib.RichTextBox` jetzt über `RICHTEXT.RichtextCtrl.1`,
wenn die 32-Bit-OCX im x86-Prozess aktivierbar ist. `TextRTF` wird dabei direkt über das COM-RCW
gelesen und geschrieben; der VISIA-Runner bleibt mit diesem Pfad ohne Ausnahme und ohne
Messagebox stabil. `MSComctlLib.TreeView` bleibt vorerst beim Managed-Adapter, da der native
`Nodes`-Collection-ABI noch nicht stabil genug für den Runner ist. Die vollständige native
TreeView-/ImageList-/ImageCombo-/CommonDialog-Oberfläche und ihre Event-ABIs bleiben offen.

## Aktueller Format-Nachtrag

`Format$` verarbeitet die VBA-Datums-Token `w` (Wochentag), `ww` (Kalenderwoche) und `q`
(Quartal) jetzt auch im vollständigen Managed-Compilerpfad. `FirstDayOfWeek` unterstützt die
VB6-Werte `vbUseSystem`/`vbSunday` bis `vbSaturday`; `FirstWeekOfYear` unterstützt
`vbUseSystem`/`vbFirstJan1`/`vbFirstFourDays`/`vbFirstFullWeek`. Die Woche wird mit dem
invariant-gregorianischen Kalender berechnet; `vbUseSystem` übernimmt die aktuellen
Culture-Einstellungen für Wochenbeginn und Wochenregel, systemabhängige Text-/Locale-Ausgabe
bleibt ein separater Schritt. Runtime- und E2E-Regressionen decken die Token und Parameter ab.
Die Gesamtsuite umfasst nun **954 Tests**.

## Aktueller Standard-OCX-Hosting-Nachtrag

Die auf dem Testsystem registrierten 32-Bit-Standard-OCX werden jetzt im x86-Runner konkret
aktiviert: `MSComctlLib.ImageListCtrl.2`, `ImageComboCtl.2`, `ListViewCtrl.2`, `ProgCtrl.2`,
`Slider.2`, `SBarCtrl.2`, `TabStrip.2`, `Toolbar.2` und `RICHTEXT.RichtextCtrl.1` laufen über
echte `AxHost`-Wrapper. `MSComDlg.CommonDialog.1` wird als nichtvisuelle COM-Komponente gehalten;
seine Properties werden direkt über das RCW gelesen und geschrieben. Die native Auswahl bleibt
opt-in und fällt bei fehlender Registrierung oder falscher Bitness auf die bestehenden Managed-
Adapter zurück. TreeView bleibt wegen des noch instabilen nativen `Nodes`-ABI bewusst beim
Managed-Adapter. Der x86- und der x64-WinForms-Testlauf umfassen jeweils **28 Tests**; die
Gesamtsuite liegt nun bei **954 Tests**. Native Connection-Point-Events, vollständiges
`IDispatch`-ByRef-Marshalling und die komplette TreeView-Collection bleiben offen.

## Aktueller Control-Array-Lifecycle-Nachtrag

`Load` und `Unload` auf einem bereits erzeugten WinForms-Control-Element erzeugen jetzt keine
künstliche Formbindung mehr. Der Host initialisiert bei `Load` den Control-Handle, macht das
Element sichtbar und blendet es bei `Unload` aus, ohne das Control oder seine Owner-Form zu
disponieren; Forms behalten ihren bisherigen Initialisierungs- und Terminierungsweg. Damit ist
der Lifecycle vorhandener Designer-Index-Elemente belastbar. Dynamische Neuerzeugung außerhalb
der statisch gebundenen Control-Array-Indizes, vollständige VB6-Recreate-/Dispose-Semantik und
die native `TreeCtrl.2`-`Nodes`-Collection bleiben separate Aufgaben. `TreeCtrl.2` ist auf dem
Testsystem nun ebenfalls registriert und als COM-Klasse aktivierbar, wird aber wegen dieses
instabilen nativen Collections-ABI weiterhin über den Managed-TreeView-Adapter gehostet. Die
x86- und x64-WinForms-Regression umfasst jeweils **29 Tests**; die Gesamtsuite umfasst nun
**955 Tests**.

## Aktueller nativer TreeView-/IDispatch-Nachtrag

Die registrierte `MSComctlLib.TreeCtrl.2` wird im opt-in-Native-Host jetzt als echter `AxHost`
aktiviert. Für das zugrunde liegende `Nodes`-RCW verwendet die Runtime eine direkte Windows-
`IDispatch`-Brücke vor dem CLR-Reflection-Fallback. Dadurch funktionieren im x86-Pfad
`Nodes.Count`, `Nodes.Add`, einbasierter `Nodes.Item`-Zugriff sowie Lesen und Schreiben der
Node-Properties, ohne den instabilen Reflection-Aufruf auf alten OCX-Collections auszulösen.
Der normale Host behält den portablen Managed-TreeView-Adapter; der native Pfad bleibt wegen
weiterer Event-, ByRef- und vollständiger ImageList-/ImageCombo-Verträge opt-in. Alle auf dem
Testsystem registrierten Standard-OCX bleiben architekturabhängig und benötigen den x86-Runner,
wenn nur die 32-Bit-Registrierung vorhanden ist. Die x86- und x64-WinForms-Regression umfasst
jeweils **31 Tests**; die Gesamtsuite umfasst nun **957 Tests**. Der direkte native AppHost-
Start der neu emittierten VISIA-Ausgabe endet ohne `System.Private.CoreLib`-Ladefehler; der
automatisierte Runner-Lauf bleibt in der nicht-interaktiven Testumgebung ohne sichtbaren
Fenster-Handle und muss für eine visuelle GUI-Abnahme in einer interaktiven Windows-Sitzung
geprüft werden.

## Aktueller nativer OCX-Objektübergabe-Nachtrag

Native OCX-Properties verwenden bei objektwertigen Zuweisungen jetzt den passenden
`PROPERTYPUTREF`-Vertrag und entpacken `IVBComObjectProvider`-Wrapper vor der VARIANT-
Marshalling-Grenze auf ihr zugrunde liegendes COM-RCW. Falls ein OCX die alternative
Automation-Konvention erwartet, wird mit `PROPERTYPUT` beziehungsweise `PROPERTYPUTREF`
erneut versucht, bevor der Reflection-Fallback greift. Der x86-Regressionspfad erzeugt ein
echtes `IPictureDisp`, fügt damit ein Bild in die native `ImageList.ListImages`-Collection ein
und weist anschließend die native ImageList der `ImageCombo.ImageList`-Property zu. Damit ist
die Objektübergabe zwischen zwei real aktivierten Standard-OCX abgesichert. Die x86- und
x64-WinForms-Regression umfasst weiterhin jeweils **31 Tests**; die Gesamtsuite bleibt bei
**957 Tests**. Vollständiges COM-ByRef-Marshalling, Connection-Point-Events und die restlichen
nativen ABI-Sonderfälle bleiben separate Roadmap-Schritte.

## Aktueller nativer OCX-Collections-Nachtrag

`For Each` über native Host-/OCX-Collections nutzt jetzt auch die reale RCW-Enumeration. Einige
ältere `IEnumVARIANT`-Implementierungen liefern hinter den gezählten Elementen noch einen
`VT_EMPTY`-Platzhalter; der Host verwirft diesen `null`-Eintrag für COM-Collections, ohne die
Enumeration normaler Managed-Collections zu verändern. Der x86-Regressionspfad legt einen
TreeView-Node über die native `Nodes`-Collection an und prüft, dass `VBInteraction.EnumerateControls`
genau diesen einen Node für den generierten `For Each`-Vertrag zurückgibt. Die x86- und x64-
WinForms-Regression umfasst weiterhin jeweils **31 Tests**; die Gesamtsuite bleibt bei
**957 Tests**. Vollständiges COM-ByRef-Marshalling und Connection-Point-Events bleiben offen.

## Aktueller TypeInfo-gesteuerter COM-ByRef-Nachtrag

Die Raw-`IDispatch`-Brücke liest vor einem Aufruf die `FUNCDESC`-/`PARAMDESC`-Metadaten der
TypeLibrary und setzt `VT_BYREF | VT_VARIANT` nur für Parameter mit `PARAMFLAG_FOUT`. Die nach
`Invoke` geänderten Werte werden in das ursprüngliche Late-Bound-Argumentarray zurückgeschrieben;
Parameter ohne `[out]`-Kennzeichnung bleiben als normale `VARIANTARG`-Werte geschützt. Falls ein
Server trotz TypeInfo den ByRef-Aufruf ablehnt, wird derselbe Aufruf nochmals vollständig ByVal
ausgeführt. Die bestehenden Scripting-Dictionary- und nativen x86-OCX-Regressionspfade bleiben
stabil. Vollständige `[in]`-/`[out]`-Typkonversion, SAFEARRAY-/UDT-ByRef-Marshalling und
Connection-Point-Events bleiben separate COM-ABI-Schritte.
