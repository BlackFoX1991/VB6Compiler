# Änderungshistorie

Chronologisches Arbeitsjournal des Compilers: was in welchem Schritt implementiert, gemessen
und regressionsgesichert wurde. **Älteste Einträge zuerst, neue kommen ans Ende.**

Dieses Dokument beschreibt ausdrücklich **nicht** den Ist-Stand. Jeder Eintrag war zum Zeitpunkt
seines Entstehens aktuell und ist es seitdem nicht mehr zwingend. Wer wissen will, wo das Projekt
heute steht und was offen ist, liest `ROADMAP.md`.

Die Einträge stammen aus der Roadmap, in der sie ursprünglich angehängt wurden. Beim Herauslösen
wurde nur das jeweils führende „Aktueller" aus den Überschriften entfernt — 130 Abschnitte konnten
nicht gleichzeitig aktuell sein. Inhaltlich ist nichts geändert.

## Paritätsmessungen im Verlauf

Erhoben mit `vb6c <projekt.vbp> --report` gegen VISIA 4.8.7.1 (10.152 Zeilen, 42 Quelldateien).
Jede Zeile ist ein Messpunkt nach einem abgeschlossenen Arbeitsschritt:

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
| Aktueller Managed-Emit-Messpunkt (2026-08-25) | **0** | **0** | **0** | **0** | **40 von 40** |

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

## Frühe Messungen und ihre Planungsfolgen

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

## Compiler-Kern nach dem Managed-Emit-Messpunkt

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
zusammengesetzte Layouts bleiben offen. Eigenständige Arrays von unterstützten skalaren oder UDT-Elementen
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
Emitter. Scalar-Pointer-Transfers für `Declare ... As Any` inklusive `ByVal VarPtr(...)` und
temporärer UTF-16-Puffer für `ByVal StrPtr(...)` sind über `IntPtr` abgedeckt; beschreibbare
Stringziele werden nach dem Native-Aufruf mit ihrer ursprünglichen VB6-Länge zurückgeschrieben.
Die semantisch vorhandene Standard-`Collection` besitzt jetzt ebenfalls eine echte
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

## .NET-Nachtrag

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

## CLI-Legacy-Nachtrag

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

## Forms-Host-Nachtrag

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

## Variant-Nachtrag

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

## VBG-Diagnostik-Nachtrag

`StartupProject=` wird nun gegen die tatsächlich deklarierten `.vbp`-Einträge aufgelöst.
Fehlende oder falsch geschriebene Startprojekte erzeugen `VB6VBG0007`, verhindern die Batch-
Emission und liefern über den CLI-Report einen Fehler-Exitcode. Der Prozesspfad ist mit einer
echten `.vbg`-Regression abgesichert. Die Gesamtsuite umfasst **888 Tests**.

## LSP-Navigations-Nachtrag

Der LSP liefert neben Compilerdiagnosen nun echte Completion-, Go-to-definition- und
Dokument-Symbol-Antworten. Die Antworten werden direkt aus dem bestehenden Syntaxbaum erzeugt,
berücksichtigen modulare Sub-/Function-/Property-/Event-/Declare-/Enum-/Type-/Const- und
Variablendeklarationen und ergänzen eine kleine Liste häufig genutzter VB6-Intrinsics. Wortpräfixe
und Cursorpositionen werden als LSP-Zeilen-/Spaltenpositionen aufgelöst; `didClose` entfernt
Dokumente wieder aus dem Serverzustand. Der vollständige JSON-RPC-Pfad ist mit einer Regression
für Completion, Definition und Dokument-Symbole abgesichert. Die Gesamtsuite umfasst
**897 Tests**. Vollständige Typermittlung, projektübergreifende Definitionen und semantisch
kontextabhängige Completion bleiben nachgelagerte Visual-Studio-Integrationsschritte.

## COM-Event-Nachtrag

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

## VB6-Variant-Mod-Nachtrag

Der `Mod`-Operator folgt für `Single`, `Double` und `Decimal` nun der klassischen
VB6/VBA-Regel: Fließkommawerte werden vor der Restbildung zu Ganzzahlen gerundet, und das
Ergebnis bleibt ein Long-artiger Variant-Wert. Die Regression deckt die historischen Beispiele
`12 Mod 4.3 = 0`, `12.6 Mod 5 = 3` sowie den kompilierten Variant-Ausführungspfad ab. Die
Gesamtsuite umfasst **906 Tests**.

## VBG-Referenznachtrag

Die `.vbg`-Emission validiert nun auch den tatsächlichen Lauf eines Consumers gegen eine zuvor
emittierte referenzierte VB6-Klassenbibliothek. Externe Klassenmember verwenden dabei denselben
Managed-Namen wie ihre Library-Definitionen (`__vb6_...`), sodass Projektgruppen mit
`Reference=...; Shared.vbp; ...` nicht nur in Dependency-Reihenfolge gebaut werden, sondern auch
zur Laufzeit aufgelöst werden. Der vollständige CLI-Pfad ist mit einem gestarteten Consumer
regressionsgesichert. Die Gesamtsuite umfasst **908 Tests**.

## MSBuild-SDK-Nachtrag

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

## Standard-Control-Nachtrag

Der WinForms-Host deckt nun auch häufige Legacy-Controlmember ab: `ListBox` und `ComboBox`
unterstützen `AddItem`, `RemoveItem`, `Clear`, die indizierte `List`-Property sowie `ListCount`
und `ListIndex`; `TextBox` unterstützt `SelStart`, `SelLength` und `SelText`; `CheckBox` und
`OptionButton` stellen `Value` bereit. Die Verträge laufen durch den bestehenden Twips-/Late-
Bound-Hostpfad und sind mit einer STA-Regression für Einfügen, Ersetzen, Entfernen, Auswahl und
Textselektion abgesichert. Vollständige OCX-Memberbibliotheken, MDI und UserControl-Hosting
bleiben separate Forms-/Interop-Schritte. `Timer` wird als eigener unsichtbarer WinForms-Host-
Control mit `Interval`, `Enabled` und konventionellem `TimerName_Timer`-Handler verdrahtet.
Die Gesamtsuite umfasst **899 Tests**.

## Conditional-Compilation-Nachtrag

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

## Declare-UDT-Nachtrag

Blittable `Type`-Records werden im Managed-Emitter jetzt als sequenzielle Structs in nativen
`Declare`-Signaturen verwendet und erhalten explizit das für den VB6-UDT-Pfad erforderliche
4-Byte-Packing. Echte Windows-Aufrufe von `GetSystemTime`, `GetVersionExA` und
`RtlMoveMemory` regressionssichern den vollständigen `ByRef`-Pfad einschließlich Feld-Write-back,
`Byte`-/`Double`-Alignment sowie feste `String * n`-Felder über `BYVALTSTR`/`SizeConst`. Variable
Stringfelder, Arrays, nicht-blittable UDTs und Callback-Delegates bleiben separate ABI-Schritte.
Die Gesamtsuite umfasst **919 Tests**.

## UDT-Len-Nachtrag

`Len` erkennt emittierte VB6-UDTs über ihren Managed-Namespace und fragt ihren nativen
Struct-Umfang über `Marshal.SizeOf` ab. Dadurch liefert ein `Byte`-/`Double`-Record mit VB6-
4-Byte-Packing `12` statt der CLR-defaulteten Ausrichtung; feste `String * n`-Felder werden
über ihre `BYVALTSTR`-Metadaten ebenfalls korrekt berücksichtigt. Die direkte Managed-Ausführung
ist mit zwei End-to-End-Tests regressionsgesichert. Die Gesamtsuite umfasst **921 Tests**.

## Declare-Stringpuffer-Nachtrag

Variable `ByVal String`-Parameter werden im Managed-P/Invoke als ANSI-`StringBuilder` emittiert.
Aufrufseitig addressierbare VB6-Strings werden nach dem nativen Aufruf per `ToString()` in ihr
ursprüngliches Ziel zurückgeschrieben; Rückgabewerte von Funktionen mit gleichzeitigem Puffer-
Write-back bleiben über Compiler-Temporaries erhalten. `GetComputerNameA` ist als echter Windows-
End-to-End-Aufruf regressionsgesichert. Array-Marshalling, nicht-blittable UDTs und Callback-
Delegates bleiben separate ABI-Schritte. Die Gesamtsuite umfasst **918 Tests**.

## LenB-Nachtrag

`Len` und `LenB` verwenden jetzt Variant-Rückgaben, sodass `Null` gemäß dem VB6-Vertrag erhalten
bleibt. `LenB` ist als eigene Intrinsic-Signatur durch Binder, IR, Managed-Emitter und Runtime
verdrahtet: Unicode-Strings liefern zwei Bytes je UTF-16-Codeeinheit, Scalar-Varianten behalten
ihre VB6-Speicherbreite, und emittierte UDTs verwenden den nativen In-Memory-Umfang einschließlich
Padding. Die direkte Ausführung ist mit String-, Scalar-, `Null`- und UDT-Fällen regressions-
gesichert. Die Gesamtsuite umfasst **924 Tests**.

## CommonDialog-Nachtrag

Der WinForms-Host behandelt `MSComDlg.CommonDialog` jetzt als nichtvisuelle Komponente statt als
unbekanntes `Panel`. `FileName`, `Filter`, `DialogTitle`, `FilterIndex`, `CancelError` und
`DefaultExt` werden über einen Managed-Adapter bereitgestellt; `ShowOpen` und `ShowSave` nutzen
die nativen WinForms-Dateidialoge und übernehmen den ausgewählten Dateinamen zurück in den
VB6-Objektvertrag. Die Komponente bleibt aus der visuellen Control-Hierarchie heraus, ist aber
über die bestehende Form-/Control-Namensauflösung und den Late-Bound-Dispatch erreichbar.
Vollständiges ActiveX-OCX-Hosting, insbesondere die echte `MSComDlg`-Typbibliothek und deren
gesamte Ereignis-/ABI-Oberfläche, bleibt separat offen. Die Gesamtsuite umfasst **925 Tests**.

## TreeView-Nachtrag

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

## ImageList-/ImageCombo-Nachtrag

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

## Generated-Assembly-Runner-Nachtrag

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

## RichTextBox-Host-Nachtrag

Der Managed-WinForms-Host bildet für `RichTextLib.RichTextBox` nun den häufigen VB6-Vertrag
für `TextRTF`, `SelStart`, `SelLength`, `SelText`, `SelColor`, `SelBold`, `SelItalic` und
`SelUnderline` ab. `FileName`, `Modified`, `RightMargin`, `HideSelection` und
`GetLineFromChar` sind ebenfalls verdrahtet; `LoadFile` und `SaveFile` akzeptieren den
optionalen `rtfRTF`-/`rtfText`-Dateityp und führen PlainText-Zeilenenden am Host auf VB6-`CRLF`
zurück. Die Regression nutzt den echten Late-Bound-Hostpfad und prüft Auswahlformatierung,
RTF-Roundtrip, Zeilenauflösung sowie Textdatei-Laden/Speichern. Vollständige RichTextLib-OCX-
ABI- und native Connection-Point-Kompatibilität bleiben offen. Die Gesamtsuite umfasst
**929 Tests**.

## FRX-Ressourcen-Nachtrag

`VBDesignerParser` erkennt nun auch die VB6-Designerform `TextRTF = $"file.frx":offset`.
`VBFrxResourceReader` validiert den little-endian 32-Bit-Längenpräfix am Offset, prüft die
Dateigrenze und stellt die folgenden Nutzdaten als `VBDesignerProperty.ResourceData` bereit.
Die Bytes bleiben bewusst opaque: RTF-, Bild-, Icon- und OCX-spezifische Interpretation gehört
in den jeweiligen Hostadapter und wird nicht durch eine unsichere Universaldecodierung ersetzt.
Fehlerhafte vorhandene Ressourcen erzeugen `VB6FRX0001` als Warnung, während fehlende optionale
Designerdateien für reine Analysepfade weiterhin diagnostikfrei bleiben. Die Gesamtsuite umfasst
**931 Tests**.

## Designer-Initialisierungs-Nachtrag

Designerwerte für `Caption`, `Text`, Sichtbarkeit, Aktivierung, Position, Größe, Farben,
`RichTextBox`-Auswahl und `Timer.Interval` werden nun beim generierten Form-Konstruktor nach der
Control-Erzeugung als explizite `InteractionSetMember`-Aufrufe emittiert. Der portable Runtime-
Vertrag reicht diese Werte an den konfigurierten Host weiter; der WinForms-Host setzt sie über
Twips-, OLE-Farb- und RichTextBox-Konvertierungen. Nicht skalare oder noch opaque Ressourcenwerte
bleiben bewusst beim jeweiligen Hostadapter. Die IR-Regression prüft den Designer-Property-
Namen und den emittierten Wert; die Gesamtsuite bleibt bei **931 Tests**.

## Forms-Designerwert-Nachtrag

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

## FRX-Bild-Nachtrag

`.frx`-Ressourcen für Form-/Control-`Picture` und Form-`Icon` werden nun als transportierbare
Werte in den generierten Form-Konstruktor übernommen. Der WinForms-Host entpackt die historische
VB6-StdPicture-Hülle und dekodiert BMP-/ICO-Payloads für `PictureBox`, `Image` und Form-Hintergrund
bzw. Form-Icon. Der Pfad bleibt absichtlich auf die intrinsischen Bildmember begrenzt; die
ressourcenbasierte `ImageList`-Einträge werden nun ebenfalls in den Managed-Adapter übernommen;
OCX-eigenes Rendering und vollständige OLE-Picture-Konvertierung folgen in separaten Host-/
ActiveX-Slices. Die VISIA-Emission wurde erneut erzeugt
und im STA-Runner ohne Ausnahme oder Messagebox gestartet. Die Gesamtsuite umfasst **935 Tests**.

## ImageList-FRX-Nachtrag

Verschachtelte `BeginProperty Images`-/`ListImageN`-Blöcke werden nun als Designer-Initialisierer
für `MSComctlLib.ImageList` erkannt. `ListImageN.Picture` dekodiert die eingebettete BMP-/ICO-
StdPicture-Payload, `ListImageN.Key` erhält den Legacy-Schlüssel, und fehlende Zwischenindizes
werden einsbasiert im Managed-Collection-Adapter angelegt. Die Bildobjekte bleiben bewusst im
Managed-Vertrag; eine echte native `ImageList`-Zuordnung zu OCX-Controls und deren Rendering
bleibt ein separater ActiveX-Host-Schritt. Die Regression deckt sowohl den verschachtelten
Designerpfad als auch den bestehenden `ListImages`-Late-Bound-Vertrag ab. Die Gesamtsuite
umfasst **935 Tests**.

## Shape-/Line-Forms-Nachtrag

`VB.Shape` und `VB.Line` werden im Managed-WinForms-Host nicht mehr als generische Panels
angelegt. `Shape` rendert Rechteck, Quadrat, Oval, Kreis und abgerundete Varianten mit
`BackColor`, `FillColor`, `FillStyle`, `BackStyle`, `BorderColor` und `BorderWidth`; `Line`
zeichnet seine Endpunkte über die VB6-Twips-Konvertierung aus `X1`, `Y1`, `X2` und `Y2`.
Die Designer-Allowlist übernimmt diese Werte in den generierten Formkonstruktor, und die
Regression prüft sowohl die IR-Emission als auch gerenderte Pixel im STA-Host. Native
Zeichen-APIs wie `PaintPicture`, vollständige AutoRedraw-/DrawMode-Semantik und MDI bleiben
separate Forms-Schritte. Die Gesamtsuite umfasst **938 Tests**.

## Menu-Forms-Nachtrag

Verschachtelte `VB.Menu`-Designerobjekte werden jetzt mit ihrem ursprünglichen Typnamen bis zur
IR-Emission erhalten und im WinForms-Host als echter `MenuStrip`-/`ToolStripMenuItem`-Baum
angelegt. `Caption`/`Text`, `Visible`, `Enabled`, `Checked`, `Index`, `Tag` und `Shortcut`
laufen über den bestehenden Late-Bound-Hostvertrag; Parent-Menüs werden anhand des qualifizierten
Designerpfads verbunden, und `MenuName_Click`-Handler werden an `ToolStripMenuItem.Click`
angeschlossen. Die Regression deckt Designer-Emission, Hierarchie und Event-Auslösung ab.
Separator-Semantik, vollständige VB6-Shortcut-Konvertierung, `PopupMenu` und MDI-Menüs bleiben
separate Forms-Schritte. Die Gesamtsuite umfasst **938 Tests**.

## Managed-AppHost-Nachtrag

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

## PopupMenu-Forms-Nachtrag

`VBInteraction.PopupMenu` delegiert nun an den konfigurierten `IVB6Host`. Der WinForms-Host baut
für ein `VB.Menu` einen separaten `ContextMenuStrip`-Snapshot auf, sodass der vorhandene
`MenuStrip`-Baum an Ort und Stelle bleibt. Verschachtelte Items, Separatoren, Sichtbarkeit,
Aktivierung, Checkzustand und Tags werden in den Snapshot übernommen; Popup-Klicks werden auf die
bereits am Original-Menü verdrahteten VB6-Handler weitergeleitet. Flags, vollständige
VB6-Shortcut-Konvertierung und MDI-Popup-Menüs bleiben weitere Kompatibilitätsschritte. Die
Regression prüft Delegation, Snapshot-Verhalten, Originalhierarchie und Handlerauslösung. Die
Gesamtsuite umfasst **939 Tests**.

## GraphicsLine-Forms-Nachtrag

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

## UserControl-Host-Nachtrag

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

## Form-Lifecycle-Nachtrag

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

## MDI-Forms-Nachtrag

`VB.MDIForm`-Designerwurzeln werden als MDI-Containerinitialisierung in die Managed-IR übernommen.
`MDIChild=True` wird als Form-Designerwert gebunden; der WinForms-Host ordnet solche Child-Forms
automatisch dem registrierten MDI-Container zu und hält den Wert über den Host-Dispatch lesbar.
Die Regression deckt sowohl Designer-Emission als auch die konkrete Parent-/Child-Hierarchie ab.
Vollständige MDI-Fensterbefehle, MDI-Menüs und persistente Window-Management-Regeln bleiben offen.
Die Gesamtsuite umfasst **950 Tests**.

## Native-OCX-/AppHost-Nachtrag

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

## COM-Wrapper-Interop-Nachtrag

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

## Native-OCX-Dispatch-Nachtrag

Der WinForms-Host leitet Memberzugriffe auf native `AxHost`-Controls jetzt nach den normalen
VB6-/WinForms-Sonderregeln direkt an das zugrunde liegende COM-RCW weiter. Damit funktionieren
auch COM-Properties und Methoden, die der CLR-Wrapper selbst nicht als Managed-Property anbietet.
Der x86-Test aktiviert die auf diesem Rechner registrierte `MSCOMCTL.OCX` als echtes
`MSComctlLib.ListViewCtrl.2`, setzt `View` und liest den Automation-Wert wieder aus. Die
64-Bit-Fallback-Regel bleibt aktiv, weil die 32-Bit-OCX dort trotz sichtbarer ProgID nicht
aktivierbar ist. Vollständiges `IDispatch`-ABI-Marshalling, native OCX-Events und die weiteren
MSComctl-/RichText-/CommonDialog-Oberflächen bleiben offen.

## nativer RichText-Nachtrag

Der opt-in-Native-Pfad hostet `RichTextLib.RichTextBox` jetzt über `RICHTEXT.RichtextCtrl.1`,
wenn die 32-Bit-OCX im x86-Prozess aktivierbar ist. `TextRTF` wird dabei direkt über das COM-RCW
gelesen und geschrieben; der VISIA-Runner bleibt mit diesem Pfad ohne Ausnahme und ohne
Messagebox stabil. `MSComctlLib.TreeView` bleibt vorerst beim Managed-Adapter, da der native
`Nodes`-Collection-ABI noch nicht stabil genug für den Runner ist. Die vollständige native
TreeView-/ImageList-/ImageCombo-/CommonDialog-Oberfläche und ihre Event-ABIs bleiben offen.

## Format-Nachtrag

`Format$` verarbeitet die VBA-Datums-Token `w` (Wochentag), `ww` (Kalenderwoche) und `q`
(Quartal) jetzt auch im vollständigen Managed-Compilerpfad. `FirstDayOfWeek` unterstützt die
VB6-Werte `vbUseSystem`/`vbSunday` bis `vbSaturday`; `FirstWeekOfYear` unterstützt
`vbUseSystem`/`vbFirstJan1`/`vbFirstFourDays`/`vbFirstFullWeek`. Die Woche wird mit dem
invariant-gregorianischen Kalender berechnet; `vbUseSystem` übernimmt die aktuellen
Culture-Einstellungen für Wochenbeginn und Wochenregel, systemabhängige Text-/Locale-Ausgabe
bleibt ein separater Schritt. Runtime- und E2E-Regressionen decken die Token und Parameter ab.
Die Gesamtsuite umfasst nun **954 Tests**.

## Standard-OCX-Hosting-Nachtrag

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

## Control-Array-Lifecycle-Nachtrag

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

## nativer TreeView-/IDispatch-Nachtrag

Die registrierte `MSComctlLib.TreeCtrl.2` wird im opt-in-Native-Host jetzt als echter `AxHost`
aktiviert. Für das zugrunde liegende `Nodes`-RCW verwendet die Runtime eine direkte Windows-
`IDispatch`-Brücke vor dem CLR-Reflection-Fallback. Dadurch funktionieren im x86-Pfad
`Nodes.Count`, `Nodes.Add`, einbasierter `Nodes.Item`-Zugriff sowie Lesen und Schreiben der
Node-Properties, ohne den instabilen Reflection-Aufruf auf alten OCX-Collections auszulösen.
Der normale Host behält den portablen Managed-TreeView-Adapter; der native Pfad bleibt wegen
weiterer Event-, ByRef- und vollständiger ImageList-/ImageCombo-Verträge opt-in. Alle auf dem
Testsystem registrierten Standard-OCX bleiben architekturabhängig und benötigen den x86-Runner,
wenn nur die 32-Bit-Registrierung vorhanden ist. Die x86- und x64-WinForms-Regression umfasst
jeweils **31 Tests**; die Gesamtsuite umfasst nun **959 Tests**. Der direkte native AppHost-
Start der neu emittierten VISIA-Ausgabe endet ohne `System.Private.CoreLib`-Ladefehler; der
automatisierte Runner-Lauf bleibt in der nicht-interaktiven Testumgebung ohne sichtbaren
Fenster-Handle und muss für eine visuelle GUI-Abnahme in einer interaktiven Windows-Sitzung
geprüft werden.

## nativer OCX-Objektübergabe-Nachtrag

Native OCX-Properties verwenden bei objektwertigen Zuweisungen jetzt den passenden
`PROPERTYPUTREF`-Vertrag und entpacken `IVBComObjectProvider`-Wrapper vor der VARIANT-
Marshalling-Grenze auf ihr zugrunde liegendes COM-RCW. Falls ein OCX die alternative
Automation-Konvention erwartet, wird mit `PROPERTYPUT` beziehungsweise `PROPERTYPUTREF`
erneut versucht, bevor der Reflection-Fallback greift. Der x86-Regressionspfad erzeugt ein
echtes `IPictureDisp`, fügt damit ein Bild in die native `ImageList.ListImages`-Collection ein
und weist anschließend die native ImageList der `ImageCombo.ImageList`-Property zu. Damit ist
die Objektübergabe zwischen zwei real aktivierten Standard-OCX abgesichert. Die x86- und
x64-WinForms-Regression umfasst weiterhin jeweils **31 Tests**; die Gesamtsuite bleibt bei
**959 Tests**. TypeInfo-gesteuertes typisiertes COM-ByRef-Marshalling für unterstützte
Automation-Typen steht; Connection-Point-Events und die restlichen
nativen ABI-Sonderfälle bleiben separate Roadmap-Schritte.

## nativer OCX-Collections-Nachtrag

`For Each` über native Host-/OCX-Collections nutzt jetzt auch die reale RCW-Enumeration. Einige
ältere `IEnumVARIANT`-Implementierungen liefern hinter den gezählten Elementen noch einen
`VT_EMPTY`-Platzhalter; der Host verwirft diesen `null`-Eintrag für COM-Collections, ohne die
Enumeration normaler Managed-Collections zu verändern. Der x86-Regressionspfad legt einen
TreeView-Node über die native `Nodes`-Collection an und prüft, dass `VBInteraction.EnumerateControls`
genau diesen einen Node für den generierten `For Each`-Vertrag zurückgibt. Die x86- und x64-
WinForms-Regression umfasst nun jeweils **32 Tests**; die Gesamtsuite umfasst **960 Tests**.
Vollständige UDT-/Pointer-/Event-ABI und weitere Connection-Point-Sonderfälle bleiben offen.

## TypeInfo-gesteuerter COM-ByRef-Nachtrag

Die Raw-`IDispatch`-Brücke liest vor einem Aufruf die `FUNCDESC`-/`PARAMDESC`-/`TYPEDESC`-Metadaten
der TypeLibrary und setzt für `PARAMFLAG_FOUT`-Parameter den passenden Automation-Typ. Die
unterstützten skalaren `VARTYPE`s, `DATE`, `CURRENCY`, `VARIANT` und kompatible SAFEARRAYs werden
mit einer inneren VARIANT initialisiert; bei typisierten ByRef-Werten zeigt der äußere VARIANT
auf die Datenunion, bei `VT_BYREF|VT_VARIANT` auf die innere VARIANT. Nach `Invoke` werden die
geänderten Werte in das ursprüngliche Late-Bound-Argumentarray zurückgeschrieben. Nicht
abbildbare UDT-, C-Array- und Pointer-Verträge oder nicht konvertierbare Eingaben lösen einen
sicheren vollständigen ByVal-Wiederholungsversuch aus. Die bestehenden Scripting-Dictionary-,
emittierten COM-Host- und nativen x86-OCX-Regressionspfade bleiben stabil; vollständige
`[in]`-/`[out]`-Sonderfälle, UDT-/Pointer-/SAFEARRAY-Descriptor-Marshalling und Connection-Point-
Events bleiben separate COM-ABI-Schritte.

## SAFEARRAY-/CLR-Array-Variant-Nachtrag

Automation-Arrays, die über COM als `System.Array` in die Managed-Runtime gelangen, werden jetzt
wie Variant-Arrays erkannt. `IsArray`, `IsObject`, `VarType` und `TypeName` liefern die passenden
VB6-Array-Subtypen; `LBound`/`UBound` berücksichtigen die echten CLR-Untergrenzen und der
Variant-Elementzugriff liest und schreibt auch mehrdimensionale beziehungsweise nicht bei null
beginnende CLR-Arrays. Die bisherigen `VBArray<T>`-Pfade und Default-Property-Indizes für normale
Managed-Objekte bleiben unverändert. ByRef-Elementadressen von CLR-/SAFEARRAY-Werten, vollständige
SAFEARRAY-Descriptor-Konvertierung und SAFEARRAYs mit UDT-/Pointer-Elementen bleiben separate
Interop-Schritte. Die Gesamtsuite umfasst **958 Tests**.

## COM-Connection-Point-Metadaten-Nachtrag

Der COM-Eventpfad verwendet weiterhin importierte Source-IIDs und DISPIDs, wenn der Compiler diese
aus einer TypeLibrary kennt. Für rein late-bound COM-Objekte ohne importierte Eventmetadaten liest
die Runtime nun den `IDispatch`-`ITypeInfo`-Vertrag, durchsucht die als `FSOURCE` markierten
Connection-Point-Schnittstellen und ermittelt den Event-DISPID per Namen. Die daraus gewonnene
Identität wird an `ComEventsHelper` weitergereicht und beim Abmelden mit derselben Delegate-Instanz
entfernt; CLR- und WinForms-Eventbrücken bleiben unverändert. Vollständige COM-Event-Signatur-
Konversion, Cancel-/ByRef-Fehlerverträge und der gesamte Connection-Point-Lebenszyklus bleiben
separate ABI-Schritte.

## nativer OCX-Event-Nachtrag

Native `AxHost`-Wrapper bevorzugen für VB6-Ereignisse nun den zugrunde liegenden COM-
Connection-Point und nicht die geerbten WinForms-Ereignisse des Wrappers. Wenn ein OCX keine
brauchbare Event-TypeInfo über `IDispatch` liefert, versucht die Runtime zuerst `IProvideClassInfo`
und danach die registrierte TypeLib des konkreten CLSID; dabei werden `FSOURCE`-Interfaces aus
Coclasses rekursiv durchsucht. Der x86-Test aktiviert die registrierte `RichTextLib.RichTextBox`
und verifiziert den nativen `Change`-Event inklusive sauberem Abmelden. Vollständige Event-
Signaturkonversion, Cancel-/ByRef-Verträge, Bitness-/Designer-Sonderfälle und der gesamte
Connection-Point-Lifecycle bleiben offen.

## Managed-COM-Server-Nachtrag

Der Managed-Emitter akzeptiert für Bibliotheksausgaben die CLI-Option `--com-host`. Jede
emittierte VB6-Klasse erhält dabei eine deterministische CLSID aus Assembly- und Klassennamen,
eine passende `ProgID`, `ComVisible` sowie `ClassInterface(AutoDual)`; vorhandene Interface-
Verträge erhalten ebenfalls COM-kompatible Identitäten. Der Artefaktpfad ruft das installierte
.NET-SDK in einem isolierten temporären Projekt auf, ersetzt dessen Zwischenassembly durch die
VB6-Assembly und übernimmt den SDK-generierten `*.comhost.dll`-Server samt CLSID-Map. Eine
Windows-Prozessregression ruft den exportierten `DllGetClassObject`-Entry-Point direkt auf,
erzeugt über `IClassFactory` eine Instanz und erreicht die `IDispatch`-Methode eines emittierten
VB6-Klassenmoduls.

Der Schalter ist bewusst auf `ManagedOutputKind.Library` begrenzt. COM-Registry-Installation,
Reg-Free-Manifest-/Typbibliotheks-Emission, vollständige `[in]`-/`[out]`-Konversion über alle
Automation- und User-Defined-Typen und ein eigener Raw-`IUnknown`-/`IDispatch`-Serververtrag
bleiben nachgelagerte Interop-Schritte. Die
Gesamtsuite umfasst **960 Tests**.

## Variant-Objekt-Default-Nachtrag

Objektwerte in Variant-Kontexten lösen jetzt die VB6-Default-Property aus, wenn der numerische,
zeichenbezogene oder typbezogene Kontext einen Wert benötigt. Das gilt für `+`, `-`, `*`, `/`,
`\`, `Mod`, `^`, bitweise Operatoren, Vergleiche, `&`, `VarType`, die numerischen/string-
bezogenen Konvertierungen sowie `Debug.Print`; Default-Property-Ketten werden begrenzt verfolgt,
und Objekte ohne passende Default-Property behalten den bisherigen Objektvertrag. `VarType`
liefert dadurch den Subtyp des Default-Wertes, während echte Fehler aus einem Default-Getter
unverändert weitergereicht werden. Runtime- und End-to-End-Regressionen decken Managed-Klassen
mit `VB_UserMemId = 0` sowie CLR-Default-Properties ab. Die Gesamtsuite umfasst nun **963 Tests**.

## Variant-Objekt-Intrinsic-Nachtrag

Die Default-Property-Auflösung greift nun auch an den bisher offenen Intrinsic-Grenzen: `Len` und
`LenB` bestimmen die Speicher-/Zeichenlänge des Default-Wertes, `Format`, `Str`, `Oct` und der
Zeichenparameter von `String` verwenden den aufgelösten Wert, und `IsNumeric`/`IsDate` prüfen
dessen tatsächlichen Variant-Inhalt. Der `Like`-Pfad löst beide Operanden vor der Null- und
Stringbehandlung auf; `Val` profitiert im Compilerpfad von der bestehenden Variant-zu-String-
Konversion. `IsArray`, `IsObject` und `TypeName` bleiben bewusst container-/identitätsbezogen.
Runtime-Tests für CLR-Default-Properties und ein emittiertes `.vbp` mit numerischem und textuellem
Default-Wert decken die Intrinsics gemeinsam ab. Die Gesamtsuite umfasst nun **967 Tests**.

## nativer OCX-Event-ByRef-Nachtrag

Der verpflichtende x86-WinForms-Lauf aktiviert die registrierten Standard-OCX-Komponenten jetzt
mit **33/33** Tests. Neben dem parameterlosen nativen `RichTextBox.Change`-Event deckt eine echte
`RichTextBox.KeyPress`-Connection-Point-Regression einen `ByRef`-Parameter ab: Der Handler erhält
`KeyAscii`, ändert ihn von `x` auf `y`, und der geänderte Wert wird vom OCX in den Text übernommen.
Damit ist der generische `VBEvents`-Delegate-Adapter für diesen nativen ByRef-Signaturtyp belegt;
weitere Event-Signaturen, Cancel-/ByRef-Sonderfälle, Connection-Point-Lifecycle und vollständige
native ABI-Konversion bleiben separate Schritte. Die Gesamtsuite umfasst nun **968 Tests**.

## Variant-Objekt-Math-Nachtrag

Die verbleibenden Variant-Math-Intrinsics lösen nun ebenfalls die Default-Property auf, bevor sie
Null-, Array- oder numerische State-Regeln anwenden: `Abs`, `Sgn`, `Fix`, `Round` und `Int` folgen
dem tatsächlichen Default-Wert eines Objekts. `CVErr` übernimmt denselben Vertrag für die
explizite Error-Variant-Konversion; ein Default-Wert `Null` bleibt dabei `Null`. Direkte Runtime-
Tests decken numerische, Null- und Error-Default-Properties ab, und ein emittiertes `.vbp` prüft
den vollständigen Managed-Aufrufpfad. Die Gesamtsuite umfasst nun **970 Tests**.

## Variant-Objekt-Boolean-Nachtrag

Der zentrale Variant-zu-Boolean-Pfad löst Default-Properties jetzt auch vor `If`-Bedingungen,
`IIf`-Ausdrücken und `Switch`-Kriterien auf. Nichtnullige numerische Default-Werte werden wahr,
`Null` bleibt falsch, und ein `Missing`-Default löst weiterhin den VB6-Fehler 448 aus. Ein direkter
Runtime-Test sowie ein emittiertes `.vbp` mit allen drei Boolean-Kontexten sichern den Vertrag ab.
Die Gesamtsuite umfasst nun **972 Tests**.

## nativer OCX-Parameterized-Event-Nachtrag

Der native x86-OCX-Pfad deckt neben `RichTextBox.Change` und dem einzelnen `ByRef`-
`RichTextBox.KeyPress`-Parameter jetzt auch ein parametrisiertes `RichTextBox.MouseDown`-Event
ab. Die Connection-Point-Regression übergibt `Button`, `Shift`, `X` und `Y` aus einer echten
Windows-Nachricht an den VB6-Handler und prüft die Automation-Typkonversion (`I2`/`R4`). Der
erzwungene x86-WinForms-Lauf umfasst damit **34/34** Tests; vollständige Event-Signatur- und
Connection-Point-Lifecycle-Regeln bleiben offen. Die Gesamtsuite umfasst nun **973 Tests**.

## x86-AppHost-Start-Nachtrag

Ein aus einem Legacy-`.vbp` mit `--x86` erzeugtes EXE wird jetzt in der CLI-Regression tatsächlich
gestartet. Der native .NET-AppHost lädt die danebenliegende Managed-DLL, `VB6.Runtime.dll` und
die Runtime-Konfiguration korrekt, führt `Sub Main` aus und beendet sich ohne
`System.Private.CoreLib`-Ladefehler. Damit ist der zuvor nur über PE-Header geprüfte x86-Output-
Vertrag auch als Prozessstart abgesichert. Die Gesamtsuite umfasst nun **974 Tests**.

## SAFEARRAY-ByRef-Nachtrag

Der TypeInfo-gesteuerte Raw-`IDispatch`-Pfad materialisiert interne `VBArray<T>`-Werte für
unterstützte typed SAFEARRAY-ByRef-Parameter jetzt als CLR-Arrays. Rang, echte Untergrenzen und
die physische VB6-Elementreihenfolge bleiben erhalten; skalare Automation-Elemente werden vor
dem Übergang in den nativen VARIANT konvertiert. Nach dem Aufruf werden gleich geformte
SAFEARRAY-Rückgaben wieder in den bestehenden `VBArray<T>`-Container geschrieben, statt dessen
Identität durch ein fremdes CLR-Array zu ersetzen. Ein echter emittierter `comhost`-Prozess prüft
weiterhin den zweidimensionalen, nicht bei null beginnenden `ByRef Variant`-SAFEARRAY-ABI. UDT-,
Pointer- und nicht kompatible SAFEARRAY-Descriptoren bleiben bewusst separate Interop-Schritte.
Die Gesamtsuite umfasst nun **974 Tests**.

## Managed-WinForms-Event-ByRef-Nachtrag

Der Managed-WinForms-Host schreibt `ByRef`-Änderungen aus `KeyPress`-Handlern jetzt in das
zugrunde liegende `KeyPressEventArgs.KeyChar` zurück. Für `KeyDown` und `KeyUp` wird ein vom
VB6-Handler geänderter `KeyCode` über den verfügbaren WinForms-Vertrag als `Handled`/`SuppressKeyPress`
abgebildet, da WinForms den KeyCode selbst schreibgeschützt anbietet. ByVal-Handler bleiben davon
unverändert; der Rückweg wird nur für tatsächlich deklarierte ByRef-Parameter aktiviert. Der
Managed-Lauf und der verpflichtende x86-Lauf mit registrierten Standard-OCX-Komponenten umfassen
weiterhin jeweils **34/34** Tests. Vollständige Event-Signaturkonversion und der native
Connection-Point-Lifecycle bleiben separate ABI-Schritte.

## COM-Connection-Point-Lifecycle-Nachtrag

`VBEvents` kann generierte Methodensubscriptions jetzt objektbezogen nach Source oder Target
entfernen. `WinFormsHost.Unload` und `Dispose` nutzen diesen Vertrag für Formulare, Controls und
Komponenten, sodass auch über `ComEventsHelper` installierte native Connection-Point-Delegates vor
dem Freigeben der OCX-Objekte entfernt werden. Die bestehende explizite Abmeldung und die
Reassignment-Regel bleiben unverändert; die Runtime-Regression umfasst nun **200** Tests, der
Managed-WinForms-Lauf und der verpflichtende x86-Lauf mit registrierten Standard-OCX-Komponenten
jeweils **34/34**. Vollständige Event-Signaturkonversion sowie UDT-/Pointer-ABI bleiben separate
Interop-Schritte. Die Gesamtsuite umfasst nun **975 Tests**.

## COM-Host-Registrierungs-Nachtrag

Die CLI kann einen erzeugten SDK-`.comhost.dll` jetzt explizit über
`--register-com` beziehungsweise `--unregister-com` installieren oder entfernen. Dabei wird
unter Windows das passende `regsvr32` aus `System32` oder `SysWOW64` anhand von `--x64` oder
`--x86` gewählt und mit `/s` gestartet, damit Registry-/Load-Fehler als Exitcode und
Standardfehler an den Build zurückgehen statt eine native Messagebox zu öffnen. Der Pfad ist
bewusst auf Dateien mit `.comhost.dll`-Suffix begrenzt; Typbibliotheks-Emission und UDT-/Pointer-
Marshalling bleiben separate COM-Verträge. Die Compiler-Regression umfasst nun **347** Tests,
die Gesamtsuite **977**.

## direkter ActiveX-ProgID-Nachtrag

Der native WinForms-Host versucht bei qualifizierten, im Projekt auftretenden Typnamen neben den
bisherigen Standard-Aliassen jetzt auch den direkt registrierten ProgID. Vor der Erstellung des
`AxHost` wird per `IOleObject`-Query geprüft, dass die COM-Klasse tatsächlich ein visuelles
ActiveX-Control ist; nonvisual Komponenten wie `MSComDlg.CommonDialog` fallen weiterhin in den
dedizierten COM-Adapter. Der verpflichtende x86-Lauf mit den registrierten Standard-OCX umfasst
weiterhin **34/34** Tests, die Gesamtsuite **977**. Vollständiges generisches OCX-Event-/UDT- und
Pointer-Marshalling bleibt ein separater ABI-Vertrag.

## nativer OCX-Event-Routing-Nachtrag

COM-Provider werden im generischen `VBEvents`-Pfad nicht mehr an gleichnamige geerbte CLR-/WinForms-
Events eines `AxHost`-Wrappers gebunden. Native ActiveX-Quellen bleiben dadurch am COM-
Connection-Point, sodass ByRef- und Automation-Signaturen nicht durch die Wrapper-Signatur
verdeckt werden; reine Managed-CLR-Events behalten ihren bisherigen Adapter. Die Runtime-
Regression umfasst **201** Tests, der verpflichtende x86-WinForms-Lauf mit registrierten
Standard-OCX weiterhin **34/34**. Vollständige generische Event-Signatur-, UDT- und Pointer-
Marshalling-Verträge bleiben separate ABI-Schritte.

## TypeLib-Event-ByRef-Nachtrag

Der TypeLib-Importer behandelt `VT_PTR`-Parameter klassischer Automation-Events jetzt als
`ByRef`, wenn der Pointer auf einen unterstützten skalaren Automation-Typ zeigt. Dadurch wird
beispielsweise `RichTextLib.RichTextBox.KeyPress(KeyAscii)` aus der echten `RICHTX32.OCX`-
TypeLibrary als `Integer ByRef` gebunden; verschachtelte oder nicht sicher abbildbare Pointer
bleiben `Object`. Der Importvertrag ist mit der registrierten TypeLibrary regressionsgesichert;
die vollständige UDT-/SAFEARRAY-/Pointer-ABI bleibt weiterhin offen. Die Compiler-Suite umfasst
nun **348** Tests.

## nativer Designer-Event-Nachtrag

Der direkte `WinFormsHost.TrySubscribeEvent`-Pfad erkennt native `AxHost`-Controls jetzt als COM-
Provider und verbindet konventionelle Designer-Handler wie `Editor_KeyPress` über den nativen
Connection Point. Der Wrapper-CLR-Eventpfad wird dabei nicht doppelt aktiviert; Abmeldung und
Lifecycle-Aufräumen laufen weiterhin über `VBEvents`. Der echte x86-RichTextBox-ByRef-Test deckt
diesen Host-Hook ab; Runtime und WinForms bleiben bei **201** beziehungsweise **34/34** Tests.

## generierter nativer OCX-Designer-End-to-End-Nachtrag

Ein echtes kompiliertes `.vbp`/`.frm` mit `RichTextLib.RichTextBox` erzeugt und hostet jetzt den
nativen `AxHost` vollständig über den Managed-Compilerpfad. Die konventionelle VB6-Prozedur
`Editor_KeyPress` wird nach der Handle-Erzeugung automatisch am COM-Connection-Point verbunden;
die Handlerauflösung bleibt dabei VB6-konform case-insensitive. Der x86-Test prüft den gesamten
Weg vom TypeLib-importierten `Integer ByRef` über den generierten Formkonstruktor bis zum
geänderten Zeichenwert im OCX. Native Designerbindungen werden nach `Show` erneut aufgebaut,
weil ein `AxHost` im Konstruktor noch kein COM-Objekt besitzen muss. Runtime und WinForms umfassen
damit **201** beziehungsweise **35/35** Tests; die Gesamtsuite umfasst **980 Tests**.

## CLI-VBG-OCX-Nachtrag

Der CLI-Gruppenpfad kompiliert jetzt auch eine reale `.vbg` mit einem `.vbp`, das eine registrierte
`RichTextLib.RichTextBox`-Designerquelle und TypeLib-Referenz enthält. `vb6c --emit-assembly
<ausgabeverzeichnis> --x86` erzeugt daraus die x86-Managed-Companion-Assembly, den nativen
AppHost und die Runtime-Abhängigkeit; der PE-Header trägt `Machine.I386` und `Requires32Bit`.
Damit ist der command-line Compile-Vertrag für Legacy-Projektgruppen mit nativen OCX-Designer-
inputs explizit regressiongesichert. Die CLI-Suite umfasst nun **10** Tests, die Gesamtsuite
**981 Tests**.

## nativer WithEvents-Nachtrag

`WithEvents`-Zuweisungen auf native OCX-Controls funktionieren jetzt auch aus `Form_Load` heraus.
Der Compiler bewahrt die aus der TypeLibrary importierten Connection-Point-Events, wenn ein
Projekt denselben Control-Vertrag zugleich über `Reference=` und `Object=` einbindet; der stabile
explizite Control-Vertrag behält dabei seine vorhandenen Late-Bound-Mitglieder. Der WinForms-Host
wiederholt eine vor der nativen COM-Aktivierung angelegte Subscription nach `Show`, sodass auch
spät erzeugte `AxHost`-COM-Objekte korrekt verbunden werden. Der kompilierte x86-`.vbp`/`.frm`-
Regressionstest prüft `Form_Load`, `source_Change` und den bestehenden ByRef-KeyPress-Pfad;
Compiler und WinForms umfassen damit **348** beziehungsweise **35/35** Tests, die Gesamtsuite
weiterhin **981** Tests.

## importierter COM-Event-Identitäts-Nachtrag

Verzögert angelegte native Event-Subscriptions bewahren nun auch die aus der TypeLibrary
importierte Source-IID und DISPID, solange der `AxHost` noch kein COM-Objekt besitzt. Beim späteren
Retry nach der Aktivierung wird dadurch der bereits gebundene Connection-Point verwendet; die
zusätzliche x86-Regression prüft `WithEvents source_KeyPress(KeyAscii As Integer)` samt ByRef-
Write-back neben dem konventionellen Designer-Handler. Die bestehende Testabdeckung bleibt bei
**981** Tests, davon **35/35** im WinForms-x86-Lauf.

## intrinsischer Control-Array-Event-Nachtrag

Designer-Control-Arrays für intrinsische WinForms-Steuerelemente bewahren jetzt ihre aus
`Index=` ermittelten Unter- und Obergrenzen bis in den generierten Klassenkonstruktor. Dadurch
werden beispielsweise `Buttons(0)` und `Buttons(1)` als echte Array-Elemente angelegt und die
konventionellen Handler `Buttons_Click(Index)` sowie `Buttons_KeyPress(Index, KeyAscii)` erhalten
den jeweiligen VB6-Index. Auch der ByRef-Parameter bleibt bei Array-Handlern an seiner korrekten
Position und kann `KeyAscii` zurückschreiben. Der kompilierte `.vbp`/`.frm`-Regressionstest prüft
beide Elemente und beide Ereignispfade; die Gesamtsuite umfasst damit **982** Tests, davon
**36/36** im WinForms-Lauf.

## lückenhafter Control-Array-Index-Nachtrag

Die Designer-Indexliste wird jetzt zusätzlich zur Array-Range bewahrt. Bei nicht zusammenhängenden
VB6-Designer-Arrays wie `Buttons(0)` und `Buttons(2)` erzeugt der Formkonstruktor dadurch nur die
tatsächlich vorhandenen Controls; ein nicht vorhandenes `Buttons(1)` wird weder als Host-Control
angelegt noch an einen konventionellen Event-Handler gebunden. Der bestehende kompilierte
`.vbp`/`.frm`-Regressionstest deckt den lückenhaften Click- und KeyPress-Pfad ab; die
Testgesamtzahl bleibt bei **983**, davon **36/36** im WinForms-Lauf.

## MSBuild-VBG-SDK-Nachtrag

Der MSBuild-SDK-Gruppenpfad ist jetzt über den tatsächlich gepackten
`VB6.Compiler.Sdk/1.0.0`-Vertrag regressiongesichert. Ein SDK-Projekt mit `VB6ProjectGroup` baut
eine reale `.vbg` über `dotnet msbuild`, verfolgt Gruppen-, Projekt-, Quell- und Designerinputs
inkrementell und überspringt unveränderte Builds. Das Target schreibt zusätzlich ein Output-
Manifest; wenn ein erzeugtes Assembly-, AppHost-, Runtime-, PDB- oder Runtimeconfig-Artefakt
fehlt, wird der Compile-Stempel invalidiert und die Gruppe vollständig repariert. Der CLI-Bereich
umfasst damit **11** Tests, die Gesamtsuite **983** Tests. Die vollständige Visual-Studio-
Projektmodell- und Design-Time-Integration bleibt der nächste Ausbau dieses Vertrags.

## TypeLib-SAFEARRAY-Elementtyp-Nachtrag

Der TypeLib-Importer bewahrt `VT_ARRAY|T` und verschachtelte `VT_SAFEARRAY(T)`-Beschreibungen
jetzt als `ArrayTypeSymbol` mit dem importierten Elementtyp. Dadurch wird beispielsweise
`MSHTML.IHTMLDocument2.write` aus der realen Windows-`mshtml.tlb` als `Variant()` gebunden und
kann den vorhandenen `VBArray<T>`-/Automation-Array-Vertrag verwenden. C-Arrays und Pointer-
Konstrukte bleiben weiterhin opaque, bis ihr natives ABI explizit modelliert ist. Der echte
`mshtml`-TypeLib-Bindungstest sowie eine Runtime-Regression für `SAFEARRAY(I4)` sichern Elementtyp,
Untergrenzen und Rückkopieren ab. Die Gesamtsuite umfasst damit **985 Tests**.

## COM-ByVal-SAFEARRAY-Nachtrag

Der Raw-`IDispatch`-Aufruf erkennt typisierte, nicht-ByRef-SAFEARRAY-Parameter aus der TypeInfo
und materialisiert `VBArray<T>`-Argumente vor `Invoke` als native `VT_ARRAY|T`-VARIANTs. Der
bestehende skalare und ByRef-Dispatcher bleibt dabei unverändert; nicht unterstützte C-Arrays,
Pointer und UDT-Elemente fallen weiterhin kontrolliert auf den bisherigen Pfad zurück. Der
Native-VARIANT-Test prüft die echten Untergrenzen und die `VT_ARRAY|VT_VARIANT`-Signatur; die
Vollsuite umfasst nun **986 Tests**, VISIA bleibt bei **40/40** fehlerfreien Projektitems.

## COM-SAFEARRAY-Rückgabewert-Nachtrag

Der Managed-Emitter bewahrt bei dynamischen TypeLib-Property- und Methodenaufrufen jetzt den
deklarierten `ArrayTypeSymbol`-Rückgabetyp. Ein vom Raw-`IDispatch` geliefertes CLR-
`System.Array` wird dadurch in `VBArray<T>` überführt; Rang, explizite Untergrenzen und
Elementkonversionen bleiben erhalten. Der bisherige `Variant`-Rückgabepfad sowie direkte
`VBArray<T>`-Werte bleiben unverändert. Die Runtime-Regression prüft einen zweidimensionalen
SAFEARRAY mit nicht-nullbasierter Grenze; die Vollsuite umfasst nun **987 Tests**.

## Variant-Array-Zuweisungsnachtrag

Der Managed-Emitter konvertiert dynamische `Object`-/`Variant`-Ergebnisse jetzt auch beim
Zuweisen in eine typisierte VB6-Arrayvariable über `VBArrayOperations.FromObject<T>`. Damit
funktionieren beispielsweise spät gebundene COM-Properties, die ein SAFEARRAY liefern, in
`Dim values() As Variant` inklusive `LBound`, `UBound` und Elementzugriff. Der echte
`Scripting.Dictionary.Keys`-End-to-End-Test sichert diesen Legacy-Pfad; die Vollsuite bleibt
bei **987 Tests**.

## Declare-SAFEARRAY-Nachtrag

`Declare`-Parameter der Form `ByRef values() As ...` werden im Managed-Backend jetzt als native
`SAFEARRAY**`-Argumente emittiert. Der Compiler materialisiert unterstützte Automation-Arrays mit
ihren echten VB6-Untergrenzen, hält die native Pointer-Storage bis zum Aufruf und schreibt
Elementänderungen sowie ersetzte Arrayformen anschließend zurück. Der Vertrag deckt die
unterstützten skalaren Automation-Typen und `Variant()` ab; UDT-, Pointer- und `Currency`-
SAFEARRAYs bleiben wegen ihres eigenen nativen Deskriptors separate Interop-Schritte. Die
Regression prüft IR, Managed-Emission und einen echten `oleaut32`-SAFEARRAY-Write-back; die
Vollsuite umfasst nun **989 Tests**.

## Declare-Currency-SAFEARRAY-Nachtrag

`Currency()`-Declare-Parameter werden jetzt als native `SAFEARRAY(CY)`-Deskriptoren materialisiert.
Die Runtime schreibt den skalierten 64-Bit-Currency-Wert direkt in die Automation-Elemente und
führt native Änderungen anschließend wieder verlustarm nach `VBCurrency` zurück. Damit ist der
`Currency`-Sonderfall vom CLR-`decimal`-SAFEARRAY-Mapping entkoppelt; UDT-, Pointer- und Callback-
ABIs bleiben weiterhin separate Interop-Schritte. Eine Windows-Regression prüft Erzeugung,
native Elementänderung und Rückkopieren; die Vollsuite umfasst nun **990 Tests**.

## Declare-Currency-Scalar-Nachtrag

Skalare `Currency`-Parameter und Rückgabewerte sind jetzt im Managed-`Declare`-Vertrag als native
8-Byte-`CY`-Werte zugelassen. Der bestehende `VBCurrency`-Speicher bewahrt dabei die VB6-Skalierung
mit vier Nachkommastellen; ein echter `oleaut32!VarCyFromR8`-Aufruf prüft den `ByRef Currency`-
Rückweg. UDT-, Pointer- und Callback-ABI-Sonderfälle bleiben separate Roadmap-Schritte; die
Vollsuite umfasst nun **991 Tests**.

## Declare-Callback-Nachtrag

`AddressOf`-Prozeduren können im Managed-`Declare`-Pfad jetzt als native Funktionszeiger verwendet
werden. Die Runtime erzeugt dafür nicht-generische Delegate-Thunks mit `Winapi`-Calling-Convention
und hält die Delegate-Instanzen über die gesamte Prozesslaufzeit; statische Callback-Prozeduren und
Instanzmethoden im selben generierten Klassenobjekt werden unterstützt. Ein echter
`EnumSystemLocalesA`-Aufruf prüft die Callback-Ausführung; die Vollsuite umfasst nun **992 Tests**.

## Declare-ByRef-Variant-Nachtrag

Der bestehende Managed-P/Invoke-Vertrag behandelt `Variant`-Parameter jetzt ausdrücklich als native
`VARIANT`-Slots, auch wenn sie in VB6 als `ByRef` deklariert sind. Ein echter
`oleaut32!VariantChangeType`-Aufruf schreibt sowohl den Zielwert als auch seinen `VarType` zurück;
damit ist kein zusätzlicher String- oder Array-Sonderpuffer erforderlich. UDT-, Pointer- und
komplexe SAFEARRAY-Descriptoren bleiben weiterhin separate ABI-Schritte; die Vollsuite umfasst
nun **993 Tests**.

## Declare-Boolean-ABI-Nachtrag

`Boolean`-Parameter und Rückgabewerte externer `Declare`-Prozeduren erhalten jetzt den expliziten
`VARIANT_BOOL`-Marshalling-Descriptor. Dadurch verwendet der Managed-Emitter die 2-Byte-VB6-
Automation-Repräsentation statt des impliziten 4-Byte-Win32-`BOOL`-Vertrags. Ein echter
`oleaut32!VarBoolFromI4`-Aufruf prüft den `ByRef Boolean`-Rückweg; die Vollsuite umfasst nun
**994 Tests**.

## Declare-ByRef-Callback-Nachtrag

Blittable `ByRef`-Parameter bleiben in den dynamisch erzeugten nativen Callback-Delegaten jetzt
erhalten, einschließlich generierter VB6-UDT-Records. Ein echter
`user32!EnumDisplayMonitors`-Aufruf prüft die Rückgabe eines nativen `RECT*` in einen VB6-Callback
auf AnyCPU und x86; komplexe verschachtelte Pointer-, Variant-, String- und nicht-blittable
Callback-Signaturen benötigen weiterhin eigene ABI-Adapter. Die Vollsuite umfasst nun **995 Tests**.

## Callback-String-/BOOL-Nachtrag

Die dynamisch erzeugten `AddressOf`-Delegaten tragen für native Callback-Parameter und Rückgaben
jetzt explizite `BOOL`- beziehungsweise ANSI-String-Marshalling-Attribute. Ein echter
`kernel32!EnumSystemLocalesA`-Aufruf prüft einen `String`-Callbackparameter und den
vier-Byte-Win32-`Boolean`-Rückgabevertrag auf AnyCPU und x86; Variant-, UDT- und verschachtelte
Pointer-Callbacks bleiben als separate komplexe ABI-Schritte offen. Die Vollsuite umfasst nun
**996 Tests**.

## Declare-StrPtr-Nachtrag

`ByVal StrPtr(text)` in einem `Declare ... As Any`-Aufruf verwendet jetzt einen temporären
UTF-16-Puffer mit deterministischer Freigabe. Ist das Ziel eine beschreibbare Stringvariable,
wird der native Inhalt nach dem Aufruf mit der ursprünglichen Zeichenlänge zurückübertragen.
Ein echter `kernel32!RtlMoveMemory`-Roundtrip ist auf AnyCPU und x86 regressionsgesichert;
direkte freie `StrPtr`-Aufrufe und weitere rohe String-/UDT-Pointer bleiben bewusst separate
native Speicherverträge. Die Vollsuite umfasst nun **997 Tests**.

## Variant-Callback-Nachtrag

Einfache `Variant`-Parameter und -Rückgaben von `AddressOf`-Prozeduren werden im Managed-Emitter
jetzt als CLR-`object` mit explizitem `UnmanagedType.Struct`-Descriptor geführt. Die dynamische
Callback-Registry trennt diese nativen `VARIANT`-Slots vom unveränderten `Object`-ABI auch im
Delegattyp-Cache; eine Reflection- und Funktionszeiger-Regression prüft beide Formen. Variant-
Arrays, UDTs und verschachtelte Pointer im Callback bleiben separate komplexe ABI-Schritte; die
Vollsuite umfasst nun **998 Tests**.

## COM-Connection-Point-Variant-Nachtrag

Native COM-Connection-Point-Events verwenden jetzt einen eigenen dynamischen Delegattyp, der die
vom Managed-Emitter gesetzten `Variant`-Descriptors übernimmt und zusätzlich `VARIANT_BOOL` sowie
`BSTR` für Automation-Eventparameter abbildet. Der Win32-Callback-ABI bleibt davon getrennt; der
geprüfte x86-OCX-Pfad bleibt mit RichText- und Standard-Control-Events kompatibel. UDT-,
SAFEARRAY- und verschachtelte Pointer-Eventverträge bleiben weitere Interop-Schritte; die Vollsuite
umfasst nun **999 Tests**.

## COM-Connection-Point-SAFEARRAY-Nachtrag

TypeLib-/VB6-Eventhandler mit unterstützten typisierten SAFEARRAY-Parametern verwenden jetzt einen
rohen COM-Delegaten mit `System.Array` und explizitem `SafeArraySubType`. Der Adapter konvertiert
die native Array-Repräsentation in `VBArray<T>`, bewahrt Rang und echte Untergrenzen und schreibt
ByRef-Ersatzarrays über denselben Automation-Descriptor zurück; `Date` und `Currency` erhalten
ihre jeweiligen `VARTYPE`s auch in den erzeugten Assembly-Metadaten. UDT-, Pointer- und nicht
unterstützte SAFEARRAY-Elemente bleiben bewusst separate ABI-Schritte. Die Vollsuite umfasst nun
**1003 Tests**.

## Einzelprojekt-CLI-Nachtrag

`vb6c <projekt.vbp> --emit-assembly <ausgabeverzeichnis>` akzeptiert jetzt neben einem direkten
Dateipfad auch ein vorhandenes oder endungsloses Zielverzeichnis. Der Compiler erzeugt darin den
Legacy-Projektnamen aus `ExeName32` beziehungsweise `Name` und wählt für EXE-/OleDll-/ActiveX-
Projekte automatisch `.exe` beziehungsweise `.dll`; ein nicht vorhandenes endungsloses Verzeichnis
wird angelegt. Ein echter CLI-Prozessstart für eine EXE und die DLL-Ausgabe eines Library-Projekts
sind regressionsgesichert. Die Vollsuite umfasst nun **1005 Tests**.

## ObjPtr-COM-Nachtrag

`ObjPtr` verwendet jetzt den nativen `LongPtr`-Vertrag und liefert für echte COM-/ActiveX-
Objekte den kontrollierenden `IUnknown`-Zeiger, ohne die von `GetIUnknownForObject` erworbene
temporäre Referenz zu leaken. `Nothing` beziehungsweise ein leerer Wert ergibt null, skalare
Varianten melden Type Mismatch. Ein echter `htmlfile`-RCW sowie ein generierter `ObjPtr(Nothing)`-
Aufruf sind auf AnyCPU regressiongesichert. Direkte `VarPtr`-/`StrPtr`-Speicheradressen und
UDT-/Pointer-Marshalling bleiben wegen ihrer separaten Lebensdauer- und ABI-Regeln offen. Die
Vollsuite umfasst nun **1010 Tests**.

## AddressOf-Variant-Array-Nachtrag

`AddressOf`-Prozeduren mit `Variant()`-Parametern und -Rückgaben verwenden jetzt einen eigenen
nativen `SAFEARRAY(VARIANT)`-Delegaten. Der Callback-Adapter konvertiert die native `System.Array`-
Repräsentation in `VBArray<object>`, bewahrt echte Untergrenzen und schreibt ersetzte `ByRef`-
Arrays einschließlich ihrer neuen Bounds zurück. Ein echter Function-Pointer-Aufruf prüft sowohl
`ByRef Variant()` mit `ReDim` als auch einen `Variant()`-Rückgabewert auf AnyCPU und x86. UDT-,
Pointer-, String- und nicht unterstützte Arrayelement-ABIs bleiben separate Schritte. Die
Vollsuite umfasst nun **1011 Tests**.

## CLI-Entry-Point-Diagnostik-Nachtrag

Die öffentliche `AnalyzeForEmission()`-Analyse wendet jetzt denselben Entry-Point-Vertrag wie
die Managed-Emission an. Dadurch melden `vb6c <projekt.vbp> --report` und
`vb6c <projekt.vbg> --report` fehlende oder ungültige EXE-Startpunkte bereits mit
`VB6PRJ0004`/`VB6PRJ0005`; gültige Form-Starts und Library-Projekte ohne `Sub Main` bleiben
zulässig. Die VBG-Vorprüfung verwendet denselben Vertrag, bevor einzelne Projekte emittiert
werden. Zwei echte CLI-Prozessregressionen decken Einzelprojekt und Gruppe ab; die Vollsuite
umfasst nun **1013 Tests**.

## VISIA-Managed-Emit-Messpunkt

Der aktuelle `.vbp`-Vertrag analysiert das vollständige VISIA-Projekt mit **40 von 40** fehlerfreien
Projektitems und **0** Parser-, Lexer- oder Semantikdiagnosen. Zusätzlich erzeugt
`vb6c conformance/VISIA/4.8.7.1/prjVisia.vbp --emit-assembly <verzeichnis> --x86` erfolgreich die
Managed-Assembly, den nativen x86-AppHost, PDB und Runtime-Dateien. Der Conformance-Ratchet prüft
jetzt sowohl die 40/40-Schwelle als auch die Diagnosezahl 0; die Vollsuite umfasst **1014 Tests**.

## LongPtr-SAFEARRAY-Nachtrag

`LongPtr()`-Arrays verwenden im Managed-Interop-Pfad jetzt einen expliziten nativen Elementvertrag:
`VT_I4` für x86 und `VT_I8` für x64. Das gilt für `ByRef`-`Declare`-SAFEARRAYs sowie für
`AddressOf`-Callbackparameter und -Rückgaben; der Adapter bewahrt VB6-Untergrenzen und schreibt
ersetzte Arrays inklusive neuer Bounds zurück. COM-Event-Delegaten verwenden denselben typisierten
Arraypfad. Ein `AnyCPU`-Emit wird für diesen architekturabhängigen Vertrag kontrolliert mit einer
Backenddiagnose abgelehnt. Runtime-, Reflection- und End-to-End-Regressionen laufen auf x86 und
x64; die Vollsuite umfasst nun **1019 Tests**.

## Declare-Dispatch-SAFEARRAY-Nachtrag

`Declare`-Parameter der Form `ByRef values() As Object` beziehungsweise `As Control` werden im
Managed-Backend jetzt als native `SAFEARRAY(VT_DISPATCH)**`-Argumente materialisiert. Die Runtime
schreibt echte COM-/ActiveX-`IDispatch`-Einträge direkt in den nativen Deskriptor, entpackt
Host-Provider vor dem Aufruf und lässt `Nothing`-Elemente als nulles Dispatch-Element bestehen.
Beim Write-back werden COM-Objekte übernommen und native null-Dispatch-Einträge wieder als VB6-
`Nothing` dargestellt. Bounds, Dimensionen und die bestehende Array-Identität bleiben erhalten;
UDT-, Pointer- und verschachtelte String-Arrays bleiben separate ABI-Schritte. Die Regression
prüft Emitter-Metadaten für Object/Control und einen echten `Scripting.Dictionary`-Roundtrip;
die Vollsuite umfasst nun **1021 Tests**.

## Callback-String-SAFEARRAY-Nachtrag

`AddressOf`-Prozeduren können `ByRef values() As String` jetzt als nativen
`SAFEARRAY(VT_BSTR)`-Callbackparameter und `String()`-Rückgabewert verwenden. Der Managed-Adapter
bewahrt die VB6-Untergrenzen, konvertiert zwischen `VBArray<string>` und `System.Array` und schreibt
ersetzte Bounds sowie Inhalte über die native Delegate-Grenze zurück. Verschachtelte String-Pointer-
ABIs, Stringfelder in UDTs und weitere rohe Pointerverträge bleiben separate Interop-Schritte;
die Regression läuft auf x86 und x64, die Vollsuite umfasst nun **1022 Tests**.

## Variant-Vergleichs-Nachtrag

Variant-Vergleiche ordnen einen numerischen Wert jetzt vor einem nicht numerisch konvertierbaren
String ein, statt in einen CLR-Typvergleich zu fallen. Das gilt auch für erhaltene Date-Variantwerte;
numerische Strings und reine String-zu-String-Vergleiche behalten ihre bisherigen Promotions- bzw.
lexikalischen Regeln. Direkte Runtime- und kompilierte Managed-Regressionen decken `<`, `=`, `>` und
den Date-Fall ab. Die abschließende Variant-Promotionstabelle sowie Objekt-/Array-Varianten bleiben
weiterhin offen; die Vollsuite umfasst nun **1024 Tests**.

## Variant-Math-State-Nachtrag

`Abs`, `Fix` und `Round` verwenden jetzt denselben `Missing`-/Array-Guard wie die übrigen
Variant-Math-Pfade. Ein ausgelassenes `Optional Variant`-Argument führt damit deterministisch zum
VB6-Fehler 448, eine Array-Variante zum Type-Mismatch 13, und die bestehende `Null`-/`Empty`-
Semantik bleibt unverändert. Runtime- und kompilierte Regressionen decken beide Zustände für alle
drei Intrinsics ab; die Vollsuite umfasst nun **1026 Tests**.

## OLE-Date-Variant-Nachtrag

`VBDateValue` und `DateTime` werden in den zentralen numerischen `C*`-Konversionen jetzt als
OLE-Automation-Doubles behandelt. Das schließt `CDbl`, `CDec`, Integer-/Pointer-/Currency- und
Single-Konversionen, `CBool`/`CStr` sowie Variant-Addition und -Subtraktion ein; Date-Arithmetik
behält dabei den `Date`-Subtype. Damit können auch aus COM-Dispatch stammende `DateTime`-Werte den
gleichen Managed-Variantpfad wie interne VB6-Datewerte nutzen. Die Regression deckt Konversionen
und Date-Arithmetik ab; die Vollsuite umfasst nun **1027 Tests**.

## Managed-Interop- und UDT-Nachtrag

Managed-`LSet` unterstützt jetzt unterschiedlich aufgebaute, rein skalare UDTs: Der Rohdatentransfer
schneidet auf die Zielgröße zu beziehungsweise füllt den Rest mit Nullbytes auf. Die generische
`ref`-Runtime-Signatur hält das Ziel während temporärer Marshaling-Puffer als verwaltete Referenz
stabil. Layouts mit Strings, Arrays, `Variant`, Referenzen, `Boolean` oder `LongPtr` bleiben bis zu
ihrem eigenen ABI-Vertrag diagnostisch geschützt.

`Chr` und `Asc` verwenden für den erweiterten Bereich `128..255` deterministisch Windows-1252;
`ChrW` und `AscW` bleiben UTF-16-basiert. Nicht abbildbare Zeichen und die undefinierten
Windows-1252-Bytes werden kontrolliert abgelehnt.

`DateTime` wird in den Managed-`AddressOf`- und COM-Event-SAFEARRAY-Verträgen jetzt als `VT_DATE`
beschrieben. Bounds, ByRef-Write-back und Ersatzarrays sind für Callback- und Event-Adapter
regressionsgesichert; ein echter externer nativer COM-Connection-Point mit `VT_DATE` bleibt ein
separater Integrationsschritt. Die Vollsuite umfasst nun **1036 Tests**.

## Dokumentationsabgleich: reg-free COM, `VT_UNKNOWN` und Variant/Decimal

Der aktuelle Baum belegt jetzt den vollständigen reg-free-Manifest-Schritt für den Managed-COM-
Pfad: `--com-host` erzeugt den nativen `.comhost.dll`-Loader, `--com-manifest` schreibt daneben
ein Side-by-Side-Manifest mit Assembly-Identität, Architektur, CLSID und `ProgID`; der MSBuild-
SDK-Vertrag reicht beide Optionen weiter. Eine Windows-CLI-Regression erzeugt das Manifest und
aktiviert die Klasse anschließend weiterhin über den COM-Host. Dieser Vertrag ist damit für den
Managed-Library-Pfad belegt; Registrierung, vollständige TypeLib-Emission und der native LLVM-
COM-Server bleiben offen.

`Declare`-Objektarrays unterstützen jetzt zusätzlich `SAFEARRAY(VT_UNKNOWN)`: `IUnknown*`-Elemente,
`Nothing`, Referenzfreigabe sowie der Rückweg nach `VBArray<object>` sind im Runtime-Code und in
einer Windows-COM-Regression belegt. `VT_RECORD`/`IRecordInfo`, rohe Pointer-/C-Array-Verträge
und nicht unterstützte SAFEARRAY-Elementtypen bleiben offen.

Der Variant/Decimal-Pfad enthält weiterhin belegte Teilverträge für Decimal-Subtype 14,
arithmetische Decimal-Operationen, Date-Konversionen und mehrere `Missing`-/Array-Guards. Die
vollständige VB6-Promotionstabelle ist nicht erledigt; der aktuelle operator-spezifische Vertrag
bleibt jedoch erhalten: `Currency * Double` liefert `Currency`, während `Currency + Double`
`Double` liefert. Der serielle Solution-Lauf vom 25.08.2026 umfasst **1043 Testfälle**, davon
**1043 bestanden** und **0 fehlgeschlagen**. Nach dem Lauf blieb kein sichtbares Win32-
Messagebox- oder Dialogfenster offen.

## MSBuild-Designer-Input-Nachtrag

Das MSBuild-SDK verfolgt bei Einzelprojekten und `.vbg`-Gruppen jetzt auch Legacy-
`.dsr`-Designerquellen als Inputs. Änderungen an einer `Designer=...; Datei.dsr`-Quelle
invalidieren damit den inkrementellen Compile-Stempel und lösen die CLI-Emission erneut aus.
Die Regression ist über einen echten `dotnet msbuild`-Gruppenbuild abgesichert. Der serielle
Solution-Lauf umfasst damit weiterhin **1043 Testfälle**, davon **1043 bestanden** und **0 fehlgeschlagen**.

## Boolean-UDT-LSet-Nachtrag

Der Managed-`LSet`-Vertrag akzeptiert jetzt auch UDTs mit VB6-`Boolean`-Feldern. Der Emitter
kennzeichnet diese Felder als 2-Byte-`VARIANT_BOOL`, während Layoutprüfung und Runtime-Rohtransfer
dieselbe Größe und Ausrichtung verwenden. Ein kompilierter Transfer zwischen unterschiedlich
aufgebauten UDTs ist regressionsgesichert. Nicht unterstützte dynamische Strings, Arrays,
`Variant`-Felder und weitere native ABI-Layouts bleiben weiterhin separate ABI-Schritte; die
Vollsuite umfasst nun **1046 Testfälle**, davon **1046 bestanden** und **0 fehlgeschlagen**.

## Empty-/Single-Variant-Divisionsnachtrag

Bei der Variant-Division wird `Empty` jetzt wie ein Integer-Operand in die
Promotionentscheidung einbezogen. Dadurch liefert `Empty / Single` einen `Single`-Variantwert
statt fälschlich `Double`; `Single / Empty` bewahrt denselben effektiven Typ und meldet danach
korrekt Division durch null. Runtime- und kompilierter Managed-Ausführungspfad sind regressions-
gesichert. Die Vollsuite umfasst damit **1045 Testfälle**, davon **1045 bestanden** und **0
fehlgeschlagen**.

## LongPtr-UDT-LSet-Nachtrag

Der Managed-`LSet`-Vertrag akzeptiert jetzt zusätzlich native-width `LongPtr`-Felder in
unterschiedlich aufgebauten UDTs. Die generierte Struct-Repräsentation verwendet `IntPtr`;
Layout-Guard und Rohtransfer sind für die aktuelle Prozessarchitektur ausgelegt. Der x64-
Managed-Ausführungspfad ist mit einem kompilierten Pointer-/Long-Transfer abgesichert;
Cross-Architecture-Targeting sowie dynamische Strings, Arrays, `Variant`-Felder, verschachtelte
Pointer und weitere rohe C-Array-Layouts bleiben offen. Die Vollsuite umfasst nun **1047
Testfälle**, davon **1047 bestanden** und **0 fehlgeschlagen**.

## MSBuild-SDK-Validierungsnachtrag

Das VB6-MSBuild-SDK bricht jetzt mit einer eindeutigen Diagnose ab, wenn das konfigurierte
`.vbp` oder `.vbg` nicht existiert oder beide Eingabearten gleichzeitig gesetzt sind. Damit
führt ein falsch konfiguriertes SDK-Projekt nicht mehr stillschweigend nur einen normalen
.NET-Build aus. Der Einzelprojektpfad verwendet zusätzlich einen Compile-Stempel mit
Output-Manifest: unveränderte Builds werden übersprungen, fehlende Artefakte automatisch
repariert. Beide Verträge sind über echte `dotnet msbuild`-Regressionen abgesichert; die
Vollsuite umfasst nun **1049 Testfälle**, davon **1049 bestanden** und **0 fehlgeschlagen**.

## COM-EXCEPINFO-Nachtrag

Der native `IDispatch::Invoke`-Pfad gibt die von COM gelieferten `EXCEPINFO`-BSTR-Felder
`Source`, `Description` und `HelpFile` jetzt auch bei Fehler-HRESULTs und Folgefehlern sicher
mit `SysFreeString` frei. Der Windows-only Runtime-Test prüft die vollständige Bereinigung;
die Dispatch-Aufrufe verwenden außerdem die aktuelle Prozess-LCID mit einem stabilen
Invariant-Fallback. Die Vollsuite umfasst nun **1051 Testfälle**, davon **1051 bestanden** und
**0 fehlgeschlagen**.

## Managed-Form-AppHost-Nachtrag

Form-Startup-Projekte können über den CLI-`--emit-assembly`-Pfad jetzt direkt als sichtbare
Managed-Windows-Anwendungen gestartet werden. Der Emit aktiviert dafür den optionalen
`VB6.Runtime.WinForms`-Host, kopiert dessen Runtime-Assembly neben die erzeugte Anwendung,
fordert `Microsoft.WindowsDesktop.App` an und markiert den generierten Entry-Point als STA.
Der Host startet nach `Load`/`Show` die Nachrichtenschleife, räumt sich nach `Unload` oder dem
Schließen des Startformulars auf und übernimmt einen bereits gesetzten externen Runner-Host ohne
doppelte Registrierung. Die öffentliche Compiler-API bleibt standardmäßig headless; mit
`ManagedEmitOptions.EnableWinFormsHost` ist derselbe direkte AppHost-Vertrag opt-in verfügbar.
Vollständige `.frx`-/MDI-/OCX-/Connection-Point-Abdeckung bleibt in M9 offen. Die Vollsuite
umfasst nun **1055 Testfälle**, davon **1055 bestanden** und **0 fehlgeschlagen**.

## nativer-OCX-CLI-Nachtrag

Der CLI-Gruppenpfad ist zusätzlich als echter Prozessvertrag abgesichert: Eine `.vbg` mit
`RICHTX32.OCX` und `RichTextLib.RichTextBox` wird für x86 emittiert, startet den erzeugten
Windows-AppHost mit der registrierten `AxHost`-Komponente und beendet sich nach `Unload Me`
fehlerfrei. Damit ist neben Analyse und Artefakt-Erzeugung auch der direkte Legacy-Startpfad für
ein installiertes Standard-OCX geprüft; vollständige OCX-Event-/ABI-Abdeckung und weitere
Bitness-/Designer-Sonderfälle bleiben separate M9-Schritte. Die Vollsuite umfasst nun
**1056 Testfälle**, davon **1056 bestanden** und **0 fehlgeschlagen**.

## COM-ROT-Nachtrag

`GetObject(, "ProgID")` bindet auf Windows nun ein bereits laufendes, registriertes COM-Objekt
über die Running Object Table. Monikerpfade, Host-Sinks und der deterministische headless
Platzhalter bleiben erhalten; ein registrierter ProgID ohne laufende Instanz liefert den nativen
COM-Fehler statt stillschweigend ein falsches Objekt. Die vollständige ROT-/Server-Lebensdauer
und die übrige COM-ABI bleiben separate Interop-Schritte.
Die Vollsuite umfasst nun **1057 Testfälle**, davon **1057 bestanden** und **0 fehlgeschlagen**.

## Shell-Nachtrag

`Shell` startet auf Windows nun echte Prozesse, trennt die üblichen VB6-Befehlszeilenformen in
Programm und Argumente und bildet `vbHide`, Minimieren und Maximieren auf den Windows-Prozessstil
ab. Nicht-Windows-/headless-Läufe behalten den deterministischen Rückgabewert `0`.

## COM-Connection-Point-Lifecycle-Nachtrag

COM-Event-Subscriptions halten jetzt neben dem VB6-/Host-Wrapper auch den tatsächlich verbundenen
RCW fest. Dadurch kann `Unsubscribe` den ursprünglichen Connection Point noch entfernen, wenn ein
ActiveX-Wrapper seine aktuelle `ComObject`-Referenz inzwischen verloren oder freigegeben hat.
Die Aufräumlogik behandelt bereits ungültige RCWs als best-effort Cleanup, entfernt aber weiterhin
die Managed-Subscription. Eine echte x86-RichTextBox-Regression setzt den Provider nach der
Verbindung zurück, trennt den Handler und prüft, dass ein anschließendes `Change`-Event nicht mehr
ankommt. Die Vollsuite umfasst nun **1059 Testfälle**, davon **1059 bestanden** und **0 fehlgeschlagen**.

## SendKeys-Host-Nachtrag

`SendKeys` wird aus dem portablen Runtime-Vertrag jetzt an `IVB6Host` weitergereicht. Der
WinForms-Host verwendet für `Wait=True` `SendWait` und für den asynchronen VB6-Fall `Send`;
headless Hosts behalten den deterministischen No-op-Vertrag. Die Runtime-Weiterleitung ist mit
einem konfigurierten Host regressiongesichert. Die Vollsuite umfasst nun **1060 Testfälle**, davon
**1060 bestanden** und **0 fehlgeschlagen**.

## LoadPicture-Dateipfad-Nachtrag

Der WinForms-Host lädt `VBPicture`-Werte aus `LoadPicture("datei")` jetzt auch in den normalen
`Picture`-Propertypfaden von Forms und Controls. Die Bilddaten werden unabhängig vom Quelldatei-
Handle in eine eigene `Bitmap`-Instanz kopiert; der bestehende `.frx`- und `PaintPicture`-Pfad
bleibt unverändert. Eine echte PNG-Regression setzt `PictureBox.Picture` über `LoadPicture` und
prüft die resultierende Bildgröße. Die Vollsuite umfasst nun **1061 Testfälle**, davon **1061
bestanden** und **0 fehlgeschlagen**.

## Clipboard.GetText-Nachtrag

`Clipboard.GetText` wird jetzt aus dem gebundenen Member-Aufruf direkt auf den typisierten
`InteractionClipboardGetText`-IR-Vertrag abgesenkt und im Managed-Emitter an
`VBInteraction.ClipboardGetText` gebunden. Headless-/Testhosts können über `ClipboardTextSink`
deterministischen Text liefern; der WinForms-Host liest Text über die Windows-Zwischenablage und
behandelt fehlende UI-/Clipboard-Handles als leeren Wert. Compiler- und Runtime-Regressionen
prüfen getrennt den emittierten IR-Aufruf und den Sink-Vertrag. Die Vollsuite umfasst nun **1063
Testfälle**, davon **1063 bestanden** und **0 fehlgeschlagen**.

## Command-Prozessargument-Nachtrag

Managed-Anwendungen initialisieren `Command` jetzt am generierten Application-Entry-Point aus der
aktuellen Prozesszeile. Dadurch liefert ein direkt gestarteter CLI-AppHost auch quotierte Argumente
wie `first "two words"` im VB6-kompatiblen Command-String. Der `GeneratedApplicationRunner` setzt
seine explizit übergebenen Argumente als Host-Override, bevor der Entry-Point ausgeführt wird;
portable Runtime-Aufrufe ohne generierte Application-Initialisierung bleiben deterministisch leer.
Eine echte CLI-Regression startet den erzeugten AppHost mit zwei Argumenten und prüft die vollständige
Ausgabe. Die Vollsuite umfasst nun **1065 Testfälle**, davon **1065 bestanden** und **0 fehlgeschlagen**.

## Err-Feld-Nachtrag

Der Managed-`Err`-Vertrag stellt jetzt zusätzlich `HelpFile`, `HelpContext` und `LastDllError`
bereit. `Err.Raise` bewahrt die beiden Hilfeangaben im threadlokalen Fehlerzustand; `LastDllError`
liest den von Managed-`Declare`-Aufrufen gesetzten nativen Last-Error-Slot. Dafür werden emittierte
P/Invoke-Imports mit dem expliziten `SetLastError`-Metadatenflag versehen. `Err.Clear` setzt die
gespeicherten Hilfeangaben wieder auf die VB6-Defaultwerte zurück. Runtime-, Managed-End-to-End-
und echter `kernel32!SetLastError`-Regressionstest sichern den Vertrag. Die Vollsuite umfasst nun
**1068 Testfälle**, davon **1068 bestanden** und **0 fehlgeschlagen**.

## Collection-Fehlercode-Nachtrag

Der Managed-`Collection`-Pfad verwendet jetzt die relevanten VB6-Fehlernummern: ein bereits
vergebener Schlüssel liefert **457**, ungültige Schlüssel oder ein ungültiger einbasierter Index
liefern **5**, und `Add` mit gleichzeitig gesetzten `Before`- und `After`-Argumenten wird ebenfalls
als Fehler **5** behandelt. Die Fehler werden über den threadlokalen `Err`-Dispatcher geführt und
bleiben damit unter `On Error Resume Next` aus VB6-Code auswertbar. Direkte Runtime- und generierte
Managed-Programmtests sichern Duplicate-Key, Missing-Key und ungültige Positionsangaben. Die
Vollsuite umfasst nun **1070 Testfälle**, davon **1070 bestanden** und **0 fehlgeschlagen**.

## LBound-/UBound-Variant-Nachtrag

`LBound` und `UBound` akzeptieren weiterhin Variant-Ausdrücke, deren Array-Natur erst zur
Laufzeit feststeht. Enthält der Ausdruck dann kein Array, liefern die Runtime-Funktionen jetzt
den VB6-Fehler **13 (Type mismatch)** statt einer generischen CLR-Fehlernummer. Array-Werte,
echte CLR-Arrays und die bisherigen Bounds-/Dimensionsfehler bleiben unverändert.

## VBG-Referenzabschluss

Eine `.vbg`-Analyse prüft jetzt nicht nur `Project=`-Einträge und `StartupProject=`, sondern auch
projektbezogene `Reference=`-Einträge jedes enthaltenen `.vbp`. Verweist ein Projekt auf ein
vorhandenes, aber nicht in der Gruppe deklariertes `.vbp`, wird `VB6VBG0008` ausgegeben und die
Gruppenemission erzeugt kein unvollständiges Consumer-Artefakt. Der Compiler- und CLI-Prozesspfad
sind regressionsgesichert.

## registrierter COM-Referenzabschluss

Historische `Reference=`- und `Object=`-Einträge ohne auflösbaren lokalen Dateipfad können auf
Windows jetzt über `HKCR\TypeLib` beziehungsweise `HKCR\CLSID` anhand GUID, Version, LCID und
Prozessbitness aufgelöst werden. Explizit vorhandene Projekt-/Dateipfade behalten Vorrang; der
Managed-TypeLib-Importer kann dadurch registrierte `stdole`-/OCX-Verträge auch aus alten VBP-
Dateinamen laden. Eine echte registrierte `stdole2.tlb`-Regression deckt die Bindung ab.

## Object-/Variant-Array-Descriptor-Nachtrag

`Object()`-Arrays werden im Managed-Backend weiterhin als `VBArray<object>` gespeichert, tragen
aber jetzt zusätzlich ihren VB6-Elementnamen und den Automation-Subtype. Dadurch liefern lokale
`ReDim`-Arrays, Variant-Zuweisungen, `Clone`, `ReDim Preserve` und SAFEARRAY-Ersatzwerte
unterscheidbar `Object()`/`8201`, während gewöhnliche `Variant()`-Arrays bei `Variant()`/`8204`
bleiben. Der Descriptor wird außerdem durch die Runtime-Konvertierung und die Reflection-basierten
Callback-/COM-Event-Adapter nicht mehr durch mehrdeutige `FromObject`-Überladungen verloren.

Die aktuelle Vollsuite umfasst nun **1080 Testfälle**, davon **1080 bestanden** und
**0 fehlgeschlagen**.

## MSBuild-VBG-Output-Reconciliation-Nachtrag

Der inkrementelle `VB6ProjectGroup`-Target liest vor einer erneuten Gruppenemission sein
eigenes Output-Manifest und entfernt die dort verzeichneten vorherigen Artefakte. Nach dem
Compile wird das Manifest mit den tatsächlich neu erzeugten Assemblies, AppHosts, Runtime-
Dateien, PDBs und Manifests neu geschrieben. Dadurch bleiben beim Entfernen eines `Project=`-
Eintrags oder beim Ändern eines `ExeName32`-Ziels keine veralteten Projektartefakte im
Gruppenverzeichnis liegen. Der Vertrag ist über den gepackten SDK-Pfad und einen echten
`dotnet msbuild`-Rebuild regressionsgesichert; die Vollsuite bleibt bei **1080 Testfällen**.

## MSBuild-VBG-Referenzinput-Nachtrag

Der SDK-Inputvertrag verfolgt unterhalb des VBP-/VBG-Verzeichnisses nun auch lokale
`.ocx`-, `.tlb`-, `.olb`- sowie TypeLib-tragende `.dll`- und `.exe`-Dateien. Eine geänderte
ActiveX- oder TypeLib-Datei invalidiert damit den inkrementellen Build, während das konfigurierte
Ausgabeverzeichnis aus dem Scan ausgeschlossen bleibt und keinen Selbsttrigger erzeugt. Der
gepackte SDK-Pfad ist mit einer echten `.ocx`-Änderung regressionsgesichert; die Vollsuite bleibt
bei **1080 Testfällen**.

## Variant-&-Null-Nachtrag

Der Runtime- und Managed-Emitter-Vertrag bildet die VBA-Sonderregel für den `&`-Operator jetzt
vollständig ab: Ein einzelnes `Null` wird als leerer String verkettet, `Null & Null` bleibt
dagegen ein `Null`-Variant. Die Regression deckt den direkten Runtime-Aufruf sowie den
kompilierten Pfad mit `IsNull` und `TypeName` ab. Die aktuelle Vollsuite umfasst **1082
Testfälle**, davon **1082 bestanden** und **0 fehlgeschlagen**.

## MSBuild-VBP-Output-Reconciliation-Nachtrag

Der inkrementelle `VB6Project`-Target verfolgt nun auch die MSBuild-Projektdatei als Input und
liest vor einer erneuten Einzelprojektemission sein eigenes Output-Manifest. Ändert sich dadurch
der konfigurierte `VB6CompilerOutput`-Pfad, werden der alte Assembly-/PDB-/Runtime-Satz entfernt
und der neue Output-Satz geschrieben. Der gepackte SDK-Pfad ist mit einem echten
`dotnet msbuild`-Rename regressionsgesichert; die Vollsuite bleibt bei **1082 Testfällen**.

## Decimal-Debug-Ausgabe-Nachtrag

`Debug.Print` kuerzt Decimal-Variantwerte nicht mehr auf 15 signifikante Stellen. Die Runtime
verwendet fuer den Decimal-Subtype jetzt denselben `G29`-Praezisionsvertrag wie `CStr`, sodass
hochpraezise `CDec`-Werte im direkten Runtime-Aufruf und im kompilierten Managed-Programm
vollstaendig erhalten bleiben. Die beiden Regressionen erhoehen die gemessene Vollsuite auf
**1084 Testfaelle**, davon **1084 bestanden** und **0 fehlgeschlagen**.

## Debug-Assert-Nachtrag

`Debug.Assert` wird nun kontextsensitiv hinter `Debug.` geparst, semantisch als Boolean-Ausdruck
gebunden und im kompilierten Managed-Programm vollstaendig elidiert. Damit werden auch Assert-
Bedingungen mit Seiteneffekten nicht ausgefuehrt, wie es der VB6-EXE-Vertrag verlangt. Parser- und
E2E-Regressionen erhoehen die gemessene Vollsuite auf **1087 Testfaelle**, davon **1087 bestanden**
und **0 fehlgeschlagen**.

## Portable-PDB-Prozedurscope-Nachtrag

Der Portable-PDB-Emitter schreibt nun fuer jede Methode mit IL einen Scope ueber den gesamten
Methodenkoerper, auch wenn die VB6-Prozedur keine Benutzer-Locals besitzt. Damit sind
Prozedurgrenzen fuer Debugger und Visual Studio nicht mehr von einer `Dim`-Deklaration abhaengig.
Der neue PDB-Test erhoeht die gemessene Vollsuite auf **1088 Testfaelle**, davon **1088 bestanden**
und **0 fehlgeschlagen**.

## Variant-SAFEARRAY-Zustandsnachtrag

`Variant()`-SAFEARRAYs werden fuer Managed-`Declare`- und Raw-COM-ByRef-Aufrufe nun als echte
`SAFEARRAY(VT_VARIANT)`-Deskriptoren materialisiert. Die einzelnen nativen `VARIANT`-Slots tragen
ihren VB6-Zustand direkt: `Empty`, `Null`, `Nothing`, `Missing`, `Error`, `Date` und `Currency`
werden nicht mehr ueber ein beliebiges `object[]`-Mapping verformt. Der Rueckweg liest Rang und
Bounds aus dem nativen Deskriptor und rekonstruiert die VB6-Zustaende, bevor kompatible Arrays in
den bestehenden `VBArray<T>`-Container zurueckgeschrieben werden. Die Runtime-Regression erhoeht
die gemessene Vollsuite auf **1089 Testfaelle**, davon **1089 bestanden** und **0 fehlgeschlagen**.

## Declare-SAFEARRAY-Rueckgabe-Nachtrag

Externe `Declare Function`-Signaturen mit `As T()` werden am nativen P/Invoke-Rand nun als
`System.Array` mit explizitem `SAFEARRAY(T)`-Marshalling emittiert. Direkt nach dem nativen Aufruf
wandelt der Managed-Emitter das Ergebnis ueber `VBArrayOperations.FromObject<T>` wieder in den
gebundenen `VBArray<T>`-Vertrag um; untere Grenzen, Rang und typisierte Elementkonversion bleiben
damit im restlichen VB6-Programm erhalten. Die Signaturvalidierung akzeptiert nur Elementtypen,
fuer die bereits ein nativer SAFEARRAY-Vertrag existiert. Der E2E-Test ruft
`oleaut32!SafeArrayCreateVector` ueber ein echtes VB6-`Declare` auf und prueft die nicht-nullbasierte
Rueckgabe. Die gemessene Vollsuite umfasst damit **1090 Testfaelle**, davon **1090 bestanden** und
**0 fehlgeschlagen**.

## x86-Default-Nachtrag

`.vbp`- und `.vbg`-Projekte emittieren ohne Plattformschalter jetzt als x86, weil jedes
Legacy-VB6-Projekt 32-Bit ist und seine ActiveX-Controls nicht in einen 64-Bit-Prozess laden.
`--x64` und `--anycpu` bleiben opt-in, einzelne Quelldateien bleiben AnyCpu, und
`ManagedEmitOptions` behaelt AnyCpu als API-Default: Die Entscheidung gehoert an die
Projektgrenze, nicht in den Emitter.

Damit faellt ein latenter Fehler weg. Ohne Schalter lieferte `CreateCompilationOptions` bisher
`null`, und `VBConditionalCompilation` fiel auf `IntPtr.Size == 8` zurueck — ein Legacy-Projekt
sah `#If Win64` also je nach Bitness des Compilerprozesses als wahr an, auf jeder 64-Bit-Maschine.
Jetzt ist `Win64` fuer Legacy-Projekte falsch, wie in VB6.

Zwei Regressionen decken das ab: ein `.vbp` ohne Schalter emittiert `I386` mit `Requires32Bit`,
und `#If Win64` waehlt den 32-Bit-Zweig. Am Korpus geprueft: `prjVisia.vbp` erzeugt Assembly und
AppHost als `I386`, die Paritaet bleibt bei 0 Fehlern und 40 von 40. Die gemessene Vollsuite
umfasst **1092 Testfaelle**, davon **1092 bestanden** und **0 fehlgeschlagen**.

## vbUseSystem-Nachtrag

`vbUseSystem` bleibt bewusst locale-abhaengig: Wer den Wert 0 uebergibt, fragt ausdruecklich die
Systemeinstellung ab, und ihn auf Sonntag festzunageln waere die Abweichung von VB6. Das ist die
eine gebilligte Ausnahme von der Invariant-Culture-Regel der Runtime und steht als solche in den
entschiedenen Weichenstellungen.

Die Runtime war dabei mit sich selbst uneins. `VBStrings.ToFirstDayOfWeek` loeste 0 ueber
`CurrentCulture` auf, waehrend `VBDateTime.ResolveFirstDayOfWeek` die Werte 0 und 1 gleich
behandelte und Sonntag lieferte. Derselbe VB6-Begriff verhielt sich also verschieden, je nachdem
ob er ueber `Format` oder ueber `Weekday`, `WeekdayName` und `DatePart` lief. `VBDateTime` folgt
jetzt derselben Regel; explizite Konstanten bleiben in beiden Pfaden kulturunabhaengig, und beide
Aufloeser verweisen im Kommentar aufeinander.

Zwei Regressionen halten beide Seiten fest: `vbUseSystem` liefert unter `en-US` und `de-DE`
verschiedene Wochentage und Wochennummern, explizite Konstanten dagegen dieselben. Die gemessene
Vollsuite umfasst **1094 Testfaelle**, davon **1094 bestanden** und **0 fehlgeschlagen**.

## Paint-und-AutoRedraw-Nachtrag

`Paint` war das einzige verbreitete Event, das der WinForms-Host nicht verdrahtet hat. Die
Korpusmessung ueber die 6 `.frm`- und 4 `.ctl`-Quellen zeigt 34 `Click`-, 14 `MouseDown`- und 13
`Resize`-Handler, die alle liefen, sowie 3 `Paint`-Handler, die nicht liefen. Gleichzeitig setzt
der Korpus 12 mal `AutoRedraw`, waehrend die Eigenschaft zwar gebunden und im Controlzustand
gespeichert, aber an keiner Stelle ausgewertet wurde.

Beides gehoert zusammen. VB6 zeichnet bei `AutoRedraw = True` in eine persistente Bitmap, die das
Control selbst wieder anzeigt, und feuert `Paint` dann **nicht**; bei `False` geht die Ausgabe
direkt auf die sichtbare Flaeche und ist beim naechsten Neuzeichnen verloren — genau deshalb gibt
es `Paint`. Der Host zeichnete bisher unbedingt persistent, verhielt sich also immer so, als waere
`AutoRedraw` eingeschaltet, obwohl der VB6-Default fuer Forms und PictureBoxen `False` ist.

`BeginDrawing` entscheidet das jetzt pro Zeichenoperation: innerhalb eines `Paint`-Handlers auf
dessen Zeichenkontext, bei `AutoRedraw` auf die persistente Flaeche mit anschliessendem
`Invalidate`, sonst direkt auf die sichtbare Flaeche. Wird `AutoRedraw` abgeschaltet, wird die
Bitmap wie in VB6 verworfen. `Paint` laeuft bewusst nicht ueber den generischen Reflection-Pfad,
weil der Dispatch Hostzustand braucht — die `AutoRedraw`-Abfrage und den Zeichenkontext fuer die
Dauer des Handlers; die Subscription wird trotzdem regulaer registriert, damit
`UnsubscribeEvent` weiter greift. Verdrahtet ist sie fuer Designer-Controls einschliesslich
Control-Array-Index, Forms und UserControls.

Drei Regressionen decken den Vertrag ab: `Paint` feuert nur bei abgeschaltetem `AutoRedraw`, eine
aus dem Handler gezeichnete Linie landet im Zeichenkontext, und das Abschalten verwirft die
persistente Flaeche. Die vier bestehenden Zeichentests setzen `AutoRedraw` jetzt ausdruecklich,
statt sich auf das alte unbedingte Verhalten zu verlassen. Die gemessene Vollsuite umfasst
**1097 Testfaelle**, davon **1097 bestanden** und **0 fehlgeschlagen**; die Korpusparitaet bleibt
bei 0 Fehlern und 40 von 40.

## ScaleMode-Nachtrag

Die Zeichenpfade kannten drei `ScaleMode`-Werte: Pixel, Point und alles uebrige als Twips. Inch,
Millimeter, Zentimeter und Character landeten damit stillschweigend auf Twips — jede Koordinate um
Groessenordnungen daneben, ohne Meldung. Genau der Fall, fuer den die Regel „Diagnose statt
Naeherung" gilt.

VB6 definiert jeden `ScaleMode` ausser `User` als feste Anzahl Einheiten pro Zoll, der Faktor ist
also exakt und nicht geschaetzt. `GetScaleFactors` liefert ihn jetzt fuer Twip, Point, Pixel,
Character, Inch, Millimeter und Zentimeter, und zwar **pro Achse**: Character ist die einzige
asymmetrische Einheit — 120 Twips breit, 240 Twips hoch —, was ein einzelner Skalar nicht
ausdruecken kann. Die Berechnung stand zuvor doppelt in `Line` und `PaintPicture` und liegt jetzt
an einer Stelle. `User` (0) bleibt Twips, solange kein eigener Massstab ueber `ScaleWidth` und
`ScaleHeight` existiert; das ist der Wert, den VB6 dort ebenfalls liefert. Ein `ScaleMode`
ausserhalb 0 bis 7 meldet wie in VB6 Fehler 380.

Zwei Regressionen: eine Zoll-Strecke deckt in allen sechs Einheiten dieselbe Pixelbreite ab,
Character trifft dabei mit 12 zu 6 Einheiten ein Quadrat, und `ScaleMode` 8 loest 380 aus.

Nebenbefund aus der Nachmessung: `DrawMode` kommt im Korpus gar nicht vor. Die drei Treffer der
urspruenglichen Zaehlung waren ein gleichnamiges Enum, ein Kommentar und ein
`SetROP2`-P/Invoke-Parameter, keine VB6-Eigenschaft. Die Roadmap fuehrt `DrawMode` deshalb wie MDI
als zurueckgestellt. Die gemessene Vollsuite umfasst **1099 Testfaelle**, davon **1099 bestanden**
und **0 fehlgeschlagen**; die Korpusparitaet bleibt bei 0 Fehlern und 40 von 40.

## Control-Array-Laufzeit-Nachtrag

Der Korpus laedt Control-Array-Elemente zur Laufzeit nach: `frmDesign.frm` ruft
`Load ctlButton(ctlButton.UBound + 1)` und `Unload Control(Control.Index)`, dazu kommen 16
Eventhandler mit `Index As Integer`. Gebunden wurde das bisher als gewoehnlicher `Load`-Intrinsic
mit ausgewertetem Argument — was nicht funktionieren kann: VB6 adressiert einen Slot, den `Load`
erst anlegen soll, sodass die Auswertung des Elements scheitert, bevor `Load` ueberhaupt laeuft.

Der Binder erkennt die Form jetzt als eigenes Statement und behaelt das Array als zuweisbaren
Platz. Voraussetzung ist ein Elementtyp mit Control-Vertrag; Formulare und Einzelcontrols behalten
den gewoehnlichen Intrinsic-Pfad. Gelowert wird nach dem Muster von `ReDim Preserve` — Platz laden,
Runtime rufen, Ergebnis zurueckschreiben —, denn das Wachsen ersetzt die Arrayreferenz, und erst
das Zurueckschreiben macht das neue Element ueberall sichtbar.

Die Runtime waechst das Array bis zum Index und waehlt das unterste vorhandene Element als
Vorlage, in VB6 also das vom Designer erzeugte. Ein bereits geladenes Element meldet Fehler 360,
ein Index unterhalb der Untergrenze Fehler 9. `Unload` leert den Slot, behaelt aber die Grenzen,
damit der Index adressierbar bleibt und erneut geladen werden kann.

Der WinForms-Host klont Typ, Position, Groesse, Schrift und Farben der Vorlage, haengt den Klon in
denselben Container und laesst ihn unsichtbar — wie in VB6, wo ein sofort sichtbares Element exakt
auf seiner Vorlage laege. Die Events laufen ueber den vorhandenen Designer-Pfad mit dem neuen
Arrayindex.

Vier Regressionen: Wachstum mit Vorlagenwahl, die beiden Fehlerfaelle, `Unload` mit erhaltenen
Grenzen sowie das Klonen im Host einschliesslich Wiederholaufruf und Entfernen. Die gemessene
Vollsuite umfasst **1103 Testfaelle**, davon **1103 bestanden** und **0 fehlgeschlagen**; die
Korpusparitaet bleibt bei 0 Fehlern und 40 von 40.

## OCX-Eventsignatur-Nachtrag

Der Korpus behandelt auf den fuenf verwendeten OCX-Typen drei Events, die ueber den intrinsischen
Satz hinausgehen: `NodeClick` auf `TreeView` mit typisiertem `Node`-Argument, `SelChange` auf
`RichTextBox` und `Dropdown` auf `ImageCombo`. `CommonDialog` und `ImageList` tragen im Korpus gar
keine Handler. Keines der drei war verdrahtet.

Die managed Adapter liefern sie jetzt mit der VB6-Signatur: `NodeClick` uebergibt den geklickten
Node statt der WinForms-Mausargumente, `SelChange` und `Dropdown` nehmen wie in VB6 keine
Argumente.

Dabei kam ein aelterer Fehler mit heraus. `FindEvent` uebersetzt VB6-Eventnamen auf ihre
WinForms-Entsprechung, aber die Designer-Controls umgingen das und uebergaben direkt
WinForms-Namen: `TextChanged`, `Enter`, `Leave` und `DoubleClick`. Fuer die managed Adapter war das
folgenlos, fuer einen nativen OCX nicht — dort geht der Name unuebersetzt an den
COM-Connection-Point, und `TextChanged` oder `Enter` bedeuten einem ActiveX-Control nichts.
`Change`, `GotFocus`, `LostFocus` und `DblClick` konnten auf dem nativen Pfad also gar nicht
feuern. Alle Subscriptions verwenden jetzt die VB6-Namen, die Uebersetzung liegt an der einen
Stelle, die dafuer vorgesehen ist.

Damit liefern beide Pfade dieselbe Signatur, wie es die Roadmap fuer M9 verlangt. Die Regression
deckt die drei neuen Events ab; dass die bestehenden Change- und Fokustests unveraendert gruen
bleiben, belegt die Namensuebersetzung. Die gemessene Vollsuite umfasst **1104 Testfaelle**, davon
**1104 bestanden** und **0 fehlgeschlagen**; die Korpusparitaet bleibt bei 0 Fehlern und 40 von 40.

## Testschulden-Nachtrag

Zwei Luecken im eigenen Sicherheitsnetz geschlossen.

`ConformanceCorpusTests` prueft bisher nur, dass der Korpus **gebunden** wird. Emission ist die
naechste Fehlerflaeche und zugleich das groesste Programm, das das Backend je sieht: 40 Module,
Forms, UserControls und Klassen in einer Assembly. Ein Emitterdefekt, den kein handgeschriebener
Test provoziert, zeigt sich dort zuerst. Der neue Fall emittiert das Projekt und prueft Assembly
und PDB.

Ausserdem hatten 21 der 72 Diagnose-Codes keine einzige Testreferenz. Bei einem Compiler, dessen
Regel „lieber melden als raten" lautet, ist ein ungetesteter Diagnosepfad das Loch im eigenen Netz:
Die Meldung kann veralten, die Bedingung kann aufhoeren zu greifen, und nichts faellt auf. 16 der
17 semantischen Codes sind jetzt abgedeckt — VB6S0002, 0009, 0012, 0013, 0014, 0017, 0040, 0042,
0043, 0057, 0059, 0060, 0061, 0065, 0066 und 0069. Die Faelle pruefen den Code, nicht den
Meldungstext, damit die Formulierung frei bleibt; drei laufen ueber den UDT-Deklarationsbinder, der
ein eigener Durchgang vor dem Prozedurbinder ist.

Ohne Test bleiben fuenf: `VB6L0002`, `VB6L0003` und `VB6L0004` liegen im eingefrorenen
LLVM-Emitter, `VB6E0002` ist der interne PDB-Fehlerkanal und braeuchte Fehlerinjektion, und
`VB6S0068` verlangt einen Interface-Vertrag aus einem Klassenprojekt statt einer einzelnen
Quelldatei.

Nebenbefund: `Open ... For Random` wird laengst gebunden, der urspruengliche Testfall fuer
`VB6S0057` traf deshalb nichts. Die gemessene Vollsuite umfasst **1121 Testfaelle**, davon **1121
bestanden** und **0 fehlgeschlagen**; die Korpusparitaet bleibt bei 0 Fehlern und 40 von 40.

## Nativer-OCX-Event-Nachtrag

Die Umstellung der Designer-Subscriptions auf VB6-Eventnamen war hergeleitet, nicht gemessen. Der
native Lauf im 32-Bit-Testhost gegen die registrierten OCX hat sie geprueft — und die Erklaerung
zur Haelfte widerlegt.

Zuerst fiel auf, dass die vorhandenen nativen Tests die Aenderung gar nicht beruehren: Sie pruefen
`Change` ueber `WithEvents`, und der einzige Designer-Konventions-Handler war `Editor_KeyPress` —
ein Name, der in VB6 und WinForms zufaellig gleich lautet und deshalb auch vorher funktionierte.
Das Fixture traegt jetzt `Editor_Change`, `Editor_GotFocus`, `Editor_LostFocus` und
`Editor_DblClick`.

Damit zeigte sich, dass ein VB6-Event auf einem ActiveX-Control aus zwei Quellen kommen kann.
`Change` und `DblClick` stammen aus dem Control und brauchen den VB6-Namen am Connection-Point;
mit `TextChanged` beziehungsweise `DoubleClick` feuert nichts. Fokus-Events dagegen sind
**Extender-Events**: In VB6 liefert sie der Container, im Event-Interface des OCX fehlen sie. Der
Host schickte sie trotzdem nur an den Connection-Point, sodass `GotFocus` und `LostFocus` auf einem
nativen OCX **mit keinem Namen** feuern konnten. Schlaegt die COM-Subscription fehl, versucht der
Host den Namen jetzt am hostenden Wrapper; `AxHost` lehnt geerbte Events ab, die das Control nicht
implementiert, und diese Absage wird als Antwort behandelt.

Die Nachpruefung an `MSComctlLib.TreeView` legte eine zweite Luecke offen:
`AttachOcxControlEvents` schaltete auf die CLR-Typen der managed Adapter, und ein nativer OCX ist
keiner davon. `NodeClick`, `SelChange` und `Dropdown` wurden auf dem nativen Pfad also nie
abonniert. Sie werden einem nativen Control jetzt unabhaengig vom CLR-Typ angeboten; der
Connection-Point nimmt an, was er kennt.

Nativ gemessen, jeweils mit Gegenprobe: `Change` und `DblClick` an RichTextBox, `NodeClick` an
TreeView, `GotFocus` und `LostFocus` an beiden. Die Extender-Regel gilt damit fuer beide geprueften
Controls. Nur managed geprueft bleiben `SelChange` und `Dropdown`.

Der TreeView-Test prueft zuerst `Click`: Kommt der an, aber `NodeClick` nicht, liegt es an der
Subscription und nicht an der Mauszustellung — ohne diese Trennung waere der Fehlschlag nicht zu
deuten. Die Klickpunkte werden abgetastet, damit der Treffer nicht an den Einrueckungsmassen des
OCX haengt; die Fokuspruefungen zaehlen "mindestens einmal", weil die Wiederholung ein
AxHost-Artefakt der Fokuswanderung zwischen Wrapper und innerem Fenster ist.

x86 mit `VB6_REQUIRE_NATIVE_OCX=1`: **47 von 47** gruen. Die gemessene x64-Vollsuite umfasst
**1122 Testfaelle**, davon **1122 bestanden** und **0 fehlgeschlagen**; die Korpusparitaet bleibt
bei 0 Fehlern und 40 von 40.

## Roadmap-Stabilisierung: serieller Prüfpfad und Plattformvertrag

`build.ps1` bündelt Restore, seriellen Release-Build, die 13 Testprojekte und den VISIA-Report;
die CI verwendet denselben Pfad. Das MSBuild-SDK unterstützt `VB6TargetPlatform` mit x86 als
Legacy-Default sowie validiertem x64-/AnyCpu-Opt-in für Einzelprojekte und Gruppen. Diagnosecodes
sind durch explizite Tests beziehungsweise dokumentierte Guards abgedeckt; IR-/Emittertests
sichern `Debug.Assert`, Control-Array-Lebenszyklus, PE-Architekturen und SAFEARRAY-Metadaten.
Der kanonische Lauf vom 27.08.2026 meldet 1172 von 1172 Tests grün und 40/40 VISIA-Items ohne
Fehler.

## Kompatibilitätsprofil-Vertrag

`VBCompatibilityProfile` ist als additive Runtime-/Compiler-API mit `Deterministic` als Default
und `VB6Sp6` als dokumentationsbasiertem x86-Profil umgesetzt. CLI und MSBuild-SDK akzeptieren
`--compatibility` beziehungsweise `VB6CompatibilityProfile`; IR und generierte Assemblies tragen
die Auswahl. Explizites x64/AnyCpu wird für `vb6-sp6` abgelehnt. Die neuen Compiler-, Emitter- und
CLI-Regressionen erhöhen die kanonische Suite auf **1172 von 1172** bestandene Tests; VISIA bleibt
bei 40/40. Da VB6 nicht installiert ist, bleibt `oracle-verified` bewusst offen.

## VB6-SP6-Kompatibilitätsmatrix

`docs/vb6-sp6-compatibility-matrix.json` inventarisiert die zentralen Sprach-, Runtime-,
Projekt-, COM/ActiveX-, Forms- und Build-Verträge mit Quellen, Locale-/Bitness-/COM-Rahmen,
Implementierungs- und Verifikationsstatus sowie portablen Erwartungsfällen. `build.ps1` validiert
die Matrixstruktur vor dem Build. Die Feingranularität einzelner Intrinsics und Stock-Controls
bleibt als offener Ausbau der Etappe A markiert.

`IVB6Host` trägt nun außerdem ein additives `CompatibilityProfile` mit deterministischem Default.
`WinFormsHost`, `WinFormsApplicationHost` und `GeneratedApplicationRunner` übernehmen das Profil
instanzbezogen aus dem Konstruktor beziehungsweise aus den Assembly-Metadaten. COM-/ActiveX-
Semantik und ABI-Pfade bleiben für die nächste Host-Etappe offen.

`VBStrings.StrConv` besitzt jetzt eine additive Profilüberladung: Der bestehende Aufruf bleibt
invariant und deterministisch, während `VB6Sp6` für die dokumentierte Locale-Schicht die aktive
Systemkultur verwendet. Der Managed-Emitter reicht das Profil als verstecktes, typisiertes
Argument aus dem IR weiter; Runtime- und End-to-End-Tests sichern beide Pfade.

## Managed-Fehlerautomat: `Erl` und aktive Handler

Der Managed-Emitter führt jetzt einen prozedurbezogenen Fehlerkontext. Numerische VB6-Zeilenlabels
werden im IR als `Erl`-Position gestempelt; `Err.Clear` löscht weiterhin nur den Fehlerwert, während
`Resume` den aktiven Handler beendet. Löst ein Handler selbst einen Fehler aus, wird dieser an den
Aufrufer weitergereicht, statt denselben Handler rekursiv erneut anzuspringen. Die Regressionen
decken `Erl`, `Resume` und den verschachtelten Handlerpfad ab; 417 Compiler- und 253 Runtime-Tests
sind grün, der VISIA-Report bleibt bei 40/40 fehlerfreien Projekt-Items.

## Forms-Zeichenvertrag: `Cls`

`Cls` ist als Host-Intrinsic in Symboltabelle, IR und Managed-Emitter aufgenommen. Im headless
Profil bleibt die Operation deterministisch über einen Sink beobachtbar; `WinFormsHost` leert
wahlweise den aktiven Paint-Kontext, die persistente `AutoRedraw`-Fläche oder die sichtbare Fläche.
Compiler-, Runtime- und WinForms-Tests sichern den Vertrag.

## `VB6Sp6`-Locale-Schicht für ANSI-Strings

`LenB`, `Asc` und `Chr` erhalten additive profilbewusste Überladungen. Das deterministische Profil
behält die bisherigen UTF-16-/Windows-1252-Verträge; `VB6Sp6` löst die aktive Windows-ANSI-Codepage
für Bytezählung und Bytezeichen auf. Der Managed-Emitter reicht das Profil als verstecktes Argument
weiter, ohne bestehende Runtime-Signaturen zu verändern.

## `VB6Sp6`-Locale-Schicht fuer Format

`Format` besitzt nun eine additive profilbewusste Ueberladung. Der bestehende Vier-Argument-
Aufruf bleibt invariant und deterministisch; `VB6Sp6` verwendet fuer numerische Masken die
aktive Kultur und lokalisiert Monats-/Wochentagsnamen. Der Managed-Emitter reicht das Profil als
verstecktes Argument weiter. Der de-DE-Regressionsfall ist in der Runtime-Suite dokumentiert;
die Matrix fuehrt ihn als `profile.format-locale`.

## Standardbibliothek: Kern-Finanzfunktionen

`FV`, `PV`, `PMT`, `NPV`, `IRR`, `SLN`, `SYD` und `DDB` sind als Double-basierte
Managed-Intrinsics aufgenommen. Nullzins, End-/Anfangsperioden (`Type`), ParamArray-Abzinsung,
iterative Cashflow-Wurzel, AbschreibungsplÃ¤ne und ungueltige Argumente sind als Runtime-Vertraege
abgedeckt; Compiler-End-to-End- und Runtime-Regressionen sichern die Formeln.

## Headless-MSBuild: deklarationsbasierter Resolver

Das SDK verwendet für Einzelprojekte und Projektgruppen jetzt einen CLI-Resolver, der aus `.vbp`
und `.vbg` ein exaktes Input-Manifest mit SHA-256-Fingerprints erzeugt. Erfasst werden nur
deklarierte Quell-/Designerdateien, `.frx`-Sidecars, `RESFILE`-Ressourcen sowie `Reference=`- und
`Object=`-Dateien; nicht deklarierte Dateien in Unterordnern lösen keinen Rebuild mehr aus.
`ResolveVB6Project`, `ResolveVB6ProjectGroup`, `GetVB6ProjectOutputs` und
`GetVB6ProjectGroupOutputs` stehen als stabile Targets zur Verfügung. `DesignTimeBuild=true`
validiert und löst das Manifest auf, überspringt aber die Assembly-Emission. Die gepackte
ProjectSystem-Task und vollständige Clean/Rebuild-/TypeLib-Orchestrierung bleiben offen.

## Standardbibliothek: vollständiger Financial-Slice

`IPmt`, `PPmt`, `NPer`, `Rate` und `MIRR` ergänzen die bereits vorhandenen Finanzfunktionen.
Zahlungsanteile, Nullzins, End-/Anfangsperioden, iterative Zinsrückrechnung und getrennte
Finanzierungs-/Wiederanlageraten sind in Runtime, Symboltabelle, IR und Managed-Emitter
verdrahtet. Direkte Runtime- und generierte Programmtests decken damit alle dokumentierten
VB6-Finanzfunktionen ab; Meilenstein 7 führt diesen Teilvertrag nun als abgeschlossen.

## Standardbibliothek: vollständiger `Format$`-Vertrag

`Format`/`Format$` deckt jetzt die dokumentierten benannten Zahlen-, Boolean-, Datums- und
Zeitformate, bis zu vier numerische Abschnitte einschließlich `Null`, alle String-Platzhalter mit
Literalen/Escapes sowie die vollständige Datums-/Zeit-Tokenoberfläche ab. Dazu gehören insbesondere
`c`, `ddddd`, `dddddd`, `ttttt`, `AMPM`, zweistellige Jahre und systemabhängige Datums-/
Zeittrennzeichen. Das Profil `VB6Sp6` verwendet zusätzlich die aktive Währung und die regionalen
Kurz-/Langmuster; der deterministische Vertrag bleibt invariant. Runtime- und Managed-E2E-Tests
sichern die neuen Fälle, und Meilenstein 7 führt `Format$` nun als abgeschlossen.

## Standardbibliothek: vollständiger Math-Slice

Die Managed-Math-Oberfläche umfasst jetzt `Abs`, `Sgn`, `Fix`, `Int`, `Round`, `Sqr`, `Exp`,
`Log`, `Sin`, `Cos`, `Tan`, `Atn`, `Rnd` und `Randomize`. `Int`, `Fix` und `Abs` erhalten die
VB6-Variant-Untertypen einschließlich `Currency` und `Date`; insbesondere kürzt `Fix` negative
Currency-Werte gegen null, während `Int` gegen minus unendlich abrundet. `Round` akzeptiert nun
auch Currency über die zentrale Decimal-Konvertierung. Runtime-Regressionen sichern zusätzlich
Banker's Rounding sowie Definitionsbereichs- und Überlauffehler; ein Managed-E2E-Test prüft die
Untertypen im generierten Programm. Meilenstein 7 führt den Math-Slice damit als abgeschlossen.

## Standardbibliothek: profilbewusste Date-/Time-Grenzen

`DateValue` und `TimeValue` akzeptieren im `VB6Sp6`-Profil die aktive System-Locale für
Textwerte; der bestehende parameterlose Vertrag bleibt invariant. `WeekdayName` und `MonthName`
reichen das Profil ebenfalls bis zur Runtime durch. `DateAdd` rundet nicht-ganzzahlige Angaben
jetzt über den gemeinsamen VB-`Long`-Konversionsvertrag (einschließlich Banker's Rounding),
und `DatePart(..., vbUseSystem, vbUseSystem)` verwendet die Kalenderwochenregel der aktiven
Kultur. `IsDate` und `IsNumeric` verwenden für Textwerte ebenfalls den ausgewählten Vertrag.
Runtime- und Managed-End-to-End-Regressionen sowie der Matrixeintrag
`profile.datetime-locale-and-rounding` sichern den dokumentationsbasierten Vertrag.

## Kanonischer Release-Nachweis (27.08.2026)

Der serielle `build.ps1`-Lauf ist mit **1195/1195** Tests, 0 Warnungen/Fehlern im Release-Build
und **40/40** fehlerfrei analysierten VISIA-Projekt-Items grün. LLVM-, LSP- und IDE-Flächen bleiben
wie beschlossen außerhalb des Ausbauumfangs.

## Forms-Grundvertrag: deterministische `ScaleX`-/`ScaleY`-Einheiten

Die headless Runtime rechnet `ScaleX` und `ScaleY` fuer alle festen VB6-Modi (Twips, Points,
Pixels, Characters, Inch, Millimeter und Zentimeter) ueber Einheiten pro Zoll um. Der
`vbUser`-Fallback verwendet ohne control-spezifische `ScaleWidth`/`ScaleHeight` Twips; ungueltige
Modi werden mit einem Argumentfehler abgewiesen. Der headless Pixelpfad nutzt 96 DPI; der
WinForms-Host behaelt seine Device-DPI-Umrechnung fuer Control-Koordinaten. Runtime- und
emittierte Form-E2E-Regressionen decken beide Achsen und ungueltige Modi ab.

`DrawMode` fuehrt auf persistenten `AutoRedraw`-Flaechen jetzt alle 16 GDI-ROP2-Wahrheitstabellen
aus. `GraphicsLine` und `PaintPicture` verwenden dafuer einen gemeinsamen Quell-/Ziel-Rastermerge;
ungueltige Werte werden mit VB6-Fehler 380 abgewiesen. Direkte sichtbare und aktive Paint-Kontexte
bleiben als naechster Forms-Ausbau offen.

`StrConv` verarbeitet nun kombinierte Casing-, Breiten- und japanische Kana-Flags. `vbWide` und
`vbNarrow` mappen den ASCII-/Leerzeichenbereich in die jeweilige Voll-/Halbbreite, während
`vbKatakana` und `vbHiragana` die Unicode-Kana-Bereiche umsetzen; nicht anwendbare Locale werden
explizit abgewiesen. Ein gesetzter LCID überschreibt im `VB6Sp6`-Profil die Prozesskultur, während
das deterministische Profil invariant bleibt.

## Kanonischer Release-Nachweis (28.08.2026)

Der serielle `build.ps1 -NoRestore -Configuration Release`-Lauf ist mit **1247/1247** Tests,
0 Warnungen/Fehlern im Release-Build und **40/40** fehlerfrei analysierten VISIA-Projekt-Items
grün. Der Lauf enthält die neuen Date-/Time- und Locale-Regressionen; LLVM-, LSP- und IDE-Flächen
bleiben wie beschlossen außerhalb des Ausbauumfangs.

`Erase` auf `ByRef`-Arrayparametern schreibt die Deallokation jetzt in den Caller zurück. Ein
anschließendes `ReDim` sieht den freigegebenen Zustand und erzeugt wieder einen gültigen Descriptor;
Semantik- und Managed-End-to-End-Tests sichern den Vertrag.

Skalare Vergleiche werden im Managed-Emitter nicht mehr über `VBOperators.Equal(object?, object?)`
ausgeführt. Gemeinsame Integer-, Floating-Point-, Currency-, Date-, Boolean-, String- und
LongPtr-Typen verwenden typisierte Vergleichshelfer; Variant-/Objektvergleiche behalten ihren
kompatiblen Runtime-Pfad. Ein Emit-Metadaten-Test und ein generiertes E2E-Programm sichern beide
Pfade.

`Option Compare Text` gilt nun auch für skalare Stringrelationen (`=`, `<>`, `<`, `<=`, `>` und `>=`)
und für `Select Case`. Binder und IR übertragen den Modulmodus bis zum typisierten Managed-
Vergleich; `Option Compare Binary` bleibt ordinal und case-sensitiv.

`StrComp` ist nun als vollständiges String-Intrinsic verdrahtet. Die drei Argumentformen liefern
die normalisierten VB6-Ergebnisse `-1`, `0` oder `1`; `vbBinaryCompare` verwendet ordinalen
Vergleich, `vbTextCompare` ordinalen Vergleich ohne Beachtung der Groß-/Kleinschreibung. Symbol,
IR, Managed-Emitter und Runtime sind durch direkte und generierte Regressionen abgedeckt. Wird der
optionale Vergleichsmodus bei `InStr`, `InStrRev`, `Replace`, `Split`, `Filter` oder `StrComp`
weggelassen, übernimmt der Binder nun `Option Compare Text` beziehungsweise `Option Compare Binary`.

`RSet target = source` ist nun als kontextuelle Zuweisungssyntax verdrahtet. Feste String-Ziele
werden im Managed-Backend rechtsbündig mit VB6-konformem Links-Padding und Behalten der linken
Zeichen beim Kürzen geschrieben; variable String-Ziele behalten normale Zuweisungssemantik.
Parser-, Runtime- und generierte Managed-Regressionen decken die Kurz- und Langquellen sowie den
expliziten Guard für nicht abbildbare Layouts ab.

Die kontextuelle `Mid(target, start[, length]) = replacement`- beziehungsweise `Mid$`-Syntax ist
nun ebenfalls umgesetzt. Der Managed-String-Helper schreibt 1-basiert in place, begrenzt die
Ersetzung an Ersatztext- und Ziellänge und erhält die Breite fester Strings. Parser-, Runtime-
und Managed-E2E-Tests sichern die dokumentierten Beispiele einschließlich der gekürzten langen
Ersatztexte.

Die byteorientierten String-Intrinsics `LeftB`, `RightB`, `MidB` und `InStrB` sind ebenfalls
verdrahtet. Längen und Suchpositionen zählen nun die Bytes der ausgewählten Stringkodierung;
`VB6Sp6` verwendet die aktive ANSI-Codepage, während das deterministische Profil seine stabile
UTF-16-Darstellung beibehält. Kürzung, 1-basierte Positionen sowie binäre und textuelle Suche
sind durch Runtime- und generierte Managed-Regressionen abgedeckt.

Das MSBuild-SDK räumt bei `Clean` nun die über das Output-Manifest bekannten Einzelprojekt- und
Projektgruppenartefakte inklusive Compile-Stempeln und Input-/Output-Manifesten auf. Der Standard-
`Rebuild`-Pfad erzeugt die konfigurierten VB6-Ausgaben danach wieder; nicht deklarierte Dateien
bleiben unangetastet.

## Mode-aware `Loc`-Semantik für Datei-I/O

`Loc` ist jetzt als Compiler-Intrinsic und Managed-Runtime-Aufruf verfügbar. Binary-Dateien liefern
die aktuelle Byteposition, Random-Dateien die feste Datensatznummer und Sequential-Dateien den
aktuellen 128-Byte-Block. Runtime- und Compiler-E2E-Tests sichern alle drei Einheiten; UDT-/Array-/
Variant-Layouts bleiben als nächste Datei-I/O-Schritte offen.

`Reset` ist zusätzlich als Datei-I/O-Intrinsic verdrahtet und schließt alle offenen Kanäle; Runtime-
und Compiler-Regressionen prüfen, dass der nächste `FreeFile`-Kanal wieder bei 1 beginnt.

`Write #` serialisiert mehrere Werte jetzt in der VB6-Maschinendarstellung: Strings werden zitiert
(eingebettete Anführungszeichen verdoppelt), Boolean-Werte als `#TRUE#`/`#FALSE#` und `Null` als
`#NULL#`, mit Kommatrennung und CRLF-Abschluss. Komplexe Record-Layouts bleiben bewusst der
nächsten Datei-I/O-Etappe vorbehalten.

`Input #` rekonstruiert für Variant-Ziele die von `Write #` erzeugten Empty-, Null-, Boolean-,
Date- und Error-Marker sowie skalare Zahlen. Binäre Variant-Werte tragen ihr VB6-Typ-Tag und
Payload; eigenständige unterstützte skalare Arrays einschließlich variabler String-Elemente werden
in Binary elementweise und dynamische
Top-Level-Arrays in Random mit Descriptor und Ziel-Write-back übertragen. Variant-Arrays als
Variant-Wert, Objekte und komplexere zusammengesetzte Layouts bleiben offen.

`Lock`/`Unlock` sind als Datei-I/O-Statements durchgängig verdrahtet. Binary-Bytebereiche und
Random-Datensatzbereiche werden 1-basiert auf native Dateisperren abgebildet; bei Sequential wird
unabhängig vom Bereich die gesamte Datei gesperrt bzw. freigegeben. Die Syntax-, Runtime- und
Compiler-Regressionen sichern Einzelbereiche, Ganzdateisperren und das Random-Record-Mapping.

Die optionale `Access`-Klausel von `Open` (`Read`, `Write`, `Read Write`) wird nun separat geparst,
gebunden und bis zur Runtime als .NET-`FileAccess` weitergereicht. Die verwalteten Dateikanäle
verweigern damit nicht erlaubte Lese-/Schreiboperationen bereits an der Stream-Grenze; Parser-,
Runtime- und Managed-E2E-Regressionen decken alle drei Rechte sowie ungültige Werte ab.

`Open` akzeptiert nun auch die dokumentierte Kurzform ohne `For`-Klausel. Sie wird als `Random`
mit der Standard-Recordlänge 128 gebunden; Parser- und Managed-E2E-Tests sichern die Default-
Semantik über `Put` und `Loc`.

`Print #` akzeptiert außerdem eine leere Outputliste (`Print #n,`) und schreibt dafür eine reine
CRLF-Zeile. Der Fall ist im Parser und über einen vollständigen Managed-Datei-I/O-Test abgesichert.

`Print #`-Outputlisten unterstützen jetzt mehrere Ausdrücke mit den dokumentierten Semikolon- und
Kommatrennern. Semikolon verkettet Werte direkt, Komma wechselt in die nächste Ausgabezone, und ein
abschließendes Semikolon lässt die Zeile für den nächsten `Print #`-Aufruf offen. Parser- und
Managed-E2E-Regressionen sichern die Separatoren sowie die Fortsetzungssemantik ab.

`Width #` ist als Datei-I/O-Statement durchgängig implementiert. Die Runtime hält pro Kanal die
aktuelle Zeilenbreite und beginnt bei fortgesetzten `Print #`-Werten vor dem nächsten Wert eine neue
CRLF-Zeile, sobald 1 bis 255 Zeichen erreicht sind; Breite 0 bleibt unbegrenzt. Parser-, Runtime-
und Managed-E2E-Regressionen decken Default, Grenzwerte und das automatische Wrapping ab.

`Input #` stellt für Variant-Ziele jetzt die maschinenlesbaren `Write #`-Marker für Empty, Null,
Boolean, Date und Error sowie skalare Zahlen wieder her. Binäre `Get`-/`Put`-Transfers tragen für
skalare Variant-Felder das VB6-Typ-Tag und die zugehörige Payload; eigenständige unterstützte
skalare Arrays einschließlich variabler String-Elemente werden elementweise übertragen, und dynamische Top-Level-Arrays tragen in Random
ihren Descriptor mit Write-back der gelesenen Form. Runtime-, Compiler- und UDT-Regressionen
sichern die Subtypen und die Wertkopie. Variant-Arrays als Variant-Wert und Objektvarianten bleiben
für eine spätere SAFEARRAY-/COM-Etappe abgegrenzt.

Die Sharing-Klauseln von `Open` (`Shared`, `Lock Read`, `Lock Write`, `Lock Read Write`) werden
jetzt profilunabhängig auf explizite .NET-`FileShare`-Regeln abgebildet; ungültige Modi werden vor
dem Öffnen mit einem Argumentfehler abgewiesen.

## Kanonischer Release-Nachweis (29.08.2026)

Der serielle `build.ps1 -NoRestore -Configuration Release`-Lauf ist mit **1250/1250** Tests,
0 Warnungen/Fehlern im Release-Build und **40/40** fehlerfrei analysierten VISIA-Projekt-Items
grün. Der Lauf bestätigt den Byte-String-Slice (`LeftB`, `RightB`, `MidB`, `InStrB`) und den
Luna-Einstiegsgate. Die Matrix enthält nun 49 atomare Erwartungen (35 `implemented`, 14
`planned` beziehungsweise `partial`); die nächste offene Implementierungskarte ist
`L1-02-A` für Grammatik-/Kontextregeln. `build.ps1` prüft Pflichtfelder, eindeutige IDs,
Referenzen und Testpfade.

## Modul-Sichtbarkeit (29.08.2026)

`Public` und `Global` deklarierte Modulvariablen werden jetzt projektweit in andere Standardmodule
importiert. `Private` und `Dim` bleiben auf das deklarierende Modul begrenzt; ein Fremdzugriff unter
`Option Explicit` liefert `VB6S0001`. `ModuleVariableSymbol.IsPublic` hält die Sichtbarkeit bis in
die Bindung fest. Zwei Projektanalyse-Regressionen sichern den gültigen Cross-Module-Fall und den
abgewiesenen Private-Fall.

## Matrix-Wahrheit und Queue-Abgleich (29.08.2026)

Die Queue-Karten Q-01 und Q-02 trennen und sichern die beiden Matrixachsen mechanisch: `planned`
bleibt `not-yet-verified`, und `oracle-verified` ist ohne einen implementierten Nachweis verboten.
L1-05R materialisiert die 34 fehlenden Erwartungen aus L1-03 und L1-04; sie bleiben bewusst geplant.
Q-03 stuft die überklagten Vollständigkeitsflächen für `Format` und `Math` auf `partial` zurück,
Q-04 gibt den Matrixstand im kanonischen Lauf aus, und Q-05 ignoriert `build_diag.txt`, entfernt
zwei tote Diagnoseassertions und löscht die nicht mehr verwendeten C#-Backend-Verzeichnisse.

Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf ist mit **1252/1252** Tests,
0 Warnungen/Fehlern im Release-Build und **40/40** fehlerfrei analysierten VISIA-Projekt-Items grün.
Die TRX-Dateien des Laufs messen aktuell **33 implemented**, **3 partial**, **47 planned** und
**36/83 documented-verified**; die nächste offene Implementierungskarte bleibt `L1-02-A`.

## L1-02-A Parser-Kontext (29.08.2026)

Der Parser akzeptiert nun auch module-level `Dim WithEvents`-Deklarationen und bewahrt den
`WithEvents`-Marker im bestehenden Syntaxknoten. Der gezielte Parserlauf besteht mit 6/6 Tests;
der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1253/1253** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die neue atomare Erwartung `l1-02-a-dim-withevents-declaration` ist als `implemented`/
`documented-verified` materialisiert. Die Matrix steht damit bei **34 implemented**, **3 partial**,
**47 planned** und **37/84 documented-verified**; die breite Erwartung für `L1-02-A` bleibt wegen
der offenen Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Option Private Module (29.08.2026)

`Option Private Module` wird auf Modulebene als eigene Syntaxdirektive erkannt; innerhalb einer
Prozedur bleibt die Direktive ein Parserfehler. Der gezielte Option-Lauf besteht mit 5/5 Tests;
der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1255/1255** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die atomare Erwartung `l1-02-a-option-private-module-syntax` ist
`implemented`/`documented-verified`; die Matrix steht bei **35 implemented**, **3 partial**,
**47 planned** und **38/85 documented-verified**. Die breite Familienerwartung `L1-02-A` bleibt
wegen der noch offenen Grammatik-/Kontextregeln `partial`.

## L1-02-A Static-Prozeduren (29.08.2026)

Der Parser akzeptiert nun `Static Sub` sowie Sichtbarkeitsmodifizierer vor `Static Function` und
bewahrt die Modifier im Syntaxbaum. Die gezielte Parserkarte besteht mit 8/8 Tests; der
kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1257/1257** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die atomare Erwartung `l1-02-a-static-procedure-syntax` ist
`implemented`/`documented-verified`; die Matrix steht bei **36 implemented**, **3 partial**,
**47 planned** und **39/86 documented-verified**. Die Laufzeit-Persistenz der lokalen Variablen
in statischen Prozeduren bleibt eine separate Semantik-Karte; `L1-02-A` bleibt insgesamt `partial`.

## L1-02-A DefType-Direktiven (29.08.2026)

Der Parser akzeptiert nun die module-level-DefType-Direktiven (`DefInt`, `DefStr` und die übrigen
VB6-Standardnamen) mit einzelnen, gebundenen und kommaseparierten Buchstabenbereichen. Innerhalb
einer Prozedur werden diese Direktiven als ungültiger Kontext diagnostiziert; fehlerhafte Bereiche
wie `A-1` bleiben ebenfalls sichtbar. Der gezielte Option-/DefType-Lauf besteht mit **8/8** Tests;
der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1260/1260** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die atomare Erwartung `l1-02-a-deftype-directive-syntax` ist
`implemented`/`documented-verified`; die Matrix steht bei **37 implemented**, **3 partial**,
**47 planned** und **40/87 documented-verified**. Die Anwendung der Defaulttypen auf Binder und
Semantik bleibt eine separate Karte; `L1-02-A` bleibt insgesamt `partial`.

## L1-02-A DefType-Defaulttypen (29.08.2026)

Die module-level-`DefType`-Bereiche werden jetzt im Managed-Lowerer aufgelöst: untypisierte
Modul-/Lokaldeklarationen, Parameter sowie Function- und Property-Get-Rückgaben erhalten den
Defaulttyp des ersten Buchstabens. Explizite `As`-Typen und Bezeichner-Typsuffixe überschreiben
den Default weiterhin. Der gezielte Binder-/Lowerer-Lauf besteht mit **5/5** Tests; der
kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1262/1262** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die atomare Erwartung `l1-02-a-deftype-default-semantics` ist
`implemented`/`documented-verified`; die Matrix steht bei **38 implemented**, **3 partial**,
**47 planned** und **41/88 documented-verified**. Implizite Variablen, die erst durch eine
Zuweisung entstehen, sowie überlappende DefType-Bereiche bleiben für eine separate Karte sichtbar;
`L1-02-A` bleibt insgesamt `partial`.

## L1-02-A DefType-implizite Variablen (29.08.2026)

Der Binder verwendet die module-level-DefType-Tabelle nun auch für Variablen, die ohne Deklaration
bei einer Zuweisung oder in einem Ausdruck entstehen. `Option Explicit` bleibt unverändert; ein
Bezeichner-Typsuffix überschreibt den Default. Der gezielte Binderlauf besteht mit **6/6** Tests;
der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1263/1263** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die atomare Erwartung `l1-02-a-deftype-implicit-variables` ist
`implemented`/`documented-verified`; die Matrix steht bei **39 implemented**, **3 partial**,
**47 planned** und **42/89 documented-verified**. Die Validierung überlappender DefType-Bereiche
bleibt als separate Karte offen; `L1-02-A` bleibt insgesamt `partial`.

## L1-02-A DefType-Bereichskonflikte (29.08.2026)

Überlappende module-level-`DefType`-Buchstabenbereiche werden im Binder jetzt mit dem
deterministischen Semantikdiagnosecode `VB6S0070` abgewiesen; direkt angrenzende, nicht
überlappende Bereiche bleiben gültig und behalten ihre jeweiligen Defaulttypen. Der gezielte
Binderlauf besteht mit **8/8** Tests; der kanonische `build.ps1 -NoRestore -Configuration Release`-
Lauf misst **1265/1265** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei
analysierte VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-deftype-range-conflicts` ist
`implemented`/`documented-verified`; die Matrix steht bei **40 implemented**, **3 partial**,
**47 planned** und **43/90 documented-verified**. Die breite Erwartung `L1-02-A` bleibt wegen
offener Grammatik-/Kontextregeln `partial`.

## L1-02-A Statische Prozedursemantik (29.08.2026)

`Dim`-Variablen in `Static Sub`- und `Static Function`-Prozeduren werden im Binder als
persistent gespeicherte Modul-Slots geführt. Ihre Werte bleiben über Aufrufe erhalten, während
gewöhnliche Prozeduren weiterhin pro Aufruf lokale Speicherplätze verwenden; Array-Deklarationen
behalten dabei ihren statischen Speichervertrag. Der gezielte Binder-/Managed-Lauf besteht mit
**11/11** Tests; der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1268/1268** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-static-procedure-semantics` ist
`implemented`/`documented-verified`; die Matrix steht bei **41 implemented**, **3 partial**,
**47 planned** und **44/91 documented-verified**. Die breite Erwartung `L1-02-A` bleibt wegen
offener Grammatik-/Kontextregeln `partial`.

## L1-02-A Prozedur-Sichtbarkeit (29.08.2026)

`Public`- und `Global`-Prozeduren werden in Standardmodulen projektweit aufgelöst, während
`Private`-Prozeduren nur im deklarierenden Modul sichtbar bleiben. Der Symbolvertrag führt diese
Entscheidung als `ProcedureSymbol.IsPublic`; die gemeinsame Symbolinstanz für öffentliche
Standardmodul-Prozeduren bleibt dabei erhalten. Die gezielten Sichtbarkeits-/Metadatenläufe
bestehen mit **1/1** und **1/1** Tests; der kanonische `build.ps1 -NoRestore -Configuration Release`-
Lauf misst **1270/1270** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei
analysierte VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-procedure-visibility` ist
`implemented`/`documented-verified`; die Matrix steht bei **42 implemented**, **3 partial**,
**47 planned** und **45/92 documented-verified**. Die breite Erwartung `L1-02-A` bleibt wegen
offener Grammatik-/Kontextregeln `partial`.

## L1-02-A Option-Private-Module-Semantik (29.08.2026)

`Option Private Module` wird jetzt im `SemanticModel` als externe Exportpolitik markiert; öffentliche
Mitglieder bleiben innerhalb desselben Projekts für Schwester-Module sichtbar. Ein externer
Standardmodul-Importpfad wird bewusst nicht behauptet. Der gezielte Binder-/Projektlauf besteht
mit **1/1** und **1/1** Tests; der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf
misst **1272/1272** Tests, 0 Warnungen/Fehlern im Release-Build und **40/40** fehlerfrei
analysierte VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-option-private-module-semantics`
ist `implemented`/`documented-verified`; die Matrix steht bei **43 implemented**, **3 partial**,
**47 planned** und **46/93 documented-verified**. Die breite Erwartung `L1-02-A` bleibt wegen
offener Grammatik-/Kontextregeln `partial`.

## Q-09 Dokumentationsabgleich (29.08.2026)

Der Qualitätsdurchgang hält die Matrixzerlegung und ihre Messehrlichkeit jetzt auch in den
Arbeitsdokumenten fest: Q-01 setzt die `verification`-Achse für geplante Erwartungen auf
`not-yet-verified`, Q-02 erzwingt diese Zuordnung sowie das Oracle-Verbot im `build.ps1`, und
L1-05R materialisiert die 34 fehlenden Erwartungen aus L1-03/L1-04. Q-03 stuft die überklagten
Vollständigkeitsflächen für `Format` und `Math` auf `partial` zurück; Q-04 gibt den Matrixstand im
kanonischen Lauf aus; Q-05 bereinigt die belegten Altlasten. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1272/1272** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**93 Erwartungen**, davon **43 implemented**, **3 partial**, **47 planned** und **46/93
documented-verified**. Q-06 bis Q-08 sind in Roadmap, README und Ausführungsplan gespiegelt;
Q-09 aktualisiert die dauerhaften Projektregeln, ohne die Changelog-Historie umzuschreiben.

## L1-02-A Global-Modulvariablen (30.08.2026)

Der projektweite Binder-Nachweis für `Global`-Modulvariablen ist ergänzt: Unter `Option Explicit`
wird die Variable aus einem anderen Standardmodul aufgelöst und als öffentliche
`ModuleVariableSymbol`-Instanz erkannt. Der gezielte Projektlauf besteht mit **1/1** Test. Der
kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1273/1273** Tests, 0
Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
Erwartung `l1-02-a-global-module-variable-resolution` ist `implemented`/`documented-verified`;
die Matrix umfasst **94 Erwartungen**, davon **44 implemented**, **3 partial**, **47 planned** und
**47/94 documented-verified**.

## L1-02-B Benannte Argumente und Auswertungsreihenfolge (30.08.2026)

Der Managed-E2E-Nachweis für benannte Argumente mit Seiteneffekten ist ergänzt: Bei
`second:=NextValue(), first:=NextValue()` werden die Ausdrücke genau einmal in deklarierter
Parameterreihenfolge ausgewertet und vom Callee als `1:2` beobachtet. Der gezielte Lauf besteht mit
**1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1274/1274** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die Erwartung `l1-02-b-named-arguments-side-effect-order` ist
`implemented`/`documented-verified`; die Matrix umfasst **95 Erwartungen**, davon **45 implemented**,
**3 partial**, **47 planned** und **48/95 documented-verified**.

## L1-02-B Fehlformen benannter Argumente (30.08.2026)

Die deterministischen Fehlformen der benannten Argumentübergabe sind regressionsgesichert:
Doppelte Parameternamen und Positionsargumente nach einem `name:=value`-Argument melden jeweils
`VB6S0069`, ohne eine vorhandene Parameterbindung zu überschreiben. Der gezielte Semantiklauf
besteht mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1275/1275** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die Erwartung `l1-02-b-named-arguments-invalid-shapes` ist
`implemented`/`documented-verified`; die Matrix umfasst **96 Erwartungen**, davon **46 implemented**,
**3 partial**, **47 planned** und **49/96 documented-verified**.

## L1-02-B Vollständige Named-Argument-Familie (30.08.2026)

Die breite Erwartung `l1-02-b-named-arguments-evaluation-order` ist geschlossen. Parser-,
Optional-, Compiler- und Semantiktests belegen die Zuordnung per `name:=value`, optionale Defaults,
die deklarierte Auswertungsreihenfolge sowie deterministische `VB6S0069`-Diagnosen für unbekannte,
doppelte und falsch angeordnete Argumente. Der gezielte Nachweis besteht mit **27/27** Tests. Der
kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1275/1275** Tests, 0
Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
Matrix umfasst **96 Erwartungen**, davon **47 implemented**, **3 partial**, **46 planned** und
**50/96 documented-verified**.

## L1-02-C Verschachtelte UDT-Arrayfelder (30.08.2026)

Verschachtelte UDT-Arrayfelder mit expliziten, nicht bei null beginnenden Grenzen bewahren Rang,
Unter- und Obergrenzen sowie ihren Elementtyp. Uninitialisierte Elemente liefern die VB6-
Skalaranfangswerte; ein Feld eines verschachtelten Arrayelements kann an eine `ByRef`-Prozedur
übergeben werden, deren Änderung in den Aufrufer zurückgeschrieben wird. Der gezielte Compiler-
lauf besteht mit **1/1** Test; der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf
misst **1276/1276** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei
analysierte VISIA-Projekt-Items. Die atomare Erwartung `l1-02-c-nested-udt-array-storage` ist
`implemented`/`documented-verified`; die Matrix steht bei **48 implemented**, **3 partial**,
**46 planned** und **51/97 documented-verified**. Die breite Erwartung `L1-02-C` bleibt für
weitere Array-/UDT-Regeln offen.

## L1-02-C Array-Parameterdiagnosen (30.08.2026)

Der Binder weist `ByVal`-Arrayparameter mit `VB6S0028` zurück und meldet `VB6S0032`, wenn ein
Arrayparameter feste Rang- oder Bounds-Angaben trägt. Der gezielte Semantiklauf besteht mit **1/1**
Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1277/1277** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
atomare Erwartung `l1-02-c-array-parameter-diagnostics` ist `implemented`/`documented-verified`;
die Matrix steht bei **54 implemented**, **3 partial**, **46 planned** und **57/103
documented-verified**. Die breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

## L1-02-C Mehrdimensionale ReDim-Preserve-Struktur (30.08.2026)

Eine dynamische mehrdimensionale Managed-Arraystruktur bewahrt bei `ReDim Preserve` Rang,
frühere Grenzen und die Untergrenze der letzten Dimension. Bestehende Werte bleiben an ihren
mehrdimensionalen Indizes erhalten; neu hinzugekommene Slots liefern die VB6-Skalardefaults.
Der gezielte Compilerlauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1277/1277** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-c-redim-preserve-multidimensional` ist `implemented`/`documented-verified`; die Matrix
steht bei **49 implemented**, **3 partial**, **46 planned** und **52/98 documented-verified**.
Die breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

## L1-02-C UDT-Array-Rangdiagnose (30.08.2026)

Der Binder weist einen Zugriff auf ein festes UDT-Arrayfeld mit zu wenigen Indizes deterministisch
mit `VB6S0027` zurück. Der gezielte Semantiklauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1277/1277** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-c-udt-array-rank-diagnostics` ist `implemented`/`documented-verified`; die Matrix steht
bei **50 implemented**, **3 partial**, **46 planned** und **53/99 documented-verified**. Die
breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

## L1-02-C For-Each-UDT-Arraydiagnose (30.08.2026)

Der Analyzer weist `For Each` über ein Array eines Standardmodul-UDT deterministisch mit
`VB6S0056` zurück. Damit wird der dokumentierte VB6-Vertrag abgebildet, nach dem ein solches UDT
nicht implizit in die erforderliche Variant-Steuervariable coerct wird. Der gezielte Compilerlauf
besteht mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1277/1277** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die atomare Erwartung `l1-02-c-foreach-udt-array-diagnostic` ist
`implemented`/`documented-verified`; die Matrix steht bei **53 implemented**, **3 partial**,
**46 planned** und **56/102 documented-verified**. Die breite Erwartung `L1-02-C` bleibt für
weitere Array-/UDT-Regeln offen.

## L1-02-C ReDim-Elementtypdiagnose (30.08.2026)

Der Binder weist ein `ReDim` mit einem gegenüber der dynamischen Arraydeklaration abweichenden
Elementtyp deterministisch mit `VB6S0031` zurück. Der gezielte Semantiklauf besteht mit **1/1**
Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1277/1277** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
atomare Erwartung `l1-02-c-redim-element-type-diagnostic` ist `implemented`/`documented-verified`;
die Matrix steht bei **51 implemented**, **3 partial**, **46 planned** und **54/100
documented-verified**. Die breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

## L1-02-C ReDim auf ParamArray (30.08.2026)

Der Binder weist ein `ReDim` auf einem `ParamArray` deterministisch mit `VB6S0066` zurück. Der
gezielte Semantiklauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1277/1277** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-c-redim-paramarray-diagnostic` ist `implemented`/`documented-verified`; die Matrix steht
bei **52 implemented**, **3 partial**, **46 planned** und **55/101 documented-verified**. Die
breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

## L1-02-C Dynamische UDT-Arrayfelder (30.08.2026)

Ein dynamisches UDT-Arrayfeld wird über seinen Empfänger mit `ReDim` angelegt, behält die
expliziten Unter- und Obergrenzen und bewahrt den deklarierten Elementtyp sowie beschreibbare
verschachtelte Felder. Der gezielte Compilerlauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1277/1277** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-c-dynamic-udt-array-member` ist `implemented`/`documented-verified`; die Matrix steht
bei **55 implemented**, **3 partial**, **46 planned** und **58/104 documented-verified**. Die
breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

## L1-02-A Modul-Deklarationskontext (30.08.2026)

`Public`, `Private` und `Global`-Variablendeklarationen innerhalb einer Prozedur oder eines
verschachtelten Statement-Blocks werden als ungültige Moduldeklarationen mit `VB6P0001`
diagnostiziert und zeilenweise übersprungen. Eine
lokale `Dim`-Deklaration bleibt dabei eine gültige `DimStatementSyntax`; nachfolgende Statements
werden weiter geparst. Der gezielte Parserlauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1278/1278** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-a-module-declaration-context-guard` ist `implemented`/`documented-verified`; die Matrix
steht bei **56 implemented**, **3 partial**, **46 planned** und **59/105 documented-verified**.
Die breite Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Const-Deklarationskontext (30.08.2026)

`Public`, `Private` und `Global Const`-Deklarationen innerhalb einer Prozedur oder eines
verschachtelten Statement-Blocks werden mit `VB6P0001` diagnostiziert und zeilenweise
übersprungen; eine lokale `Const`-Deklaration bleibt gültig. Der gezielte Parserlauf besteht
mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1279/1279** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-constant-declaration-context-guard` ist
`implemented`/`documented-verified`; die Matrix steht bei **57 implemented**, **3 partial**,
**46 planned** und **60/106 documented-verified**. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Enum-/Type-Deklarationskontext (30.08.2026)

`Public`, `Private` und `Global`-Sichtbarkeitspräfixe vor `Enum`-/`Type`-Deklarationen innerhalb
einer Prozedur oder eines verschachtelten Statement-Blocks werden mit `VB6P0001` diagnostiziert
und zeilenweise übersprungen; module-level-Sichtbarkeitsdeklarationen bleiben gültig. Der gezielte
Parserlauf besteht mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-
Lauf misst **1281/1281** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei
analysierte VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-enum-type-declaration-context-guard`
ist `implemented`/`documented-verified`; die Matrix steht bei **59 implemented**, **3 partial**,
**46 planned** und **62/108 documented-verified**. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Prozedurdeklarationskontext (30.08.2026)

`Public`, `Private` und `Global`-Sub-/Function-Deklarationen innerhalb einer Prozedur oder
eines verschachtelten Statement-Blocks werden mit `VB6P0001` diagnostiziert und zeilenweise
übersprungen; eine module-level-Sichtbarkeitsdeklaration bleibt gültig. Der gezielte Parserlauf
besteht mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1280/1280** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-procedure-declaration-context-guard` ist
`implemented`/`documented-verified`; die Matrix steht bei **58 implemented**, **3 partial**,
**46 planned** und **61/107 documented-verified**. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Declare-Deklarationenkontext (30.08.2026)

`Public`, `Private` und `Global`-Sichtbarkeitspräfixe vor `Declare`-Deklarationen innerhalb einer
Prozedur oder eines verschachtelten Statement-Blocks werden mit `VB6P0001` diagnostiziert und
zeilenweise übersprungen; module-level-Sichtbarkeitsdeklarationen bleiben gültig. Der gezielte
Parserlauf besteht mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-
Lauf misst **1282/1282** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei
analysierte VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-declare-declaration-context-guard`
ist `implemented`/`documented-verified`; die Matrix steht bei **60 implemented**, **3 partial**,
**46 planned** und **63/109 documented-verified**. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Property-/Event-Deklarationskontext (30.08.2026)

`Public`, `Private` und `Global`-Sichtbarkeitspräfixe vor `Property`-/`Event`-Deklarationen
innerhalb einer Prozedur oder eines verschachtelten Statement-Blocks werden mit `VB6P0001`
diagnostiziert und zeilenweise übersprungen; module-level-Sichtbarkeitsdeklarationen bleiben
gültig. Der gezielte Parserlauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1283/1283** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-a-property-event-declaration-context-guard` ist `implemented`/`documented-verified`; die
Matrix steht bei **61 implemented**, **3 partial**, **46 planned** und **64/110
documented-verified**. Die breite Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln
auf `partial`.

## L1-02-A WithEvents-Deklarationskontext (30.08.2026)

`Dim`, `Public`, `Private` und `Global`-Sichtbarkeitspräfixe vor `WithEvents`-Deklarationen
innerhalb einer Prozedur oder eines verschachtelten Statement-Blocks werden mit `VB6P0001`
diagnostiziert und zeilenweise übersprungen; module-level-Sichtbarkeitsdeklarationen bleiben
gültig. Der gezielte Parserlauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1284/1284** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-a-withevents-declaration-context-guard` ist `implemented`/`documented-verified`; die
Matrix steht bei **62 implemented**, **3 partial**, **46 planned** und **65/111
documented-verified**. Die breite Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln
auf `partial`.

## L1-02-A Implements-Deklarationskontext (30.08.2026)

`Implements`-Deklarationen innerhalb einer Prozedur oder eines verschachtelten Statement-Blocks
werden mit `VB6P0001` diagnostiziert und zeilenweise übersprungen; eine module-level-
`Implements`-Deklaration bleibt gültig. Der gezielte Parserlauf besteht mit **1/1** Test. Der
kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1285/1285** Tests,
0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items.
Die atomare Erwartung `l1-02-a-implements-declaration-context-guard` ist
`implemented`/`documented-verified`; die Matrix steht bei **63 implemented**, **3 partial**,
**46 planned** und **66/112 documented-verified**. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Option-Direktivenkontext (30.08.2026)

`Option Explicit`, `Option Base`, `Option Compare` und `Option Private Module` werden innerhalb
einer Prozedur oder eines verschachtelten Statement-Blocks mit `VB6P0001` diagnostiziert und
zeilenweise übersprungen; module-level-Direktiven bleiben gültig. Der gezielte Parserlauf besteht
mit **1/1** Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst
**1286/1286** Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte
VISIA-Projekt-Items. Die atomare Erwartung `l1-02-a-option-directive-context-guard` ist
`implemented`/`documented-verified`; die Matrix steht bei **64 implemented**, **3 partial**,
**46 planned** und **67/113 documented-verified**. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Attribute-Kontext (30.08.2026)

`Attribute`-Metadatenzeilen werden innerhalb einer Prozedur oder eines verschachtelten
Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen; module-level-
Attribute bleiben gültig. Der gezielte Parserlauf besteht mit **1/1** Test. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Lauf misst **1287/1287** Tests, 0 Warnungen/Fehler
im Release-Build und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die atomare Erwartung
`l1-02-a-attribute-context-guard` ist `implemented`/`documented-verified`; die Matrix steht bei
**65 implemented**, **3 partial**, **46 planned** und **68/114 documented-verified**. Die breite
Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

## L1-02-A Dim-Modulvariablen-Sichtbarkeit (30.08.2026)

Eine module-level-`Dim`-Variable kann aus ihrem deklarierenden Modul gelesen und geschrieben
werden, bleibt aber für ein anderes `Option Explicit`-Modul mit `VB6S0001` verborgen;
`ModuleVariableSymbol.IsPublic` bleibt `false`. Der gezielte Compilerlauf besteht mit **1/1**
Test. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst **1288/1288**
Tests, 0 Warnungen/Fehler im Release-Build und **40/40** fehlerfrei analysierte VISIA-
Projekt-Items. Die atomare Erwartung `l1-02-a-dim-module-variable-resolution` ist
`implemented`/`documented-verified`; die Matrix steht bei **66 implemented**, **3 partial**,
**46 planned** und **69/115 documented-verified**. Die breite Erwartung `L1-02-A` bleibt
für weitere Grammatik-/Kontextregeln auf `partial`.

## Qualitätslauf Q-01 bis Q-09 (30.08.2026)

Die Kompatibilitätsmatrix führt nach der atomaren Zerlegung nun **115 Erwartungen** mit zwei
unabhängigen Achsen: **66 `implemented`**, **3 `partial`** und **46 `planned`** auf der
Implementierungsachse sowie **69 `documented-verified`** und **46 `not-yet-verified`** auf der
Verifikationsachse. Die 34 Erwartungen aus L1-03/L1-04 sind als geplante, noch nicht verifizierte
Einträge materialisiert; `L1-02-A` bleibt für den noch offenen Grammatik-/Kontextumfang auf
`partial`. Die Vollständigkeitsansprüche `format.complete-surface` und `math.complete-surface`
stehen nach dem Konsistenzcheck ebenfalls ehrlich auf `partial`.

`build.ps1` prüft die Statusachsen und die Gegenprobe mechanisch; ein Statuslauf mit falscher
100-%-Achse wird abgewiesen. Roadmap, README und Ausführungsplan führen denselben aktuellen
Readout und verweisen auf die verbindlichen Leitplanken. Der kanonische
`build.ps1 -NoRestore -Configuration Release`-Nachweis misst aus 13 frischen TRX-Dateien
**1288/1288** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu **40/40** fehlerfrei
analysierte VISIA-Projekt-Items und den Matrix-Readout **66 implemented, 3 partial, 46 planned
von 115 | 69/115 documented-verified**. `git diff --check` bleibt ohne echte Whitespace-Fehler;
ein installierter nativer VB6-Compiler wird weiterhin nicht vorausgesetzt.

## L1-02-C Array-/UDT-Shape abgeschlossen (30.08.2026)

Die breite Erwartung `l1-02-c-array-udt-shape` ist geschlossen. Der Managed-Pfad bewahrt Rang,
explizite Unter-/Obergrenzen und Elementtypen durch IR-Lowering und ByRef-Write-back; feste und
verschachtelte UDT-Arrayfelder behalten deterministische Defaultwerte und Bounds. Ungültige Bounds,
Rangänderungen und nicht darstellbare UDT-Layouts werden durch die bestehenden
VB6-kompatiblen Laufzeit-/Semantikdiagnosen abgewiesen. Ein IR-Test prüft die typisierte
mehrdimensionale ReDim-Form, ein Semantiktest den `VB6S0046`-Guard; die bestehenden E2E- und
Runtime-Tests decken Write-back, UDT-Defaults sowie Bounds-/ReDim-Fehler ab.

Die gezielten Läufe bestanden mit **26 Compiler-Tests**, **22 Semantiktests** und **21 Runtime-
Arraytests**. Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst aus 13
frischen TRX-Dateien **1290/1290** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu
einen Release-Build ohne Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-
Items. Die Matrix steht bei **67 implemented**, **3 partial**, **45 planned** von **115** sowie
**70/115 documented-verified**. Ein nativer VB6-SP6-Compiler ist weiterhin nicht installiert;
`oracle-verified` wurde nicht gesetzt.

## L1-02-D Control-Flow-/Error-State abgeschlossen (30.08.2026)

Die breite Erwartung `l1-02-d-control-flow-error-state` ist geschlossen: If/Select-, Schleifen-
und GoTo-Kanten werden als explizite Managed-CFG-Blöcke gelowert; aktive `On Error`-Handler,
Resume-Ziele sowie `Err`-/`Erl`-Zustände bleiben auch über Prozeduraufrufe erhalten. Illegale
Kontrollfluss-/Fehlerbehandlungskonstrukte liefern stabile Diagnosen. Die gezielten Läufe
bestanden mit **13 Compiler-Tests**, **11 Parser-Tests** und **1 Managed-Diagnostic-Test**.
Der kanonische `build.ps1 -NoRestore -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1293/1293** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **3 partial**, **44 planned** von **115** sowie **71/115
documented-verified**. Ein nativer VB6-SP6-Compiler ist weiterhin nicht installiert;
`oracle-verified` wurde nicht gesetzt.


## Operator-Fehlerkontrakte, Doku-Abgleich und Hausputz (30.08.2026)

Die breite Erwartung `l1-02-e-operator-dispatch` ist **begonnen, nicht geschlossen**. Gebaut ist
ihre `overflow`-Klausel: `VBErrors.Set` bildet `OverflowException` auf VB6-Fehler **6** und
`DivideByZeroException` auf **11** ab, statt beide wie bisher auf **5** („Invalid procedure call“)
zusammenfallen zu lassen. Die Zuordnung sitzt zentral im Err-Zustand und gilt damit für jeden
Operator statt pro Aufrufstelle; die bereits vorhandene Unterscheidung in `DivideDouble`/
`DivideSingle` — `x / 0` ist Division durch Null, `0 / 0` ist Überlauf — wird dadurch erstmals bis
zu `Err.Number` sichtbar. Zwei End-to-End-Tests in `VariantEqualityExecutionTests` decken das ab:
`Long`-Überlauf und ungültige Variant-Array-Arithmetik (**6**/**13**) sowie die Divisionsfälle
(**11**/**6**/**11**). Die Klauseln `dispatch` und `compare` sind nicht nachgemessen; die
Erwartung steht deshalb auf `partial`/`documented-verified` und bleibt als offener
Familienstatus sichtbar.

Dazu der Abschluss des Qualitätsdurchgangs `Q`: `build_diag.txt` (1,7 MB, in `8fb3feb`
versehentlich mitcommittet) verlässt das Tracking und steht in `.gitignore`. Im README fallen
zwei historische CI-Absätze (**258** und **243** Tests) und eine veraltete Testzahl im
LLVM-Abschnitt (**1036**) weg — Changelog-Prosa im README, die dem aktuellen Messwert
widersprach; die Verifikationshistorie steht in dieser Datei.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1296/1296** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **4 partial**, **43 planned** von **115** sowie **72/115**
`documented-verified`. Die Statusregel aus `Q-02` wurde gegengeprüft: Eine testweise auf
`documented-verified` gehobene `planned`-Erwartung bricht `build.ps1` mit der erwarteten Meldung
ab. Ein nativer VB6-SP6-Compiler ist weiterhin nicht installiert; `oracle-verified` wurde nicht
gesetzt.

## Nativer OCX-Pfad nachgemessen (30.08.2026)

Die README-Zeile zum nativen OCX-Pfad stand auf **48/48** und war seit mehreren Slices nicht mehr
nachgemessen. Der Lauf ist jetzt gemacht: `MSCOMCTL.OCX` (6.01.9834), `RICHTX32.OCX`,
`COMDLG32.OCX` und `MSCOMCT2.OCX` (je 6.01.9782) liegen in `SysWOW64` und sind registriert; eine
Installation war nicht nötig.

```
$env:VB6_REQUIRE_NATIVE_OCX = '1'
dotnet test tests/VB6.Runtime.WinForms.Tests -c Release -- RunConfiguration.TargetPlatform=x86
```

Ergebnis: **50/50** bestanden, **0** übersprungen. Die in `CLAUDE.md` geforderte Gegenprobe mit
`TargetPlatform=x64` schlägt mit **7** Fehlern fehl — fünf RichTextBox-, ein TreeView- und ein
Standard-OCX-Fall melden „requires a registered 32-bit control“. Der x86-Lauf ist damit eine echte
Messung und kein stillschweigend übersprungener. Das README nennt beide Zahlen.

## L1-02-F Variant-Zustand und Null-Konvertierungen (30.08.2026)

Die breite Erwartung `l1-02-f-variant-state-conversions` ist **begonnen, nicht geschlossen**.

Der Kern der Karte: Eine ungültige Null-Konvertierung meldete **5** („Invalid procedure call"),
VB6 meldet **94** („Invalid use of Null"). Der neue Guard `VBVariants.ThrowIfNull` sitzt im
Prolog der Basiskonvertierungen `CByte`, `CInt`, `CLng`, `CLngLng`, `CLngPtr`, `CUShort`,
`CUInt`, `CULng`, `CCur`, `CSng`, `CDbl`, `CDate`, `CBool` und `CStr`. Damit gilt er für den
ausdrücklichen Aufruf (`VBConversions.CInt`) **und** den impliziten Pfad (`ConvertCInt` über
`RejectImplicitError`), weil beide durch dieselbe Funktion laufen.

**`CDec` ist bewusst ausgenommen.** Ein erster Versuch hat den Guard auch dort gesetzt und den
bestehenden Test `CDec_ProducesVariantDecimalAndPreservesNull` zum Fallen gebracht. Der Test hat
recht: `CDec` liefert einen Variant mit Decimal-Subtyp und kann Null tragen, anders als `CInt`
oder `CStr`, deren Zieltyp Null nicht darstellen kann. Ohne installiertes Orakel wiegt eine
benannte, bestehende Vertragszusage schwerer als eine Herleitung; der Guard wurde dort
zurückgenommen und die Begründung steht als Kommentar an der Stelle.

Zweiter Befund aus derselben Messung: `CDate("kein Datum")` und `CInt("keine Zahl")` meldeten
**5** statt **13** („Type mismatch"). `VBErrors.Set` bildet `FormatException` und
`InvalidCastException` jetzt auf 13 ab — dieselbe zentrale Stelle wie die Überlauf- und
Divisionskontrakte der Vorkarte.

Nachgewiesen sind damit die Klauseln `state` (Subtyp-Tags überleben Zuweisung und Rückweg) und
`numeric` (Banker's Rounding in `CLng` und `CCur`, Überlauf 6, Type Mismatch 13), dazu die
Konvertierungshälfte von `null`. **Offen und genau vermessen** bleibt die Null-Weitergabe durch
`Left`, `Right`, `Mid`, `Trim`, `LTrim`, `RTrim`, `UCase` und `LCase`: In VB6 liefern sie bei
Null selbst Null, hier sind sie `String -> String` deklariert und melden seit dieser Karte 94.
Das ist keine Verschlechterung — vorher entkam dort eine nackte `InvalidCastException`, die als
5 ankam —, aber auch nicht das VB6-Verhalten. Die Umstellung auf `Variant -> Variant` verschiebt
den statischen Typ sehr häufiger Ausdrücke und bekommt deshalb eine eigene Karte. `Len`, `Abs`,
`Sgn`, `Int`, `Fix` und `CDec` reichen Null bereits korrekt weiter; `IsNumeric` und `TypeName`
tun es korrekterweise nicht.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1311/1311** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **5 partial**, **42 planned** von **115** sowie **73/115**
`documented-verified`. Ein nativer VB6-SP6-Compiler ist weiterhin nicht installiert;
`oracle-verified` wurde nicht gesetzt.

## L1-02-G Variant-Promotionstabelle festgeschrieben (30.08.2026)

Diese Karte hat nichts repariert, sondern etwas abgesichert. Die Promotionstabelle wurde über
**49 Operandenpaare** nachgemessen — Arithmetik, Division, `Mod`, `^`, Verkettung, Logik und
Vergleich — und war **durchgehend korrekt**. Sie war nur fast ungetestet, also jederzeit
still kaputtzumachen. Jetzt liegen 24 Promotionszeilen mit Subtyp *und* Wert als Test vor,
dazu Vergleich, Logik und Verkettung sowie die Ablehnungsfälle.

Der lehrreiche Fehlschlag dabei: Die erste Testfassung schrieb die Operanden inline und lief
in einen echten Überlauf. Bei **Variant**-Operanden geht Integer nach Long, Long nach Double
und Byte nach Integer; bei **statisch typisierten** Ausdrücken gilt die Projektinvariante und
`CInt(32767) + CInt(1)` überläuft. Beide Regeln sind richtig, gelten aber für verschiedene
Dinge. Der Test führt seine Operanden deshalb über `Variant`-Variablen und sagt im Kommentar,
warum.

**Zwei Änderungen wurden begonnen und wieder zurückgenommen.** Die explizite Konvertierung
eines Error-Variants (`CInt(CVErr(5))`) liefert dessen Code, während der implizite Pfad
korrekt **13** meldet; VB6 meldet nach Dokumentationslage auch beim expliziten Aufruf 13. Der
Versuch, das anzugleichen, riss `ErrorVariantConversions_DistinguishExplicitAndImplicitPaths` —
einen Test, der diese Unterscheidung im Namen führt — und hängt über `CInt(Missing) = 448` an
der Missing-Argument-Mechanik. Ein benannter, bestehender und mit anderer Funktionalität
verkoppelter Vertrag wiegt ohne Orakel schwerer als eine Herleitung; die Änderung wurde
vollständig zurückgenommen, `src/` ist gegenüber dem Vorstand bytegleich.

**Offen** bleibt in der `errors`-Klausel „incompatible object operands": Gemessen an einer
`Collection` meldet `o + 1` korrekt **13**, `o & "x"` dagegen **0** und `o = 1` **5**. Ob 13
der Sollwert ist, hängt an der Default-Property der `Collection`; ohne Orakel wurde nichts
geändert. Ebenfalls notiert, aber ausserhalb dieser Karte: `Debug.Print` und `CStr` geben ein
Date-Variant als OADate-Seriennummer aus (`46024`) statt als Datum. Das berührt den in
`CLAUDE.md` als offen geführten Zielkonflikt zwischen VB6-Locale-Treue und
Invariant-Determinismus und wird nicht einseitig aufgelöst.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1314/1314** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **6 partial**, **41 planned** von **115** sowie **74/115**
`documented-verified`. `l1-02-g-variant-promotion-table` steht auf `partial`: `promotion` und
`empty` sind nachgewiesen, `errors` nur für Null-, String- und Error-Operanden.

## Zwei Arbeitsregeln aus den L1-02-Karten festgehalten (30.08.2026)

Über die bisher bearbeiteten Karten hinweg zeigte sich ein Muster, das jetzt als Regel steht
statt als Erfahrung: **Die Umsetzung ist hier durchweg weiter als ihre Absicherung.** Bei
`l1-02-f` und `l1-02-g` lautete der Befund zweimal hintereinander „das Verhalten war bereits
richtig, nur ungetestet"; bei der Promotionstabelle waren alle 49 gemessenen Operandenpaare
korrekt.

Daraus zwei neue Leitplanken:

- **§11 Erst messen, dann bauen.** Eine Karte beginnt mit einer Messung des Ist-Verhaltens über
  die volle Breite ihres Vertrags — ein Wegwerfprogramm über `VB6TestProgram.RunLines`, das
  `VarType`, `Err.Number` und Ergebniswert ausgibt, nicht Codelektüre. Die echten Lücken
  (`Err.Number` 5 statt 94, 5 statt 13) waren beim Lesen unsichtbar. Korrektes, aber
  ungetestetes Verhalten ist ein Kartenergebnis: Es wird festgeschrieben, die
  verification-Achse wandert nach oben, die implementation-Achse bleibt stehen.
- **§12 Bestandsschutz benannter Verträge.** Reißt eine Änderung einen Test, dessen Name eine
  Vertragszusage ausspricht, wird die Änderung vollständig zurückgenommen — nicht der Test
  angepasst. Ohne Orakel schlägt der bestehende Vertrag die Herleitung aus der
  VB6-Dokumentation. Belegt an `CDec(Null)` und `CInt(CVErr(5))`, wo beide Male die
  dokumentationsgestützte Herleitung plausibel und falsch war.

§9 bekommt die passende Abbruchbedingung, der Abschlussbericht in §10 zwei zusätzliche Punkte
(Fallzahl der Vorabmessung, zurückgenommene Änderungen). Der Ausführungsplan führt beides als
Regeln 7 und 8 sowie als neues Pflichtfeld `Vorabmessung` im Arbeitskartenvertrag; `CLAUDE.md`
nimmt beide als Fallen auf, weil sie nicht nur für Luna gelten.

Reine Dokumentationsänderung; `src/` und `tests/` bleiben unberührt.

## L1-02-H Variant-Objekt- und Array-Dispatch (30.08.2026)

Erste Karte nach der neuen §11-Regel. Die Vorabmessung umfasste **8 Programme mit rund 40
beobachteten Werten**; **drei Lücken** kamen heraus, der Rest war bereits korrekt.

**`TypeName` gab den CLR-Typnamen preis.** Ein `Collection`-Objekt meldete `VBCollection`
statt `Collection`. Ein Zeichen — aber es macht den Namen des Runtime-Typs zu beobachtbarem
Programmverhalten.

**Der spät gebundene Pfad konnte `Collection.Add` nicht aufrufen.** `VBCollection.Add`
deklarierte `Key`, `Before` und `After` als erforderlich, obwohl VB6 sie als optional führt.
Der typisierte Pfad lief, weil der Binder über `AddValue` immer alle vier Argumente übergibt;
`Dim c As Variant` und `Dim c As Object` scheiterten dagegen schon an
`CanAcceptArgumentCount` mit `MissingMemberException`. `[Optional]` an den drei Parametern
behebt das, ohne den Dispatcher anzufassen — dessen Optional-Behandlung war bereits da.

Dabei fiel ein zweiter Punkt auf: `OptionalValue` lieferte für ein ausgelassenes
`object`-Argument `null`. In VB6 ist ein ausgelassenes optionales Variant-Argument aber
**Missing**, nicht Empty — und nur dann beantwortet `IsMissing` im Ziel die Frage, ob das
Argument übergeben wurde. `Add "x"` hätte sonst `Before` als angegeben behandelt.

**Fehlerzuordnung.** Ein Zugriff ausserhalb der Arraygrenzen meldete **5**, VB6 meldet **9**
(„Subscript out of range"); ein nicht vorhandenes Mitglied meldete **5**, VB6 meldet **438**.
Beide laufen jetzt über `VBErrors.Set`. Bewusst **nicht** gemappt wurde
`ArgumentOutOfRangeException`: Sie deckt auch Fälle wie `Space(-1)` ab, für die VB6 weiterhin 5
meldet — eine pauschale Zuordnung hätte dort eine korrekte Nummer kaputtgemacht.

Bereits korrekt und jetzt festgeschrieben: Objektidentität über `Is`, die Unterscheidung
Nothing/Null in `IsObject`/`IsNull`/`TypeName`, Arraygrenzen, `VarType` 8204, Elementsubtypen
über ByRef-Rückschreiben und `ReDim Preserve` hinweg, sowie die Argument-Coercion beim
indizierten Zugriff (2.6 auf 3, 2.5 auf 2, `Currency`-Konvertierung, String bleibt Key).

**Offen** bleibt in der `unsupported`-Klausel die ausdrücklich genannte SAFEARRAY-Hälfte; sie
liegt am COM-/TypeLib-Rand. Die Karte steht deshalb auf `partial`.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1318/1318** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **7 partial**, **40 planned** von **115** sowie **75/115**
`documented-verified`.

## L1-02-I Objektmitglieder und Lebenszyklus (30.08.2026)

Die Vorabmessung nach §11 umfasste **12 Projektläufe** und hat **drei echte Defekte** gefunden.
Behoben wurde nur einer — die anderen beiden sind zu gross fuer diese Karte, und der Versuch
beim dritten wurde bewusst zurueckgenommen.

**Behoben: `TypeName` gab den emittierten Typnamen preis.** Eine Klasse `Box` meldete
`__vb6_class_Box`, sowohl typisiert als auch aus einem Variant. Damit war das Namensschema des
Emitters beobachtbares Programmverhalten. `VBFunctions.TypeName` nimmt die Präfixe
`__vb6_class_`, `__vb6_interface_`, `__vb6_udt_` und `__vb6_module_` jetzt zurück — dieselbe
Klasse von Leck wie `VBCollection` in der Vorkarte, nur gravierender, weil hier die
Namensmangelung selbst sichtbar wurde.

**Zurueckgenommen: `Public`-Felder sichtbar machen.** `ManagedEmitter` emittiert *jedes*
Klassenfeld als `FieldAttributes.Private`. Ein `Public X As Long` ist damit über Modulgrenzen
unbenutzbar und scheitert mit `FieldAccessException` — schon im einfachsten Fall mit einer
Klasse und einem Zugriff aus `Main`. Die Sichtbarkeit liegt in `ModuleVariableSymbol.IsPublic`
bereits vor und wurde versuchsweise über ein neues `IrField.IsPublic` bis zum Emitter
durchgereicht.

Die Messung danach war eindeutig: Mit sichtbarem Feld läuft der Zugriff weiter und endet in
einer **Zugriffsverletzung** (`0xC0000005`) statt in einer fangbaren Ausnahme. Der Feldzugriff
selbst ist also defekt; die private CLR-Sichtbarkeit maskiert das bisher. Eine Änderung, die
eine saubere Ausnahme in einen Prozessabsturz verwandelt, ist keine Verbesserung, auch wenn sie
einen häufigeren Fall reparieren würde. Vollständig zurückgenommen; `src/` trägt davon nichts.

Nebenbefund derselben Messung: Der Binder meldet den Zugriff auf ein **privates** Klassenfeld
von aussen **nicht** — `analysis.Success` ist `true`, und nur die CLR-Sichtbarkeit verhindert
ihn. Das ist ein zweites, eigenständiges Loch.

**Nicht angefasst: der Lebenszyklus.** `Dim x As New C` ruft `Class_Initialize` sofort bei der
Deklaration auf, auch wenn `x` nie benutzt wird; VB6 erzeugt die Instanz bei der ersten
Verwendung. Und `Class_Terminate` feuert **nie** — weder bei `Set o = Nothing` noch beim
Verlassen des Gültigkeitsbereichs. Letzteres ist der bekannte Zielkonflikt zwischen
VB6-Referenzzählung und GC-Laufzeit und verlangt eine Architekturentscheidung, die nach §9 nicht
nebenbei getroffen wird.

Gemessen und bereits korrekt: `Set` teilt die Referenz und `Let` kopiert den Wert (jetzt
festgeschrieben), `Implements` mit `TypeOf`-Prüfung und Dispatch über die Interface-Referenz,
`WithEvents` mit Ereigniszustellung.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1319/1319** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **8 partial**, **39 planned** von **115** sowie **76/115**
`documented-verified`. `l1-02-i-object-members-lifecycle` steht auf `partial`: `assignment` und
`contracts` sind nachgewiesen, `lifecycle` ist nicht gebaut.

## Public-Felder von Klassen: der Empfänger war falsch (30.08.2026)

Vorgezogen aus den offenen Befunden von `L1-02-I`, weil ein Basisfeature betroffen war: Ein
`Public X As Long` in einer `.cls` war aus einem anderen Modul **überhaupt nicht benutzbar**.

Die Ursache lag nicht dort, wo sie zuerst aussah. Die Sichtbarkeit war nur der Deckel: Jedes
Klassenfeld wurde als `FieldAttributes.Private` emittiert, der Zugriff scheiterte mit
`FieldAccessException`, und darunter lag der eigentliche Defekt. `EmitLoad`, `EmitStore` und
`EmitAddress` riefen für ein `IrFieldPlace` einheitlich `EmitAddress` auf den Empfänger. Für
ein UDT ist das richtig — ein Werttyp braucht die Adresse. Eine Klasse ist aber **bereits eine
Referenz**: `ldfld`/`stfld` wollen dann das Objekt selbst, und die Adresse des lokalen Slots
zu laden liest am falschen Offset. Sobald das Feld sichtbar war, endete der Zugriff in einer
Zugriffsverletzung.

Warum der Defekt so lange unsichtbar blieb, hat zwei Gründe. Innerhalb der Klasse ist der
Empfänger `Me`, und `EmitAddress(IrThisPlace)` macht `LoadArgument(0)` — lädt also die
Referenz, nicht ihre Adresse; der Pfad war dort zufällig richtig. Und von aussen verdeckte die
private CLR-Sichtbarkeit den falschen Zugriff als plausibel aussehende
`FieldAccessException`. Ein Fehler, der einen zweiten maskiert.

Der neue Helfer `EmitFieldReceiver` unterscheidet über das bereits vorhandene
`IsReferenceType`: Referenz laden, Werttyp adressieren. Dazu die Sichtbarkeit —
`IrField.IsPublic`, gespeist aus `ModuleVariableSymbol.IsPublic`, und im Emitter
`FieldAttributes.Assembly` statt `Private`. Erst beides zusammen macht `Public`-Felder
benutzbar; einzeln bringt keines der beiden etwas.

Der End-to-End-Test deckt beide Richtungen ab: `Long`- und `String`-Felder über die
Modulgrenze, mehrere Klassen nebeneinander, eine Klasse mit `Implements` **und** Feld, sowie
ein UDT-Feld als Gegenprobe für den Werttyp-Pfad. Ein `Private`-Feld bleibt von aussen
unerreichbar.

**Offen** bleibt der Nebenbefund aus derselben Messung: Der Binder meldet den Zugriff auf ein
privates Klassenfeld weiterhin **nicht** — `analysis.Success` ist `true`, und nur die
CLR-Sichtbarkeit verhindert ihn. Das gehört in eine Binder-Karte.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1320/1320** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrixzahlen
bleiben unverändert: Der Fix schliesst keinen neuen Vertrag, sondern repariert einen Pfad,
den `l1-02-i-object-members-lifecycle` bereits als `partial` führt.

## Breitendurchgang: elf Defekte, ein Muster (30.08.2026)

Vor der nächsten Karte ein gezielter Durchgang über Klassenmitglieder, Modulgrenzen,
ByRef-Rückschreiben, Laufzeitfehlernummern und die Standardbibliothek. Das vollständige
Register steht in `LUNA_EXECUTION_PLAN.md`; hier das Ergebnis und die Konsequenz.

**Kein einziger Defekt lag im Normalfall.** Alle sassen an einer Grenze, und fast alle waren
leise.

Der schwerste Befund: Ein `Public`-Feld einer Klasse wird vom Binder als **Property**
modelliert. `PropertySymbol` hat keinen Marker, der eine synthetisierte Feld-Property von einem
echten `Property Get` unterscheidet; der Lowerer bildet nur den einfachen Lese-/Schreibfall
wieder auf ein `IrFieldPlace` ab. Daraus folgen vier Symptome mit **einer** Ursache — verlorenes
ByRef-Rückschreiben (**5 statt 6, ohne jede Diagnose**), abgelehntes `Set` auf ein Objektfeld,
nicht indizierbare Array-Felder und ein Parserfehler bei `Public S As String * 5`. Die
Gegenprobe zeigt, dass ByRef über Locals, `Global`-Variablen, UDT-Member und Array-Elemente
korrekt zurückschreibt — nur über Klassenfelder nicht.

Dazu drei fehlende Fehlernummern (**91** bei Zugriff auf eine nicht gesetzte Objektvariable,
**53** bei nicht gefundener Datei, vermutlich **9** bei `Collection`-Index) und acht fehlende
Standardfunktionen (`StrReverse`, `FormatNumber`, `FormatCurrency`, `FormatPercent`,
`FormatDateTime`, `Partition`, `CallByName`, `QBColor`) — letzteres deckt sich mit der bereits
erfolgten Rückstufung von `format.complete-surface` und `math.complete-surface`.

Die Konsequenz steht als **§13 der Leitplanken**: die vier Grenzen, an denen zu messen ist
(Bindungsart, Modulgrenze, Wert gegen Referenz, Deklarationsform), die Regel dass Fehlernummer
**5** ein Verdacht und kein Ergebnis ist, die Priorisierung „still falsch" vor „falscher Code"
vor „meldet nicht", der Hinweis dass eine Reparatur mit schlechterem Fehlerbild auf einen
**zweiten** Defekt darunter deutet, und das Verbot, aus einer einzelnen Probe auf die Ursache
zu schliessen. Alle fünf Regeln stammen aus konkreten Fehlschlägen dieses Durchgangs, zwei
davon aus falschen Ursachenvermutungen, die erst die nächste Probe widerlegt hat.

Reine Mess- und Dokumentationsarbeit; `src/` und `tests/` bleiben unberührt. Der kanonische
Lauf bleibt bei **1320/1320** Tests, VISIA **40/40**.

## Befunde kartenfähig gemacht (30.08.2026)

Das Befundregister war eine Liste, keine Warteschlange. Nach dem Arbeitskartenvertrag braucht
jede Karte **genau eine Erwartungs-ID**; ohne sie hätte Luna die Befunde nicht abschliessen
können, weil §1 einen Statuswechsel ohne Erwartung verbietet. Drei neue Erwartungen schliessen
die Lücke, alle `planned`/`not-yet-verified`:

- `s1-class-public-field-storage` — ByRef-Rückschreiben, `Set` auf Objektfelder, Array-Felder
  und `String * n` als Klassenmember (Befunde A1–A4).
- `s2-documented-runtime-error-numbers` — 91 bei nicht gesetzter Objektvariablen, 53 bei
  fehlender Datei, `Collection`-Index (B1–B3).
- `s3-remaining-standard-intrinsics` — die acht nicht deklarierten Funktionen (C).

Die Matrix steht damit bei **118 Erwartungen**. Das Register nennt jetzt zu jeder Karte die
Erwartungs-ID **und** die Einstiegsdateien, damit keine Repository-Gesamtsuche nötig ist.

**Zwei Widersprüche behoben**, die Luna in die falsche Richtung geschickt hätten. „Aktueller
Einstieg" nannte `l1-02-j-nested-error-resume` als nächste Karte, während das Register `S1`
vorzog; die Reihenfolge lautet jetzt ausdrücklich **S1 → S2 → S3 → `l1-02-j`**, mit der
Begründung aus §13 („still falsch" hat Vorrang). Und der Abschnitt „Reihenfolge der Wellen"
trug eine zweite, längst veraltete Zahlenangabe (115 Erwartungen, 67/3/45/70); sie ist durch
einen Verweis auf den `build.ps1`-Readout ersetzt — eine Kopie weniger, die auseinanderlaufen
kann.

Reine Planungsarbeit; `src/` und `tests/` bleiben unberührt. Der kanonische Lauf misst
**1320/1320** Tests, VISIA **40/40**, und das Matrix-Gate akzeptiert die drei neuen
Erwartungen.

## S1 (Teil A1): ByRef-Rückschreiben über ein Public-Feld (30.08.2026)

Der gefährlichste Befund des Breitendurchgangs ist behoben: `Bump c.N` mit einem
`ByRef`-Parameter verwarf das Rückschreiben **still** und lieferte 5 statt 6 — kein Fehler,
keine Diagnose, falscher Wert.

Die Ursache lag im Binder. `AddReadWriteProperty` in `VBProjectCompilation.cs` macht aus jeder
Klassenvariablen ein synthetisiertes Get/Let-Property-Paar, und `PropertySymbol` hatte keinen
Marker, der so etwas von einem echten `Property Get` unterscheidet. Die ByRef-Positivliste in
`Binder.cs` lehnt Property-Zugriffe zu Recht ab — ein `Property Get` besitzt keinen
Speicherplatz, an den zurückgeschrieben werden könnte — und traf damit auch die Felder.

`PropertySymbol.IsFieldBacked` schliesst die Lücke. Gesetzt wird es nur für die synthetisierten
Paare echter Klassenvariablen, ausdrücklich **nicht** für Designer-Controls, die `IsLateBound`
sind. Der Lowerer brauchte keine Änderung: `LowerPropertyPlace` bildet einen solchen Zugriff
über `TryGetClassFieldPlace` längst auf ein `IrFieldPlace` ab — der Binder legte nur vorher
einen Temp an, der nie dorthin kam.

Die Messung folgte den vier Grenzen aus §13. Rückgeschrieben wird jetzt von aussen, über `Me`
von innen und für ein `Variant`-Feld (je **6**). Die Gegenproben halten: Ein echtes
`Property Get`/`Let` behält den Temp (**5**), ein UDT-Member schreibt wie bisher zurück (**6**).
Beide stehen als Assertion im Test, damit die Unterscheidung nicht später verloren geht.

**Die Grenzmessung hat dabei einen neuen Befund geliefert** (Grenze 1, Bindungsart): Ein spät
gebundener Zugriff auf ein öffentliches Klassenfeld findet es überhaupt nicht —
`Dim o As Object : o.N = 5` meldet `MissingMemberException`, weil `VBDynamicDispatch` Methoden
und Properties sucht, aber keine Felder. Der Befund ist vorbestehend und wurde **nicht**
nebenbei behoben; er steht im Register.

`s1-class-public-field-storage` steht damit auf `partial`: Die `byref`-Klausel ist gebaut und
nachgewiesen, `set`, `array` und `fixed-string` bleiben offen.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1321/1321** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrix steht bei
**68 implemented**, **9 partial**, **41 planned** von **118** sowie **77/118**
`documented-verified`.

## S1 (Teil A2): Set auf ein Public-Feld (30.08.2026)

`Set c.ObjFeld = New Collection` meldete `VB6S0064` („no object-assignable property"), obwohl
echter Speicher dahinterliegt. `AddReadWriteProperty` erzeugte nur ein Get/Let-Paar; ein
`Set`-Accessor fehlte. Er wird jetzt für Felder angelegt, die eine Objektreferenz tragen können
— `ClassTypeSymbol` oder `Variant`. Der Lowerer brauchte auch hier keine Änderung.

**Der erste Anlauf war zu breit und hat einen bestehenden Test gerissen.** Mit einem
Set-Accessor für *jedes* Feld fiel
`EmitManagedApplication_ExecutesClassFieldsMethodsPropertiesAndInitialize`: Die letzten beiden
Ausgabezeilen fehlten. Nach §13 wurde nicht geraten, sondern isoliert — **ohne** `WithEvents`
läuft ein unqualifiziertes `Set held = New Src` weiterhin, **mit** `WithEvents` band es
plötzlich an die neue Property und umging die Verdrahtung der Ereignishandler.

Eine `WithEvents`-Variable ist kein einfacher Speicher: Ihre Zuweisung verdrahtet die Handler
neu. Sie bekommt deshalb bewusst **keinen** Set-Accessor. Das ist keine Notlösung, sondern der
Vertrag — `Set Me.held = …` meldet seitdem `VB6S0064`, statt die Verdrahtung still zu umgehen.
Der Test hält beide Seiten fest: vier Feldformen mit `Set` und, als letzte Zeile, ein
`WithEvents`-Handler, der weiterhin feuert.

**Weiterer Befund aus der Gegenprobe** (Grenze 4, Deklarationsform): Eine Klasse mit **beiden**
Accessoren `Property Get` und `Property Set` gleichen Namens liefert aus dem `Get` **Empty**.
Isoliert nachgemessen: Das `Set` speichert korrekt — innen gelesen kommt der Wert an — und ein
`Get` ohne zugehöriges `Set` liefert korrekt. Nur die Kombination bricht, und das ist die
Normalform jeder VB6-Objekt-Property. Vorbestehend, gehört nicht zu `S1`, steht im Register.

`s1-class-public-field-storage` bleibt `partial`: `byref` und `set` sind gebaut und
nachgewiesen, `array` und `fixed-string` offen.

Der kanonische `build.ps1 -Configuration Release`-Lauf misst aus 13 frischen TRX-Dateien
**1322/1322** Tests, **0** Fehler und **0** nicht ausgeführte Tests, dazu einen Release-Build ohne
Warnungen/Fehler und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die Matrixzahlen
bleiben unverändert.

## Roadmap traegt die Befunde (30.08.2026)

Die Rollenteilung aus §4 sagt: Roadmap ist Ist-Stand **und Offenes**. Die elf gemessenen
Defekte des Breitendurchgangs standen aber nur im Ausführungsplan — die Roadmap führte die
betroffenen Flächen zwar allgemein („alle dokumentierten String-, Math-, … Verträge
implementieren"), nannte aber keinen der konkreten Befunde. Wer nur die Roadmap liest, hätte
sie nicht gefunden.

Etappe B trägt jetzt fünf Zeilen: `Public`-Felder als echter Speicher (`byref` und `set`
erledigt, `array`, `String * n` und der spät gebundene Zugriff offen), die Kombination
`Property Get` **und** `Property Set`, den fehlenden Binder-Guard für private Klassenfelder,
`As New`/`Class_Terminate` samt der offenen Architekturfrage, und Fehlernummer **91**.

Etappe C trägt drei: die acht nicht deklarierten Standardfunktionen, die Dateifehlernummer
**53** samt des ohne Orakel unentschiedenen `Collection`-Index, und die acht String-Intrinsics,
die `Null` nicht weiterreichen.

Der gemessene Ist-Stand verweist zusätzlich auf das Befundregister im Ausführungsplan, damit
die Messwerte nur an einer Stelle stehen und nicht auseinanderlaufen können.

Reine Dokumentationsarbeit; `src/` und `tests/` bleiben unberührt. Der kanonische Lauf bleibt
bei **1322/1322** Tests, VISIA **40/40**.

## Array-typisierte Klassenfelder sind indizierbar (31.08.2026)

Karte `S1`, Teil A3 aus dem Befundregister: `c.Nums(1)` bei `Public Nums(1 To 3) As Long`
meldete `VB6S0006` — „Procedure 'Nums' expects 0 argument(s), but 1 were supplied".

Die Vorabmessung nach §11 hat die vermutete Ursache widerlegt. Notiert war
„`AddReadWriteProperty` erzeugt Properties ohne Parameter", die naheliegende Reparatur wäre
gewesen, ihr welche zu geben. Das Wegwerfprogramm über 18 Fälle hat gezeigt, dass das falsch
gewesen wäre: `c.Nums` **ohne** Index lieferte längst das echte Array. `LBound(c.Nums)`,
`UBound(c.Nums)`, `For Each v In c.Nums` und `c.Nums = other` liefen von Anfang an. Kaputt war
allein die indizierte Form, und zwar im Binder: `BindClassMemberInvocation` liest jede Property
mit Argumenten als indizierte Property.

Die synthetisierte Get/Let-Property bleibt deshalb bewusst parameterlos. Parameter hätten sie
von einem echten `Property Get` ununterscheidbar gemacht und damit A1 und A2 wieder aufgerissen,
die genau an dieser Unterscheidung hängen. Stattdessen erkennt der Binder eine Property mit
`{ IsFieldBacked: true, IsLateBound: false }` und `ArrayTypeSymbol`-Typ und bindet `c.Nums(1)`
als `BoundElementAccessExpression` über den Feldzugriff — denselben Knoten, den ein indiziertes
UDT-Member erzeugt.

Lowerer und Emitter brauchten keine Zeile. `LowerPlace` bildet `BoundElementAccessExpression`
bereits auf `IrArrayElementPlace` ab, und weil ein VB6-Array eine Referenz ist, deckt diese eine
Substitution Lesen, Schreiben und ByRef-Rückschreiben gleichzeitig ab. Die Änderung liegt
vollständig in `Binder.cs`.

Gemessen und danach als Test festgeschrieben: Lesen, Schreiben, `LBound`/`UBound`, `ReDim` von
außen, `Me.Nums(i)` von innen, Zuweisung des ganzen Arrays, Variant-, String- und
zweidimensionale Felder, `For Each` sowie ByRef-Rückschreiben in ein Element (**6**).

Gegenproben nach §13, alle unverändert: eine echte indizierte `Property Get` bleibt ein Aufruf
und liefert beim ByRef-Versuch **105** statt **6**, weil ihr Argument ein Temp ist; ein skalares
Feld mit Index meldet weiterhin `VB6S0006`; ein falscher Rang meldet `VB6S0027` statt still eine
Dimension zu verwerfen.

**Neuer Befund, nach §9 gemeldet statt nebenbei erledigt:** Eine deklarierte
`Property Get Nums() As Long()` — eine echte Property mit Array-Rückgabetyp — kann nicht
indiziert werden; `c.Nums(1)` meldet `VB6S0006`. In VB6 wird die Property gerufen und ihr
Ergebnis indiziert. Der Befund ist vorbestehend und wird von der Feld-Erkennung ausdrücklich
nicht berührt, weil sie `IsFieldBacked` verlangt. Er steht jetzt in Roadmap-Etappe B und im
Befundregister und braucht eine eigene Karte.

Die Matrix-Erwartung `s1-class-public-field-storage` hat zusätzlich die bislang fehlende
`expected`-Zeile `late-bound` bekommen — der spät gebundene Feldzugriff war im Register
vermerkt, aber nicht in der Karte. `S1` bleibt `partial`: `fixed-string` (A4) und `late-bound`
sind offen.

Kanonischer Nachweis: `build.ps1 -Configuration Release` misst **1326/1326** Tests, **0** Fehler,
Release ohne Warnungen und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
Matrixzahlen bleiben bei **68 implemented, 9 partial, 41 planned von 118 | 77/118
documented-verified**.

## Fixed-length Strings in allen Deklarationsformen (31.08.2026)

Karte `S1`, Teil A4 aus dem Befundregister. Im Register stand „`Public S As String * 5` in
`.cls` ist ein Parserfehler" — also ein Klassenproblem. Die Vorabmessung nach §11 über 14 Fälle
hat den Zuschnitt korrigiert: `String * n` wurde **überall** abgelehnt außer als UDT-Member.
Auch `Dim S As String * 5` in einer Prozedur und `Public S As String * 5` in einer `.bas` waren
Parserfehler, weil der Parser die Form nur in `ParseTypeDeclaration` kannte. Die Karte war keine
Klassenkarte, sondern eine Deklarationskarte.

Die Reparatur braucht je Schicht genau eine Stelle, weil `Dim`, `Static`, `ReDim` und jede
Modulform durch `ParseVariableDeclarators` beziehungsweise `ResolveVariableDeclaratorType`
laufen. `VariableDeclaratorSyntax` trägt jetzt `StarToken` und `FixedStringLength` in derselben
Form wie `TypeMemberSyntax`. Die Längenprüfung im Binder ist bewusst identisch mit der des
UDT-Members: `VB6S0042` für einen Nicht-String-Typ, `VB6S0043` für eine Länge außerhalb der
Literal-Teilmenge, `VB6S0044` für eine Länge außerhalb von 1 bis 65526 — dieselben Codes auf
dieselbe Eingabe, damit die beiden Deklarationswege nicht auseinanderlaufen.

**Unter der Parser-Lücke saßen zwei weitere Defekte, die erst danach sichtbar wurden.** Genau
das Muster aus §13: Ein besseres Fehlerbild legt ein schlechteres Verhalten frei, und wer nach
der ersten Reparatur aufhört, hinterlässt „läuft durch, liefert falsch".

1. **Kein Auffüllen bei einfacher Zuweisung.** `S = "ab"` bei `String * 5` ergab `[ab]` statt
   `[ab   ]`. `BoundArrayElementAssignmentStatement` und `BoundMemberAssignmentStatement` liefen
   längst über `LowerFixedStringWrite`, `BoundAssignmentStatement` nicht.
2. **Falscher Anfangswert.** Ein `String * 4` ist in VB6 vier Leerzeichen. Nur das UDT-Member
   war korrekt; `InitializeVariableDeclaration`, `EmitModuleInitializers` und
   `LowerClassConstructor` prüften alle drei auf `TypeSymbol.String` und ließen den
   Fixed-Length-Typ durchfallen. `NeedsModuleInitialization` schloss ihn ebenfalls aus, sodass
   für eine Modulvariable gar kein Initialisierer entstand.

Gemessene Endlage über 14 Fälle, alle korrekt: Anfangswert einheitlich vier Leerzeichen über
Local, Modulvariable, Klassenfeld und UDT-Member; Abschneiden beim Überschreiten und Auffüllen
beim Unterschreiten; Vergleich gegen den aufgefüllten Wert liefert `True`; Verkettung behält die
Breite; Arrays von `String * n` und private Klassenfelder verhalten sich gleich.

**Zwei Befunde nach §9 gemeldet statt nebenbei geändert:**

- Eine **benannte Konstante als Länge** (`String * Breite`) meldet `VB6S0043`. Das ist dieselbe
  Teilmengenbeschränkung, die das UDT-Member schon trug. Sie wurde bewusst gespiegelt statt
  einseitig erweitert; beide Formen gemeinsam zu öffnen ist eine eigene Karte.
- **`String * 4` an einen `ByRef s As String`** meldet `VB6S0008`. Echtes VB6 erlaubt die
  Übergabe mit Copy-in/Copy-out. Die Typstrenge bei ByRef ist aber eine ausdrücklich
  dokumentierte Entscheidung dieses Projekts, und nach §12 wird ein benannter Vertrag nicht
  ohne Ansage aufgeweicht. Der Zielkonflikt steht jetzt in Roadmap-Etappe B.

Von `S1` bleibt damit nur noch `late-bound`: `VBDynamicDispatch` sucht Methoden und Properties,
aber keine Felder, deshalb findet `Dim o As Object : o.N = 5` ein öffentliches Klassenfeld gar
nicht. Die Erwartung bleibt bis dahin `partial`.

Kanonischer Nachweis: `build.ps1 -Configuration Release` misst **1332/1332** Tests, **0** Fehler,
Release ohne Warnungen und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Ein separater
Lauf ohne die neuen Tests, nur mit den `src/`-Änderungen, blieb vorher bei **1326/1326** grün —
die vier Schichtänderungen erzeugen keine Regression. Die Matrixzahlen bleiben bei **68
implemented, 9 partial, 41 planned von 118 | 77/118 documented-verified**.

## UDT-Arraygrenzen falten Konstanten und melden, was nicht faltet (31.08.2026)

Kein Eintrag aus dem Breitendurchgang. Der Befund fiel bei der Vorabmessung zu einer ganz
anderen Frage an — ob eine benannte Konstante als `String * n`-Länge zugelassen werden soll —
und war deutlich schwerer als die Frage, die ihn ausgelöst hat.

In einem `Type`-Block funktionierten als Arraygrenze **ausschließlich nackte Integer-Literale**.
Jede andere Form stürzte zur Laufzeit ab, ohne dass der Compiler etwas meldete: konstante
Arithmetik, eine benannte Konstante, eine Konstante als Untergrenze, eine mehrdimensionale
Deklaration mit einem Ausdruck. Auch die beiden Formen, die garantiert Fehler sind — eine
Variable als Grenze und eine Obergrenze unter der Untergrenze — kamen als Absturz statt als
Diagnose heraus.

Die Ursache war eine stille Rückgabe. Schlug `TryEvaluateIntegerConstant` fehl, lieferte
`BindArrayBounds` eine **leere** Grenzenliste zurück, ohne zu melden. Das Member bekam keinen
Speicher, das Array wurde nie angelegt, und der erste Zugriff riss das Programm mit einer
`NullReferenceException` ab.

Zur Abgrenzung gegen die Smart-App-Control-Falle in `CLAUDE.md`: Die gemessenen Exitcodes waren
`-1073741819` (0xC0000005, `NullReferenceException`) beziehungsweise `-532462766` bei
`a(5 To 1)`, jeweils mit ausgeschriebener Ausnahme in der Ausgabe des Kindprozesses. Echte
Defekte, kein blockiertes Assembly.

Warum der UDT-Binder überhaupt einen eigenen Falter hat: Ein UDT-Member hat ein festes Layout,
seine Grenzen müssen zur Übersetzungszeit feststehen. Ein gewöhnliches `Dim a(1 To Breite * 2)`
wertet seine Grenzen dagegen zur Laufzeit aus und kommt ohne Falter aus — deshalb lief es die
ganze Zeit korrekt, während dieselbe Schreibweise im `Type` abstürzte.

Zwei Verhaltensänderungen, beide in `UserDefinedTypeDeclarationBinder`:

1. **Der Falter beherrscht, was VB6 an dieser Stelle erlaubt.** Benannte Konstanten unabhängig
   von der Deklarationsreihenfolge — sie werden als Fixpunkt gesammelt, bevor ein Member
   aufgelöst wird, sodass ein `Type` eine weiter unten stehende Konstante verwenden darf und
   eine Konstante sich auf eine weiter unten stehende beziehen darf — sowie `+`, `-`, `*` und
   `\`, verschachtelt und mit `checked`-Überlaufprüfung. Eine Konstante ohne `As`-Typ zählt mit.
2. **Was nicht faltet, wird gemeldet.** Neu `VB6S0071` für eine nicht-konstante Grenze und
   `VB6S0072` für eine Obergrenze unter der Untergrenze. Beide Codes tragen Positivassertions,
   wie die Regel für neue Diagnose-Codes es verlangt.

Gegenproben unverändert: Ein gewöhnliches `Dim` mit demselben Ausdruck läuft weiter, und ein
verschachteltes UDT-Arrayfeld verhält sich wie zuvor.

**Bewusst nicht mitgenommen:** Die Breite eines `String * n` hängt am selben Falter, hat aber
ihre eigene Literal-only-Prüfung — und zwar in **zwei** Pfaden: `BindFixedStringLength` für das
UDT-Member und `ResolveFixedLengthStringType` für den Deklarator. Nur die UDT-Seite zu öffnen
hätte die beiden Deklarationsformen wieder auseinanderlaufen lassen, was beim Fixed-String-Schritt
zuvor gerade bewusst vermieden wurde. `String * Breite` meldet deshalb weiterhin in beiden Formen
`VB6S0043`; die Umstellung beider Stellen auf den Falter ist eine eigene Karte und jetzt
vorbereitet.

Kanonischer Nachweis: `build.ps1 -Configuration Release` misst **1336/1336** Tests, **0** Fehler,
Release ohne Warnungen und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Ein separater
Lauf nur mit der Binder-Änderung, ohne die neuen Tests, blieb vorher bei **1332/1332** grün. Die
Matrixzahlen bleiben bei **68 implemented, 9 partial, 41 planned von 118 | 77/118
documented-verified**.

## Spaet gebundener Zugriff auf oeffentliche Klassenfelder (31.08.2026)

Karte `S1`, Teil A5 — der letzte offene Teil, damit ist `S1` als erste Karte des
Breitendurchgangs vollstaendig und steht auf `implemented`.

Ein spaet gebundenes `o.N` ueber `Dim o As Object` meldete 438, obwohl das oeffentliche
Klassenfeld existierte. Die Ursache liegt an der Naht zwischen Uebersetzung und Laufzeit: Der
Binder modelliert ein `Public`-Feld als Get/Let-Property, der Emitter bildet es aber wieder auf
ein **CLR-Feld** ab. `VBDynamicDispatch` durchsuchte Methoden und Properties — also genau die
beiden Formen, die das Feld zur Laufzeit nicht ist.

Die VB6-Sichtbarkeit steckt dabei bereits im CLR-Attribut: Der Emitter gibt einem `Public`-Feld
`FieldAttributes.Assembly` und einem `Private`-Feld `FieldAttributes.Private`. Die neue
Feldsuche akzeptiert deshalb `!IsPrivate` — ein privates Feld bleibt von aussen unerreichbar,
ohne dass eine zweite Sichtbarkeitsquelle entsteht. Ein Arrayfeld wird ueber `IVBArray`
indiziert; ein falscher Index-Rang meldet Fehler 9, wie der frueh gebundene Pfad auch.

**Ein zweiter Defekt lag darunter und wurde erst nach der ersten Reparatur sichtbar** — zum
vierten Mal in Folge dasselbe Muster. `o.Nums(1) = 7` riss den **Compiler** ab: Der Binder
erzeugte fuer ein indiziertes spaet gebundenes Zuweisungsziel die Aufrufgestalt einer Funktion
(`BoundMemberInvocationExpression`), die als Zuweisungsziel keinen Platz hat, und `LowerPlace`
warf eine `InvalidOperationException`, die als unbehandelte Ausnahme aus dem Emit herausfiel
statt als Diagnose. Verantwortlich war die Bedingung `syntax.Indices.IsEmpty`, die die
Indexform aus dem Property-Zweig ausschloss. Sie ist entfallen; die Indizes gehen jetzt als
Argumente an den Dispatch, den der Lowerer ueber `LowerDynamicSet` bereits bedienen konnte. Der
Defekt war **nicht** feldspezifisch — er traf jedes indizierte spaet gebundene Zuweisungsziel.

Gemessen wurden 11 Faelle, alle korrekt: Lesen und Schreiben eines Long-Feldes, String-Feld,
Objektfeld mit `Set`, indiziertes Arrayfeld, Zugriff ueber `Variant` statt `Object` und ueber
einen `With`-Block.

Gegenproben unveraendert: eine Methode liefert **42**, ein echtes `Property Get` **99**, ein
privates Feld meldet weiterhin **438**, und ein gaenzlich unbekanntes Mitglied ebenfalls
**438**.

Kanonischer Nachweis: `build.ps1 -Configuration Release` misst **1337/1337** Tests, **0**
Fehler, Release ohne Warnungen und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
Matrix bewegt sich erstmals seit dem Breitendurchgang nach oben: **69 implemented, 8 partial,
41 planned von 118 | 77/118 documented-verified**.

Naechste offene Karte ist `S2` (`s2-documented-runtime-error-numbers`).

## Dokumentierte Laufzeitfehlernummern statt des Sammelwerts 5 (31.08.2026)

Karte `S2` (`s2-documented-runtime-error-numbers`), geschlossen. Sie deckt drei Gruppen ab, in
denen VB6 eine bestimmte Nummer vergibt und der Compiler bisher den Sammelwert **5** lieferte.

**B1 — nicht gesetzte Objektvariable meldet 91.** Gemessen wurden vier Wege: Methodenaufruf,
Property-Lesen, explizites `Set c = Nothing` und ein spaet gebundener Zugriff ueber `Object`.
Alle vier lieferten 5, alle vier liefern jetzt 91. Die Zuordnung `NullReferenceException => 91`
ist bewusst breit: Der frueh gebundene Pfad ruft auf null und erzeugt die CLR-Ausnahme, der
spaet gebundene wirft sie in `RequireTarget` selbst, und beide Wege treffen sich in
`VBErrors.Set`. Sie trifft damit auch einen Null-Zugriff, der aus einem Compilerdefekt stammt —
hingenommen, weil VB6 an dieser Stelle 91 meldet und der bisherige Sammelwert 5 denselben
Defekt genauso verdeckt hat, nur mit einer Nummer, die noch weniger aussagt. Der
Regressionslauf ueber 1337 Tests hat keine Verschiebung gezeigt.

**B2 — fehlender Pfad meldet 53.** Das Register nannte `Open` und `FileLen`. Die Messung ueber
die ganze Flaeche hat zwei weitere Faelle gefunden, die **schwerer** waren als der gemeldete
Befund, weil sie ueberhaupt keinen Fehler meldeten:

- `Kill` auf eine fehlende Datei lief still durch, `Err.Number` blieb **0**. Das Loeschen einer
  nicht existierenden Datei galt als Erfolg.
- `FileDateTime` auf eine fehlende Datei lieferte ein **Datum** — `-109205.04`, der
  1601er-Platzhalter von `File.GetLastWriteTime` als OADate.

Ursache ist in beiden Faellen das Framework, nicht der Compiler: `File.Delete` wirft fuer eine
fehlende Datei nicht, `File.GetLastWriteTime` auch nicht. Sie brauchen deshalb eine eigene
Existenzpruefung, die die Framework-Ausnahme wirft — so bleibt die Fehlernummer an genau einer
Stelle definiert, in der Zuordnung in `VBErrors.Set`. `Open` und `FileLen` warfen bereits
`FileNotFoundException`; dort genuegte die neue Zuordnung
`FileNotFoundException or DirectoryNotFoundException => 53`.

**B3 — Collection trennt Index und Schluessel.** Eine Position ausserhalb der Sammlung meldet
jetzt **9** („Subscript out of range"), ein unbekannter Schluessel weiterhin **5**. Der
Schluesselfall war bereits korrekt und hat die dokumentierte Trennung bestaetigt.

Eine Entscheidung ist dabei im Code sichtbar gemacht worden: `ResolveIndex` bedient auch
`Add`s `Before`/`After`. Dort bleibt die Nummer **5**, weil eine Position ausserhalb der
Sammlung bei `Add` ein ungueltiges *Argument* ist und kein Subscript. Der Parameter
`outOfRangeNumber` traegt diese Unterscheidung an der Aufrufstelle, statt sie im Rumpf zu
verstecken.

**Gegenproben unveraendert, als eigener Test festgeschrieben:** `Left(s, -1)`, `Mid(s, 0)`,
`Sqr(-1)` und `Log(0)` melden weiterhin **5**; `CByte(300)` und `CInt("99999")` melden **6**;
ein doppelter `Collection`-Schluessel meldet **457**; ein Array-Subscript meldet **9**;
`CLng(Null)` meldet **94**. Ohne diesen Test sieht jede kuenftige Verschiebung nach 91 oder 53
wie ein Fortschritt aus.

**Methodischer Nachtrag, als Falle festgehalten:** Die beiden schwersten Befunde standen gerade
**nicht** in der Liste der falschen 5. Eine Fehlernummernmessung, die nur bekannte Fehlerfaelle
abfragt, findet die Klasse „meldet ueberhaupt nicht" nie — die **0** gehoert in die Faelle, und
jeder Fall braucht neben „welche Nummer?" auch die Frage „meldet er ueberhaupt?".

Kanonischer Nachweis: `build.ps1 -Configuration Release` misst **1341/1341** Tests, **0**
Fehler, Release ohne Warnungen und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Ein
separater Lauf nur mit den Runtime-Aenderungen, ohne die neuen Tests, blieb vorher bei
**1337/1337** gruen. Die Matrix steht auf **70 implemented, 8 partial, 40 planned von 118 |
78/118 documented-verified**.

Naechste offene Karte ist `S3` (`s3-remaining-standard-intrinsics`).

## Acht Standard-Intrinsics und Resume-Label-Zustand (31.08.2026)

Karte `S3` (`s3-remaining-standard-intrinsics`) ist geschlossen. `StrReverse`,
`FormatNumber`, `FormatCurrency`, `FormatPercent`, `FormatDateTime`, `Partition`,
`CallByName` und `QBColor` sind jetzt als Intrinsics deklariert, ueber den IR-/Managed-Pfad
verbunden und in der Runtime implementiert. Die zugehoerigen VB-Konstanten fuer
Formatoptionen, Datumsformate und `CallByName` sind Teil der eingebauten Sprachumgebung.

`CallByName` verwendet den vorhandenen dynamischen Member-Dispatch fuer Methode, Lesen und
Setzen; `QBColor` bildet die dokumentierte 16-Farben-OLE_COLOR-Tabelle ab. Die Formathelfer
behandeln ihre optionalen Tri-State-Parameter und das ausgewaehlte Kompatibilitaetsprofil.

`Resume <Label>` leert jetzt vor dem Sprung den aktiven Handlerzustand. Ohne aktiven Fehler
meldet es unter `On Error Resume Next` die dokumentierte Fehlernummer **20** und setzt mit der
naechsten Anweisung fort. Der breite Vertrag fuer verschachtelte Aufruf-/Resume-Faelle bleibt
als Karte `l1-02-j` offen.

Kanonischer Nachweis: `build.ps1 -Configuration Release` misst **1351/1351** Tests, **0**
Fehler, Release ohne Warnungen und **40/40** fehlerfrei analysierte VISIA-Projekt-Items. Die
Matrix steht auf **71 implemented, 8 partial, 39 planned von 118 | 79/118
documented-verified**.

Naechste offene Karte ist `l1-02-j` (`l1-02-j-nested-error-resume`).

## Bare Resume verliess eine geschuetzte Region (31.08.2026)

Ein Konstrukt-Sweep ueber 116 generierte Programme hat einen Defekt aus dem S3-Block
gefunden, den weder die Suite noch der VISIA-Korpus sah: Unter `On Error Resume Next`
umschloss der Lowerer auch ein blankes `Resume` beziehungsweise `Resume Next` mit einer
eigenen geschuetzten Region. Beide verlassen die Prozedur ueber den Resume-Dispatch-Switch,
und ein Switch aus einer geschuetzten Region heraus ist kein gueltiges `leave` -- die
emittierte Methode scheiterte an der Verifikation mit `InvalidProgramException` statt zu
laufen. Nur `Resume <Label>` darf die Region tragen; dort endet sie vor dem Sprung.

Die Suite war mit und ohne den Defekt gruen, weil kein Test ein blankes `Resume Next` unter
`On Error Resume Next` ausfuehrte und VISIA die Form nicht enthaelt. `Lower_DoesNotWrap
ABareResumeNextInAProtectedRegion` schliesst die Luecke auf IR-Ebene und ist ohne den Fix rot.

Derselbe Sweep hat drei vorbestehende Luecken protokolliert, die noch offen sind:
`Debug.Print` nimmt nur einen Ausdruck an (`;` und `,` sind Parserfehler), Datumsliterale
`#1/2/2000#` werden nicht geparst, und ein `Property Get`/`Property Set`-Paar gleichen Namens
laesst den Compiler mit einer unbehandelten `ArgumentException` abstuerzen statt zu melden.

Kanonischer Nachweis: **1352/1352** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix bleibt unveraendert bei **71 implemented, 8 partial,
39 planned von 118 | 79/118 documented-verified**.

## Drei Sweep-Befunde: Print-Listen, Datumsliterale, mehrdeutige Namen (31.08.2026)

Der Konstrukt-Sweep hat drei vorbestehende Luecken belegt, die weder die Suite noch der
VISIA-Korpus sah. Alle drei sind geschlossen.

**`Debug.Print` nahm nur einen einzigen Ausdruck.** `Debug.Print a; b`, `Debug.Print a, b` und
selbst ein nachgestelltes `;` waren Parserfehler, obwohl die mehrteilige Form in Legacy-Code die
Regel ist. `Debug.Print` traegt jetzt dieselbe Ausgabeliste wie `Print #`: beliebig viele
Ausdruecke, `;` haengt an, `,` springt in die naechste 14-Spalten-Zone, ein nachgestellter
Separator haelt die Zeile offen, und ein blankes `Debug.Print` gibt eine Leerzeile. Die
Zahlenformatierung selbst bleibt unveraendert -- weiterhin nur das fuehrende
Vorzeichen-Leerzeichen, kein nachgestelltes.

**Datumsliterale `#1/2/2000#` wurden ueberhaupt nicht geparst.** Kein Test im Repo verwendete
eins, und VISIA enthaelt keins. Der Lexer erkennt sie jetzt und loest sie zur Lexzeit in ein
OLE-Automation-Datum auf; ab da ist es eine gewoehnliche Date-Konstante. Der `#` bleibt
mehrdeutig, deshalb entscheidet der Lexer erst, wenn ein schliessendes `#` auf derselben Zeile
steht und der Text dazwischen als Datum oder Zeit parst -- `Print #1, "a#b#c"` und `5# - 2#`
bleiben unberuehrt. Gelesen wird invariant, weil VB6-Datumsliterale unabhaengig vom Gebietsschema
in US-Reihenfolge stehen.

**Eine `Function` oder `Property Get`, deren Name case-insensitiv mit einer Modulvariablen
kollidiert, liess den Compiler abstuerzen** -- unbehandelte `ArgumentException` statt Diagnose,
weil der Funktionsname als eigener Rueckgabespeicher in denselben Scope kommt. VB6 meldet hier
"Ambiguous name detected"; der Binder meldet jetzt **VB6S0073**.

Der Sweep laeuft danach mit **112 von 116** Faellen durch statt 86, bei 2 statt 28 Ablehnungen.

Zwei weitere vorbestehende Defekte sind dabei sichtbar geworden und bleiben offen: `Date + Integer`
bricht mit einer `OverflowException` ab statt Datumsarithmetik zu rechnen, und `Debug.Print` eines
Date-Wertes gibt die rohe OADate-Zahl aus statt eines Datums.

Kanonischer Nachweis: **1370/1370** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Date-Arithmetik lief ueber den Integer-Ruecksprung (31.08.2026)

`Date + 5` brach mit einer `OverflowException` ab. Der Binder behandelte `Date` nicht als
arithmetischen Operanden, deshalb fiel das Operandenpaar in den Ruecksprung `TypeSymbol.Integer`
und der OADate-Wert wurde nach `Integer` konvertiert -- fuer jedes reale Datum ein Ueberlauf.
Bei kleineren Werten haette derselbe Pfad **still falsch** gerechnet statt abzustuerzen.

`Date` nimmt jetzt an `+`, `-` und `*` teil; gerechnet wird auf dem Double. Der Ergebnis-Subtyp
folgt dem Vertrag, den der Variant-Pfad in
`EmitManagedApplication_PreservesDateSubtypeThroughVariantArithmetic` bereits festschreibt:
Addieren und Subtrahieren einer Zahl behaelt `Date`, die Differenz zweier `Date` ist `Double`,
und `*` ist `Double`. Der bestehende Variant-Test bleibt unveraendert gruen.

Dabei kam eine zweite, bis dahin unerreichbare Inkonsistenz zum Vorschein: `AddMethod` im
Lowerer fuehrte `Date` bereits auf `AddDouble`, `SubtractMethod` nicht -- der Date-Fall landete
auf `SubtractInteger` und der Emitter suchte eine Ueberladung mit zwei Doubles unter einem
Integer-Namen (`VB6E0003`). Die Zeile ist jetzt an `AddMethod` angeglichen.

Kanonischer Nachweis: **1376/1376** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Ein Date wurde als Seriennummer gedruckt (01.09.2026)

`Debug.Print` eines Date-Wertes gab die rohe OADate-Zahl aus -- `43832` statt eines Datums.
Betroffen war nicht nur `Debug.Print`: die Darstellung haengt an `VBDebug.Format`, das auch
`Print #` bedient, und der typisierte `Date` erreichte sie ueberhaupt nur als blanker `Double`,
weil er sich die Repraesentation mit ihm teilt.

Zwei Aenderungen: Ein Date-Ausgabeelement wird jetzt als Date-Wert uebergeben statt als roher
Double, und `VBDebug.Format` rendert einen Date-Wert in der dokumentierten **General
Date**-Form -- nur Datum, solange keine Tageszeit vorhanden ist, sonst Datum und Zeit. Die
Formatmaschine dafuer war vorhanden und getestet; im deterministischen Profil bleibt die
Ausgabe invariant.

**Zwei bestehende Zusicherungen wurden dabei angepasst**, was §12 sonst verbietet:
`EmitManagedApplication_UsesVariantStateForInt` las `43832` und
`EmitManagedApplication_PassesSelectedProfileToDateTimeIntrinsics` las `43834`. Beide Namen
sprechen keine Zusage ueber Datumsdarstellung aus -- der eine prueft den Variant-Zustand von
`Int`, der andere das Durchreichen des Profils; die Zahl war dort Ableseform, nicht Vertrag.
Damit das nicht wieder als Nebenwirkung haengt, sagt `DateDisplayExecutionTests` die
Darstellung jetzt im Namen zu.

Kanonischer Nachweis: **1381/1381** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Formulargroesse aus dem Designer (01.09.2026)

Der VISIA-Korpus wurde erstmals nicht nur analysiert und emittiert, sondern **gestartet**. Das
Programm laeuft, stuerzt nicht ab und zeigt `frmSplash` als echtes Fenster -- die Kette traegt
also durchgehend. Das Fenster war aber **300x300**, die WinForms-Standardgroesse, statt der
544x352, die der Designer mit `ClientWidth = 8160` und `ClientHeight = 5280` Twips vorgibt.

Ursache: Ein VB6-Formular schreibt seine Groesse **nie** als `Width`/`Height` auf Formularebene,
sondern immer als `ClientWidth`/`ClientHeight`. Die Whitelist unterstuetzter Designer-
Eigenschaften kannte nur die erste Form -- also genau die, die Formulare nicht verwenden. Alle
sechs VISIA-Formulare fuehren `ClientWidth`/`ClientHeight` und keines `Width`/`Height`.

Beide Namen sind jetzt in der Whitelist, der Host setzt sie auf `ClientSize` und liest sie von
dort zurueck -- dieselbe Flaeche, die `ScaleWidth`/`ScaleHeight` bereits bedienen. Gemessen:
`frmSplash` rendert mit 544x352, ein minimales Testformular ebenso.

Beim Nachmessen fiel eine Falle des Emissionspfads auf: `FindWinFormsRuntimeAssembly` durchsucht
`src/VB6.Runtime.WinForms/bin` **rekursiv** und nimmt den ersten Treffer, was hier eine Woche
alte Debug-Kopie war. Eine Host-Aenderung wirkt dadurch scheinbar nicht, obwohl sie uebersetzt
ist. Wer am Host misst, prueft den Zeitstempel der kopierten DLL.

Offen und separat: Ein unqualifiziertes `ScaleWidth` innerhalb von `Form_Load` liefert einen
sinnlosen Wert, der sich von Lauf zu Lauf aendert.

Kanonischer Nachweis: **1382/1382** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Spaet gebundene Zahlen lieferten die Boxadresse (01.09.2026)

`Me.ScaleWidth` in `Form_Load` antwortete mit Werten wie `89723312`, die sich bei jedem Zugriff
und bei jedem Programmlauf aenderten. `Caption` war dabei korrekt, alle numerischen
Eigenschaften falsch -- ein stilles falsches Ergebnis ohne jede Diagnose.

Eine Ablaufverfolgung im Dispatch zeigte, dass der Host durchgaengig den richtigen Wert liefert
(`[disp] ScaleWidth -> HOST = 8160`). Der Defekt sitzt dahinter: `VBDynamicDispatch.GetMember`
gibt `object` zurueck, waehrend der gebundene Baum den Membertyp bereits kennt. Der Lowerer
typisierte die Runtime-Call deshalb direkt als `Long`, und der Emitter liess den geboxten
Verweis auf dem Stack stehen, wo eine Zahl erwartet wurde. Gelesen wurde die **Adresse der Box** --
daher die monoton wachsenden Werte, ein frisch allozierter Kasten pro Zugriff. Strings blieben
richtig, weil dort die Referenz der Wert ist.

Ein dynamischer Memberzugriff mit numerischem Zieltyp bleibt jetzt als `Variant` typisiert und
traegt eine ausdrueckliche Konvertierung. Gemessen an einem Formular: `ScaleWidth` 8160,
`ScaleHeight` 5280, `Width` 8400 (Aussenmass inklusive Rahmen), `Caption` unveraendert -- sowohl
unqualifiziert als auch ueber `Me.`.

Kanonischer Nachweis: **1383/1383** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Die WinForms-Companion-DLL wurde nichtdeterministisch gewaehlt (01.09.2026)

Beim Messen der Formulargroesse wirkte eine uebersetzte Host-Aenderung scheinbar nicht: das
emittierte Programm bekam eine eine Woche alte Debug-Kopie von
`VB6.Runtime.WinForms.dll`, obwohl der frische Release-Stand danebenlag.

`FindWinFormsRuntimeAssembly` sammelte alle Kandidaten -- die Kopie neben der geladenen Runtime,
`AppContext.BaseDirectory` und alles unter `src/VB6.Runtime.WinForms/bin` rekursiv -- und
sortierte sie ausschliesslich danach, ob der Ordner `net10.0-windows` heisst. Damit wurde
ausgerechnet die **richtige** Kopie degradiert: sie liegt neben der Compiler-Ausgabe, und deren
Ordner heisst `net10.0`. Innerhalb gleicher Sortierschluessel entschied die Aufzaehlungsreihenfolge
des Verzeichnisses, also `Debug` vor `Release`.

Die Aufloesung ist jetzt explizit: Liegt die Companion-DLL neben der geladenen Runtime oder im
Basisverzeichnis, gewinnt sie sofort -- die beiden muessen ohnehin zusammenpassen. Erst danach
greift der Baum-Fallback, und der ordnet nach Konfiguration der geladenen Runtime, dann
Zielframework, dann juengstem Stand.

Gegenprobe: mit einem kuenstlich auf 2030 datierten Debug-Artefakt waehlt der Resolver weiterhin
den Release-Stand; `EmitAssembly_CopiesTheWinFormsCompanionOfThisBuild` ist ohne den Fix rot.

Kanonischer Nachweis: **1384/1384** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Die LUNA-Dokumente sind entfallen (01.09.2026)

`LUNA_GUARDRAILS.md`, `LUNA_EXECUTION_PLAN.md` und `LUNA_WORKORDER_Q.md` sind entfernt. Alle
lebenden Verweise darauf in `CLAUDE.md`, `docs/ROADMAP.md` und `README.md` zeigen jetzt auf die
Kompatibilitaetsmatrix, die die Karten-IDs und beide Statusachsen ohnehin fuehrt. Drei
Paragrafenverweise in `CLAUDE.md` ("§1", "§11", "§12") standen ohne Ziel da und sind zu
eigenstaendigen Saetzen gemacht; die Regeln selbst bleiben damit erhalten.

Was mit den Dateien verschwunden ist und nirgends sonst steht: der Arbeitskartenvertrag, die
Wellenreihenfolge L0-L7, der Testtakt und das Befundregister des Breitendurchgangs vom
30.08.2026. Die Karten-IDs und ihr Status stehen weiterhin in der Matrix, die Historie im
Changelog.

Bei der Gelegenheit sind drei Luecken in der README-Merkmalsliste geschlossen worden, die die
Sweep-Arbeit der letzten zwei Tage nicht nachgezogen hatte: die `Debug.Print`-Ausgabeliste, die
`#...#`-Datumsliterale und die Uebernahme der Designer-Formulargroesse.

Kanonischer Nachweis unveraendert: **1384/1384** Tests, **0** Fehler, Release ohne Warnungen,
**40/40** VISIA-Projektitems.

## CStr eines Date lieferte die Seriennummer (01.09.2026)

`CStr(aDate)` gab `43832` zurueck statt eines Datums, ebenso die Verkettung `"am " & d`, die
Zuweisung an einen `String` und `Print #`. Nach der Debug.Print-Aenderung war das der letzte
Textweg, der einer eigenen Darstellung folgte.

Zwei Ursachen, beide dieselbe wie zuvor bei `Debug.Print`. `VBConversions.CStr` rendert einen
`VBDateValue` ausdruecklich als OADate-Zahl -- das ist auf die dokumentierte **General
Date**-Form umgestellt. Und ein typisierter `Date` erreichte `CStr` ueberhaupt nur als blanker
`Double`, weil er sich die Repraesentation mit ihm teilt; die Konvertierung nach `String` boxt
ihn jetzt als Date-Wert. Aus demselben Grund laufen die Ausgabeelemente von `Print #` nun ueber
denselben Helfer wie die von `Debug.Print`.

Damit stimmen alle fuenf Textwege ueberein: `CStr`, `&`, Zuweisung an `String`, `Debug.Print`
und `Print #` liefern `2020-01-02` beziehungsweise `2020-01-02 18:00:00`. Eine ausdrueckliche
numerische Konvertierung bleibt unveraendert: `CDbl` liefert weiterhin `43832`, `Year` liefert
`2020`.

Die befuerchtete Breitenwirkung trat nicht ein: **kein einziger bestehender Test** sicherte die
Seriennummer als Textform zu, der Lauf blieb ohne Anpassung fremder Zusicherungen gruen.

Kanonischer Nachweis: **1388/1388** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Matrix unveraendert bei **71 implemented, 8 partial, 39 planned von 118 |
79/118 documented-verified**.

## Karte l1-02-j: verschachtelte Handler und alle Resume-Formen (01.09.2026)

Zuerst gemessen, dann gebaut. Eine Probenreihe ueber die Vertragsflaeche der Karte --
Verschachtelung, jede `Resume`-Form, `Err`/`Erl`-Zustand -- ergab **15 von 16 Faellen bereits
korrekt**. Zwei vermeintliche Befunde waren keine: das blanke `Resume` wiederholt die fehlerhafte
Anweisung richtig, meine erste Probe hatte den Zaehler nur vor der wiederholten Zeile stehen und
lief deshalb selbst endlos.

Der eine echte Defekt: `Resume` und `Resume Next` ohne aktiven Fehler brachen das Programm mit
einer unbehandelten 20 ab, auch wenn ein `On Error Resume Next` daneben stand. Ursache ist die
Form des Dispatch -- er ist ein `switch` in die Statement-Fortsetzungen, die **ausserhalb** jeder
geschuetzten Region liegen. Ein Sprung dorthin aus einem `try` heraus verifiziert nicht; genau
daran war der frueher zurueckgenommene Versuch gescheitert, die Anweisung einzuwickeln.

Deshalb wird jetzt nicht mehr geworfen, sondern vorher gefragt: `VBErrors.HasActiveResume`
entscheidet, und ohne aktiven Fehler traegt `RecordResumeWithoutError` die dokumentierte **20**
in `Err` ein, worauf die Methode zur naechsten Anweisung durchfaellt. Eine Prozedur ganz ohne
geschuetzte Region hat kein Ziel zum Fortsetzen -- dort bleibt es bei der geworfenen 20.

Gemessen: unter `On Error Resume Next` melden `Resume` und `Resume Next` jetzt `err=20` und
laufen weiter; ohne jedes `On Error` haelt das Programm an; `Resume <Label>`, gewoehnliches
`Resume Next` und die Wiederholung sind unveraendert. Die Probenreihe laeuft mit **16 von 16**
ohne Befund durch.

Die Karte steht auf **`partial`** und nicht auf `implemented`: offen bleibt, ob `Err` geleert
sein muss, wenn ein Handler ueber `End Sub` verlaesst statt ueber `Exit Sub` oder `Resume`.
Gemessen wird der Wert behalten. Die Dokumentation nennt `Exit Sub` ausdruecklich und `End Sub`
nicht; ohne Orakel wird das nicht entschieden.

Kanonischer Nachweis: **1399/1399** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **71 implemented, 9 partial, 38 planned von 118 |
80/118 documented-verified**.

## Karte l1-02-k: fehlende Standard-Intrinsics, erster Durchgang (01.09.2026)

Gemessen statt geschaetzt: 171 Intrinsics sind deklariert, und ein Bindungstest ueber die
dokumentierte Kartenflaeche fand **sechs** Namen, die nicht binden. Die `$`-Varianten
(`Left$`, `Format$`, `Str$`, `Hex$`, `Date$`, `Time$`), `LBound`/`UBound` und `CVDate` binden
entgegen der Vermutung alle bereits.

Drei davon sind jetzt geschlossen:

- **`AscB`** und **`ChrB`** vervollstaendigen die Byte-String-Familie, die `LeftB`, `RightB`,
  `MidB`, `InStrB` und `LenB` bereits bilden. Sie arbeiten auf derselben profilabhaengigen
  Byte-Sicht; im deterministischen Profil ist `Len(ChrB(65))` = 1 und `LenB(ChrB(65))` = 2.
  Ein leeres Argument und ein Code ausserhalb 0-255 melden **5**.
- **`CLngLng`** erreicht die LongLong-Konvertierung, die es in der Runtime und im IR laengst
  gab, waehrend kein VB6-Quelltext sie benennen konnte -- `LongLong` war ein deklarierter Typ
  ohne zugehoerige Konvertierungsfunktion.

Drei bleiben offen und halten die Karte auf `partial`:

- **`Error`** und **`Error$`** binden nicht; die `Error`-Anweisung (`Error 5`) ist ebenfalls ein
  Parserfehler. Beides braucht Parserarbeit, weil `Error` heute nur als Kontextwort in
  `On Error` existiert.
- **`Tab(n)`** und **`Spc(n)`** binden nicht. Sie gehoeren in die Ausgabelisten von `Print #`
  und `Debug.Print` und brauchen deshalb eine Anbindung an die Ausgabelistenmechanik, nicht nur
  einen Tabelleneintrag.

Kanonischer Nachweis: **1403/1403** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **71 implemented, 10 partial, 37 planned von 118 |
81/118 documented-verified**.

## Karte l1-02-k geschlossen: Error, Tab und Spc (01.09.2026)

Die drei verbliebenen Namen der Karte binden jetzt.

**`Error` und `Error$`** waren nicht ansprechbar, weil `ERROR` ein Keyword ist -- `On Error`
braucht es. Beide Formen bekommen deshalb ihren eigenen Weg: die **Anweisung** `Error <Nummer>`
ist ein eigener Syntaxknoten, der ueber `VBErrors.RaiseNumber` mit der dokumentierten
Beschreibung ausloest; die **Funktion** `Error(n)` wird im Ausdruckspfad als Aufruf gebaut, nicht
als Name, den die Element-Zugriffsschleife sonst in einen Index verwandelt haette. Der Lexer
nimmt fuer das Keyword jetzt auch ein Typsuffix an, damit `Error$` ueberhaupt lexbar ist.

`VBErrors.ErrorText` liefert die dokumentierten Meldungen; eine Nummer, die VB6 nicht
dokumentiert, ergibt wie dort "Application-defined or object-defined error".

**`Tab(n)` und `Spc(n)`** erzeugen keinen Wert, sondern positionieren das naechste Element. Sie
reisen als `VBPrintPosition`-Marker durch die Ausgabeliste, den beide Print-Wege aufloesen --
`Debug.Print` und `Print #` verhalten sich damit gleich. Bekannte Abweichung: steht die
Tab-Spalte bereits hinter der aktuellen Position, beginnt VB6 eine neue Zeile, waehrend hier
ohne Auffuellung weitergeschrieben wird.

Gemessen: `Error(5)` = "Invalid procedure call or argument", `Error$(53)` = "File not found",
`Error 53` setzt `Err.Number` 53 mit derselben Beschreibung, `Error 6` erreicht einen Handler,
`"a"; Tab(10); "b"` setzt das b auf Spalte 10, `"a"; Spc(3); "b"` ergibt `a   b`, und
`Print #` positioniert identisch.

Die Karte steht damit auf **`implemented`** / `documented-verified`.

Kanonischer Nachweis: **1410/1410** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **72 implemented, 9 partial, 37 planned von 118 |
81/118 documented-verified**.

## Karte l1-02-l geschlossen: Locale, Date/Time, Math und Financial (01.09.2026)

Die Karte war im Managed-Pfad bereits vollständig verdrahtet, aber noch als `planned` geführt.
Die gezielte Messung deckt die vier karteneigenen Testdateien ab: Locale-/Profilgrenzen für
`DateValue`, `TimeValue`, Datumsnamen und `Format`; deterministische Rundungs-, Bereichs- und
Random-Verträge der Math-Intrinsics; sowie alle unterstützten Annuitäten-, Cashflow- und
Abschreibungsfunktionen. Die Financial-Oberfläche wird zusätzlich durch einen emittierten
Managed-VB6-Prozess geprüft.

Gemessen: **36 Runtime- und 14 Managed-End-to-End-Tests**, alle grün. Der Status lautet damit
**`implemented`** / `documented-verified`; ein VB6-SP6-Orakel ist weiterhin nicht installiert.
Die nächste offene Managed-Karte ist `l1-02-m-headless-host-services`.

Kanonischer Nachweis: **1410/1410** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **73 implemented, 9 partial, 36 planned von 118 |
82/118 documented-verified**.

## Karte l1-02-m: explizite Headless-Interaktionsdienste (01.09.2026)

`MsgBox`, `InputBox` und die Settings-API besaßen einen dokumentierten deterministischen
Headless-Fallback, konnten aber nicht über den vorhandenen `IVB6Host` ersetzt werden. Das ist
jetzt ein echter Hostvertrag: `TryShowMessageBox`, `TryShowInputBox`, `TryGetSetting` und
`TrySaveSetting` erlauben einer Anwendung, die Dienste explizit bereitzustellen. Gibt ein Host
einen Dienst nicht an, bleibt das bisherige Verhalten erhalten — MessageBox-Standardantwort,
InputBox-Default und prozesslokaler, case-insensitiver Settings-Speicher.

Gemessen: Ein Testhost liefert eine eigene Dialogantwort und persistiert Settings, während die
emittierten `MsgBox`-/`InputBox`- und Standardbibliotheksprogramme weiterhin ohne interaktiven
Desktop durchlaufen. **23 Runtime- und 21 Managed-End-to-End-Tests** sind grün.

Die Karte steht auf **`partial`** / `documented-verified`: `Screen`, `Printer` und die
vollständige Clipboard-Oberfläche brauchen weiterhin eigene, explizite Hostverträge.

Kanonischer Nachweis: **1411/1411** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **73 implemented, 10 partial, 35 planned von 118 |
83/118 documented-verified**.

## Karte l1-02-m: vollständiger Registry-Vertrag (01.09.2026)

Die Settings-Familie deckt jetzt neben `GetSetting`/`SaveSetting` auch `DeleteSetting` und
`GetAllSettings` ab. Die neuen Intrinsics durchlaufen Symboltabelle, Binder, IR und Managed-
Emitter bis zu `VBInteraction`. Der explizite `IVB6Host`-Vertrag erhält passende Delete- und
Enumerations-Hooks; ohne Host bleibt ein prozesslokaler, case-insensitiver Speicher aktiv.
`DeleteSetting` löscht gezielt einen Schlüssel, einen Bereich oder einen Anwendungseintrag und
meldet einen nicht vorhandenen Löschbereich als deterministischen Laufzeitfehler. `GetAllSettings`
liefert die dokumentierte zweidimensionale Key/Value-Variant-Matrix und bei nicht vorhandener
Anwendung oder Bereich einen uninitialisierten Variantwert.

Gemessen: Die `InteractionRuntimeTests` prüfen Rang, Bounds, Ordnung, Schlüssel-/Bereichs- und
Anwendungs-Löschung sowie die Host-Delegation. Ein emittiertes Managed-Programm prüft dieselben
API-Aufrufe durch den vollständigen Compilerpfad. **24 Runtime- und 16 Managed-End-to-End-Tests**
sind grün.

Die Karte bleibt **`partial`** / `documented-verified`: Registry ist geschlossen, während
`Screen`, `Printer` und die vollständige Clipboard-Oberfläche weiterhin eigene Hostverträge
benötigen.

Kanonischer Nachweis: **1413/1413** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **73 implemented, 10 partial, 35 planned von 118 |
83/118 documented-verified**.

## Karte l1-02-m: Clipboard-Grundvertrag (01.09.2026)

Der globale `Clipboard` besitzt jetzt seine deklarierte Kernoberfläche `Clear`, `GetData`,
`GetFormat`, `GetText`, `SetData` und `SetText` einschließlich der VB6-Formatkonstanten. Sie
bindet nicht mehr als untypisiertes Late-Binding-Objekt, sondern wird über explizite IR-Operationen
in die Managed-Runtime gesenkt. `IVB6Host` enthält dafür je eigene Read-/Write-/Format-/Clear-
Hooks; `WinFormsHost` reicht Text- und unterstützte Datenformate an die Windows-Zwischenablage
weiter. Ohne Desktop sorgt der Runtime-Speicher für eine reproduzierbare Mehrformat-Sicht und
`Clear` entfernt alle Formate.

Gemessen: Die Runtime prüft Text-, RTF- und Datenformate, Leerung und Host-Delegation. Ein
emittiertes Managed-Programm prüft Syntax, Standardkonstanten, IR, Text-/Daten-Rundlauf,
Formatabfrage und Leerung. **25 Runtime- und 17 Managed-End-to-End-Tests** sind grün.

Die Karte bleibt **`partial`** / `documented-verified`: Registry und Clipboard sind geschlossen;
die ausstehenden expliziten Hostoberflächen sind `Screen` und `Printer`.

Kanonischer Nachweis: **1415/1415** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **73 implemented, 10 partial, 35 planned von 118 |
83/118 documented-verified**.

## Karte l1-02-g: Variant-Promotion und Objektfehlergrenze geschlossen (01.09.2026)

Die vorhandene Promotionstabelle für Numeric-, Boolean-, String-, Date- und Empty-Variants
war bereits breit getestet. Offen war allein der Fehlerpfad für Objekte, deren Default-Member
ohne Argument keinen skalaren Wert liefern kann: `Collection.Item` ist parameterpflichtig.
`+` meldete dafür schon Fehler 13, `&` ließ den CLR-Objekttext durch und `=` fiel in einen
falschen Vergleichsfehler.

Nach der Default-Member-Auflösung prüfen nun alle dynamischen arithmetischen, logischen,
Vergleichs- und Verkettungsoperatoren diesen Restfall einheitlich als VB6-Fehler 13. Ein Objekt
mit skalarem Default-Member bleibt weiterhin gültig; `Nothing` behält seinen eigenen
Objektzustandspfad. Die Runtime-Regression deckt 14 Operatorfamilien mit `Collection` ab, und
ein emittiertes Programm misst für `+`, `&` und `=` jeweils `Err.Number = 13`.

Damit ist `l1-02-g-variant-promotion-table` **`implemented`** /
`documented-verified`. Der umfangreichere Vertrag für Objektidentität, Variant-Arrays und
Automation-Dispatch verbleibt auf `l1-02-h-variant-object-array-dispatch`.

Kanonischer Nachweis: **1430/1430** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **83 implemented, 10 partial, 25 planned von 118 |
93/118 documented-verified**.

## Karte l1-02-h: ByRef-Write-back für indizierte Variantwerte (01.09.2026)

Ein indizierter Variantwert wurde bisher für `ByRef` als CLR-Adresse angefordert. Das funktioniert
nur für den internen `VBArray<Variant>`-Speicher. Eine schreibbare Default-Property und ein
CLR-/SAFEARRAY-Wert haben keine solche Adresse; der Aufruf brach vor dem Callee mit „Variant does
not contain an array“ ab.

Der Lowerer hält Empfänger, Indizes und Elementwert nun jeweils in Compiler-Temporaries. Der
Callee erhält den Wert als echte ByRef-Variable, und der Managed-Emitter schreibt ihn nach der
Rückkehr über den regulären dynamischen Elementpfad zurück. Damit bleiben Arraygrenzen,
Elementkonversion und schreibbare Default-Member einheitlich. Die temporären Indizes bewahren
zudem die einmalige Auswertung bei Seiteneffekten; Funktionsrückgabewerte werden vor dem
Write-back gesichert und danach wieder bereitgestellt.

Die neue Projekt-Regression schreibt durch eine Variant-Default-Property zurück, prüft einen
einmal ausgeführten Indexausdruck und den erhaltenen Funktionsrückgabewert. Die Karte bleibt
**`partial`** / `documented-verified`: Native SAFEARRAY-Descriptoren mit UDT-/Pointer-Elementen
und weitere COM-ABI-Formen gehören weiterhin zum separaten Interop-Vertrag.

Kanonischer Nachweis: **1431/1431** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix bleibt bei **83 implemented, 10 partial, 25 planned von 118 |
93/118 documented-verified**.

## Karte l1-02-n: Profilbewusste sequentielle Datei-Textkanäle (01.09.2026)

`Print #`, `Write #`, `Line Input #`, `Input #` und die `Input`-Funktion erhalten im
Managed-Emitter nun das gewählte Kompatibilitätsprofil als verstecktes Laufzeitargument.
`Deterministic` behält die bisherige stabile UTF-8-Übertragung einschließlich UTF-8-BOM-Behandlung;
`VB6Sp6` kodiert und dekodiert sequentielle Textdateien über die aktive Windows-ANSI-Codepage.
Damit stimmen Textdateien mit den bereits profilbewussten `LenB`-/`Asc`-/`Chr`-Pfaden überein,
ohne Binär-Stringtransfers oder Fixed-String-UDT-Layouts zu verändern.

Die neue Runtime-Regression misst die exakten Ausgabebytes und den Rückweg über `Line Input #`
und `Input #`; ein emittiertes VB6Sp6-Programm prüft die verdeckte Profilweitergabe für
`Print #`, `Write #`, `Line Input #` und `Input #`. Der vollständige Texttransfer-Teilvertrag
`l1-03-f-file-text-transfer-codepage` ist damit **`implemented`** /
`documented-verified`; die breite Restkarte `l1-02-n-file-io-remaining` ist
**`partial`**. Variant-Arrays/-Objekte und komplexere Random-/UDT-Recordlayouts bleiben bewusst
offen.

Kanonischer Nachweis: **1421/1421** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **74 implemented, 11 partial, 33 planned von 118 |
85/118 documented-verified**.

## Karte l1-03-g: Skalare und UDT-Dateirecords verifiziert (01.09.2026)

Die Matrixkarte für skalare Binary-/Random-Recordlayouts war noch als `planned` geführt, obwohl
der Implementierungspfad bereits vollständig vorhanden ist. Die gezielte Prüfung über
`FileIoExecutionTests` und `FixedLengthStringUdtExecutionTests` umfasst Feldreihenfolge,
`Integer`/`Long`/`Boolean`/`Date`/`Currency`, Random-Recordgrenzen und `Len`, verschachtelte UDTs
sowie gepaddete `String * n`-Felder. Alle **50** zugehörigen Managed-Tests sind grün.

Die Karte `l1-03-g-file-scalar-udt-record-layout` lautet damit **`implemented`** /
`documented-verified`; Variant-Arrays/-Objekte und zusammengesetzte Random-Recordlayouts bleiben
separate offene Teilverträge.

Kanonischer Nachweis bleibt **1421/1421** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **75 implemented, 11 partial, 32 planned von 118 |
86/118 documented-verified**.

## Karte l1-03-h: Datei-Array-Descriptoren und Bounds verifiziert (01.09.2026)

Die gezielte Prüfung der bereits vorhandenen Array-Dateipfade deckt die gesamte Kartenoberfläche
ab: feste und dynamische Scalar-, `String`- und UDT-Arrays übertragen ihre Elemente; Rang,
Unter- und Obergrenzen bleiben erhalten; Binary schreibt keine äußere Descriptor-Hülle, Random
trägt sie für dynamische Top-Level-Arrays. Ein uninitialisierter dynamischer UDT-Member bleibt
dabei uninitialisiert. Runtime-, Array-IR- und Managed-E2E-Regressionen sind in den drei
karteneigenen Testdateien zusammen **61**-mal grün.

`l1-03-h-file-array-descriptor-bounds` lautet damit **`implemented`** /
`documented-verified`. Nicht Teil dieses Vertrags sind Variant-Werte, deren Inhalt selbst ein
Array oder Objekt ist; diese bleiben explizit bei der Composite-Variant-Karte offen.

Kanonischer Nachweis bleibt **1421/1421** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **76 implemented, 11 partial, 31 planned von 118 |
87/118 documented-verified**.

## Karten l1-03-d/e: Open- und Positionsverträge verifiziert (01.09.2026)

Die zuvor noch geplanten Datei-Karten für `Open` sowie `Seek`/`EOF`/`LOF`/`Loc` waren bereits
durchgängig implementiert: Die Syntax bewahrt Access- und Sharing-Klauseln, die Runtime setzt
die passenden `FileAccess`-/`FileShare`-Regeln und der fehlende `For`-Modus wählt Random mit
Standardlänge 128. Datei-Positionen bleiben 1-basiert; `Loc` liefert Byte-, Datensatz- oder
Sequential-Blockeinheiten je nach Kanalmodus.

`FileStatementParserTests` (17) und `FileIoExecutionTests` (37) sind gezielt grün; die
Runtime-Regressionen decken die Streamgrenzen, Moduseinheiten und Zugangsbeschränkungen ab.
Damit lauten `l1-03-d-file-open-modes-access-sharing` und
`l1-03-e-file-position-functions` jeweils **`implemented`** /
`documented-verified`.

Kanonischer Nachweis bleibt **1421/1421** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **78 implemented, 11 partial, 29 planned von 118 |
89/118 documented-verified**.

## Karte l1-03-i: Variant-/Objekt-Dateilayouts abgegrenzt (01.09.2026)

Binäre Scalar-Variants tragen weiterhin Typ-Tag und Payload. Für den noch nicht implementierten
Composite-Teil ist der Fehlervertrag jetzt gezielt gesichert: Variant-Werte mit Array, nicht
serialisierbare Objektvarianten sowie eingehende SAFEARRAY-Tags führen zu einem expliziten
VB-Typfehler; sie werden weder als Text noch als flaches Array übertragen. Die neue Runtime-
Regression prüft alle drei Grenzen. Damit ist
`l1-03-i-file-variant-object-composite-layout` **`partial`** /
`documented-verified`; die vollständige SAFEARRAY-/COM-Besitzsemantik und komplexe
Variant-/UDT-Recordlayouts bleiben offen.

Kanonischer Nachweis: **1423/1423** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **78 implemented, 12 partial, 28 planned von 118 |
90/118 documented-verified**.

## Karte l1-03-a: Projektarten, Startobjekt und Bitness verifiziert (01.09.2026)

Die VBP-Karte war im Code bereits weiter als ihr Matrixstatus: `Exe` sowie `OleDll`, `OleExe`,
`Control`, `Dll`, `ActiveX DLL`, `ActiveX EXE` und `ActiveX Control` werden als Anwendung oder
Bibliothek klassifiziert. Bibliotheken benötigen kein `Sub Main`; EXE-Projekte verwenden
`Sub Main` oder eine deklarierte Form als Startobjekt. Ausgabename und -endung folgen den
Projektmetadaten, und CLI/SDK wählen für Legacyprojekte deterministisch x86, sofern keine
Plattform angegeben ist.

Die neue Regression prüft alle sieben unterstützten Bibliothekstypen ohne künstlichen Entry Point.
Sie ergänzt die Form-/Ausgabe-/Plattform-CLI-Tests und die Projektkompilierungsfälle. Die Karte
`l1-03-a-project-kinds-startup-bitness` ist damit **`implemented`** /
`documented-verified`; Binary Compatibility, vollständige Ressourcen/Komponenten und breitere
Projektgruppenabhängigkeiten bleiben getrennt offen.

Kanonischer Nachweis: **1424/1424** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **79 implemented, 12 partial, 27 planned von 118 |
91/118 documented-verified**.

## Karte l1-03-b: Projektgruppen-Abhängigkeiten und Zyklen verifiziert (01.09.2026)

VBG-Projektgruppen lösen deklarierte VBP-Referenzen innerhalb der Gruppe auf und emittieren die
Abhängigkeitsclosure vor ihren Verbrauchern. Neu prüft die Gruppenanalyse Referenzzyklen
deterministisch und meldet sie als `VB6VBG0009` einschließlich der zyklischen Projektfolge. Eine
fehlerhafte Gruppe bricht die Emission vor dem Anlegen des Ausgabeverzeichnisses ab, so dass keine
Teil-Artefakte entstehen.

Die neue Regression erzeugt `First.vbp -> Second.vbp -> First.vbp`, sichert die stabile
Gruppendiagnose und bestätigt die artefaktfreie Emissionsgrenze. Zusammen mit den vorhandenen
Loader- und Abhängigkeitsreihenfolge-Tests ist
`l1-03-b-project-group-dependency-order` damit **`implemented`** /
`documented-verified`; Binary Compatibility, Ressourcen und Komponenten bleiben auf der folgenden
Projektkarte offen.

Kanonischer Nachweis: **1425/1425** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **80 implemented, 12 partial, 26 planned von 118 |
92/118 documented-verified**.

## Karte l1-03-c: Projektmetadaten und exakte Eingaben verifiziert (01.09.2026)

VBP-Version und Binary-Compatibility-Einstellungen bleiben unverändert als benannte
Projektmetadaten adressierbar. Der deterministische CLI-Eingabemanifestpfad beschränkt sich auf
deklarierte Quellen, Designer-Sidecars, Referenzen, OCX-Komponenten und `ResFile*`-Ressourcen;
nicht deklarierte Dateien im Projektverzeichnis bleiben ausgeschlossen.

Neue Regressionen prüfen den Erhalt von Versions- und Compatibility-Feldern im ProjectSystem sowie
im erzeugten Manifest TypeLib-, OCX- und `.res`-Dateien neben Form und `.frx`, während eine
unabhängige Quelldatei weiter fehlt. Damit ist
`l1-03-c-project-compatibility-resources-components` **`implemented`** /
`documented-verified`. Die Karte besagt Eingabeadressierung, nicht bereits Resource-Embedding,
Component-Package- oder Binary-Compatibility-Emission; diese bleiben ausdrücklich offen.

Kanonischer Nachweis: **1426/1426** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **81 implemented, 12 partial, 25 planned von 118 |
93/118 documented-verified**.

## Karte l1-02-f: Variant-Zustände und Null-Konvertierungen geschlossen (01.09.2026)

`Mid`, `Left`, `Right`, `UCase`, `LCase`, `Trim`, `LTrim` und `RTrim` waren als reine
String-Operationen deklariert. Ein `Null`-Variant wurde dadurch vor dem Aufruf zu `String`
konvertiert und meldete Fehler 94, obwohl diese Variant-Formen Null weitergeben müssen. Die
Intrinsic-Signaturen sind nun `Variant -> Variant`; Runtime-Overloads halten Null als Null,
lösen Default-Member auf und verwenden für alle übrigen Werte die bestehende VB6-Stringkonversion.

Die neue Runtime-Regression deckt beide `Mid`-Formen sowie alle sieben weiteren String-Funktionen
ab; das emittierte VB6-Programm misst denselben Weg durch Binder, IR und Managed-Emitter. Damit
sind die Zustands-, Null- und numerischen Klauseln von
`l1-02-f-variant-state-conversions` **`implemented`** / `documented-verified`. Objekt-/Array-
Varianten und die vollständige Operator-Promotion bleiben getrennte Teilkarten.

Kanonischer Nachweis: **1428/1428** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **82 implemented, 11 partial, 25 planned von 118 |
93/118 documented-verified**.

## Karte l1-02-m: Screen-Hostvertrag (01.09.2026)

`Screen` ist nicht länger nur ein semantisch bekanntes, dynamisch dispatchtes Objekt. Der
Managed-Pfad senkt `ActiveForm`, `ActiveControl`, `TwipsPerPixelX`, `TwipsPerPixelY` und
`MousePointer` in eigene IR- und Runtime-Operationen ab. `IVB6Host` liefert dafür einen
zusammenhängenden `VBScreenState` sowie einen Pointer-Setter; der Headless-Fallback hat keinen
aktiven Host, verwendet 96-DPI-Umrechnung (15 Twips pro Pixel) und hält den Zeigerwert
deterministisch pro Prozess. Der WinForms-Adapter liefert aktive gebundene Form bzw. Control,
den aktuellen DPI-Faktor und reicht verbreitete VB6-Zeigerformen an Windows-Cursor weiter.

Gemessen: Runtime-Regressionen prüfen den Headless-Zustand, die Objektidentität des Screen-
Facades und einen injizierten Host. Managed-End-to-End- und IR-Regressionen prüfen die
kompilierten Skalare, den Setter sowie alle fünf lesbaren Member. **26 Runtime- und 19
Managed-End-to-End-Tests** sind gezielt grün.

Die Karte bleibt **`partial`** / `documented-verified`: Die Screen-Oberfläche ist geschlossen;
als verbleibender expliziter Hostvertrag bleibt `Printer`.

## Karte l1-02-m: Printer-Hostvertrag (01.09.2026)

Der globale `Printer` besitzt jetzt einen typisierten Standardvertrag statt eines nicht gebundenen
Namens: Geräte-/Dokumentnamen, Druck- und Seitenattribute, Koordinaten, Skalierung,
Twip-Faktoren, `Font`, `Print`, `NewPage`, `EndDoc`, `KillDoc`, Messen und `PaintPicture` werden
in eigene Managed-IR-Operationen abgesenkt. `VBPrinterState` ist der gemeinsame Snapshot für
einen Host; die Druckausgabe selbst erfolgt nur über dessen explizite Callbacks. Ohne passenden
Host modelliert die Runtime einen sicheren virtuellen Letter-/96-DPI-Druckauftrag und erzeugt
keinen physischen Druck.

Gemessen: Der Runtime-Test prüft den deterministischen Dokument-Lebenszyklus sowie State-,
Mess- und Output-Delegation zu einem Testhost. Das emittierte Programm prüft globale Instanz,
Kern-Eigenschaften, `Print`/Seitenwechsel, Messen und Skalierung; eine IR-Regression prüft die
typisierten Getter, Setter und Operationen. **27 Runtime- und 21 Managed-End-to-End-Tests** sind
gezielt grün.

Die Karte bleibt **`partial`** / `documented-verified`, weil native Druckertreiber und die
erweiterten Druck-Grafikprimitive bewusst nach M8/M9 gehören. Die zuvor offene explizite
Printer-Hostoberfläche selbst ist damit geschlossen.

Kanonischer Nachweis: **1415/1415** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **73 implemented, 10 partial, 35 planned von 118 |
83/118 documented-verified**.

## Karte l1-02-j: aktiver Handler bei `Exit Sub` (01.09.2026)

Die letzte offene Klausel der Karte war kein fehlender `Resume`-Zweig, sondern der Zustand von
`Err` beim expliziten Verlassen eines aktiven Handlers. Die offizielle VBA-Referenz schreibt für
`Exit Sub`, `Exit Function` und `Exit Property` innerhalb eines Fehlerhandlers ein Zurücksetzen
der `Err`-Eigenschaften vor. Der Managed-Emitter kannte jedoch nur einen unmarkierten
Prozedur-Return; dadurch blieb `Err` nach `Exit Sub` mitsamt Nummer, Quelle, Beschreibung und
`Erl` im aufrufenden Code sichtbar.

`IrReturnTerminator` trägt nun die explizite Kennzeichnung für diesen Rückweg. Der Lowerer setzt
sie ausschließlich für die gebundene `Exit Sub`/`Function`/`Property`-Anweisung; normale
Fall-through-Returns bleiben davon getrennt. Der Managed-Emitter ruft vor dem Prozeduraustritt
die neue, handlerbewusste Runtime-Operation auf, die `Err` nur bei einem tatsächlich aktiven
Handler löscht. Die End-to-End-Regression prüft alle Felder nach der Rückkehr, und eine
IR-Regression sichert die klare Trennung zwischen explizitem Exit und natürlichem Ende.

Damit sind Verschachtelung, das Wiederherstellen äußerer Handler, jede `Resume`-Form,
`Err`/`Erl`-Fortschreibung sowie das dokumentierte Reset-Verhalten vollständig abgedeckt.
`l1-02-j-nested-error-resume` und die zugehörige Control-Flow-Matrixfläche stehen auf
**`implemented`** / `documented-verified`.

Kanonischer Nachweis: **1433/1433** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **84 implemented, 9 partial, 25 planned von 118 |
93/118 documented-verified**.

## Karte `math.complete-surface`: Variant-Untertypen nachgemessen (01.09.2026)

Die Math-Familie war in Runtime, Intrinsic-Bindung und Roadmap bereits vollständig umgesetzt,
stand in der Matrix aber noch auf `partial`. Der Abschlussabgleich hat jede Klausel direkt einer
Ausführungsprobe zugeordnet: `Null`/`Empty`, die Zahluntertypen von `Abs`/`Fix`/`Int`, negative
Bruchteile, Banker's Rounding, die Definitions- und Überlaufgrenzen sowie die wiederholbare
24-Bit-`Rnd`/`Randomize`-Folge.

Als bislang nur indirekt belegte Grenze ergänzt der Runtime-Test negative `Currency`- und
`Date`-Werte: `Abs` bewahrt den Currency-Untertyp und Betrag, `Fix` schneidet ein Date-Variant
gegen Null ab, und `Int` rundet dasselbe Variant gegen minus unendlich. Der gezielte Testlauf
misst **9/9** Math-Runtimefälle grün; die bestehenden Managed-E2E-Fälle decken die Intrinsic-
Bindung und Ausführung ab.

`math.complete-surface` ist damit **`implemented`** / `documented-verified`; nicht zur Karte
gehörige Objekt-/Array-Operator- und Automation-Regeln bleiben getrennt offen.

Kanonischer Nachweis: **1434/1434** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **85 implemented, 8 partial, 25 planned von 118 |
93/118 documented-verified**.

## Karte `format.complete-surface`: Stringmasken exakt verifiziert (01.09.2026)

Der Format-Code deckte bereits benannte Zahlen-, Boolean-, Datums- und Zeitformate,
vierteilige Zahlenmasken, Datumstoken einschließlich Kalenderwochen und die profilabhängige
Kultur ab. Die letzte nur indirekte Teststelle war die Stringmasken-Ausrichtung: `Debug.Print`
entfernt sichtbare Randauffüllung in seinen Vergleichshelfern und kann deshalb `@` nicht als
Leerzeichenprobe belegen.

Die Runtime-Regression prüft die Zeichenkette selbst: `@@@` füllt rechtsbündig mit einem
Leerzeichen auf, `!@@@` füllt linksbündig, und `&&&` lässt ein nicht benötigtes Platzhalterzeichen
weg. Damit sind `@`, `&`, `<`, `>`, `!`, Literale und Escapes ohne einen ausgabebedingten
Blindfleck abgedeckt. Die bestehenden Tests belegen weiter die vier Zahlenabschnitte einschließlich
`Null`, sämtliche dokumentierten Datum-/Zeit-Token und die Trennung von invariantem
`Deterministic`-Profil und kulturabhängigem `VB6Sp6`-Profil.

`format.complete-surface` ist somit **`implemented`** / `documented-verified`.

Kanonischer Nachweis: **1435/1435** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht auf **86 implemented, 7 partial, 25 planned von 118 |
93/118 documented-verified**.

## As-New-Reaktivierung und die Nothing-Speichergrenze (02.09.2026)

`Dim x As New C` legt die Instanz jetzt erst beim ersten Zugriff an, und `Set x = Nothing` leert
den Slot so, dass die nächste Referenz erneut aktiviert. Der Binder merkt sich die
`New`-Deklaration am lokalen Symbol, das IR trägt dafür einen eigenen Aktivierungsausdruck, und
der Lowerer gibt ihn auch für ein ByRef übergebenes As-New-Local aus — sonst umginge die reine
Adressbildung die verzögerte Anlage.

Dazu gehört die Speicherfrage, an der die erste Fassung vorbeigelaufen ist: Ein Klassen-Slot
erhält für `Nothing` die CLR-Nullreferenz, denn genau daran erkennt die Reaktivierung den
geleerten Zustand. Der generische `Object` ist aber ebenfalls ein Klassensymbol, während sein
Speicher variantenförmig bleibt und den identitätstragenden Nothing-Marker braucht — ohne ihn
ist ein Element in einem `SAFEARRAY(VT_DISPATCH)` nicht mehr von `Empty` zu unterscheiden. Die
Regel schließt `VBStandardTypes.Object` deshalb ausdrücklich aus.

Gemessen ist das an der bestehenden Vertragszusage: Der Marshalling-Fall für Variant-Array-
Callbacks prüft die Marker-Identität der Elemente nach `Set values(4) = Nothing` und fiel mit der
breiten Regel um. Er ist unverändert geblieben; eingeengt wurde die Regel.

Die Karte `l1-02-i-object-members-lifecycle` bleibt **`partial`** — Lebenszyklus, `Implements`
und `WithEvents` sind davon unberührt.

Kanonischer Nachweis: **1437/1437** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht unverändert auf **86 implemented, 7 partial, 25 planned von
118 | 93/118 documented-verified**.

## Karte l1-02-h: Variant-Wertkopie und die Objektgrenze (02.09.2026)

Ein Breitendurchgang über die Vertragsfläche der Karte hat einen Defekt und sechs falsche
Fehlernummern gemessen; der Quelltext allein zeigte keinen davon.

**Arrays im Variant wurden nicht kopiert.** VB6 kopiert ein Array an jeder Wertgrenze. Für
UDT-Member setzt `IrLowerer.LowerValueCopy` das bereits um, aber ob ein Variant ein Array trägt,
steht erst zur Laufzeit fest. Gemessen teilten `b = a` zwischen Variants, das Ablegen eines
typisierten Arrays in einem Variant und ein `ByVal`-Argument die Speicherung: der Aufgerufene
schrieb in das Array des Aufrufers zurück. Die neue Runtime-Operation
`VBArrayOperations.CopyAssignedValue` legt an jeder echten Wertgrenze eigene Speicherung an;
Objekte behalten ihre Referenzidentität, Skalare gehen unverändert durch. Lesende Intrinsics
bleiben bewusst außen vor — ein `UBound` würde sonst das ganze Array kopieren, was eine
IR-Regression festhält.

**Ein Variant ohne Objekt meldete die falsche Nummer.** `Nothing` ist im Variant ein
identitätstragender Marker, kein `null`; die Membersuche lief deshalb ins Leere und meldete 438,
während der typisierte Pfad daneben schon 91 lieferte. Ein Mitgliedszugriff auf einen Skalar
meldete ebenfalls 438 statt 424. Beide Operanden von `Is` und die rechte Seite von `Set`
verlangten gar kein Objekt: `Empty Is Nothing` lieferte sogar `True`, weil `Empty` dieselbe
CLR-`null` ist, die ein Objektslot für `Nothing` benutzt — genau die Unterscheidung, um die es in
dieser Karte geht. Der Wächter prüft am statischen Typ und greift nur bei Variant-Operanden, damit
der Objektpfad unberührt bleibt. Dafür trägt der Bound Tree jetzt mit, dass eine Zuweisung ein
`Set` war.

**Eine Fehlernummer ging in der Reflexion verloren.** `Collection.Item` meldet für eine Position
außerhalb der Sammlung korrekt 9, aber der spät gebundene Aufruf verpackte sie in eine
`TargetInvocationException` ohne VB6-Nummer, die in `VBErrors.Set` im Sammelwert 5 landete. Die
Verpackung wird jetzt aufgelöst. Ein bestehender Test schrieb genau diese Verpackung fest; seine
Vertragszusage — die Ausnahme eines Default-Getters kommt heraus — gilt unverändert, festgelegt
war nur das Reflexionsdetail.

Die Karte bleibt **`partial`** / `documented-verified`: Automation-Dispatch über echte
IDispatch-Server und die vollständige SAFEARRAY-Formenlehre sind nicht Teil dieser Messung.

Kanonischer Nachweis: **1444/1444** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Die Matrix steht unverändert auf **86 implemented, 7 partial, 25 planned von
118 | 93/118 documented-verified**.

## Karte l1-02-j: Fehler im Kopf einer Kontrollflussanweisung (02.09.2026)

Die Karte stand auf `implemented` / `documented-verified`, doch eine ganze Fehlerklasse entkam
`On Error` vollständig und beendete den Prozess: ein Fehler bei der Auswertung einer Bedingung.
Der Befund gilt auch für längst vorhandene Fehler wie einen Index außerhalb der Arraygrenzen und
ist damit älter als die zuletzt ergänzten Formen.

Der Grund ist strukturell. `CanProtectForErrorHandling` nimmt Kontrollflussanweisungen von der
Absicherung aus, weil ihr Rumpf mehrere Basisblöcke umfasst und eine Schutzregion keine
Blockgrenze überschreiten darf — der Emitter lehnt das ausdrücklich ab. Ihr **Kopf** läuft aber im
aktuellen Block: die Bedingung von `If`, `ElseIf`, `While` und `Do`, der Selektor von
`Select Case` sowie Startwert, Grenze und Schrittweite von `For`.

Genau dieser Kopf bekommt jetzt seine eigene Schutzregion. Die Fehler-Fortsetzung, die
`IrErrorBoundaryEndInstruction` bereits getrennt führt, zeigt dabei auf den Block hinter der
Anweisung — das ist die Resume-Next-Semantik: der Fehler steht in `Err`, kein Zweig und kein
Schleifenrumpf läuft, und die Ausführung geht hinter der Anweisung weiter. Ein `On Error GoTo`
gewinnt weiterhin mit seinem Handlerziel.

Gemessen über alle sechs Kopfformen mit `Err.Number` 9 und unverändertem Zweigzustand, dazu
Gegenproben mit fehlerfreien Köpfen und ein `On Error GoTo`-Fall.

Die Karte bleibt **`implemented`** / `documented-verified`; der vorherige Stand war für diese
Fehlerklasse zu optimistisch.

Kanonischer Nachweis: **1444/1444** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Breitendurchgang über die offenen partial-Karten (02.09.2026)

Ein Durchgang mit Wegwerfprogrammen über `l1-02-e`, `l1-02-i`, `l1-02-m`, `l1-02-n` und
`l1-03-i` hat vier Defekte gemessen. Keiner war beim Lesen des Quelltexts sichtbar, und drei
lagen in Flächen, die als abgeschlossen galten.

**Ein Array-`Put` unter aktivem Handler erzeugte ungültiges IL.** `Put` und `Get` eines Arrays
expandieren in eine Elementschleife und verlassen dabei ihren Basisblock. Die Schutzregion war
aber schon vorher geöffnet, also spannte sich die Try-Region über die Sprünge, und die CLR lehnte
die ganze Methode mit `InvalidProgramException` ab — das Programm startete nicht. Ohne aktiven
Handler lief derselbe Code. Betroffen war jedes Array, typisiert wie Variant. Der Emitter hatte
gegen diesen Fall bereits eine Prüfung, sie stand jedoch außerhalb der Blockschleife und lief erst
nach der letzten Anweisung der Prozedur; eine Region, die Blöcke überspannte und später ordentlich
geschlossen wurde, fiel dadurch nie auf. Die Region wird jetzt **nachträglich** eingefügt: erst
wird die Anweisung gesenkt, und nur wenn sie im selben Block geblieben ist, wandert der Anfang an
die gemerkte Position. Das deckt jede künftige Anweisung ab, die sich in Blöcke aufspaltet, ohne
sie aufzählen zu müssen. Die Emitterprüfung liegt jetzt in der Blockschleife und meldet einen
Verstoß laut, statt ungültiges IL zu erzeugen.

**Die dreiwertige Logik kannte ihre absorbierenden Fälle nicht.** `ApplyVariantBitwise` gab
pauschal `Null` zurück, sobald ein Operand `Null` war. VB6 entscheidet aber früher: `And` steht
fest, sobald eine Seite False ist, `Or`, sobald eine Seite True ist, und `Imp`, sobald der
Vordersatz False oder der Nachsatz True ist. Fünf von elf gemessenen Kombinationen waren falsch.
Der bestimmende Operand kommt jetzt unverändert zurück, damit `False And Null` Boolean bleibt und
`0 And Null` seinen numerischen Untertyp behält. Numerisch entscheiden nur 0 und der Wert mit
allen gesetzten Bits; `Null Or 1` bleibt `Null`, wie es ein bestehender Test bereits festhält.
Bei `Imp` gilt die Regel bewusst nur für Boolean-Operanden — die numerische Tabelle ist nicht
gleich klar dokumentiert.

**Eine deklarierte Objektvariable sah aus wie `Empty`.** Ein Klassenslot hält für `Nothing` die
CLR-Nullreferenz, und die liest ein Variant als `Empty`: `TypeName` meldete `"Empty"` statt
`"Nothing"`, `VarType` 0 statt 9, `IsObject` False statt True. Über ein Variant war dasselbe
korrekt. Die Konvertierung `ClassTypeSymbol → Variant` fiel durch alle Zweige und reichte die rohe
Referenz durch; sie hängt den Marker jetzt wieder an — das exakte Spiegelbild der Regel, die ein
Variant-`Nothing` beim Ablegen in einem Klassenslot zu `null` macht. Ein wirklich leeres Variant
bleibt davon unberührt.

**Zwei weitere Sammelwerte 5 im Datei-I/O.** Lesen über das Dateiende meldete 5 statt 62, ein
nicht geöffneter Kanal 5 statt 52. Beide Texte standen in der Fehlertabelle bereits, nur erreichte
sie niemand: `GetStream` warf eine generische `InvalidOperationException`, `LineInput` eine
`EndOfStreamException`. `Close` auf einen nicht geöffneten Kanal bleibt geräuschlos wie in VB6.

Zwei bestehende Tests mussten ihre Form ändern, keiner seine Zusage: `ObjectVariants_Propagate\
DefaultGetterFailures` hielt die `TargetInvocationException` der Reflexion fest und
`Operations_OnAClosedFileNumberFail` die generische `InvalidOperationException` — beide Formen
verschluckten genau die VB6-Nummer, um die es ging. Der Grund steht jetzt im jeweiligen Test.

Offen und ausdrücklich nicht mitgenommen: die Funktionsform `Input(n, #f)` wird nicht geparst
(`VB6P0001`), während die Anweisungsform `Input #f, var` funktioniert. `Printer.Page` zählt ab 0
statt ab 1 — der Headless-Vertrag legt die Seitenzählung allerdings selbst fest. `l1-02-a` war mit
dem Einzeldatei-Helfer nicht messbar und braucht ein Mehrprojekt-Setup.

Die Kartenstände bleiben unverändert; gemessen wurde die Absicherung, nicht der Umfang.

Kanonischer Nachweis: **1449/1449** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Der Kanalmarker in der Argumentliste (02.09.2026)

Der Breitendurchgang hatte notiert, dass `Input(n, #f)` nicht geparst wird — `VB6P0001` auf dem
`HashToken` — während die Anweisungsform `Input #f, var` funktioniert. Der Befund lag nicht dort,
wo die Meldung hinzeigte: Intrinsic und Runtime waren beide längst vorhanden, `Input(3, f)` ohne
Marker lief auch vorher. Gescheitert ist allein der Parser am `#`.

VB6 erlaubt den Kanalmarker in jeder Argumentliste, nicht nur bei `Input`: `LOF(#1)`, `EOF(#1)` und
`Seek(#1)` sind ebenso gültig. `ParseArgument` nimmt ihn jetzt entgegen und legt ihn als eigenen
Knoten ab — dasselbe Muster, das der Parser für `ByVal`/`ByRef` am Aufrufort schon nutzt. Der
Binder reicht auf den Ausdruck dahinter durch; das Zeichen trägt keine Semantik.

Die Mehrdeutigkeit gegen das Datumsliteral entsteht dabei nicht neu: Der Lexer macht aus einem
gültigen Datum zwischen Rauten bereits einen eigenen Token und lässt nur alles andere als
`HashToken` stehen. Ein `#` in Argumentposition kann deshalb nur der Marker sein. Ein Parsertest
hält beide Seiten fest, dazu ein End-to-End-Fall mit `Input`, `LOF`, `EOF`, `Seek` und einem
Datumsliteral in derselben Prozedur.

Kanonischer Nachweis: **1452/1452** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Karte l1-02-a: Member über ihren Modulnamen ansprechen (03.09.2026)

Der Breitendurchgang über `l1-02-a` hat die vier dokumentierten Klauseln bestätigt — `Public` und
`Global` lösen unqualifiziert über Module hinweg auf, `Private` und `Dim` bleiben modullokal und
melden von außen `VB6S0001` — und ebenso die gesamte Prozedur-/Blockgrammatik, die die Karte als
Restfläche führt: prozedurweites `Const`, `Static` über Aufrufe hinweg, `Dim` in einem Block
(bleibt prozedurweit gültig), Label mit `GoTo`, `With` einschließlich Verschachtelung,
Doppelpunktketten und Zeilenfortsetzung.

Gefehlt hat die **Qualifizierung über den Modulnamen**. `Deklarierend.Wert()` meldete
`VB6S0001: Variable 'Deklarierend' is not declared`, und zwar für jede Form: Variable, `Global`,
Konstante, Funktion mit und ohne Argument, Aufruf als Anweisung und Zuweisung. Der Modulname war
schlicht kein auflösbarer Bereich.

Den Entwurf hat eine Messung entschieden. Dieser Compiler **verbietet** gleichnamige öffentliche
Member über Module hinweg: `VB6PRJ0003` für Prozeduren, `VB6PRJ0006` für Modulvariablen. Ein
öffentlicher Name ist damit projektweit eindeutig, und die Qualifizierung kann nie auflösen,
sondern nur benennen. Genau deshalb darf die Bindung sie abstreifen — beide Formen treffen
beweisbar dasselbe Symbol. Ohne diesen Befund wäre das eine Näherung gewesen.

Nur Standardmodule zählen: bei einer Klasse benennt derselbe Bezeichner den Typ, keinen Bereich.
Eine Variable gleichen Namens gewinnt immer, sonst wäre der Punkt kein Memberzugriff mehr; eine
Gegenprobe im Test hält das fest. `Modul.Funktion(...)` geht dabei ausdrücklich in den Aufruf- und
nicht in den Indexpfad — die Klammern gehören zur Argumentliste.

**Abweichung von VB6, bewusst so belassen:** Echtes VB6 erlaubt doppelte öffentliche Namen und
braucht die Qualifizierung, um sie zu unterscheiden. Dieser Compiler lehnt den zweiten Träger ab.
Das ist eine vorbestehende Entscheidung mit eigenen Diagnosen und wurde hier nicht angetastet —
sie ist aber der Grund, warum das Abstreifen hier trägt und in VB6 selbst nicht tragen würde.

Die Karte bleibt **`partial`** / `documented-verified`: numerische Zeilenlabels werden weiterhin
nicht geparst, wodurch `Erl` strukturell 0 bleibt.

Kanonischer Nachweis: **1453/1453** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Zeilennummern und ein Erl, das etwas meldet (03.09.2026)

`Erl` konnte strukturell nie etwas anderes als 0 liefern. Der Grund lag nicht dort, wo er zuerst
vermutet wurde: Die Laufzeitkette ist vollständig — der Lowerer senkt für ein Label
`ErrorSetLineNumber`, der Emitter bildet es auf `VBErrors.SetLineNumber` ab, die Runtime hält den
Wert. Unerreichbar war allein die Syntax.

Beide Labelprädikate im Parser verlangten, dass das Label allein auf seiner Zeile steht; der
Kommentar dort nennt den Grund offen, nämlich dass der VISIA-Korpus jedes seiner 21 Label so
schreibt. Damit war `10 Debug.Print "x"` — die klassische BASIC-Form, auf die sich `Erl` überhaupt
bezieht — ein Parserfehler `VB6P0001`.

Die Erweiterung gilt gezielt für **Zeilennummern**, nicht für benannte Label: Keine Anweisung
beginnt sonst mit einem Integer-Literal, die Form ist also eindeutig. Bei einem benannten Label
wäre `Foo: Bar` gegen einen parameterlosen Aufruf mehrdeutig; dort ebenfalls zu greifen hieße
raten. Dazu verlangt die Anweisungsschleife nach einem Label kein Zeilenende mehr — die
beschriftete Anweisung folgt ihm auf derselben Zeile.

Gemessen: nummerierte Anweisungen laufen, `GoTo <Nummer>` springt, eine Nummer allein auf ihrer
Zeile bleibt gültig, `Erl` meldet nach einem Fehler in Zeile 40 die 40 und bleibt ohne Fehler bei
0. Ein Parsertest hält beide Formen fest, ein End-to-End-Fall die Wirkung auf `Erl`.

Kanonischer Nachweis: **1455/1455** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Random-Sätze nachgemessen und die Satzlänge benannt (03.09.2026)

Der Breitendurchgang über die Random-/`Len`-Fläche von `l1-02-n` hat elf Proben gemessen, zehn
davon korrekt: eine zum UDT passende Satzlänge, das Auffüllen auf eine größere `Len`, der Rundlauf
mit fester Zeichenkette und Boolean, `LOF`, `Loc` und `Seek` nach Satzgrenzen, die VB6-Standardlänge
128 ohne `Len`, skalare Sätze mit unbeschriebenen Lücken sowie das fortsetzende `Put #f, ,`.

Falsch war die Nummer für eine Satzlänge, die den Wert nicht fasst: **5** statt der dokumentierten
**59** („Bad record length"). Der Text stand in der Fehlertabelle bereits, erreichbar war er nicht —
drei Stellen in `VBFiles` warfen dafür eine generische `InvalidOperationException`.

Damit ist heute zum vierten Mal dasselbe Muster aufgetreten: eine korrekt implementierte Prüfung,
deren Nummer auf dem Weg nach oben verloren geht, weil sie als .NET-Ausnahme ohne VB6-Nummer
gemeldet wird. Und zum dritten Mal hat ein bestehender Test genau diese Form festgeschrieben und
damit die Lücke abgesichert statt sie zu zeigen. Die Zusage der Tests blieb jeweils unberührt;
geändert wurde nur die geprüfte Ausnahmeform, mit dem Grund im Test.

Kanonischer Nachweis: **1456/1456** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Ein Breitendurchgang ohne Defekt — und was er trotzdem einbrachte (03.09.2026)

Nachdem dasselbe Muster viermal aufgetreten war — eine korrekt implementierte Prüfung, deren
VB6-Nummer verloren geht, weil sie als .NET-Ausnahme ohne Nummer gemeldet wird — lag der Verdacht
nahe, dass es endemisch ist. Die Runtime enthält 78 generische Würfe.

Die Messung widerlegt das. Der größte Block, 33 Stellen in `VBStrings`, ist **bewusste
Lückenmeldung**: „Format mask is outside the current subset" ist kein VB6-Fehler, sondern genau die
Regel, lieber zu melden als zu raten. Und alle neunzehn geprüften Argumentverträge sind korrekt:
`Asc("")`, `Left(-1)`, `Mid(0)`, `Space(-1)`, `String(-1)`, `Chr(-1)`, `StrConv` mit ungültiger
Konstante, `InStr(0)`, `Sqr(-1)` und `Log(0)` melden die **dokumentierte** 5 („Invalid procedure
call or argument") und nicht den Sammelwert in Verkleidung; nicht konvertierbarer Text meldet 13,
ein Überlauf 6, eine Division durch null 11. `Choose` mit einem Index außerhalb und `Switch` ohne
Treffer sind kein Fehler, sondern liefern `Null`.

Der Ertrag liegt deshalb nicht in einer Korrektur, sondern darin, dass diese neunzehn Verträge
**völlig ungetestet** waren. Die Messung ist jetzt ein Regressionsschutz. Ein Fall gehörte beim
Schreiben ausdrücklich nicht dazu: `ReDim Preserve` auf ein festes Array ist korrekterweise die
Übersetzungsdiagnose `VB6S0029` und kein Laufzeitfehler.

Kanonischer Nachweis: **1457/1457** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## PSet und die Zeichenposition des Hosts (03.09.2026)

Der Plan führte `PSet`, `Circle` und `Point` als schnellen Gewinn — es fehle nur die
Host-Umsetzung. Die Messung hat das widerlegt: `PSet (10, 20)` scheiterte bereits am **Parser**
(`VB6P0001` auf dem Komma in der Koordinatenklammer), `Point` und `PaintPicture` unqualifiziert am
Binder mit `VB6S0005`. Es war die volle Schichtkette, nicht ein Nachtrag im Host.

Gebaut nach der vorhandenen `Line`-Vorlage: `LinePointSyntax` wiederverwendet, dazu das
`Step`-Schlüsselwort, die optionale Farbe und die qualifizierte Form `Picture1.PSet`. Der
WinForms-Host trifft dieselbe Dreiwegewahl der Zeichenfläche wie `Line`, einschließlich der
Raster-Operation, wenn `DrawMode` nicht `CopyPen` ist und eine persistente `AutoRedraw`-Fläche
existiert. Ohne UI-Host bleibt `PSet` ein deterministischer No-op wie `Line` und `Cls`.

**Der Host führte bisher keine Zeichenposition.** `Line Step` rechnet relativ zum Startpunkt, für
`PSet Step` gibt es aber keinen. Statt `Step` halb zu bauen, ist `CurrentX`/`CurrentY` jetzt
vollständig da — als Zustand und über den Memberzugriff lesbar und schreibbar, sodass
`Form.CurrentX` aus VB6-Code funktioniert. `PSet` lässt die Position auf dem gesetzten Punkt
stehen; der Pixeltest prüft, dass der Punkt sitzt, der Nachbar frei bleibt und `Step` von dort misst.

Zwei Nebenbefunde, nicht in dieser Karte behoben: Der Binder verwirft eine ihm unbekannte
Anweisung **still** (`_ => null`) — nach der Parserschicht übersetzte `PSet` sauber und tat nichts.
Und `Point` sowie `PaintPicture` sind unqualifiziert weiterhin nicht deklariert.

Ein bestehender Test hat seine Form geändert: `Bind_CombinesWhitespaceSeparatedIndexed\
MemberArguments` prüft die Regel, dass `obj.Member (a, b), c` zu einer Argumentliste verbindet,
und benutzte dafür `form.PSet`. Die Regel gilt unverändert — nachgemessen mit einem anderen
Membernamen —, nur das Beispiel taugte nicht: `PSet` ist keine spät gebundene Methode, und weil
der Host kein solches Mitglied kennt, verpuffte genau dieser Pfad.

Kanonischer Nachweis: **1462/1462** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Circle mit Bögen, Segmenten und Seitenverhältnis (03.09.2026)

Wie schon bei `PSet` scheiterte jede unqualifizierte Form am Parser. Die qualifizierte Form
`Picture1.Circle (50, 50), 20` parste zwar, erzeugte aber **keinen** Zeichenaufruf: sie lief über
den spätgebundenen Memberpfad, den der Host nicht kennt, und tat damit nichts.

Die Karte ist größer als `PSet`, weil VB6 vier optionale Argumente kennt — Farbe, Start- und
Endwinkel, Seitenverhältnis — und jedes davon **mittendrin** ausgelassen werden darf.
`Circle (x, y), r, , 0, 3.14` zeichnet einen Bogen in der aktuellen Vordergrundfarbe; würde der
Parser die Lücke schlucken, käme der Startwinkel als Farbe an. Ein eigener Parsertest hält das fest.

Drei Übersetzungsentscheidungen stehen mit Begründung im Code:

- **Winkel**: VB6 misst im Bogenmaß gegen den Uhrzeigersinn ab drei Uhr, GDI+ in Grad im
  Uhrzeigersinn ab derselben Stelle. Umgekehrt wird deshalb die Drehrichtung, nicht nur die Einheit.
- **Negative Winkel**: In VB6 verlangt ein negativer Winkel zusätzlich die Radiuslinie, aus dem
  Bogen wird ein Tortenstück. Das Vorzeichen entscheidet zwischen `DrawArc` und `DrawPie`, für die
  Lage zählt der Betrag.
- **Seitenverhältnis**: Der Radius gilt entlang der x-Achse, die y-Achse wird gestreckt.

Der Pixeltest prüft das gegenständlich: bei Verhältnis 2.0 liegt der obere Rand doppelt so weit vom
Mittelpunkt entfernt wie der rechte, und im Mittelpunkt liegt keine Farbe — `Circle` zeichnet den
Umriss, nicht die Fläche. Die Zeichenposition wird wie bei `PSet` fortgeschrieben, `Step` misst von
ihr aus. Ohne UI-Host bleibt `Circle` ein deterministischer No-op.

Kanonischer Nachweis: **1467/1467** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Point liest zurück, was gezeichnet wurde (03.09.2026)

`PSet` und `Circle` schreiben auf die Zeichenfläche; `Point` ist die Gegenrichtung und damit die
erste Stelle, an der der Host ein Pixel **liest**. Das ändert die Form: `PSet` und `Circle` sind
Anweisungen mit eigener Syntax und scheiterten am Parser, `Point` ist eine Funktion mit
gewöhnlicher Aufrufform und scheiterte deshalb eine Schicht später, am Binder mit
`VB6S0005 – Procedure 'Point' is not declared`.

Der Vertrag ist `IVB6Host.TryGetGraphicsPoint` mit einer Standardimplementierung, die `false`
liefert. Der Rückgabewert trennt „keine Farbe" von einer Farbe, die zufällig 0 ist;
`VBInteraction.GraphicsPoint` macht daraus die dokumentierte VB6-Antwort **-1** für einen Punkt
außerhalb der Fläche — und dieselbe -1 gilt kopflos, wo es gar keine Fläche gibt.

Zwei Entscheidungen stehen mit Begründung im Host:

- **Ein unberührtes Pixel meldet die Hintergrundfarbe.** Auf der persistenten Zeichenfläche ist es
  durchsichtig; VB6 sieht dort die Farbe, auf die die Fläche gelöscht wurde. Ohne diese Umsetzung
  käme statt `BackColor` ein durchsichtiges Schwarz zurück.
- **Die qualifizierte Form geht einen anderen Weg.** `Picture1.PSet (x, y)` ist eine eigene
  Anweisung mit Ziel, `Picture1.Point(x, y)` dagegen ein gewöhnlicher Memberaufruf und landet in
  `TryInvokeMember`. Beide Wege teilen sich dieselbe Leseroutine.

`Point` ist wie `Cls` ein gewöhnlicher Bezeichner: eine eigene Prozedur dieses Namens verdeckt das
Intrinsic. Ein E2E-Test hält das fest, weil der Name in Altprojekten frei vergeben sein kann.

Kanonischer Nachweis: **1472/1472** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## EXCEPINFO war gefüllt, freigegeben und nie gelesen (03.09.2026)

`VBComDispatch.Invoke` übergab `IDispatch::Invoke` eine `EXCEPINFO`-Struktur, gab deren BSTR-Felder
korrekt wieder frei — und wertete sie nie aus. Zurück kam nur der blanke `HRESULT`. Ein Server, der
`Err.Raise 53` auslöste, kam beim Anwendercode deshalb als `false` aus `TryInvoke` an, fiel in den
Reflection-Pfad und endete dort als **438**. Nummer, Beschreibung, Quelle und Hilfe des Servers
waren zu diesem Zeitpunkt bereits freigegeben.

Die Struktur wird jetzt gelesen, **bevor** sie freigegeben wird, und über `VBErrors.Raise` auf das
`Err`-Objekt abgebildet. Drei Regeln stehen mit Begründung im Code:

- **wCode schlägt scode.** `EXCEPINFO` trägt den Fehler in genau einem der beiden Felder.
- **Ein scode im FACILITY_CONTROL-Bereich ist eine VB6-Nummer, die über COM gereist ist.** Ein
  Server, der `Err.Raise 9` auslöst, sendet `0x800A0009`; der Client muss wieder 9 sehen. Alles
  außerhalb dieses Bereichs bleibt der volle negative HRESULT — genau damit rechnet
  `vbObjectError`-Arithmetik im Anwendercode.
- **Ohne Beschreibung bleibt 440.** Ein Server darf scheitern, ohne zu sagen warum; VB6 braucht
  trotzdem eine Nummer, und 440 ist sein dokumentierter Sammelwert für Automatisierungsfehler.

Die entscheidende Einschränkung: gemeldet wird **nur** bei `DISP_E_EXCEPTION`. Jeder andere
HRESULT beschreibt die Aufrufform, nicht den Server — und behält damit seine Wiederholungen mit
ByVal-Aufrufform beziehungsweise dem anderen `PROPERTYPUT`-Vertrag. Genau dafür sind die
Rückfallpfade da; ein Fehler daraus darf nicht beim Anwender landen.

Absicherung: vier Runtime-Tests über die reine Abbildung und über die Entscheidungsregel, welcher
HRESULT überhaupt beim Anwender ankommt. Der Ende-zu-Ende-Nachweis gegen einen echten COM-Server
fehlt noch — er hängt an den kontrollierten Testkomponenten aus Phase 0 des Managed-Abschlussplans.
Bis dahin bleibt diese Fläche **dokumentationsgestützt**, nicht gegenständlich gemessen.

Kanonischer Nachweis: **1476/1476** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Der harte ANSI-CharSet bei Declare ist kein Fehler (03.09.2026)

`ManagedEmitter` setzt für jeden `Declare` fest `CharSetAnsi` zusammen mit `ExactSpelling`. Der
Abschlussplan hatte das als möglichen Bugfix geführt — mit dem ausdrücklichen Vorbehalt, vorher zu
klären, ob echtes VB6 ein `Alias "MessageBoxW"` als ANSI marshallt oder Unicode ableitet.

Die Klärung fällt gegen den Fix aus. VB6 ist ANSI-only: es marshallt jeden `String` eines `Declare`
als `LPSTR`, unabhängig davon, worauf der Alias zeigt. Ein Alias auf die W-Funktion bekommt dort
ANSI-Bytes und liefert Unsinn — beobachtbares VB6-Verhalten, kein Compilerfehler. `ExactSpelling`
gehört dazu: VB6 hängt nie still ein `A` oder `W` an einen Aliasnamen.

Damit greift die Regel, die alles andere schlägt: eine Unicode-Ableitung wäre keine Reparatur,
sondern eine Verschiebung der Semantik für Altcode. Ein `Declare`, das heute in VB6 ANSI überträgt,
würde danach etwas anderes übertragen. Ein Unicode-Aufruf bleibt in VB6 das, was er immer war — ein
Aufruf über ein Bytearray.

Geändert hat sich deshalb nichts am Emitter, nur an der Absicherung: ein Emittertest hält jetzt
gegenständlich fest, dass ein `Alias "MessageBoxW"` mit `CharSetAnsi`, `ExactSpelling` und
wörtlichem Importnamen in den Metadaten landet. Die Entscheidung ist damit gepinnt statt nur
gemeint. Ein additives Opt-in für echtes Unicode-Marshalling bleibt möglich — additiv, mit eigener
Syntax, ohne bestehende `Declare`-Zeilen zu berühren.

Kanonischer Nachweis: **1477/1477** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Die intrinsische Control-Menge ist vollständig — und der stille Panel-Rückfall ist weg (03.09.2026)

VB6 kennt 21 intrinsische Controls. Vierzehn waren im Host nachgebaut, sieben fehlten:
`VScrollBar`, `HScrollBar`, `DriveListBox`, `DirListBox`, `FileListBox`, `Data` und der
`OLE`-Container. Sie fielen alle in denselben Zweig — `_ => new Panel()` — und wurden damit zu
einem leeren Rechteck, ohne dass irgendetwas es gemeldet hätte.

Warum sie überhaupt nachgebaut werden müssen, statt sie zu laden: ActiveX-Controls stehen im
`.vbp` namentlich mit GUID und werden tatsächlich geladen. Intrinsische Controls haben dort
**keinen Eintrag** — sie stecken in `MSVBVM60.DLL`, der VB6-Laufzeit selbst. Sie zu laden hieße,
diese Laufzeit zu laden, und genau das schließt die Roadmap aus.

**Scrollbars.** VB6 und WinForms sind hier zweimal verschieden, und beides ist beobachtbar. Eine
WinForms-Scrollbar erreicht ihr eigenes `Maximum` nie, weil der Schieber `LargeChange` Einheiten
der Bahn belegt — der Bereich muss also um diesen Betrag geweitet werden, damit `Value = Max`
überhaupt erreichbar ist. Und VB6 trennt, was WinForms zusammenlegt: Ziehen am Schieber löst
fortlaufend `Scroll` aus und `Change` genau einmal, beim Loslassen. Ein `Change`-Handler, der
einen Datensatz nachlädt, darf nicht pro Mauspixel laufen. Ein Wert ausserhalb `Min..Max` meldet
380 statt still begrenzt zu werden.

**Dateisystem-Controls.** `Drive`, `Path`, `Pattern` und `FileName` tragen den Vertrag; ein
qualifizierter `FileName` verschiebt den Pfad mit, wie es die VB6-Dateidialoge tun. Ein nicht
vorhandener Pfad meldet 76, ein nicht vorhandenes Laufwerk 68. Die `DirListBox` ist die einzige
Liste, deren gültige Indizes unter null reichen: `List(-1)` ist das Elternverzeichnis.

**Ein Nebenbefund an der Ereignisübersetzung.** `FindEvent` bildete `Change` pauschal auf
`TextChanged` ab. Für eine Scrollbar ist das falsch, und für `Scroll` hätte die WinForms-Bedeutung
gewonnen. Die Regel lautet jetzt: ein Wrapper, der den VB6-Namen **selbst deklariert**, schlägt die
Übersetzungstabelle. Nur selbst deklarierte Ereignisse — sonst hätte `GotFocus` still von `Enter`
auf `Control.GotFocus` gewechselt, und das ist eine andere Semantik.

**`Data` und `OLE` sind bewusst halb.** Ihre Entwurfsflächen sind vollständig da, damit Formulare
mit ihnen laden und ihr Layout stimmt. Das Recordset kommt über DAO/ADO per COM, die Einbettung
über die generische ActiveX-Schicht — beides sind spätere Etappen. Bis dahin melden die davon
abhängigen Mitglieder 445, statt still nichts zu tun. Das ist ein Platzhalter mit Ansage, keine
gemessene VB6-Antwort.

**Erst danach der Rückfall.** Die Reihenfolge war der Punkt: Solange die dokumentierten Controls
fehlten, hätte eine Diagnose „unbekannt" gesagt und „noch nicht gebaut" gemeint. Jetzt ist die
Menge vollständig, und ein unqualifizierter Name ausserhalb davon meldet **429** — ein Control,
das auch VB6 nicht hätte erzeugen können. Ein **qualifizierter** Name behält den Platzhalter: er
gehört einer Typbibliothek, also einem Stock-OCX oder einem UserControl des Projekts, und wird von
der generischen ActiveX-Schicht bedient. Damit bleiben die generierten `Visia.*`-UserControls des
Korpus unberührt; die Containernamen `Form`, `MDIForm`, `UserControl` und `PropertyPage` sind
ausdrücklich ausgenommen, weil sie gar keine Controls sind.

Kanonischer Nachweis: **1483/1483** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Die CLI setzt jede Option nur noch einmal um (03.09.2026)

`src/VB6.Compiler.Cli/Program.cs` implementierte die Optionsgrammatik dreimal — einmal im
`.vbp`-Zweig, einmal im Einzeldatei-Zweig, einmal in `HandleProjectGroup` —, jeweils mit
handgeschriebenen Aritätswächtern der Form `args.Length is >= 3 and <= 8`. Eine neue Option hieß
drei Stellen ändern, und ein vergessener Zweig fiel nur über die langsamen Prozesstests auf. Das
stand als Falle in `CLAUDE.md` und war die Vorab-Aufräumung, die dem gepackten Resolver-Task
vorausgehen muss: eine Task, die aus einem dieser drei Zweige herausgelöst wird, würde sonst
stillschweigend von den anderen beiden abweichen.

Jetzt parst `CommandLineParser.TryParse` die ganze Grammatik **einmal**, vor der Verzweigung nach
Eingabeart, und liefert ein `CommandLineOptions`. Die drei Zweige lesen daraus nur noch ihren
Befehl. Sie bleiben getrennt — eine `.vbg` akzeptiert weiterhin kein `--dump-ir` —, aber sie
entscheiden das jetzt über denselben geparsten Befehl statt über eine eigene Argumentanalyse.

Drei Dinge sind dabei bewusst gleich geblieben:

- **Der Plattform-Default hängt an der Eingabeart.** `.vbp` und `.vbg` sind x86, weil
  Legacy-Projekte 32-Bit-ActiveX laden; eine einzelne Quelldatei bleibt AnyCPU. Das steht jetzt an
  einer Stelle statt an dreien.
- **`--compatibility` ohne Befehl ist selbst der Befehl.** Es analysiert mit diesem Profil, und der
  Optionsdurchlauf muss deshalb auf ihm beginnen statt dahinter. Genau daran ist der erste Versuch
  gescheitert — der bestehende Test hat es gefangen.
- **VB6Sp6 wählt x86 und lehnt jede andere ausdrückliche Wahl ab.**

Weggefallen sind die willkürlichen Aritätsobergrenzen. Eine unbekannte Option wird jetzt **beim
Namen genannt**, statt dass eine zu lange Kommandozeile pauschal die Nutzung ausgibt.

Kanonischer Nachweis: **1484/1484** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Der schnelle IDispatch-Pfad hat auf x64 nie mehr als ein Argument übertragen (03.09.2026)

Der Abschlussplan sieht für Etappe D kontrollierte COM-Testkomponenten aus IDL vor — eine
Grundwahrheit, die nicht unser eigener Emitter erzeugt hat. Auf dieser Maschine ist kein Windows
SDK installiert: kein `midl.exe`, kein `oaidl.h`. Die Komponenten sind hier also nicht baubar.

Als Ersatz dient ein COM-Server, den Windows selbst mitbringt: `Scripting.Dictionary` aus
`scrrun.dll` — echte Typbibliothek, duale Schnittstelle, Default-Property, dokumentierte
Fehlernummern. Nicht so aussagekräftig wie eine eigene Komponente für exotische
Typbibliotheksformen, aber eine ehrliche Grundwahrheit für den Aufrufpfad. Die erste Messung dagegen
fand drei Defekte, von denen keiner beim Lesen des Quelltexts sichtbar war.

**1. `VariantSize` war fest 16.** Ein `VARIANT` ist auf x86 sechzehn Bytes, auf **x64
vierundzwanzig** — die Union trägt `BRECORD`, und das sind zwei Zeiger. Mit der festen 16 überlappte
jedes Argument ab dem zweiten das vorige. Ein Aufruf mit einem Argument oder ohne funktionierte,
jeder Aufruf mit zweien kam als Unsinn am Server an und wurde vom Standard-Proxy mit
`RPC_X_NULL_REF_POINTER` abgewiesen, bevor er überhaupt lief.

**Von außen sah nichts kaputt aus.** `TryInvoke` meldete `false`, der Reflection-Rückfall in
`VBDynamicDispatch` beantwortete den Aufruf, und das Programm lief weiter — nur ohne die
Fehlernummern des Servers. Der schnelle Pfad war auf x64 für mehrargumentige Aufrufe schlicht tot,
und kein Test hat es gesehen, weil der Rückfall funktioniert.

**2. `rgdispidNamedArgs` war null, wenn es keine benannten Argumente gab.** Innerhalb desselben
Apartments verzeiht das jeder Server. Ein STA-Objekt, das von einem MTA-Thread aus gerufen wird —
und der Testhost ist MTA —, geht über den Standard-IDispatch-Proxy, und der weist den Nullzeiger ab.
Der Zeiger wird jetzt immer bereitgestellt, `cNamedArgs` bleibt 0.

**3. `EXCEPINFO` fehlte ein Feld.** Zwischen `dwHelpContext` und `pfnDeferredFillIn` steht in
`oaidl.h` ein `pvReserved`. Ohne dieses Feld rutschte alles danach: `Scode` las die erste Hälfte
eines Funktionszeigers statt der Fehlernummer. Ein Server, der über `scode` statt `wCode` meldet,
sah damit aus, als hätte er gar nichts gemeldet.

**Und eine Korrektur an der Meldestelle von gestern.** Der EXCEPINFO-Nachtrag meldete beim ersten
misslungenen `Invoke`. Das ist zu früh: `Scripting.Dictionary.Add` lehnt die ByRef-Aufrufform ab,
die seine **eigene** Typbibliothek beschreibt, und zwar mit `0x800A0005` — im FACILITY_CONTROL-
Bereich, also nach der Regel ein Serverfehler. Der ByVal-Rückfall gelingt danach. Wer sofort meldet,
macht aus einem funktionierenden Rückfall einen Fehler 5. Gemeldet wird jetzt erst, wenn **jede**
Aufrufform durch ist.

Dazu kommt: fehlt die Beschreibung, zeigt VB6 seinen eigenen Text zur Nummer.
`Scripting.Dictionary` meldet 457 wortlos, und VB6 sagt trotzdem „This key is already associated
with an element of this collection".

Gemessenes Ergebnis, vorher und nachher:

| Aufruf | vorher | jetzt |
|---|---|---|
| `d.Add "a", 1` | über Reflection | direkt über IDispatch |
| `d.Add` mit doppeltem Schlüssel | `Err.Number = 5`, Beschreibung `0x800A01C9` | **457** mit VB6-Text |
| `d.Remove` mit fehlendem Schlüssel | `Err.Number = 5` | **32811** |

Absicherung: zwei Runtime-Tests direkt gegen den Fremdserver und ein Ende-zu-Ende-Test, der
denselben Weg durch generierten VB6-Code nimmt. Damit ist die Lücke geschlossen, die der
EXCEPINFO-Nachtrag ausdrücklich offengelassen hatte.

**Weiter offen:** eine aus IDL gebaute Testkomponente für VT_CARRAY, Pointer-auf-Pointer und
frühgebundene vtable-Interfaces. Die braucht ein Windows SDK und ist hier nicht baubar.

Kanonischer Nachweis: **1487/1487** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Benannte Argumente auf spät gebundenen Aufrufen (03.09.2026)

Mit einem messbaren COM-Gegenüber ließ sich Etappe D Punkt 1 endlich prüfen. Das Ergebnis war
knapp: Default-Property lesen (`d("a")`), Default-Property schreiben (`d("c") = 3`), indizierte
Property schreiben (`d.Item("a") = 9`) und ein SAFEARRAY-Rückgabewert (`d.Keys()`) funktionierten
alle bereits. Gefehlt hat genau eine Sache — **benannte Argumente**, und zwar schon im Binder:

```
VB6S0069  Named argument 'Key' is not a parameter of procedure 'Add'.
```

Der Befund ist folgerichtig: `NormalizeNamedArguments` löst Namen gegen die Signatur der Prozedur
auf. Ein spät gebundener Aufruf **hat** keine Signatur — `CreateDynamicObjectProcedure` liefert nur
ein einzelnes ParamArray, und gegen dessen einen Parameternamen passt kein Name der Welt.

Der Weg ist deshalb ein anderer als bei einem gewöhnlichen Aufruf: Der Name wird **nicht** zur
Übersetzungszeit in eine Position aufgelöst, sondern reist mit. Der Binder verpackt ihn in eine
`VBVariants.NamedArgument(name, wert)`, die im ohnehin vorhandenen Variant-Array mitfährt — so muss
zwischen Binder und Dispatcher keine Schicht eine zweite Argumentform lernen.

Aufgelöst wird dann dort, wo das Ziel bekannt ist, und das ist an zwei Stellen verschieden:

- **COM-Ziel:** `GetIDsOfNames` bekommt den Membernamen **und** die Parameternamen in einem
  einzigen Aufruf und liefert die DISPIDs zurück — genau der Mechanismus, den VB6 benutzt. Dafür
  musste die Interop-Deklaration korrigiert werden: `rgDispId` ist ein *Array*, ein
  `out int` hätte für einen Namen funktioniert und für jeden weiteren den Stack beschädigt.
- **Managed-Ziel:** die Parameternamen stehen in den Metadaten; `ResolveNamedArguments` ordnet dort
  zu.

Die DISPPARAMS-Reihenfolge ist die fehleranfällige Stelle. `rgvarg` muss mit den **benannten**
Werten beginnen, in der Reihenfolge von `rgdispidNamedArgs`, danach folgen die positionellen in
umgekehrter Reihenfolge. Da `Invoke` die Liste ohnehin von hinten nach vorn schreibt, heißt das:
positionelle Argumente in Quellreihenfolge, benannte umgekehrt angehängt, und der Wert eines
Property-Put ganz zuletzt — dort, wo das bestehende `DISPID_PROPERTYPUT` ihn erwartet.

Ein Name, den das Ziel nicht kennt, meldet **448** — auf beiden Wegen dieselbe dokumentierte
VB6-Antwort. Eine bekannte Signatur wird unverändert zur Übersetzungszeit aufgelöst; `VB6S0069`
bleibt für sie in Kraft, und ein Test hält beide Seiten dieser Grenze fest.

Gemessen gegen `Scripting.Dictionary`: `d.Add Key:="a", Item:=1`, dieselben Namen in umgekehrter
Reihenfolge und `d.Add "c", Item:=3` gemischt mit einer Position liefern alle das richtige
Ergebnis; und gegen eine eigene Klasse über `As Object` ebenso.

Kanonischer Nachweis: **1491/1491** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Eine Lücke in der Argumentliste ist kein leerer Wert (03.09.2026)

Beim Nachmessen der letzten offenen Zeile von Etappe D — optionale Parameter — zeigte sich der
Unterschied zwischen zwei Formen, die gleich aussehen. `fso.CreateTextFile(pfad)` mit zwei
weggelassenen optionalen Argumenten am **Ende** funktionierte. `fso.OpenTextFile(pfad, , False)`
mit einer Lücke **mittendrin** meldete 5.

VB6 überträgt eine solche Lücke als VARIANT vom Typ `VT_ERROR` mit dem Code
`DISP_E_PARAMNOTFOUND`. Genau daran erkennt ein Server, dass das Argument **nicht angegeben**
wurde — im Unterschied dazu, dass es als `Empty` angegeben wurde. Beides ist beobachtbar
verschieden: der Server setzt im einen Fall seinen dokumentierten Vorgabewert ein, im anderen
nimmt er den leeren Wert.

Der Marker `VBVariants.MissingValue()` reiste stattdessen unverändert bis zum Marshalling und kam
dort als Objekt an, das der Server nicht lesen konnte. Am Ende der Liste fiel das nicht auf, weil
dort gar kein Argument übertragen wird — die Lücke ist dann einfach ein kürzeres `rgvarg`.

`VBComDispatch` schreibt jetzt für ein ausgelassenes Argument selbst ein `VT_ERROR` mit
`DISP_E_PARAMNOTFOUND`. Damit ist die IDispatch-Zeile der Etappe D vollständig: LCID, benannte
Argumente, `DISPID_VALUE`, `DISPID_PROPERTYPUT`, `EXCEPINFO`, optionale Parameter und
Default-Properties sind alle gegenständlich gegen einen Fremdserver gemessen.

Kanonischer Nachweis: **1492/1492** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Menü-Arrays, und was an Declare schon fertig war (03.09.2026)

Zwei Zeilen der Roadmap, beide zuerst gemessen.

**`Declare` und `AddressOf` waren fertig.** 24 Tests decken Signaturen (skalar, `Currency`,
`Boolean`/VARIANT_BOOL, vorzeichenlose Breiten, `LongPtr`), Zeichenketten (ANSI, Rückschreibpuffer),
Zeiger (`As Any`, `StrPtr`), UDTs (blittable, feste Zeichenketten, VB6-Vier-Byte-Packung), Arrays
(SafeArray ByRef und als Rückgabe, `LongPtr`-Arrays) und Callbacks (nativ, ANSI+Boolean,
Variant-Slots, Variant- und String-Arrays, ByRef-UDT) ab. Die Nachmessung fand keinen Defekt:
`Declare Sub`, `ByRef … As Any` auf ein Arrayelement innerhalb eines UDT und ANSI-`ByVal`-Strings
verhalten sich richtig. Zwei bis dahin ungetestete Formen sind jetzt festgehalten — mehr war nicht
zu tun.

**Menü-Arrays fehlten ganz.** `LoadControlArrayElement` prüfte `template is not Control` und gab in
allen anderen Fällen `null` zurück. Ein Menü ist im Host aber keine `Control`, sondern ein
`MenuProxy` — `Load mnuDatei(1)` tat deshalb **still gar nichts**. Wieder derselbe Musterfehler:
ein Rückfall, der nicht meldet.

Die Fehlersemantik lag bereits richtig in der Runtime und gilt für jedes Arrayelement: ein Index
unter der Untergrenze meldet 9, ein bereits geladener Index meldet 360, und ein Index über der
Obergrenze erweitert das Array. Gefehlt hat nur die Host-Umsetzung:

- Das geladene Element landet im **selben Drop-down wie seine Vorlage, direkt dahinter**, damit die
  Reihenfolge im Menü der Deklaration entspricht.
- Es erbt Beschriftung, `Enabled`, `Checked` und Tastenkürzel, startet aber **unsichtbar** — wie
  jedes geladene Element eines Control-Arrays.
- `Unload` nimmt es aus seinem Container **und** aus der Komponentenliste, sodass derselbe Index
  danach ein frisches Element erzeugt.

UserControl-Arrays gehen denselben Weg wie gewöhnliche Controls, weil ein generiertes UserControl
eine `Control` ist. Gegenständlich gemessen ist das **nicht** — dafür bräuchte die Suite ein
generiertes UserControl, und der VISIA-Korpus wird analysiert, nicht ausgeführt.

Kanonischer Nachweis: **1494/1494** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## As New gab es nur für lokale Variablen (03.09.2026)

Beim Nachmessen der Instancing-Zeile aus Etappe D fielen zwei **Sprachlücken** auf, die mit COM
nichts zu tun haben und das Akzeptanzkriterium direkt betreffen:

```
Private g As New Zaehler      ' Modulvariable  -> Fehler 91
Private inner As New Zaehler  ' Klassenfeld    -> Fehler 91
Dim lokal As New Zaehler      ' lokal          -> richtig
```

Der Binder erzeugte für alle drei Formen brav einen `BoundNewExpression`-Initialisierer. Gesenkt
wurde er aber nur für Locals: `NeedsModuleInitialization` kannte nur `String`, feste Zeichenketten
und Arrays, und der Lesepfad des Lowerers verzweigte ausschließlich auf `LocalVariableSymbol`. Der
Initialisierer stand also im gebundenen Baum und ist nie irgendwo angekommen — die Variable blieb
`Nothing`, und der erste Zugriff meldete 91.

Der Fix folgt dem lokalen Vorbild statt einer eigenen Mechanik: `IrEnsureClassExpression` trägt
statt eines Locals einen **Place**, also Global oder Feld. Der Emitter kann dabei nicht den
Dup-und-Store-Trick der lokalen Variante benutzen, weil ein Feld seinen Empfänger vor dem Wert auf
dem Stack braucht; er liest den Place stattdessen nach dem Erzeugen noch einmal — ein zusätzlicher
Ladebefehl gegen eine Form, die mit jedem Place funktioniert.

Damit stimmt der VB6-Vertrag gegenständlich: `Class_Initialize` läuft beim **ersten Lesen**, nicht
beim Laden des Moduls, und nach `Set g = Nothing` entsteht beim nächsten Lesen ein frisches Objekt.

**Darauf aufbauend `Attribute VB_PredeclaredId = True`.** Eine Klasse mit diesem Attribut besitzt
in VB6 eine globale Instanz ihres eigenen Namens — dasselbe, was ein Formular implizit hat. Das war
bisher nicht umgesetzt; `Zaehler.Erhoehe` meldete `VB6S0001`. Da eine verzögert erzeugte globale
Instanz genau das ist, was `As New` jetzt kann, ist die Default-Instanz eine gewöhnliche
`As New`-Projektvariable und keine zweite Mechanik.

Dabei kam ein dritter Befund heraus: `VBClassModuleSource.Normalize` **löscht jede
`Attribute`-Zeile** eines Klassenmoduls und behält nur die Default-Property-Zeile. Das Attribut
erreichte den Parser also gar nicht. Die Ausnahmeliste heißt jetzt `IsSemanticAttribute` und nennt
beide Zeilen, die die Bedeutung des Codes ändern — wer eine dritte braucht, trägt sie dort ein.

Eine Klasse **ohne** das Attribut bleibt weiterhin kein Wert; ein Test hält auch diese Seite fest.

Kanonischer Nachweis: **1497/1497** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Instancing entschied bisher gar nichts (03.09.2026)

Jede Klasse eines ActiveX-Projekts wurde COM-sichtbar emittiert — auch eine, die VB6 als
`Private` führt. Damit landete eine reine Hilfsklasse in der Typbibliothek, in der Registrierung
und im reg-freien Manifest, wo VB6 sie ausdrücklich heraushält.

Die Ursache lag zwei Schichten früher: `VBClassModuleSource.Normalize` löscht jede
`Attribute`-Zeile eines Klassenmoduls, und die Instancing-Angabe steht genau dort — VB6 schreibt
sie als `VB_Exposed` und `VB_Creatable`. Beide Zeilen bleiben jetzt erhalten, wie schon die
Default-Property-Zeile und `VB_PredeclaredId`.

Die Abbildung folgt der VB6-Tabelle:

| Instancing | `VB_Exposed` | `VB_Creatable` | Ergebnis |
|---|---|---|---|
| Private | False | False | **nicht** COM-sichtbar |
| PublicNotCreatable | True | False | sichtbar, **ohne** ProgID |
| MultiUse / SingleUse | True | True | sichtbar mit ProgID |

Die ProgID ist die Stelle, an der „nicht erzeugbar" wirksam wird: ohne sie lässt sich die Klasse
als Rückgabewert benutzen, aber nicht über ihren Namen erzeugen. Und weil der Manifestschreiber
seine Klassenliste aus dem `ComVisible`-Attribut der emittierten Assembly liest, wirkt die
Entscheidung ohne weiteres Zutun bis in Manifest und Registrierung durch.

Ein `.cls` **ohne** diese Attribute — handgeschrieben oder ein Formular — behält den bisherigen,
großzügigen Default. Sonst wäre jede Testdatei dieses Repos plötzlich privat.

Kanonischer Nachweis: **1498/1498** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Connection Points für erzeugte COM-Klassen (03.09.2026)

Eine erzeugte COM-Klasse konnte bisher keine Ereignisse nach außen geben. `RaiseEvent` erreichte
die Handler des eigenen Programms und endete dort; ein COM-Client hatte keine Möglichkeit, sich
anzumelden.

Der naheliegende Weg — `ComSourceInterfacesAttribute` — trägt hier nicht. Die CLR baut den
Connection Point daraus nur, wenn die Klasse **CLR-Ereignisse mit Delegattypen** hat. VB6-Ereignisse
werden in dieser Runtime aber **namentlich zur Laufzeit** verteilt (`VBEvents.Raise(quelle, name,
argumente)`); es gibt weder Delegat noch CLR-Event, und beides nachzurüsten hieße, das
Ereignismodell umzubauen.

Der Container wird deshalb selbst implementiert. `VBComEventSource` ist die Basisklasse jeder
COM-sichtbaren erzeugten Klasse und stellt `IConnectionPointContainer` bereit; ein Ereignis
erreicht eine Senke auf demselben Weg wie jeder andere spät gebundene Aufruf dieser Runtime — die
Senke ist ein `IDispatch`, ihre DISPID wird über den Namen aufgelöst, der Aufruf geht durch
`Invoke`. `VBEvents.Raise` reicht das Ereignis **nach** den eigenen Handlern hinaus, sodass ein
Programm, das sein Ereignis selbst behandelt und veröffentlicht, dieselbe Reihenfolge sieht wie in
VB6.

Vier Entscheidungen stehen mit Begründung im Code:

- **Die Basis kommt nur an COM-sichtbare Klassen.** Eine `Private`-Klasse bleibt ein schlichtes
  `Object`; sie trägt keinen COM-Ballast, den niemand sehen kann.
- **Die Basis ist `ComVisible(true)`.** Das ist keine Bequemlichkeit: Die CLR weigert sich, ein
  AutoDual-Klasseninterface zu bauen, wenn ein Basistyp für COM unsichtbar ist — der erste Versuch
  scheiterte genau daran mit `0x80131509` bei `IClassFactory::CreateInstance`. In das Interface
  gelangt trotzdem nichts, weil jedes Mitglied der Basis eine **explizite**
  Schnittstellenimplementierung ist.
- **Jede angefragte Interface-ID liefert denselben Connection Point.** VB6 hat genau eine
  Ereignisquelle je Klasse, und ein Client ohne Typbibliothek — bis zur Typbibliothekserzeugung
  also jeder — fragt mit `IID_NULL`. Ihn an einer Formalie abzuweisen hieße, das Ereignis nie
  zustellen zu können.
- **Eine Senke, die das Ereignis nicht kennt, ist kein Fehler** und darf die übrigen Senken nicht
  um ihre Zustellung bringen.

Dazu kam eine Ergänzung im Emitter: Eine Klasse mit Basistyp muss deren Konstruktor aufrufen. Der
erzeugte `.ctor` tat das bisher nirgends — bei `System.Object` fällt das nicht auf, bei einer
Basis mit eigenem Zustand schon.

Absicherung: drei Runtime-Tests über Advise, Zustellung, Unadvise samt `CONNECT_E_NOCONNECTION` für
einen unbekannten Cookie, und ein Emittertest, der die Basistypwahl gegenständlich prüft. Der
bestehende Aktivierungstest über den echten `comhost` bleibt grün — er hat den AutoDual-Fehler
gefunden.

**Offen bleibt** die Gegenprobe mit einem echten, prozessfremden COM-Client. Sie hängt an der
Typbibliothekserzeugung und an einer nativen Testkomponente; ohne Windows SDK ist beides hier nicht
baubar.

Kanonischer Nachweis: **1502/1502** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## MDI: ActiveForm, Arrange und die Fensterliste (03.09.2026)

Von MDI standen bisher nur die beiden Registrierungen: ein Formular konnte Container werden
(`MDIForm = True`) und ein anderes sein Kind (`MDIChild = True`). Alles, was ein Programm damit
danach tut, fehlte.

**`ActiveForm`** antwortet jetzt — und zwar mit dem **VB6-Objekt**, nicht mit dem WinForms-Fenster
dahinter. VB6 reicht überall seine eigenen Formularobjekte herum, deshalb muss eine Eigenschaft,
die ein Fenster liefert, über diese Grenze zurückübersetzen. Auf einem Container ist es das aktive
Kind, sonst das aktive Fenster der Anwendung — dasselbe, was `Screen.ActiveForm` meldet.

**`Arrange`** bildet die vier VB6-Konstanten auf `LayoutMdi` ab: Cascade (0), horizontal (1) und
vertikal (2) gekachelt, Symbole anordnen (3). Ein unbekannter Wert meldet **380** statt sich eine
Anordnung auszusuchen — dieselbe Regel wie bei `ScaleMode` und `DrawMode`.

**`WindowList`** markiert das Menü, das VB6 mit den offenen Kindfenstern füllt. VB6 erlaubt genau
eines pro Formular, und der WinForms-Menüstreifen ebenso; eine Zuweisung ersetzt daher schlicht,
was vorher dort stand.

Kanonischer Nachweis: **1504/1504** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Der Resolver läuft jetzt im MSBuild-Prozess (03.09.2026)

Etappe G war bis auf einen Punkt fertig: Die Eingabeauflösung startete für jeden Build einen
`vb6c`-Prozess, nur um ein Manifest zu schreiben. Bei einem inkrementellen Build, der sonst nichts
tut, ist das der größte Teil der Kosten.

Voraussetzung war die Aufräumung der Woche zuvor — erst nachdem die Optionsgrammatik an einer
Stelle lag, konnte die Manifestlogik herausgelöst werden, ohne von einem der drei CLI-Zweige
abzuweichen. Sie liegt jetzt als `VBInputManifest` in `VB6.ProjectSystem`, und **beide** Wege
benutzen dieselbe Implementierung; die CLI ist auf einen Aufruf zusammengeschrumpft.

Neu ist `VB6.Compiler.Sdk.Tasks` mit einem einzigen `ITask`. Er reist im NuGet-Paket unter
`tasks/net10.0` mit, zusammen mit `VB6.ProjectSystem` — der einzigen Abhängigkeit, die er braucht.
Der Compiler selbst bleibt draußen: er gehört nicht in den MSBuild-Prozess.

**Der CLI-Weg bleibt.** Das ist kein harter Schnitt: Findet das SDK die Task-Assembly nicht, oder
setzt jemand `VB6UseResolverTask=false`, läuft alles wie vorher. Beide Zweige stehen mit
gegensätzlichen Bedingungen nebeneinander in denselben Targets.

Der Nachweis kommt ohne Logauswertung aus: Das Testprojekt setzt `VB6CompilerPath` auf eine Datei,
die es nicht gibt, und ruft nur das Auflösungsziel auf. Läuft es durch und liegt das Manifest da,
kann kein Prozess gestartet worden sein. Mit `VB6UseResolverTask=false` **muss** derselbe Aufruf
scheitern — sonst wären die beiden Wege nicht wirklich getrennt.

Zwei Kleinigkeiten am Rande: Das Paket unterdrückt `NU5100` mit Begründung — eine Task-Assembly
gehört bewusst nicht nach `lib`, sie wird geladen und nicht referenziert. Und
`Microsoft.Build.Utilities.Core` musste auf 17.14 gehoben werden; die zuerst gewählte 17.11 trägt
eine bekannte Sicherheitslücke, und der Build meldet das als Fehler.

Kanonischer Nachweis: **1505/1505** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Das Stock-OCX-Inventar — und ein Test, der es ehrlich hält (03.09.2026)

Etappe F beginnt mit einer Bestandsaufnahme: welche der Microsoft-redistributablen VB6-Controls
dieses Projekt bedient, und **wie**. Die Matrix führt sie jetzt in `activeXStockControls`, 34
Einträge mit Datei, Bibliothek und einer von drei Einstufungen:

- **`managed-adapter`** — der Host baut das Control selbst nach; es läuft **ohne installiertes
  OCX**. Das sind heute `TreeView`, `ImageList`, `ImageCombo`, `RichTextBox` und `CommonDialog`.
- **`native-only`** — läuft nur über `AxHost` mit registriertem x86-OCX; sonst bleibt ein sichtbarer
  Platzhalter. Das betrifft die übrigen Windows-Common-Controls: `ListView`, `Toolbar`,
  `StatusBar`, `ProgressBar`, `Slider`, `TabStrip`.
- **`not-implemented`** — weder das eine noch das andere: `MSFlexGrid`, `SSTab`, `MaskEdBox`,
  `Winsock`, `MSComm`, die datengebundenen Controls und der Rest.

Der Punkt der Einstufung ist, dass sie **nicht** aussagt, ob das OCX zufällig installiert ist. Das
wäre eine Eigenschaft der Maschine, keine des Compilers, und genau diese Vermischung macht solche
Tabellen wertlos.

Damit das Inventar eine Messung bleibt und keine Behauptung wird, liest ein Test die Matrix und
prüft sie gegen den Host: **jedes** als `managed-adapter` geführte Control muss sich ohne
installiertes OCX erzeugen lassen, und ein Platzhalter zählt dabei ausdrücklich **nicht** als
Umsetzung. Wer künftig eine Einstufung hochschreibt, ohne den Adapter zu bauen, bekommt einen roten
Test statt einer schöneren Tabelle.

Die zweite Hälfte prüft die Gegenrichtung: Ein Control ohne Adapter darf das Laden eines Formulars
nicht abbrechen. Ohne OCX bleibt ein Platzhalter, mit OCX entsteht ein echtes Control — beides ist
zulässig, ein harter Abbruch nicht.

**Bei der Gelegenheit die Matrix nachgezogen.** Sieben Karten standen noch auf `planned`, obwohl
die Arbeit erledigt und getestet ist — `IDispatch` samt EXCEPINFO und benannten Argumenten, die
intrinsischen Controls, Control-Arrays, der Zeichenpfad, MDI und der MSBuild-Resolver-Task. Der
Stand geht damit von 86/7/25 auf **88 implemented, 12 partial, 18 planned**, bei 100 von 118
`documented-verified`. `oracle-verified` bleibt unverändert bei null: dafür bräuchte es einen Lauf
gegen echtes VB6 SP6.

Kanonischer Nachweis: **1507/1507** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Typbibliotheken über ICreateTypeLib2 (03.09.2026)

Ein spät gebundener Client braucht keine Typbibliothek. Ein **früh** gebundener — VB6, VBA, C++ —
sieht die Klassen ohne sie überhaupt nicht. Bis hierher gab es keine, und damit war eine erzeugte
ActiveX-DLL für genau die Clients unbrauchbar, für die man sie baut.

`ICreateTypeLib2` steckt in `oleaut32.dll` und braucht **kein** Windows SDK — anders als die
IDL-Testkomponenten, die hier weiterhin nicht baubar sind. .NET liefert allerdings nur die
lesende Hälfte der Typbibliotheks-API, die schreibende ist deshalb selbst deklariert. Dabei gilt:
nur die Mitglieder, die dieser Schreiber wirklich ruft, tragen echte Signaturen; die übrigen halten
ihren Platz in der vtable und nehmen bewusst kein einziges Argument, damit ein versehentlicher
Aufruf nicht kompiliert.

Die erzeugte Form ist die, die VB6 für ein Klassenmodul schreibt: eine **Dispinterface** mit
führendem Unterstrich trägt die Mitglieder, eine **Coclass** mit dem blanken Namen nennt sie als
Default. So schreibt ein Client `New Klasse` und nicht `New _Klasse`. Die GUIDs entstehen aus
derselben Ableitung wie im Emitter, sodass Coclass und `GuidAttribute` übereinstimmen, ohne dass
eine Seite die andere lesen muss. Und weil die Klassenliste aus dem `ComVisible`-Attribut kommt,
bleibt eine `Private`-Klasse draußen — wie in VB6.

Der Nachweis ist ein Rundlauf: schreiben, dann mit `LoadTypeLibEx` wieder laden und die Typen und
Mitglieder auslesen. Eine `.tlb`, die niemand laden kann, ist nichts wert; erst der Leser beweist,
dass der Schreiber eine echte erzeugt hat.

Zwei Fehler auf dem Weg, beide lehrreich:

- **Ein freigegebenes RCW weiterbenutzt.** Die Coclass verweist auf die Dispinterface, die aber
  schon freigegeben war — „COM object that has been separated from its underlying RCW". Die
  Freigabe gehört hinter den Verweis, nicht davor.
- **Die Emission sperrte plötzlich ihre eigene Ausgabedatei.** Das Auslesen der COM-Klassen lädt
  die Assembly, und ein entladbarer Ladekontext gibt seine Datei erst frei, wenn der Sammler
  vorbeikommt. Zwei bestehende Tests fielen daran um — zu Recht: Wer eine DLL emittiert, darf sie
  danach nicht sperren. Gelesen wird jetzt aus einer **Kopie** in `%TEMP%`; deren Sperre ist
  gleichgültig, die der Ausgabe wäre es nicht.

Kanonischer Nachweis: **1508/1508** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Der ActiveX-EXE-Local-Server (03.09.2026)

Ein `Type=ActiveX EXE` wurde bisher als **DLL** emittiert. Damit fehlte genau das, was diese
Projektart ausmacht: eine ausführbare Datei, die COM mit `/Embedding` startet, wenn ein Client eine
ihrer Klassen anfordert.

VB6 startet dieselbe Datei in zwei Rollen. Doppelt angeklickt läuft `Sub Main`. Von COM gestartet
darf das Programm **gar nicht** laufen — die Datei registriert ihre Klassenobjekte, pumpt
Nachrichten, bis der Client fertig ist, und beendet sich. Beide Richtungen sind auffällig, wenn man
sie verwechselt: ein Server, der unter `/Embedding` sein Programm startet, zeigt ein Fenster, das
niemand wollte; einer, der sich nicht registriert, lässt den Client auf eine Antwort warten, die
nie kommt.

Der Einstiegspunkt wird deshalb **erzeugt** und nicht aus `Sub Main` genommen: Ein ActiveX EXE darf
gar keine haben — seine ausführbare Datei existiert für COM, nicht für ein Programm, das jemand
startet. Der erzeugte Einstieg bietet sich zuerst COM an und ruft `Sub Main` nur, wenn es beides
gibt: ein normal gestartetes Programm und eine `Sub Main`.

`VBComLocalServer` in der Runtime trägt den Rest: `CoRegisterClassObject` mit `REGCLS_SUSPENDED`
und anschließendem `CoResumeClassObjects` — damit kein Client eine Klasse erreicht, während eine
andere noch nicht registriert ist —, eine Nachrichtenschleife, weil COM prozessübergreifende
Aufrufe als Fenstermeldungen zustellt, und ein Zähler, der weiß, wann der letzte Client fertig ist.
Der Zähler hängt an einer `ConditionalWeakTable`: Der Client gibt einen **Proxy** frei, nicht das
Objekt, also erfährt der Server das Ende nur über den Sammler.

Drei Befunde auf dem Weg, alle drei mit Ansage im Code:

- **Ein CCW entsteht nur für öffentliche Typen.** Die Klassenfabrik war `internal` — für COM damit
  unsichtbar. Das Symptom ist `E_NOINTERFACE`, wenn COM das registrierte Klassenobjekt nach
  `IClassFactory` fragt: Der Server startet, registriert sich, und kann trotzdem nichts liefern.
- **Ein Server ohne Klassen schwieg.** Er beendete sich wortlos und ließ den Client warten. Er
  meldet das jetzt.
- **Git Bash wandelt `/Embedding` in einen Pfad um.** Das kostete eine Runde Fehlersuche am
  falschen Ende — von PowerShell aus lief derselbe Server sofort.

Der Nachweis ist ein echter Rundlauf ohne Attrappe: Die erzeugte `.exe` wird unter `HKCU` als
`LocalServer32` registriert, COM startet sie, der Testprozess erzeugt die Klasse, ruft `Summe`
spät gebunden über die **Prozessgrenze** (`Marshal.IsComObject` ist wahr, es ist ein echter Proxy),
gibt frei — und der Server beendet sich von selbst. Ohne Registrierungsleiche: Der Test räumt den
Schlüssel wieder ab.

**Ein bestehender Test hat dabei seine Zusage verloren, und das war richtig so.**
`EmitManaged_EmitsAllSupportedLibraryProjectKindsWithoutSubMain` prüfte, dass **jede**
Bibliotheksart ohne Einstiegspunkt emittiert — einschließlich ActiveX EXE. Diese Zusage war eine
Herleitung, keine gemessene VB6-Eigenschaft: VB6 baut daraus eine `.exe`, und eine `.exe` hat einen
Einstiegspunkt. Die beiden EXE-Arten sind deshalb aus der Liste genommen und haben ihren eigenen
Test bekommen.

Kanonischer Nachweis: **1510/1510** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## UserControls: Ambient, Extender und der richtige Anfang (03.09.2026)

Die Nachmessung des UserControl-Vertrags fand vier Lücken auf einmal:

```
lifecycle = init, read      ' InitProperties fehlt, ReadProperties feuert für ein neues Control
ambient   = NEIN
extender  = NEIN
show/hide = nie
```

**`InitProperties` statt `ReadProperties`.** VB6 unterscheidet ein **neues** Control von einem
**wiederhergestellten**: Ist nichts gespeichert, bekommt es `UserControl_InitProperties` und setzt
seine Vorgaben selbst; liegt etwas im PropertyBag, bekommt es `UserControl_ReadProperties`. Vorher
lief immer `ReadProperties` — mit einem leeren Bag, was dem Programm eine Wiederherstellung
vorspielte, die es nie gab. Der PropertyBag beantwortet die Frage jetzt selbst (`IsEmpty`).

**`Ambient` und `Extender`** sind die beiden Objekte, über die ein UserControl seinen Container
erreicht, und beide fehlten ganz. `Ambient` trägt, was der Container *vorschlägt* — Schriftart,
Farben — und ob dies der laufende Modus ist; `UserMode` ist hier immer wahr, denn einen
Entwurfsmodus gibt es in diesem Compiler nicht. `Extender` trägt, was der Container **besitzt**:
ein UserControl benennt sich nicht selbst, und ebenso wenig bestimmt es seine Position.

Dabei fiel ein zweiter Fehler auf: Das gehostete Fenster trug gar keinen Namen, `Extender.Name`
hätte den **Typnamen** gemeldet statt `Widget1`. Der Container setzt ihn jetzt beim Anlegen.

**`Show` und `Hide`** melden eine **Änderung**, nicht einen Zustand. Der erste Versuch hängte sich
schlicht an `VisibleChanged` — und meldete dreimal „versteckt" für ein Control, das nie sichtbar
war, weil WinForms dieses Ereignis auch beim Einhängen auslöst. Gemeldet wird jetzt nur ein echter
Wechsel; zweimal derselbe Wert ist kein Ereignis.

**Ein bestehender Test hat seine Zusage verloren.**
`HostEmbedsGeneratedUserControlClassesAsDesignerComponents` prüfte, dass ein frisch angelegtes
UserControl `ReadProperties` bekommt. Das war eine Herleitung aus dem vorhandenen Code, keine
gemessene VB6-Eigenschaft — und sie ist falsch herum. Der Test prüft jetzt `InitProperties`, und
die Vorgaben, die er dort setzt, wandern über `WriteProperties` wieder hinaus.

Kanonischer Nachweis: **1512/1512** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Das Abschlussgate prüft sich jetzt selbst (03.09.2026)

Etappe H ist kein Bauabschnitt, sondern ein Nachweis — und ein Nachweis, den niemand nachrechnet,
ist eine Behauptung. Drei seiner vier Zusagen lassen sich maschinell prüfen, und
`CompatibilityMatrixTests` prüft sie jetzt:

- **Jede Erwartung nennt Tests, und diese Tests existieren.** Verweise dürfen Muster sein
  (`tests/VB6.Compiler.Tests/Variant*Tests.cs`) — auch ein Muster muss auf mindestens eine
  vorhandene Datei zeigen. Eine umbenannte oder gelöschte Testdatei macht die Matrix damit rot
  statt still ungenau.
- **Die zitierten Zahlen sind die Zahlen der Datei.** Der Stand steht an drei Stellen — in der
  Matrix, in `README.md` und in `docs/ROADMAP.md`. Laufen sie auseinander, ist die Matrix nicht
  mehr die Quelle. Genau das fällt jetzt auf.
- **`oracle-verified` bleibt leer.** Das ist die schärfste Regel des Projekts: Dieser Status darf
  nur nach einem Lauf gegen einen echten VB6-SP6-Compiler stehen, und ein solches Orakel existiert
  hier nicht. Der Test hält den Wert auf null. Wer ihn ändern will, muss diese Zusicherung löschen
  — nicht eine Zahl hochschreiben.

Dazu kommt eine ausdrückliche Liste in der Roadmap: **was bewusst `documented-verified` bleibt**,
und warum. Fünf Punkte, jeder mit eigenem Grund — die locale-abhängigen Kalenderfälle mit ihrem
offenen Zielkonflikt, die Fälle, in denen die Dokumentationsherleitung nachweislich falsch war,
die exotischen Typbibliotheksformen ohne Windows SDK, die nativen OCX-Flächen hinter ihrem
x86-Opt-in, und undokumentiertes controlspezifisches Verhalten. Ein späterer Leser kann damit
„bewusst nicht geprüft" von „vergessen" unterscheiden.

Zwei weitere Zeilen der Etappe H sind auf `[~]` gerückt, weil die Arbeit gemessen dasteht: Die
Raw-COM-Probes laufen **in beide Richtungen** — generierter Code gegen echte Fremdserver, und
unsere Klassen von einem Fremdprozess aus, in-process wie out-of-process. Und die Forms-Seite deckt
Lifecycle, MDI, Control-Arrays samt Menü-Arrays und die Zeichenprimitive ab; die Pixeltests laufen
allerdings bei der DPI des Testhosts, nicht bei einer festgeschriebenen.

Kanonischer Nachweis: **1515/1515** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## PropertyPage und UserDocument sehen ihre eigenen Controls (03.09.2026)

Beide Artefaktarten **kompilierten** bereits: Ein `.pag` und ein `.dob` werden geladen, geparst und
gebunden. Sobald aber ein Control darauf lag, brach es ab:

```
VB6S0001  Variable 'cmdOk' is not declared.
```

Die Ursache war eine Prädikatsgrenze, die zwei verschiedene Fragen beantwortet hat.
`IsHostModuleKind` bedeutete gleichzeitig „hat eine Entwurfsoberfläche" **und** „besitzt eine
globale Instanz ihres Namens" — und stand auf `Form or UserControl`. Für die Entwurfsoberfläche ist
das zu eng, denn eine PropertyPage trägt ihren OK-Knopf genauso wie ein Formular. Für die globale
Instanz wäre eine Erweiterung dagegen **falsch**: Ein Formular ist in VB6 über seinen Namen
ansprechbar, ohne dass jemand es erzeugt; eine PropertyPage ist das nie.

Die beiden Fragen sind jetzt getrennt. `HasDesignerSurface` umfasst Form, UserControl,
PropertyPage und UserDocument und entscheidet über Designer-Controls, Host-Eigenschaften und
Host-Intrinsics; `IsHostModuleKind` bleibt bei Form und UserControl und entscheidet allein über die
globale Instanz. PropertyPage und UserDocument sind dabei Container der UserControl-Form — sie
bekommen deren Eigenschaftsfläche, nicht die eines Formulars.

Zwei Tests halten beide Seiten: Ein `.pag` mit Knopf und ein `.dob` mit Textfeld kompilieren samt
Zugriff auf ihre Controls, und ein Projekt, das eine PropertyPage über ihren Namen anspricht, wird
weiterhin abgelehnt.

Kanonischer Nachweis: **1517/1517** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Eine Objekt-Property mit Get und Set las Nothing zurück (03.09.2026)

Der Befund stand seit längerem in der Roadmap und war präzise beschrieben: Eine Klasse mit
`Property Get` **und** `Property Set` gleichen Namens liefert aus dem `Get` `Empty`; das `Set`
speichert nachweislich korrekt, und ein `Get` **ohne** `Set` liefert korrekt. Nur die Kombination
bricht — und sie ist die Normalform jeder Objekt-Property.

Die Messung bestätigte genau das und schnitt die Ursache ein:

```
feld-leer:False        ' das Set hat gespeichert
get-nothing:True       ' das Get liefert Nothing
h.Obj.Kennung -> ok    ' die Kette über dasselbe Get funktioniert
```

Dass der verkettete Zugriff funktioniert, war der entscheidende Hinweis: Nicht der Aufruf des Get
war falsch, sondern **sein Rumpf**. In `BindSetAssignment` wurde ein blanker Name zuerst gegen die
Set-Property der enthaltenden Klasse geprüft — **vor** dem lokalen Gültigkeitsbereich. Innerhalb von
`Property Get Obj` ist `Obj` aber der Rückgabewert. `Set Obj = m_obj` band damit an die
gleichnamige `Property Set` und schrieb `m_obj` auf sich selbst; der Rückgabewert blieb unberührt,
also Nothing.

Der Let-Pfad und **beide** Lesepfade prüfen den lokalen Gültigkeitsbereich zuerst. Nur der Set-Pfad
tat es nicht — eine einzelne fehlende Bedingung. Sie steht jetzt dort, mit derselben Reihenfolge
wie überall sonst.

Der Test prüft beide Paare, Get/Set und Get/Let, und zwar über vier Leseformen: `TypeName`,
`Is Nothing`, den verketteten Aufruf und `Set o = h.Obj`.

Kanonischer Nachweis: **1518/1518** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Eine Property, die ein Array liefert, lässt sich jetzt indizieren (03.09.2026)

`c.Nums(1)` meldete `VB6S0006 – Procedure 'Nums' expects 0 argument(s), but 1 were supplied`. Der
Binder hielt die Klammern für eine Argumentliste, obwohl sie ein Index sind: In VB6 wird die
parameterlose Property gerufen und **ihr Ergebnis** indiziert.

Für den verwandten Fall gab es den Weg bereits — eine Property, die eine Collection liefert, wird
gerufen und deren Default-Property indiziert. Das Array daneben fehlte. Es geht denselben Weg über
`BoundElementAccessExpression`, das eine beliebige Array-wertige Ausdrucksquelle indiziert und im
Lowerer bereits unterstützt war.

Der erste Versuch riss dabei einen bestehenden Test: `Public Grid(1 To 2, 1 To 2) As Long` mit
`c.Grid(1)` muss **VB6S0027** melden. Mein Zweig hätte daraus stillschweigend einen Zugriff mit
einem Index gemacht und den Übersetzungsfehler in einen Laufzeitfehler verwandelt. Die Übersetzung
greift jetzt nur, wenn die Zahl der Indizes zur deklarierten Dimensionszahl passt; alles andere
fällt weiter in die Diagnose.

Kanonischer Nachweis: **1519/1519** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Ein privates Klassenfeld ist von außen kein Mitglied mehr (03.09.2026)

`h.m_geheim` auf ein `Private`-Feld übersetzte anstandslos. Erst zur Laufzeit verweigerte die CLR
den Zugriff — ohne Zeilenangabe, ohne Bezug zur Deklaration, und in der Messung schlicht als
abgebrochene `Debug.Print`-Zeile sichtbar.

Die Ursache lag beim Aufbau der Mitgliedsfläche: **jede** Modulvariable einer Klasse wurde als
Property in die Fläche aufgenommen, unabhängig von ihrer Sichtbarkeit. Die Information war da —
`ModuleVariableSymbol.IsPublic` — nur wurde sie nicht weitergereicht.

Die Property trägt sie jetzt mit. Ein privates Feld **bleibt** dabei in der Fläche, damit die
Klasse es weiterhin über `Me` erreicht; der Binder weist es nur von außerhalb ab, mit dem neuen
Code **VB6S0074**. Das ist die schmalere und richtige Grenze: Es aus der Fläche zu entfernen hätte
auch `Me.m_geheim` innerhalb der Klasse gebrochen, was VB6 erlaubt.

Der kanonische Lauf hat die Ergänzung sofort eingefordert: `EveryProductionDiagnosticCodeIsCovered`
schlug fehl, weil der neue Code keinen Test hatte — genau die Regel, für die diese Prüfung da ist.
Der Test hält beide Seiten fest, den abgewiesenen Zugriff von außen und den erlaubten über `Me`.

Kanonischer Nachweis: **1520/1520** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Null wurde längst weitergereicht — nur nicht geprüft (03.09.2026)

Die Roadmap führte: „`Left`, `Right`, `Mid`, `Trim`, `LTrim`, `RTrim`, `UCase` und `LCase` reichen
`Null` nicht weiter, sondern melden **94**; sie sind als `String -> String` deklariert statt als
`Variant -> Variant`."

Die Messung widerspricht dem in allen neun geprüften Fällen: Jede dieser Funktionen liefert für
`Null` sauber `vbNull` bei `Err.Number = 0`. Die Intrinsics sind längst `Variant -> Variant`
deklariert. Die Zeile beschrieb einen Zustand, den es nicht mehr gibt.

Das ist derselbe Musterfall, den `CLAUDE.md` bereits zweimal festhält: Die Umsetzung ist weiter als
ihre Absicherung. Ungeprüft ist ein solches Verhalten aber nur einen Refactor davon entfernt,
wieder zu verschwinden — also steht es jetzt in zwei Tests.

Der zweite Test hält die **Gegenseite**, die beim Lesen leicht untergeht: Die Dollar-Form ist
`String -> String`, und dort hat `Null` keinen Platz. `Left$(Null, 2)` meldet **94** statt still
eine leere Zeichenkette zu liefern. Beide Familien nebeneinander sind der eigentliche Vertrag —
Altcode liest Datenbankfelder in Variants und verlässt sich darauf, dass Null die Runde übersteht.

Kanonischer Nachweis: **1522/1522** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Eine benannte Konstante darf jetzt die Breite eines String * n sein (03.09.2026)

Beide Deklarationsformen akzeptierten nur ein **Literal**:

```
Private Const BREITE As Long = 5
Dim lokal As String * BREITE     ' VB6S0043
Feld As String * BREITE          ' VB6S0043
```

Der UDT-Binder hatte längst einen vollwertigen Falter — für Arraygrenzen. Er kann Literale,
Klammern, Vorzeichen, benannte Konstanten und ganzzahlige Operatoren, und er behandelt Überlauf
als „faltet nicht", damit die Meldung an der Verwendungsstelle entsteht. Nur die beiden
Breitenprüfungen liefen daran vorbei.

Die Roadmap hatte die Bedingung schon benannt: **beide** Prüfstellen müssen gemeinsam umgestellt
werden, sonst laufen UDT-Member und Deklarator auseinander. Der Falter liegt deshalb jetzt als
`VBIntegerConstantFolder` für sich, und beide Stellen rufen ihn. Das ist keine Ordnungsliebe: Eine
Breite, die im UDT-Member faltet und im `Dim` nicht, ließe denselben Quelltext je nach Fundort
etwas anderes bedeuten.

Gemessen sind alle drei Verhaltensweisen, wie es `CLAUDE.md` für diese Fläche verlangt, und zwar in
beiden Formen: Anfangswert von *n* Leerzeichen, Abschneiden beim Überschreiten, Auffüllen beim
Unterschreiten. Eine Laufzeitgröße bleibt `VB6S0043` — sie zu akzeptieren hieße, den Speicher erst
zur Laufzeit festzulegen, und genau das kann ein festes Layout nicht.

Kanonischer Nachweis: **1524/1524** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## String * n an ByRef String: der Zielkonflikt ist entschieden (03.09.2026)

Die Roadmap führte diesen Punkt als **offen**: Ein `String * n` an einen `ByRef s As String` meldete
`VB6S0008`; VB6 erlaubt die Übergabe mit Copy-in/Copy-out, und der Konflikt zur bewusst typstrengen
ByRef-Regel war ungelöst.

Er löst sich an der Regel, die alles andere schlägt: **Legacy-Projekte kompilieren unverändert.**
Altcode übergibt feste Zeichenketten genau so, und ein Fehler wäre hier die strengere, aber falsche
Antwort. Die typstrenge Regel bleibt trotzdem — sie gilt einer Variablen des **falschen Typs**, für
die das Rückschreiben kein Ziel hätte. Eine Zeichenkette fester Breite hat eines.

Der Weg war schon da, nur nicht verbunden: `RequiresByRefTemporary` erzeugt die Kopie,
`IrCallArgument.WriteBackPlace` schreibt sie zurück, und der Emitter implementiert das bereits für
Variant-Arrayelemente. Neu ist die Unterscheidung, **wann** zurückgeschrieben wird — für die
übrigen Temporär-Fälle ist das Verwerfen genau richtig, weil dort kein Ziel existiert.

Gemessen in beiden Richtungen: Der Aufgerufene sieht `Len(s) = 5`, also die volle Breite; was er
zurückgibt, kommt als `"xy   "` mit `Len = 5` an, also wieder auf Breite gebracht. Das gilt für die
lokale Variable wie für das UDT-Member.

Kanonischer Nachweis: **1526/1526** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Class_Terminate feuert — nur nicht nach VB6s Uhr (03.09.2026)

Die Roadmap führte: „`Dim x As New C` erzeugt eifrig statt bei der ersten Verwendung, und
`Class_Terminate` feuert nie — weder bei `Set o = Nothing` noch beim Verlassen des
Gültigkeitsbereichs."

Beide Hälften stimmen so nicht mehr. Die erste ist mit der `As New`-Arbeit erledigt: Die Erzeugung
ist verzögert, in allen drei Speicherformen. Für die zweite zeigt die Messung ein anderes Bild als
„feuert nie": Nach genügend Allokationen erscheinen die `term`-Ausgaben sehr wohl. Der Emitter legt
für eine Klasse mit `Class_Terminate` einen **Finalizer** an, und der läuft — nur eben, wenn der
Sammler vorbeikommt, nicht wenn die letzte Referenz verschwindet.

Der Unterschied bleibt beobachtbar und wird ausdrücklich **nicht** übertüncht. Eine halbe
Referenzzählung wäre schlimmer als gar keine: Sie ließe `Class_Terminate` auf einem noch lebenden
Objekt laufen, weil eine übersehene Referenz — in einem Variant, einem Array, einem UDT-Member,
einem Klassenfeld — den Zähler zu früh auf null brächte. Zu spät aufzuräumen ist ärgerlich; zu früh
aufzuräumen ruft Anwendercode auf einem Objekt auf, das noch benutzt wird.

Festgehalten ist jetzt der Mechanismus, deterministisch prüfbar: Eine Klasse **mit**
`Class_Terminate` bekommt einen Finalizer, eine ohne bekommt keinen. Die deterministische
Lebensdauer bleibt die offene Architekturfrage, die sie war — und steht als solche in der Roadmap,
nicht als Fehler.

Kanonischer Nachweis: **1527/1527** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## VarPtr sagt jetzt, warum es nicht antworten kann (03.09.2026)

`VarPtr` und `StrPtr` waren unimplementierte Stubs, die eine `PlatformNotSupportedException`
warfen. Beim Anwender kam davon nur **Fehler 5** an — der Sammelwert, den `CLAUDE.md` als „sieht wie
ein Ergebnis aus" führt. `ObjPtr` und `LSet` funktionierten dagegen.

Die Adresse einer verwalteten Speicherzelle gilt nur, solange die Zelle festgehalten wird, und ein
**zurückgegebener** Zeiger überlebt genau das nicht: Der Sammler darf die Zelle danach verschieben.
Unterstützt ist deshalb ausschließlich die Stelle, an der VB6 den Zeiger sofort weiterreicht — ein
`ByVal … As Any`-Argument eines `Declare`, das der Lowerer direkt in eine Adresse übersetzt und das
die Runtime nie erreicht. Diese Form funktioniert und ist getestet.

**Ein Zwischenschritt, der zurückgenommen wurde, und warum.** Der erste Versuch meldete den
allgemeinen Fall zur Übersetzungszeit über den Emitter-Kanal `VB6E0001` — die dokumentierte Art,
„das kann das Backend noch nicht" zu sagen. Der kanonische Lauf hat das sofort abgelehnt: Der
VISIA-Korpus benutzt `VarPtr(chars(0))` als Rückgabewert, und die Übersetzung brach ab. Damit
verletzte die Meldung das oberste Kriterium des Projekts — ein altes `.vbp` übersetzt unverändert.
Eine Übersetzungsmeldung ist hier also die falsche Antwort, so richtig sie im Allgemeinen wäre.

Geblieben ist die Verbesserung, die beides einhält: Die Übersetzung läuft durch, und die Nummer
bleibt VB6s 5 für einen ungültigen Aufruf — aber `Err.Description` sagt jetzt, **was** nicht ging
und **wo** es ginge, statt den Sammelwert unerklärt zu lassen.

Kanonischer Nachweis: **1529/1529** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Die Funktionstabelle ist vollständig (03.09.2026)

Etappe C führt als große Zeile „alle dokumentierten String-, Math-, Financial-, Datum/Zeit-,
`Format`-, Array-, Konvertierungs-, Information-, Interaction-, Environment-, Registry-, App-,
Screen-, Printer- und Clipboard-Verträge implementieren". Diese Zeile klingt offen-endig, also war
die erste Frage nicht „was bauen", sondern **wie weit ist sie**.

Ein Abgleich der Intrinsics-Tabelle gegen die dokumentierte VB6-Funktionsliste ergab: 180
Deklarationen, und genau **zwei** fehlten — `FileAttr` und `IMEStatus`. (`LBound` und `UBound`
stehen nicht in der Tabelle, weil sie ihre eigene gebundene Form haben.) Die Finanzfamilie,
`Partition`, `Switch`, `Choose`, die Registry-Vierergruppe, die B-Varianten und `StrConv` waren
bereits alle da — der erste Durchlauf hatte sie nur groß/klein verschieden geschrieben gefunden.

`FileAttr` meldet den Modus eines offenen Kanals in denselben Bits, die `Open` benutzt: 1 Input,
2 Output, 4 Random, 8 Append, 32 Binary. Ein `ReturnType` von 2 fragt nach dem DOS-Dateihandle —
das hat auch 32-Bit-VB6 nicht, und die Antwort ist dort wie hier 5, aus demselben Grund. Ein
geschlossener Kanal meldet 52, wie jede andere Kanalfunktion.

`IMEStatus` antwortet mit 0, `vbIMEModeNoControl`. Dieser Host installiert nie eine ostasiatische
Eingabemethode, und das ist die Antwort, die VB6 auf einem System ohne eine solche gibt. Einen
Fehler zu melden hieße, Code zu brechen, der bloß fragt.

Damit ist die **Deklarationsfläche** der Standardbibliothek geschlossen. Offen bleibt, was die Zeile
daneben meint: die Vollständigkeit einzelner Verträge — `Format` und `Math` bleiben ausdrücklich
`partial`, weil dort die Frage nicht lautet, ob die Funktion existiert, sondern ob sie jede
dokumentierte Eingabe richtig beantwortet.

Kanonischer Nachweis: **1531/1531** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## General Number zeigte die Umrechnungsreste eines Double (04.09.2026)

Ein Breitendurchgang über die dokumentierten `Format`-Masken fand 16 von 17 Fällen richtig — und
einen falsch, dafür deutlich:

```
Format(1234.567, "General Number")  ->  1234.5670000000000072759576142
```

Das ist der exakte Decimal-Wert des Double 1234.567, und VB6 gibt ihn nie aus. Die Ursache war eine
feste Zuordnung `"GENERAL NUMBER" => "G29"`: 29 signifikante Stellen zeigen bei einem Double genau
die Reste, die die Umrechnung hinterlässt.

Die richtige Regel steht schon in `CLAUDE.md`, nur an anderer Stelle — `Debug.Print` und `CStr`
benutzen **G15 für Gleitkomma und Currency, G29 für den Decimal-Subtyp**. `General Number` folgt ihr
jetzt ebenfalls, und die formatlose Form `Format(1234.567)` gleich mit, denn sie hatte denselben
Fehler.

Alles andere stimmte auf Anhieb: `Currency`, `Fixed`, `Standard`, `Percent`, `Scientific`, die drei
booleschen Paare, die Abschnittssyntax `0;(0)`, die Platzhalter `@`, `>` und `<` sowie
`#,##0.00`, `0.0`, `0%` und `00000`. Zwei Tests halten die Fläche jetzt fest, statt sie zu
vermuten.

Kanonischer Nachweis: **1533/1533** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Wie viele Stellen VB6 zeigt (04.09.2026)

Der Breitendurchgang über die Math-Fläche fand die Rechenverträge durchgehend richtig: `Int` und
`Fix` unterscheiden sich nur bei negativen Werten, `Sgn`, `Abs`, `Sqr`, `Log`, `Exp`, die
Ganzzahldivision, `Mod` mit vorheriger Rundung seiner Operanden, `^` mit negativem Exponenten, und
die Fehlernummern 5 für ein ungültiges Argument und 11 für die Division durch null.

Falsch war etwas anderes — die **Stellenzahl**:

```
CStr(Atn(1) * 4)   ->  3.141592653589793     ' VB6: 3.14159265358979
Debug.Print 1 / 3  ->  0.333333343267441     ' VB6: 0.3333333
```

Zwei verschiedene Ursachen mit derselben Wirkung.

`CStr` ging für Gleitkommazahlen über `Convert.ToString`, und dessen Vorgabe ist die **kürzeste
Zeichenkette, die den Wert exakt zurückliest** — bis zu 17 Stellen, in denen genau die
Umrechnungsreste sichtbar werden. VB6 zeigt einen Double mit 15 und einen Single mit 7
signifikanten Stellen; beide Fälle sind jetzt ausdrücklich benannt.

Der zweite Fall ist lehrreicher: `1 / 3` ist in VB6 ein **Single**, weil beide Operanden Integer
sind — das ist die dokumentierte Regel des `/`-Operators, und der Binder setzt sie richtig um. Die
Ausgabe druckte den Single aber mit `G15` und zeigte damit Stellen, die seine Genauigkeit gar nicht
mehr deckt. `CLAUDE.md` führte bisher pauschal „G15 für Gleitkomma"; die Trennung nach Single und
Double fehlte.

Kanonischer Nachweis: **1536/1536** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems. Bemerkenswert: Eine Änderung an der Zahlenausgabe hat **keinen** bestehenden
Test bewegt — die vorherige Genauigkeit war nirgends festgeschrieben.

## Ein ActiveX-Control bekommt seinen Designer-Zustand jetzt am Stück (04.09.2026)

VB6 setzt die Eigenschaften eines OCX nicht einzeln. Es reicht dem Control den ganzen persistierten
Zustand über `IPersistPropertyBag` und lässt es lesen, was es kennt. Im Repo gab es diesen Vertrag
nirgends — `grep` fand weder `IPersistPropertyBag` noch `IPersistStreamInit` in `src/`. Der
Designer-Zustand erreichte ein Control ausschließlich als Folge von Einzelzuweisungen über
`TrySetMember`, und `VBInteraction.SetMember` verwirft deren Rückgabewert.

Weil die OCX auf dieser Maschine inzwischen registriert sind, ließ sich der Befund zum ersten Mal
**messen** statt herleiten. Ein Wegwerfprogramm unter x86 gegen elf Stock-Controls:

- Alle elf implementieren `IPersistPropertyBag` **und** `IPersistStreamInit`.
- Der Slider fragt beim Laden 18 Eigenschaften ab, das MSFlexGrid 41 — beginnend mit `_ExtentX`.
- `_ExtentX`, `_ExtentY` und `_Version` stehen für jedes OCX in der `.frm`, sind über IDispatch
  aber **nicht setzbar**: das Control weist sie mit einer `COMException` ab. Im Host kam
  entsprechend dreimal `TrySetMember -> False` zurück, ohne dass irgendwo etwas gemeldet wurde.

Die Werte waren also schlicht verloren. Ergänzt sind drei Schichten: `VBComPersistence` mit der
Container-Tüte, ein Host-Haken `CompleteDesignerInitialization`, und im Lowerer der Aufruf, der die
Designer-Hülle schließt — nach der letzten Designer-Eigenschaft, vor `Class_Initialize`. Eine Klasse
ohne Designer-Controls bekommt ihn nicht.

Der Nachweis kommt vom Control selbst: nach der Übergabe wird `IPersistPropertyBag.Save` in eine
mitschreibende Tüte gerufen, und dort stehen `_ExtentX = 4657` und `_ExtentY = 873` — genau die
Werte, die der Einzelzugriff verweigert hatte. Der Fall ist damit **nativ gemessen**, nicht
dokumentiert hergeleitet.

Zwei Zusagen sind bewusst schwächer, als es aussieht. Erstens ist die Reihenfolge nicht die von
VB6: dort lädt das Control seinen Zustand bei der Erzeugung, hier erst am Ende der Hülle. Weil
jeder Einzelwert mitgeschrieben und mitgereicht wird, ist das Ergebnis dasselbe; die Zwischenzeit
ist es nicht. Zweitens bleibt `IPersistStreamInit` offen — der Text einer RichTextBox hängt dort
und kam in der Messung erwartungsgemäß leer zurück.

Nebenbefund aus demselben Lauf: Die native OCX-Fläche ist jetzt vollständig prüfbar.
`build.ps1 -RequireNativeOcx` meldet unter x86 **69/69** bestanden, **0** übersprungen; die
Gegenprobe unter x64 lässt 8 davon hart fehlschlagen. Der bisherige Messwert (50/50, 7 in der
Gegenprobe) ist damit fortgeschrieben.

Kanonischer Nachweis: **1545/1545** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Eine verschachtelte Designer-Gruppe erreicht das Control jetzt als Objekt (04.09.2026)

Der Designer schreibt die Bilder einer ImageList nicht als Eigenschaften, sondern als Struktur:
`BeginProperty Images` und darin je ein `BeginProperty ListImageN`. Ein natives Control fragt beim
Laden auch nicht nach diesen Namen — es erzeugt seine Sammlung selbst, reicht sie als Objekt in die
Tüte hinein und erwartet, dass der Container sie füllt. Gemessen am Toolbar-OCX: der Lesewunsch kam
als `Buttons [__ComObject]` herein.

Zwei Dinge fehlten dafür.

**Der Parser verlor den Elternnamen.** `VBDesignerParser` bildete den Eigenschaftsnamen aus der
*innersten* Gruppe: aus `Images` → `ListImage1` → `Picture` wurde `ListImage1.Picture`, und die
Zugehörigkeit zur Sammlung war weg. Im Korpus fiel das nie auf, weil die Namen dort zufällig
eindeutig sind. Der Name trägt jetzt den ganzen Pfad. Der bestehende Parsertest hat die alte Form
festgeschrieben — das ist keine VB6-Zusage, sondern unsere eigene Darstellung, und die alte war
nachweislich verlustbehaftet; der Test ist entsprechend umgeschrieben.

**Die Tüte kannte nur Werte.** `VBDesignerPropertyBag` baut den Punktpfad jetzt wieder zu
Gruppen auf. Kommt ein Lesewunsch mit einem Objekt herein, das `IPersistPropertyBag` beherrscht,
bekommt dieses Objekt eine Untertüte über die passende Gruppe. Reicht das Control stattdessen
`null` — es erwartet, dass der Container das Objekt erzeugt —, bleibt die Gruppe ungelesen und das
Control behält seinen Vorgabewert. Das ist gemeldet als „nicht gefunden", nicht als leerer Erfolg.

Der Nachweis kommt wieder vom Control: ein registriertes `MSComctlLib.ImageListCtrl.2` meldet nach
der Übergabe `ListImages.Count = 2`. Vorher war die Sammlung leer.

Kanonischer Nachweis: **1547/1547** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **70/70**, Gegenprobe unter x64 lässt 9 hart fehlschlagen.

## Ein `Object=`-OCX bringt jetzt seine ganze Typbibliothek mit (04.09.2026)

`Dim o As MSComctlLib.OrientationConstants` meldete `VB6S0003`, `ccOrientationVertical` meldete
`VB6S0001` — obwohl das Projekt `MSCOMCTL.OCX` ordnungsgemäß über `Object=` referenziert. Ein altes
`.vbp`, das eine dieser Konstanten benutzt, übersetzte damit nicht; das verletzt das
Akzeptanzkriterium des Projekts unmittelbar.

Die Messung zeigte zuerst etwas Irreführendes: `MSComctlLib.ListView` band anstandslos, ein
erfundener Name wurde abgewiesen. Es sah also nach einem funktionierenden Import mit einer Lücke
bei Enums aus. Tatsächlich war überhaupt nichts importiert — die neun bindenden Namen stammten aus
einer **von Hand gepflegten Liste** in `VBExternalTypeCatalog`. Ein funktionierender Ersatzweg, der
den toten Hauptweg verdeckt; dasselbe Muster wie beim IDispatch-Rückfall.

Zwei Ursachen lagen darunter, beide im Pfadresolver:

1. **Die GUID auf einer `Object=`-Zeile ist eine TypeLib-Id, keine CLSID.** Gesucht wurde unter
   `HKCR\CLSID\{…}`; ein installiertes OCX registriert sich unter `HKCR\TypeLib\{…}`. Der Schlüssel
   existiert dort schlicht nicht, und die Auflösung lieferte immer `null`.
2. **Die Versionsauswahl brach nach dem exakten Treffer ab.** Das Projekt pinnt `#2.0#`,
   registriert ist `2.1`. Eine Nebenversion ist in COM aufwärtskompatibel, VB6 bindet daran. Die
   Suche geht jetzt exakt, dann gleiche Hauptversion absteigend, dann der Rest.

Danach kommen aus MSCOMCTL.OCX 131 Typinformationen an — 42 Enums mit ihren Konstanten, 41
Dispinterfaces, 48 Coklassen — statt neun handgeschriebener Namen. Die expliziten Kontrakte für die
bekannten Controls behalten Vorrang: `MergeImportedTypeLibrary` fügt nur mit `TryAdd` hinzu.

Die Gegenprobe steht als eigener Test: `MSComctlLib.GibtsNicht` muss weiterhin `VB6S0003` melden.
Sonst wäre der Gewinn ein unbemerkter Verlust — ein Bibliothekspräfix, das jeden Tippfehler
durchwinkt.

Kanonischer Nachweis: **1549/1549** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Importierte Skalartypen sind wieder die, die VB6 hat (04.09.2026)

Ein Wegwerfprogramm über `stdole2.tlb` — 26 Aliase, 3 Records, 5 vtable-Interfaces, alles
registriert vorhanden — gab acht Zeilen aus, von denen drei falsch waren:

```
OLE_COLOR   UInt32  20      ' VB6: Long, 3
OLE_HANDLE  Empty   0       ' VB6: Long, 3
GUID.Data1  UInt32  20      ' VB6: Long, 3
GUID.Data2  UInt16  18      ' VB6: Integer, 2
```

`OLE_HANDLE` ist `VT_INT`, und die Konstante fehlte im Importer schlicht — der Typ fiel auf
Variant durch, `Debug.Print` gab eine leere Zeile aus. `VT_UINT` fehlte genauso.

Der andere Fall ist der interessantere: `VT_UI4` wurde auf `UInteger` abgebildet, also auf eine
**moderne Erweiterung dieses Projekts**. VB6 hat keinen vorzeichenlosen 32-Bit-Typ und bildet
`VT_UI4` auf `Long` ab; so steht `stdole.GUID` auch im Objektkatalog. Der Unterschied ist nicht
kosmetisch: `VarType` antwortete 20, und 20 ist in VB6 `vbLongLong`. Ein Altprogramm mit
`If VarType(x) = vbLong` nahm den falschen Zweig. Damit verletzte die Abbildung genau die Regel,
die alles andere schlägt — Erweiterungen kommen additiv dazu und verschieben nie die Semantik für
alten Code. `Byte` bleibt die Ausnahme, denn `Byte` **ist** der vorzeichenlose 8-Bit-Typ von VB6.

Ein bestehender Test hatte `UInteger`/`UShort` festgeschrieben. Er sprach damit keine VB6-Zusage
aus, sondern unsere Abbildungsentscheidung — und pinnte einen Typ, den VB6 gar nicht kennt; das
allein entscheidet die Frage. Der Test trägt die Begründung jetzt im Text.

Offen und ausdrücklich notiert: `stdole.GUID.Data4` ist ein `VT_CARRAY` und kommt weiterhin als
`Object` an. `g.Data4(0)` übersetzt deshalb als spät gebundener indizierter Zugriff und reißt zur
Laufzeit mit einer `NullReferenceException` ab, statt einen Wert oder eine Meldung zu liefern.
Das ist die nächste Karte.

Kanonischer Nachweis: **1550/1550** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Ein festes C-Array in einem importierten Record ist jetzt ein Array (04.09.2026)

`stdole.GUID.Data4` ist in VB6 `Data4(0 To 7) As Byte`. Bei uns kam es als blankes `Object` an, und
`g.Data4(0)` wurde damit zu einem spät gebundenen indizierten Zugriff auf `Nothing` — das Programm
brach mit einer `NullReferenceException` aus `VBDynamicDispatch.RequireTarget` ab. Keine Meldung,
keine Zeile, kein Bezug zur Deklaration.

Die Grenzen stehen in der `ARRAYDESC` hinter dem `VT_CARRAY`. Ihr Offset ist der wenig offensichtliche
Teil: die Bounds folgen dem Element-`TYPEDESC` und einer Dimensionszahl, ausgerichtet auf die
vier Byte einer `SAFEARRAYBOUND` — also `SizeOf(TYPEDESC) + 4` auf beiden Architekturen, **nicht**
die verwaltete Größe einer nachgebauten Struktur. Ein Member mit Grenzen ist im Symbolmodell längst
vorgesehen (`UserDefinedTypeMemberSymbol.ArrayBounds`); es fehlte nur der Weg dorthin.

Gemessen gegen die registrierte `stdole2.tlb`:

```
TypeName(g.Data4(0))  ->  Byte
LBound / UBound       ->  0 / 7
h = g : h.Data4(3)=5  ->  g bleibt 200
```

Die letzte Zeile ist die wichtigere: Eine UDT-Wertkopie kopiert auch ihre Arrays, und das gilt jetzt
auch für einen Record, dessen Grenzen aus einer Typbibliothek stammen.

Ausdrücklich offen: `VT_PTR` kommt weiterhin als `Object` an, **ohne** Diagnose. Das verletzt
„melden statt raten", und die Karte `l1-03-j` bleibt deshalb `partial`.

Kanonischer Nachweis: **1551/1551** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **70/70**.

## `New` auf einer importierten Coklasse legt jetzt ein Objekt an (04.09.2026)

`Set d = New Scripting.Dictionary` — gewöhnlicheres VB6 gibt es kaum — scheiterte beim Emittieren
mit `VB6E0001: Class 'Scripting.Dictionary' has no managed constructor`. Der Backend-Fehlerkanal
hat dabei richtig gearbeitet: es gab wirklich keinen Konstruktor, weil die Klasse nicht von dieser
Übersetzung stammt. Nur hatte ihm nie jemand gesagt, dass er stattdessen die registrierte Coklasse
aktivieren soll. Jedes `New` auf eine importierte Klasse war damit ein Übersetzungsfehler.

Der Weg liegt jetzt im Lowerer, nicht im Emitter: Trägt die importierte Klasse eine Klassen-Id, wird
`New` zu einem Laufzeitaufruf `VBInteraction.CreateComInstance`. Der Emitter muss von
COM-Aktivierung nichts wissen. Die Id kommt aus der Typbibliothek und wird nur für eine **erzeugbare**
Coklasse gesetzt (`TYPEFLAG_FCANCREATE`); eine Schnittstelle bekommt keine, und `New` darauf bleibt
abgewiesen. Schlägt die Aktivierung fehl, meldet die Runtime **429** statt einen Platzhalter
zurückzugeben — sonst wandert der Fehler zum ersten Memberzugriff und verliert seine Ursache.

Dahinter kam sofort der zweite Befund: `f.Size = 9` auf einem `StdFont` riss mit
„Type 'VB6.Runtime.VBCurrency' cannot be marshalled to a Variant. Type library is not registered"
ab. Die Meldung liest sich wie ein Registrierungsproblem und ist keines. `stdole.FONTSIZE` ist
`VT_CY`, der Wert also ein `VBCurrency` — eine Struktur dieser Runtime, die so nicht in eine VARIANT
passt. Gemessen an der Grenze: `VBComDispatch.TryInvoke` lieferte für das Setzen **False** und der
Reflexionsweg dahinter warf. Wieder ein Ersatzweg, der eine Lücke im Hauptweg verdeckt hat.

Beide Wege gehen jetzt durch `VBComValue.ToAutomation` — eine Stelle, an der `VBCurrency` zu
`Decimal` und `VBDateValue` zu `DateTime` wird, statt an jedem Aufrufort einzeln.

Gemessen gegen registrierte Komponenten:

```
New stdole.StdFont            -> Courier New / 9 / False
New Scripting.Dictionary      -> Count 2, Item("b") 2, Exists("a") True
New Scripting.FileSystemObject-> GetExtensionName(...) = txt
```

Noch offen und gemeldet, nicht still: `For Each` über eine COM-Collection (`d.Keys`) wird mit
`VB6S0055` abgewiesen.

Kanonischer Nachweis: **1553/1553** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **70/70**.

## `For Each` über einen Variant oder ein COM-Objekt (04.09.2026)

`For Each k In d.Keys` auf einem `Scripting.Dictionary` meldete `VB6S0055 – For Each collection type
'Variant' is not an array or Collection in the current compiler subset`. Der Binder wollte die Art
der Quelle zur Übersetzungszeit kennen. VB6 will das nicht: Ein Variant trägt, was er trägt — ein
Array, eine `Collection` oder ein Objekt mit `_NewEnum` —, und VB6 fragt den Wert zur Laufzeit.

Die Laufzeit konnte das längst. `VBInteraction.EnumerateControls` zählt eine COM-Collection über
den RCW bereits auf, samt der Sonderbehandlung für IEnumVARIANT-Implementierungen mit einem
überzähligen VT_EMPTY-Eintrag. Es fehlte nur der Weg dorthin — wieder der Fall „die Umsetzung ist
weiter als ihre Absicherung".

Der neue Weg hat trotzdem eine eigene Laufzeitfunktion bekommen, denn er braucht ein anderes
Fehlerverhalten: Für die Controlsammlung ist eine leere Antwort ein zulässiges Ergebnis — headless
gibt es keine Controls. Für einen Variant ist sie es nicht. `EnumerateObjectValues` meldet deshalb
**438**, wenn der Wert keinen Enumerator hat, und **91** für `Nothing`.

Gemessen (Array-Variant, Collection-Variant, `Dictionary.Keys`, `Dictionary` selbst über
`_NewEnum`) — alle vier zählen jetzt auf.

**Vorbestehender Befund, hier sichtbar geworden:** `On Error Resume Next` schützt den Kopf einer
`For Each`-Schleife nicht. Das gilt für jede Kontrollflussanweisung — `If`, `For`, `While`,
`Select Case`, `With` —, weil eine geschützte Region keinen Basisblock überqueren darf und der
Lowerer sie deshalb ausnimmt (`CanProtectForErrorHandling`). Der Fehler erreicht den Handler erst
über die aufrufende Anweisung. Der Test misst die Nummern deshalb aus einer gerufenen Prozedur
heraus. Das ist eine offene Lücke, keine Entscheidung — sie steht jetzt in `CLAUDE.md`.

Unverändert bleibt die Quelle vom Typ `Object`: Sie geht weiter den Controlsammlungs-Weg und
antwortet leer statt mit 438. Ohne Orakel wird das nicht auf Verdacht umgestellt.

Kanonischer Nachweis: **1556/1556** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **70/70**.

## Ein Bild aus der `.frx` erreicht ein natives Control als Bild (04.09.2026)

Die Untertüten aus der vorigen Karte haben eine Lücke sichtbar gemacht, die vorher unerreichbar war:
`Images.ListImage1.Picture` kam beim Control an — als **Base64-Zeichenkette**. Das Ergebnis war kein
fehlendes Bild, sondern `0xC0000005`: Das Control las die Zeichenkette als Schnittstellenzeiger, und
der Prozess starb.

Die Messung zeigte, warum die Tüte das nicht selbst abfangen kann. Ein registriertes ImageList
fordert auf ListImage-Ebene für **alle drei** Eigenschaften denselben Typ an:

```
READ Images.ListImage1.Picture  angefordert=null
READ Images.ListImage1.Key      angefordert=null
READ Images.ListImage1.Tag      angefordert=null
```

`null` heißt „gib mir, was du hast". Für `Key` ist eine Zeichenkette richtig, für `Picture` tödlich,
und der Container kann die beiden nicht unterscheiden. VB6 legt dort auch nie eine Zeichenkette ab —
es speichert das Bildobjekt. Die Wandlung gehört also vor die Tüte, in den Host: eine `.frx`-Nutzlast
wird über `AxHost.GetIPictureDispFromPicture` zu einem `IPictureDisp`. Was sich nicht wandeln lässt,
wird **weggelassen** statt weitergereicht — dann fehlt ein Bild, statt dass ein Prozess stirbt.

Die Runtime hat zusätzlich eine Regel bekommen, die für sich richtig ist: Schlägt die Wandlung auf
den angeforderten Typ fehl, meldet die Tüte „nicht gefunden" statt `S_OK` mit einem Wert anderer
Form. Ein `S_OK` mit falscher Form ist kein kleineres Scheitern als gar keine Antwort, sondern ein
größeres.

Gemessen an einem registrierten ImageList: `Count = 1`, `Key = rot`, `Picture` ist ein Bildobjekt.

Kanonischer Nachweis: **1557/1557** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **71/71**.

## Korrektur: `IPersistStreamInit` blockiert den RichTextBox-Text nicht (04.09.2026)

Zwei Einträge weiter oben steht, der Text einer RichTextBox hänge an `IPersistStreamInit` und fehle
deshalb. Das ist falsch, und die Herkunft des Irrtums ist lehrreich: In der ersten Messung hatte ich
`Text` gesetzt und leer zurückbekommen. `Text` ist aber gar nicht die persistierte Eigenschaft — der
Designer schreibt `TextRTF`, und in der `.frm` des Korpus steht genau das
(`TextRTF = $"frmInfo.frx":2CFA`).

Ein Protokoll der Lesewünsche zweier Stock-Controls klärt es:

```
RICHTEXT.RichtextCtrl.1   ... READ TextRTF   angefordert=null
MSComctlLib.Toolbar.2     ... READ Buttons   angefordert=__ComObject
                              READ Buttons.NumButtons  angefordert=Int16
```

Beide bieten ihren vollständigen Zustand über die Eigenschaftstüte an. Nachgemessen über den Host:
`TextRTF` kommt an und überlebt die Übergabe des ganzen Zustands. Unter den elf gemessenen
Stock-Controls braucht **keines** `IPersistStreamInit`; die Schnittstelle bleibt offen für Controls,
die ihren Zustand ausschließlich als Strom führen, und das ist eine deutlich schmalere Aussage als
die zurückgezogene.

Roadmap und `CLAUDE.md` sind entsprechend berichtigt.

Kanonischer Nachweis: **1558/1558** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **72/72**.

## Der Kopf einer `For Each` trägt jetzt seine Fehlerregion (04.09.2026)

Aus der vorigen Karte blieb ein Befund offen: Ein 438 aus der Aufzählungsquelle beendete das
Programm, obwohl ein Handler danebenstand. Ich hatte das als strukturelle Grenze notiert — eine
geschützte Region darf keinen Basisblock überqueren, also seien Kontrollflussanweisungen ungeschützt.

Das war zur Hälfte falsch. `LowerProtectedHeader` gibt es seit Langem und umschließt genau die
Kopfinstruktionen, die im Ausgangsblock bleiben; `For`, `Do` und die Bedingung eines `If` benutzen
es, und es beantwortet auch die Frage, die ich für offen hielt: Bei `Resume Next` wird am
**Schleifenausgang** fortgesetzt. Nur `LowerForEach` benutzte es nicht.

Der erste Versuch, es generisch in `LowerStatement` nachzurüsten, hat den Korpus mit „Nested error
handling regions are not supported" zerlegt — die Umklammerung legte sich um die Regionen, die der
Helfer bereits erzeugt. Der Emitter hat den Fehler dabei genau richtig gemeldet, mit Bezug auf die
Ursache. Die Rücknahme und der Weg über den vorhandenen Helfer waren fünf Zeilen.

Gemessen, beide Formen:

```
On Error Resume Next : 438, Schleife übersprungen, danach läuft eine gute Quelle weiter
On Error GoTo        : Handler sieht 438
Set n = Nothing      : 91
```

`CLAUDE.md` führte die Grenze seit heute Vormittag zu breit; der Eintrag ist berichtigt und nennt
jetzt den Helfer samt der Falle, in die der generische Versuch lief.

Kanonischer Nachweis: **1559/1559** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **72/72**.

## Zeiger und SCODE in einem importierten Record (04.09.2026)

Zwei weitere Automationstypen fehlten in der Abbildungstabelle, mit demselben Muster wie `VT_INT`
vorher: Sie fielen durch und wurden still zu Variant beziehungsweise Object.

```
stdole.EXCEPINFO.scode       vt=10 (VT_ERROR)  ->  Empty,   VarType 0   ' VB6: Long, 3
stdole.EXCEPINFO.pvReserved  vt=26 (VT_PTR)    ->  Nothing, VarType 9   ' VB6: Long, 3
```

Ein Zeiger, der dort steht, wo ein Wert steht, ist in VB6 ein `Long` — auf der 32-Bit-Zeigerbreite,
die die Sprache hat. Als `Object` antwortete er beim Lesen `Nothing`, und `x.pvReserved = 0` boxte
geräuschlos eine Zahl hinein.

Zwei Fehlversuche auf dem Weg dorthin, beide vom kanonischen Lauf gefangen:

1. `VT_PTR` **generell** auf `Long` abzubilden riss `font.Name = "Courier New"` mit einer
   `FormatException`. Ein Zeiger steht in einer Typbibliothek auch für **ByRef** — der Getter von
   `stdole.IFontDisp.Name` ist als `VT_PTR` auf BSTR beschrieben. Die Wertform wird deshalb dort
   entschieden, wo sie als solche bekannt ist: in `ImportRecordMembers`.
2. `VT_HRESULT` mitzunehmen riss denselben Fall erneut. `VT_HRESULT` ist der **Rückgabetyp jedes
   Dispinterface-Getters**; sein Wert reist im retval-Parameter. Abgebildet wurde damit jede
   Eigenschaft zur Zahl. `VT_ERROR` bleibt abgebildet, `VT_HRESULT` bewusst nicht.

Zur Zeiger-auf-Zeiger-Form, die die Karte als „explizit" verlangt: Sie bleibt ein ausdrücklich
gesetzter opaker Objektkontrakt **ohne** Diagnose. Die Messung über vier registrierte Bibliotheken
begründet das — die Form kommt dort in 144 bis 838 Parametern vor, praktisch ausschließlich in der
`QueryInterface`/`GetIDsOfNames`-Boilerplate, die VB6-Code nie aufruft. Eine Diagnose darauf wäre
Rauschen, keine Warnung.

Damit steht `l1-03-j-typelib-alias-record-pointer-import` auf `implemented`; die Matrix führt
**89 implemented, 13 partial, 16 planned**.

Kanonischer Nachweis: **1559/1559** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **72/72**.

## `.pag` und `.dob` laufen jetzt nachweislich, nicht nur analysiert (04.09.2026)

Für PropertyPage- und UserDocument-Artefakte gab es Tests, die das Projekt **analysieren**. Ob die
Klassen sich erzeugen lassen und ihre Prozeduren laufen, stand nirgends. Sie tun es — wieder ein
Fall, in dem die Umsetzung weiter war als ihre Absicherung; der Test hält es jetzt fest.

Die erste Fassung des Tests behauptete zu viel: Sie prüfte den Designer-**Wert** eines Controls
(`txtName.Text`) und schlug fehl. Die Gegenprobe mit einer Form im selben Programm zeigt, dass es
nicht an den Artefakten liegt — ohne UI-Host verwirft `VBInteraction.SetMember` jeden
Designer-Wert, und für eine Form gilt genau dasselbe. Headless ist das gewollt. Geprüft wird
deshalb, was dort wahr ist: Die Klasse ist erzeugbar, ihre Prozedur läuft, und die Designer-Hülle
hat ihre Controls angelegt. Der Wert gehört in einen Hosttest.

Zur „Property Pages"-Zeile der Etappe F: Gemeint sind dort die Eigenschaftsseiten, die ein
**Container zur Entwurfszeit** eines fremden OCX anzeigt. Das ist IDE-Fläche, und die IDE ist in
diesem Projekt ausdrücklich zurückgestellt. Die kompilierbare Seite — das `.pag`-Artefakt des
eigenen Projekts — ist hiermit abgedeckt.

Kanonischer Nachweis: **1560/1560** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **72/72**.

## Eine nicht gesetzte Collection in `For Each` meldet 91 statt 5 (04.09.2026)

Gemessen über die drei Quellformen einer nicht gesetzten Objektvariablen:

```
Dim c As Collection : For Each e In c   ->  Err 5    ' VB6: 91
Dim o As Object     : For Each e In o   ->  Err 0    ' VB6: 91
Dim v As Variant : Set v = Nothing      ->  Err 91   ' richtig
```

Die 5 ist der Sammelwert „nicht zugeordnet" — `VBCollection.EnumerateValues` warf eine
`ArgumentNullException`, und `VBErrors.Set` bildet jede unbekannte Ausnahme darauf ab. Sie sieht wie
ein Ergebnis aus und ist keines. Der Fall ist jetzt ausdrücklich 91.

**Die `Object`-Quelle bleibt bewusst bei 0.** Der Versuch, sie mitzunehmen, riss den bestehenden
Test `EmitManagedApplication_EnumeratesHostObjectWithObjectControlVariable`, der das Schweigen für
den Controlsammlungs-Weg als Zusage festschreibt — und dort ist eine leere Antwort für einen Host
ohne Controls legitim. Die Projektregel ist eindeutig: Ein bestehender Test, der eine Vertragszusage
ausspricht, schlägt eine Herleitung; die Änderung wird zurückgenommen und die Frage notiert. Die
Unstimmigkeit zu dem Variant-Fall, der 91 meldet, steht damit als offene Frage im Code statt als
stille Entscheidung.

Nebenbefund aus derselben Messung, vorbestehend und **nicht** von der `For Each`-Erweiterung
verursacht: `Controls` und `Me.Controls` lösen ohne UI-Host überhaupt nicht auf und melden 438 —
schon `TypeName(Controls)` tut es. Das ist eine Host-Frage, keine Schleifenfrage.

Kanonischer Nachweis: **1561/1561** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **72/72**.

## Das emittierte VISIA läuft — und zeigte den falschen Fenstertitel (04.09.2026)

Der Korpus wurde bisher analysiert und emittiert. Ob er **läuft**, stand nirgends. Er tut es: Der
emittierte `Visia.exe` startet, hält die Nachrichtenschleife, legt neun Fenster an, zwei davon
sichtbar, und schreibt nichts auf `stderr`.

Im Titelbalken des Splashfensters stand dabei `__vb6_class_frmSplash`. `WinFormsHost` setzte
`Name` und `Text` einer Form aus `target.GetType().Name` — also aus dem Namen, den der **Emitter**
vergibt. `VBFunctions` trägt für genau diese Frage schon einen Helfer, samt Begründung im Kommentar:
„sonst wird das Namensschema des Emitters zu beobachtbarem Programmverhalten". Der Host benutzte ihn
nur nicht; er ist jetzt öffentlich und wird benutzt. Eine `.frm` ohne Caption — ein randloses
Splashfenster etwa — zeigt damit `frmSplash`, wie in VB6.

**Eine Fehlmessung auf dem Weg dorthin, der Vollständigkeit halber:** Zwischendurch schien jeder
Fenstertitel auf ein einzelnes Zeichen verkürzt zu sein (`'H'` statt `'Hallo Welt'`). Das lag an
meinem Messprogramm: `GetWindowTextW` ohne `CharSet.Unicode` marshallt den `StringBuilder` als ANSI,
und aus einer UTF-16-Zeichenkette wird so das erste Zeichen. Mit korrektem Marshalling stimmten die
Titel. Ein Messfehler sieht einem Compilerfehler zum Verwechseln ähnlich — die Gegenprobe mit einem
minimalen Programm hat ihn entlarvt, bevor daraus eine Karte wurde.

Kanonischer Nachweis: **1562/1562** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **73/73**. Zusätzlich: `Visia.exe` startet und zeigt sein
Splashfenster.

## Der Korpus zeigt jetzt seine Oberfläche (04.09.2026)

Nachdem `Visia.exe` das erste Mal startete, blieb es dauerhaft auf dem Splashfenster stehen. Die
Kette dahinter hatte fünf Glieder, und jedes einzelne war ein eigener Defekt.

**1. Der Timer feuerte nie.** `TimerControl` startete mit `Enabled = false`; in VB6 ist die Vorgabe
**True**, und eine `.frm` schreibt oft nur `Interval`. Dazu kommt: `Interval = 0` heißt in VB6
„feuert nicht", während ein WinForms-Timer keine 0 annimmt. Der VB6-Zustand wird deshalb getrennt
gehalten und beim Ändern beider Werte zusammengesetzt.

**2. Das Programm endete beim Entladen der Startform.** `Application.Run(form)` bindet die
Nachrichtenschleife an *diese* Form. VB6 endet, wenn die **letzte** Form entladen ist. Die Schleife
gehört jetzt einem `ApplicationContext`, der verlassen wird, sobald keine Bindung mehr steht — aus
dem `Unload`-Pfad und aus `FormClosed` gleichermaßen.

**3. Ein unbehandelter Fehler bot „Weiter" an.** Der WinForms-Standard fängt die Ausnahme auf dem
UI-Thread ab und zeigt einen Dialog mit einer Wahl, die VB6 nie gibt — und versteckt die Diagnose
hinter „Details". Die Anwendung meldet und endet jetzt, wie VB6 es tut.

**4. Jeder Fehler verlor seine Herkunft.** Fünf Stellen taten `throw exception.InnerException`,
was den Stack an Ort und Stelle neu beginnt. Ein Fehler aus der Nachrichtenschleife kam mit drei
Rahmen an — der Brücke, ihrem Aufrufer und `Main` — und sagte nichts darüber, woher er stammte.
Über `ExceptionDispatchInfo` bleibt der Ursprungsstack erhalten; erst damit war der nächste Punkt
überhaupt auffindbar.

**5. Die Default-Instanz einer Form fehlte.** `frmMain.Show` ist die übliche VB6-Art, ein zweites
Fenster zu öffnen: Eine Form trägt `VB_PredeclaredId`, ihr Name *ist* eine Instanz. Diese Behandlung
gab es nur für `.cls`-Klassen; die globale Variable einer Form blieb `Nothing`. Ein UserControl hat
in VB6 keine Default-Instanz und bekommt hier auch keine.

**6. Designer-Ereignisse feuerten während des Aufbaus.** Danach starb `frmMain` in
`conInTab_Resize`: WinForms meldet `Resize`, sobald eine Größe zugewiesen wird, und der Handler griff
auf ein `Line`-Control zu, das die Designer-Hülle zwei Zeilen weiter unten erst noch anlegen wollte.
VB6 legt eine Form zuerst aus und lässt das Programm danach laufen. Die Hülle hat jetzt beide Enden
als ausdrücklichen Vertrag — `BeginDesignerInitialization` und `CompleteDesignerInitialization` —,
und dazwischen läuft kein Ereignis.

Der erste Entwurf öffnete die Hülle **implizit** beim ersten `CreateControl`. Das riss sechs
Hosttests: Wer Controls direkt anlegt, schließt nie und verlöre damit für immer jedes Ereignis.
Beide Enden gehören dem erzeugten Programm.

Ergebnis: `Visia.exe` startet, zeigt zwei Sekunden den Splash, übergibt an „Visia Compiler" und
stellt dessen Oberfläche her — Menü, Project View, Properties, Toolbox.

Kanonischer Nachweis: **1565/1565** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems; nativ unter x86 **75/75**.

## Zeichenbreite und Beschneidung schließen die Zeichenkarten (04.09.2026)

`l1-04-h` war vollständig da, nur nicht als erfüllt geführt: Alle sechzehn ROP2-Modi werden pro
Farbkanal gegen eine Referenztabelle geprüft, auf dem aktiven Paint-Kontext **und** auf der
persistenten AutoRedraw-Fläche, und `DrawMode = 17` meldet 380.

`l1-04-f` hatte eine echte Lücke: **`DrawWidth` gab es im Host überhaupt nicht.** Beide Stifte
standen fest auf einem Pixel, und das Setzen der Eigenschaft wurde nicht einmal beantwortet — ein
Programm, das eine dicke Linie zeichnen wollte, bekam eine haarfeine, ohne Hinweis. Jetzt trägt der
Zeichenzustand die Breite, `Line`, `Circle` und `PSet` benutzen sie, und ein Wert außerhalb
1..32767 meldet 380 statt sich stillschweigend etwas Nahes auszusuchen. `PSet` setzt dabei wie in
VB6 ein Quadrat von `DrawWidth` Pixeln um den Punkt.

Die Zusage „clipping" war bereits erfüllt und ist jetzt gemessen: Eine Linie von -500 bis +500 auf
einer 20-Pixel-Fläche wird beschnitten, ein Punkt weit außerhalb hinterlässt nichts und reißt
nichts ab.

Matrix: **91 implemented, 13 partial, 14 planned**; 104/118 documented-verified.

Kanonischer Nachweis: **1567/1567** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Jede ActiveX-DLL mit COM-Hosting riss den Compiler ab (04.09.2026)

Beim Schließen der MSBuild-Karten fiel auf, dass die erzeugte `.tlb` nicht im Zielgraphen steht:
`Clean` ließ sie liegen, und ein früh gebundener Client band danach weiter gegen eine veraltete
Bibliothek. Der Test dafür deckte etwas Schwereres auf.

```
vb6c ComSdk.vbp --emit-assembly out\ComSdk.dll --com-host
-> Unhandled exception. System.IO.FileLoadException:
   The assembly architecture is not compatible with the current process architecture.
```

`ManagedTypeLibraryWriter` **lud** die eben emittierte Assembly, um ihre COM-Klassen zu lesen. Ein
Legacy-`.vbp` defaultet auf x86, `vb6c` läuft als x64 — der Ladevorgang scheitert grundsätzlich.
**Jede** VB6-ActiveX-DLL mit COM-Hosting endete so, mit einer unbehandelten Ausnahme statt einer
Diagnose. Der bestehende Test emittierte offenbar nie mit `--com-host` aus einem `.vbp`.

Gelesen wird jetzt über einen `MetadataLoadContext`: Metadaten statt Ausführung, unabhängig von der
Architektur. Das erledigt nebenbei den Grund für den bisherigen Umweg — die Datei wurde vorher in
ein `%TEMP%`-Verzeichnis kopiert, weil ein ausführender Ladekontext sie bis zur nächsten
Speicherbereinigung gesperrt hält. Die Kopie und der eigene `AssemblyLoadContext` sind entfallen.
Attribute werden dabei als `CustomAttributeData` gelesen, denn ein Kontext, der nichts ausführt,
kann kein Attributobjekt bauen.

Gegenprobe an der erzeugten Datei: `LoadTypeLibEx` meldet `S_OK`, die Bibliothek heißt `ComSdk` und
trägt das Dispinterface `_Rechner` mit `Verdopple` und die Coklasse `Rechner` — inhaltlich dasselbe
wie über den Reflexionsweg vorher.

Damit stehen `l1-04-p` und `l1-04-q` auf `implemented`: Inkrementalität, Clean, Rebuild,
Ausgabenabgleich und die `.tlb` laufen gegen echte `dotnet msbuild`-Aufrufe des gepackten SDK, und
`DesignTimeBuild` wertet aus, ohne zu emittieren.

Matrix: **93 implemented, 13 partial, 12 planned**; 106/118 documented-verified.

Kanonischer Nachweis: **1568/1568** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Auch der Manifest-Schreiber lud die Assembly (04.09.2026)

Derselbe Defekt wie beim TypeLib-Schreiber, eine Datei weiter: `ManagedComManifestWriter` las die
COM-Klassen ebenfalls über einen ausführenden Ladekontext, und `--com-manifest` auf einem
Legacy-`.vbp` endete deshalb mit derselben unbehandelten `FileLoadException`. Beide lesen jetzt
Metadaten; die eigenen `AssemblyLoadContext`-Klassen sind entfallen.

Der Test dazu deckt genau die Architekturgrenze ab, an der beide scheiterten: eine ActiveX-DLL,
ausdrücklich für **x86** emittiert, mit `.tlb` **und** Manifest. Das Manifest nennt dabei
`processorArchitecture="x86"` — die Bitness folgt der Ausgabe, nicht dem Compilerprozess.

Damit sind vier weitere Karten belegt und geschlossen:

- `l1-03-m` — 25 Ausführungstests über Skalarbreiten, ANSI/BSTR-Grenzen mit Rückschreiben,
  As-Any-Zeiger und UDT-Layout samt Vier-Byte-Packung.
- `l1-03-n` — AddressOf liefert einen LongPtr mit passend erzeugter Delegatsignatur, und die
  Callback-Registry hält ihn über den nativen Aufruf am Leben.
- `l1-03-o` — neu gemessen: Die Variant-Zustandsmarker **Empty**, **Null** und **Nothing**
  überstehen eine echte `MarshalAs(SafeArray, VT_VARIANT)`-Grenze als drei verschiedene Zustände,
  statt zu einem „nichts" zu verschmelzen. Das war die einzige Zusage der Karte ohne Nachweis.
- `l1-03-k` — Bounds, Rückschreiben und die VARTYPE-Zuordnung für Currency, LongPtr, Dispatch und
  Unknown sind über die Dispatch- und Declare-Puffertests abgedeckt.

Matrix: **97 implemented, 13 partial, 8 planned**; 110/118 documented-verified.

Kanonischer Nachweis: **1570/1570** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.

## Die Projekt-Ressourcendatei fehlte ganz (04.09.2026)

`ResFile32=` wurde vom Projektlader stillschweigend verworfen, und `LoadResString` gab es nicht —
ein Altprojekt mit Ressourcendatei scheiterte an „Variable ist nicht deklariert", was auf den
Aufruf zeigt statt auf die fehlende Fläche. Die Karte `l1-04-b` nennt `.res` samt Nutzlast
ausdrücklich, also ist es gebaut, nicht nur gemeldet.

Der Weg folgt VB6: Dort werden die Ressourcen in die ausführbare Datei **gelinkt**, nicht daneben
gelegt. Der Emitter bettet die Bytes deshalb als verwaltete Ressource ein
(`ManagedPEBuilder.managedResources`), und die Runtime liest sie aus der laufenden Assembly. Ein
ausgeliefertes Programm braucht die `.res` damit nicht mehr.

Die Adressierung ist der Teil, den man leicht falsch macht: `LoadResString(id)` benennt **keine**
Zeichenkettenressource. Win32 legt Zeichenketten in Blöcken zu sechzehn ab; die Blockkennung ist
`id \ 16 + 1`, die Position darin `id Mod 16`. Wer einen Block als eine Zeichenkette liest, bekommt
die ganze Tabelle.

Eine fehlende Kennung meldet **326**, keine leere Zeichenkette. Eine fehlende Ressourcendatei
meldet der Compiler gegen die Projektzeile, statt jeden Aufruf im Programm mit 326 antworten zu
lassen.

Gemessen an einer von Hand gebauten `.res` mit einer Zeichenkettentabelle: `LoadResString(0)` →
"Hallo", `LoadResString(1)` → "Welt", `LoadResString(500)` → 326.

Damit sind vier weitere Karten geschlossen:

- `l1-03-p` — Identitäten sind aus den Namen abgeleitet, nicht erzeugt: zwei Übersetzungen derselben
  Quelle ergeben dieselbe CLSID, dieselbe ProgID und eine **bytegleiche** Typbibliothek.
- `l1-03-q` — `.tlb`, reg-freies Manifest mit der Bitness der Ausgabe, und der Local Server.
- `l1-04-a` — die Kodierungszusage war die letzte offene: `VB6TextFile` liest BOMs für UTF-8/16/32
  und fällt sonst auf Windows-1252 zurück, wie VB6 es geschrieben hat. Vorhanden war das längst,
  geprüft nicht.
- `l1-04-b` — alle Designer-Artefaktarten plus die Ressourcendatei.

Matrix: **101 implemented, 13 partial, 4 planned**; 114/118 documented-verified.

Kanonischer Nachweis: **1576/1576** Tests, **0** Fehler, Release ohne Warnungen, **40/40**
VISIA-Projektitems.
