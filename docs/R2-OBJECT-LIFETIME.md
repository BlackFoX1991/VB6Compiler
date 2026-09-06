# R2 — Deterministische Objektlebensdauer: Entwurfsvertrag

Stand: 2026-09-06. Dieses Dokument zerlegt die noch offene Karte `managed-r2-lifetime`.
Es beschreibt den Besitzvertrag, der heute gilt, und benennt, was an ihm noch nicht gemessen ist.

## Ausgangspunkt

VB6 terminiert ein Objekt, wenn seine letzte Referenz verschwindet — nicht irgendwann danach.
`Class_Terminate` ist damit beobachtbares Verhalten und kein Aufräumdetail: Programme schließen
darin Dateien, geben Sperren frei und schreiben Ausgaben, deren Reihenfolge zum Ergebnis gehört.
Ein Finalizer erfüllt das nicht, weil er den Zeitpunkt nicht zusagt.

`VBObjectLifetime` führt deshalb eine eigene Referenzzählung für erzeugte VB6-Objekte, und der
Emitter meldet ihr jede Besitzänderung. Der GC bleibt zuständig für den Speicher, nicht für den
Zeitpunkt. Die Registrierung von Terminatoren für Finalizer und Prozessabbau
(`RunPendingTerminators`) ist der Rückfall für nicht erreichte Fälle, ausdrücklich **kein**
Nachweis richtiger Zeitpunkte.

Die Nachweise waren bis Schnitt 20 sämtlich **indirekt**: eine `InvalidComObjectException` auf
einem freigegebenen RCW, die Erreichbarkeit eines Members, das Prozessende eines ActiveX-EXE-
Servers. Jeder davon beantwortet „hat jemand losgelassen", keiner „wie oft". Seit Schnitt 21
liest `ComReferenceCountTests` den nativen Zähler einer testeigenen Identität direkt
(`CountingComIdentity`); erst damit sind Doppelfreigabe und Leck unterscheidbar.

## Zielvertrag

Ein erzeugtes VB6-Objekt wird terminiert, wenn seine letzte VB6-Referenz verschwindet — in beiden
Profilen, ohne neue Syntax.

- Jede Wert- und Referenzgrenze meldet ihren Besitzübergang: Locals, Modul- und Klassenfelder,
  Parameter, Rückgaben, Array-, Collection- und Variant-Speicher, Events.
- Eine neue Referenz wird **vor** der Freigabe der ersetzten gesichert. Selbstzuweisung und
  Aliasbildung dürfen kein lebendes Objekt terminieren.
- Terminierung läuft genau einmal. Ein Fehler im Terminator beendet nicht das Programm, und ein
  reentranter Aufruf terminiert nicht ein zweites Mal.
- Für COM gilt zusätzlich: Der native Referenzzähler steht nach dem letzten VB6-Besitzer **exakt**
  auf seinem Wert vor der Übernahme. Nicht darüber — das wäre ein Leck. Nicht darunter — das wäre
  eine Freigabe fremden Besitzes, und die fällt an einer ganz anderen Stelle auf als dort, wo sie
  verursacht wurde.

## Besitzfamilien

Welcher Helfer an einer Grenze steht, entscheidet der Emitter anhand der IR-Form des gespeicherten
Werts (`ManagedEmitter.LifetimeReplacementMethod`), nicht anhand des statischen Typs. Die
Reihenfolge ist fest: Variant-Kopie, direkte Aktivierung, fremdes COM-Memberergebnis, eigener
Besitz, geliehen.

| Familie | Erkannt an | Erwerb | Freigabe |
| --- | --- | --- | --- |
| Direkte COM-Aktivierung | `New` eines importierten Coclass, `CreateObject`, `GetObject` | `TransferComActivation` übernimmt den rohen RCW-Anteil in den ersten VB6-Speicherplatz | Beim letzten VB6-Besitzer: IUnknown-Hold **und** RCW-Anteil |
| Geliehene Referenz | Jede gewöhnliche Objektübergabe | `Replace`/`Retain` nimmt nur einen kontrollierten IUnknown-Hold | Beim letzten VB6-Besitzer nur der Hold; der fremde RCW bleibt gültig |
| Fremdes COM-Memberergebnis | Late-bound Member oder externe Prozedur auf einem importierten COM-Vertrag | `ReplaceComMemberResult`: ein Interface-Ergebnis bringt eine frische COM-Referenz mit | Wie direkte Aktivierung |
| Late-bound CLR-Ergebnis | Dieselbe Aufrufoberfläche, aber `Marshal.IsComObject` ist falsch | `RetainOrAdoptComResult` fällt auf ein gewöhnliches Retain zurück | Wie geliehen |
| Erzeugte Prozedurrückgabe | Aufruf einer generierten Prozedur mit Klassen-/Array-/Variant-Ergebnis | `Transfer`: der vorhandene Speicherbesitz wandert weiter | Kein zweiter RCW-Anteil |
| Behälterspeicher | `VBArray<T>`-Element, `VBCollection`-Eintrag | `IVBObjectLifetimeContainer` reicht Retain/Release an die Elemente durch | Beim letzten Behälter |
| Variant-Kopie | Kopiergrenze eines Variant | Array-Ergebnis wird eigener Besitzer, Skalar/Objekt bleibt geliehen | Entsprechend der gewählten Seite |
| `WithEvents`-Subscription | Advise auf Quelle und Senke | Hält **beide** Seiten | Erst beim Unsubscribe, damit keine COM-Quelle vor dem Unadvise verschwindet |

Die Unterscheidung zwischen den letzten beiden Zeilen der ersten Spalte ist nicht kosmetisch: Ein
geliehener Wrapper, den ein fremder Host hält, darf durch VB6-Speicherfreigabe **nicht** ungültig
werden, und eine frische COM-Referenz, die niemand sonst hält, muss es werden.

## Gemessener Stand

Die Zählung ist an allen oben genannten Familien gegen eine testeigene Identität gemessen
(`ComReferenceCountTests`, Schnitt 21). Der Befund war, dass die Umsetzung bereits stimmt:
Jeder Übergang kehrt exakt auf den Ausgangswert zurück, drei erzwungene GC-Läufe ändern an einem
gehaltenen Objekt nichts, und ein Release ohne passendes Retain ist folgenlos statt schädlich.

Eine Eigenschaft ist dabei festzuhalten, weil sie leicht als Zähler missverstanden wird:
`AdoptComObject` nimmt den IUnknown-Hold nur, wenn noch keine VB6-Referenz besteht, erhöht aber
`OwnedRcwReferences` bei jeder Adoption. Wird dieselbe Identität zweimal adoptiert, ruft
`ReleaseComObject` deshalb `Marshal.ReleaseComObject` mehrfach; der erste Aufruf gibt bereits alle
nativen Referenzen des Wrappers frei, jeder weitere läuft in die abgefangene
`InvalidComObjectException`. Das Ergebnis ist gemessen korrekt — aber `OwnedRcwReferences` ist eine
Zählung von Adoptionen, keine native Referenzzählung, und die Schleife trägt ihre Richtigkeit
nicht selbst, sondern über den Abbruch.

## Durchführung und Abnahme

1. **Slot-Protokoll an allen Wertgrenzen.** *Erledigt (Schnitte 18–19).* Direkte Aktivierung,
   fremdes Memberergebnis und geliehener Wert sind getrennte Helfer; Emitter-Tests sichern die
   Auswahl, Runtime-Tests die Wirkung.
2. **Beobachtbare Freigabe über eine Prozessgrenze.** *Erledigt (Schnitt 20).* Ein
   registrierungsfreier ActiveX-EXE-Server bleibt nach dem Freigeben des ersten Slots über seinen
   Alias aufrufbar und beendet sich erst nach dem letzten.
3. **Exakte native Referenzzählung.** *Erledigt (Schnitt 21).* Zehn Fälle über eine testeigene
   IUnknown-Identität, ohne Abhängigkeit von einer registrierten Komponente.
4. **Host-geteilte Wrapper.** *Offen.* Gemessen ist bisher der einfache Fall: VB6 gibt frei, der
   Host behält seine Referenz. Nicht gemessen ist ein Wrapper, den ein fremder Host **gleichzeitig**
   über einen eigenen Anteil hält, während VB6 ihn adoptiert — genau die Konstellation, in der
   `Marshal.ReleaseComObject` einen fremden Anteil mitnehmen kann.
5. **Unabhängige Fremdclient-Probe mit Zählerbeobachtung.** *Offen.* `VB6.ComActivationProbe`
   spricht heute roh über Vtable-Slots mit einem Server, gibt seine Zeiger aber nur ordentlich frei,
   ohne die Rückgabewerte von `AddRef`/`Release` zu lesen. Erst damit wäre die Zählung auch von
   außen und über eine Prozessgrenze belegt statt nur in-proc.
6. **Zyklen und `End`.** Vorhandene Ausführungstests decken Zyklen, abruptes `End`, behandelte
   Fehler, Initialisierungsfehler und reentrante Terminierung ab. Ein pauschaler Shutdown-Drain
   ersetzt diese Regeln nicht und gilt nicht als Nachweis des Zeitpunkts.

`managed-r2-lifetime` bleibt `planned` / `not-yet-verified`, solange 4 und 5 offen sind. Ein
Vertrag, dessen Zählung nur im eigenen Prozess und nur ohne fremde Mitbesitzer gemessen ist, ist
kein abgenommener COM-Lebensdauervertrag.
