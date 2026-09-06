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

### Anteile, nicht Referenzen

Der Punkt, an dem der COM-Besitz tatsächlich hängt, ist gemessen und war vorher nirgends
aufgeschrieben: Die CLR führt **einen** RCW pro IUnknown-Identität. Ein zweites Marshalling
derselben Identität liefert dasselbe Objekt und vergibt einen weiteren **Anteil** daran — keine
weitere native Referenz. `Marshal.ReleaseComObject` verbraucht genau einen Anteil und gibt die
native Referenz erst frei, wenn der letzte weg ist.

Daraus folgt die eigentliche Vorbedingung der Adoption: Sie **verbraucht einen Anteil** und setzt
voraus, dass der adoptierte Wert einen eigenen mitgebracht hat. Ein gemarshalltes COM-Ergebnis tut
das immer. Bekommt die Adoption dagegen genau das Objekt, das ein verwalteter Halter benutzt, ohne
eigenen Anteil, dann nimmt die Freigabe durch VB6 den Wrapper des Halters mit.

Gemessen wurde beides: Mit eigenem Anteil überlebt der Halter (auch bei zwei adoptierten
Memberergebnissen), ohne eigenen Anteil ist sein Wrapper danach ungültig. **Kein erzeugter Pfad
erreicht den zweiten Fall** — ein aus VB6-Speicher zurückgelesener Wert gilt als geliehen, und jeder
adoptierende Pfad läuft über Marshalling. Die Grenze ist trotzdem als Fall festgeschrieben, damit
ein künftiger Pfad dort scheitert statt an einer weit entfernten Aufrufstelle.

Zu unterscheiden ist das von einem **nativen** Fremdhalter — einem echten COM-Client in einem
anderen Apartment oder Prozess oder einem Control, das sein Objekt selbst hält. Der hat seine
eigene `AddRef` und ist von `Marshal.ReleaseComObject` gar nicht betroffen. Gefährdet ist nur ein
verwalteter Mithalter im selben Prozess, der sich denselben RCW teilt.

### Weiteres

Eine Eigenschaft ist außerdem festzuhalten, weil sie leicht als Zähler missverstanden wird:
`AdoptComObject` nimmt den IUnknown-Hold nur, wenn noch keine VB6-Referenz besteht, erhöht aber
`OwnedRcwReferences` bei jeder Adoption. `ReleaseComObject` ruft `Marshal.ReleaseComObject`
entsprechend oft — und das ist richtig, solange jede Adoption ihren eigenen Anteil mitgebracht hat:
Dann verbraucht jeder Aufruf genau einen. Nur wenn öfter adoptiert als angeliefert wurde, gibt der
erste Aufruf bereits alles frei und die restlichen laufen in die abgefangene
`InvalidComObjectException`. Der Zähler heißt also Adoptionen, nicht native Referenzen, und die
Schleife trägt ihre Richtigkeit in diesem Fall über den Abbruch statt über die Zählung.

## Durchführung und Abnahme

1. **Slot-Protokoll an allen Wertgrenzen.** *Erledigt (Schnitte 18–19).* Direkte Aktivierung,
   fremdes Memberergebnis und geliehener Wert sind getrennte Helfer; Emitter-Tests sichern die
   Auswahl, Runtime-Tests die Wirkung.
2. **Beobachtbare Freigabe über eine Prozessgrenze.** *Erledigt (Schnitt 20).* Ein
   registrierungsfreier ActiveX-EXE-Server bleibt nach dem Freigeben des ersten Slots über seinen
   Alias aufrufbar und beendet sich erst nach dem letzten.
3. **Exakte native Referenzzählung.** *Erledigt (Schnitt 21).* Zehn Fälle über eine testeigene
   IUnknown-Identität, ohne Abhängigkeit von einer registrierten Komponente.
4. **Host-geteilte Wrapper.** *Erledigt für den verwalteten Mithalter (Schnitt 22).* Ein Halter,
   der seinen eigenen Anteil hat, überlebt die Adoption und Freigabe durch VB6 — auch bei zwei
   adoptierten Memberergebnissen. Der Fall ohne eigenen Anteil ist als Grenze festgeschrieben; kein
   erzeugter Pfad erreicht ihn. Ein **nativer** Fremdhalter ist von `Marshal.ReleaseComObject`
   ohnehin nicht betroffen; seine Prüfung gehört zu Schritt 5.
5. **Unabhängige Fremdclient-Probe mit Zählerbeobachtung.** *Offen.* `VB6.ComActivationProbe`
   spricht heute roh über Vtable-Slots mit einem Server, gibt seine Zeiger aber nur ordentlich frei,
   ohne die Rückgabewerte von `AddRef`/`Release` zu lesen. Erst damit wäre die Zählung auch von
   außen und über eine Prozessgrenze belegt statt nur in-proc.
6. **Zyklen und `End`.** Vorhandene Ausführungstests decken Zyklen, abruptes `End`, behandelte
   Fehler, Initialisierungsfehler und reentrante Terminierung ab. Ein pauschaler Shutdown-Drain
   ersetzt diese Regeln nicht und gilt nicht als Nachweis des Zeitpunkts.

`managed-r2-lifetime` bleibt `planned` / `not-yet-verified`, solange 5 offen ist. Die gesamte
Zählung ist bisher im eigenen Prozess gemessen; ein nativer Fremdhalter jenseits der
Prozessgrenze hat sie noch nicht bestätigt, und ohne diese Gegenprobe ist es kein abgenommener
COM-Lebensdauervertrag.
