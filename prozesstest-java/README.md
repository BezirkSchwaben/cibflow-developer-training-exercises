# Prozesstest in Java

Ein Projekt für zwei Kapitel: In Kapitel 12 schreibt ihr hier in Übung 9 eure Prozesstests in Java, zusätzlich zu denen in C#. In Kapitel 10 zeigt der Trainer an der Musterlösung dieses Projekts, wie ein Prozesstest funktioniert.

Der Test läuft mit dem Java-Stack aus dem CIB seven Training: `cibseven-bpm-junit5` startet die Engine mit H2 im Speicher, `cibseven-bpm-assert` liefert die Prüfungen. Kein Server, keine Oberfläche, kein Docker. Stack und Worker braucht dieser Test nicht, er läuft auch, wenn beide aus sind. Nach jedem Lauf zeigt ein Bericht im Browser, was die Tests im Modell durchlaufen haben, siehe [Abdeckung im Modell](#abdeckung-im-modell).

## Voraussetzung und Befehle

JDK 21 oder neuer (`java -version`). Maven braucht ihr nicht, der Maven Wrapper lädt es beim ersten Lauf. Die Befehle gehen vom Repo-Root aus.

```bash
# macOS, Linux, Git Bash
cd prozesstest-java
./mvnw test
./mvnw -o test       # ab dem zweiten Lauf, ohne Netz
```

```powershell
# Windows PowerShell
cd prozesstest-java
.\mvnw.cmd test
.\mvnw.cmd -o test   # ab dem zweiten Lauf, ohne Netz
```

Der erste Lauf lädt Maven und die Bibliotheken, rund 70 MB. Lasst ihn deshalb einmal vor der Schulung laufen. Danach dauert ein Lauf wenige Sekunden. Lief der Test bei euch schon vor bpmn-to-code (siehe [IDs aus dem Modell](#ids-aus-dem-modell)), lädt der erste Lauf nach dem `git pull` noch rund 13 MB nach. Lief er mit bpmn-to-code, aber vor dem Abdeckungsbericht (siehe [Abdeckung im Modell](#abdeckung-im-modell)), sind es rund 2 MB. Ohne Netz meldet `./mvnw -o test` bis dahin `Cannot access central (https://repo.maven.apache.org/maven2) in offline mode`.

Unter Windows können ✔, ✘, ↷ und „“ in der Konsole als Fragezeichen erscheinen. Dann vorher `chcp 65001` ausführen. Hilft das nicht, schaltet `.\mvnw.cmd test "-Dbaum.theme=ASCII"` den Baum auf `+--`, `[OK]`, `[XX]` und, für übersprungen, `[??]` um.

## Hinter einem Proxy

Der erste Lauf lädt alles von `repo.maven.apache.org`. Scheitert er mit `PKIX path building failed` oder `Could not transfer artifact`, sitzt ihr hinter einem Proxy, oder eure IT prüft verschlüsselte Verbindungen. .NET und Docker laufen dann oft trotzdem: Maven liest den Proxy nicht aus den Umgebungsvariablen, und Java prüft Zertifikate gegen eine eigene Liste, nicht gegen die des Betriebssystems.

- **Proxy:** Tragt ihn in `~/.m2/settings.xml` ein, unter Windows `%USERPROFILE%\.m2\settings.xml`, so wie in [Configuring a proxy](https://maven.apache.org/guides/mini/guide-proxies.html) beschrieben. Adresse und Port kennt eure IT.
- **`PKIX path building failed`:** Java kennt das Zertifikat nicht, mit dem eure IT die Verbindungen prüft. Fragt die IT nach dem Zertifikat und danach, wie es ins JDK kommt.
- **Am schnellsten:** den ersten Lauf außerhalb des Behördennetzes starten, etwa über einen Hotspot. Danach liegt alles unter `~/.m2/`, und `./mvnw -o test` läuft ohne Netz.

Klappt es bis zur Schulung nicht, schreibt ihr den Java-Teil zu zweit am Rechner eurer Nachbarn.

## Was drin ist

- `src/main/resources/genehmigungsworkflow.bpmn`: Kopie der Entwickler-Fassung `loesung/genehmigungsworkflow-entwickler.bpmn`, das Modell nach Übung 8 mit „Genehmigung verbuchen“ als External Task, dieselben IDs.
- `src/main/resources/verbuchen-fehlerpfad.bpmn`: Kopie der Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` für den Bonus. Die CI prüft, dass beide Kopien byte-gleich zu ihren Quellen sind.
- `src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java`: der Happy Path fertig, als Vorbild mit den fünf nummerierten Schritten eines Testfalls. Ablehnung, Nachbesserung und Timer stehen als `TODO Kapitel 12, Schritt 5` mit `@Disabled` darunter, dazu die Hilfsmethoden `antragStarten` und `speicherpunktAnstossen`.
- `src/test/resources/camunda.cfg.xml`: die Engine im Speicher, ohne Job Executor, mit den Listenern für den Abdeckungsbericht.
- `pom.xml`: Engine und Testbibliotheken, dazu bpmn-to-code. Es erzeugt bei jedem Lauf aus den Modellen die Klassen mit den IDs, siehe [IDs aus dem Modell](#ids-aus-dem-modell). Dazu CIB seven Process Test Coverage für den Bericht, siehe [Abdeckung im Modell](#abdeckung-im-modell).
- `.mvn/jvm.config`: stellt Maven und bpmn-to-code leiser, übrig bleiben Testbaum, Meldungen und Fehler. Deshalb fehlen die Zeilen BUILD SUCCESS und BUILD FAILURE.

Die Musterlösung liegt als vollständiges Projekt unter [`loesung/prozesstest-java/`](../loesung/prozesstest-java/), mit Maven Wrapper, `pom.xml` und denselben Modellkopien. Sie unterscheidet sich nur in den Tests: `GenehmigungsworkflowTest.java` mit allen vier Tests und, als Bonus, `FehlerpfadTest.java` für die Variante. Dazu kommen, nur für die Demo in Kapitel 10, `GenehmigungsworkflowTag1Test.java` und `genehmigungsworkflow-tag1.bpmn`. „Tag1“ steht dort für das Modell ohne External Task. Ihr startet sie im Repo-Root mit `cd loesung/prozesstest-java` und `./mvnw test` (PowerShell: `.\mvnw.cmd test`), nichts wird kopiert. Vergleichen könnt ihr im Repo-Root mit `git diff --no-index prozesstest-java/src/test/java loesung/prozesstest-java/src/test/java`.

Speicherpunkte: Der Baustein „CIB easyForm“ setzt hinter „Antrag gestellt“, hinter „Antrag prüfen“ und, seit Übung 7, hinter „Antrag nachbessern“ je einen Speicherpunkt (`camunda:asyncAfter`). Ohne Job Executor stößt der Test alle drei selbst an (`speicherpunktAnstossen`, darin `execute(job(...))`), den Timer ebenso. Nach dem Abschließen von „Antrag prüfen“ entscheidet das Gateway also erst am Speicherpunkt. Die Aufgaben ohne Formular („Ablehnung mitteilen“, „Erinnerung senden“, „Genehmigende Stelle benachrichtigen“) haben keinen Speicherpunkt, nach `complete(task())` läuft die Engine dort sofort weiter.

Timer: „3 Tage ohne Entscheidung“ steht im Modell fürs Training auf drei Minuten (`PT3M`). Der Test prüft deshalb eine Fälligkeit in drei Minuten und führt den Timer-Job gleich aus, statt zu warten.

## IDs aus dem Modell

Die Tests der Übung und des Bonus schreiben keine ID als Text. Process ID, Element-IDs, Topic und Fehlercode lesen sie aus Klassen, die [bpmn-to-code](https://github.com/Miragon/bpmn-to-code) von Miragon aus den Modellen erzeugt. Das Maven-Plugin läuft bei jedem `./mvnw test` vor dem Übersetzen der Tests, liest die Modelle in `src/main/resources/` und schreibt die Klassen neu nach `target/generated-test-sources/bpmn-to-code/`. Den Ordner leert Maven vorher, so bleibt keine Klasse eines alten Stands liegen. Im Repo stehen die Klassen nicht, von Hand ändert ihr sie nicht.

| Modell | Klasse |
|---|---|
| `genehmigungsworkflow.bpmn` | `io.miragon.schulung.genehmigung.api.ProcessGenehmigungProcessApi` |
| `verbuchen-fehlerpfad.bpmn` | `io.miragon.schulung.genehmigung.api.fehlerpfad.ProcessVerbuchenFehlerpfadProcessApi` |
| `genehmigungsworkflow-tag1.bpmn`, nur in der Musterlösung | `io.miragon.schulung.genehmigung.api.tag1.ProcessGenehmigungProcessApi` |

Die Demo-Klasse `GenehmigungsworkflowTag1Test` nutzt ihre Klasse nicht, sie schreibt die IDs als Text. Warum, steht unter [Demo](#demo-kapitel-10-trainer), Variante A.

Aus jeder ID im Modell wird eine Konstante in `Elements`: aus `Task_Pruefen` wird `TASK_PRUEFEN`, aus `StartEvent_Antrag` wird `START_EVENT_ANTRAG`. Dazu kommen `PROCESS_ID`, das Topic als `ServiceTasks.GENEHMIGUNG_VERBUCHEN` und in der Variante der Fehlercode als `Errors.BUCHUNG_ABGELEHNT`. Der Test importiert sie statisch, mit `Elements.*` alle IDs auf einmal:

```java
import static io.miragon.schulung.genehmigung.api.ProcessGenehmigungProcessApi.Elements.*;
import static io.miragon.schulung.genehmigung.api.ProcessGenehmigungProcessApi.ServiceTasks.GENEHMIGUNG_VERBUCHEN;

speicherpunktAnstossen(antrag, START_EVENT_ANTRAG);
assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue());
assertThat(antrag).externalTask().hasTopicName(GENEHMIGUNG_VERBUCHEN);
```

Eine ID ist vom Typ `ElementId`, die Prüfungen der Engine erwarten Text. Deshalb schreibt ihr `TASK_PRUEFEN.getValue()`, nur die Hilfsmethode `speicherpunktAnstossen` nimmt die Konstante selbst. Ebenso liefert `PROCESS_ID.getValue()` die Process ID und `BUCHUNG_ABGELEHNT.getCode()` den Fehlercode, das Topic `GENEHMIGUNG_VERBUCHEN` ist schon Text. Variablennamen wie `entscheidung` und `buchungsnummer` bleiben Text: Das Modell legt sie nicht als Ein- oder Ausgabe fest, deshalb erzeugt bpmn-to-code für sie keine Konstanten.

Ändert jemand eine ID im Modell, heißt beim nächsten Lauf auch die Konstante anders, und Maven übersetzt den Test nicht mehr. Die Meldung nennt jede Zeile, die noch die alte ID nutzt. Mit `Task_Pruefen2` statt `Task_Pruefen` im Startstand:

```
[ERROR] COMPILATION ERROR :
[ERROR] .../GenehmigungsworkflowTest.java:[65,47] Symbol nicht gefunden
  Symbol: Variable TASK_PRUEFEN
  Ort: Klasse io.miragon.schulung.genehmigung.GenehmigungsworkflowTest
```

Englisch heißt das `cannot find symbol`. Die neue Konstante heißt `TASK_PRUEFEN_2`. Alle Namen stehen in der erzeugten Datei, etwa `target/generated-test-sources/bpmn-to-code/io/miragon/schulung/genehmigung/api/ProcessGenehmigungProcessApi.java`, Abschnitt `Elements`. Eure IDE kennt die Klassen erst nach dem ersten `./mvnw test`, ladet danach das Maven-Projekt neu.

Weil Maven die Klassen bei jedem Lauf neu erzeugt, steht über dem Testbaum jedes Mal eine Zeile `Compiling 3 source files with javac ...`, in der Musterlösung `Compiling 6 source files ...`. Je Modell gibt es eine eigene Ausführung des Plugins mit eigenem Paket: `genehmigungsworkflow.bpmn` und `genehmigungsworkflow-tag1.bpmn` tragen dieselbe Process ID und ergäben im selben Paket dieselbe Klasse. Im Startstand fehlt `genehmigungsworkflow-tag1.bpmn`, seine Ausführung erzeugt dann nichts.

## Abdeckung im Modell

Die Tests laufen mit der `ProcessEngineCoverageExtension` aus [CIB seven Process Test Coverage](https://github.com/cibseven-community-hub/cibseven-process-test-coverage), einer Community-Extension von CIB seven, statt mit der `ProcessEngineExtension`. Sie erbt von ihr, Engine und Prüfungen bleiben gleich. Dazu schreibt sie nach jeder Testklasse einen Bericht in einen Ordner mit dem Namen der Klasse, hier:

```
target/process-test-coverage/io.miragon.schulung.genehmigung.GenehmigungsworkflowTest/report.html
```

Öffnet die Datei im Browser, per Doppelklick oder im Ordner `prozesstest-java/` mit `open target/process-test-coverage/*/report.html` (macOS) oder `start target\process-test-coverage\io.miragon.schulung.genehmigung.GenehmigungsworkflowTest\report.html` (Windows). Der Bericht braucht kein Netz, jeder Lauf schreibt ihn neu.

Er zeigt das Modell: Grün gefüllt sind die Elemente, die die Tests der Klasse durchlaufen haben, dunkelgrün die Pfeile. Darunter steht unter „Run Selection“ die Klasse mit `Covered`, `Total` und `Coverage`: wie viele Elemente und Pfeile (Sequence Flows) des Modells die Tests durchlaufen haben, von wie vielen, in Prozent. Das Feld `Coverage` ist ab 90 % grün, ab 50 % gelb, darunter rot. Ein Klick auf die Zeile klappt die Testmethoden auf, ein Klick auf eine Methode zeigt nur ihren Pfad. Im Startstand läuft nur der Happy Path, 9 von 27, `33.33%`. Mit Ablehnung und Nachbesserung sind es 16 von 27, `59.26%`, mit dem Timer 21 von 27, `77.78%`. Weiß bleibt dann nur noch die Rücknahme, für sie gibt es keinen Test. Eine Mindestabdeckung verlangen die Tests nicht, ein Bericht mit wenig Grün macht keinen Test rot.

## Übung 9 (Kapitel 12)

Die Aufgabe steht im Aufgabenblatt, [Kapitel 12, Schritt 5](../aufgaben/kapitel-12-worker-und-tests.md#5-prozesstest-in-java). Kurz: Ihr schreibt die Tests für Ablehnung und Nachbesserung, wer schneller ist, auch den Timer. Die Kommentare in jeder TODO-Methode sagen, was ihr startet, wo die Instanz wartet und was ihr prüft.

Im Startstand läuft nur der Happy Path, die drei anderen Tests überspringt JUnit (`↷`) und nennt dahinter den Grund aus `@Disabled`:

```
── Genehmigungsworkflow - 1.1 s
   ├─ ✔ Happy Path: Antrag genehmigt und verbucht - 0.21 s
   ├─ ↷ Ablehnung: „Ablehnung mitteilen“, nie verbuchen (TODO Kapitel 12, Schritt 5) - 0 s
   ├─ ↷ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ (TODO Kapitel 12, Schritt 5) - 0 s
   └─ ↷ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen (TODO Kapitel 12, Schritt 5, für alle, die schneller sind) - 0 s

Results:

Tests run: 4, Failures: 0, Errors: 0, Skipped: 3
```

Maven setzt `[INFO]` vor jede Zeile, die Blöcke hier lassen das weg. Sind Tests übersprungen, steht `[WARNING]` vor `Tests run`. Das ist kein Fehler.

Fertig seid ihr, wenn Ablehnung und Nachbesserung ein ✔ tragen und `Failures: 0, Errors: 0` dasteht. Mit Timer tragen alle vier Zeilen ein ✔, und es steht `Skipped: 0` da.

## Demo (Kapitel 10, Trainer)

Die Live-Demo in Kapitel 10 „Wie ein Prozesstest funktioniert“: Test grün, Modell geändert, Test rot. Kein Übungsteil, niemand tippt mit. Die Demo läuft direkt in der Musterlösung, im Ordner `loesung/prozesstest-java/`, mit der Klasse `GenehmigungsworkflowTag1Test` am fertigen Modell nach Übung 7, nur mit User Tasks (`src/main/resources/genehmigungsworkflow-tag1.bpmn`). „Tag1“ im Namen steht für dieses Modell ohne External Task. Dort sind alle vier Tests fertig: Im Startstand sind Ablehnung, Nachbesserung und Timer übersprungen, Variante B bliebe dann grün. Der Startstand `prozesstest-java/` bleibt für die Übung unberührt.

In der Musterlösung liegen auch `GenehmigungsworkflowTest.java` (dieselben vier Tests am Modell mit External Task) und der Bonus `FehlerpfadTest.java`. Der Zusatz `-Dtest=GenehmigungsworkflowTag1Test` lässt beide weg, so laufen genau die vier Tests der Demo. Ohne den Zusatz laufen alle drei Klassen, Maven zählt dann 10 statt 4 Tests.

### Vorbereitung

1. Im Repo-Root `cd loesung/prozesstest-java`. Alle Läufe der Demo starten in diesem Ordner. Vor der Demo zeigt `git status` keine Änderung.
2. Einmal die Tests laufen lassen und die Demo einmal durchspielen:
   ```bash
   # bash, zsh, Git Bash
   ./mvnw test -Dtest=GenehmigungsworkflowTag1Test
   ```
   ```powershell
   # PowerShell
   .\mvnw.cmd test "-Dtest=GenehmigungsworkflowTag1Test"
   ```

Das Modell zeigt ihr in VS Code mit der Erweiterung Miragon BPMN Modeler, daneben das Terminal. Schrift groß, vor jedem Lauf `clear`. Geändert wird nur `loesung/prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn`, das Modell unter `prozess/` bleibt unberührt.

### Ablauf

1. **Grün:** `./mvnw test -Dtest=GenehmigungsworkflowTag1Test`, jeder weitere Lauf nutzt denselben Befehl.
   ```
   ── Genehmigungsworkflow, Modell ohne External Task - 1.2 s
      ├─ ✔ Happy Path: Antrag genehmigt und verbucht - 0.20 s
      ├─ ✔ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.06 s
      ├─ ✔ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.05 s
      └─ ✔ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.03 s

   Results:

   Tests run: 4, Failures: 0, Errors: 0, Skipped: 0
   ```
   Im Editor nur den Happy-Path-Test `genehmigterAntragWirdVerbucht` zeigen, die Imports einklappen. Seine Kommentare nummerieren die fünf Schritte eines Testfalls: Starten, Warten, Entscheiden, Verbuchen, Beenden. Der Bericht dieses Laufs liegt unter `target/process-test-coverage/io.miragon.schulung.genehmigung.GenehmigungsworkflowTag1Test/report.html`: 21 von 27, `77.78%`, weiß bleibt die Rücknahme, siehe [Abdeckung im Modell](#abdeckung-im-modell).
2. **Variante A, ID ändern:** „Antrag prüfen“ anklicken und im Properties-Panel die ID `Task_Pruefen` in `Task_Pruefen2` ändern, speichern. Der Modeler zieht die Verweise mit (Timer, Sequenzflüsse). Ohne Modeler im Texteditor mit Suchen und Ersetzen, Option „Nur ganzes Wort“. Den Test nicht anfassen. Ergebnis: alle vier Tests rot, denn jeder wartet bei „Antrag prüfen“.
   ```
   ── Genehmigungsworkflow, Modell ohne External Task - 1.1 s
      ├─ ✘ Happy Path: Antrag genehmigt und verbucht - 0.16 s
      ├─ ✘ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.04 s
      ├─ ✘ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.04 s
      └─ ✘ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.03 s
   ```
   Die Meldungen stehen unter dem Baum im Block „Results“, je Test eine Zeile, sortiert nach Methodenname. Der Happy Path heißt dort `genehmigterAntragWirdVerbucht` und steht an zweiter Stelle:
   ```
   GenehmigungsworkflowTag1Test.genehmigterAntragWirdVerbucht:55 Expecting ProcessInstance {id='8', processDefinitionId='Process_Genehmigung:1:3', businessKey='Antrag-1'} to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Pruefen2].
   ```
   Darunter steht `Tests run: 4, Failures: 4, Errors: 0, Skipped: 0`. Genau dafür schreibt die Demo-Klasse die IDs als Text: Der Test übersetzt, läuft an und wird erst beim Prüfen rot. Mit den Konstanten aus bpmn-to-code, wie in Übung 9, fiele dieselbe Änderung schon beim Übersetzen auf, siehe [IDs aus dem Modell](#ids-aus-dem-modell).
3. **Variante B, „abgelehnt“ löschen:** Erst das Modell zurücksetzen (Schritt 5), dann den Pfeil „abgelehnt“ (`Flow_Abgelehnt`) anklicken und löschen, speichern. Im Texteditor gehören dazu auch `<bpmn:outgoing>` am Gateway, `<bpmn:incoming>` an `Task_Ablehnen` und die Kante `Flow_Abgelehnt_di`. Ergebnis: Nur der Ablehnungs-Test ist rot, Maven zählt ihn als „Error“, nicht als „Failure“. Das Abschließen der Aufgabe gelingt, die Engine scheitert erst am Speicherpunkt dahinter, die Zeile nennt deshalb `speicherpunktAnstossen`:
   ```
   GenehmigungsworkflowTag1Test.abgelehnterAntragWirdMitgeteilt:83->speicherpunktAnstossen:162 » ProcessEngine ENGINE-02004 No outgoing sequence flow for the element with id 'Gateway_Entscheidung' could be selected for continuing the process.
   ```
   Darunter steht `Tests run: 4, Failures: 0, Errors: 1, Skipped: 0`.
4. **Gegenprobe:** Erst das Modell zurücksetzen, dann nur die Beschriftung „Antrag prüfen“ ändern, etwa in „Antrag fachlich prüfen“. Alle vier Tests bleiben grün, der Test hängt an der ID.
5. **Modell zurücksetzen**, nach jeder Variante, im Ordner `loesung/prozesstest-java/`, in bash und PowerShell gleich:
   ```bash
   git restore .
   ```
   Im Repo-Root heißt derselbe Befehl `git restore loesung/prozesstest-java`. Er setzt das Modell zurück. Der fertige Test gehört in der Musterlösung zum Repo-Stand und bleibt.
6. **Nach der Demo** noch einmal zurücksetzen und prüfen: `git status` zeigt keine Änderung mehr, und der Lauf aus Schritt 1 ist wieder grün. Bleibt eine geänderte Modellkopie im Repo, wird die CI rot: Sie vergleicht `genehmigungsworkflow-tag1.bpmn` mit `prozess/genehmigungsworkflow.bpmn`. Den Startstand `prozesstest-java/` hat die Demo nicht angefasst, dort meldet `./mvnw test` weiter 1 bestanden, 3 übersprungen.

Maven setzt vor jede Zeile `[INFO]`, vor die Meldungen und vor `Tests run` bei Rot `[ERROR]`, die Blöcke hier lassen das weg. Bei Rot folgt unter „Results“ noch der Fehlertext von Maven. Baum und Meldungen stehen darüber, notfalls etwas hochscrollen.

### Rot nach Übung 8

Für den Einstieg in Kapitel 12: Der Test der Demo kennt „Genehmigung verbuchen“ als Aufgabe. Nach dem Umbau aus Übung 8 wird er rot. Zeigen lässt sich das mit der Entwickler-Fassung über der Modellkopie der Demo:

```bash
# im Repo-Root: die Entwickler-Fassung über die Modellkopie der Demo legen
cp loesung/genehmigungsworkflow-entwickler.bpmn loesung/prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn
cd loesung/prozesstest-java
./mvnw test -Dtest=GenehmigungsworkflowTag1Test
git restore .
```

Nur der Happy Path wird rot, Maven zählt ihn als „Error“:

```
GenehmigungsworkflowTag1Test.genehmigterAntragWirdVerbucht:66 » IllegalArgument Illegal call of complete(task = 'null') - must not be null!
```

Darunter steht `Tests run: 4, Failures: 0, Errors: 1, Skipped: 0`. Danach wie nach jeder Variante prüfen: `git status` zeigt keine Änderung.

## Modellkopien nachziehen

Ändert sich ein Modell unter `prozess/` oder die Entwickler-Fassung unter `loesung/`, kopiert ihr es im Repo-Root neu nach `src/main/resources/`, im Startstand und in der Musterlösung. Die Klassen mit den IDs zieht ihr nicht nach, der nächste Lauf erzeugt sie aus den neuen Kopien:

```bash
cp loesung/genehmigungsworkflow-entwickler.bpmn prozesstest-java/src/main/resources/genehmigungsworkflow.bpmn
cp loesung/genehmigungsworkflow-entwickler.bpmn loesung/prozesstest-java/src/main/resources/genehmigungsworkflow.bpmn
cp prozess/genehmigungsworkflow.bpmn loesung/prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn
cp prozess/varianten/verbuchen-fehlerpfad.bpmn prozesstest-java/src/main/resources/
cp prozess/varianten/verbuchen-fehlerpfad.bpmn loesung/prozesstest-java/src/main/resources/
```
