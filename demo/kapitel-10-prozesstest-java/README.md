# Demo Kapitel 10: Prozesstest in Java

Die Live-Demo zu Kapitel 10 „Wie ein Prozesstest funktioniert“: grün, geändert, rot. Kein Übungsteil, niemand tippt mit. Der Entwickler-Track schreibt seine Prozesstests in Kapitel 12 in C#.

Der Test läuft mit dem Java-Stack aus dem CIB seven Training: `cibseven-bpm-junit5` startet die Engine mit H2 im Speicher, `cibseven-bpm-assert` liefert die Prüfungen. Kein Server, keine Oberfläche, kein Docker.

## Voraussetzung und Befehle

JDK 21 oder neuer (`java -version`). Maven braucht ihr nicht, der Maven Wrapper lädt es beim ersten Lauf.

```bash
cd demo/kapitel-10-prozesstest-java
./mvnw test          # Windows: .\mvnw.cmd test
./mvnw -o test       # ab dem zweiten Lauf, ohne Netz (Windows: .\mvnw.cmd -o test)
```

Der erste Lauf lädt Maven und die Bibliotheken, rund 55 MB. Danach dauert ein Lauf wenige Sekunden.

Unter Windows können ✔, ✘ und „“ in der Konsole als Fragezeichen erscheinen. Dann vorher `chcp 65001` ausführen. Hilft das nicht, schaltet `.\mvnw.cmd test "-Dbaum.theme=ASCII"` den Baum auf `+--`, `[OK]` und `[XX]` um.

## Was drin ist

- `src/main/resources/genehmigungsworkflow.bpmn`: Kopie der Vorlage `prozess/genehmigungsworkflow.bpmn`, dieselben IDs. Die CI prüft, dass sie byte-gleich ist.
- `src/test/java/.../GenehmigungsworkflowTest.java`: vier Testfälle, Happy Path (die fünf Schritte der Folie), Ablehnung, Nachbesserung, Timer.
- `src/test/resources/camunda.cfg.xml`: die Engine im Speicher, ohne Job Executor.
- `.mvn/jvm.config`: stellt Maven leiser, übrig bleiben Testbaum, Meldungen und Fehler. Deshalb fehlen die Zeilen BUILD SUCCESS und BUILD FAILURE.

Speicherpunkte: Der Baustein „CIB easyForm“ setzt hinter „Antrag eingereicht“ und hinter „Antrag prüfen“ je einen Speicherpunkt (`camunda:asyncAfter`). Ohne Job Executor stößt der Test beide selbst an (`speicherpunktAnstossen`, darin `execute(job(...))`), den Timer ebenso. Nach dem Abschließen von „Antrag prüfen“ entscheidet das Gateway also erst am Speicherpunkt.

## Ablauf der Demo

Vorher einmal `./mvnw test` laufen lassen und die Demo einmal durchspielen. Das Modell zeigt ihr in VS Code mit der Erweiterung Miragon BPMN Modeler, daneben das Terminal. Schrift groß, vor jedem Lauf `clear`. Geändert wird nur die Kopie in `src/main/resources/`, die Modelle der Teilnehmenden bleiben unberührt.

1. **Grün:** `./mvnw test`
   ```
   ── Genehmigungsworkflow - 1.2 s
      ├─ ✔ Happy Path: Antrag genehmigt und verbucht - 0.23 s
      ├─ ✔ Ablehnung: „Ablehnung mitteilen“, nie verbuchen - 0.06 s
      ├─ ✔ Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“ - 0.05 s
      └─ ✔ Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen - 0.03 s
   ```
   Im Editor nur den Happy-Path-Test `genehmigterAntragWirdVerbucht` zeigen, die Imports einklappen. Seine Kommentare nummerieren die fünf Schritte der Folie.
2. **Variante A, ID ändern:** „Antrag prüfen“ anklicken und im Properties-Panel die ID `Task_Pruefen` in `Task_Pruefen2` ändern, speichern. Der Modeler zieht die Verweise mit (Lane, Timer, Sequenzflüsse). Ohne Modeler im Texteditor mit Suchen und Ersetzen, Option „Nur ganzes Wort“. Den Test nicht anfassen. Ergebnis: alle vier Tests rot, denn jeder wartet bei „Antrag prüfen“. Die Meldungen stehen unter dem Baum im Block „Results“, je Test eine Zeile, sortiert nach Methodenname. Der Happy Path heißt dort `genehmigterAntragWirdVerbucht` und steht an zweiter Stelle:
   ```
   GenehmigungsworkflowTest.genehmigterAntragWirdVerbucht:54 Expecting ProcessInstance {id='7', processDefinitionId='Process_Genehmigung:1:3', businessKey='Antrag-1'} to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Pruefen2].
   ```
3. **Variante B, „abgelehnt“ löschen:** Den Pfeil „abgelehnt“ (`Flow_Abgelehnt`) anklicken und löschen, speichern. Im Texteditor gehören dazu auch `<bpmn:outgoing>` am Gateway, `<bpmn:incoming>` an `Task_Ablehnen` und die Kante `Flow_Abgelehnt_di`. Ergebnis: Nur der Ablehnungs-Test ist rot, Maven zählt ihn als „Error“, nicht als „Failure“. Das Abschließen der Aufgabe gelingt, die Engine scheitert erst am Speicherpunkt dahinter, die Zeile nennt deshalb `speicherpunktAnstossen`:
   ```
   GenehmigungsworkflowTest.abgelehnterAntragWirdMitgeteilt:83->speicherpunktAnstossen:158 » ProcessEngine ENGINE-02004 No outgoing sequence flow for the element with id 'Gateway_Entscheidung' could be selected for continuing the process.
   ```
4. **Gegenprobe:** Nur die Beschriftung „Antrag prüfen“ ändern. Alle Tests bleiben grün, der Test hängt an der ID.
5. **Zurücksetzen**, nach jeder Variante und am Ende:
   ```bash
   git restore src/main/resources/genehmigungsworkflow.bpmn
   ```
   Sonst wird die CI rot, sie vergleicht die Kopie mit der Vorlage.

Bei Rot folgt unter „Results“ noch der Fehlertext von Maven. Baum und Meldungen stehen darüber, notfalls etwas hochscrollen.

Ändert sich die Vorlage `prozess/genehmigungsworkflow.bpmn`, kopiert ihr sie neu nach `src/main/resources/`.
