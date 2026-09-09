# Roadmap

Stand: 2026-09-09. Eine aktive Restliste für den Managed-Abschluss.
Die chronologische Historie steht in [CHANGELOG.md](CHANGELOG.md).

## Produktziel und Grenzen

Ziel ist ein moderner Compiler, der bestehende VB6-Projekte unverändert nach .NET übersetzt:
Sprache, eigene Runtime, COM-/ActiveX-Konsum und -Emission, Forms, persistierte Projektartefakte
sowie ein headless-fähiges MSBuild SDK. Bekannte Semantik- und ABI-Lücken gehören zum Abschluss.
VISIA ist Regressionstestkorpus, kein eigenes Portierungsprodukt.

LLVM, LSP, eigene IDE, visueller Designer und Visual-Studio-CPS sind nachgelagerte Produkte.
Die folgende Roadmap beschreibt R0–R7 als verbindliche Managed-Reihenfolge.
Implementierungsarbeit startet jeweils mit einer konkreten Matrixkarte, nicht mit einer
unspezifischen Suche nach „weiteren Randfällen“.

## Gemessener Ausgangsstand

Die Tabelle unten wird von `build.ps1 -UpdateVerificationDocs` aus dem Laufbericht geschrieben,
nicht von Hand. Ein gewöhnlicher Build fasst dieses Dokument nicht an.

<!-- verification:roadmap-measurements:begin -->
Messung vom 2026-09-09 auf `main` / `9664a20` mit nicht committeten Änderungen, Lauf `20260909T190441Z-4dbd2109`:

| Messpunkt | Ergebnis | Aussagegrenze |
| --- | --- | --- |
| Release-Build | 0 Warnungen, 0 Fehler | `TreatWarningsAsErrors`: eine Warnung bricht den Build ab |
| Standardlauf, 13 Testprojekte | 1851 Fälle: 1851 bestanden, 0 fehlgeschlagen | Serieller Lauf über alle Testprojekte |
| Nativer x86-Lauf mit `VB6_REQUIRE_NATIVE_OCX=1` | nicht ausgeführt | Ein fehlender nativer Lauf ist kein bestandener; das Gate bleibt offen |
| VISIA-Analyse | 40/40 Projektitems, 0 Diagnosen | Analyse und Binden, keine Laufzeitabnahme der Anwendung |

Vollständiges Gate (Standardlauf und nativer x86-Lauf auf demselben Quellstand): **False**.
Der Laufbericht liegt unter `artifacts/verification-report.json` und wird nicht versioniert.
<!-- verification:roadmap-measurements:end -->

Standardlauf und nativer x86-Lauf stehen getrennt in der Tabelle und werden nie addiert. Die
früher genannte **1698** war genau so eine Summe — aus damals 1617 Standardfällen und 81
zusätzlichen x86-Ausführungen — und wurde jahrelang als Testzahl gelesen. Seit R0 schreibt
`build.ps1 -UpdateVerificationDocs` diese Tabelle aus `artifacts/verification-report.json`, statt
sie von Hand fortzuschreiben; Artefakte werden nicht versioniert.

<!-- verification:roadmap-matrix:begin -->
**Kompatibilitätsmatrix nach der Restplanung:** **172 Erwartungen**, davon **160 implemented**, **0 partial** und **12 planned**;
**160/172 documented-verified**, 12 `not-yet-verified`, 0 `oracle-verified`.
<!-- verification:roadmap-matrix:end -->

Das sind Statuszahlen definierter Erwartungen, keine Prozentangabe der VB6-Kompatibilität.
Die Erweiterung gegenüber 121 Erwartungen macht zuvor nicht atomar erfassten Restumfang sichtbar;
sie ist keine Verschlechterung des Compilerverhaltens.

## Bereits vorhandene Grundlage

- Direkte Pipeline: Lexer/Parser → Binder → typisiertes IR → CIL, Metadaten und Portable PDB.
  Es gibt keinen C#-/Roslyn-Zwischencode.
- Prozeduren, Klassen, Properties, Events, WithEvents, Implements, Arrays/UDTs, Variant-
  Grundlagen, Standardbibliothek und On-Error-/Resume-Kontrollfluss mit ausführenden Tests.
- Eigene Runtime, profilabhängige Locale-Verträge, Datei-I/O und Host-Schnittstellen.
- Projekt-/Gruppenauflösung, Designer-/FRX-/Ressourcenverarbeitung, COM-TypeLib-Import,
  comhost-/Manifest-/TypeLib-Ausgabe und ActiveX-EXE-Aktivierung.
- WinForms-/AxHost-Host mit intrinsischen Controls, Control-Arrays, Grafik, MDI und
  geprüften nativen x86-OCX-Pfaden.
- Gepackte SDK-Resolver-Task, deklarationsbasierte Input-/Output-Manifeste, inkrementelles
  Build, DesignTimeBuild, Clean/Rebuild und TypeLib-Ausgaben.

Diese Liste beschreibt die gemessenen Teilverträge. Die Restkarten unten begrenzen die
Vollständigkeitszusage, insbesondere bei Objektlebensdauer, Zeigern und externen COM-Verträgen.

## Verbindliche Entscheidungen

1. **Sprachsemantik in beiden Profilen.** Die geplanten Lebensdauer-/Zeigerkorrekturen gelten
   für `Deterministic` und `VB6Sp6`. Locale, Plattformvorgaben und erlaubte Erweiterungen
   bleiben profilabhängig. Der aktuelle Runtime-Code wird durch dieses Dokument nicht geändert.
2. **Legacy-Projekte defaulten auf x86.** x64/AnyCPU bleiben explizite Managed-Opt-ins;
   einzelne Quelldateien und die öffentliche Emit-API behalten ihre bisherigen Defaults.
   `VB6Sp6` verlangt x86 und weist compiler-eigene Spracherweiterungen zurück.
3. **Profilzustand reist mit der Assembly.** Kein globaler Runtime-Profilumschalter.
   `vbUseSystem` fragt in beiden Profilen ausdrücklich die Systemkultur ab; dies ist eine
   entschiedene Ausnahme und kein offener Zielkonflikt.
4. **Moderne Erweiterungen bleiben additiv.** Integer = 16 Bit, Long = 32 Bit,
   checked Arithmetik und Currency-Skalierung bleiben erhalten.
5. **Implementierung und Nachweis sind getrennt.** Dokumentiertes Sollverhalten, ein
   gemessener Teilvertrag und eine komplette native Ausführung sind unterschiedliche Nachweise.
   Ein vorhandener Test allein ersetzt keine fachliche Prüfung seiner Erwartung.
6. **Kein Orakel vorausgesetzt.** Offizielle VB6-Dokumentation, veröffentlichte Windows/OLE/COM-
   Verträge und unabhängig beobachtbares Komponentenverhalten bilden die Grundlage.
   Ergänzende VBA-Quellen werden ausdrücklich als solche benannt; strittige VB6-Fälle bleiben
   offen, bis eine belastbare Erwartung vorliegt.

## Statusmodell und Arbeitsweise

Die Quelle für Karten, Status und Abhängigkeiten ist
[vb6-sp6-compatibility-matrix.json](vb6-sp6-compatibility-matrix.json).

- `implemented`: genau die beschriebene Erwartung ist umgesetzt.
  `partial`: ein konkret beschriebener Teil fehlt. `planned`: das Ziel ist noch offen.
- `verification` bleibt unabhängig. Eine geplante Erwartung steht auf `not-yet-verified`.
  `oracle-verified` verlangt einen echten Lauf gegen den Originalcompiler.
- Neue Restkarten besitzen `milestone` und `dependsOn`. IDs bestehender Erwartungen bleiben
  stabil. Die Karte `l1-02-a-language-grammar-context` bezeichnet jetzt ausschließlich ihren
  gemessenen Modul-Sichtbarkeitsvertrag; der abgegrenzte R1-Sprachumfang ist in
  `managed-r1-grammar` inventarisiert.
- Ein Bereich ist umgesetzt, wenn alle zugeordneten Erwartungen umgesetzt sind; vollständig
  geplante Bereiche sind `planned`, gemischte Bereiche `partial`. Nicht verifizierte
  Kinder verhindern die Verifikationszusage für den Gesamtbereich. `gap` nennt offene IDs.
- Bei geplanten Karten verweisen `testRefs` auf vorhandene Baseline-/Regressionsdateien, damit
  die aktuelle Matrixprüfung weiter funktioniert. Diese Verweise sind **kein Nachweis** der
  zukünftigen Abnahme. Abhängigkeiten, Etappenreihenfolge und Bereichsstatus prüfen seit R0
  `CompatibilityMatrixStatusTests` automatisch.
- Pro Karte: Vertrag und Baseline messen, erforderliche Schichten ändern, zielgerichtete
  Tests und E2E-Nachweis ausführen, Status und Dokumentation fortschreiben, Changelog ergänzen.
  Ungültige VB6-Formen dürfen durch belegte Negativtests abgeschlossen werden.
- Der kanonische Lauf bleibt `build.ps1 -Configuration Release` mit seriellen Testprojekten.
  Native OCX-Abnahme erfolgt zusätzlich mit `-RequireNativeOcx` in geeigneter Umgebung.
  Vor einem Etappenabschluss müssen alle Karten der Etappe und ihre Abhängigkeiten geschlossen sein.

## Abgeschlossene Etappen

### R0 — Messwerte und Status bereinigen

Geschlossen. Die Karten stehen als `implemented` / `documented-verified` in der Matrix und
nicht mehr in der Restliste; die Nachweise sind hier festgehalten, damit sie nachvollziehbar
bleiben.

`build.ps1` liest für jeden Lauf dessen eigene TRX-Dateien, weist eine Datei zurück, die älter
ist als der Lauf, der sie erzeugt haben soll, und meldet Prozessfehler, fehlende Ergebnisdateien
und leere Läufe je mit ihrem Grund. Standardlauf, nativer x86-Lauf und Wiederholungen sind
getrennte Einträge: Nur die ersten beiden entscheiden das Gate, eine bestandene Wiederholung
macht einen fehlgeschlagenen Gesamtlauf nicht grün, und ein nicht ausgeführter nativer Lauf wird
als fehlend berichtet statt als bestanden. `artifacts/verification-report.json` hält Quellstand
samt Dirty-Kennzeichen, Zeitpunkt, Projektzähler, VISIA und Matrixzählung fest.

Die Statusregeln prüft jetzt eine Maschine, nicht mehr ein Leser: unbekannte und zyklische Abhängigkeiten,
Karten, die auf eine spätere Etappe warten, offene Karten ohne Etappe oder Roadmap-Eintrag,
Bereichsstatus, die nicht aus ihren Erwartungen folgen, und die in ROADMAP, README und CLAUDE.md
dokumentierten Zahlen. Jede Regel wurde durch einmaliges Brechen gegengeprüft.

`-UpdateVerificationDocs` schreibt die markierten Messwertblöcke aus dem Laufbericht und
verweigert das für einen Teillauf; ein gewöhnlicher Build fasst kein Dokument an. Die Prosa um
jeden Marker bleibt von Hand geschrieben — generiert werden die Zahlen und ihre Aussagegrenzen.

| Karte | Nachweis |
| --- | --- |
| `managed-r0-reporting` | `build.ps1`, `artifacts/verification-report.json`, `VerificationDocumentTests` |
| `managed-r0-status-checks` | `CompatibilityMatrixStatusTests`, `CompatibilityMatrixTests`, `build.ps1 -UpdateVerificationDocs` |

### R1 — Sprach- und Runtime-Verträge vervollständigen

Geschlossen. Sechzehn Karten stehen als `implemented` / `documented-verified` in der Matrix.

Die Etappe war zwischenzeitlich wieder offen. Der Breitendurchgang vom 2026-09-09 hat gezeigt,
dass die Inventare der beiden Sammelkarten aus den **vorhandenen Tests** gebildet waren und nicht
aus den dokumentierten Formen — sie belegten damit Qualität statt Vollständigkeit. Sieben Namen
banden gar nicht: `Beep`, `AppActivate`, `SavePicture`, `ChDrive`, `InputB`, `Stop` und `DoEvents`
in seiner Funktionsform. Alle sind nachgetragen, jeder mit eigener Karte.

Beim Nachtragen fiel ein achter Befund auf, den niemand gesucht hatte: Eine Funktion, deren
Parameter alle optional sind, gab ohne Klammern eine leere Argumentliste weiter und erzeugte eine
ungültige Assembly. `Dir$` ohne Klammern ist das kanonische VB6-Muster zum Weiterzählen einer
Dateisuche — es traf also idiomatischen Legacy-Code, nicht einen Randfall.

Zwei Entscheidungen daraus sind festgehalten: `Stop` ist bewusst **kein** Keyword-Token, weil VB6
ein reserviertes Wort nach einem Punkt erlaubt und `.Stop` eine gewöhnliche Control-Methode ist.
Und `DoEvents` ist jetzt eine Funktion, was den `IVBHost`-Vertrag geändert hat; die klammerlose
Anweisungsform bindet unverändert weiter.

Getragen hat die Etappe die Reihenfolge „erst messen, dann bauen". Jedes Inventar wurde aus
dokumentierten Formen gebildet und blieb endlich — keine dauerhaft offene Sammelzeile „alle
weiteren Randfälle". Mehrfach lautete der Befund, dass das Verhalten bereits stimmte und nur
ungetestet war: In der Variant-Promotionstabelle waren alle 49 gemessenen Operandenpaare
korrekt, und `managed-r1-intrinsics` erwies sich als Zusammenfassung von vierzehn bereits
atomar dokumentierten Verträgen statt als fehlende Runtime-Familie. Die vier `r1-*`-Karten
sind genau die dabei gefundenen echten Abweichungen — jeder Befund bekommt eine eigene
Erwartung mit Eingabe, Ergebnis und Diagnose, statt in einer Sammelkarte zu verschwinden.

Die Datei-Layouts wurden gegen Rohbytes abgenommen, nicht gegen einen Selbst-Roundtrip: Ein
`Put`/`Get`-Paar bestätigt nur sich selbst. Die Get-/Put-Verträge stützen sich dabei auf
benannte VBA-Dokumentation; das ist ein Vertragsbeleg, kein Original-VB6-Lauf, und deshalb
bleiben die Erwartungen `documented-verified` statt `oracle-verified`. Ebenso festgeschrieben
ist die Trennung zwischen einem deklarierten `Variant()`-Array, einem skalaren Variant mit
Array-Inhalt und Objektwerten: Die beiden letzten werden mit ihrer dokumentierten Ablehnung
abgenommen, statt einen Besitzvertrag zu erfinden.

| Karte | Nachweis |
| --- | --- |
| `managed-r1-grammar` | `CompilationTests`, `DeclarationShapeExecutionTests`, `StatementShapeExecutionTests`, `EvaluationOrderExecutionTests`, `ModulePropertyExecutionTests` |
| `managed-r1-udt-shapes` | `UdtShapeExecutionTests`, `DynamicUserDefinedTypeArrayExecutionTests`, `ReDimExecutionTests`, `ArrayBinderGuardTests` |
| `managed-r1-operators` | `VariantArithmeticTests`, `VariantEqualityExecutionTests`, `VariantObjectDispatchExecutionTests` |
| `managed-r1-conversions` | `VariantStateTests`, `CurrencyRuntimeTests`, `DateTimeRuntimeTests`, `VariantStateExecutionTests` |
| `managed-r1-intrinsics` | `StringIntrinsicRuntimeTests`, `MathRuntimeTests`, `FinancialIntrinsicTests`, `FormatStringInputTests`, `StandardLibraryIntrinsicExecutionTests` |
| `managed-r1-file-layout` | `FileRuntimeTests`, `FileIoExecutionTests`, `FileStatementGuardTests` |
| `managed-r1-profiles` | `CultureIndependenceTests`, `FileStringIoExecutionTests`, `ScalarStringIntrinsicExecutionTests` |
| `r1-grammar-return-error-number` | `GoSubReturnExecutionTests` |
| `r1-grammar-invalid-form-diagnostics` | `UncoveredDiagnosticTests` |
| `r1-grammar-array-option-base` | `ArrayExecutionTests` |
| `r1-udt-nested-array-value-copy` | `FixedUdtArrayExecutionTests` |
| `r1-intrinsics-missing-host-names` | `StandardLibraryHostContractExecutionTests`, `FileIoExecutionTests` |
| `r1-intrinsics-doevents-return` | `StandardLibraryHostContractExecutionTests`, `FormHostRuntimeTests` |
| `r1-intrinsics-bare-name-optionals` | `FileIoExecutionTests` |
| `r1-strings-inputb` | `FileIoExecutionTests`, `FileRuntimeTests` |
| `r1-grammar-stop-statement` | `ErrorHandlingParserTests`, `StatementShapeExecutionTests` |


### R2 — Deterministische Objektlebensdauer

Geschlossen. Terminate erfolgt beim Wegfall der letzten Referenz; der vollständige Besitzvertrag
mit seinen acht Familien und den sechs Abnahmeschritten steht in
[R2-OBJECT-LIFETIME.md](R2-OBJECT-LIFETIME.md).

Die Etappe war zwischenzeitlich wieder offen. Nicht die Lebensdauer, sondern die **Erzeugung**:
`l1-02-i-object-members-lifecycle` sagt „As New creates lazily" zu, und das hielt für drei der
vier Zugriffsformen. Ein Zugriff auf ein `Public`-Feld instanziierte nicht nach — lesend,
schreibend und über ein Arrayfeld, bei Locals wie bei Modulvariablen. Ursache war eine Zeile:
Ein Feldzugriff braucht einen Platz und lief damit an der einzigen Stelle vorbei, die die
Nachinstanziierung einsetzt. Die vier auslösenden Zugriffsformen stehen seither als Inventar an
der Fixstelle, weil nichts sonst eine fünfte bemerken würde.

Erzeugte Klassen sind über Aliase, Selbstzuweisung, ByRef/ByVal, Rückgaben, Felder, Variant-,
Array- und Collection-Speicher, `WithEvents`, behandelte Fehler, Initialisierungsfehler,
reentrante Terminierung, Zyklen, `End` und referenzierte Projektassemblies abgedeckt. Für COM
trägt jede Wertgrenze einen eigenen Helfer: direkte Aktivierung, fremdes Memberergebnis und
geliehener Wert sind unterschiedliche Verträge, und ein late-bound CLR-Ergebnis ist keiner von
beiden.

Der Nachweis ist in drei Stufen geführt, weil jede allein zu wenig sagt. Der **native Zähler**
wird gegen eine testeigene IUnknown-Identität gelesen: Jeder Übergang kehrt exakt auf seinen
Ausgangswert zurück, erzwungene GC-Läufe bewegen nichts, ein Release ohne Retain ist folgenlos.
Der **verwaltete Mithalter** überlebt Adoption und Freigabe durch VB6, weil Adoption einen Anteil
am Wrapper verbraucht und jedes gemarshallte COM-Ergebnis seinen eigenen mitbringt. Und über die
**Prozessgrenze** hält ein fremder Client den Server am Leben, nachdem die Runtime alle Slots
geleert hat; erst seine Freigabe beendet ihn.

Die Aussagegrenze steht ausdrücklich dabei: Die Zähler, die ein Client liest, gehören seinem
Proxy, nicht dem Objekt im Server. Der Grenzfall einer Adoption ohne eigenen Anteil ist als Test
festgehalten, obwohl ihn kein erzeugter Pfad erreicht — nicht als Nachweis, sondern als Wächter.

| Karte | Nachweis |
| --- | --- |
| `managed-r2-lifetime` | `ObjectLifetimeTests`, `ComReferenceCountTests`, `ClassTerminateGuaranteeExecutionTests`, `WithEventsExecutionTests`, `ManagedEmitterTests`, `LocalServerActivationTests` |
| `r2-asnew-field-instantiation` | `AsNewExecutionTests`, `ClassInstanceExecutionTests` |

### R3 — Adressierbarer Speicher und native ABI

Geschlossen. Alle sechs Karten stehen als `implemented` / `documented-verified` in der Matrix und
nicht mehr in der Restliste.

Der x86-Managed-Slice erzeugt Runtime-besessenen, GC-stabilen Speicher für sieben
Familien: Locals, Modulvariablen einschließlich `Static`-Locals, ByVal-Parameter, flache UDTs,
eindimensionale Arrayelemente, private Instanzfelder und beide String-Adressen. Native Änderungen
werden an Load, Store, ByRef-Adresse und Write-back synchronisiert; ein Arrayelement besitzt dabei
bewusst keine Kopierzelle, sondern macht den Speicher seines Arrays unbeweglich.

Für kontrollierte private x86-Aufrufe reicht `managed-r3-byref-alias` genau diese Zelle als
ByRef-Argument durch: `VarPtr` im Aufgerufenen ist damit die Adresse des Aufrufers. Fehlt dem
Argument noch eine Zelle, übernimmt eine aufrufgebundene Zelle die Übergabe, das Rückschreiben und
die Freigabe. `AddressOf`-Ziele, Ereignishandler und öffentliche Klassenmitglieder bleiben bei
Fehler 5, weil der Emitter dort nicht jede Aufrufstelle kontrolliert.

Die **Invalidierung** ist abgenommen und schließt alle fünf Enden ausführend ab: `ReDim` und
`ReDim Preserve` beenden die alte Adresse, `Erase` auf einem festen Array leert an Ort und Stelle
und behält sie, `Erase` auf einem dynamischen Array gibt die Variable frei und die nächste
Elementadresse meldet Fehler 9. Das Prozedurende hängt am Return-Terminator, nicht an einer Stelle
am Textende. Das Objektende hat eine eigene Route bekommen: Eine Klasse mit adressierbarem Feld
registriert sich auch ohne `Class_Terminate`, und die Zelle wird über eine enge
Markerschnittstelle freigegeben statt dadurch, dass irgendein `IDisposable`-Feld gefunden wird.

Der **SAFEARRAY-Vertrag** hebt die beiden Grenzen auf, die der Slot-Vertrag stehen ließ. Der
gepinnte Puffer trägt einen echten `oleaut32`-Deskriptor; `VarPtr` auf das ganze Array nennt ihn,
mehrdimensionale Elemente liegen in SAFEARRAY-Reihenfolge. Beides ist gegen Windows Automation
gemessen und zusätzlich aus VB6-Quelltext über die ganze Kette beobachtet.

Der **VARIANT-Vertrag** ist die einzige Familie, die keinen verwalteten Speicherplatz freilegen
kann: Ein Variant reist überall sonst als CLR-`object`. Die Zelle besitzt die sechzehn Byte
deshalb selbst und rechnet bei jedem Zugriff in beide Richtungen um. Der Subtyp ist dabei der
ganze Punkt — Empty, Null und Nothing sind verwaltet alle die Abwesenheit eines Werts, nativ aber
`VT_EMPTY`, `VT_NULL` und `VT_DISPATCH` mit Nullzeiger. Gemessen über alle drei Speicherfamilien.

Beim Messen kamen zwei Befunde heraus, die beim Lesen nicht sichtbar waren. Die Elementauswahl war
eine Ausschlussliste und ließ `VT_BOOL` durch — zwei Byte breit über einem einbyteigen CLR-`bool`,
also ein Deskriptor, der doppelt so viel Speicher verspricht wie da ist. Ersetzt durch eine
Invariante: `cbElements` muss die CLR-Schrittweite sein. Und die Freigabe stand auf der Annahme,
`FADF_STATIC` halte OleAut32 von den Nutzdaten fern; die Messung hat sie widerlegt.
`SafeArrayDestroy` nullt den Puffer auch mit gesetztem Flag, also gibt jetzt ausschließlich
`SafeArrayDestroyDescriptor` frei.


Das **Declare- und Callback-ABI** war beim Nachmessen bereits vollständig: 39 Fälle decken UDT-,
Zeiger-, String- und Array-Signaturen mit Besitz, Grenzen und Rückschreiben ab, und die
x64-Seite läuft mit, weil die Ausführungsfälle AnyCPU sind. Ergänzt wurde der eine fehlende Fall,
ein aufgehobener `AddressOf`-Zeiger, der nach echtem Speicherdruck über eine native API weiter
gerufen wird. Zum Abmelden gibt es bewusst keinen Weg: Die Registry hält den Delegaten bis zum
Prozessende, weil eine native Seite den Zeiger unbegrenzt behalten darf.
| Karte | Nachweis |
| --- | --- |
| `managed-r3-pointers` | `PointerIntrinsicTests`, `IrLowererTests`, `ManagedEmitterTests`, `VBAddressableCellTests`, `VBArrayTests` |
| `managed-r3-byref-alias` | `PointerIntrinsicTests`, `IrLowererTests`, `ManagedEmitterTests`, `VBAddressableCellTests` |
| `managed-r3-invalidation` | `PointerIntrinsicTests`, `ObjectLifetimeTests`, `VBAddressableCellTests`, `VBArrayTests` |
| `managed-r3-safearray` | `PointerIntrinsicTests`, `VBArrayTests` |
| `managed-r3-variant` | `PointerIntrinsicTests`, `VBAddressableCellTests`, `VariantStateTests` |
| `managed-r3-callback-abi` | `DeclarePInvokeExecutionTests`, `AddressOfExecutionTests`, `VBCallbackRegistryTests` |

## Abgenommene Teilverträge

Karten, die geschlossen sind, während ihre Etappe noch offene hat. Der Nachweis steht hier, die
Etappe selbst weiter unten in der Restliste.

### R4 — COM-Konsum, Emission und Binary Compatibility

Der **VTable-Ausgabeparameter** ist abgenommen. Gemessen wurde zuerst: Eine Sonde über die echte
`stdole`-Typbibliothek liest für `IFont.Clone` einen Parameter mit `wParamFlags = 0x2`, also
`PARAMFLAG_FOUT` und nicht `FRETVAL`, bei Slot 20 und Rückgabe `VT_HRESULT`.

Der Unterschied ist der ganze Vertrag: Ein RETVAL ist der Wert, den der Ausdruck liefert und der
gar nicht im Quelltext steht; ein Ausgabeparameter ist ein Argument, das das Programm mitgibt und
danach liest. Die VB6-Form ist deshalb `f.Clone g` und nie `Set g = f.Clone`.

Der Importer löst für so einen Parameter genau eine Zeigerebene auf — `IFont**` wird `IFont` —,
sonst hieße er `Object` und `f.Clone g` scheiterte an der ByRef-Typprüfung. Der Delegat bekommt
einen ByRef-Slot, das Rückschreiben läuft über dasselbe Argumentarray, mit dem der Aufruf kam.
Ausgeführt gegen ein registriertes `StdFont`: Der Klon trägt den Zustand des Originals, ist ein
**anderes** Objekt und danach unabhängig.

**Binary Compatibility** ist ebenfalls abgenommen. `CompatibleMode=2` mit `CompatibleEXE32` liest
die Typbibliothek der genannten Komponente — eingebettet oder daneben liegend — und übernimmt
Bibliotheks-Id, CLSIDs, IIDs und DISPIDs. Ein fehlender Vorgänger ist kein Fehler: Die erste
Version einer Komponente hat nichts, wozu sie kompatibel bleiben könnte.

Die Abnahme ist ein Fremdclient in einem **eigenen Prozess**. Er aktiviert die *neue* Komponente
registrierungsfrei über ihren comhost mit der CLSID der *alten* und ruft ein Mitglied über die
DISPID der alten Bibliothek auf. Er kennt keinen einzigen Mitgliedsnamen — genau wie ein Client,
der gegen die Vorversion gebaut wurde.

Dabei kam ein echter Defekt heraus: Die DISPIDs der Typbibliothek waren gar nicht die, auf die der
laufende Server antwortet. Die CLR liest ihre Nummern aus `DispIdAttribute`, die Bibliothek zählte
selbst — der Aufruf endete in `DISP_E_MEMBERNOTFOUND`. Der Emitter vergibt sie jetzt in
Deklarationsreihenfolge und stempelt sie; die Bibliothek liest sie von dort. Damit stimmen
TypeLib und Server über die Nummern überein, was auch `managed-r4-typelib-metadata` verlangt.

Ein weggefallenes Mitglied meldet `VB6E0004` und bricht die Emission ab, bevor irgendetwas
geschrieben wird. Ein hinzugekommenes ist verträglich — ein Client, der es nicht kennt, ruft es
nicht.

**Die rohen Automation-Layouts** sind abgenommen — an registrierten Fremdbibliotheken, nicht an
eigens gebauten Fixtures: Eine eigene Fixture belegt nur, dass der Importer mit sich selbst
übereinstimmt. Zuerst wurde gemessen, welche der fünf Formen auf der Maschine überhaupt vorkommen;
echte `VT_SAFEARRAY`-Mitglieder finden sich erst in `taskschd` und `mshtml`.

Ein skalarer Alias löst auf seinen Basistyp auf — über *alle* Aliase der Bibliothek gegen deren
eigenes `tdescAlias` geprüft, nicht gegen eine Liste im Test. Ein C-Array-Feld behält seine festen
Grenzen (`GUID.Data4` als `0:7`). Ein Zeigerparameter wird genau eine Ebene aufgelöst; eine zweite
bleibt ein undurchsichtiger nativer Zeiger. Der Record trägt auf x86 exakt das native Layout —
`LenB(EXCEPINFO)` ist 32, ausgeführt auf dem x86-Host, und die Erwartung ist aus den
Felddeskriptoren der Bibliothek gerechnet statt hingeschrieben.

Der SAFEARRAY läuft durch einen echten Out-of-Process-Server (`Schedule.Service`): hinein ein
VB6-Array mit den Grenzen 1:2, heraus `VarType` 8204 mit denselben Grenzen und Werten, und das
eigene Array des Aufrufers ist danach unverändert — der Server kopiert, er übernimmt nicht.

Gemessene Abweichung, bewusst nicht geändert: Auf **x64** stimmt das Recordlayout nicht, aus zwei
getrennten Gründen — VB6 packt einen UDT auf 4, das native x64-ABI richtet auf 8 aus, und ein
Zeigerfeld, das VB6 `Long` nennt, ist dort 8 Byte breit. Beides sind Entscheidungen über die
moderne Erweiterung, nicht über VB6.

| Karte | Nachweis |
| --- | --- |
| `managed-r4-vtable-out` | `ComVTableExecutionTests` |
| `managed-r4-binary-compatibility` | `BinaryCompatibilityTests`, `BinaryCompatibilityClientTests` |
| `managed-r4-automation-layouts` | `AutomationLayoutTests` |

## Aktive Restliste

Die 14 folgenden Karten sind `planned` / `not-yet-verified`. R0 bis R3 sind geschlossen und stehen
als abgeschlossene Etappen darüber. Die IDs in den Tabellen sind dieselben wie in der Matrix; die
dortigen `dependsOn`-Listen legen die ausführbare Reihenfolge fest. Bereits erfüllte fachliche
Einzelverträge bleiben in der Matrix erhalten und werden nicht neu implementiert.

Was von hier an offen ist, verlangt durchgehend etwas, das der Compiler nicht allein herstellen
kann: einen echten Fremdclient über die Prozessgrenze, registrierte native Komponenten, eine
laufende Anwendung. Das ist der Grund, warum R4 die schwerste Etappe ist und nicht die größte.

### R4 — COM-Konsum, Emission und Binary Compatibility

Nach R3.

Der VTable-Ausgabeparameter ist abgenommen und steht als Teilvertrag darüber. Rohe Layouts werden weiterhin in beide Richtungen mit unabhängigen Probes geprüft.

Heute werden COM-Identitäten aus Namen abgeleitet und Version/Binary-Compatibility-Einstellungen gelesen. Das ersetzt nicht die Auswertung der mit `CompatibleEXE32` angegebenen älteren Komponente. Die Abnahme verlangt einen bereits gebauten Fremdclient, der nach einer kompatiblen Serveränderung weiterläuft; inkompatible Änderungen müssen diagnostiziert werden. TypeLib, Assembly und Host müssen identische DISPIDs, Signaturen, Interfaces und Versionsinformationen liefern.

ClassFactory-/IUnknown-Lebensdauer, Instancing, Event-Quellen und Connection-Point-Enumeratoren werden vervollständigt. Die vorhandene Aktivierung über comhost, registry-free Manifest und ActiveX-EXE bleibt als bereits gemessene Grundlage erhalten.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r4-typelib-metadata` | **TypeLib-Metadaten vervollständigen:** Interfaces, Properties, Events, optionale Parameter, DISPIDs, Versionen und UDTs müssen in TypeLib/Assembly/Host/Registrierung übereinstimmen und von Fremdclients aufrufbar sein. Sechs dieser Punkte sind seit 2026-09-09 umgesetzt und an der zurückgelesenen Bibliothek gemessen; offen bleiben UDTs als `TKIND_RECORD` samt `VT_RECORD`-Marshalling und die Abnahme durch einen Fremdclient. |
| `managed-r4-server-lifetime` | **Server- und Event-Ownership schließen:** IUnknown/ClassFactory, Instancing, Connection-Point-Enumeratoren, Attach/Detach und Shutdown per Fremdclient prüfen; vorhandene Enumeration-Stubs schließen. |

### R5 — Forms, ActiveX und persistierte Artefakte

Nach R4.

Die vorhandenen WinForms-/AxHost-Adapter, intrinsischen Controls und PropertyBag-Pfade sind die Basis. Stream-only-Persistenz und die OLE-Verträge generierter UserControls brauchen eigene Abnahmen in einem unabhängigen Container. Ein im Managed-Host ausführbares `.ctl` ist kein Beleg für vollständige OCX-Kompatibilität.

Kompilierte PropertyPages samt ApplyChanges gehören zum Managed-/COM-Umfang. Eine eigene Oberfläche zum visuellen Erstellen und Bearbeiten dieser Seiten gehört zur späteren IDE. DataEnvironment, DataReport und UserDocument werden an ihren tatsächlichen Daten-/Report-/Containerabläufen geprüft; reine Klassifikation oder Ausführung einer eigenen Testmethode reicht nicht. ADO/OLE DB werden konsumiert; Datenbank-Provider werden nicht neu implementiert.

Die Grafikimplementierung arbeitet derzeit auf verwalteten Bitmaps. Entscheidend sind belegte Pixel-/Eventergebnisse bei definierten Größen, Clipping und Skalierung; die Dokumentation behauptet dafür keine native DC-/DIB-Implementierung. Native OCX-Nachweise bleiben an konkrete registrierte Komponenten und den erzwungenen x86-Lauf gebunden.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r5-stream-persistence` | **Stream-basierte Control-Persistenz:** IPersistStreamInit-Zustand laden/sichern; InitNew, fehlende Schnittstelle und beschädigten Stream mit einer passenden Control-Fixture prüfen. |
| `managed-r5-usercontrol-ole` | **Generierte UserControls im Fremdcontainer:** Kompilierte ctl-Komponente unabhängig aktivieren, zeichnen, speichern, laden und freigeben; OLE View/In-Place, Ambient Properties und Events prüfen. |
| `managed-r5-property-pages` | **PropertyPage-COM-Vertrag:** Kompilierte pag-Artefakte im vorhandenen externen Container ausführen; ApplyChanges erreicht das Control und Persistenz, eigene Designer-UI bleibt späteres Produkt. |
| `managed-r5-enterprise` | **Enterprise-Artefakte ausführen:** DataEnvironment-Kommandos, DataReport-Bindung/Ausgabe und UserDocument-Hosting über kontrollierte Fixtures/verfügbare ADO-Komponenten prüfen; fehlende Abhängigkeiten sichtbar lassen. |
| `managed-r5-forms` | **Forms- und Control-Verträge schließen:** Start-/Defaultinstanz, Unload/Wiederladen, Fokus, Tab/Z-Order, Modalität, Menüs, Control-Arrays und Stock-Events auf Identität/Ereignisreihenfolge prüfen; native Fälle verlangen x86. |
| `managed-r5-paint-mdi` | **Sichtbare Zeichen- und MDI-Abläufe abnehmen:** Paint/AutoRedraw, aktive/persistente Flächen, Clipping, Skalierung sowie MDI-Menü/Fokus über Pixel- und Eventprüfungen abnehmen; Bitmap-Implementierung korrekt benennen. |

### R6 — Anwendungs- und SDK-Abnahme

Nach R5.

VISIA bleibt unverändert und erhält festgelegte Laufzeitszenarien: Start, Projekt laden, zentrale Fenster/Controls, Dateioperationen und Beenden. Erwartete Ausgaben, Dateien und Eventabläufe müssen reproduzierbar sein. Zusätzlich entsteht ein repo-eigenes VB6-Referenzprojekt für Geschäftslogik mit Forms, Klassen, Datei-I/O und ADO; es wird nicht als fremder Legacy-Korpus bezeichnet.

Das SDK ist bereits gepackt nutzbar: Resolver-Task, exakte Manifeste, DesignTimeBuild, inkrementelle Builds, Clean/Rebuild und TypeLib-Ausgaben sind implementiert. Die offene Karte betrifft ihre gemeinsame Anwendungs-/Deployment-Abnahme. Ausgabeverzeichnisse müssen startfähig sein, fehlende Artefakte repariert werden und unabhängige Dateien bei Clean erhalten bleiben.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r6-visia-workflows` | **VISIA-Laufzeitszenarien:** Unverändertes VISIA: Start, Projekt laden, zentrale Fenster/Controls, Dateioperationen und Beenden mit festen Ausgaben, Dateien und Eventabläufen prüfen. |
| `managed-r6-business-reference` | **VB6-Geschäftslogik-Referenzprojekt:** Repo-eigenes versioniertes VB6-Projekt mit Forms, Klassen, Datei-I/O und ADO reproduzierbar gegen unabhängige fachliche Erwartungen prüfen; nicht als fremden Legacy-Korpus ausweisen. |
| `managed-r6-sdk-deployment` | **SDK und Deployment gemeinsam abnehmen:** Gepacktes SDK, vbp/vbg, COM/Ressourcen, Output-Start, No-op, Reparatur und Clean/Rebuild zusammen prüfen; nur manifestierte Dateien entfernen. |

### R7 — Managed-Abschluss

Nach R6 und allen übrigen Managed-Erwartungen.

Abschluss bedeutet: keine offene Implementierung im zugesagten Managed-Umfang, vollständiger grüner Standardlauf, verpflichtender nativer x86-Lauf und bestandene Anwendungsszenarien auf demselben Quellstand. Wiederholungen dürfen keinen fehlgeschlagenen Gesamtlauf verdecken. Die erforderlichen nativen Komponenten werden dokumentiert; ihr Fehlen ist keine erfolgreiche Prüfung.

Original-VB6-Gegenprüfung bleibt optional und als Verifikationsstatus getrennt sichtbar. Ein dokumentationsbasierter Abschluss darf nicht als `oracle-verified` oder als mathematischer Beweis vollständiger Austauschbarkeit beworben werden. Änderungen am geprüften Stand erfordern passende neue Nachweise.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r7-release` | **Managed-Abschlussgate:** Alle Managed-Erwartungen schließen; vollständige Standard-/native x86-Läufe und Anwendungsszenarien auf demselben Quellstand; Wiederholungen nicht zu grünem Gesamtlauf zusammenführen. |

## Historische Zuordnung

Die alten Nummern bleiben als Orientierung erhalten, nicht als zweite aktive Aufgabenliste.
Details früherer Schritte stehen im Changelog und in der Git-Historie.

| Bisheriger Abschnitt | Neue Zuständigkeit |
| --- | --- |
| Etappe A: Matrix | R0, Inventar in R1 |
| Etappe B: Sprache/Objekte | R1, R2 und R3 |
| Etappe C: Runtime/Dateien/Projekte | R1, R4 Binary Compatibility und R6 |
| Etappe D: COM/ABI | R3 und R4 |
| Etappe E: Forms/Grafik/MDI | R5 |
| Etappe F: ActiveX/Enterprise | R5 |
| Etappe G: SDK | Bereits implementierte Basis; gemeinsame Abnahme in R6 |
| Etappe H: Abschluss | R0-Prüfregeln und R7 |
| M0: Paritätsmessung | Vorhandene Basis, Laufzeitabnahme R6 |
| M1–M2: Literale/Syntax | Vorhandene Basis, Inventar R1 |
| M3: Arrays/UDTs | R1 sowie native Layouts R3/R4 |
| M4–M5: Variant/Klassen | R1, R2 und R4 |
| M6: IR/Fehlerbehandlung | Vorhandene Basis; Ownership-/Fehlerpfade R2 |
| M7: Standardbibliothek | R1 |
| M8: Interop/SDK/LLVM | R3/R4/R6; LLVM bleibt separater Ausblick |
| M9: Forms | R5 |
| M10: LSP/IDE | Nachgelagerter Ausblick |

## Ausblick nach dem Managed-Abschluss

Diese Meilensteine sind zurückgestellt und zählen nicht zum R7-Gate:

1. **LSP:** projekt-/workspaceweite Symbolauflösung, kontextabhängige Completion und
   Buildintegration auf dem vorhandenen Diagnose-/Navigation-Slice. Abnahme an Mehrprojekt-
   Workspaces mit konsistenten Compilerdiagnosen.
2. **IDE und Debugger:** eigenständige Arbeitsumgebung, Projektmodell, Build-/Startabläufe,
   Breakpoints, Schritte, Locals und Fehlernavigation auf den erzeugten VB6-PDBs.
3. **Visueller Designer:** verlustfreier FRM/FRX-Roundtrip, Controls, PropertyPages und
   Ereignisverdrahtung; Speichern ohne fachliche Quelltextverluste. Installations-/Verteilpakete
   sind eine gesonderte Produktaufgabe, kein Nebenprodukt eines Compilerlaufs.
4. **Optionales LLVM-Backend:** erste Aufgabe ist ein echter Assemble-/Link-/Ausführungstest
   für x86/x64. Die bestehenden IR-Texttests belegen keine native Lauffähigkeit.
   Weitere Runtime-/COM-/Debug-Verträge folgen erst auf dieser Basis.

Es werden keine Kalendertermine aus der Anzahl der Matrixkarten abgeleitet. Die Etappen haben
unterschiedlichen Umfang; ihre Fertig-Marke ist jeweils die genannte Abnahme.
