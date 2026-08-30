# Arbeitsauftrag Q — Matrixschärfung und Dokumentationsabgleich

Verbindliche Kartenfolge. Es gelten zusätzlich [`LUNA_GUARDRAILS.md`](LUNA_GUARDRAILS.md) und
`CLAUDE.md`. Bei Widerspruch gewinnen die Leitplanken.

**Kartenpräfix `Q`** — bewusst getrennt von der `L`-Wellennummerierung in
[`LUNA_EXECUTION_PLAN.md`](LUNA_EXECUTION_PLAN.md). Die Q-Karten sind ein Qualitäts- und
Konsistenzdurchgang, keine neue Welle.

## Warum es diesen Auftrag gibt

Das Abschlussgate lautet „alle Matrixerwartungen `implemented`". Die Matrix nahm bisher nur
**fertige** Arbeit auf und stand damit auf 35/35 — das Gate war erfüllt, während die Roadmap 57
offene Punkte führte. Die laufende Zerlegung repariert die implementation-Achse. Sie hat dabei
dieselbe Lücke auf der zweiten Achse geöffnet: Stand bei Abfassung **49/49 `documented-verified`**,
davon 13 `planned` und 1 `partial`.

Dieser Auftrag macht beide Achsen wieder aussagefähig, sichert das mechanisch ab und zieht die
Dokumentation auf einen einheitlichen Stand.

## Reihenfolge und Abhängigkeit

**Phase 1 — jederzeit ausführbar, strikt in dieser Reihenfolge.** `Q-01` **MUSS** vor `Q-02` fertig
sein, sonst bricht der Build sofort an den eigenen Daten ab.

**Einschub `L1-05R`** — die fehlende Materialisierung der Familien aus `L1-03`/`L1-04`. Läuft nach
Phase 1 und **nach `Q-02`**, damit das Gate die 34 neuen Einträge sofort korrekt erzwingt.

**Phase 2 — erst starten, wenn die L1-Zerlegung abgeschlossen ist** (`L1-05R` fertig,
Erwartungszahl stabil). Sonst werden Zahlen dokumentiert, die sich noch bewegen.

Prüfbefehl für den Phasenübergang:

```bash
node -e "const d=require('./docs/vb6-sp6-compatibility-matrix.json');console.log('Erwartungen:',d.expectations.length)"
```

Zweimal im Abstand von einigen Minuten dieselbe Zahl **und** keine offenen L1-Karten → Phase 2 frei.
Bleibt die Zahl unter dem erwarteten Zielwert stehen: **melden, nicht weitermachen** (§9).

---

# Phase 1

## Q-01 — verification-Achse auf Wahrheit setzen

**Ziel.** Kein Eintrag behauptet einen Nachweis, den es nicht gibt.

**Dateien.** `docs/vb6-sp6-compatibility-matrix.json`

**Änderung.**
- Jede Erwartung mit `implementation: "planned"` bekommt `verification: "not-yet-verified"`.
- Jede Erwartung mit `implementation: "partial"` bekommt `not-yet-verified`, **außer** der bereits
  gebaute Teil ist durch eine benannte, grüne Assertion in `testRefs` abgedeckt — dann bleibt
  `documented-verified`. Die Entscheidung wird pro Eintrag getroffen, nicht pauschal.
- Erwartungen mit `implementation: "implemented"` bleiben unverändert.

**Abnahme.**
- Prüfskript aus §1 der Leitplanken meldet `OK`.
- Die Verteilung zeigt **nicht** 100 % auf einer Achse.
- `.\build.ps1 -NoRestore -Configuration Release` bleibt grün.

**Verbot.** Kein Eintrag wird auf der implementation-Achse verschoben, um die verification-Achse
bequemer zu machen. Diese Karte fasst `implementation` **nicht** an.

---

## Q-02 — Statuswahrheit mechanisch absichern

**Ziel.** Die Verletzung aus `Q-01` kann nicht zurückkehren — auch nicht durch künftige Karten.

**Dateien.** `build.ps1`

**Änderung.** In der bestehenden Validierungsschleife über `$matrix.expectations` (~Zeile 53–81),
direkt **nach** der vorhandenen `verification`-Prüfung:

```powershell
if ($expectation.implementation -eq 'planned' -and $expectation.verification -ne 'not-yet-verified') {
    throw "Compatibility matrix expectation '$($expectation.id)' is planned but claims verification '$($expectation.verification)'."
}

if ($expectation.implementation -ne 'implemented' -and $expectation.verification -eq 'oracle-verified') {
    throw "Compatibility matrix expectation '$($expectation.id)' claims oracle verification without being implemented."
}
```

**Abnahme.**
- Kanonischer Lauf grün.
- **Gegenprobe zwingend:** eine `planned`-Erwartung testweise auf `documented-verified` setzen —
  `build.ps1` **MUSS** mit genau dieser Meldung abbrechen. Danach zurücksetzen und erneut grün laufen.
  Ohne durchgeführte Gegenprobe gilt die Karte als offen.

**Verbot.** Keine neue Validierungsfunktion, kein zweiter Durchlauf über die Erwartungen. Die
Schleife existiert bereits und prüft dort schon IDs, Matrixreferenzen, Statuswerte und `testRefs`.

---

## Q-03 — überklagte Vollständigkeit zurückstufen

**Ziel.** Keine Erwartung behauptet eine Fläche als geschlossen, die die Roadmap als offen führt.

**Dateien.** `docs/vb6-sp6-compatibility-matrix.json`

**Änderung.** `format.complete-surface` und `math.complete-surface` von `implemented` auf `partial`.
`verification` bleibt `documented-verified` — das Getestete *ist* dokumentationsgeprüft; nur der
Vollständigkeitsanspruch fällt weg.

**Begründung, die in der Karte belegt werden muss.** `docs/ROADMAP.md`, Etappe C, erster Punkt führt
„Alle dokumentierten String-, Math-, Financial-, Datum/Zeit-, `Format`-, Array-, … Verträge
implementieren" als `[ ]`. Beide Erwartungen behaupten in ihrer ID genau diese Fläche.

**Abnahme.** Kein Eintrag mehr auf `implemented`, dessen `expected` eine Fläche beschreibt, die in
der Roadmap `[ ]` oder `[~]` ist. Kanonischer Lauf grün.

**Verbot.** `financial.core-annuity` wird **nicht** angefasst — die ID grenzt den Anspruch korrekt
auf den Kern ein. Keine weiteren Rückstufungen „auf Verdacht"; jede braucht eine benannte
Roadmap-Zeile.

---

## Q-04 — Fortschritt im kanonischen Lauf sichtbar machen

**Ziel.** Der Matrixstand steht neben Testzahl und VISIA, statt nur im JSON.

**Dateien.** `build.ps1`

**Änderung.** Nach dem VISIA-Report (nach dem `Tee-Object` auf `artifacts\visia-report.txt`, **vor**
dem `if ($RequireNativeOcx)`-Block) eine Zeile ausgeben, gezählt aus dem bereits geladenen
`$matrix`-Objekt:

```
Matrix: <n> implemented, <n> partial, <n> planned von <gesamt> | <n>/<gesamt> documented-verified
```

**Abnahme.**
- Die Zeile erscheint im Lauf und nennt denselben Nenner wie das Prüfskript aus §1.
- Kein erneutes Einlesen der JSON-Datei — `$matrix` ist oben bereits geladen.

**Verbot.** Kein Schreiben in Dateien, kein neues Artefakt. Nur Ausgabe.

---

## Q-05 — Aufräumen

**Ziel.** Drei belegte Altlasten weg, ohne Verhaltensänderung.

**Dateien.** `.gitignore`, `tests/VB6.Compiler.Tests/ImplicitVariantAnalysisTests.cs`,
`tests/VB6.Compiler.Tests/ForEachSyntaxGuardTests.cs`

**Änderung.**

1. **`build_diag.txt`** (1,7 MB, in `8fb3feb` versehentlich mitcommittet): Eintrag in `.gitignore`
   ergänzen. Die Datei bleibt lokal liegen. **Das Entfernen aus dem Tracking
   (`git rm --cached`) gehört zur Git-Übergabe und wird hier NICHT ausgeführt** (§10).
2. **Zwei tote Assertions** entfernen — sie prüfen die Abwesenheit von Diagnose-Codes, die in `src/`
   nicht mehr existieren, und können darum nicht mehr fehlschlagen:
   - `ImplicitVariantAnalysisTests.cs:49` → Zeile mit `VB6S0021`
   - `ForEachSyntaxGuardTests.cs:26` → Zeile mit `VB6S0052`
3. **Leere Verzeichnisse** `src/VB6.CodeGen.CSharp/` und `tests/VB6.CodeGen.CSharp.Tests/` löschen.
   Reste des entfernten C#-Backends: nur `bin`/`obj`/`TestResults`, in keiner Solution,
   `git ls-files` liefert 0.

**Abnahme.**
- `git ls-files src/VB6.CodeGen.CSharp tests/VB6.CodeGen.CSharp.Tests` → leer, Verzeichnisse weg.
- `grep -rn "VB6S0021\|VB6S0052" tests/` → keine Treffer.
- Testzahl sinkt **nicht** — es werden Assertions entfernt, keine Testmethoden.
- Kanonischer Lauf grün.

**Verbot.** Nur diese beiden Zeilen. Die umgebenden Testmethoden prüfen echte semantische Fakten und
bleiben vollständig erhalten. Keine weiteren „toten" Assertions auf Verdacht entfernen.

---

---

# Einschub L1-05R — Restarbeit aus `L1-05`

**Läuft zwischen Phase 1 und Phase 2. `Q-02` MUSS vorher fertig sein.**

**Ziel.** Die Matrix enthält für **jede** Kartenfamilie aus `L1-02`, `L1-03` und `L1-04` genau eine
Erwartung.

**Ausgangslage.** `L1-05` versprach „aus jedem aufgeführten Vertrag eine eigene Matrix-Erwartung mit
eindeutiger ID". Materialisiert wurden nur die 14 Familien aus `L1-02`. Die **17 Familien aus
`L1-03`** und die **17 aus `L1-04`** fehlen; die Karten stehen trotzdem auf `[x]`. Genau dieser Fall
ist in §2 der Leitplanken als Beispiel für eine zu früh geschlossene Karte benannt.

**Dateien.** `docs/vb6-sp6-compatibility-matrix.json`, `docs/LUNA_EXECUTION_PLAN.md`

**Änderung.**
- Für jede Familie `L1-03-A` … `L1-03-Q` und `L1-04-A` … `L1-04-Q` eine Erwartung anlegen, nach dem
  Muster der bestehenden `l1-02-*`-Einträge: sprechende ID, `matrixEntry` aus der Kartentabelle,
  `input`, strukturiertes `expected`, `testRefs` auf die in der Karte genannten Einstiegsdateien.
- **Alle neuen Einträge:** `implementation: "planned"`, `verification: "not-yet-verified"`.
  Das durch `Q-02` scharf geschaltete Gate erzwingt das bereits — es wird hier nicht umgangen.
- Jeder `testRefs`-Pfad **MUSS existieren**; `build.ps1` prüft das. Von den in den Kartentabellen
  genannten Testklassen existieren alle bis auf `ComActivationProbe` als Datei — dort ist der
  Projektpfad `tests/VB6.ComActivationProbe` zu verwenden.

**Abnahme.**
- Erwartungszahl **83** (35 implementiert + 1 partial + 47 geplant, Aufteilung je nach Fortschritt
  von `L1-02-A`).
- Jede Kartenfamilie aus den drei Tabellen hat genau eine Erwartung; keine Familie doppelt, keine
  ohne.
- Prüfskript aus §1 der Leitplanken meldet `OK`.
- Kanonischer Lauf grün.
- Der Kartenstatus von `L1-03`/`L1-04`/`L1-05` im Ausführungsplan spiegelt den erreichten Zustand
  wahrheitsgemäß.

**Verbot.** Keine Erwartung wird höher als `planned` angelegt, nur weil das zugehörige Verhalten
teilweise schon existiert. Wer beim Anlegen bemerkt, dass eine Fläche bereits gebaut ist, legt sie
`planned` an und meldet das — die Höherstufung ist Arbeit einer eigenen Karte mit Testnachweis (§1).

---

# Phase 2 — erst nach abgeschlossener L1-Zerlegung

Vor Beginn: kanonischen Lauf ausführen und die **gemessenen** Zahlen notieren. Alle folgenden Karten
verwenden **diese** Zahlen. Keine Zahl aus einem Dokument abschreiben (§3).

## Q-06 — `docs/ROADMAP.md`

**Änderung.**

1. **„Gemessener Ist-Stand"** (~Zeile 33–52): Testzahl und Datum auf den gemessenen Wert; VISIA-Zeile
   bestätigen. **Den Matrixstand als dritte Messung aufnehmen** — bislang stehen dort nur
   Korpusparität und Regressionssuite, obwohl die Matrix laut Abschlussgate gleichrangig ist.
2. **~Zeile 156 — sachlich falsch:** „Die Matrix unterscheidet `implemented`, `documented-verified`
   und ein später optionales `oracle-verified`" wirft beide Achsen in einen Satz. Umschreiben auf
   zwei getrennte Achsen mit ihren jeweiligen Werten (§1).
3. **Etappe A** (~Zeile 237): Der `[~]`-Punkt nennt „die noch ausstehende Feingranularität einzelner
   Intrinsics" als offen. Nach der Zerlegung existiert sie — Status und Text fortschreiben.
4. **Etappe C, erster Punkt** (~Zeile 268): bleibt `[ ]`. Den Bezug zur Rückstufung aus `Q-03`
   explizit machen, damit beide Aussagen nicht wieder auseinanderlaufen.

**Abnahme.** Keine Verlaufsprosa ergänzt (§4). Zahlen stimmen mit README und Changelog überein.

---

## Q-07 — `README.md`

Der öffentliche Einstieg ist am stärksten veraltet. Englisch, wie die Datei.

**Änderung.**

1. **„Current status"** (Zeile 9): beschreibt einen Stand von vor mehreren Meilensteinen — „M0
   through M3 are complete, the Variant core is implemented with the remaining promotion matrix still
   open, and the first M5 class/object-model slice is now in place". Auf den tatsächlichen Stand
   umschreiben und den verbindlichen Managed-Abschlussplan (Etappen A–H) nennen. Die Aufzählung von
   „an LSP with diagnostics/navigation" als laufender Fortschritt entfernen — LSP/IDE liegen
   ausdrücklich auf Eis.
2. **Zeile 115/116 — Selbstwiderspruch:** Der Hinweis „historical capability inventory was last
   counted at 1195 tests; the authoritative current count is … " steht direkt über einem
   Aufzählungspunkt, der weiterhin „**1195 test cases**" behauptet. Die veraltete Zahl entfernen
   statt den Widerspruch mit einem Disclaimer zu überdecken.
3. **„Current verification"** (~Zeile 126–141): drei überlappende Absätze zu 2026-08-28 und zweimal
   2026-08-29, jeder eine Fließtextliste sämtlicher Features. Das ist Changelog-Prosa im README. Auf
   den aktuellen Messwert plus Verweis auf `docs/CHANGELOG.md` eindampfen.
4. **„Next milestones"** (~Zeile 329–338): nennt die alte Reihenfolge („COM/ActiveX consumption …
   finish the Variant promotion matrix"). Auf die Etappen A–H und die Luna-Queue umstellen, damit
   README, Roadmap und Ausführungsplan dieselbe Reihenfolge nennen.

**Verbot.** Die Zeile „With `-RequireNativeOcx` … **48/48 passed**" bleibt **unverändert**. Sie
verlangt einen x86-Testhost mit installierten OCX; ohne eigenen Lauf nach §3 wird sie weder bestätigt
noch umgeschrieben.

---

## Q-08 — `docs/LUNA_EXECUTION_PLAN.md`

**Änderung.**

1. **„Aktueller Einstieg"**: Erwartungszahl, Aufteilung implementiert/partial/geplant und die nächste
   offene Karte auf den Stand nach Abschluss der Zerlegung bringen.
2. **„Arbeitskartenvertrag"**: Die Zeile `Matrix | Genau eine Erwartungs-ID` um die Regel ergänzen,
   dass ein Kartenabschluss **beide Achsen** fortschreibt — `planned` → `implemented` **und**
   `not-yet-verified` → `documented-verified`. Ohne diese Regel entsteht die in `Q-01` reparierte
   Inkonsistenz beim nächsten Kartenabschluss sofort wieder.
3. **„Abschlusskriterien"**: um die verification-Achse ergänzen. Bisher steht dort nur „alle
   Matrixerwartungen `implemented`"; `documented-verified` gehört als zweite Bedingung dazu.
4. **„Testtakt"**: den Matrix-Readout aus `Q-04` als Teil des Wellen-Gates aufnehmen.
5. **Verweis auf [`LUNA_GUARDRAILS.md`](LUNA_GUARDRAILS.md)** im Kopf ergänzen.

---

## Q-09 — `docs/CHANGELOG.md` und `CLAUDE.md`

**`docs/CHANGELOG.md`** — ein Eintrag **ans Ende**: Matrixzerlegung in geplante Erwartungen,
Reparatur der verification-Achse samt neuer Build-Regel, Rückstufung der beiden
Vollständigkeitsansprüche, Fortschritts-Readout, Aufräumarbeiten, plus der gemessene kanonische
Nachweis mit realer Testzahl aus den TRX-Dateien.

**`CLAUDE.md`** — vier veraltete Angaben:

1. Roadmap „~430 Zeilen" → realer Wert; Changelog „~2400" → realer Wert. Beide mit `wc -l` messen.
2. Abschnitt **„Fokus"**: nennt als Arbeitsfront noch M8/M9 COM/ActiveX und Forms. Real steuert der
   verbindliche Managed-Abschlussplan (Etappen A–H) über die Luna-Queue. Dort fortschreiben.
   **Hinweis:** Der Abschnitt „Roadmap und Historie" verweist bereits auf `LUNA_GUARDRAILS.md`,
   `LUNA_EXECUTION_PLAN.md` und diese Datei und erklärt die beiden Statusachsen — **nicht doppelt
   ergänzen**, nur den „Fokus"-Abschnitt anfassen.
3. **„Fallen"**: behauptet fünf ungetestete Diagnose-Codes (`VB6L0002/3/4`, `VB6E0002`, `VB6S0068`).
   Real sind **alle** in `src/` definierten Codes in Tests referenziert, die fünf genannten mit
   Positivassertions. Vor dem Umschreiben nachmessen:
   ```bash
   comm -23 <(grep -rho 'VB6[A-Z]\{1,2\}[0-9]\{4\}' src/ --include='*.cs' | sort -u) \
            <(grep -rho 'VB6[A-Z]\{1,2\}[0-9]\{4\}' tests/ --include='*.cs' | sort -u)
   ```
4. Der Hinweis auf die CLI-Duplikation nennt `args.Length is >= 3 and <= 6`; real steht dort ein
   höherer Wert. Zahl nachmessen und korrigieren — **die Warnung selbst bleibt gültig und bleibt
   stehen**.

**Verbot.** Keine inhaltliche Überarbeitung der Changelog-Historie. Nur anhängen.

---

## Q-10 — Abschlussprüfung und Übergabe

**Änderung.** Keine. Diese Karte prüft nur.

**Abnahme — alles einzeln ausführen und das Ergebnis berichten:**

```powershell
.\build.ps1 -Configuration Release
```

| Prüfung | Sollwert |
|---|---|
| Release-Build | 0 Warnungen, 0 Fehler |
| Tests | alle grün, Zahl aus `artifacts/test-results/*.trx` |
| VISIA | 40/40, 0 Fehler |
| Matrix-Gate | grün, inklusive `Q-02`-Regeln |
| Matrix-Readout | Nenner entspricht der tatsächlichen Erwartungszahl |
| Statusprüfskript §1 | `OK`, keine Achse auf 100 % |
| `git diff --check` | keine echten Whitespace-Fehler |

Dokumentationsabgleich — dieselbe Zahl überall:

```bash
grep -rn "40/40\|40 von 40" README.md docs/ROADMAP.md docs/LUNA_EXECUTION_PLAN.md
grep -rno "[0-9]\{4\} \(Tests\|test cases\)" README.md docs/ROADMAP.md docs/LUNA_EXECUTION_PLAN.md
```

Abweichungen sind ein Fehler und blockieren die Übergabe.

**Übergabe.** Luna committet **nicht** (§10). Der Abschlussbericht nennt geschlossene Karten,
geänderte Dateien, die gemessenen Zahlen, offene Punkte und was bewusst nicht getan wurde. Die
Git-Arbeit — Branch, zwei Commits, Merge `--no-ff`, `git rm --cached build_diag.txt` — erfolgt
danach getrennt.

---

## Was dieser Auftrag bewusst nicht enthält

- **Die CLI-Duplikation** (`--compatibility` an mehreren Stellen, wachsende Arity-Guards). Reale
  Schuld, eigener Umbau. In `CLAUDE.md` wird nur die falsche Zahl korrigiert.
- **Retroaktive Aufteilung des vorhandenen Arbeitsbaums pro Schicht.** Die Pipeline-Dateien enthalten
  mehrere Themen ineinander verschränkt; eine Trennung ergäbe nicht baubare Zwischenstände. Die
  Schichtdisziplin greift ab dem nächsten Feature, nicht rückwirkend.
- **Inhaltliche Änderungen an der Changelog-Historie.**
- **Aussagen über den nativen OCX-Pfad** ohne einen Lauf nach §3.
