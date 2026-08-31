# Luna Execution Plan

Dieser Plan ist die operative Arbeitswarteschlange zur
[Roadmap](ROADMAP.md). Die Roadmap beschreibt Produktziel, Ist-Stand und Meilensteine;
diese Datei beschreibt, wie ein einzelner Luna-Lauf die offenen Verträge schnell,
reproduzierbar und ohne neue Architekturentscheidungen abarbeitet.
Die verbindlichen Leitplanken stehen in [`LUNA_GUARDRAILS.md`](LUNA_GUARDRAILS.md) und gehen
bei Widersprüchen diesem Ausführungsplan vor.

Stand: 2026-08-31  
Ausführung: ein aktiver Arbeitsblock zur Zeit, keine parallelen Subagenten.

## Aktueller Einstieg

- Der letzte kanonische Nachweis ist **1376/1376 Tests**, Release ohne Warnungen/Fehler
  und VISIA **40/40**.
- Der Byte-String-Block (`LeftB`, `RightB`, `MidB`, `InStrB`) hat gezielte Runtime- und
  Compiler-Tests bestanden; der anschließende kanonische Lauf ist ebenfalls grün.
- `L0-01`, `L0-02`, `L0-03` und die Queue-/Schema-Karten `L1-01` bis `L1-05` sind abgeschlossen.
- Die Matrix umfasst aktuell **118 Erwartungen**: **71 implemented**, **8 partial** und
  **39 planned**; **79** sind `documented-verified`.
- **`S1` bis `S3` sind geschlossen** und stehen auf `implemented`. `S1` deckt `byref`, `set`,
  `array`, `fixed-string` und `late-bound`; `S2` deckt `unset-object` (91), `missing-file` (53)
  und `collection-index` (9 gegen 5); `S3` deklariert und implementiert die acht fehlenden
  Standard-Intrinsics.
- **Die nächste Karte ist `l1-02-j` (`l1-02-j-nested-error-resume`).** Begründung und Messwerte im Befundregister weiter
  unten. `l1-02-a-language-grammar-context` bleibt als breiter Familienstatus bewusst
  `partial`.
- Die 14 L1-02-Familien sind als eindeutige geplante Matrix-Erwartungen `l1-02-a` bis
  `l1-02-n` materialisiert. Die erste Karte `L1-02-A` hat ihren Modul-Sichtbarkeits-Slice
  (`Public`/`Global` versus `Private`/`Dim`) implementiert und steht deshalb auf `partial`;
  der Parser akzeptiert zusätzlich module-level `Dim WithEvents`-Deklarationen, die
  module-level-Direktive `Option Private Module`, statische Prozedurdeklarationen und
  module-level-`DefType`-Direktiven; die verbleibenden Grammatik-/Kontextregeln dieser Karte
  bleiben als offener Teil des breiten Familienstatus sichtbar.

Die Atomikkarte `l1-02-a-deftype-directive-syntax` ist nach dem gezielten Lauf und dem
kanonischen Gate geschlossen. Sie deckt ausschließlich die Parser-Syntax und den
Module-level-Kontext ab; die tatsächliche Anwendung der Defaulttypen auf implizite Variablen,
Parameter und Function-/Property-Get-Rückgaben bleibt eine separate Semantik-Karte.

Die Folgkarte `l1-02-a-deftype-default-semantics` ist ebenfalls geschlossen: Der Managed-
Lowerer materialisiert den geltenden Defaulttyp in untypisierten Deklarationen, Parametern und
Function-/Property-Get-Rückgaben; explizite `As`-Typen und Bezeichner-Suffixe haben Vorrang.

Die Anschlusskarte `l1-02-a-deftype-implicit-variables` ist geschlossen: Der Binder verwendet
denselben Defaulttyp auch für Variablen, die bei Zuweisungen oder in Ausdrücken implizit entstehen;
`Option Explicit` bleibt davon unberührt.

Die Anschlusskarte `l1-02-a-static-procedure-semantics` ist geschlossen: `Dim`-Variablen in
`Static`-Prozeduren werden als persistente Speicherplätze gebunden und bleiben über Aufrufe
erhalten; gewöhnliche Prozeduren behalten ihre lokale Lebensdauer.

Die Anschlusskarte `l1-02-a-procedure-visibility` ist geschlossen: `Public`/`Global`-Prozeduren
werden projektweit geteilt, `Private`-Prozeduren bleiben auf ihr deklarierendes Modul begrenzt,
und `ProcedureSymbol.IsPublic` trägt diesen Vertrag bis zur Auflösung.

Die Anschlusskarte `l1-02-a-option-private-module-semantics` ist geschlossen: `Option Private
Module` wird im `SemanticModel` als externe Exportpolitik geführt, ohne die Auflösung öffentlicher
Mitglieder innerhalb desselben Projekts zu beschneiden. Ein externer Standardmodul-Importpfad ist
weiterhin nicht behauptet und bleibt eine spätere Projekt-/Assembly-Karte.

Die Anschlusskarte `l1-02-a-global-module-variable-resolution` ist geschlossen: Eine `Global`-
Modulvariable wird unter `Option Explicit` aus einem anderen Standardmodul aufgelöst und als
öffentliche `ModuleVariableSymbol`-Instanz geführt. Der gezielte Projektlauf besteht mit **1/1**
Test; der kanonische Lauf misst **1275/1275** Tests, VISIA **40/40** und 0 Fehler.

Die atomare Anschlusskarte `l1-02-b-named-arguments-side-effect-order` ist ebenfalls geschlossen:
Benannte Argumente werden genau einmal an ihre Parameter gebunden, in deklarierter Reihenfolge
ausgewertet und liefern trotz umgekehrter Schreibreihenfolge die erwarteten Werte. Der gezielte
Compilerlauf besteht mit **1/1** Test; der kanonische Lauf misst **1275/1275** Tests, VISIA
**40/40** und 0 Fehler.

Die atomare Karte `l1-02-b-named-arguments-invalid-shapes` ist geschlossen: Doppelte benannte
Argumente sowie Positionsargumente nach einem benannten Argument melden jeweils deterministisch
`VB6S0069`, ohne eine Parameterbindung stillschweigend zu überschreiben. Der gezielte
Semantiklauf besteht mit **1/1** Test; der kanonische Lauf misst **1275/1275** Tests, VISIA
**40/40** und 0 Fehler.

Die atomare Karte `l1-02-c-nested-udt-array-storage` ist geschlossen: verschachtelte UDT-
Arrayfelder bewahren explizite Unter- und Obergrenzen sowie ihren Elementtyp, exponieren die
VB6-Skalaranfangswerte für uninitialisierte Elemente und schreiben Änderungen an einem ByRef-
Feld in den Aufrufer zurück. Der gezielte Compilerlauf besteht mit **1/1** Test; der kanonische
Lauf misst **1276/1276** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

Die atomare Karte `l1-02-c-redim-preserve-multidimensional` ist geschlossen: `ReDim Preserve`
bewahrt bei einer mehrdimensionalen dynamischen Managed-Arraystruktur Rang, frühere Grenzen und
die Untergrenze der letzten Dimension, erhält bestehende Werte an ihren Indizes und initialisiert
neue Slots mit den VB6-Skalardefaults. Der gezielte Compilerlauf besteht mit **1/1** Test; der
kanonische Lauf misst **1277/1277** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

Die atomare Karte `l1-02-c-udt-array-rank-diagnostics` ist geschlossen: Der Binder meldet bei
einem UDT-Arrayfeldzugriff mit weniger Indizes als dem deklarierten Rang den stabilen Diagnosecode
`VB6S0027`. Der gezielte Semantiklauf besteht mit **1/1** Test; der kanonische Lauf misst
**1277/1277** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung `L1-02-C` bleibt für
weitere Array-/UDT-Regeln offen.

Die atomare Karte `l1-02-c-redim-element-type-diagnostic` ist geschlossen: Ein `ReDim`, das für
eine dynamische Arrayvariable einen abweichenden Elementtyp restatiert, wird im Binder mit
`VB6S0031` abgewiesen. Der gezielte Semantiklauf besteht mit **1/1** Test; der kanonische Lauf
misst **1277/1277** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung `L1-02-C` bleibt
für weitere Array-/UDT-Regeln offen.

Die atomare Karte `l1-02-c-redim-paramarray-diagnostic` ist geschlossen: Der Binder weist ein
`ReDim` auf einem `ParamArray` deterministisch mit `VB6S0066` zurück. Der gezielte Semantiklauf
besteht mit **1/1** Test; der kanonische Lauf misst **1277/1277** Tests, VISIA **40/40** und 0
Fehler. Die breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

Die atomare Karte `l1-02-c-foreach-udt-array-diagnostic` ist geschlossen: Der Analyzer weist
`For Each` über ein Array eines Standardmodul-UDT mit `VB6S0056` zurück, statt das UDT implizit in
eine Variant-Steuervariable zu zwingen. Der gezielte Compilerlauf besteht mit **1/1** Test; der
kanonische Lauf misst **1277/1277** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-C` bleibt für weitere Array-/UDT-Regeln offen.

Die atomare Karte `l1-02-c-array-parameter-diagnostics` ist geschlossen: Der Binder meldet
`VB6S0028` für `ByVal`-Arrayparameter und `VB6S0032` für unzulässige feste Parametergrenzen. Der
gezielte Semantiklauf besteht mit **1/1** Test; der kanonische Lauf misst **1277/1277** Tests,
VISIA **40/40** und 0 Fehler. Die breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln
offen.

Die atomare Karte `l1-02-c-dynamic-udt-array-member` ist geschlossen: Ein dynamisches UDT-
Arrayfeld wird über seinen Empfänger mit `ReDim` angelegt, behält die expliziten Unter- und
Obergrenzen und bewahrt den deklarierten Elementtyp sowie beschreibbare verschachtelte Felder.
Der gezielte Compilerlauf besteht mit **1/1** Test; der kanonische Lauf misst **1277/1277** Tests,
VISIA **40/40** und 0 Fehler. Die breite Erwartung `L1-02-C` bleibt für weitere Array-/UDT-Regeln
offen.

Die atomare Karte `l1-02-a-module-declaration-context-guard` ist geschlossen: `Public`, `Private`
und `Global`-Variablendeklarationen werden innerhalb einer Prozedur oder eines verschachtelten
Statement-Blocks als ungültige Moduldeklarationen diagnostiziert und zeilenweise übersprungen;
eine lokale `Dim`-Deklaration
bleibt dabei eine gültige `DimStatementSyntax`. Der gezielte Parserlauf besteht mit **1/1** Test;
der kanonische Lauf misst **1278/1278** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-constant-declaration-context-guard` ist geschlossen: `Public`, `Private`
und `Global Const`-Deklarationen werden innerhalb einer Prozedur oder eines verschachtelten
Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen; eine lokale `Const`
bleibt dabei eine gültige `ConstStatementSyntax`. Der gezielte Parserlauf besteht mit **1/1** Test;
der kanonische Lauf misst **1279/1279** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-procedure-declaration-context-guard` ist geschlossen: `Public`, `Private`
und `Global`-Sub-/Function-Deklarationen werden innerhalb einer Prozedur oder eines verschachtelten
Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen; eine module-level
Sichtbarkeitsdeklaration bleibt gültig. Der gezielte Parserlauf besteht mit **1/1** Test; der
kanonische Lauf misst **1280/1280** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-enum-type-declaration-context-guard` ist geschlossen: `Public`, `Private`
und `Global` vor `Enum`-/`Type`-Deklarationen werden innerhalb einer Prozedur oder eines verschachtelten
Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen; module-level-
Sichtbarkeitsdeklarationen bleiben gültig. Der gezielte Parserlauf besteht mit **1/1** Test; der
kanonische Lauf misst **1281/1281** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-declare-declaration-context-guard` ist geschlossen: `Public`, `Private`
und `Global` vor `Declare`-Deklarationen werden innerhalb einer Prozedur oder eines verschachtelten
Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen; module-level-
Sichtbarkeitsdeklarationen bleiben gültig. Der gezielte Parserlauf besteht mit **1/1** Test; der
kanonische Lauf misst **1282/1282** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-property-event-declaration-context-guard` ist geschlossen: `Public`,
`Private` und `Global` vor `Property`-/`Event`-Deklarationen werden innerhalb einer Prozedur oder
eines verschachtelten Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen;
module-level-Sichtbarkeitsdeklarationen bleiben gültig. Der gezielte Parserlauf besteht mit **1/1**
Test; der kanonische Lauf misst **1283/1283** Tests, VISIA **40/40** und 0 Fehler. Die breite
Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-withevents-declaration-context-guard` ist geschlossen: `Dim`, `Public`,
`Private` und `Global` vor `WithEvents`-Deklarationen werden innerhalb einer Prozedur oder eines
verschachtelten Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen;
module-level-Sichtbarkeitsdeklarationen bleiben gültig. Der gezielte Parserlauf besteht mit **1/1**
Test; der kanonische Lauf misst **1284/1284** Tests, VISIA **40/40** und 0 Fehler. Die breite
Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-implements-declaration-context-guard` ist geschlossen: `Implements`-
Deklarationen innerhalb einer Prozedur oder eines verschachtelten Statement-Blocks werden mit
`VB6P0001` diagnostiziert und zeilenweise übersprungen; eine module-level-`Implements`-Deklaration
bleibt gültig. Der gezielte Parserlauf besteht mit **1/1** Test; der kanonische Lauf misst
**1285/1285** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung `L1-02-A` bleibt für weitere
Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-option-directive-context-guard` ist geschlossen: `Option Explicit`,
`Option Base`, `Option Compare` und `Option Private Module` werden innerhalb einer Prozedur oder
eines verschachtelten Statement-Blocks mit `VB6P0001` diagnostiziert und zeilenweise übersprungen;
module-level-Direktiven bleiben gültig. Der gezielte Parserlauf besteht mit **1/1** Test; der
kanonische Lauf misst **1286/1286** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung
`L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf `partial`.

Die atomare Karte `l1-02-a-attribute-context-guard` ist geschlossen: `Attribute`-Metadatenzeilen
werden innerhalb einer Prozedur oder eines verschachtelten Statement-Blocks mit `VB6P0001`
diagnostiziert und zeilenweise übersprungen; module-level-Attribute bleiben gültig. Der gezielte
Parserlauf besteht mit **1/1** Test; der kanonische Lauf misst **1287/1287** Tests, VISIA **40/40**
und 0 Fehler. Die breite Erwartung `L1-02-A` bleibt für weitere Grammatik-/Kontextregeln auf
`partial`.

Die atomare Karte `l1-02-a-dim-module-variable-resolution` ist geschlossen: Eine module-level-
`Dim`-Variable kann aus ihrem deklarierenden Modul gelesen und geschrieben werden, bleibt
aber für ein anderes `Option Explicit`-Modul mit `VB6S0001` verborgen; `ModuleVariableSymbol.IsPublic`
bleibt `false`. Der gezielte Compilerlauf besteht mit **1/1** Test; der kanonische Lauf misst
**1288/1288** Tests, VISIA **40/40** und 0 Fehler. Die breite Erwartung `L1-02-A` bleibt für
weitere Grammatik-/Kontextregeln auf `partial`.

Die breite Karte `l1-02-c-array-udt-shape` ist geschlossen: Der Managed-Pfad bewahrt Rang,
explizite Unter-/Obergrenzen und Elementtypen auch über IR-Lowering und ByRef-Write-back; feste
und verschachtelte UDT-Arrayfelder behalten ihre deterministischen Defaultwerte und Bounds. Ungültige
Bounds, Rangänderungen und nicht darstellbare UDT-Layouts werden mit den bestehenden
VB6-kompatiblen Laufzeit-/Semantikdiagnosen abgewiesen. Der gezielte Nachweis umfasst **26
Compiler-Tests**, **22 Semantiktests** und **21 Runtime-Arraytests**; der anschließende kanonische
Lauf misst **1290/1290** Tests, VISIA **40/40** und 0 Fehler. Die Matrix-Erwartung
`l1-02-c-array-udt-shape` steht damit auf `implemented`/`documented-verified`.

Die breite Karte `l1-02-d-control-flow-error-state` ist geschlossen: If/Select-, Schleifen- und
GoTo-Kanten werden als explizite Managed-CFG-Blöcke gelowert; aktive `On Error`-Handler,
Resume-Ziele sowie `Err`-/`Erl`-Zustände bleiben auch über Prozeduraufrufe erhalten. Illegale
Kontrollfluss-/Fehlerbehandlungskonstrukte liefern stabile Diagnosen. Der gezielte Nachweis
umfasst **13 Compiler-Tests**, **11 Parser-Tests** und **1 Managed-Diagnostic-Test**; der
kanonische Lauf misst **1293/1293** Tests, VISIA **40/40** und 0 Fehler. Die Matrix-Erwartung
`l1-02-d-control-flow-error-state` steht damit auf `implemented`/`documented-verified`.

Die breite Karte `l1-02-e-operator-dispatch` ist **begonnen, nicht geschlossen**. Gebaut und
nachgewiesen ist bislang ausschließlich die `overflow`-Klausel: Ein `checked`-Überlauf meldet
`Err.Number` **6**, eine Division durch Null **11** und `0 / 0` **6**; ungültige Operanden-
kombinationen wie `Array(1, 2) + 1` bleiben bei **13**. Die Zuordnung liegt in `VBErrors.Set`,
damit sie für jeden Operator gilt statt pro Aufrufstelle. Die Klauseln `dispatch` und `compare`
sind **nicht** nachgemessen; die Erwartung steht deshalb auf `partial`/`documented-verified` und
bleibt als offener Familienstatus sichtbar.

Die breite Karte `l1-02-f-variant-state-conversions` ist ebenfalls **begonnen, nicht
geschlossen**. Nachgewiesen sind zwei ihrer drei Klauseln:

- `state`: Die Subtyp-Tags überleben Zuweisung und Rückweg — `vbEmpty`, `vbNull`, `vbDate`,
  `vbCurrency`, `vbDecimal`, `vbError`.
- `numeric`: `CLng` rundet zur geraden Zahl, `CCur` ebenso auf vier Nachkommastellen;
  Currency- und Integer-Überlauf melden **6**, ein nicht interpretierbares Datum oder eine
  nicht interpretierbare Zahl **13**.
- `null`: Ungültige Konvertierungen melden jetzt **94** („Invalid use of Null") statt **5**;
  Operatoren reichen Null unverändert weiter.

**Offen und genau vermessen** bleibt die Null-Weitergabe durch die String-Intrinsics. In VB6
liefern `Left`, `Right`, `Mid`, `Trim`, `LTrim`, `RTrim`, `UCase` und `LCase` bei einem
Null-Argument selbst Null; hier sind sie als `String -> String` deklariert, konvertieren das
Argument also und melden seit dieser Karte **94** statt Null zurückzugeben. `Len`, `Abs`,
`Sgn`, `Int`, `Fix` und `CDec` reichen Null bereits korrekt weiter, `IsNumeric` und `TypeName`
tun es korrekterweise nicht. Die Umstellung der acht Funktionen auf `Variant -> Variant`
verschiebt den statischen Typ sehr häufiger Ausdrücke und gehört deshalb in eine eigene Karte,
nicht in diese.

Die breite Karte `l1-02-g-variant-promotion-table` ist **begonnen, nicht geschlossen**. Die
Promotionstabelle selbst wurde über 49 Operandenpaare nachgemessen und war **durchgehend
korrekt** — sie war nur fast ungetestet. Sie ist jetzt festgeschrieben: 24 Zeilen
Arithmetik mit Subtyp und Wert, dazu Vergleich, Logik und Verkettung.

Bemerkenswert daran sind die Überlaufstufen: Bei Variant-Operanden geht Integer nach Long,
Long nach Double und Byte nach Integer, statt zu überlaufen. Das steht in direkter Spannung
zur Projektinvariante „reine Integer-Ausdrücke werden nicht promoted" — die gilt für
**statisch typisierte** Ausdrücke. Der Test führt seine Operanden deshalb bewusst über
`Variant`-Variablen; inline geschrieben wäre `CInt(32767) + CInt(1)` ein typisierter Ausdruck
und müsste überlaufen. Der Kommentar im Test hält das fest, damit die beiden Regeln nicht
verwechselt werden.

**Offen** bleibt in der `errors`-Klausel der Fall „incompatible object operands". Gemessen an
einer `Collection`: `o + 1` meldet korrekt **13**, `o & "x"` meldet dagegen **0** und `o = 1`
meldet **5**. Ob 13 hier überhaupt der Sollwert ist, hängt an der Default-Property der
`Collection` und ist ohne Orakel nicht zu entscheiden — deshalb wurde nichts geändert.

Die breite Karte `l1-02-h-variant-object-array-dispatch` ist **begonnen, nicht geschlossen**.
Die Vorabmessung nach §11 umfasste 8 Programme mit rund 40 beobachteten Werten; drei Lücken
kamen dabei heraus, der Rest war korrekt.

Gebaut und nachgewiesen:

- `identity`: `Is`, `Nothing`, `VarType` 9 und die Unterscheidung Nothing/Null waren korrekt.
  Neu ist `TypeName` — es meldete den CLR-Typnamen **`VBCollection`** statt **`Collection`**.
- `member`: `VBCollection.Add` deklarierte seine drei in VB6 optionalen Parameter als
  **erforderlich**. Der typisierte Pfad übergibt immer alle vier und lief; der spät gebundene
  Pfad (`Dim c As Variant` oder `As Object`) scheiterte an `CanAcceptArgumentCount` mit
  `MissingMemberException`. `[Optional]` an den drei Parametern behebt das ohne Änderung am
  Dispatcher. Ergänzend liefert `OptionalValue` für ausgelassene `object`-Parameter jetzt den
  **Missing-Marker** statt `null` — nur so beantwortet `IsMissing` im Ziel die Frage, ob das
  Argument übergeben wurde. Die Argument-Coercion war bereits korrekt: Index 2.6 rundet auf 3,
  2.5 auf 2 (Banker's Rounding), `Currency` konvertiert, ein String bleibt Key.
- `array`: Grenzen, `VarType` 8204, Elementsubtypen, ByRef-Rückschreiben und `ReDim Preserve`
  waren durchgehend korrekt und sind jetzt festgeschrieben.
- `unsupported` (Teil): Ein Zugriff ausserhalb der Grenzen meldete **5**, VB6 meldet **9**; ein
  nicht vorhandenes Mitglied meldete **5**, VB6 meldet **438**. Beide laufen jetzt über
  `VBErrors.Set`. Bewusst **nicht** gemappt wurde `ArgumentOutOfRangeException` — die deckt auch
  Fälle wie `Space(-1)` ab, für die VB6 weiterhin 5 meldet.

**Offen** bleibt in `unsupported` die ausdrücklich genannte **SAFEARRAY**-Hälfte. Sie liegt am
COM-/TypeLib-Rand und wurde nicht angefasst; die Karte bleibt deshalb `partial`.

Die breite Karte `l1-02-i-object-members-lifecycle` ist **begonnen, nicht geschlossen**. Die
Vorabmessung nach §11 umfasste 12 Projektläufe. Sie hat **drei echte Defekte** gefunden, von
denen nur einer in dieser Karte behoben wurde.

Gebaut und nachgewiesen (`assignment`):

- `Set` teilt die Referenz, `Let` kopiert den Wert; `Is` und `VarType`/`TypeName` bestätigen das.
- `TypeName` gab den **emittierten** Typnamen preis: `__vb6_class_Box` statt `Box`. Damit war
  das Namensschema des Emitters beobachtbares Programmverhalten. `VBFunctions.TypeName` nimmt
  die Präfixe `__vb6_class_`, `__vb6_interface_`, `__vb6_udt_` und `__vb6_module_` jetzt zurück.

Gemessen und bereits korrekt (`contracts`): `Implements` mit `TypeOf`-Prüfung und Dispatch über
die Interface-Referenz, `WithEvents` mit Ereigniszustellung an den Handler.

### Offene Befunde aus dieser Karte

**1. `Public`-Felder einer Klasse sind über Modulgrenzen unbenutzbar.** `ManagedEmitter`
emittiert *jedes* Klassenfeld als `FieldAttributes.Private`; ein Zugriff aus einem anderen
Modul scheitert zur Laufzeit mit `FieldAccessException`. Betroffen ist schon der einfachste
Fall — eine Klasse, ein `Public X As Long`, ein Zugriff aus `Main`.

Ein Versuch, die Sichtbarkeit über `IrField.IsPublic` bis zum Emitter durchzureichen, wurde
**zurückgenommen**: Mit sichtbarem Feld läuft der Zugriff weiter und endet in einer
**Zugriffsverletzung** (`0xC0000005`) statt in einer sauberen Ausnahme. Der Feldzugriff selbst
ist also defekt, und die private CLR-Sichtbarkeit maskiert das bisher. Eine Änderung, die eine
fangbare Ausnahme in einen Prozessabsturz verwandelt, ist keine Verbesserung — der eigentliche
Emitter-Defekt braucht eine eigene Karte.

Nebenbefund: Der Binder meldet den Zugriff auf ein **privates** Klassenfeld von aussen **nicht**
— `analysis.Success` ist `true`. Die CLR-Sichtbarkeit ist dort derzeit das einzige Netz.

**2. `As New` erzeugt eifrig statt faul.** `Dim x As New C` ruft `Class_Initialize` sofort bei
der Deklaration auf, auch wenn `x` nie benutzt wird. VB6 erzeugt die Instanz bei der ersten
Verwendung.

**3. `Class_Terminate` feuert nie.** Weder bei `Set o = Nothing` noch beim Verlassen des
Gültigkeitsbereichs. Das ist der bekannte Zielkonflikt zwischen VB6-Referenzzählung und
GC-Laufzeit und verlangt eine Architekturentscheidung (Scope-basiertes Freigeben oder
Referenzzählung im Lowering) — nach §9 nicht nebenbei zu treffen.

Befunde 2 und 3 bilden zusammen die `lifecycle`-Klausel; sie ist damit **nicht** gebaut, und die
Karte bleibt `partial`.

### Nachtrag: der Emitter-Defekt aus Befund 1 ist behoben

Die Ursache lag nicht bei der Sichtbarkeit, sondern beim **Empfänger** des Feldzugriffs.
`EmitLoad`, `EmitStore` und `EmitAddress` riefen für ein `IrFieldPlace` einheitlich
`EmitAddress` auf den Empfänger. Für ein UDT ist das richtig — ein Werttyp braucht die
Adresse. Eine Klasse ist aber **bereits eine Referenz**: `ldfld`/`stfld` wollen dann das
Objekt selbst, und die Adresse des lokalen Slots zu laden liest am falschen Offset. Das ist
die Zugriffsverletzung.

Warum es innerhalb der Klasse trotzdem lief: Dort ist der Empfänger `Me`, und
`EmitAddress(IrThisPlace)` macht `LoadArgument(0)` — lädt also die Referenz, nicht ihre
Adresse. Nur über eine lokale Variable in einem anderen Modul trat der Fehler auf, und dort
verdeckte ihn die private CLR-Sichtbarkeit als `FieldAccessException`.

Der neue Helfer `EmitFieldReceiver` unterscheidet über das bereits vorhandene
`IsReferenceType`: Referenz laden, Werttyp adressieren. Dazu die Sichtbarkeit aus Befund 1 —
`IrField.IsPublic` aus `ModuleVariableSymbol.IsPublic`, im Emitter `FieldAttributes.Assembly`
statt `Private`. Erst beides zusammen macht `Public`-Felder benutzbar.

Gegenproben: Ein `Private`-Feld bleibt von aussen unerreichbar, und der UDT-Pfad liefert
unverändert die Adresse. **Offen bleibt** der Nebenbefund — der Binder meldet den Zugriff auf
ein privates Klassenfeld weiterhin nicht; nur die CLR-Sichtbarkeit verhindert ihn. Das
gehört in eine Binder-Karte, nicht in den Emitter.

## Erfahrungsbefund aus den L1-02-Karten

Über die bisher bearbeiteten Karten hinweg zeigt sich ein wiederkehrendes Muster: **Die
Umsetzung ist durchweg weiter als ihre Absicherung.** Bei `l1-02-f` und `l1-02-g` lautete der
Befund zweimal hintereinander „das Verhalten war bereits richtig, nur ungetestet"; bei
`l1-02-g` waren alle 49 gemessenen Operandenpaare der Promotionstabelle korrekt.

Daraus folgen zwei Dinge, die als Regeln 7 und 8 unten und als §11/§12 der Leitplanken
verbindlich sind:

1. Die echten Lücken waren beim Lesen des Quelltexts **nicht** sichtbar — sie fielen erst beim
   flächigen Messen auf (`Err.Number` 5 statt 94, 5 statt 13). Wer ohne Messung baut, riskiert,
   funktionierenden Code umzubauen und die eigentliche Lücke zu übersehen.
2. Zweimal war eine dokumentationsgestützte Änderung falsch und ein bestehender, benannter Test
   hatte recht. Ohne Orakel ist der Test der bessere Zeuge.

Für die Statusachsen heißt das: Eine Karte, die nur Tests hinzufügt, hebt `verification`, nicht
`implementation`. Das ist kein geringeres Ergebnis — ungetestetes korrektes Verhalten ist
jederzeit still kaputtzumachen.

## Befundregister aus dem Breitendurchgang (30.08.2026)

Ein gezielter Durchgang über Klassenmitglieder, Modulgrenzen, ByRef-Rückschreiben,
Laufzeitfehlernummern und die Standardbibliothek. **Jeder Eintrag ist gemessen**, keiner
hergeleitet. Sie sind nach Gefährlichkeit sortiert, nicht nach Aufwand.

Jede Gruppe ist als Matrix-Erwartung materialisiert und damit kartenfähig. Die
Einstiegspunkte sind genannt, damit keine Repository-Gesamtsuche nötig ist.

| Karte | Erwartungs-ID | Deckt ab | Einstieg |
|---|---|---|---|
| `S1` | `s1-class-public-field-storage` | A1–A5, geschlossen | `Binder.cs` (`TryGetProperty`-Zweig ~Z. 3273, ByRef-Positivliste ~Z. 3949), `Semantics.cs` (`PropertySymbol` Z. 420), `IrLowerer.cs` (`_classFields`), `Parser.cs` (Klassenmember) |
| `S2` | `s2-documented-runtime-error-numbers` | B1–B5, geschlossen | `VBErrors.cs` (`Set`-Zuordnung), `VBFiles.cs` (Open/FileLen), `VBCollection.cs` (`ResolveIndex`) |
| `S3` | `s3-remaining-standard-intrinsics` | C, geschlossen | `VBIntrinsicSymbols.cs` (Deklaration), `VBStrings.cs` / `VBFunctions.cs` (Implementierung) |

`S1` kam zuerst, weil A1 still falsch war; `S2` folgte, weil B1 in echtem VB6-Code häufig
vorkommt. Alle drei Karten sind geschlossen. Die Befunde unter **D**
haben bewusst **keine** eigene Karte: Sie hängen an Architekturentscheidungen oder an einer
fremden Kartenfläche und werden nach §9 gemeldet, nicht nebenbei erledigt.

### A — Ein `Public`-Feld einer Klasse wird als Property modelliert

Gemeinsame Ursache von vier Symptomen. `Binder.cs` löst `c.N` über
`classType.TryGetProperty(...)` auf und liefert eine `BoundPropertyAccessExpression`;
`PropertySymbol` hat **keinen** Marker, der eine synthetisierte Feld-Property von einem echten
`Property Get` unterscheidet. Der Lowerer bildet den einfachen Lese-/Schreibfall danach wieder
auf ein `IrFieldPlace` ab — alles andere fällt durch.

| # | Symptom | Gemessen | VB6 | Schwere |
|---|---|---|---|---|
| A1 | `Bump c.N` mit `ByRef`-Parameter | ~~5~~ → **6** | 6 | **behoben am 30.08.2026** |
| A2 | `Set c.ObjFeld = New Collection` | ~~`VB6S0064`~~ → läuft | funktioniert | **behoben am 30.08.2026** |
| A3 | `c.Nums(1)` bei `Public Nums() As Long` | ~~`VB6S0006`~~ → läuft | funktioniert | **behoben am 31.08.2026** |
| A4 | `Public S As String * 5` in `.cls` | ~~`VB6P0001`~~ → läuft | funktioniert | **behoben am 31.08.2026** |
| A5 | `o.N` über `Dim o As Object` | ~~438~~ → läuft | funktioniert | **behoben am 31.08.2026** |

**A1 ist der gefährlichste Befund des ganzen Durchgangs**: falsches Ergebnis ohne Diagnose.
Gegenprobe: ByRef-Rückschreiben funktioniert für lokale Variablen, `Global`-Variablen,
UDT-Member und Array-Elemente (alle **6**) — **nur** für Klassenfelder nicht, von aussen wie
von innen über `Me.N`.

**A1 ist behoben.** `PropertySymbol` trägt jetzt `IsFieldBacked`; `AddReadWriteProperty` in
`VBProjectCompilation.cs` setzt es für die synthetisierten Get/Let-Paare von Klassenvariablen
(nicht für Designer-Controls, die `IsLateBound` sind), und die ByRef-Positivliste in
`Binder.cs` akzeptiert eine `BoundPropertyAccessExpression` mit
`{ IsFieldBacked: true, IsLateBound: false }`. Der Lowerer konnte den Feldplatz über
`TryGetClassFieldPlace` schon vorher — der Binder legte nur vorher einen Temp an.

Gegenproben nach §13: Ein echtes `Property Get`/`Let` behält den Temp (5), ein UDT-Member
schreibt weiter zurück (6), und `Me.N` von innen schreibt ebenfalls zurück (6).

**A2 ist behoben.** `AddReadWriteProperty` legt für ein Feld, das eine Objektreferenz tragen
kann (`ClassTypeSymbol` oder `Variant`), zusätzlich einen `Set`-Accessor an. Der Lowerer
brauchte auch hier keine Änderung.

**Ausnahme mit Grund: `WithEvents`.** Der erste Anlauf gab jedem Feld den Set-Accessor und riss
`EmitManagedApplication_ExecutesClassFieldsMethodsPropertiesAndInitialize`. Die isolierende
Probe zeigte warum: **ohne** `WithEvents` läuft ein unqualifiziertes `Set held = New Src`
weiterhin, **mit** `WithEvents` band es plötzlich an die Property und umging die Verdrahtung —
der Handler feuerte nicht mehr. Eine `WithEvents`-Variable ist kein einfacher Speicher; sie
bekommt deshalb bewusst keinen Set-Accessor. `Set Me.held = …` meldet seitdem `VB6S0064`,
statt die Verdrahtung still zu umgehen.

**A3 ist behoben.** Die Vorabmessung über 18 Fälle hat die Ursache verschoben: Nicht
`AddReadWriteProperty` war das Problem, sondern der Binder. `c.Nums` **ohne** Index lieferte
bereits das echte Array — `LBound`/`UBound`, `For Each` und eine Zuweisung des ganzen Arrays
liefen von Anfang an. Nur die *indizierte* Form fiel durch, weil `BindClassMemberInvocation`
jede Property mit Argumenten als indizierte Property las und die synthetisierte Get/Let-Property
bewusst keine Parameter trägt.

Die Property bekommt deshalb **keine** Parameter — das würde sie von einem echten
`Property Get` ununterscheidbar machen. Stattdessen erkennt der Binder eine Property mit
`{ IsFieldBacked: true, IsLateBound: false }` und `ArrayTypeSymbol`-Typ und bindet
`c.Nums(1)` als `BoundElementAccessExpression` über den Feldzugriff — denselben Knoten, den ein
indiziertes UDT-Member erzeugt. Lowerer und Emitter brauchten keine Änderung: `LowerPlace`
bildet den Knoten schon auf `IrArrayElementPlace` ab, und weil ein VB6-Array eine Referenz ist,
decken Lesen, Schreiben und ByRef-Rückschreiben sich mit derselben Substitution ab.

Gemessen wurden 18 Fälle, alle korrekt: Lesen, Schreiben, `LBound`/`UBound`, `ReDim` von außen,
`Me.Nums(1)` von innen, ByRef-Rückschreiben in ein Element (**6**), Zuweisung des ganzen Arrays,
Variant-, String- und zweidimensionale Felder sowie `For Each`.

Gegenproben nach §13, alle unverändert: eine echte indizierte `Property Get` bleibt ein Aufruf
(**105** statt **6** beim ByRef-Versuch — der Temp schreibt nicht zurück), ein skalares Feld mit
Index meldet weiterhin `VB6S0006`, ein falscher Rang meldet `VB6S0027`, und das Feld von innen
über eine Methode funktioniert weiter.

**A4 ist behoben — und war deutlich breiter als notiert.** Im Register stand „Klassenmember".
Die Vorabmessung über 14 Fälle hat gezeigt: `String * n` wurde **überall** abgelehnt außer als
UDT-Member. Auch `Dim S As String * 5` in einer Prozedur und `Public S As String * 5` in einer
`.bas` waren Parserfehler; der Parser kannte die Form nur in `ParseTypeDeclaration`. Die Karte
war damit keine Klassenkarte, sondern eine Deklarationskarte.

Die Reparatur liegt an einer Stelle je Schicht, weil alle vier Deklarationsformen — `Dim`,
`Static`, `ReDim` und jede Modulform — durch `ParseVariableDeclarators` beziehungsweise
`ResolveVariableDeclaratorType` laufen. `VariableDeclaratorSyntax` trägt jetzt `StarToken` und
`FixedStringLength` in derselben Form wie `TypeMemberSyntax`, und die Längenprüfung im Binder ist
bewusst identisch mit der des UDT-Members: dieselben Codes `VB6S0042`/`VB6S0043`/`VB6S0044` auf
dieselbe Eingabe.

**Darunter saßen zwei weitere Defekte, die erst nach der Parser-Reparatur sichtbar wurden** —
das Muster aus §13, dass ein besseres Fehlerbild ein schlechteres Verhalten freilegt:

1. **Kein Auffüllen bei einfacher Zuweisung.** `S = "ab"` bei `String * 5` ergab `[ab]` statt
   `[ab   ]`. Array-Elemente und UDT-Member liefen längst über `LowerFixedStringWrite`, die
   Zuweisung an eine einfache Variable nicht.
2. **Falscher Anfangswert.** Ein `String * 4` ist in VB6 vier Leerzeichen. Nur das UDT-Member
   war korrekt; Local, Modulvariable und Klassenfeld lieferten alle Länge 0.

Gemessene Endlage über 14 Fälle: Anfangswert einheitlich `[    ]` mit Länge 4 über Local,
Modulvariable, Klassenfeld und UDT-Member; Abschneiden und Auffüllen korrekt; Vergleich gegen
den aufgefüllten Wert `True`; Verkettung behält die Breite; Arrays von `String * n` und private
Felder ebenfalls korrekt.

**Zwei Befunde nach §9 gemeldet statt nebenbei geändert:**

- Eine **benannte Konstante als Länge** (`String * Breite`) meldet `VB6S0043`. Das ist dieselbe
  Teilmengenbeschränkung, die das UDT-Member schon trug; sie wurde bewusst gespiegelt statt
  einseitig erweitert. Beide Formen gemeinsam zu öffnen ist eine eigene Karte.
- **`String * 4` an einen `ByRef s As String`** meldet `VB6S0008`. Echtes VB6 erlaubt das mit
  Copy-in/Copy-out. Die Typstrenge bei ByRef ist aber eine ausdrücklich dokumentierte
  Entscheidung dieses Projekts — nach §12 wird sie nicht ohne Ansage aufgeweicht.

**A5 ist behoben — und `S1` damit geschlossen.** Der Grund für den Befund liegt genau an der
Naht zwischen Übersetzung und Laufzeit: Der Binder modelliert ein `Public`-Feld als Property,
der Emitter bildet es aber wieder auf ein **CLR-Feld** ab. `VBDynamicDispatch` durchsuchte
Methoden und Properties — also genau die beiden Formen, die es zur Laufzeit nicht ist.

Die VB6-Sichtbarkeit trägt dabei das CLR-Attribut: Der Emitter gibt einem `Public`-Feld
`FieldAttributes.Assembly` und einem `Private`-Feld `FieldAttributes.Private`. Die Feldsuche
akzeptiert deshalb `!IsPrivate` — ein privates Feld bleibt von aussen unerreichbar, ohne dass
eine zweite Sichtbarkeitsquelle gepflegt werden müsste.

**Ein zweiter Defekt lag darunter, wieder erst nach der ersten Reparatur sichtbar:**
`o.Nums(1) = 7` riss den **Compiler** ab. Der Binder erzeugte für ein indiziertes spät
gebundenes Zuweisungsziel die Aufrufgestalt einer Funktion
(`BoundMemberInvocationExpression`), die als Zuweisungsziel keinen Platz hat; `LowerPlace` warf
eine `InvalidOperationException`, die als unbehandelte Ausnahme aus dem Emit herausfiel statt
als Diagnose. Die Bedingung `syntax.Indices.IsEmpty` schloss die Indexform aus dem
Property-Zweig aus. Sie ist entfallen; die Indizes gehen jetzt als Argumente an den Dispatch,
den der Lowerer über `LowerDynamicSet` bereits bedienen konnte. Der Befund war **nicht**
feldspezifisch — er traf jedes indizierte spät gebundene Zuweisungsziel.

Gemessen wurden 11 Fälle, alle korrekt: Lesen, Schreiben, String- und Objektfeld mit `Set`,
indiziertes Arrayfeld, Zugriff über `Variant` statt `Object`, und über einen `With`-Block.
Gegenproben unverändert: Methode (**42**) und echtes `Property Get` (**99**) laufen wie zuvor,
ein privates Feld meldet weiterhin **438**, ein unbekanntes Mitglied ebenfalls **438**.

**Damit ist `S1` vollständig** — `byref`, `set`, `array`, `fixed-string` und `late-bound` — und
die Erwartung steht auf `implemented`.

**Neuer Befund aus der Gegenprobe (Grenze 4, Deklarationsform):** Ein **echtes**
`Property Get Nums() As Long()`, also eine deklarierte Property mit Array-Rückgabetyp, kann
nicht indiziert werden — `c.Nums(1)` meldet `VB6S0006`. In VB6 wird die Property gerufen und
ihr Ergebnis indiziert. Der Befund ist vorbestehend, wird durch die Feld-Erkennung ausdrücklich
**nicht** berührt (sie verlangt `IsFieldBacked`) und braucht eine eigene Karte.

**Weiterer Befund aus der Gegenprobe (Grenze 4, Deklarationsform):** Eine Klasse mit **beiden**
Accessoren `Property Get` und `Property Set` gleichen Namens liefert aus dem `Get` **Empty**.
Isoliert: Das `Set` speichert nachweislich korrekt (innen gelesen kommt der Wert an), und ein
`Get` **ohne** zugehöriges `Set` liefert korrekt. Nur die Kombination bricht — und das ist die
Normalform jeder VB6-Objekt-Property. Der Befund ist vorbestehend, gehört **nicht** zu `S1` und
braucht eine eigene Karte.

**Neuer Befund aus der Grenzmessung (§13, Grenze 1):** Ein **spät gebundener** Zugriff auf ein
öffentliches Klassenfeld findet es überhaupt nicht — `Dim o As Object : o.N = 5` meldet
`MissingMemberException`. `VBDynamicDispatch` sucht Methoden und Properties, aber keine Felder.
Der Befund ist vorbestehend, nicht durch diese Karte entstanden, und gehört zu `S1`; er ist in
deren `expected` bislang nicht genannt und braucht dort eine Ergänzung oder eine eigene Karte.

**Nebenbefund, bestätigt:** `AddReadWriteProperty` unterscheidet nicht zwischen `Public` und
`Private`. Deshalb bindet `b.hidden` von aussen fehlerfrei; nur die CLR-Sichtbarkeit verhindert
den Zugriff zur Laufzeit. Das ist dieselbe Stelle und dieselbe Karte wert.

### B — Fehlende VB6-Fehlernummern

| # | Fall | Gemessen | VB6 | Stand |
|---|---|---|---|---|
| B1 | Mitgliedsaufruf auf nicht gesetzter Objektvariablen | ~~5~~ → **91** | **91** „Object variable not set" | **behoben am 31.08.2026** |
| B2 | `Open` / `FileLen` auf nicht existierende Datei | ~~5~~ → **53** | **53** „File not found" | **behoben am 31.08.2026** |
| B4 | `Kill` auf nicht existierende Datei | ~~**0**~~ → **53** | **53** | **behoben am 31.08.2026** |
| B5 | `FileDateTime` auf nicht existierende Datei | ~~**Datum**~~ → **53** | **53** | **behoben am 31.08.2026** |
| B3 | `c(0)` und `c.Remove 5` auf einer `Collection` | ~~5~~ → **9** | **9** | **behoben am 31.08.2026** |

B1 war häufig in echtem VB6-Code und deshalb vorrangig. Bereits korrekt und nicht angefasst:
`Left(s, -1)`, `Mid(s, 0)`, `Sqr(-1)`, `Log(0)` melden **5**; `CByte(300)` und `CInt("99999")`
melden **6**; ein doppelter `Collection`-Schlüssel meldet **457**.

**B4 und B5 sind neu und waren schwerer als das gemeldete B2.** Die Messung über die ganze
Fläche hat zwei Fälle gefunden, die überhaupt keinen Fehler meldeten: `Kill` auf eine fehlende
Datei lief still durch (Err.Number **0**), und `FileDateTime` lieferte für sie ein Datum
(`-109205.04`, der 1601er-Platzhalter von `File.GetLastWriteTime`). Beides ist „still falsch"
statt „falsche Nummer" und damit nach §13 die schwerere Klasse. Ursache: .NET schweigt an
beiden Stellen — `File.Delete` wirft für eine fehlende Datei nicht, `File.GetLastWriteTime`
auch nicht. `Open` und `FileLen` warfen dagegen bereits `FileNotFoundException`, dort genügte
die Zuordnung in `VBErrors.Set`.

**B1 ist eine bewusst breite Zuordnung.** `NullReferenceException => 91` trifft jeden
Null-Zugriff, auch einen, der aus einem Compilerdefekt stammt. Das ist hingenommen: VB6 meldet
an dieser Stelle 91, und der bisherige Sammelwert 5 hat denselben Defekt genauso verdeckt — nur
mit einer Nummer, die noch weniger aussagt. Der Regressionslauf über 1341 Tests hat keine
Verschiebung gezeigt.

**Entscheidung zu B3, im Code sichtbar gemacht:** `ResolveIndex` bedient auch `Add`s
`Before`/`After`. Dort bleibt die Nummer **5** — eine Position außerhalb der Sammlung ist bei
`Add` ein ungültiges *Argument*, kein Subscript. Nur `Item` und `Remove` melden **9**. Der
Parameter `outOfRangeNumber` macht die Trennung an der Aufrufstelle sichtbar, statt sie im
Rumpf zu verstecken. Bestätigend: der unbekannte *Schlüssel* meldete schon vorher **5**, was
zur dokumentierten Trennung „Index → 9, Schlüssel → 5" passt.

### C — Acht fehlende Standardfunktionen

`S3` ist geschlossen. `StrReverse`, `FormatNumber`, `FormatCurrency`, `FormatPercent`,
`FormatDateTime`, `Partition`, `CallByName` und `QBColor` sind als Intrinsics deklariert,
über IR und Managed-Emitter verbunden und mit Runtime- sowie Managed-E2E-Tests belegt.
`CallByName` nutzt den vorhandenen dynamischen Member-Dispatch für `vbMethod`, `vbGet`,
`vbLet` und `vbSet`; die Formatfunktionen respektieren den gewählten Kompatibilitätsmodus.

### E — Nachtrag vom 31.08.2026: UDT-Arraygrenzen stürzten ab

Nicht Teil des ursprünglichen Durchgangs. Der Befund fiel bei der Vorabmessung zur Frage an,
ob eine benannte Konstante als `String * n`-Länge zugelassen werden soll — und war deutlich
schwerer als die Frage, die ihn ausgelöst hat.

In einem `Type`-Block funktionierten **ausschließlich nackte Integer-Literale** als Arraygrenze.
Jede andere Form stürzte zur Laufzeit ab, ohne dass der Compiler etwas meldete.

| Grenze im `Type` | Gemessen vorher | VB6 |
|---|---|---|
| `a(1 To 3)` | `1/3` | `1/3` |
| `a(1 To 2 + 1)` | **Absturz** (`NullReferenceException`) | `1/3` |
| `a(1 To Breite)` | **Absturz** | `1/3` |
| `a(1 To Breite * 2)` | **Absturz** | `6` |
| `a(Start To 4)` | **Absturz** | `2/4` |
| `a(1 To 2, 1 To 1 + 1)` | **Absturz** | `2` |
| `Const` nach dem `Type` | **Absturz** | läuft |
| `a(1 To n)` — echter Fehler | **Absturz** | Meldung |
| `a(5 To 1)` — echter Fehler | **Absturz** | Meldung |

Ursache war eine stille Rückgabe: Schlug `TryEvaluateIntegerConstant` fehl, lieferte
`BindArrayBounds` eine **leere** Grenzenliste zurück, ohne zu melden. Das Member bekam keinen
Speicher, das Array wurde nie angelegt, der erste Zugriff riss das Programm ab. Selbst die
beiden Fälle, die garantiert Fehler sind, kamen so als Absturz statt als Diagnose heraus.

Zur Abgrenzung gegen die Smart-App-Control-Falle: Die gemessenen Exitcodes waren
`-1073741819` (0xC0000005, `NullReferenceException`) und `-532462766` bei `a(5 To 1)`, jeweils
mit ausgeschriebener Ausnahme in der Ausgabe des Kindprozesses. Echte Defekte, kein blockiertes
Assembly.

**Behoben am 31.08.2026.** Der Falter beherrscht jetzt benannte Konstanten unabhängig von der
Deklarationsreihenfolge — sie werden vollständig gesammelt, bevor ein Member aufgelöst wird —
sowie `+ - * \` mit `checked`-Überlaufprüfung. Was nicht faltet, meldet neu `VB6S0071`; eine
Obergrenze unter der Untergrenze meldet neu `VB6S0072`. Beide Codes haben Positivassertions.

Gegenproben unverändert: ein gewöhnliches `Dim a(1 To Breite * 2)` läuft weiter (es wertet
seine Grenzen zur Laufzeit aus und braucht den Falter gar nicht), und ein verschachteltes
UDT-Arrayfeld verhält sich wie zuvor.

**Bewusst nicht mitgenommen:** Die Breite eines `String * n` hängt am selben Falter, hat aber
noch ihre eigene Literal-only-Prüfung — in **zwei** Pfaden (`BindFixedStringLength` für das
UDT-Member, `ResolveFixedLengthStringType` für den Deklarator). Nur die UDT-Seite zu öffnen
hätte die beiden Formen wieder auseinanderlaufen lassen, was bei A4 gerade bewusst vermieden
wurde. Eigene Karte, die beide Stellen gemeinsam umstellt.

### D — Bereits früher gemeldet, weiterhin offen

- `Dim x As New C` erzeugt eifrig statt bei der ersten Verwendung.
- `Class_Terminate` feuert nie — weder bei `Set o = Nothing` noch beim Verlassen des Bereichs.
- Der Binder meldet den Zugriff auf ein **privates** Klassenfeld von aussen nicht; nur die
  CLR-Sichtbarkeit verhindert ihn.
- `Left`, `Right`, `Mid`, `Trim`, `LTrim`, `RTrim`, `UCase`, `LCase` reichen `Null` nicht
  weiter, sondern melden 94.
- `Debug.Print`/`CStr` geben ein Date-Variant als OADate-Seriennummer aus.
- Die SAFEARRAY-Hälfte von `l1-02-h` ist nicht abgedeckt.

### Was der Durchgang **nicht** gefunden hat

Sichtbarkeit über Modulgrenzen für `Public Const`, `Public Type` und `Public Enum` ist korrekt;
Variant- und UDT-Felder einer Klasse funktionieren; die ByRef-Kette über Locals, Globals,
UDT-Member und Array-Elemente stimmt.

## Arbeitskartenvertrag

Jede Karte muss vor dem Start folgende Felder enthalten:

| Feld | Vorgabe |
|---|---|
| ID/Status | `L0-01`-Format; `ready`, `active`, `verified` oder `blocked` |
| Matrix | Genau eine Erwartungs-ID; unabhängige Ergebnisse werden vorher geteilt; ein Kartenabschluss schreibt beide Achsen fort: `planned` → `implemented` und `not-yet-verified` → `documented-verified` |
| Abhängigkeiten | IDs, die vorher `verified` sein müssen |
| Einstieg | Konkrete Produktions- und Testdateien, keine Repository-Gesamtsuche |
| Vorabmessung | Ist-Verhalten **jedes** `expected`-Feldes gemessen, bevor Code entsteht (Leitplanken §11); Ergebnis mit Fallzahl in der Karte |
| Umfang | Eine Verhaltensänderung und eine betroffene Pipeline-Schicht |
| Prüfung | Exakter Build- und `vstest`-Filter |
| Abnahme | Positivfall, Fehlerfall, Profil-/Bitness-Fall und Rückwärtskompatibilität |
| Dokumentation | Matrixstatus, Roadmap-Hinweis und Changelog-Eintrag nach erfolgreicher Prüfung |

Regeln:

1. Luna liest nur die aktive Karte, den referenzierten Matrixeintrag und die genannten Dateien.
2. Bestehende öffentliche APIs werden additiv erweitert; keine Umbenennung oder Entfernung.
3. Kein Code außerhalb des Kartenumfangs. Ein neu entdeckter Querbereich wird als neue Karte
   angelegt, nicht nebenbei mitimplementiert.
4. Eine Karte bleibt höchstens vier eng verwandte Tests groß; sonst wird sie vor dem Coding
   geteilt.
5. Bei einem echten Blocker werden Ursache, reproduzierbarer Befehl und benötigte Entscheidung
   in der Karte dokumentiert; Luna arbeitet nicht spekulativ weiter.
6. Keine Resets, keine pauschalen Prozessabbrüche und keine automatischen Commits.
7. **Erst messen, dann bauen** (Leitplanken §11). Eine Karte beginnt mit einem Wegwerfprogramm
   über `VB6TestProgram.RunLines`, das die beobachtbaren Werte der ganzen Vertragsfläche
   ausgibt. Was schon korrekt ist, wird durch Tests festgeschrieben statt umgebaut: Die
   verification-Achse wandert dann nach oben, die implementation-Achse bleibt stehen.
8. **Bestandsschutz benannter Verträge** (Leitplanken §12). Reißt eine Änderung einen Test,
   dessen Name eine Vertragszusage ausspricht, wird die Änderung zurückgenommen — nicht der
   Test angepasst. Ohne Orakel schlägt der bestehende Vertrag die Herleitung aus der
   VB6-Dokumentation. Die offene Frage wird mit den gemessenen Werten notiert.

## Reihenfolge der Wellen

Der aktuelle Matrixstand steht im Readout von `build.ps1` und im Abschnitt „Aktueller
Einstieg" — hier bewusst **keine** zweite Zahlenangabe, weil genau solche Kopien
auseinanderlaufen. Luna arbeitet die Karten in dieser Reihenfolge ab; innerhalb einer Familie
gilt Abhängigkeit vor alphabetischer ID. Die Befundkarten `S1` bis `S3` gehen der Welle
voraus, siehe Befundregister.

### L0 — Pausenstand und Baseline ✅

- [x] `L0-01`: Byte-Intrinsics mit Grenzwerten, leerem Match, `Option Compare`, ANSI/DBCS und
  deterministischem Profil prüfen; bestehende gezielte Tests wiederholen.
- [x] `L0-02`: vollständigen seriellen Release-Lauf ausführen, reale TRX-Zahl ermitteln und
  Roadmap/README/Changelog synchronisieren.
- [x] `L0-03`: Matrix-JSON und `git diff --check` validieren; keine Produktstatusänderung ohne
  grünen Vollauf.

### L1 — Ausführungsqueue und Matrix

- [x] `L1-01`: diese Datei mit der Roadmap verknüpfen und den aktuellen P0-Stand eintragen.
- [x] `L1-02`: Sprache/Variant/Runtime in einzelne dokumentierte Kartenfamilien zerlegen.
- [x] `L1-03`: Projekte, Datei-I/O, COM/ABI in einzelne Kartenfamilien zerlegen.
- [x] `L1-04`: Forms, ActiveX, Enterprise und MSBuild in einzelne Kartenfamilien zerlegen.
- [x] `L1-05`: Matrix-Schema um den Implementierungsstatus pro Erwartung erweitern; der Build prüft
  eindeutige IDs, Statuswerte, Matrixreferenzen und Testreferenzen.
- [x] `L1-05R`: die 34 atomaren Erwartungen für `L1-03-A` bis `L1-04-Q` in der Matrix
  materialisieren; alle bleiben bis zu echten Assertions `planned`/`not-yet-verified`.

#### L1-02-Ausgabe: atomare Kartenfamilien

Die folgenden Karten sind die verbindliche Zerlegung. Vor jeder Implementierung wird aus der
gewählten Kartenfamilie eine eigene Matrix-Erwartung mit eindeutiger ID materialisiert; bis dahin
bleiben die bestehenden breiten Matrixeinträge die Zuordnungsebene.

| Karte | Matrixeintrag | Abnahmefokus und Einstieg |
|---|---|---|
| `L1-02-A` | `language-declarations-and-statements` | Grammatik, Deklaratoren, Sichtbarkeit und Kontextwörter; `tests/VB6.Parser.Tests`, `tests/VB6.Semantics.Tests` |
| `L1-02-B` | `language-declarations-and-statements` | Named Arguments, optionale Defaults und Auswertungsreihenfolge; `NamedArgumentParserTests`, `OptionalParameterParserTests`, Compiler-E2E |
| `L1-02-C` | `language-arrays-and-udts` | Rang, Untergrenzen, `ReDim`, `Erase`, UDT-Felder und ByRef-Arrayform; `*Array*Tests`, `*Udt*Tests` |
| `L1-02-D` | `language-control-flow-and-errors` | verschachtelte Basic Blocks, `On Error`, `Resume`, `Err` und `Erl`; `ErrorHandlingParserTests`, `ControlFlowGuardTests` |
| `L1-02-E` | `language-operators-and-variants` | typisierte Operatoren, `Option Compare`, logische Operatoren und Überläufe; `VariantEqualityExecutionTests`, `LikeAndObjectIdentityExecutionTests` |
| `L1-02-F` | `runtime-variant-and-conversions` | `Empty`, `Null`, `Missing`, `Error`, `Date`, `Currency`, `Decimal` und Informationstypen; `VariantStateTests`, `VariantFoundationExecutionTests` |
| `L1-02-G` | `runtime-variant-and-conversions` | vollständige Promotionen für arithmetische, logische, Vergleichs- und Verkettungsoperatoren; `VariantArithmeticTests`, `VariantMultiplyTests`, `VariantConcatenationExecutionTests` |
| `L1-02-H` | `runtime-variant-and-conversions` | Objekt-/Arrayvarianten, Default-Member, Indexzugriff und ByRef-Ersatz; `VariantObjectDispatchExecutionTests`, `VariantStateExecutionTests` |
| `L1-02-I` | `language-operators-and-variants` | `Let`/`Set`, `As New`, Collection, `Implements`, Events und `WithEvents`; `CollectionExecutionTests`, `ClassMemberBinderTests`, `VariantObjectDispatchExecutionTests` |
| `L1-02-J` | `language-control-flow-and-errors` | aktive Handler, verschachtelte Aufrufe, Weitergabe und jede `Resume`-Zielart; `ControlFlowGuardTests`, `ManagedDiagnosticTests` |
| `L1-02-K` | `runtime-standard-library` | verbleibende String-, Konvertierungs-, Array- und Information-Intrinsics einschließlich Fehlernummern; `StringFunctionTests`, `StringIntrinsicRuntimeTests`, `*IntrinsicExecutionTests` |
| `L1-02-L` | `runtime-standard-library` | Date/Time, Format, Math und Financial gegen die vollständige dokumentierte Oberfläche auditieren; `DateTimeRuntimeTests`, `MathRuntimeTests`, `MathIntrinsicExecutionTests`, `FinancialIntrinsicTests` |
| `L1-02-M` | `runtime-standard-library` | Interaction, Environment, Registry, App, Screen, Printer und Clipboard mit expliziten Headless-Hosts; `InteractionRuntimeTests`, `StandardLibraryIntrinsicExecutionTests` |
| `L1-02-N` | `runtime-file-io` | Text-/Binary-/Random-/Sequential-Grundregeln, Codepage, Variant-Zustände und zusammengesetzte Layouts; `FileRuntimeTests`, `FileStringIoExecutionTests`, `FileIoExecutionTests` |

Jede spätere Implementierungskarte referenziert genau eine dieser Familien, eine neue atomare
Erwartungs-ID und maximal vier gezielte Tests. Die Reihenfolge innerhalb der Familien ist
`A → B → C → D → E → F → G → H → I → J → K → L → M → N`.

Die erste Materialisierungswelle ist abgeschlossen: `L1-02-A` bis `L1-02-N` verweisen auf die
Matrix-IDs `l1-02-a-language-grammar-context`, `l1-02-b-named-arguments-evaluation-order`,
`l1-02-c-array-udt-shape`, `l1-02-d-control-flow-error-state`, `l1-02-e-operator-dispatch`,
`l1-02-f-variant-state-conversions`, `l1-02-g-variant-promotion-table`,
`l1-02-h-variant-object-array-dispatch`, `l1-02-i-object-members-lifecycle`,
`l1-02-j-nested-error-resume`, `l1-02-k-standard-library-remaining`,
`l1-02-l-locale-datetime-math-financial`, `l1-02-m-headless-host-services` und
`l1-02-n-file-io-remaining`. Die noch nicht abgeschlossenen Familienerwartungen stehen bewusst
auf `planned`, bis die jeweilige Implementierung mit ihren gezielten Tests verifiziert ist;
`L1-02-A` steht für den bereits verifizierten Modul-Sichtbarkeits-Slice auf `partial`. Die breite
`L1-02-B`-Erwartung ist nach dem Nachweis von Zuordnung, Defaults, Auswertungsreihenfolge und
deterministischen Fehlformen `implemented`/`documented-verified`. Die atomare
Erwartung `l1-02-a-dim-withevents-declaration` ist für den module-level-Kontextfall bereits
`implemented`/`documented-verified`; auch `l1-02-a-option-private-module-syntax`,
`l1-02-a-static-procedure-syntax`, `l1-02-a-deftype-directive-syntax`,
`l1-02-a-deftype-default-semantics`, `l1-02-a-deftype-implicit-variables`,
`l1-02-a-deftype-range-conflicts`, `l1-02-a-static-procedure-semantics`,
`l1-02-a-procedure-visibility` und `l1-02-a-option-private-module-semantics` sind als
`implemented`/`documented-verified` geschlossen; `l1-02-a-global-module-variable-resolution` ist
ebenfalls `implemented`/`documented-verified`. Die atomare Karte
`l1-02-b-named-arguments-side-effect-order` ist als `implemented`/`documented-verified`
geschlossen. Die atomare Karte `l1-02-b-named-arguments-invalid-shapes` ist ebenfalls als
`implemented`/`documented-verified` geschlossen; gemeinsam bilden sie den Nachweis für die
breite Erwartung `l1-02-b-named-arguments-evaluation-order`.
Die Bereichskarte prüft überlappende DefType-Buchstaben mit `VB6S0070`; direkt angrenzende,
nicht überlappende Bereiche bleiben gültig.
Die breite Familienerwartung bleibt wegen der offenen Grammatik- und Kontextregeln `partial`.

#### L1-03-Ausgabe: Projekte, Datei-I/O und COM/ABI

| Karte | Matrixeintrag | Abnahmefokus und Einstieg |
|---|---|---|
| `L1-03-A` | `project-vbp-vbg` | Projektarten, Startup, Output und Target-Bitness; `ProjectCompilationTests`, CLI-Prozesstests |
| `L1-03-B` | `project-vbp-vbg` | `.vbg`-Abhängigkeiten, Referenzen und deterministische Emissionsreihenfolge; `VBProjectGroupCompilationTests`, `VBProjectGroupLoaderTests` |
| `L1-03-C` | `project-vbp-vbg` | Version/Binary Compatibility, Ressourcen, Components und vollständige Inputauflösung; `VBProjectLoaderTests`, `ProjectDiagnosticCoverageTests` |
| `L1-03-D` | `runtime-file-io` | `Open`-Modi, Access/Sharing, Default-Random und Kanalfehler; `FileStatementParserTests`, `FileRuntimeTests` |
| `L1-03-E` | `runtime-file-io` | `Seek`, `EOF`, `LOF` und mode-aware `Loc` inklusive 1-basierter Einheiten; `FileRuntimeTests`, `FileIoExecutionTests` |
| `L1-03-F` | `runtime-file-io` | `Print #`, `Write #`, `Input #`, `Line Input` und Codepage-/Separatorregeln; `FileStringIoExecutionTests`, `FileIoExecutionTests` |
| `L1-03-G` | `runtime-file-io` | Binary-/Random-Scalar-, UDT- und Fixed-String-Records; `FileIoExecutionTests`, `FixedLengthStringUdtExecutionTests` |
| `L1-03-H` | `runtime-file-io` | eigenständige String-/numerische/UDT-Arrays mit Descriptor- und Bounds-Erhalt; `FileIoExecutionTests`, Array-E2E-Tests |
| `L1-03-I` | `runtime-file-io` | Variant-Arrays, Objektvarianten und zusammengesetzte Recordlayouts; `FileRuntimeTests`, `VariantStateExecutionTests` |
| `L1-03-J` | `com-automation-types` | TypeLib-Aliase, Records, verschachtelte UDTs, Pointer und C-Arrays; `RegisteredInteropProjectTests`, `ManagedComRegistrationTests` |
| `L1-03-K` | `com-automation-types` | VARIANT/BSTR/SAFEARRAY-Besitz, Bounds, Subtypen und ByRef-Write-back; `ComDispatchRuntimeTests`, `ManagedEmitterTests` |
| `L1-03-L` | `com-automation-types` | `IDispatch` mit LCID, Named Arguments, Default-Member, PropertyPut und `EXCEPINFO`; `ComDispatchRuntimeTests`, `VariantObjectDispatchExecutionTests` |
| `L1-03-M` | `abi-declare-addressof` | `Declare`-Signaturen für x86-Skalare, Strings, Pointer und UDTs; `DeclarePInvokeExecutionTests`, `DeclareParserTests` |
| `L1-03-N` | `abi-declare-addressof` | `AddressOf`, Delegate-Lebensdauer, Callback-Parameter und native ByRef-Rückgabe; `AddressOfExecutionTests`, `AddressOfParserTests` |
| `L1-03-O` | `abi-declare-addressof` | SAFEARRAY-Callbacks für Variant-, String-, Object- und LongPtr-Elemente; `AddressOfExecutionTests`, `DeclarePInvokeExecutionTests` |
| `L1-03-P` | `com-server-and-typelib-emission` | emittierte COM-Klassen, CLSID/ProgID, ClassFactory, `IUnknown`/`IDispatch` und Aktivierung; `ManagedComRegistrationTests`, `ComActivationProbe` |
| `L1-03-Q` | `com-server-and-typelib-emission` | `.tlb`, Registrierung, registry-free Manifest und ActiveX-EXE-Local-Server; `CliProcessTests`, `ManagedComRegistrationTests` |

Die Karten `A → C`, `D → I` und `J → Q` bilden drei unabhängige Teilketten. Eine COM-Karte darf
erst starten, wenn die zugehörige Profil-/Bitness-Karte und die betroffenen Automation-Tests grün
sind; native OCX-Registrierung ist dafür nicht vorausgesetzt.

#### L1-04-Ausgabe: Forms, ActiveX, Enterprise und MSBuild

| Karte | Matrixeintrag | Abnahmefokus und Einstieg |
|---|---|---|
| `L1-04-A` | `project-persisted-designer-artifacts` | `.frm`/`.frx`-Hülle, `BeginProperty`, Offsets und Encoding verlustfrei laden; `VBDesignerParserTests` |
| `L1-04-B` | `project-persisted-designer-artifacts` | `.ctl`, `.ctx`, `.pag`, `.dob`, `.dsr`, `.res` und Ressourcenpayloads binden; `VBDesignerParserTests`, `CliProcessTests` |
| `L1-04-C` | `forms-lifecycle-and-intrinsic-controls` | Form-/Control-Lifecycle, Defaultinstanzen, Modalität, Fokus, Tab und Z-Order; `FormHostRuntimeTests`, `WinFormsHostTests` |
| `L1-04-D` | `forms-lifecycle-and-intrinsic-controls` | intrinsische Controls, Eigenschaften, Events, Menüs und Timer; `WinFormsHostTests`, `DirectManagedProjectExecutionTests` |
| `L1-04-E` | `forms-lifecycle-and-intrinsic-controls` | Control-Arrays sowie dynamisches `Load`/`Unload` für Controls, Forms und Menüs; `WinFormsHostTests`, `ProjectCompilationTests` |
| `L1-04-F` | `forms-drawing-and-mdi` | `Scale*`, Koordinaten, Zeichenattribute und persistente AutoRedraw-Flächen; `InteractionRuntimeTests`, `WinFormsHostTests` |
| `L1-04-G` | `forms-drawing-and-mdi` | sichtbare/Paint-Kontexte, PSet/Point/Line/Circle/PaintPicture/Cls und GDI-/DIB-Clipping; `GraphicsLineParserTests`, `WinFormsHostTests` |
| `L1-04-H` | `forms-drawing-and-mdi` | alle 16 DrawMode-/ROP2-Werte in aktiven und persistenten Flächen; `WinFormsHostTests`, Pixelregressionen |
| `L1-04-I` | `forms-drawing-and-mdi` | MDI-Parent/Child, `ActiveForm`, Cascade/Tile/Arrange, WindowList und Fokus; `FormHostRuntimeTests`, `ProjectCompilationTests` |
| `L1-04-J` | `activex-stock-and-generic-host` | vollständiges Microsoft-Stock-Control-Inventar und sichtbarer Verifikationsstatus; Matrix-/Fixture-Tests |
| `L1-04-K` | `activex-stock-and-generic-host` | generisches TypeLib-ActiveX mit In-Place-Aktivierung und Ambient Properties; `RegisteredInteropProjectTests`, `WinFormsHostTests` |
| `L1-04-L` | `activex-stock-and-generic-host` | Persistenz, Property Pages und Connection Points für generische Controls; `ComDispatchRuntimeTests`, `WinFormsHostTests` |
| `L1-04-M` | `activex-stock-and-generic-host` | UserControl-OLE-View/In-Place, PropertyBag, Events und Lifecycle; `UserControlHostIntrinsicAnalysisTests`, `WinFormsHostTests` |
| `L1-04-N` | `project-persisted-designer-artifacts` | DataEnvironment, DataReport, UserDocument und PropertyPage aus Artefakten ausführen; `CliProcessTests`, `ProjectCompilationTests` |
| `L1-04-O` | `msbuild-headless-sdk` | gepackte Resolver-Task für exakte Quellen, Ressourcen, Referenzen und Outputs; `CliProcessTests`, SDK-Consumer-Smoke-Test |
| `L1-04-P` | `msbuild-headless-sdk` | stabile Einzel-/Gruppen-Targets, inkrementelle Manifeste, Clean/Rebuild und TypeLib-Outputs; `CliProcessTests`, `VBProjectGroupCompilationTests` |
| `L1-04-Q` | `msbuild-headless-sdk` | `DesignTimeBuild`, NuGet-Paketierung und deklarative Validierung ohne Visual-Studio-CPS; `CliProcessTests`, SDK-README |

Die Karten `A → I` teilen Persistenz, Host und Zeichnen; `J → N` teilen generische ActiveX-
Verträge und Enterprise-Artefakte; `O → Q` bleiben headless und setzen keine IDE voraus. Jede
Karte erhält vor der Implementierung eine eigene Matrix-Erwartungs-ID und höchstens vier
gezielte Tests.

Die Materialisierung verwendet die IDs `l1-03-a` bis `l1-03-q` sowie `l1-04-a` bis
`l1-04-q`; jede ID ist genau einer Karte zugeordnet und bleibt bis zur Abnahme
`planned`/`not-yet-verified`.

### L2 — Sprache, Variant und Fehlerautomat

In dieser Reihenfolge entstehen Karten für Grammatik/Kontextregeln, Named Arguments und
Auswertungsreihenfolge, die zentrale Variant-Promotionstabelle, Null/Empty/Missing/Error/
Decimal-Sonderfälle, Objekt-/Arrayvarianten, `Let`/`Set`/Default-Member/`As New`, Collection,
`Implements`/Events/`WithEvents`, verschachteltes `On Error`/`Resume` sowie `VarPtr`/`StrPtr`/
`ObjPtr`/`LSet`/native ByRef.

### L3 — Runtime, Datei-I/O und Projektartefakte

Kartenfamilien: verbleibende String-/Array-/Konvertierungs-/Information-Intrinsics, locale-
bewusste Date/Time-/Format-Fälle, Interaction/Environment/Registry/App, Screen/Printer/
Clipboard-Adapter, Textdatei- und Codepagefälle, Variant-Arrays/Objekte in Binary/Random,
komplexe UDT-/String-/Array-Records, vollständige `.vbp`/`.vbg`-Metadaten sowie `.frm`/`.frx`/
`.ctl`/`.ctx`/`.pag`/`.dob`/`.dsr`/`.res`-Persistenz.

### L4 — Win32-, TypeLib- und COM-ABI

Kartenfamilien: TypeLib-Aliase/Records/Pointer/C-Arrays, `IDispatch` mit LCID/Named Arguments/
`DISPID_VALUE`/`PROPERTYPUT`/`EXCEPINFO`, `Declare`-Signaturen, `AddressOf`-Callbacks,
VARIANT/BSTR/SAFEARRAY-Besitz und ByRef-Write-back, eigene COM-Server/ClassFactories/
Connection Points, `.tlb`-Emission, Registrierung/Manifest und ActiveX-EXE-Local-Server.

### L5 — Forms, Zeichnen und MDI

Kartenfamilien: Lifecycle/Defaultinstanzen/Modalität/Fokus/Tab/Z-Order, intrinsische Controls/
Menüs/Timer/Eventreihenfolge, Form-/Menü-/UserControl-Arrays, sichtbare und persistente
GDI-/DIB-Zeichenkontexte mit Clipping, sowie vollständiger MDI-Zustand.

### L6 — ActiveX, UserControls und Enterprise

Kartenfamilien: Stock-Control-Inventar und ABI-Fixtures, generisches TypeLib-ActiveX-Hosting,
Ambient Properties/Persistenz/Property Pages/Connection Points, echte UserControl-OLE-Verträge
und ausführbare DataEnvironment/DataReport/UserDocument/PropertyPage-Artefakte.

### L7 — SDK und Abschlussgate

Kartenfamilien: gepackte Resolver-Task, TypeLib-/COM-Output-Orchestrierung, NuGet-Consumer-
Smoke-Test, Raw-COM-Probes, Forms-Traces/Pixeltests und abschließende Matrix-/Dokumentations-
Synchronisation.

## Testtakt

Pro implementierter Karte:

```powershell
dotnet build <betroffenes-testprojekt> --configuration Release --no-restore -m:1
dotnet vstest <betroffene-test-dll> --TestCaseFilter:"FullyQualifiedName~<kartenfilter>"
git diff --check
```

Nach jeweils vier verifizierten Karten oder am Ende einer Kartenfamilie, je nachdem was zuerst
eintritt:

```powershell
.\build.ps1 -NoRestore -Configuration Release
```

Der Wellen-Gate ist nur grün, wenn Release-Build, alle Testprojekte, VISIA 40/40, der Matrix-
Readout aus `build.ps1` und das Deterministic-Verhalten unverändert grün sind. Native OCX-Tests
bleiben optional und werden nur mit einem expliziten x86-Testhost ausgeführt.

## Schnittstellen- und Kompatibilitätsregeln

- `VBCompatibilityProfile` bleibt assembly-/instanzgebunden; kein globaler Runtime-Schalter.
- Neue profilbewusste Runtime-Funktionen sind additive Overloads; der Deterministic-Default bleibt
  unverändert.
- Erweiterungen des Hostvertrags erfolgen über additive Capability-Interfaces oder kompatible
  Defaultimplementierungen.
- COM-/Automation-Layouts folgen den dokumentierten x86-Regeln; unsichere Pointer-/C-Array-Fälle
  bleiben diagnostisch sichtbar, bis eine eigene ABI-Karte sie abdeckt.
- Ohne installierten VB6-Compiler ist `documented-verified` der verbindliche Abschlussstatus;
  `oracle-verified` bleibt optional.
- LLVM, LSP, IDE, visueller Designer und Visual-Studio-CPS bleiben außerhalb dieses Abschlussplans.

## Abschlusskriterien

Der Managed-Abschluss gilt erst als erreicht, wenn alle Matrixerwartungen `implemented` und
`documented-verified` sind,
keine offene Karte außerhalb der ausdrücklich ausgeschlossenen Bereiche existiert, der kanonische
Build grün bleibt, VISIA 40/40 meldet und die Roadmap keine `[ ]`/`[~]`-Markierung außerhalb dieser
Ausnahmen mehr enthält.
