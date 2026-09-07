# R3 — Adressierbarer Speicher: Entwurfsvertrag

Stand: 2026-09-07. Dieses Dokument zerlegt die noch offene Karte
`managed-r3-pointers`; es erweitert weder den bereits geprüften kurzfristigen `Declare`-Pfad
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

Ein Innenzeiger auf einen CLR-Local, ein Feld oder ein Arrayelement ist keine Alternative: Der GC
kann Heapobjekte bewegen, ein String kann seine Repräsentation bei einer Zuweisung austauschen, und
eine ReDim-Operation ersetzt ein Array. Ein in `Long` umgewandelter Managed-ByRef wird vom GC nicht
mehr nachverfolgt.

## Gemessene Grenze

Ein x86-Wegwerfprogramm hat die ganze Fläche abgefragt, statt sie aus dem Quelltext herzuleiten.
Stand nach dem Modulvariablen-Slice:

| Form | `VarPtr` | `StrPtr` |
| --- | --- | --- |
| lokaler Skalar | Zelle | — |
| lokaler String | Fehler 5 | Zelle |
| Modulvariable, Skalar | Zelle | — |
| Modulvariable, String | Fehler 5 | Zelle |
| `Static`-Local | Zelle | Zelle |
| ByVal-Parameter | Zelle | Zelle |
| flacher UDT, ganz und Member | Zelle | — |
| UDT mit Array-, Variant- oder String-Member | Fehler 5 | Fehler 5 |
| ByRef-Parameter | Fehler 5 | Fehler 5 |
| Klassenfeld | Fehler 5 | Fehler 5 |
| Arrayelement | Fehler 5 | Fehler 5 |
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

1. **Slot-Instrumentierung:** Der IR markiert jede Address-taken-Stelle. Der Emitter erzeugt bzw.
   findet die Zelle für Locals, ByRef-Parameter, Globals, Felder und Arrayelemente; alle weiteren
   Zugriffe auf denselben Slot laufen über die Synchronisierung.
2. **Skalar- und Recordpfad:** x86-Probes halten Zeiger über eine erzwungene GC, lesen und schreiben
   native Bytes und prüfen ByRef-Aliase sowie die einmalige Freigabe bei Prozedurende.
3. **String-, Variant- und Arraypfad:** BSTR-/VARIANT-/SAFEARRAY-Ownership, Bounds, Write-back,
   `Erase` und `ReDim` werden mit unabhängigen nativen Probes geprüft. Der bestehende
   call-scoped-`Declare`-Puffer bleibt ein eigener, kürzerer Vertrag.
4. **Callback-Ownership:** Ein registrierter `AddressOf`-Thunk hält Delegate und gebundenes Ziel
   über GC. Zusätzlich braucht R3 eine explizite Abmelde-/Freigabeidentität und einen
   Fremdclient-Nachweis, dass nach dem Abmelden kein Callback mehr erreichbar ist.

`managed-r3-pointers` und `managed-r3-callback-abi` bleiben bis zu diesen vollständigen
End-to-End-Probes `planned`. Die vorhandenen Callback-GC-Regressionen belegen nur Schritt 4s
erste Haltegarantie; die x86-Skalarzellen für Locals und Modulvariablen sind der engste Teil von
Schritt 2 und schließen keine der anderen Familien. Von Schritt 1 sind damit zwei der fünf
genannten Slot-Arten bedient: Locals und Globals. ByRef-Parameter, Felder und Arrayelemente
stehen aus.

Der erste Runtime-Baustein ist `VBAddressableCell<T>` für unmanaged Skalare: Er besitzt eine
separate native Allokation, übersteht GC und lehnt Zugriffe nach `Dispose` ab. Noch keine
allgemeine Lowering-/Emitter-Stelle erzeugt diese Zellen für eine VB6-Variable: Implementiert sind
die `Long`-, `LongLong`-, `LongPtr`-, `Integer`-, `UShort`-, `UInteger`-, `ULong`-, `Byte`-,
`Boolean`-, `Single`-, `Double`-, `Date`- und `Currency`-Slots von Locals und Modulvariablen sowie
`StrPtr` auf einem lokalen oder modulweiten String, jeweils im x86-Managed-Pfad.
Außerhalb dieser Fälle und außerhalb des unmittelbaren `Declare`-Pfads gilt weiterhin die
bestehende Fehler-5-Grenze für `VarPtr` und `StrPtr`.
