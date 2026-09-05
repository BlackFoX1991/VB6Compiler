# Roadmap

Stand: 2026-09-05. Eine aktive Restliste für den Managed-Abschluss.
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
Messung vom 2026-09-05 auf `main` / `c82639c`, Lauf `20260905T122539Z-0498127b`:

| Messpunkt | Ergebnis | Aussagegrenze |
| --- | --- | --- |
| Release-Build | 0 Warnungen, 0 Fehler | `TreatWarningsAsErrors`: eine Warnung bricht den Build ab |
| Standardlauf, 13 Testprojekte | 1636 Fälle: 1636 bestanden, 0 fehlgeschlagen | Serieller Lauf über alle Testprojekte |
| Nativer x86-Lauf mit `VB6_REQUIRE_NATIVE_OCX=1` | 81/81 bestanden, 0 übersprungen | Getrennter x86-Lauf der WinForms-Tests |
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
**Kompatibilitätsmatrix nach der Restplanung:** **151 Erwartungen**, davon **126 implemented**, **0 partial** und **25 planned**;
**126/151 documented-verified**, 25 `not-yet-verified`, 0 `oracle-verified`.
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
  gemessenen Modul-Sichtbarkeitsvertrag; der weitere Sprachumfang steht in `managed-r1-grammar`.
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

## Aktive Restliste

Die 25 folgenden Karten sind `planned` / `not-yet-verified`. R0 ist geschlossen und steht als
abgeschlossene Etappe darüber. Die IDs in den Tabellen sind dieselben wie in der Matrix; die
dortigen `dependsOn`-Listen legen die ausführbare Reihenfolge fest. Bereits erfüllte fachliche
Einzelverträge bleiben in der Matrix erhalten und werden nicht neu implementiert.

### R1 — Sprach- und Runtime-Verträge vervollständigen

Nach R0; die Abhängigkeiten innerhalb der Etappe stehen auf den Karten.

Zuerst messen, anschließend belegte Fehler korrigieren. Das Inventar wird aus dokumentierten Formen aufgebaut und ist endlich: keine dauerhaft offene Sammelzeile „alle weiteren Randfälle“. Jede gefundene Abweichung bekommt eine eigene Erwartung mit Eingabe, Ergebnis/Typ oder Diagnose. Ein bestandener Selbst-Roundtrip beweist weder ein Dateiformat noch eine native ABI; dafür sind unabhängige Bytes bzw. Fremdkomponenten erforderlich.

Bei Datei-I/O erlaubte `Variant()`-Arrays von einem skalaren Variant mit Array-Inhalt sowie von Objektwerten unterscheiden. Die [Get-](https://learn.microsoft.com/en-us/office/vba/language/reference/user-interface-help/get-statement) und [Put-Dokumentation](https://learn.microsoft.com/en-us/office/vba/language/reference/user-interface-help/put-statement) beschreibt diese Unterschiede sowie Binary-/Random-Stringdeskriptoren. Diese VBA-Quellen sind ein benannter Vertragsbeleg, kein Original-VB6-Lauf. Dokumentierte Verbote werden als Negativfälle abgenommen; Widersprüche zwischen Quelle und bisherigem Test bleiben sichtbar, bis die Erwartung fachlich geklärt ist.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r1-grammar` | **Grammatik und Kontext inventarisieren:** Endliches Inventar von Deklarationen, Statements, Sichtbarkeit und Auswertungsreihenfolge; gültige Formen ausführen, ungültige Formen gezielt diagnostizieren. |
| `managed-r1-udt-shapes` | **Array- und UDT-Grenzen prüfen:** Rang, Bounds, feste/dynamische Felder, Wertkopien und erlaubte UDT/Variant-Grenzen prüfen; dokumentiert verbotene Formen bleiben Negativtests. |
| `managed-r1-operators` | **Operator- und Default-Member-Tabelle schließen:** Ergebnistyp, Wert, Reihenfolge und Fehlernummer über skalaren Subtypen, Null/Empty/Error, Objekt-Default-Membern und Arrays unabhängig messen. |
| `managed-r1-conversions` | **Konvertierungen und Promotionsmatrix prüfen:** Missing, Error, Decimal, Date und Currency über implizite/explizite Konvertierungen, Rundung und Overflow abnehmen; strittige Alt-Tests als offene Nachweise ausweisen. |
| `managed-r1-intrinsics` | **Standardbibliothek abschließend inventarisieren:** String, Math, Financial, Datum/Zeit, Format, Information und Host-Intrinsics einschließlich Rückgabetyp, Dollar-Form, optionalen Argumenten und Fehlergrenzen messen. |
| `managed-r1-file-layout` | **Datei-Layouts bytegenau abnehmen:** Binary/Random-Strings, Variant(), skalare Variants, UDTs, Deskriptoren und Record-Grenzen gegen unabhängige Bytes prüfen; Objektwerte und skalare Variants mit Array-Inhalt von erlaubten Arrays trennen. |
| `managed-r1-profiles` | **Profilgrenzen absichern:** en-US/de-DE/ja-JP, ANSI/DBCS, vbUseSystem und Debug-/Financial-Ausgabe in gemischten Assemblies prüfen; gemeinsame Sprachsemantik und explizite Locale-Grenzen erhalten. |

### R2 — Deterministische Objektlebensdauer

Nach R1.

Die Runtime führt explizite Referenzverwaltung für VB6-Objekte; das IR trägt Besitzübergänge an allen Wert-/Referenzgrenzen. Dazu gehören Locals, Modul-/Klassenfelder, Parameter, Rückgaben, Variant-/Array-/Collection-Speicher, Events und COM. Neue Referenzen werden vor dem Freigeben ersetzter Referenzen gesichert, damit Selbstzuweisung und Aliasbildung kein lebendes Objekt terminieren.

Heute registriert `VBObjectLifetime` Terminatoren für Finalizer/Prozessabbau. Das garantiert noch nicht den VB6-Zeitpunkt. Ziel ist Terminate bei der letzten Referenz in beiden Profilen, einschließlich kontrollierter Fehler- und Reentranzpfade. Zyklen und abruptes `End` müssen gesondert gegen den Sprachvertrag geprüft werden; ein pauschaler Shutdown-Drain ersetzt diese Regeln nicht. Keine neue VB6-Syntax ist vorgesehen.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r2-lifetime` | **Referenzgezählte Objektlebensdauer:** Terminate beim Wegfall der letzten Referenz; Aliases, ByRef, Rückgaben, alle Speicherformen, Events/COM, Selbstzuweisung, Initialize-Fehler, Reentranz, Zyklen und Programmende ohne vorzeitige/doppelte Terminierung prüfen. |

### R3 — Adressierbarer Speicher und native ABI

Nach R2.

Adressierter Speicher erhält einen von der Runtime besessenen, GC-stabilen Speichervertrag. ByRef-Aliase, native Layouts und Laufzeitverwaltung werden gemeinsam entworfen; lediglich für einen einzelnen Declare-Aufruf erzeugte Kopien erfüllen den Vertrag gespeicherter Zeiger nicht. Nicht adressierte Werte behalten ihren bisherigen schnellen Speicherpfad.

`VarPtr`/`StrPtr`, BSTR, VARIANT, SAFEARRAY, UDTs und Callbacks müssen dieselben Lebensdauer- und Write-back-Regeln verwenden. Gültigkeit gilt für die definierte Speicherlebensdauer, nicht unbegrenzt nach Freigabe oder Reallokation. x86 ist das Legacy-Abnahmeziel; bestehende x64-Erweiterungen erhalten eigene Prüfungen.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r3-pointers` | **Stabiler adressierbarer Speicher:** Gespeicherte VarPtr/StrPtr und ByRef-Aliase bleiben für ihre definierte Lebensdauer über GC gültig; native Schreibzugriffe, BSTR/VARIANT/SAFEARRAY/UDT, Freigabe und Reallokation prüfen. |
| `managed-r3-callback-abi` | **Declare- und Callback-ABI vervollständigen:** UDT-, Pointer-, String- und Array-Signaturen mit Ownership, Bounds und Write-back in x86 sowie unterstützten x64-Erweiterungen messen; zurückbehaltene Callbacks nach GC und beim Abmelden prüfen. |

### R4 — COM-Konsum, Emission und Binary Compatibility

Nach R3.

Der bestehende VTable-Pfad weist Ausgabeparameter mit `VB6S0075` ab. Er erhält echten Aufruferspeicher und Rückschreiben; FOUT und FRETVAL bleiben unterschiedliche Verträge. Rohe Layouts werden in beide Richtungen mit unabhängigen Probes geprüft.

Heute werden COM-Identitäten aus Namen abgeleitet und Version/Binary-Compatibility-Einstellungen gelesen. Das ersetzt nicht die Auswertung der mit `CompatibleEXE32` angegebenen älteren Komponente. Die Abnahme verlangt einen bereits gebauten Fremdclient, der nach einer kompatiblen Serveränderung weiterläuft; inkompatible Änderungen müssen diagnostiziert werden. TypeLib, Assembly und Host müssen identische DISPIDs, Signaturen, Interfaces und Versionsinformationen liefern.

ClassFactory-/IUnknown-Lebensdauer, Instancing, Event-Quellen und Connection-Point-Enumeratoren werden vervollständigt. Die vorhandene Aktivierung über comhost, registry-free Manifest und ActiveX-EXE bleibt als bereits gemessene Grundlage erhalten.

| Karte | Ziel und Abnahme |
| --- | --- |
| `managed-r4-vtable-out` | **VTable-Ausgabeparameter:** PARAMFLAG_FOUT, FIN/FOUT und FRETVAL korrekt unterscheiden; stdole.IFont.Clone mit echtem Aufruferspeicher und Write-back ausführen statt VB6S0075. |
| `managed-r4-automation-layouts` | **Rohe Automation-Layouts abnehmen:** Aliase, Records, C-Arrays, verschachtelte Pointer und SAFEARRAY-Typen über unabhängige COM-Probes mit Layout- und Besitzprüfung in beide Richtungen abnehmen. |
| `managed-r4-binary-compatibility` | **Binary Compatibility gegen ältere Komponente:** CompatibleMode/CompatibleEXE32 für bestehende Identitäten/Aufrufverträge auswerten; alter Client läuft nach kompatibler Änderung unverändert weiter, inkompatible Änderungen liefern Diagnose. |
| `managed-r4-typelib-metadata` | **TypeLib-Metadaten vervollständigen:** Interfaces, Properties, Events, optionale Parameter, DISPIDs, Versionen und UDTs müssen in TypeLib/Assembly/Host/Registrierung übereinstimmen und von Fremdclients aufrufbar sein. |
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
