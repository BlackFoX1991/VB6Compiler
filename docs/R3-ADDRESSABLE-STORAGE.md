# R3 — Adressierbarer Speicher: Entwurfsvertrag

Stand: 2026-09-07. Dieses Dokument zerlegt die R3-Speicherkarten; es erweitert weder den bereits geprüften kurzfristigen `Declare`-Pfad
noch erklärt ihn zum Ersatz für gespeicherte Zeiger.

## Ausgangspunkt

`VarPtr` und `StrPtr` geben Werte vom Typ `Long` zurück. Der klassische Vertrag ist damit x86;
eine AnyCPU- oder x64-Ausführung darf keinen abgeschnittenen Zeiger als Erweiterung ausgeben.
Der heutige Emitter unterstützt deshalb im allgemeinen Fall nur die Stelle, an der ein `ByVal As
Any`-`Declare` den Zeiger unmittelbar konsumiert. `StrPtr` erhält dafür einen temporären UTF-16-
Puffer; Skalare gehen direkt als ByRef-Adresse in den Aufruf. Dieser Puffer ist nach der Rückkehr
ungültig und erfüllt den Speichervertrag eines gespeicherten Zeigers nicht.

Als erste, absichtlich kleine gespeicherte-Ausnahme erzeugt die x86-Managed-Emission für
`VarPtr(localLong)`, `VarPtr(localLongLong)`, `VarPtr(localLongPtr)`,
`VarPtr(localInteger)`, `VarPtr(localUShort)`, `VarPtr(localUInteger)`, `VarPtr(localULong)`,
`VarPtr(localByte)`, `VarPtr(localBoolean)`, `VarPtr(localSingle)`, `VarPtr(localDouble)`,
`VarPtr(localDate)` und `VarPtr(localCurrency)` native Vier-, Acht-, Vier-, Zwei-, zwei-, Vier-,
Acht-, Ein-, zwei-, vier-, acht-, acht- und acht Byte breite Zellen. Der Boolean-Zellwert verwendet
dabei VB6s `-1`/`0`-Darstellung statt eines CLR-`bool`; UShort, UInteger und ULong nutzen
vorzeichenlose zwei, vier beziehungsweise acht Byte breite Layouts, LongLong ist ein direkter
signierter 64-Bit-Wert, LongPtr nutzt im x86-Pfad explizit vier Byte, Single und Double nutzen ihre
unveränderten IEEE-754-Layouts, Date das acht Byte breite OLE-Automation-Datum und Currency seinen
mit 10.000 skalierten `Int64`.
Normale Loads/Stores sowie CLR-ByRef-Write-backs werden mit diesen Zellen synchronisiert, und sie
werden bei der Prozedurrückkehr freigegeben. Sie überstehen damit eine GC, solange der lokale
Speicherplatz lebt. AnyCPU und x64 behalten für denselben Ausdruck Fehler 5; dort wird kein
`IntPtr` in einen `Long` abgeschnitten.

Zusätzlich erhält `StrPtr(localString)` im x86-Managed-Pfad eine von der Runtime besessene BSTR-
Zelle. Ihr Wert und ihre UTF-16-Zeichen sind über GC stabil und native Zeichenänderungen werden
beim nächsten String-Load sichtbar. Eine Zuweisung eines neuen String-Werts ersetzt die BSTR und
invalidiert die frühere Adresse kontrolliert; nach der Prozedurrückkehr wird die aktuelle BSTR
freigegeben. Dieser Local-Slice umfasst weder BSTR-Felder/Parameter/Arrays noch einen
gespeicherten `VarPtr` eines String-Descriptors.

Der zweite Slice nimmt dieselben Layoutfamilien für **Modulvariablen**. Der Unterschied liegt
nicht im Layout, sondern in der Lebensdauer: Die Zelle ist ein statisches Begleitfeld unmittelbar
hinter ihrem Datenfeld und lebt so lange wie das Programm. Sie entsteht **faul an der
`VarPtr`-Stelle** und übernimmt dabei den aktuellen Wert des Feldes. Das ist keine Bequemlichkeit,
sondern die einzige Stelle, an der die Zelle überhaupt entstehen kann: Der Lowerer baut die Module
nacheinander, eine im späteren Modul entdeckte Zelle passt nicht mehr in die bereits gebaute
Globals-Liste eines früheren, und ein Modulinitialisierer müsste über Modulgrenzen hinweg geordnet
werden. Deshalb sind die Zugriffspfade null-tolerant: Vor dem ersten `VarPtr` ist allein das
gewöhnliche statische Feld maßgeblich, danach die Zelle.

Erst damit wird sichtbar, was ein Local nicht zeigen kann: Eine *fremde* Prozedur, die die
Modulvariable gewöhnlich zuweist oder ByRef beschreibt, wird über den gespeicherten Zeiger
sichtbar. Ein Klassenfeld ist ausdrücklich **nicht** dabei — im Binder ist es ebenfalls ein
`ModuleVariableSymbol`, hat aber keinen statischen Speicherplatz und bleibt bei Fehler 5.

Ein `Static`-Local fällt ohne eigenen Aufwand mit hinein: Der Binder legt es als
`ModuleVariableSymbol` mit synthetischem Namen an, es ist also dieselbe Speicherfamilie. Es hat
die Zelle aber eine Zeit lang nur scheinbar bekommen. Weil sein Global in einem eigenen Modul
liegt und die adressierende Prozedur nicht, scheiterte der Zugriff auf eine `private` Begleitzelle
zur Laufzeit mit einer `FieldAccessException` — und die kommt als VB6-Fehler 5 heraus, also genau
als das, was eine fehlende Unterstützung ebenfalls meldet. Die Zelle trägt deshalb dieselbe
Sichtbarkeit wie das Datenfeld, zu dem sie gehört, und ein Emitter-Test vergleicht die beiden.

Der dritte Slice nimmt den **ByVal-Parameter**. Er ist in VB6 eine private Kopie, hat also seinen
eigenen Speicherplatz und damit dieselbe Lebensdauer wie ein Local: Die Zelle ist wieder ein
Local, entsteht beim Prozedureintritt und wird bei der Rückkehr freigegeben. Ein Unterschied
bleibt: Sie startet nicht mit Null, sondern mit dem Wert, mit dem das Argument ankommt.

Der **ByRef-Parameter** ist ausdrücklich nicht dabei, und zwar nicht aus Aufwandsgründen. Der
Zielvertrag unten verlangt, dass Aliase desselben Speicherplatzes dieselbe Zelle sehen: In VB6
liefert `VarPtr` auf einen ByRef-Parameter die Adresse des Aufrufers, nicht eine eigene. Im
erzeugten Code kommt beim Aufgerufenen aber nur ein Managed Pointer an, aus dem sich die Zelle
des Aufrufers nicht finden lässt. Eine eigene Zelle wäre eine zweite, entkoppelte Kopie — sie
würde eine Adresse liefern, die auf den falschen Speicher zeigt, und das ist schlechter als der
ausdrückliche Fehler 5. Der Fall braucht eine eigene Entwurfsrunde über die Aufrufkonvention.

Der vierte Slice nimmt den **flachen Datensatz** und ist die erste Familie, bei der nicht die
Lebensdauer neu ist, sondern das Layout. Die Zelle gehört immer dem **ganzen** Datensatz; ein
Memberzeiger ist die Blockadresse plus Offset. Anders herum ginge es nicht: `VarPtr(p) + 8` und
`VarPtr(p.Z)` müssen dieselbe Adresse sein, sonst ist der Zeiger für ein `CopyMemory` über den
ganzen Datensatz wertlos — und genau dafür wird er in Legacy-Code benutzt.

Größe und Offsets kommen deshalb aus **einer** Quelle, dem Interop-Marshaller: `Marshal.SizeOf`
ist dieselbe Zahl, die `LenB` schon beantwortet, und `Marshal.OffsetOf` liefert genau die Lage,
die ein `Declare` sieht. Der Emitter berechnet keine Offsets selbst; er reicht einen Pfad aus
CLR-Feldnamen durch. Gemessen an einem `Type` aus `Long`, `Integer`, `Double` und einem
geschachtelten Datensatz ergibt das 0, 4, 8, 16 und 20 — das Vier-Byte-Packing von VB6, mit zwei
Byte Füllung hinter dem `Integer`.

Daraus folgt eine zusätzliche Synchronisationsstelle, die es bei den Skalaren nicht gab: Eine
**Memberzuweisung** schreibt in den CLR-Speicherplatz, nicht in die Zelle. Jede Zuweisung an ein
Feld verfolgt ihre Empfängerkette deshalb bis zur Wurzel und schreibt den ganzen Datensatz
zurück; dasselbe gilt nach einem ByRef-Aufruf, der in ein Member hineingeschrieben hat.

Qualifiziert ist ein Datensatz, den der Marshaller ohne Nebenspeicher in einen flachen Block
bewegen kann: Skalare, `String * n` — das liegt inline — und geschachtelte Datensätze derselben
Form. Ein Member mit variabler `String`-Länge, ein Array oder ein Variant besitzt Speicher neben
dem Datensatz; sein Layout ist ein eigener Vertrag und bleibt bei Fehler 5.

Der fünfte Slice nimmt das **eindimensionale Arrayelement** und bricht dabei mit dem Muster der
vier davor: Er legt *keine* Zelle an. Der Grund ist, dass eine Arrayreferenz reist. Bei einem
Local, einer Modulvariablen, einem ByVal-Parameter und einem Datensatz ist der CLR-Speicherplatz
der einzige Zugang, und ein Abbild daneben lässt sich an vier bekannten Stellen nachziehen. Ein
Array wird dagegen als Referenz weitergereicht: Eine fremde Prozedur schreibt in dasselbe Objekt,
ohne dass an der Aufrufstelle etwas davon zu sehen wäre. Ein Abbild würde dort stillschweigend
veralten — und ein Zeiger, der auf veraltete Bytes zeigt, ist schlimmer als keiner.

Stattdessen wird der **eine** Speicher unbeweglich. Die Elemente werden ohnehin überall als
`ref T` in ein CLR-Array herausgereicht — daher funktioniert der unmittelbare `Declare`-Pfad
bereits für mehrere Elemente am Stück. Beim ersten gespeicherten Zeiger wandert der Inhalt in den
Pinned Object Heap; jede `ref`-Referenz darauf ist danach eine stabile native Adresse. Damit
entfallen sämtliche Synchronisationsstellen, und die Aliasfrage löst sich von selbst.

`ReDim Preserve` baut ein neues Array und lässt das alte zurück — genau VB6s Regel, dass eine
Reallokation die Lebensdauer des alten Zeigers beendet. `Erase` leert an Ort und Stelle und
behält ihn.

Zwei Grenzen blieben zunächst ausdrücklich offen: mehr als eine Dimension, und `VarPtr` auf das
ganze Array. Beide hat `managed-r3-safearray` aufgehoben, und zwar an derselben Stelle — der
gepinnte Puffer bekommt einen **echten oleaut32-Deskriptor**, der genau auf ihn zeigt.

Die Reihenfolge war der eigentliche Grund für die erste Grenze: SAFEARRAY-Daten laufen die
**linkeste** Dimension zuerst, die verwaltete Aufzählung die rechteste. Der Umzug in den Pinned
Object Heap transponiert deshalb genau einmal, und ab da wählt jeder Indexzugriff das native
Layout. Gemessen wird das nicht gegen die eigene Rechnung, sondern gegen `oleaut32`: Eine Sonde
füllt einen Deskriptor über `SafeArrayPutElement` und liest den Rohpuffer zurück; eine zweite
liest die Deskriptorbytes und macht damit sichtbar, dass die Bounds dort **rechts zuerst** stehen,
obwohl die API ihre Indizes links zuerst nimmt.

Der Deskriptor trägt `FADF_STATIC`, weil `pvData` ein CLR-Array ist und nicht ihm gehört.
`VarPtr` auf das ganze Array nennt diesen Deskriptor, nie das erste Datenbyte.

Welche Elementtypen das dürfen, entscheidet **eine** Regel statt einer Ausschlussliste: Das
`cbElements` des VARTYPE muss die Schrittweite des CLR-Puffers sein. VB6 `Boolean` fällt damit
heraus — es ist `VT_BOOL` und zwei Byte breit, der Speicher ist ein einbyteiges CLR-`bool`, und
ein Deskriptor daraus verspräche doppelt so viel Speicher wie da ist. Beide Zahlen sind gemessen,
nicht angenommen. BSTR, VARIANT und die Schnittstellenzeiger fallen ohne eigene Klausel heraus,
weil sie überhaupt keine flache Breite haben. Der Lowerer lehnt Boolean zusätzlich früher ab; die
beiden Prüfungen müssen zusammenbleiben, sonst wird aus einem gemeldeten Fehler 5 ein nativer
Überlauf.

Der sechste Slice nimmt das **private Instanzfeld**. Es verhält sich wie eine Modulvariable, nur
pro Objekt: Die Zelle ist ein Instanzfeld neben dem Datenfeld, entsteht faul an der
`VarPtr`-Stelle und wird von ihrem eigenen Finalizer freigegeben, wenn das Objekt weg ist —
weiter reicht VB6s Zusage für den Zeiger ohnehin nicht.

Der Empfänger ist dabei immer `Me`. Ein `Public`-Feld wird von außen als Property gebunden, ein
Feldzugriff von anderswo erreicht diese Form also gar nicht — womit auch die Frage entfällt, wie
ein zusammengesetzter Empfänger zweimal ausgewertet würde.

Ein **`Public`-Feld** bleibt ausdrücklich bei Fehler 5, und zwar nicht aus Aufwandsgründen: Die
späte Bindung liest ein öffentliches Feld per Reflection direkt aus dem CLR-Feld
(`VBDynamicDispatch`). Eine Zelle daneben wäre auf diesem Weg unsichtbar, und der Zeiger zeigte
auf etwas, das ein spät gebundener Schreibzugriff nie erreicht.

### Ein Speicherplatz, zwei Zeigerformen

Beim Messen dieses Slices kam ein Defekt der vorherigen heraus. Ein Speicherplatz kann beide
Formen tragen: den unmittelbaren `ByVal VarPtr(x)` eines `Declare` und einen gespeicherten
Zeiger. Die unmittelbare Form reicht die *verwaltete* Adresse des Speicherplatzes weiter, nicht
die der Zelle — der Aufgerufene schreibt also in den CLR-Platz. Das Rückschreiben in die Zelle
hing aber an `IrCallArgumentKind.Address`, und die unmittelbare Form trägt die Vorgabeart. Ihr
Schreibzugriff ging deshalb verloren, sobald derselbe Platz eine Zelle hatte.

Maßgeblich ist jetzt die Form des Ausdrucks (`IrAddressExpression`), nicht die Argumentart. Der
Fall betraf Locals und Modulvariablen genauso und hat einen eigenen Ausführungstest.

### Die beiden Adressen eines String-Speicherplatzes

Ein String-Speicherplatz ist zwei Dinge, und jedes hat seine eigene Intrinsic: Die Variable hält
einen Zeiger auf die BSTR. `StrPtr` beantwortet die BSTR, `VarPtr` die Adresse der Variablen. Die
Beziehung zwischen beiden ist dokumentiert und exakt — **`StrPtr(s)` ist der `Long`, der an
`VarPtr(s)` steht** — und damit prüfbar, ohne dass ein VB6-Orakel nötig wäre.

Die BSTR-Zelle besitzt deshalb beides: einen Deskriptorplatz, der den BSTR-Zeiger hält, und die
BSTR selbst. Der Deskriptor ist die maßgebliche Stelle — ein nativer Schreibzugriff, der den
Zeiger austauscht, ist beim nächsten VB6-Lesen sichtbar, genau wie einer in die Zeichen hinein.

Dabei fiel auf, dass der unmittelbare `ByVal VarPtr(s)`-Pfad eines `Declare` für einen String die
falsche Adresse lieferte: die *verwaltete* Adresse des Speicherplatzes, an der ein Objektzeiger
steht, nicht der Deskriptor. Für einen String fällt diese Form jetzt auf den gespeicherten Zeiger
durch, damit beide Formen dieselbe Adresse nennen.

### Freigabe bei Prozedurende

Abnahmeschritt 2 verlangt die einmalige Freigabe am Prozedurende. Sie hängt am
Return-Terminator, nicht an einer einzelnen Stelle am Textende: Gemessen an einer Prozedur mit
`Exit Sub` und einer mit `On Error GoTo` hat jede **zwei** Rückkehrpunkte und je eine Zelle, und
beide Wege räumen auf. Verlässt eine Ausnahme die Prozedur, greift der Finalizer der Zelle; das
ist später, aber kein Leck, und die Adresse ist danach ohnehin außerhalb ihrer Lebensdauer.

Ein Innenzeiger auf einen CLR-Local, ein Feld oder ein Arrayelement ist keine Alternative: Der GC
kann Heapobjekte bewegen, ein String kann seine Repräsentation bei einer Zuweisung austauschen, und
eine ReDim-Operation ersetzt ein Array. Ein in `Long` umgewandelter Managed-ByRef wird vom GC nicht
mehr nachverfolgt.

## Gemessene Grenze

Ein x86-Wegwerfprogramm hat die ganze Fläche abgefragt, statt sie aus dem Quelltext herzuleiten.
Stand nach dem ByRef-Alias-Slice:

| Form | `VarPtr` | `StrPtr` |
| --- | --- | --- |
| lokaler Skalar | Zelle | — |
| lokaler String | Zelle (Variable) | Zelle (BSTR) |
| Modulvariable, Skalar | Zelle | — |
| Modulvariable, String | Zelle (Variable) | Zelle (BSTR) |
| `Static`-Local | Zelle | Zelle |
| ByVal-Parameter | Zelle | Zelle |
| flacher UDT, ganz und Member | Zelle | — |
| UDT mit Array-, Variant- oder String-Member | Fehler 5 | Fehler 5 |
| Arrayelement, eindimensional | unbeweglich | — |
| Arrayelement, mehrdimensional | Fehler 5 | Fehler 5 |
| ganzes Array | Fehler 5 | Fehler 5 |
| Private Instanzfeld | Zelle | Zelle |
| Public Instanzfeld | Fehler 5 | Fehler 5 |
| ByRef-Parameter | Zelle des Aufrufers bei kontrolliertem privatem x86-Aufruf; sonst Fehler 5 | Fehler 5 |
| Variant | Fehler 5 | Fehler 5 |

Auf AnyCPU und x64 steht in jeder Zeile Fehler 5; dort wird kein `IntPtr` in einen `Long`
abgeschnitten.

## Zielvertrag

Jeder adressierbare VB6-Speicherplatz erhält bei seinem ersten gespeicherten Pointer eine von der
Runtime besessene native Zelle. Die Zelle besitzt eine eindeutig zuordenbare Adresse, ein
Layout und eine Lebensdauer; sie legt niemals die Adresse eines verwalteten Objekts offen.

- Ein Load synchronisiert aus der nativen Zelle in den VB6-Speicherplatz, ein Store schreibt den
  neuen VB6-Wert in dieselbe Zelle. Native Änderungen werden dadurch beim nächsten VB6-Zugriff
  sichtbar, ohne dass ein Zeiger auf den GC-Heap zeigt.
- Aliase desselben Locals, Parameters, Globals, Instanzfelds oder Arrayelements erhalten dieselbe
  Zelle. Ein ByRef-Aufruf darf keine zweite, entkoppelte Kopie erzeugen.
- Die Zelle lebt mindestens bis zum Ende ihres Speicherplatzes. `Erase`, `ReDim`, Feldersetzung,
  Objektterminierung und Prozedurrückkehr invalidieren sie kontrolliert und geben ihren nativen
  Speicher genau einmal frei. Ein nativer Aufrufer, der danach weiter schreibt, verletzt wie bei
  VB6 die definierte Zeigerlebensdauer; eine solche Verwendung kann die Runtime nicht an der
  rohen Adresse prüfen und der zugrunde liegende Allocator darf Speicher wiederverwenden.
- Ein x86-`VarPtr` wird nur dann als speicherbarer Wert bereitgestellt, wenn die Adresse ohne
  Trunkierung in `Long` passt. Die bestehenden expliziten `LongPtr`-Erweiterungen erhalten
  getrennte x64-Prüfungen; sie ändern nicht den VB6-`VarPtr`-Vertrag.

## Layout- und Ownership-Familien

| Familie | Native Zelle | Rücksynchronisation und Invalidierung |
| --- | --- | --- |
| Skalare, Date, Currency, Enums | feste, unmanaged Allokation mit der dokumentierten VB6-Breite | bei jedem Load/Store; Freigabe am Ende des Slots |
| Blittable UDT | Vier-Byte-packbares Record-Layout | Bytegenauer Transfer; verschachtelte Felder teilen keine zufälligen CLR-Adressen |
| Variable und feste Strings | von der Zelle besessene BSTR/UTF-16-Repräsentation | String-Zuweisung kann die Zelle neu allokieren und invalidiert die alte Adresse |
| Variant | von der Zelle besessener `VARIANT` mit `VariantClear`-Ownership | Subtyp, `Empty`, `Null`, `Nothing`, BSTR und Fehlerwerte bleiben unterscheidbar |
| SAFEARRAY und Arrayelemente | Zelle für den Descriptor beziehungsweise das adressierte Element | Bounds, Elementtyp, Ersatzarray und `ReDim` bestimmen die Invalidierung |

COM-Interfacezeiger sind keine stille Ausnahme: Die Adresse einer Automation-Repräsentation folgt
dem R4-Ownership-Vertrag und darf nicht als CLR-RCW-Innenadresse erscheinen.

## Durchführung und Abnahme

1. **Slot-Instrumentierung — `managed-r3-pointers` und `managed-r3-byref-alias` abgenommen:**
   Der IR markiert jede Address-taken-Stelle. Der Emitter erzeugt bzw. findet die Zelle für Locals,
   Globals einschließlich `Static`-Locals, ByVal-Parameter, flache UDTs, private Instanzfelder und
   beide String-Adressen; eindimensionale Arrayelemente nutzen den einen unbeweglichen Arrayspeicher.
   Ein adressierter ByRef-Parameter bekommt keine zweite Zelle im Aufgerufenen: Bei kontrollierten
   privaten x86-Aufrufen übergibt der Emitter die native Zelle des Aufrufers. Fehlt sie dort noch,
   übernimmt eine aufrufgebundene Zelle Rückschreiben und Freigabe. Die Probe misst
   Zeigergleichheit, nativen Schreibzugriff und Speicherdruck für vorhandene wie temporäre Zellen;
   Boolean bleibt -1/0, String läuft über die BSTR-Deskriptoradresse. `AddressOf`-Ziele,
   Ereignishandler, öffentliche Klassenmitglieder und nicht-x86 bleiben bewusst bei Fehler 5.
2. **Skalar- und Recordpfad — `managed-r3-pointers` abgenommen, `managed-r3-invalidation` offen:**
   x86-Probes halten die unterstützten Zeiger über eine erzwungene GC und prüfen native Bytes,
   Load/Store-, ByRef- und Write-back-Synchronisierung. Die gezielte Invalidierung durch `ReDim`,
   `ReDim Preserve`, `Erase`, Prozedur- und Objektende bleibt eine eigene Karte. Die einmalige
   Freigabe der Local-/ByVal-Zellen am Return-Terminator ist bereits gemessen: `Exit Sub` und
   `On Error GoTo` haben je zwei Rückkehrpunkte; verlässt eine Ausnahme die Prozedur, räumt der
   Finalizer auf.
3. **String-, Variant- und Arraypfad — `managed-r3-pointers` für beide String-Adressen
   abgenommen; `managed-r3-invalidation`, `managed-r3-safearray` und `managed-r3-variant` offen:**
   BSTR-Zellen und eindimensionale Elemente folgen dem geschlossenen Slotvertrag. VARIANT- und
   SAFEARRAY-Ownership, Descriptor, mehrdimensionale Reihenfolge, Bounds, `Erase` und `ReDim`
   erhalten getrennte native Probes. Der bestehende call-scoped-`Declare`-Puffer bleibt ein eigener,
   kürzerer Vertrag.
4. **Callback-Ownership — `managed-r3-callback-abi` offen:** Ein registrierter `AddressOf`-Thunk
   hält Delegate und gebundenes Ziel über GC. Zusätzlich braucht R3 eine explizite
   Abmelde-/Freigabeidentität und einen Fremdclient-Nachweis, dass nach dem Abmelden kein Callback
   mehr erreichbar ist.

Der gemessene x86-Managed-Stand schließt damit `managed-r3-pointers` und
`managed-r3-byref-alias`: Die sieben genannten Speicherfamilien verwenden Runtime-besessenen
Speicher statt einer CLR-Innenadresse, und kontrollierte ByRef-Aufrufe teilen die Zelle des
Aufrufers. Die verbleibenden R3-Karten teilen die noch offene Fläche ohne Lücke auf:
Invalidierung, SAFEARRAY, VARIANT und Callback-ABI. Außerhalb dieser Verträge bleibt die
ausdrückliche Fehler-5-Grenze bestehen.
