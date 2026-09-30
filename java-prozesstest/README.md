# Prozesstest in Java

Ein Projekt für zwei Kapitel: In Kapitel 12 schreibt ihr hier eure Prozesstests in Java, zusätzlich zu denen in C#. In Kapitel 10 zeigt der Trainer mit demselben Projekt, wie ein Prozesstest funktioniert.

Der Test läuft mit dem Java-Stack aus dem CIB seven Training: `cibseven-bpm-junit5` startet die Engine mit H2 im Speicher, `cibseven-bpm-assert` liefert die Prüfungen. Kein Server, keine Oberfläche, kein Docker. Stack und Worker braucht dieser Test nicht, er läuft auch, wenn beide aus sind.

## Voraussetzung und Befehle

JDK 21 oder neuer (`java -version`). Maven braucht ihr nicht, der Maven Wrapper lädt es beim ersten Lauf. Die Befehle gehen vom Repo-Root aus.

```bash
# macOS, Linux, Git Bash
cd java-prozesstest
./mvnw test
./mvnw -o test       # ab dem zweiten Lauf, ohne Netz
```

```powershell
# Windows PowerShell
cd java-prozesstest
.\mvnw.cmd test
.\mvnw.cmd -o test   # ab dem zweiten Lauf, ohne Netz
```

Der erste Lauf lädt Maven und die Bibliotheken, rund 55 MB. Lasst ihn deshalb einmal vor der Schulung laufen. Danach dauert ein Lauf wenige Sekunden.

Unter Windows können ✔, ✘, ↷ und „“ in der Konsole als Fragezeichen erscheinen. Dann vorher `chcp 65001` ausführen. Hilft das nicht, schaltet `.\mvnw.cmd test "-Dbaum.theme=ASCII"` den Baum auf `+--`, `[OK]`, `[XX]` und, für übersprungen, `[??]` um.

## Was drin ist

- `src/main/resources/genehmigungsworkflow.bpmn`: Kopie der Vorlage `prozess/genehmigungsworkflow.bpmn`, dieselben IDs.
- `src/main/resources/verbuchen-fehlerpfad.bpmn`: Kopie der Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` für den Bonus. Die CI prüft, dass beide Kopien byte-gleich zu ihren Vorlagen sind.
- `src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java`: der Happy Path fertig, als Vorbild mit den fünf nummerierten Schritten der Folie. Ablehnung, Nachbesserung und Timer stehen als `TODO Kapitel 12, Schritt 5` mit `@Disabled` darunter, dazu die Hilfsmethoden `antragStarten` und `speicherpunktAnstossen`.
- `src/test/resources/camunda.cfg.xml`: die Engine im Speicher, ohne Job Executor.
- `.mvn/jvm.config`: stellt Maven leiser, übrig bleiben Testbaum, Meldungen und Fehler. Deshalb fehlen die Zeilen BUILD SUCCESS und BUILD FAILURE.

Die Musterlösung liegt unter [`loesung/java-prozesstest/`](../loesung/java-prozesstest/), nur mit den Dateien, die sich unterscheiden: `GenehmigungsworkflowTest.java` mit allen vier Tests und, als Bonus, `FehlerpfadTest.java` für die Variante.

Speicherpunkte: Der Baustein „CIB easyForm“ setzt hinter „Antrag eingereicht“ und hinter „Antrag prüfen“ je einen Speicherpunkt (`camunda:asyncAfter`). Ohne Job Executor stößt der Test beide selbst an (`speicherpunktAnstossen`, darin `execute(job(...))`), den Timer ebenso. Nach dem Abschließen von „Antrag prüfen“ entscheidet das Gateway also erst am Speicherpunkt.

## Übung (Kapitel 12)

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

Fertig seid ihr, wenn Ablehnung und Nachbesserung ein ✔ tragen und `Failures: 0, Errors: 0` dasteht. Mit Timer tragen alle vier Zeilen ein ✔, und es steht `Skipped: 0` da.

## Demo (Kapitel 10, Trainer)

Die Live-Demo zu Kapitel 10 „Wie ein Prozesstest funktioniert“, Folie „Live: grün, geändert, rot“. Kein Übungsteil, niemand tippt mit. Die Demo braucht die Musterlösung: Im Startstand sind Ablehnung, Nachbesserung und Timer übersprungen, Variante B bliebe dann grün.

### Vorbereitung

1. Musterlösung übernehmen, im Repo-Root:
   ```bash
   # bash, zsh, Git Bash
   cp -R loesung/java-prozesstest/src java-prozesstest/
   ```
   ```powershell
   # PowerShell
   Copy-Item -Path loesung\java-prozesstest\src -Destination java-prozesstest -Recurse -Force
   ```
   `git status` zeigt danach `GenehmigungsworkflowTest.java` als geändert und `FehlerpfadTest.java` als neu.
2. `cd java-prozesstest`, einmal `./mvnw test` laufen lassen und die Demo einmal durchspielen.

Das Modell zeigt ihr in VS Code mit der Erweiterung Miragon BPMN Modeler, daneben das Terminal. Schrift groß, vor jedem Lauf `clear`. Geändert wird nur die Kopie in `src/main/resources/`, die Vorlage unter `prozess/` bleibt unberührt.

### Ablauf

1. **Grün:** `./mvnw test`
   ```
   ── Genehmigungsworkflow - 1.4 s
      ├─ ✔ Happy Path: Antrag genehmigt und verbucht - 0.24 s
      ├─ ✔ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.06 s
      ├─ ✔ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.06 s
      └─ ✔ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.03 s
   ── Fehlerpfad (Variante) - 0.07 s
      ├─ ✔ Buchung abgelehnt: BPMN-Fehler BUCHUNG_ABGELEHNT, danach wartet „Buchung klären“ - 0.05 s
      └─ ✔ Gegenprobe: verbucht, „Genehmigung mitteilen“, Ende bei „Antrag genehmigt“ - 0.02 s

   Results:

   Tests run: 6, Failures: 0, Errors: 0, Skipped: 0
   ```
   Im Editor nur den Happy-Path-Test `genehmigterAntragWirdVerbucht` zeigen, die Imports einklappen. Seine Kommentare nummerieren die fünf Schritte der Folie. Der Block „Fehlerpfad (Variante)“ ist der Bonus aus Kapitel 12 und testet ein eigenes Modell.
2. **Variante A, ID ändern:** „Antrag prüfen“ anklicken und im Properties-Panel die ID `Task_Pruefen` in `Task_Pruefen2` ändern, speichern. Der Modeler zieht die Verweise mit (Lane, Timer, Sequenzflüsse). Ohne Modeler im Texteditor mit Suchen und Ersetzen, Option „Nur ganzes Wort“. Den Test nicht anfassen. Ergebnis: alle vier Tests des Genehmigungsworkflows rot, denn jeder wartet bei „Antrag prüfen“. Der Fehlerpfad bleibt grün, sein Modell hat sich nicht geändert.
   ```
   ── Genehmigungsworkflow - 1.2 s
      ├─ ✘ Happy Path: Antrag genehmigt und verbucht - 0.16 s
      ├─ ✘ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.04 s
      ├─ ✘ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.04 s
      └─ ✘ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.04 s
   ── Fehlerpfad (Variante) - 0.09 s
      ├─ ✔ Buchung abgelehnt: BPMN-Fehler BUCHUNG_ABGELEHNT, danach wartet „Buchung klären“ - 0.06 s
      └─ ✔ Gegenprobe: verbucht, „Genehmigung mitteilen“, Ende bei „Antrag genehmigt“ - 0.03 s
   ```
   Die Meldungen stehen unter dem Baum im Block „Results“, je Test eine Zeile, sortiert nach Methodenname. Der Happy Path heißt dort `genehmigterAntragWirdVerbucht` und steht an zweiter Stelle:
   ```
   GenehmigungsworkflowTest.genehmigterAntragWirdVerbucht:54 Expecting ProcessInstance {id='7', processDefinitionId='Process_Genehmigung:1:3', businessKey='Antrag-1'} to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Pruefen2].
   ```
   Darunter steht `Tests run: 6, Failures: 4, Errors: 0, Skipped: 0`.
3. **Variante B, „abgelehnt“ löschen:** Erst zurücksetzen (Schritt 5), dann den Pfeil „abgelehnt“ (`Flow_Abgelehnt`) anklicken und löschen, speichern. Im Texteditor gehören dazu auch `<bpmn:outgoing>` am Gateway, `<bpmn:incoming>` an `Task_Ablehnen` und die Kante `Flow_Abgelehnt_di`. Ergebnis: Nur der Ablehnungs-Test ist rot, Maven zählt ihn als „Error“, nicht als „Failure“. Das Abschließen der Aufgabe gelingt, die Engine scheitert erst am Speicherpunkt dahinter, die Zeile nennt deshalb `speicherpunktAnstossen`:
   ```
   GenehmigungsworkflowTest.abgelehnterAntragWirdMitgeteilt:83->speicherpunktAnstossen:158 » ProcessEngine ENGINE-02004 No outgoing sequence flow for the element with id 'Gateway_Entscheidung' could be selected for continuing the process.
   ```
   Darunter steht `Tests run: 6, Failures: 0, Errors: 1, Skipped: 0`.
4. **Gegenprobe:** Erst zurücksetzen, dann nur die Beschriftung „Antrag prüfen“ ändern, etwa in „Antrag fachlich prüfen“. Alle sechs Tests bleiben grün, der Test hängt an der ID.
5. **Zurücksetzen**, nach jeder Variante, im Ordner `java-prozesstest/`:
   ```bash
   git restore src/main/resources/genehmigungsworkflow.bpmn
   ```
   **Nach der Demo** auch die Musterlösung entfernen, im Repo-Root, in bash und PowerShell gleich:
   ```bash
   git restore java-prozesstest
   git clean -fd java-prozesstest
   ```
   `git restore` holt Modell und `GenehmigungsworkflowTest.java` auf den Startstand zurück, `git clean` löscht `FehlerpfadTest.java` und alle anderen neuen Dateien in `java-prozesstest/`. Danach zeigt `git status` keine Änderung mehr, und `./mvnw test` meldet wieder 1 bestanden, 3 übersprungen. Bleibt eine geänderte Modellkopie im Repo, wird die CI rot, sie vergleicht die Kopie mit der Vorlage.

Bei Rot folgt unter „Results“ noch der Fehlertext von Maven. Baum und Meldungen stehen darüber, notfalls etwas hochscrollen.

Ändert sich eine Vorlage unter `prozess/`, kopiert ihr sie neu nach `src/main/resources/`:

```bash
cp prozess/genehmigungsworkflow.bpmn prozess/varianten/verbuchen-fehlerpfad.bpmn java-prozesstest/src/main/resources/
```
