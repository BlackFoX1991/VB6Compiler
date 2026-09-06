# R3 — Adressierbarer Speicher: Entwurfsvertrag

Stand: 2026-09-06. Dieses Dokument zerlegt die noch offene Karte
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
`VarPtr(localLong)`, `VarPtr(localInteger)` und `VarPtr(localByte)` native Vier-, Zwei- bzw.
Ein-Byte-Zellen. Normale Loads/Stores sowie CLR-ByRef-Write-backs werden mit diesen Zellen
synchronisiert, und sie werden bei der Prozedurrückkehr freigegeben. Sie überstehen damit eine GC,
solange der lokale Speicherplatz lebt. AnyCPU und x64 behalten für denselben Ausdruck Fehler 5;
dort wird kein `IntPtr` in einen `Long` abgeschnitten.

Ein Innenzeiger auf einen CLR-Local, ein Feld oder ein Arrayelement ist keine Alternative: Der GC
kann Heapobjekte bewegen, ein String kann seine Repräsentation bei einer Zuweisung austauschen, und
eine ReDim-Operation ersetzt ein Array. Ein in `Long` umgewandelter Managed-ByRef wird vom GC nicht
mehr nachverfolgt.

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
erste Haltegarantie; die neue x86-`Long`-Zelle ist nur der engste Teil von Schritt 2 und schließt
keine der anderen Familien.

Der erste Runtime-Baustein ist `VBAddressableCell<T>` für unmanaged Skalare: Er besitzt eine
separate native Allokation, übersteht GC und lehnt Zugriffe nach `Dispose` ab. Noch keine
allgemeine Lowering-/Emitter-Stelle erzeugt diese Zellen für eine VB6-Variable: Implementiert sind
nur lokale `Long`-, `Integer`- und `Byte`-Slots im x86-Managed-Pfad. Außerhalb dieser Fälle und
außerhalb des unmittelbaren `Declare`-Pfads gilt weiterhin die bestehende Fehler-5-Grenze für
`VarPtr` und `StrPtr`.
