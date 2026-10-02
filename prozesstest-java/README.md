# Prozesstest in Java

Ein Projekt für zwei Kapitel: In Kapitel 12 schreibt ihr hier eure Prozesstests in Java, zusätzlich zu denen in C#. In Kapitel 10 zeigt der Trainer an der Musterlösung dieses Projekts, wie ein Prozesstest funktioniert.

Der Test läuft mit dem Java-Stack aus dem CIB seven Training: `cibseven-bpm-junit5` startet die Engine mit H2 im Speicher, `cibseven-bpm-assert` liefert die Prüfungen. Kein Server, keine Oberfläche, kein Docker. Stack und Worker braucht dieser Test nicht, er läuft auch, wenn beide aus sind.

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

Der erste Lauf lädt Maven und die Bibliotheken, rund 55 MB. Lasst ihn deshalb einmal vor der Schulung laufen. Danach dauert ein Lauf wenige Sekunden.

Unter Windows können ✔, ✘, ↷ und „“ in der Konsole als Fragezeichen erscheinen. Dann vorher `chcp 65001` ausführen. Hilft das nicht, schaltet `.\mvnw.cmd test "-Dbaum.theme=ASCII"` den Baum auf `+--`, `[OK]`, `[XX]` und, für übersprungen, `[??]` um.

## Hinter einem Proxy

Der erste Lauf lädt alles von `repo.maven.apache.org`. Scheitert er mit `PKIX path building failed` oder `Could not transfer artifact`, sitzt ihr hinter einem Proxy, oder eure IT prüft verschlüsselte Verbindungen. .NET und Docker laufen dann oft trotzdem: Maven liest den Proxy nicht aus den Umgebungsvariablen, und Java prüft Zertifikate gegen eine eigene Liste, nicht gegen die des Betriebssystems.

- **Proxy:** Tragt ihn in `~/.m2/settings.xml` ein, unter Windows `%USERPROFILE%\.m2\settings.xml`, so wie in [Configuring a proxy](https://maven.apache.org/guides/mini/guide-proxies.html) beschrieben. Adresse und Port kennt eure IT.
- **`PKIX path building failed`:** Java kennt das Zertifikat nicht, mit dem eure IT die Verbindungen prüft. Fragt die IT nach dem Zertifikat und danach, wie es ins JDK kommt.
- **Am schnellsten:** den ersten Lauf außerhalb des Behördennetzes starten, etwa über einen Hotspot. Danach liegt alles unter `~/.m2/`, und `./mvnw -o test` läuft ohne Netz.

Klappt es bis zur Schulung nicht, schreibt ihr den Java-Teil zu zweit am Rechner eurer Nachbarn.

## Was drin ist

- `src/main/resources/genehmigungsworkflow.bpmn`: Kopie der Vorlage `prozess/genehmigungsworkflow.bpmn`, dieselben IDs.
- `src/main/resources/verbuchen-fehlerpfad.bpmn`: Kopie der Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` für den Bonus. Die CI prüft, dass beide Kopien byte-gleich zu ihren Vorlagen sind.
- `src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java`: der Happy Path fertig, als Vorbild mit den fünf nummerierten Schritten der Folie. Ablehnung, Nachbesserung und Timer stehen als `TODO Kapitel 12, Schritt 5` mit `@Disabled` darunter, dazu die Hilfsmethoden `antragStarten` und `speicherpunktAnstossen`.
- `src/test/resources/camunda.cfg.xml`: die Engine im Speicher, ohne Job Executor.
- `.mvn/jvm.config`: stellt Maven leiser, übrig bleiben Testbaum, Meldungen und Fehler. Deshalb fehlen die Zeilen BUILD SUCCESS und BUILD FAILURE.

Die Musterlösung liegt als vollständiges Projekt unter [`loesung/prozesstest-java/`](../loesung/prozesstest-java/), mit Maven Wrapper, `pom.xml` und denselben Modellkopien. Sie unterscheidet sich nur in den Tests: `GenehmigungsworkflowTest.java` mit allen vier Tests und, als Bonus, `FehlerpfadTest.java` für die Variante. Ihr startet sie im Repo-Root mit `cd loesung/prozesstest-java` und `./mvnw test` (PowerShell: `.\mvnw.cmd test`), nichts wird kopiert. Vergleichen könnt ihr im Repo-Root mit `git diff --no-index prozesstest-java/src/test/java loesung/prozesstest-java/src/test/java`.

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

Maven setzt `[INFO]` vor jede Zeile, die Blöcke hier lassen das weg. Sind Tests übersprungen, steht `[WARNING]` vor `Tests run`. Das ist kein Fehler.

Fertig seid ihr, wenn Ablehnung und Nachbesserung ein ✔ tragen und `Failures: 0, Errors: 0` dasteht. Mit Timer tragen alle vier Zeilen ein ✔, und es steht `Skipped: 0` da.

## Demo (Kapitel 10, Trainer)

Die Live-Demo zu Kapitel 10 „Wie ein Prozesstest funktioniert“, Folie „Live: grün, geändert, rot“. Kein Übungsteil, niemand tippt mit. Die Demo läuft direkt in der Musterlösung, im Ordner `loesung/prozesstest-java/`. Dort liegt der fertige Test schon: Im Startstand sind Ablehnung, Nachbesserung und Timer übersprungen, Variante B bliebe dann grün. Der Startstand `prozesstest-java/` bleibt für die Übung unberührt.

In der Musterlösung liegt auch der Bonus `FehlerpfadTest.java`. Der Zusatz `-Dtest=GenehmigungsworkflowTest` lässt ihn weg, so laufen genau die vier Tests des Genehmigungsworkflows. Ohne den Zusatz laufen seine zwei Tests mit und bleiben in jeder Variante grün, Maven zählt dann 6 statt 4 Tests.

### Vorbereitung

1. Im Repo-Root `cd loesung/prozesstest-java`. Alle Läufe der Demo starten in diesem Ordner. Vor der Demo zeigt `git status` keine Änderung.
2. Einmal die Tests laufen lassen und die Demo einmal durchspielen:
   ```bash
   # bash, zsh, Git Bash
   ./mvnw test -Dtest=GenehmigungsworkflowTest
   ```
   ```powershell
   # PowerShell
   .\mvnw.cmd test "-Dtest=GenehmigungsworkflowTest"
   ```

Das Modell zeigt ihr in VS Code mit der Erweiterung Miragon BPMN Modeler, daneben das Terminal. Schrift groß, vor jedem Lauf `clear`. Geändert wird nur die Kopie in `loesung/prozesstest-java/src/main/resources/`, die Vorlage unter `prozess/` bleibt unberührt.

### Ablauf

1. **Grün:** `./mvnw test -Dtest=GenehmigungsworkflowTest`, jeder weitere Lauf nutzt denselben Befehl.
   ```
   ── Genehmigungsworkflow - 1.2 s
      ├─ ✔ Happy Path: Antrag genehmigt und verbucht - 0.20 s
      ├─ ✔ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.06 s
      ├─ ✔ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.05 s
      └─ ✔ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.03 s

   Results:

   Tests run: 4, Failures: 0, Errors: 0, Skipped: 0
   ```
   Im Editor nur den Happy-Path-Test `genehmigterAntragWirdVerbucht` zeigen, die Imports einklappen. Seine Kommentare nummerieren die fünf Schritte der Folie.
2. **Variante A, ID ändern:** „Antrag prüfen“ anklicken und im Properties-Panel die ID `Task_Pruefen` in `Task_Pruefen2` ändern, speichern. Der Modeler zieht die Verweise mit (Timer, Sequenzflüsse). Ohne Modeler im Texteditor mit Suchen und Ersetzen, Option „Nur ganzes Wort“. Den Test nicht anfassen. Ergebnis: alle vier Tests rot, denn jeder wartet bei „Antrag prüfen“.
   ```
   ── Genehmigungsworkflow - 1.1 s
      ├─ ✘ Happy Path: Antrag genehmigt und verbucht - 0.16 s
      ├─ ✘ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.04 s
      ├─ ✘ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.04 s
      └─ ✘ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.03 s
   ```
   Die Meldungen stehen unter dem Baum im Block „Results“, je Test eine Zeile, sortiert nach Methodenname. Der Happy Path heißt dort `genehmigterAntragWirdVerbucht` und steht an zweiter Stelle:
   ```
   GenehmigungsworkflowTest.genehmigterAntragWirdVerbucht:54 Expecting ProcessInstance {id='7', processDefinitionId='Process_Genehmigung:1:3', businessKey='Antrag-1'} to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Pruefen2].
   ```
   Darunter steht `Tests run: 4, Failures: 4, Errors: 0, Skipped: 0`.
3. **Variante B, „abgelehnt“ löschen:** Erst das Modell zurücksetzen (Schritt 5), dann den Pfeil „abgelehnt“ (`Flow_Abgelehnt`) anklicken und löschen, speichern. Im Texteditor gehören dazu auch `<bpmn:outgoing>` am Gateway, `<bpmn:incoming>` an `Task_Ablehnen` und die Kante `Flow_Abgelehnt_di`. Ergebnis: Nur der Ablehnungs-Test ist rot, Maven zählt ihn als „Error“, nicht als „Failure“. Das Abschließen der Aufgabe gelingt, die Engine scheitert erst am Speicherpunkt dahinter, die Zeile nennt deshalb `speicherpunktAnstossen`:
   ```
   GenehmigungsworkflowTest.abgelehnterAntragWirdMitgeteilt:83->speicherpunktAnstossen:160 » ProcessEngine ENGINE-02004 No outgoing sequence flow for the element with id 'Gateway_Entscheidung' could be selected for continuing the process.
   ```
   Darunter steht `Tests run: 4, Failures: 0, Errors: 1, Skipped: 0`.
4. **Gegenprobe:** Erst das Modell zurücksetzen, dann nur die Beschriftung „Antrag prüfen“ ändern, etwa in „Antrag fachlich prüfen“. Alle vier Tests bleiben grün, der Test hängt an der ID.
5. **Modell zurücksetzen**, nach jeder Variante, im Ordner `loesung/prozesstest-java/`, in bash und PowerShell gleich:
   ```bash
   git restore .
   ```
   Im Repo-Root heißt derselbe Befehl `git restore loesung/prozesstest-java`. Er setzt das Modell zurück. Der fertige Test gehört in der Musterlösung zum Repo-Stand und bleibt.
6. **Nach der Demo** noch einmal zurücksetzen und prüfen: `git status` zeigt keine Änderung mehr, und der Lauf aus Schritt 1 ist wieder grün. Bleibt eine geänderte Modellkopie im Repo, wird die CI rot, sie vergleicht die Musterlösung mit dem Startstand (`.github/scripts/loesung-abgleich.sh`). Den Startstand `prozesstest-java/` hat die Demo nicht angefasst, dort meldet `./mvnw test` weiter 1 bestanden, 3 übersprungen.

Maven setzt vor jede Zeile `[INFO]`, vor die Meldungen und vor `Tests run` bei Rot `[ERROR]`, die Blöcke hier lassen das weg. Bei Rot folgt unter „Results“ noch der Fehlertext von Maven. Baum und Meldungen stehen darüber, notfalls etwas hochscrollen.

Ändert sich eine Vorlage unter `prozess/`, kopiert ihr sie im Repo-Root neu nach `src/main/resources/`, im Startstand und in der Musterlösung:

```bash
cp prozess/genehmigungsworkflow.bpmn prozess/varianten/verbuchen-fehlerpfad.bpmn prozesstest-java/src/main/resources/
cp prozess/genehmigungsworkflow.bpmn prozess/varianten/verbuchen-fehlerpfad.bpmn loesung/prozesstest-java/src/main/resources/
```
