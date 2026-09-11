# Roadmap

Stand: 2026-09-10. Eine aktive Restliste für den Managed-Abschluss.
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
Messung vom 2026-09-11 auf `main` / `b4dc376` mit nicht committeten Änderungen, Lauf `20260911T084306Z-0ea5846c`:

| Messpunkt | Ergebnis | Aussagegrenze |
| --- | --- | --- |
| Release-Build | 0 Warnungen, 0 Fehler | `TreatWarningsAsErrors`: eine Warnung bricht den Build ab |
| Standardlauf, 13 Testprojekte | 1908 Fälle: 1908 bestanden, 0 fehlgeschlagen, 0 übersprungen | Serieller Lauf über alle Testprojekte |
| Nativer x86-Lauf mit `VB6_REQUIRE_NATIVE_OCX=1` | 93/93 bestanden, 0 übersprungen | Getrennter x86-Lauf der WinForms-Tests |
| Orakel-Gegenpruefung gegen VB6 SP6 | 11/11 bestanden, 0 übersprungen | Vergleich gegen VB6 SP6; deckt nur die Fläche ab, die ein Orakelfall stellt |
| VISIA-Analyse | 40/40 Projektitems, 0 Diagnosen | Analyse und Binden, keine Laufzeitabnahme der Anwendung |

Vollständiges Gate (Standardlauf und nativer x86-Lauf auf demselben Quellstand): **True**.
Der Laufbericht liegt unter `artifacts/verification-report.json` und wird nicht versioniert.
<!-- verification:roadmap-measurements:end -->

Standardlauf und nativer x86-Lauf stehen getrennt in der Tabelle und werden nie addiert. Die
früher genannte **1698** war genau so eine Summe — aus damals 1617 Standardfällen und 81
zusätzlichen x86-Ausführungen — und wurde jahrelang als Testzahl gelesen. Seit R0 schreibt
`build.ps1 -UpdateVerificationDocs` diese Tabelle aus `artifacts/verification-report.json`, statt
sie von Hand fortzuschreiben; Artefakte werden nicht versioniert.

<!-- verification:roadmap-matrix:begin -->
**Kompatibilitätsmatrix nach der Restplanung:** **189 Erwartungen**, davon **176 implemented**, **0 partial** und **13 planned**;
**168/189 documented-verified**, 13 `not-yet-verified`, 8 `oracle-verified`.
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
6. **Kein Orakel vorausgesetzt — aber eines benutzt, wo es erreichbar ist.** Offizielle
   VB6-Dokumentation, veröffentlichte Windows/OLE/COM-Verträge und unabhängig beobachtbares
   Komponentenverhalten bleiben die Grundlage: Der Compiler muss ohne ein Original gebaut und
   geprüft werden können, und CI setzt keines voraus. Seit dem 2026-09-10 steht daneben ein
   echtes **VB6 SP6**, das sich headless aufrufen lässt, und damit ändert sich die Praxis an einem
   Punkt: Ein strittiger Fall bleibt nicht mehr offen, bis eine belastbare Erwartung vorliegt — er
   wird gemessen. Ergänzende VBA-Quellen werden weiterhin ausdrücklich als solche benannt, und
   eine Zusage, für die kein Orakelfall existiert, bleibt `documented-verified`.

## Statusmodell und Arbeitsweise

Die Quelle für Karten, Status und Abhängigkeiten ist
[vb6-sp6-compatibility-matrix.json](vb6-sp6-compatibility-matrix.json).

- `implemented`: genau die beschriebene Erwartung ist umgesetzt.
  `partial`: ein konkret beschriebener Teil fehlt. `planned`: das Ziel ist noch offen.
- `verification` bleibt unabhängig. Eine geplante Erwartung steht auf `not-yet-verified`.
  `oracle-verified` verlangt einen echten Lauf gegen den Originalcompiler. Seit dem 2026-09-10 ist
  das erreichbar, und die Latte liegt entsprechend präzise: Eine Erwartung wird
  `oracle-verified`, wenn ein Fall in `tests/VB6.Compiler.Tests/Oracle*Tests.cs` **ihre ganze
  beschriebene Fläche** gegen das Original stellt und ohne Abweichung besteht. Ein Durchgang mit
  bekanntem Rest reicht nicht — dann bleibt die Erwartung `documented-verified` und der Rest wird
  eine Karte. Die ersten vier Durchgänge fanden je einen Rest und hielten die Achse deshalb auf 0.
  Seit dem 2026-09-10 steht `r1-division-result-type` als erste Erwartung darauf: alle 121
  Operandenpaare von `/` und die Präzision des Ergebniswerts, ohne Abweichung. Mechanisch geprüft
  wird davon die eine Hälfte — eine `oracle-verified`-Erwartung muss einen Fall in
  `tests/VB6.Compiler.Tests/Oracle*Tests.cs` nennen; dass dieser Fall die **ganze** Fläche stellt,
  bleibt eine Beurteilung.
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
  Native OCX-Abnahme erfolgt zusätzlich mit `-RequireNativeOcx` in geeigneter Umgebung, die
  Gegenprüfung gegen das Original mit `-RequireOracle` und gesetztem `VB6_ORACLE_PATH`. Beide sind
  **eigene Laufarten** im Bericht und werden nie in den Standardlauf summiert; die Messwerttabelle
  nennt jede von ihnen auch dann, wenn sie nicht lief — dann ist das die wichtigere Aussage.
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

### R4 — COM-Konsum, Emission und Binary Compatibility

Geschlossen. Alle fünf Karten stehen als `implemented` / `documented-verified` in der Matrix. Die
Abnahmen verlangten durchgehend etwas, das der Compiler nicht allein herstellen kann: einen echten
Fremdclient über die Prozessgrenze, registrierte Fremdbibliotheken, eine reg-freie Aktivierung.

Der Aufrufvertrag für UDT-Werte selbst ist als `managed-r5-record-dispatch` abgetrennt: Er
verlangt ein eigenes `IDispatch` für die erzeugten Klassen und berührt damit jede COM-gehostete
Klasse. Die Begründung steht in der Karte und ist gemessen.

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

**Die TypeLib-Metadaten** sind abgenommen. Ausgangspunkt war ein harter Befund: Eine Klasse mit
einem gewöhnlichen `Property Get`/`Let`-Paar konnte **gar keine** Typbibliothek erzeugen. Die
Ursache lag im Emitter, nicht im Writer — ein VB6-Property-Paar wurde als zwei gleichnamige
CLR-Methoden emittiert, ohne CLR-Property, und zwei Funktionen mit einem Namen sind in einer
Bibliothek mehrdeutig.

Heute ist eine Property **ein** Mitglied mit zwei Aufrufarten auf **einer** DISPID — auch
indiziert, auch als Get/Set-Paar, auch ein `Public`-Feld. Ein `Optional`-Parameter trägt
`PARAMFLAG_FOPT` und seine Vorgabe als `PARAMDESCEX`, `cParamsOpt` zählt die auslassbare Reihe.
Die Version kommt aus dem `.vbp` über die Assembly in die Bibliothek. Eine implementierte
Schnittstelle ist ein eigener Typ unter der IID der Assembly und hängt an der Coclass. Ein
Klassenmodul mit Ereignissen bekommt die Quelle `__Klasse` mit `IMPLTYPEFLAG_FSOURCE`. Ein
`Public Type` steht als `TKIND_RECORD` in der Bibliothek und wird von seinen Mitgliedern als
`VT_USERDEFINED` genannt; ein `Private Type` bleibt draußen.

Die DISPIDs stimmen zwischen Bibliothek und laufendem Server überein — dazu kam der Befund aus
Binary Compatibility, dass sie das vorher **nie** taten. Gemessen wird an der zurückgelesenen
Bibliothek und, für die Aufrufbarkeit, an einem Fremdclient im eigenen Prozess.

Was der Aufruf eines **UDT-Wertes** verlangt, war danach als eigener Vertrag abgetrennt:
`managed-r5-record-dispatch`. Er ist inzwischen geschlossen und steht als Teilvertrag von R5 unten.

| Karte | Nachweis |
| --- | --- |
| `managed-r4-vtable-out` | `ComVTableExecutionTests` |
| `managed-r4-binary-compatibility` | `BinaryCompatibilityTests`, `BinaryCompatibilityClientTests` |
| `managed-r4-automation-layouts` | `AutomationLayoutTests` |
| `managed-r4-typelib-metadata` | `TypeLibraryMemberSurfaceTests`, `TypeLibraryWriterTests` |

### Abgenommen aus R5: die eigene Dispatch-Fläche

`managed-r5-record-dispatch` ist geschlossen, bevor die übrige Etappe beginnt — er stammt aus R4
und hing dort nur an einem einzigen Punkt.

Der Weg dorthin war zuerst eine Messung, welcher Haken überhaupt greift: Ein verwaltetes Interface
mit `IID_IDispatch` gibt die CLR **nicht** heraus (`E_NOINTERFACE`), `ICustomQueryInterface` wird
dagegen für genau diese IID gefragt. Die eigene Fläche hängt deshalb dort, baut ihre vtable aus
Funktionszeigern und beantwortet `GetIDsOfNames` und `Invoke` selbst; `VBComEventSource` — ohnehin
Basis jeder COM-sichtbaren Klasse — trägt sie. Damit gehören DISPIDs, Namen und Marshalling dem
Compiler, und die Typbibliothek beschreibt genau das, was der Server tut.

Ein Record reist als echtes `VT_RECORD` mit einer **selbst implementierten** `IRecordInfo`. Deshalb
muss nichts registriert sein, und der Client liest die Felder mit demselben Interface, mit dem ein
VB6- oder C++-Client jeden UDT liest. Gemessen in beide Richtungen an einem Fremdclient im eigenen
Prozess: heraus `vt=36 name=TPunkt size=8 X=3,Y=4`; hinein ein vom Client über dieselbe
`IRecordInfo` gebauter Record mit 11 und 22, den der Server zu 33 addiert — ein falsches Layout
käme dort als falsche Zahl heraus, nicht als Fehlercode. Freigegeben wird über `VariantClear`, das
durch `RecordDestroy` des Servers läuft.

Zwei Befunde am Rand, beide teuer, wenn man sie übersieht: `ClassInterface` bleibt `AutoDual`, weil
die .NET-Klassenfabrik sonst `IID_IDispatch` bei der Erzeugung gar nicht mehr herausgibt und damit
jeder spät gebundene Client bricht — die eigene Fläche gewinnt trotzdem, über beide
Aktivierungswege gemessen. Und ByRef-Argumente müssen von Hand zurückgeschrieben werden: Das hatte
vorher die CLR getan, und ohne das gelingt der Aufruf, während die Zuweisung verschwindet.

| Karte | Nachweis |
| --- | --- |
| `managed-r5-record-dispatch` | `ComDispatchSurfaceTests`, `RecordDispatchClientTests` |

### Abgenommen aus R5: die stromgespeicherte Designer-Fläche

Ein Control entscheidet selbst, welche Persistenz es benutzt, und sagt es über die Schnittstelle,
die es anbietet. Gemessen an den elf registrierten Stock-Controls bietet **jedes**
`IPersistStreamInit` und **keines** das einfache `IPersistStream`; die beiden sind getrennte
Schnittstellen, die Init-Variante leitet nicht von der anderen ab. Jedes antwortet außerdem auf
`GetSizeMax` mit `E_NOTIMPL` — ein Container, der daraus einen Puffer bemisst, übergibt nichts,
also wächst der Strom mit.

Die Bytes bleiben dem Container undurchsichtig; das ist der Vertrag, nicht seine Schwäche. Die
Abnahme ist deshalb ein Rundlauf durch ein **zweites** Control, nicht eine Prüfung des Blocks:
`Min=5`, `Max=55`, `Value=42` kommen an, und was das Zielcontrol danach selbst schreibt, ist
bytegleich mit dem geladenen Block.

Entschieden hat den Schnitt der Zeitpunkt. Ein spätes `Load` wird **angenommen und ignoriert** —
ein erzeugtes OCX behält seine Vorgaben, ohne dass irgendetwas meldet. Der Block gehört dem
Control gegeben, bevor es entsteht, und die einzige Stelle, an die ein Container ihn so früh legen
kann, ist `AxHost.OcxState`. Dort sitzen zwei stumme Fallen: Der Puffer wird längenpräfixiert
gelesen (`Int32`-Länge, dann die Bytes), und der Speichertyp des öffentlichen Konstruktors ist die
**alte** AxHost-Konstante, die um eins auf `AxHost.StorageType` verschoben wird. Die `2` des Enums
für `StreamInit` wählt damit `Storage` — und ein Control, das Strombytes als Storage lesen soll,
reißt den Prozess ab. `WinFormsHost.TrySetPersistedState` nimmt deshalb `1` und lehnt ein bereits
erzeugtes Control mit `False` ab, statt den Ladeaufruf ins Leere laufen zu lassen.

| Karte | Nachweis |
| --- | --- |
| `managed-r5-stream-persistence` | `StreamPersistenceTests` |

### Abgenommen aus R5: das generierte UserControl im Fremdcontainer

Ein ActiveX-Control ist keine Schnittstelle, sondern ein Satz, und ein Container entscheidet an
genau diesem Satz, was er mit einem Control tun darf. Die Ausgangsmessung war eindeutig: Ein
kompiliertes `.ctl`, reg-frei aus dem eigenen Manifest aktiviert, beantwortete `IDispatch`,
`IProvideClassInfo` und `IConnectionPointContainer` — alle drei kamen von der CLR — und
`E_NOINTERFACE` auf **jede** OLE-Control-Schnittstelle. Es war ein Automationsobjekt, das aus
einem `.ctl` kam.

Der Bauweg entschied sich an einer zweiten Vorabmessung: Für `IID_IDispatch` verweigert die CLR
ein verwaltetes Interface, für eine OLE-IID gibt sie es heraus, und die vtable-Slots stimmen. Der
Vertrag steht deshalb als verwaltete Deklarationen auf `VBComUserControl`, die der Emitter für
eine Klasse aus einem `.ctl` wählt — und nur dafür: Eine PropertyPage und ein UserDocument tragen
dieselbe Designer-Fläche, sind aber keine Controls.

Drei Befunde daraus sind dauerhaft wichtig:

- **Der Zeitpunkt.** Der Designer-Umschlag eines erzeugten Controls steht in *seinem* Konstruktor
  und legt die Kinder über den Umgebungshost an. Ein Container erzeugt die Klasse mit
  `CoCreateInstance` — es gibt keinen früheren Moment als den Basiskonstruktor, und ohne Host läuft
  der Umschlag gegen nichts und das Control hat still keine Kinder. Die Presentation wird deshalb
  von dort aufgelöst, über eine **Namenssuche**, weil `VB6.Runtime` die WinForms-Assembly nicht
  referenzieren darf.
- **Die Auslieferung schlägt zurück.** Eine Komponente mit einem UserControl bringt den
  WinForms-Host mit und verlangt damit das WindowsDesktop-Framework. Eine In-Proc-.NET-Komponente
  kann keinen zweiten Frameworksatz in eine bereits initialisierte Runtime bringen: Ein einfacher
  .NET-Client scheitert bauartbedingt mit `0x800080A5`. Ein nativer Container hat keine vorgeladene
  Runtime und ist nicht betroffen; ein verwalteter muss selbst ein Desktopprozess sein.
- **Zwei Einheiten und ein Fensterstil.** Der OLE-Extent ist die *Inhalts*größe in HIMETRIC —
  `Form.Size` zu setzen ergab 136 Bildpunkte, wo 96 verlangt waren. Und `SetParent` allein genügt
  nicht: Ein als Toplevel erzeugtes Fenster behält `WS_POPUP`, und ein Popup-Kind eines fremden
  Fensters wird von niemandem geclippt oder gemalt.

Abgenommen in einem eigenen Containerprozess, der ein echtes Fenster mit eigener Pumpe besitzt und
keinen Verweis auf die Runtime des Compilers hat, durchgehend über rohe vtable-Slots:
`parent=container`, `childsize=200x120` aus dem Positionsrechteck, `movedsize=100x50` nach
`SetObjectRects`, `drawnpixels>0` in einem Kontext, den nur der Container besitzt, und
`eventcalls=1` an dessen Senke.

| Karte | Nachweis |
| --- | --- |
| `managed-r5-usercontrol-ole` | `OleControlSurfaceTests`, `UserControlSurfaceTests` |
| `managed-r5-usercontrol-presentation` | `UserControlPresentationTests`, `ControlPresentationTests` |


## Aktive Restliste

Die folgenden Karten sind `planned` / `not-yet-verified`. R0 bis R4 sind als Etappen geschlossen und
stehen als abgeschlossene Etappen darüber; ihre Nachweistabellen dort bleiben wahr für das, was sie
belegen. Die IDs in den Tabellen sind dieselben wie in der Matrix; die dortigen `dependsOn`-Listen
legen die ausführbare Reihenfolge fest. Bereits erfüllte fachliche Einzelverträge bleiben in der
Matrix erhalten und werden nicht neu implementiert.

Was von hier an offen ist, verlangte bisher durchgehend etwas, das der Compiler nicht allein
herstellen kann: einen unabhängigen Container, registrierte native Komponenten, eine laufende
Anwendung. **Seit dem 2026-09-10 kommt eine vierte Quelle dazu, und sie wiegt schwerer als die
anderen drei: ein echtes VB6 SP6** (`VB6.EXE 6.00.9782`), das sich mit `/make` headless aufrufen
lässt. Damit ist die Verifikationsachse `oracle-verified` erstmals überhaupt erreichbar — und beim
ersten Einsatz hat sie eine Zusage widerlegt, die als `documented-verified` geführt war.

### R1 — Sprach- und Runtime-Verträge: der Rest aus den Orakelmessungen

R1 ist als Etappe abgenommen und steht mit seinen Nachweisen oben. Neun Karten kamen danach hinzu,
und **keine** davon aus einer Lücke im Inventar: Sie stammen alle aus einer neuen Messmöglichkeit,
dem echten VB6 SP6. Eine ist bereits wieder geschlossen — die schwerste.

Das ist der wichtigere Teil des Befunds. Die Variant-Promotionstabelle stand mit 49 gemessenen
Operandenpaaren als vollständig korrekt in den Notizen — gemessen gegen das eigene Verständnis,
nicht gegen ein Original. Eine Messung gegen sich selbst ist ein Regressionsnachweis, kein
Vertragsnachweis; das gilt für jede Zusage, die hier `documented-verified` heißt.

Was die vier Durchgänge abgedeckt haben, und was sie **nicht** abdecken:

| Fläche | Gemessen | Ergebnis |
| --- | --- | --- |
| Ergebnistypen der Operatoren | 20 Operandenpaare, für `/` nachgemessen alle 121 | 17 stimmten; die 3 Abweichungen waren **eine** Regel, inzwischen geschlossen |
| Zahlenausgabe | 26 Ausdrücke, nachgemessen 51 plus die Schwellen 10⁻²⁰…10¹⁸ | 12 Abweichungen aus **vier** Ursachen, alle vier geschlossen |
| Fehlernummern | 26 Fehlerfälle | 25 stimmen, inklusive aller Verdächtigen des Sammelwerts 5 |
| Datei-Layouts (Binary) | 13 Werte, gegen Rohbytes | alle bytegleich |

Nicht gemessen und damit weiter offen: der `Random`-Modus von `Get`/`Put` jenseits der
Zeichenketten, DBCS- und Codepage-Grenzen, Datums- und Zeitformate, und jede Fläche, für die noch
kein Orakelfall geschrieben ist. Was kein Orakelfall stellt, bleibt `documented-verified`.

| Karte | Ziel und Abnahme |
| --- | --- |
| `r1-input-subtype` | **Subtyp der von `Input #` gelesenen Zahlen:** aus der Textform abgeleitet (`Decimal`, `Currency`), nicht pauschal `Double`. |
| `r1-chdir-missing-directory` | **Fehlernummer von ChDir:** ein fehlendes Verzeichnis meldet 76 (Path not found), nicht 53. |
| `r1-print-numeric-trailing-space` | **Nachlaufendes Leerzeichen von `Print`:** nach jeder Zahl und jedem Datum steht eines, nach String und Boolean nicht. |
| `r1-module-name-rules` | **Namensregeln für Module und Bezeichner:** die drei vom Original durchgesetzten Regeln werden gemeldet statt stillschweigend angenommen. |

Geschlossen sind inzwischen zwölf Karten, in der Reihenfolge ihrer Schwere: das Bytelayout eines
Strings bei `Put`,
der `String * n` und die Werte von `Write #` — die drei, die **Dateien** verfälschten statt Werte
oder Text und damit das Projektziel „ein altes `.vbp` wird ohne Quelltextänderung übersetzt" direkt
brachen —, der Ergebnistyp von `/`, der einen **Wert** verfälschte, und die vier Ausgabekarten, die
falschen Text bei richtigem Wert machten. Die Nachweise stehen im Changelog.

Acht Erwartungen der Matrix tragen seither `verification: oracle-verified`. Die Latte dafür steht
im Statusmodell oben; mechanisch geprüft wird nur ihre eine Hälfte. Der Wächter, der die Achse
vorher auf null hielt, ist durch einen ersetzt, der sie prüft statt sie zu verbieten.

**Die offenen Karten stammen inzwischen überwiegend aus den Messungen selbst**, und das ist der
wichtigere Befund dieser Etappe: Jede geschlossene Karte hat in ihrer Nachbarschaft neue
aufgedeckt. Sie fielen ausnahmslos erst auf, als eine geschriebene Datei **zurückgelesen** wurde
statt nur die Ausgabe verglichen — `Write #` schrieb ein Datum als Seriennummer, und erst als es
ein Datumsliteral wurde, war sichtbar, dass `CDate` einer reinen Zeitangabe das heutige Datum
gibt. Ein Vergleich von Ausgabe gegen Ausgabe hätte keinen der beiden gezeigt.

Keine der vier verbliebenen verfälscht noch einen **Wert**. `r1-input-subtype` betrifft den
beobachtbaren Subtyp einer gelesenen Zahl, die übrigen drei eine falsche Fehlernummer, ein
fehlendes Leerzeichen und eine Form, die das Original ablehnt, während wir sie annehmen.

### R5 — Forms, ActiveX und persistierte Artefakte

Nach R4.

Die vorhandenen WinForms-/AxHost-Adapter, intrinsischen Controls, PropertyBag-Pfade, die oben abgenommene Stromspeicherung und die ebenfalls abgenommenen OLE-Verträge generierter UserControls sind die Basis. Ein im Managed-Host ausführbares `.ctl` ist kein Beleg für vollständige OCX-Kompatibilität.

Kompilierte PropertyPages samt ApplyChanges gehören zum Managed-/COM-Umfang. Eine eigene Oberfläche zum visuellen Erstellen und Bearbeiten dieser Seiten gehört zur späteren IDE. DataEnvironment, DataReport und UserDocument werden an ihren tatsächlichen Daten-/Report-/Containerabläufen geprüft; reine Klassifikation oder Ausführung einer eigenen Testmethode reicht nicht. ADO/OLE DB werden konsumiert; Datenbank-Provider werden nicht neu implementiert.

Die Grafikimplementierung arbeitet derzeit auf verwalteten Bitmaps. Entscheidend sind belegte Pixel-/Eventergebnisse bei definierten Größen, Clipping und Skalierung; die Dokumentation behauptet dafür keine native DC-/DIB-Implementierung. Native OCX-Nachweise bleiben an konkrete registrierte Komponenten und den erzwungenen x86-Lauf gebunden.

Ein Lauf des Korpus am 2026-09-10 hat dieser Etappe einen **gemessenen Defekt** hinzugefügt, und er
ist der Grund, warum VISIA startet und trotzdem visuell nicht stimmt: Jedes Element eines
Designer-Control-Arrays bekommt die Eigenschaften des Blocks, der in der Designer-Datei zuerst
steht. Minimalfall gemessen — erwartet `'Eins'@8px 'Zwei'@60px 'Drei'@120px`, tatsächlich dreimal
`'Drei'@128px`. Er steht als eigene, sofort lauffähige Karte, weil er eng ist und
`managed-r5-forms` von seiner Behebung nicht geschlossen wird. Offen und **nicht gemessen** bleibt
daneben die leere Toolbar des Korpus; sie ist als Frage mit zwei Kandidaten auf
`managed-r5-paint-mdi` notiert.

| Karte | Ziel und Abnahme |
| --- | --- |
| `r5-designer-control-array` | **Designer-Eigenschaften je Element eines Control-Arrays:** Jedes Element trägt die Eigenschaften seines eigenen Designer-Blocks; die Reihenfolge der Blöcke in der Datei entscheidet nichts. |
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

Original-VB6-Gegenprüfung bleibt optional und als Verifikationsstatus getrennt sichtbar — sie ist seit dem 2026-09-10 verfügbar und läuft als eigene Laufart `oracle` über `build.ps1 -RequireOracle`. Optional heißt: Ihr Fehlen lässt das Gate nicht fallen, aber die Messwerttabelle nennt sie auch dann, und ohne sie bleibt jede Zusage dokumentationsgestützt. Ein dokumentationsbasierter Abschluss darf nicht als `oracle-verified` oder als mathematischer Beweis vollständiger Austauschbarkeit beworben werden. Änderungen am geprüften Stand erfordern passende neue Nachweise.

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
