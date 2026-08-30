# Luna-Leitplanken

Verbindliche Arbeitsregeln für jeden Luna-Lauf in diesem Repository.

Diese Datei steht **über** dem operativen Ablauf in [`LUNA_EXECUTION_PLAN.md`](LUNA_EXECUTION_PLAN.md).
Wo beide etwas anderes sagen, gilt diese Datei. Wo `CLAUDE.md` eine Projektregel setzt, gilt `CLAUDE.md`.

Jede Regel hat die Form **MUSS** / **DARF NICHT** plus eine **Prüfung**, die vor dem Abschluss einer
Karte tatsächlich ausgeführt wird. Eine Regel ohne durchgeführte Prüfung gilt als verletzt.

Wenn eine Regel im Weg steht: **anhalten und melden**, nicht umgehen. Siehe §9.

---

## §1 Statuswahrheit — die wichtigste Regel

Die Matrix hat **zwei unabhängige Achsen**. Sie werden nie vermischt und nie „aus Bequemlichkeit"
gefüllt.

| Achse | Werte | Beantwortet |
|---|---|---|
| `implementation` | `planned` → `partial` → `implemented` | Ist das Verhalten gebaut? |
| `verification` | `not-yet-verified` → `documented-verified` → `oracle-verified` | Ist es nachgewiesen? |

### Bindende Zuordnung

1. `implementation: "planned"` **MUSS** `verification: "not-yet-verified"` tragen. Ausnahmslos.
2. `implementation: "partial"` **MUSS** `not-yet-verified` oder `documented-verified` tragen —
   `documented-verified` nur, wenn der bereits gebaute Teil durch einen in `testRefs` benannten,
   grünen Test abgedeckt ist.
3. `implementation: "implemented"` **DARF NUR** gesetzt werden, wenn **jedes einzelne Feld** unter
   `expected` durch mindestens einen Test abgedeckt ist, der es tatsächlich prüft.
4. `documented-verified` verlangt **beides**: eine Quelle nach der Rangfolge aus der Roadmap *und*
   einen grünen Test. Fehlt der Test, ist der Wert `not-yet-verified`.
5. `oracle-verified` **DARF NIE** ohne echten Lauf gegen einen VB6-SP6-Originalcompiler gesetzt
   werden. Ohne installiertes Orakel bleibt `documented-verified` der Endzustand.

### Verbote

- **DARF NICHT** ein Status gesetzt werden, weil ein Build-Gate irgendeinen gültigen Wert verlangt.
  Im Zweifel gilt **immer der niedrigere Wert**.
- **DARF NICHT** ein Status „im Voraus" oder gesammelt über viele Einträge gesetzt werden. Ein Status
  wandert nur in derselben Änderung nach oben, die das Verhalten nach oben bewegt.
- **DARF NICHT** die Existenz einer Testdatei als Abdeckung gewertet werden. `testRefs` verweist auf
  Dateien; entscheidend ist, ob darin eine Assertion diesen Vertrag prüft.
- **DARF NICHT** eine Erwartung eine Vollständigkeit behaupten, die die Roadmap als `[ ]` oder `[~]`
  führt. IDs wie `*.complete-surface` verlangen entweder echte Vollständigkeit oder eine engere ID.

### Warum diese Regel zuerst steht

Der Abschluss dieses Projekts ist als „alle Matrixerwartungen `implemented`" definiert. Eine Matrix,
die nur fertige Arbeit aufnimmt, steht damit dauerhaft auf 100 % und kann den Abschluss nie messen.
Genau das ist zweimal passiert:

- Die Matrix enthielt ausschließlich abgeschlossene Arbeit und stand auf **35/35 `implemented`**.
- Nach der Zerlegung trugen die neuen `planned`-Einträge trotzdem `documented-verified` — Stand bei
  Abfassung dieser Datei: **49/49 `documented-verified`** bei 13 `planned` und 1 `partial`.

Beide Male war jeder Einzelwert plausibel und das Gesamtbild wertlos.

### Prüfung

```bash
node -e "
const d=require('./docs/vb6-sp6-compatibility-matrix.json');
const bad=d.expectations.filter(e=>
  (e.implementation==='planned' && e.verification!=='not-yet-verified') ||
  (e.implementation!=='implemented' && e.verification==='oracle-verified'));
console.log(bad.length? 'VERLETZT: '+bad.map(e=>e.id).join(', ') : 'OK');
const i={},v={};
for(const x of d.expectations){i[x.implementation]=(i[x.implementation]||0)+1;v[x.verification]=(v[x.verification]||0)+1;}
console.log('implementation:',i); console.log('verification:',v);
"
```

Steht eine Achse auf 100 %, während die Roadmap offene Punkte führt: **anhalten und melden.**

---

## §2 Definition of Done — wann eine Karte geschlossen ist

Eine Karte wird **erst** auf `verified` gesetzt, wenn **alle** Punkte zutreffen:

1. Die Verhaltensänderung ist in der benannten Pipeline-Schicht umgesetzt.
2. Tests existieren, prüfen den Vertrag und sind in `testRefs` der zugehörigen Erwartung eingetragen.
3. Der gezielte Testlauf der Karte ist grün.
4. Der kanonische Lauf ist grün — nach Takt aus §3.
5. Der Matrixstatus ist auf **beiden** Achsen fortgeschrieben (§1).
6. Der Roadmap-Status ist fortgeschrieben.
7. Der Changelog-Eintrag ist **ans Ende** angehängt.
8. `git diff --check` meldet keine echten Whitespace-Fehler.
9. Keine Datei außerhalb des Kartenumfangs wurde verändert (§6).

### Teilerfüllung schließt keine Karte

Nennt eine Karte **mehrere** Ergebnisse, ist sie erst mit dem **letzten** erledigt. Wer die Hälfte
liefert, dokumentiert die gelieferte Hälfte und lässt die Karte **offen**.

**DARF NICHT** eine Karte als erledigt markiert werden, deren eigener Text ein Ergebnis nennt, das
nicht existiert.

Beleg aus diesem Repository: `L1-05` verlangte zwei Dinge — das Statusfeld pro Erwartung *und*
„aus jedem aufgeführten Vertrag eine eigene Matrix-Erwartung mit eindeutiger ID". Geliefert wurde
das Schema; die 48 Karten wurden nicht zu Erwartungen. Die Karte stand trotzdem auf `[x]`.

### Prüfung

Vor dem Abhaken den eigenen Kartentext Satz für Satz gegen den Ist-Zustand lesen und **jedes**
genannte Ergebnis einzeln bestätigen.

---

## §3 Messwerte

### Woher Zahlen kommen

- Die Testzahl kommt **ausschließlich** aus den TRX-Dateien des Laufs:
  `artifacts/test-results/*.trx`. **DARF NICHT** aus README, Roadmap oder Changelog übernommen werden
  — die sind das Ziel der Zahl, nicht ihre Quelle.
- Eine Zahl **DARF NICHT** in ein Dokument geschrieben werden, die nicht im selben Arbeitsschritt
  gemessen wurde. Kein Fortschreiben „plus die neuen Tests".

### Kanonischer Lauf

```powershell
.\build.ps1 -NoRestore -Configuration Release
```

Takt: nach **vier** verifizierten Karten oder am Ende einer Kartenfamilie, je nachdem was zuerst
eintritt. Zusätzlich immer vor der Übergabe (§10).

Grün heißt **alles** davon:

| Messpunkt | Sollwert |
|---|---|
| Release-Build | 0 Warnungen, 0 Fehler (`TreatWarningsAsErrors` ist an) |
| Tests | alle grün, Zahl aus TRX |
| VISIA | **40/40**, 0 Fehler |
| Matrix-Gate | grün, inklusive der Statusregeln aus §1 |

Die VISIA-Zahl **DARF NICHT** steigen. Sie ist eine Regressionsschwelle, kein Fortschrittsmaß.

### Wenn der Lauf rot ist

Erst die Meldung lesen, dann Code anfassen. Zwei Ursachen sind **keine** Codefehler und in
`CLAUDE.md` beschrieben:

- `FileLoadException` / „Zugriff verweigert" / `E_INVALIDARG` → Zustandsproblem der inkrementellen
  Kopie. `bin` und `obj` **des betroffenen Testprojekts** löschen, neu bauen.
- `Eine Anwendungssteuerungsrichtlinie hat diese Datei blockiert. (0x800711C7)` oder Exitcode
  `-532462766` in der Kindprozessausgabe → **Smart App Control**. Löschen hilft nicht. Prüfen mit
  `Get-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy | Select VerifiedAndReputablePolicyState`.
  Bei `1` sind lokale Läufe nicht aussagekräftig — melden, nicht am Emitter suchen.

### Native OCX

Der native OCX-Pfad ist x86-gebunden und überspringt sich selbst. Ein grüner Normallauf sagt über ihn
**nichts**. Er zählt nur so:

```powershell
$env:VB6_REQUIRE_NATIVE_OCX = '1'
dotnet test tests/VB6.Runtime.WinForms.Tests -c Release -- RunConfiguration.TargetPlatform=x86
```

**DARF NICHT** eine Aussage über nativen OCX-Erfolg getroffen werden ohne einen so ausgeführten Lauf.

---

## §4 Dokumentenrollen — eine Aussage, ein Ort

| Datei | Enthält | Enthält niemals |
|---|---|---|
| `docs/ROADMAP.md` | Ist-Stand und Offenes, Meilensteine, Etappen | Verlaufsprosa, „Nachtrag"-Abschnitte |
| `docs/CHANGELOG.md` | chronologisches Journal, **älteste zuerst** | Statusaussagen über Offenes |
| `docs/LUNA_EXECUTION_PLAN.md` | operative Warteschlange, Karten | fachliche Zielbegründung |
| `docs/vb6-sp6-compatibility-matrix.json` | maschinenlesbarer Vertragsstand | Prosa |
| `README.md` | öffentlicher Einstieg, aktueller Stand (englisch) | Feature-für-Feature-Historie |
| `CLAUDE.md` | dauerhafte Projektregeln und Fallen | Tagesstand, Messwerte |

Regeln:

- Neue Einträge im Changelog **MÜSSEN ans Ende**. Bestehende Historie wird nicht umgeschrieben.
- **DARF NICHT** Verlaufsprosa in die Roadmap zurückwandern. Genau daran ist sie einmal auf 2800
  Zeilen gewachsen, mit 130 Abschnitten namens „Aktueller …-Nachtrag".
- Steht dieselbe Zahl in mehreren Dokumenten, **MUSS** sie nach jeder Änderung überall identisch sein.

### Prüfung

```bash
grep -rn "40/40\|40 von 40" README.md docs/ROADMAP.md docs/LUNA_EXECUTION_PLAN.md
grep -rno "[0-9]\{4\} \(Tests\|test cases\)" README.md docs/ROADMAP.md docs/LUNA_EXECUTION_PLAN.md
```

Abweichende Zahlen sind ein Fehler, kein Rundungsproblem.

---

## §5 Parallelbetrieb und Dateibesitz

In diesem Repository arbeitet zeitweise **mehr als ein Agent gleichzeitig**. Das ist real
eingetreten und hat `build.ps1`, die Matrix und den Ausführungsplan mitten in einem fremden
Analyselauf verändert.

- Vor **jeder** Änderung an einer Datei unter `docs/` wird die Datei **neu eingelesen**. Ein
  Kenntnisstand von vor fünf Minuten ist kein Kenntnisstand.
- **DARF NICHT** eine Datei aus einem älteren gelesenen Zustand heraus komplett überschrieben werden.
  Geändert wird punktuell.
- Hat sich eine Datei seit dem Lesen verändert: neu lesen, Änderung neu anwenden, **nicht** die
  fremde Änderung wegschreiben.
- Zu Beginn einer Karte werden die berührten Dateien benannt. Wer außerhalb dieser Liste schreibt,
  verletzt §6.

---

## §6 Umfangsdisziplin

- Es wird **nur** in den Dateien gearbeitet, die die aktive Karte nennt.
- Ein neu entdeckter Querbereich wird als **neue Karte** angelegt — nicht nebenbei miterledigt.
  „Wenn ich schon mal hier bin" ist der Anfang jedes unüberprüfbaren Diffs.
- Eine Karte bleibt höchstens **vier eng verwandte Tests** groß. Sonst wird sie **vor** dem Coding
  geteilt.
- Öffentliche APIs werden **additiv** erweitert. Kein Umbenennen, kein Entfernen ohne ausdrückliche
  Entscheidung.
- Eine Karte ändert **eine** Verhaltensweise in **einer** Pipeline-Schicht.

---

## §7 Projektinvarianten — nicht verhandelbar

Aus `CLAUDE.md`. Diese Regeln schlagen jede Bequemlichkeit:

**Moderne Erweiterungen dürfen VB6-Semantik niemals verändern — sie kommen additiv dazu.**

- `Integer` ist signed 16 Bit. `Long` ist signed 32 Bit.
- Arithmetik ist `checked`. VB6-Overflow ist beobachtbares Verhalten, kein Bug.
- Reine `Integer`-Ausdrücke werden **nicht** promoted, nur weil das Zuweisungsziel breiter ist.
- `Currency` ist skalierter Int64 mit vier Nachkommastellen und Banker's Rounding.
- Bezeichner sind case-insensitiv, Trivia bleibt im Lexer erhalten.
- `VB6.Runtime` konvertiert mit `CultureInfo.InvariantCulture`. Es gibt genau **zwei** dokumentierte
  Ausnahmen (`VBComDispatch`-LCID, `vbUseSystem`-Auflösung). **Keine dritte ohne Entscheidung.**
- Wo VB6-Verhalten nicht implementiert ist: **Diagnostic mit Code melden**, nicht stillschweigend
  etwas Ähnliches tun.
- Schichtgrenzen sind hart: Der Binder kennt kein IR, der Lowerer keine Syntaxknoten, der Emitter
  keinen Bound Tree.
- VB6-Semantik zur Laufzeit gehört in `VB6.Runtime` — nicht in emittierten Code.
- Der Emitter hat genau einen Fehlerkanal: `NotSupportedException` → `VB6E0001` (Sprachlücke), jede
  andere Ausnahme → `VB6E0003` (Emitter-Defekt). Diese Trennung wird gehalten.

---

## §8 Teststrategie

- **Jedes Sprachfeature braucht einen End-to-End-Test**, der emittiert, ausführt und die Ausgabe
  prüft. Der Ablauf liegt in `tests/VB6.Compiler.Tests/VB6TestProgram.cs`
  (`Run`, `RunLines`, `RunProject`). **DARF NICHT** pro Testdatei nachgebaut werden.
- Übersetzungsentscheidungen werden gegen **das IR** assertiert: `VB6TestIr.Lower(quelltext)`, dann
  `RuntimeCalls`/`ArrayCalls`/`Procedures`/`Expressions`.
- **DARF NICHT** gegen generierten Text verglichen werden. Textvergleiche waren an das entfernte
  C#-Backend gebunden und konnten zufällig zutreffen.
- Ein **neuer Diagnose-Code braucht einen Test**, der den **Code** prüft, nicht den Meldungstext.
- Headless **MUSS** ohne UI-Host durchlaufen — die Suite hat keinen.
- Beim Ergänzen von Tests die **untere Ebene** bedienen: `VB6TestIr` für Übersetzungsentscheidungen,
  E2E **zusätzlich**, nicht ersatzweise.

---

## §9 Anhalten und melden

**Sofort anhalten** und nicht spekulativ weiterarbeiten, wenn:

- der kanonische Lauf rot ist und die Ursache nicht in der eigenen Änderung liegt;
- eine Karte eine Architekturentscheidung verlangt, die nicht schon getroffen ist;
- zwei Dokumente sich widersprechen und nicht offensichtlich ist, welches recht hat;
- ein Status gesetzt werden müsste, für den der Nachweis fehlt (§1);
- ein **bestehender, benannter Test** der geplanten Änderung widerspricht (§12);
- eine Regel dieser Datei im Weg steht.

Die Meldung enthält: **Ursache**, **reproduzierbaren Befehl**, **benötigte Entscheidung**.

Ebenfalls verboten, ohne ausdrückliche Ansage:

- `git reset`, `git checkout --`, Force-Push, Verwerfen fremder Änderungen
- pauschale Prozessabbrüche
- automatische Commits (§10)
- Abschalten oder Umgehen eines Build-Gates, um einen Lauf grün zu bekommen

---

## §10 Übergabe

Luna **committet nicht**. Die Git-Arbeit erfolgt getrennt und auf ausdrückliche Ansage.

Der Arbeitsbaum **MUSS** aber jederzeit in einem übergabefähigen Zustand bleiben:

- kanonischer Lauf grün (§3)
- `git diff --check` ohne echte Whitespace-Fehler (CRLF-Warnungen sind normal)
- Dokumentation konsistent (§4)
- Matrixstatus wahr (§1)

Der Abschlussbericht eines Laufs nennt:

1. geschlossene Karten mit ID,
2. geänderte Dateien,
3. die **gemessenen** Zahlen (Tests aus TRX, VISIA, Matrixstand),
4. offene Punkte und Blocker,
5. was bewusst **nicht** getan wurde,
6. die **Vorabmessung** nach §11: wie viele Fälle gemessen wurden und wie viele davon schon
   korrekt waren,
7. zurückgenommene Änderungen nach §12 samt der offen gebliebenen Frage.

Zahlen ohne durchgeführte Messung gehören nicht in den Bericht.

---

## §11 Erst messen, dann bauen

Eine Karte beginnt **nicht** mit einer Änderung, sondern mit einer Messung des Ist-Verhaltens
über die **volle Breite ihres Vertrags**.

- **MUSS**: Vor der ersten Zeile Produktionscode wird jedes Feld unter `expected` gegen das
  laufende System gemessen — mit einem Wegwerfprogramm über `VB6TestProgram.RunLines`, das die
  beobachtbaren Werte ausgibt (`VarType`, `Err.Number`, Ergebniswert), nicht mit Codelektüre.
- **MUSS**: Die Messung deckt die Fläche ab, nicht ein Beispiel. Eine Promotionsregel wird über
  Operandenpaare gemessen, eine Fehlerzuordnung über alle betroffenen Funktionen.
- **DARF NICHT**: aus dem Quelltext hergeleitet werden, was das System tut. Der Emitter, der
  Binder und die Runtime haben zusammen zu viele Pfade, als dass Lesen das ersetzt.
- **MUSS**: Das Messergebnis geht in den Kartenbericht — auch und gerade die Fälle, die schon
  korrekt waren.

### Warum diese Regel existiert

Bei `l1-02-f` und `l1-02-g` lautete der Befund zweimal hintereinander: **Das Verhalten war
bereits richtig, nur ungetestet.** Bei `l1-02-g` waren alle 49 gemessenen Operandenpaare der
Promotionstabelle korrekt. Wer dort ohne Messung „implementiert" hätte, hätte funktionierenden
Code umgebaut und die eigentliche Lücke — die fehlende Absicherung — nicht einmal bemerkt.

Umgekehrt fand dieselbe Messung die echten Lücken, die beim Lesen unsichtbar waren: `Err.Number`
**5** statt **94** bei Null-Konvertierungen und **5** statt **13** bei `CDate("kein Datum")`.

**Korrektes, aber ungetestetes Verhalten ist ein Kartenergebnis.** Es wird durch Tests
festgeschrieben und die Erwartung wandert auf der verification-Achse nach oben — die
implementation-Achse bleibt, wo sie war, wenn nichts gebaut wurde.

### Prüfung

Der Kartenbericht nennt die Zahl der gemessenen Fälle und, getrennt, wie viele davon schon
korrekt waren. Fehlt diese Angabe, gilt die Karte als ohne Messung bearbeitet.

---

## §12 Bestandsschutz benannter Verträge

Ein bestehender Test, dessen **Name eine Vertragszusage ausspricht**, ist eine getroffene
Entscheidung — keine Altlast.

- **MUSS**: Reißt eine Änderung einen solchen Test, wird **die Änderung** überprüft, nicht der
  Test angepasst.
- **DARF NICHT**: Ein benannter Test wird umgeschrieben oder gelöscht, weil eine Herleitung aus
  der VB6-Dokumentation etwas anderes nahelegt. Ohne Orakel (§1, Regel 5) schlägt der
  bestehende Vertrag die Herleitung.
- **MUSS**: Die Änderung wird **vollständig** zurückgenommen — `git diff src/` gegen den
  Vorstand ist danach leer — und die offene Frage wird mit den gemessenen Werten notiert.
- **Ausnahme**: Der Test widerspricht einer Projektinvariante aus §7, oder es liegt ein
  tatsächlicher Orakellauf vor. Beides wird im Bericht ausgewiesen.

### Warum diese Regel existiert

Zweimal wurde eine dokumentationsgestützte Änderung begonnen und musste zurück:

- `CDec(Null)` sollte nach VB6-Doku 94 melden. `CDec_ProducesVariantDecimalAndPreservesNull`
  sagte Null — und hatte recht: `CDec` liefert einen Variant mit Decimal-Subtyp und **kann**
  Null tragen, anders als `CInt`, dessen Zieltyp das nicht kann.
- `CInt(CVErr(5))` sollte 13 melden. `ErrorVariantConversions_DistinguishExplicitAndImplicitPaths`
  führt die Unterscheidung im Namen und hängt über `CInt(Missing) = 448` an der
  Missing-Argument-Mechanik. Eine Angleichung hätte zwei Verträge auf einmal verschoben.

In beiden Fällen war die Herleitung plausibel und der bestehende Test der bessere Zeuge.

### Prüfung

```bash
git diff --stat src/
```

Nach einer zurückgenommenen Änderung leer. Die offene Frage steht mit gemessenen Werten in
`LUNA_EXECUTION_PLAN.md` und im Changelog.

