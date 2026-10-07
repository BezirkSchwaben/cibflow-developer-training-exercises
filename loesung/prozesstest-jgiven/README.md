# Ausblick: Prozesstest als Szenario mit JGiven

Ein Ausblick in Kapitel 10, kein Teil einer Übung. Der Trainer zeigt hier, wie ein Prozesstest aussieht, den auch lesen kann, wer nicht entwickelt: dieselben Fälle wie Happy Path und Ablehnung der Demo in [`loesung/prozesstest-java/`](../prozesstest-java/), am selben Modell ohne External Task, mit derselben Engine im Speicher. Nur die Testmethode liest sich anders, in Angenommen, Wenn, Dann. Wer mag, startet die Szenarien nach der Schulung selbst.

Ein eigenes Projekt, damit nur lädt, wer JGiven aufruft. `prozesstest-java/` und `loesung/prozesstest-java/` bleiben, wie sie sind.

## Was JGiven ist

[JGiven](https://jgiven.org) ist eine Bibliothek für Tests im Stil von Given, When, Then (Behavior-Driven Development), von TNG Technology Consulting, Lizenz Apache 2.0, hier in Version 2.0.3. Die Szenarien schreibt ihr in Java, ohne Textdateien daneben. Jeder Schritt ist eine Methode, deren Name sich wie ein Satz liest. Nach dem Lauf schreibt JGiven die Szenarien als Text auf die Konsole und als Daten für einen HTML-Bericht.

[CIB seven BPM JGiven](https://github.com/cibseven-community-hub/cibseven-bpm-jgiven) ist eine Community-Extension von CIB seven (`org.cibseven.community:cibseven-bpm-jgiven` 2.2.0, passend zu CIB seven 2.2.0, Lizenz Apache 2.0). Sie bringt die Basisklasse `ProcessStage` für die Stufen mit: Engine und Prozessinstanz wandern von Stufe zu Stufe, dazu fertige Schritte wie `task_is_completed_with_variables`, `variable_is_set` oder `process_is_finished`.

Wie in `loesung/prozesstest-java/` läuft dazu [CIB seven Process Test Coverage](https://github.com/cibseven-community-hub/cibseven-process-test-coverage) für den Abdeckungsbericht.

## Starten

JDK 21 oder neuer. Maven braucht ihr nicht, das Projekt bringt den Maven Wrapper mit. Im Repo-Root:

```bash
# macOS, Linux, Git Bash
cd loesung/prozesstest-jgiven
./mvnw test
./mvnw jgiven:report
open target/jgiven-reports/html/index.html     # Linux: xdg-open
```

```powershell
# Windows PowerShell
cd loesung\prozesstest-jgiven
.\mvnw.cmd test
.\mvnw.cmd jgiven:report
start target\jgiven-reports\html\index.html
```

`./mvnw test` führt die zwei Szenarien aus. JGiven schreibt sie nach der Testklasse als Text auf die Konsole, über den Testbaum:

```
Test Class: io.miragon.schulung.genehmigung.GenehmigungsworkflowSzenarioTest

 Abgelehnter Antrag wird mitgeteilt

   Angenommen ein Antrag über 1200 Euro für „Dienstreise“ ist gestellt
         Wenn die genehmigende Stelle mit „abgelehnt“ entscheidet
         Dann wartet der Antrag bei „Ablehnung mitteilen“
         Wenn die Aufgabe „Ablehnung mitteilen“ erledigt wird
         Dann endet der Antrag bei „Antrag abgelehnt“
          Und kam nie an „Genehmigung verbuchen“ vorbei


 Genehmigter Antrag wird verbucht

   Angenommen ein Antrag über 1200 Euro für „Dienstreise“ ist gestellt
         Wenn die genehmigende Stelle mit „genehmigt“ entscheidet
         Dann wartet der Antrag bei „Genehmigung verbuchen“
          Und die Entscheidung steht auf „genehmigt“
         Wenn die Aufgabe „Genehmigung verbuchen“ erledigt wird
         Dann endet der Antrag bei „Antrag genehmigt“
          Und kam nie an „Ablehnung mitteilen“ vorbei

── Genehmigungsworkflow als Szenario (JGiven) - 0.6 s
   ├─ ✔ genehmigter Antrag wird verbucht - 0.51 s
   └─ ✔ abgelehnter Antrag wird mitgeteilt - 0.06 s

Results:

Tests run: 2, Failures: 0, Errors: 0, Skipped: 0
```

Maven setzt vor die Zeilen ab dem Testbaum `[INFO]`, der Block hier lässt das weg. Im Text stehen die Szenarien nach dem Alphabet, im Testbaum in der Reihenfolge der Testklasse. Unter Windows können Umlaute, „“ und ✔ als Fragezeichen erscheinen, dann vorher `chcp 65001` ausführen.

`./mvnw jgiven:report` baut danach aus dem JSON des letzten Testlaufs (`target/jgiven-reports/json`) den HTML-Bericht `target/jgiven-reports/html/index.html`. Er braucht kein Netz. Die Oberfläche ist englisch, die Szenarien stehen deutsch darin. Die Startseite zeigt nur die Zahlen, etwa `2 Successful`, die Szenarien selbst stehen links unter SUMMARY bei „All Scenarios“. Ein Klick auf ein Szenario klappt seine Schritte auf. Ein grüner Haken am Titel heißt bestanden.

Den Abdeckungsbericht schreibt schon `./mvnw test`, wie in `loesung/prozesstest-java/`:

```
target/process-test-coverage/io.miragon.schulung.genehmigung.GenehmigungsworkflowSzenarioTest/report.html
```

Die zwei Szenarien durchlaufen 13 von 27 Elementen und Pfeilen, `48.15%`. Nachbesserung, Timer und Rücknahme bleiben weiß, für sie gibt es hier kein Szenario.

## Beim ersten Lauf

Der erste `./mvnw test` lädt die Bibliotheken. Lief `prozesstest-java/` schon einmal, liegt das meiste bereits unter `~/.m2/`, JGiven und CIB seven BPM JGiven laden rund 8 MB dazu, vor allem Kotlin und Guava. Ohne diesen Lauf sind es rund 57 MB, dazu Maven selbst mit rund 9 MB. Der erste `./mvnw jgiven:report` lädt das Maven-Plugin von JGiven nach, rund 23 MB. Dabei stehen vier Zeilen `Artifact ... is present in the local repository, but cached from a remote repository ID that is unavailable ...` über der Ausgabe von JGiven, sie sind harmlos. `./mvnw test` lädt das Plugin nicht.

Danach geht beides ohne Netz: `./mvnw -o test` und `./mvnw -o jgiven:report`. Hinter einem Proxy hilft dasselbe wie für `prozesstest-java/`, siehe [Hinter einem Proxy](../../prozesstest-java/README.md#hinter-einem-proxy).

## Wie ein Szenario aussieht

```java
@Test
@Order(1)
void genehmigter_Antrag_wird_verbucht() {
    angenommen().ein_Antrag_über_$_Euro_für_$_ist_gestellt(1200, "Dienstreise");
    wenn().die_genehmigende_Stelle_mit_$_entscheidet("genehmigt");
    dann().wartet_der_Antrag_bei_$("Task_Verbuchen")
        .und().die_Entscheidung_steht_auf_$("genehmigt");
    wenn().die_Aufgabe_$_erledigt_wird("Task_Verbuchen");
    dann().endet_der_Antrag_bei_$("End_Genehmigt")
        .und().kam_nie_an_$_vorbei("Task_Ablehnen");
}
```

- `angenommen()`, `wenn()` und `dann()` liefern die drei Stufen `AngenommenStufe`, `WennStufe` und `DannStufe`, `.und()` hängt einen weiteren Schritt an. Die deutschen Schlüsselwörter kommen aus `SzenarioTest`, der deutschen Fassung von `ScenarioTest` in JGiven.
- Aus den Unterstrichen im Methodennamen werden Leerzeichen, an jedes `$` setzt JGiven den Wert.
- Im Code steht die ID aus dem Modell (`"Task_Verbuchen"`), im Bericht die Beschriftung („Genehmigung verbuchen“). Die liest `Beschriftung.java` aus dem gerade deployten Modell. Ändert ihr nur die Beschriftung, etwa „Genehmigung verbuchen“, bleiben die Szenarien grün, und Konsole und Bericht zeigen den neuen Namen.
- Was ein Schritt tut, steht in den Stufen: starten wie das Startformular, Aufgaben abschließen, die Speicherpunkte anstoßen und prüfen, mit denselben Arten von Prüfungen wie in `GenehmigungsworkflowTag1Test` (`isWaitingAtExactly`, `isEnded`, `hasPassed`, `hasNotPassed`, Variablen). Die fertigen Schritte aus `ProcessStage` sind englisch benannt, die Stufen rufen sie deshalb nur intern auf. Im Bericht steht allein der deutsche Satz.

Wird ein Szenario rot, zeigen die Konsole und, nach `./mvnw jgiven:report`, der Bericht den Schritt, an dem es hakt, alles danach ist übersprungen. Mit der ID `Task_Pruefen2` statt `Task_Pruefen` für „Antrag prüfen“ in der Modellkopie:

```
 Genehmigter Antrag wird verbucht

   Angenommen ein Antrag über 1200 Euro für „Dienstreise“ ist gestellt
         Wenn die genehmigende Stelle mit „genehmigt“ entscheidet (failed)
         Dann wartet der Antrag bei „Genehmigung verbuchen“ (skipped)
          Und die Entscheidung steht auf „genehmigt“ (skipped)
         Wenn die Aufgabe „Genehmigung verbuchen“ erledigt wird (skipped)
         Dann endet der Antrag bei „Antrag genehmigt“ (skipped)
          Und kam nie an „Ablehnung mitteilen“ vorbei (skipped)

FAILED: java.lang.AssertionError: Expecting ProcessInstance {id='8', processDefinitionId='Process_Genehmigung:1:3', businessKey='Antrag-1'} to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Pruefen2].
```

Maven zählt die roten Szenarien als „Error“, nicht als „Failure“: `Tests run: 2, Failures: 0, Errors: 2, Skipped: 0`. Zurück geht es im Ordner `loesung/prozesstest-jgiven/` mit `git restore .`, danach zeigt `git status` keine Änderung. Die Berichte unter `target/` setzt das nicht zurück: Den grünen Stand zeigen sie erst wieder nach `./mvnw test` und danach `./mvnw jgiven:report`.

## Was drin ist

- `src/main/resources/genehmigungsworkflow-tag1.bpmn`: Kopie von `prozess/genehmigungsworkflow.bpmn`, dem fertigen Modell nach Übung 7, nur mit User Tasks. Dieselbe Kopie wie in `loesung/prozesstest-java/`, die CI prüft, dass sie byte-gleich bleibt. Ändert sich der Startstand, kopiert ihr sie im Repo-Root neu: `cp prozess/genehmigungsworkflow.bpmn loesung/prozesstest-jgiven/src/main/resources/genehmigungsworkflow-tag1.bpmn`.
- `src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowSzenarioTest.java`: die zwei Szenarien.
- `AngenommenStufe.java`, `WennStufe.java`, `DannStufe.java` daneben: die Schritte. Ihre gemeinsame Basis `GenehmigungsStufe.java` erbt von `ProcessStage`, ergänzt `und()` und stößt die Speicherpunkte an.
- `Beschriftung.java`: setzt im Bericht die Beschriftung aus dem Modell statt der ID.
- `src/test/resources/camunda.cfg.xml`: die Engine im Speicher, ohne Job Executor, mit den Listenern für den Abdeckungsbericht.
- `pom.xml`: Engine und Testbibliotheken wie in `loesung/prozesstest-java/`, ohne bpmn-to-code, dazu JGiven, CIB seven BPM JGiven und das Maven-Plugin für den HTML-Bericht. JGiven schreibt ohne Farben, sonst zerlegt es Umlaute und „“ auf der Konsole. Ab JDK 24 schaltet ein Profil eine Warnung von Java über dem Testbaum ab.
- `.mvn/jvm.config`: stellt Maven leiser, übrig bleiben Szenarien, Testbaum und Meldungen.

Die IDs stehen als Text wie in `GenehmigungsworkflowTag1Test`, das Projekt erzeugt keine Konstanten mit bpmn-to-code. Die Testklasse bindet die Engine über ein statisches Feld mit `@RegisterExtension` ein statt mit `@ExtendWith`: JGiven liest das Feld `camunda` für die Stufen, bevor `@ExtendWith` die Engine dort eintragen würde.
