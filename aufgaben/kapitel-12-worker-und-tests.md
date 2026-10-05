# Kapitel 12 · Übung 9: Worker und Tests

Tag 2, Entwickler-Track, Kapitel 12 „Worker und Tests“.

Euer Worker aus Übung 8 holt den Task schon, verbucht aber nichts. Am Ende verbucht er jede Genehmigung, ein Unit-Test und Prozesstests in C# und in Java belegen das.

## Vorab

- Der Prozesstest in C# nimmt den Prozess-Key als `_engine.ProzessKey` aus `appsettings.json`: `Process_Genehmigung` mit dem Projekt-ZIP aus dem Repo, euer eigener Key mit eurem Projekt. Auf der Folie steht an dieser Stelle `"mm-genehmigung"`.
- `betrag` kommt je nach Weg verschieden an: aus dem easyForm als Text, etwa `"1234.5"`, immer mit Punkt, per REST und im Prozesstest als Zahl (`long`). Der Handler muss beides lesen, Schritt 1 zeigt wie.
- Für den Prozesstest in Java (Schritt 5) braucht ihr JDK 21 (`java -version`). Habt ihr im Ordner `prozesstest-java/` noch nie `./mvnw test` laufen lassen (Windows: `.\mvnw.cmd test`) oder seit dem letzten Lauf `git pull` gemacht, startet es gleich zu Beginn in einem zweiten Terminal, mit Netz. Der erste Lauf lädt Maven und die Bibliotheken, rund 65 MB, nach einem `git pull` nur, was dazugekommen ist. Scheitert er mit `PKIX path building failed` oder `Could not transfer artifact`, steht unter [Meldungen aus dem Prozesstest in Java](#meldungen-aus-dem-prozesstest-in-java), was ihr tut.

## Ausgangslage

Aus Übung 8 läuft der Stack, euer Projekt ist importiert, und die Zugangsdaten sind gesetzt. „Genehmigung verbuchen“ ist seit Übung 8 als External Task deployt. Prüft das kurz: In der Weboberfläche zeigt die Kachel „Prozess starten“ euren Prozess, und dieser Befehl zeigt `EngineBenutzer` und `EnginePasswort`:

```bash
cd worker      # nur wenn euer Terminal noch im Repo-Root steht
dotnet user-secrets list --project src/GenehmigungWorker
```

Alle `dotnet`-Befehle dieser Übung laufen wie in Übung 8 im Ordner `worker/`.

Steigt ihr erst jetzt ein, macht zuerst Übung 8 nach der Übungsanleitung eurer Schulung, mindestens bis „Genehmigung verbuchen“ als External Task deployt ist. Was ihr dabei nachschlagt, steht im [Blatt zu Übung 8](kapitel-11-lokales-setup.md).

Im Startstand tragen diese Dateien Kommentare `TODO Kapitel 12, Schritt ...`. Sie bauen, aber die Arbeit darin fehlt:

| Schritt | Datei | Stand |
|---|---|---|
| 1 | `worker/src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs` | `Handle` wirft `NotImplementedException` |
| 2 | `worker/tests/GenehmigungWorker.Tests/BuchungssystemFake.cs` | `Verbuchen` wirft `NotImplementedException` |
| 2 | `worker/tests/GenehmigungWorker.Tests/GenehmigungVerbuchenHandlerTests.cs` | Unit-Test mit `Skip` |
| 3 | `worker/src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` | `Verbuchen` wirft `NotImplementedException` |
| 3 | `worker/src/GenehmigungWorker/Program.cs` | Skeleton-Schleife aus Übung 8 |
| 4 | `worker/tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs` | Prozesstest und Gegenprobe mit `Skip` |
| 5 | `prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java` | Happy Path fertig, Ablehnung, Nachbesserung und Timer mit `@Disabled` |

Fertig vorgegeben sind `Fachsystem/IBuchungssystem.cs` (die Signatur von `Verbuchen`), `ExternalTaskClient.cs` aus Übung 8 und der Test-Helfer `worker/tests/GenehmigungWorker.Tests/EngineHelfer.cs`, im Prozesstest `_engine`. Alle TODOs findet ihr in VS Code mit Strg+Umschalt+F (macOS: Cmd+Umschalt+F) und dem Suchtext `TODO Kapitel 12`.

`dotnet test` im Ordner `worker/` meldet im Startstand 3 übersprungene Tests und keinen Fehler. `./mvnw test` im Ordner `prozesstest-java/` meldet 1 bestandenen und 3 übersprungene Tests: `[WARNING] Tests run: 4, Failures: 0, Errors: 0, Skipped: 3`. Das `[WARNING]` kommt von den übersprungenen Tests und ist kein Fehler.

## Das macht ihr

Die Reihenfolge folgt der Empfehlung aus dem Kapitel: zuerst Handler und Unit-Test, die ohne Engine laufen, dann die Schleife, dann die Prozesstests in C# und in Java, zum Schluss der Lauf über das Formular. Die Folien ab „Der Handler“ sind eure Hilfestellung.

- Schritte 1 bis 3: Handler, Fake und Unit-Test, Simulation und Worker-Schleife
- Schritt 4: Prozesstest in C#
- Schritt 5: Prozesstest in Java, Ablehnung und Nachbesserung. Den Timer schreibt, wer schneller ist
- Schritt 6: End-to-end über das Formular

Hängt ihr hinterher, gelten zwei Regeln:

- Seid ihr zur Halbzeit der Übung noch nicht bei Schritt 4, macht ihr zuerst die Worker-Schleife fertig, Schritt 6 braucht sie. Danach schreibt ihr in C# den Happy Path und in Java nur die Ablehnung. Die Nachbesserung schreibt, wer noch Zeit hat.
- In Schritt 5 lasst ihr zuerst den Timer weg. Vor dem Ende der Übung geht ihr in jedem Fall zu Schritt 6. Was in Java fehlt, holt ihr danach nach, der Test braucht weder Stack noch Worker.

Die Boni sind für alle, die vor der Zeit fertig sind.

### 1. Handler schreiben

Datei `worker/src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs`, Folie „Der Handler“.

`Handle(ExternalTask task)` liest, verbucht und gibt das Ergebnis zurück. Mit der Engine spricht der Handler nicht.

1. Schlüssel für die Idempotenz: `task.BusinessKey`, und wenn es keinen gibt, `task.ProcessInstanceId`.
2. Aus `task.Variables` lesen: `antragsteller` und `begruendung` als `string`, dazu `betrag`. Der kommt aus dem easyForm als Text und per REST als Zahl. Lest beides kulturunabhängig, `using System.Globalization;` steht schon oben in der Datei:
   ```csharp
   var betrag = task.Variables["betrag"] switch
   {
       string text => decimal.Parse(text, CultureInfo.InvariantCulture),
       var zahl => Convert.ToDecimal(zahl, CultureInfo.InvariantCulture),
   };
   ```
   Auf der Folie steht die kurze Form `Convert.ToDecimal(task.Variables["betrag"], CultureInfo.InvariantCulture)`. Sie liest Text und Zahl genauso, der `switch` zeigt nur ausdrücklich, welcher Fall welcher ist. Wichtig ist in beiden `CultureInfo.InvariantCulture`: Ohne sie liest ein Rechner mit deutscher Einstellung den Punkt in `"1234.5"` als Tausendertrennzeichen und verbucht 12.345 Euro statt 1.234,50 Euro.
3. `_buchung.Verbuchen(schluessel, antragsteller, betrag, begruendung)` rufen.
4. `new() { ["buchungsnummer"] = nummer }` zurückgeben.

### 2. Fake und Unit-Test

Dateien `worker/tests/GenehmigungWorker.Tests/BuchungssystemFake.cs` und `GenehmigungVerbuchenHandlerTests.cs`, Folie „Unit-Test für den Handler“.

1. `BuchungssystemFake.Verbuchen` gibt immer `"B-2026-0001"` zurück.
2. Den Test `Verbucht_Genehmigung_und_liefert_Buchungsnummer` schreiben: Task von Hand bauen, Handler mit dem Fake, `Assert.Equal("B-2026-0001", ergebnis["buchungsnummer"])`.
3. Aus `[Fact(Skip = "...")]` wird `[Fact]`. Solange `Skip` dasteht, führt xUnit den Test nicht aus.

```bash
dotnet test --filter "Kategorie!=Prozesstest"
```

Erwartet: 1 Test bestanden, keiner fehlgeschlagen. Dieser Aufruf braucht weder Engine noch Zugangsdaten.

### 3. Simulation und Worker-Schleife

Dateien `worker/src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` und `worker/src/GenehmigungWorker/Program.cs`, Folie „Die Worker-Schleife“.

1. `BuchungssystemSimulation.Verbuchen` vergibt fortlaufende Nummern, `B-2026-0001`, dann `B-2026-0002` und so weiter, und schreibt jede Buchung ins Log (Schlüssel, `antragsteller`, `betrag`, `begruendung`, Nummer). Ein Zähler im Speicher reicht für den Anfang. Die Datei kommt im Bonus.
2. In `Program.cs` den Handler anlegen: `new GenehmigungVerbuchenHandler(new BuchungssystemSimulation())`, dazu oben `using GenehmigungWorker.Fachsystem;` und `using GenehmigungWorker.Handlers;`.
3. In der Schleife den `try`/`catch` von der Folie einsetzen: Erfolg meldet `CompleteAsync`, Fehler `FailureAsync` mit `task.Retries is int r ? r - 1 : 3` und `TimeSpan.FromMinutes(5)`.

Startet den Worker:

```bash
dotnet run --project src/GenehmigungWorker
```

Warten noch Anträge aus Übung 8 am Service Task, verbucht er sie gleich beim Start. Verbucht werden nur Anträge, die nach dem Umbau gestartet wurden. Ältere warten als Aufgabe „Genehmigung verbuchen“ in der Aufgabenliste. Im Log steht eure Buchung, und der Task taucht nicht mehr alle 30 Sekunden wieder auf.

### 4. Prozesstest in C#

Datei `worker/tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs`, Folien „Prozesstest in fünf Schritten“ und „Prozesstest in C#“.

1. Stoppt euren Worker mit Strg+C. Er hört auf dasselbe Topic und würde dem Test den Task wegschnappen.
2. `Genehmigter_Antrag_wird_verbucht` schreiben: Starten, Warten (`Task_Pruefen`), Entscheiden (`genehmigt`), Verbuchen (Handler mit dem Fake), Beenden (`COMPLETED`, `buchungsnummer`). Nehmt `_engine.ProzessKey` und `_engine.Topic` statt der Texte von der Folie. Der Code steht als Kommentar in der Datei.
3. Die Methode wird `public async Task Genehmigter_Antrag_wird_verbucht()`, die Zeilen `Assert.Fail(...)` und `return Task.CompletedTask;` fallen weg, `Skip` auch.

```bash
dotnet test
```

Erwartet: Unit-Test und Prozesstest bestanden, keiner fehlgeschlagen. Die Gegenprobe bleibt übersprungen, bis ihr sie schreibt. Nur die Prozesstests startet `dotnet test --filter "Kategorie=Prozesstest"`.

Der Prozesstest braucht, was auch der Worker braucht: laufenden Stack, bereitgestelltes Modell, Zugangsdaten. Er liest dieselbe Konfiguration, auch dieselben User Secrets. Jeder Lauf startet eine eigene Instanz mit Business Key `prozesstest-...` und räumt am Ende auf.

### 5. Prozesstest in Java

Ordner `prozesstest-java/`, Datei `src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java`, Folie „Prozesstest in Java“, dazu aus Kapitel 10 „Ein Testfall in fünf Schritten“ und „Was ein Prozesstest prüft“.

Der Test startet die Engine selbst, im Speicher, und braucht weder Stack noch Worker. Der Happy Path ist fertig, ihr schreibt Ablehnung und Nachbesserung, wer schneller ist, auch den Timer. Befehle für Windows, die Ausgabe im Startstand und warum der Test die Speicherpunkte selbst anstößt, stehen in der [README des Projekts](../prozesstest-java/README.md).

```bash
cd ../prozesstest-java     # aus worker/, aus dem Repo-Root: cd prozesstest-java
./mvnw test                # Windows PowerShell: .\mvnw.cmd test
```

IDs schreibt ihr nicht als Text. Bei jedem Lauf erzeugt bpmn-to-code aus dem Modell die Klasse `ProcessGenehmigungProcessApi`, darin jede ID als Konstante: aus `Task_Ablehnen` wird `TASK_ABLEHNEN`, aus `End_Abgelehnt` wird `END_ABGELEHNT`. Alle sind schon importiert. Die Prüfungen erwarten die ID als Text, deshalb `TASK_ABLEHNEN.getValue()`. Nur `speicherpunktAnstossen` nimmt die Konstante selbst, etwa `speicherpunktAnstossen(antrag, TASK_PRUEFEN)`. Woher die Klasse kommt und wo sie liegt, steht in der README unter [IDs aus dem Modell](../prozesstest-java/README.md#ids-aus-dem-modell).

1. Lest den Happy Path `genehmigterAntragWirdVerbucht`. Seine Kommentare nummerieren die fünf Schritte der Folie: Starten, Warten, Entscheiden, Verbuchen, Beenden. Jede Zeile mit `assertThat` ist eine Prüfung.
2. **Ablehnung**, `abgelehnterAntragWirdMitgeteilt`: starten, Speicherpunkt nach dem Start anstoßen, „Antrag prüfen“ mit `entscheidung` gleich `abgelehnt` abschließen, Speicherpunkt nach „Antrag prüfen“ anstoßen. Jetzt wartet die Instanz genau bei `TASK_ABLEHNEN`: „Ablehnung mitteilen“ ist eine Aufgabe. Schließt sie mit `complete(task())` ab. Erst dann prüfen: Instanz beendet, `TASK_ABLEHNEN` und `END_ABGELEHNT` durchlaufen, `TASK_VERBUCHEN` und `END_GENEHMIGT` nicht, `buchungsnummer` fehlt.
3. **Nachbesserung**, `nachbesserungFuehrtZurueckZurPruefung`: genauso, aber mit `nachbessern`. Jetzt wartet `TASK_NACHBESSERN`, zugewiesen an `anna`. Diese Aufgabe schließt ihr ohne Variablen ab. Danach stoßt ihr den Speicherpunkt nach „Antrag nachbessern“ an, `speicherpunktAnstossen(antrag, TASK_NACHBESSERN)`: Seit Übung 7 hängt dort ein easyForm, und der Baustein setzt einen Speicherpunkt hinter die Aufgabe. Erst dann wartet die Instanz wieder bei `TASK_PRUEFEN`, und die Aufgabe liegt wieder bei der Gruppe `genehmiger`.
4. **Timer**, `timerSendetErinnerung`, für alle, die schneller sind: Den Timer-Job `BOUNDARY_TIMER` holen, seine Fälligkeit prüfen (in drei Minuten, auf zehn Sekunden genau: „3 Tage ohne Entscheidung“ steht im Modell fürs Training auf `PT3M`) und ihn ausführen, statt zu warten. Danach wartet die Instanz an zwei Stellen, `isWaitingAtExactly(TASK_PRUEFEN.getValue(), TASK_ERINNERN.getValue())`: Der Timer unterbricht nicht, „Erinnerung senden“ kommt als zweite Aufgabe dazu. Schließt sie mit `complete(task(TASK_ERINNERN.getValue(), antrag))` ab. Danach wartet die Instanz wieder genau bei `TASK_PRUEFEN`, `TASK_ERINNERN` und `END_ERINNERT` sind durchlaufen.
5. Ist ein Test fertig, löscht ihr die Zeile `@Disabled(...)` über ihm und startet `./mvnw test`. Löscht sie erst, wenn der Test etwas prüft: Eine leere Methode ohne `@Disabled` läuft grün durch und belegt nichts.

**Die Aufrufe**, alle schon importiert. Die Kommentare in jeder TODO-Methode sagen, welche ihr braucht.

| Aufruf | Was er tut |
|---|---|
| `TASK_PRUEFEN` und `TASK_PRUEFEN.getValue()` | die ID `Task_Pruefen` aus dem Modell, als Konstante und als Text. Mit `Elements.*` sind alle IDs des Modells importiert |
| `ProcessInstance antrag = antragStarten();` | Hilfsmethode am Ende der Klasse: startet `Process_Genehmigung` (Konstante `PROCESS_ID`) wie das Startformular, `antragsteller` ist `anna` (Konstante `ANTRAGSTELLER`) |
| `speicherpunktAnstossen(antrag, START_EVENT_ANTRAG)` | Hilfsmethode: prüft, dass die Instanz am Speicherpunkt hinter diesem Element steht, und führt ihn aus. Nach dem Start mit `START_EVENT_ANTRAG`, nach „Antrag prüfen“ mit `TASK_PRUEFEN` (erst dann entscheidet das Gateway), nach „Antrag nachbessern“ mit `TASK_NACHBESSERN`, jeweils ohne `.getValue()` |
| `assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue())` | Wartezustand: Die Instanz wartet genau dort und nirgends sonst. Mit zwei IDs prüft `isWaitingAtExactly(TASK_PRUEFEN.getValue(), TASK_ERINNERN.getValue())` beide Stellen zugleich |
| `complete(task(), withVariables("entscheidung", "abgelehnt"))` | schließt die offene Aufgabe ab wie die genehmigende Stelle, `complete(task())` ohne Variablen. `task()` geht nur, solange genau eine Aufgabe offen ist. Nach dem Timer `complete(task(TASK_ERINNERN.getValue(), antrag))` |
| `assertThat(antrag).task().isAssignedTo(ANTRAGSTELLER)` | prüft, wem die offene Aufgabe gehört, `hasCandidateGroup("genehmiger")` die Gruppe |
| `assertThat(antrag).isEnded().hasPassed(...).hasNotPassed(...)` | Pfad: beendet, durchlaufen, nicht durchlaufen, je mit einer oder mehreren IDs, jede mit `.getValue()` |
| `assertThat(antrag).variables().doesNotContainKey("buchungsnummer")` | Variablen: eine, die fehlen muss. `containsEntry("entscheidung", "abgelehnt")` prüft einen Wert |
| `Job timer = job(BOUNDARY_TIMER.getValue(), antrag);` und `execute(timer);` | holt den Timer-Job und führt ihn aus, `timer.getDuedate()` ist seine Fälligkeit. Das Datum prüft ihr mit `Assertions.assertThat(...)`, wie im Kommentar |

Nehmt immer die Konstanten mit den IDs aus dem Modell, etwa `TASK_ABLEHNEN.getValue()`, nie die Beschriftung „Ablehnung mitteilen“ und nie die ID als Text. Ändert sich dann eine ID im Modell, meldet schon das Übersetzen jede Stelle im Test.

Fertig seid ihr, wenn der Baum viermal ✔ zeigt und darunter `Tests run: 4, Failures: 0, Errors: 0, Skipped: 0` steht, ohne Timer `Skipped: 1`. Maven setzt `[INFO]` davor, mit übersprungenen Tests `[WARNING]`, beides ist kein Fehler.

Wird ein Test rot, steht unter dem Baum im Block „Results“ je Test eine Zeile mit Klasse, Methode, Zeilennummer und Meldung. Was die häufigen Meldungen bedeuten, steht unter [Meldungen aus dem Prozesstest in Java](#meldungen-aus-dem-prozesstest-in-java).

#### Bonus: Fehlerpfad in Java

Für alle, die schneller fertig sind. Ihr testet die Variante mit dem fachlichen Fehler, `verbuchen-fehlerpfad.bpmn`. Sie liegt schon in `src/main/resources/`, als Kopie von `prozess/varianten/verbuchen-fehlerpfad.bpmn`, Process ID `Process_VerbuchenFehlerpfad`. Was die Variante tut, steht im [Bonus: Fachlicher Fehler](#bonus-fachlicher-fehler). Den C#-Bonus braucht ihr dafür nicht, der Test spielt den Worker selbst.

1. Legt neben `GenehmigungsworkflowTest.java` die Klasse `FehlerpfadTest.java` an, mit denselben Annotationen, aber `@Deployment(resources = "verbuchen-fehlerpfad.bpmn")`. Die IDs der Variante stehen in einer eigenen Klasse, die bpmn-to-code aus `verbuchen-fehlerpfad.bpmn` erzeugt: `ProcessVerbuchenFehlerpfadProcessApi` im Paket `io.miragon.schulung.genehmigung.api.fehlerpfad`, mit Process ID, IDs, Topic und Fehlercode.
2. **Starten:** `runtimeService().startProcessInstanceByKey(PROCESS_ID.getValue(), withVariables("antragsteller", "anna", "betrag", 60000, "begruendung", "Neue Serverhardware"))`. Die Variante hat keinen Speicherpunkt, die Instanz wartet sofort bei `TASK_VERBUCHEN`.
3. **Holen wie der Worker:** `List<LockedExternalTask> tasks = fetchAndLock(GENEHMIGUNG_VERBUCHEN, "prozesstest", 1);` Meldet der Test später `IndexOutOfBounds` mit `Index 0 out of bounds for length 0` bei `tasks.get(0)`, hat `fetchAndLock` nichts geholt. Prüft das Topic: `GENEHMIGUNG_VERBUCHEN` aus `ServiceTasks` steht für `genehmigung-verbuchen`. Die Musterlösung prüft deshalb vorher mit `hasSize(1)`.
4. **Ablehnen wie der Worker:** `externalTaskService().handleBpmnError(tasks.get(0).getId(), "prozesstest", BUCHUNG_ABGELEHNT.getCode(), "Budget der Kostenstelle reicht nicht")`. `BUCHUNG_ABGELEHNT` aus `Errors` trägt den errorCode des Modells. Antworten darf nur, wer den Task gesperrt hat, deshalb zuerst `fetchAndLock`.
5. **Prüfen:** Die Instanz wartet genau bei `TASK_BUCHUNG_KLAEREN` mit der Kandidatengruppe `genehmiger`, `errorCode` ist `BUCHUNG_ABGELEHNT.getCode()`, `errorMessage` euer Grund, `TASK_GENEHMIGUNG_MITTEILEN` ist nicht durchlaufen.
6. **Gegenprobe** als zweiter Test: mit `betrag` 1200 starten, den geholten Task mit `complete(tasks.get(0), withVariables("buchungsnummer", "B-2026-0001"))` abschließen. Dann ist die Instanz beendet, `TASK_GENEHMIGUNG_MITTEILEN` und `END_GENEHMIGT` sind durchlaufen, `TASK_BUCHUNG_KLAEREN` nicht, und `genehmigungMitgeteilt` ist `true`.

Neu zu importieren sind `fetchAndLock` und `externalTaskService`, statisch aus `BpmnAwareTests` wie die anderen, dazu `java.util.List` und `org.cibseven.bpm.engine.externaltask.LockedExternalTask`. Die Konstanten importiert ihr statisch aus `io.miragon.schulung.genehmigung.api.fehlerpfad.ProcessVerbuchenFehlerpfadProcessApi`: `Elements.*`, `PROCESS_ID`, `ServiceTasks.GENEHMIGUNG_VERBUCHEN` und `Errors.BUCHUNG_ABGELEHNT`. Kopiert ihr die Imports aus `GenehmigungsworkflowTest.java`, lasst die drei Zeilen mit `api.ProcessGenehmigungProcessApi` weg. Sonst kennt der Test zwei `TASK_VERBUCHEN`, und Java meldet `Referenz zu TASK_VERBUCHEN ist mehrdeutig`.

Stimmt der errorCode nicht, fängt kein Error-Boundary den Fehler, und die Engine beendet die Instanz still. Der Test meldet dann `to be unfinished, but found that it already finished!`, genau wie euer Modell im laufenden System eine abgelehnte Buchung ohne Vorfall beendet.

### 6. End-to-end über das Formular

Folie „End-to-end: vom Formular bis zum Worker“.

1. Zurück in den Ordner `worker/` wechseln, aus `prozesstest-java/` mit `cd ../worker`. Worker starten und das Log offen lassen: `dotnet run --project src/GenehmigungWorker`
2. Als `anna` einen Antrag stellen, als `gerda` „Antrag prüfen“ mit `genehmigt` abschließen, genau wie in Übung 8.
3. Nach wenigen Sekunden zeigt das Log den geholten Task, eure Buchung und das `complete`.
4. Prüfen im Cockpit, das in CIB flow in der Weboberfläche steckt: Die Liste der Prozesse öffnet ihr direkt unter http://localhost:7083/client/#/seven/auth/processes/list, dort „Genehmigungsworkflow“ wählen. Links in der „Versionshistorie“ ist die neueste Version gewählt. Stehen dort mehrere Versionen, etwa Version 1 aus dem Import und Version 2 aus eurem Umbau in Übung 8, wählt die Version, auf der eure Instanz lief. Im Reiter „Instanzen“ steht eure Instanz mit Enddatum. Das Augen-Symbol öffnet sie, der Reiter „Variablen“ zeigt `buchungsnummer`.

Oder per REST:

```bash
# bash, zsh, Git Bash
curl -u worker:worker "http://localhost:8080/engine-rest/history/variable-instance?variableName=buchungsnummer"
curl -u worker:worker "http://localhost:8080/engine-rest/history/activity-instance?activityId=End_Genehmigt"
```

```powershell
# PowerShell
curl.exe -u worker:worker "http://localhost:8080/engine-rest/history/variable-instance?variableName=buchungsnummer"
curl.exe -u worker:worker "http://localhost:8080/engine-rest/history/activity-instance?activityId=End_Genehmigt"
```

Die erste Antwort nennt je Instanz `processInstanceId` und `value` der `buchungsnummer`. Die zweite listet die Instanzen, die das Ende „Antrag genehmigt“ erreicht haben. In beiden Listen stehen auch die Instanzen aus Prozesstests und Smoke-Test.

## Fertig, wenn

- [ ] Ein Antrag aus dem Startformular endet bei „Antrag genehmigt“: Im Cockpit ist die Instanz abgeschlossen, und `End_Genehmigt` taucht für sie in der History auf.
- [ ] `buchungsnummer` steht in den Variablen der Instanz.
- [ ] Unit-Test und Prozesstest in C# laufen grün: `dotnet test` im Ordner `worker/` meldet keinen Fehler.
- [ ] Der Prozesstest in Java läuft grün: `./mvnw test` im Ordner `prozesstest-java/` meldet `Failures: 0, Errors: 0`, übersprungen ist höchstens der Timer.

## Hinweise

- In Kapitel 11 stand der `try`/`catch` mit `complete` und `failure` noch im Handler. Jetzt gibt der Handler nur das Ergebnis zurück, zurückgemeldet wird einmal in der Schleife. Deshalb lässt sich der Handler ohne Engine testen.
- `BusinessKey ?? ProcessInstanceId`: Das Startformular setzt keinen Business Key, der Worker loggt `Business Key (keiner)`. Im Formular-Lauf ist der Schlüssel deshalb die Prozessinstanz-ID. Der Prozesstest setzt seinen Business Key selbst.
- `failure`: Beim ersten Fehler ist `Retries` null, der Worker meldet 3 verbleibende Versuche, danach zählt er herunter. Dazwischen liegen fünf Minuten, bei 0 legt die Engine einen Incident an. Wie viele Versuche übrig sind und warum es scheiterte, zeigt `curl -u worker:worker "http://localhost:8080/engine-rest/external-task?topicName=genehmigung-verbuchen"` (PowerShell: `curl.exe`) in `retries` und `errorMessage`, Incidents seht ihr im Cockpit unter „Vorfälle“.
- Idempotenz: Scheitert `CompleteAsync` nach einer erfolgreichen Buchung, läuft der `catch`, der Task kommt erneut, und der Handler bucht ein zweites Mal. Dagegen hilft nur ein Fachsystem, das den Schlüssel kennt. Genau das baut ihr im Bonus.
- Habt ihr den Worker eben erst gestoppt, kann der Prozesstest rund 30 Sekunden brauchen. Die letzte Long-Polling-Anfrage des Workers bleibt in der Engine noch bis zu zehn Sekunden offen und kann den Task des Tests holen. Dann gehört er für 30 Sekunden dem gestoppten Worker. Der Test-Helfer fragt deshalb bis zu 45 Sekunden lang nach.
- Die Tests im Startstand stehen in C# auf `Skip` und in Java auf `@Disabled`, damit `dotnet test` und `./mvnw test` von Anfang an sauber durchlaufen. Ein übersprungener Test ist kein grüner Test.

## Typische Stolpersteine

| Was ihr seht | Woran es liegt, was ihr tut |
|---|---|
| `NotImplementedException: TODO Kapitel 12: ...` im Test oder im Worker-Log | Dieser Schritt ist noch offen. |
| Der Worker meldet `failure`, und der Task kommt nicht wieder | Nach `failure` wartet der Task fünf Minuten. Hat euer Code den Fehler verursacht, korrigiert ihn und startet den Worker neu. Dann stellt ihr einen neuen Antrag, oder ihr gebt den alten Task sofort frei: seine `id` aus dem `curl`-Befehl unter „Hinweise“ nehmen und `curl -u worker:worker -X POST http://localhost:8080/engine-rest/external-task/<id>/unlock` (PowerShell: `curl.exe`). Der laufende Worker holt ihn gleich danach. |
| Build-Fehler `CS0246`, `GenehmigungVerbuchenHandler` oder `BuchungssystemSimulation` nicht gefunden | In `Program.cs` fehlen `using GenehmigungWorker.Fachsystem;` und `using GenehmigungWorker.Handlers;`. |
| Build-Fehler bei `new BuchungssystemSimulation()` | Ihr habt der Simulation im Bonus einen Konstruktorparameter gegeben. Passt den Aufruf in `Program.cs` an. |
| `KeyNotFoundException: The given key 'betrag' was not present` | Die Variable fehlt in der Instanz oder ist falsch geschrieben. Die Namen sind Teil des Vertrags mit dem Modell. |
| `InvalidCastException` bei `antragsteller` oder `begruendung` | Die Variable ist kein Text. `(string)` setzt Text voraus. |
| Die Buchung hat den zehn- oder hundertfachen Betrag, etwa `123450` statt `1234.50` (Musterlösung: `123.450,00 Euro` statt `1.234,50 Euro`), im Cockpit steht `betrag` aber richtig | `betrag` kam als Text aus dem easyForm, und der Handler liest ihn mit deutscher Kultur. Schritt 1, `CultureInfo.InvariantCulture`. |
| Im Cockpit steht `betrag` schon falsch, etwa `123450` statt `1234.50`, oder die Instanz aus dem Formular endet ohne `buchungsnummer` | Ihr habt den Betrag in einem Browser mit englischer Spracheinstellung mit Komma eingegeben, das Zahlenfeld hat das Komma verschluckt. Schreibt ihn dort mit Punkt, etwa `1234.50`. Über 50.000 Euro lehnt die Simulation der Musterlösung ab, und euer Modell beendet die Instanz dann still, siehe Bonus fachlicher Fehler. |
| Prozesstest in C#: `Kein External Task auf Topic ... auch nicht nach 45 Sekunden` | Euer Worker läuft noch und hat den Task schon verbucht. Stoppt ihn. Oder der Umbau aus Übung 8 ist nicht deployt, „Genehmigung verbuchen“ liegt dann als Aufgabe in der Aufgabenliste. Oder die Instanz steht gar nicht am Service Task, dann stimmen Entscheidung oder Modell nicht. |
| Prozesstest in C#: `lieferte 404 NotFound` beim Start, mit der Frage, ob das Modell bereitgestellt ist | Modell nicht bereitgestellt (Projekt importieren und den Umbau deployen wie in Übung 8), oder `ProzessKey` passt nicht zur Process ID des Modells. |
| Prozesstest in C#: `Die Engine unter http://localhost:8080/ antwortet nicht` | Der Stack läuft nicht oder startet noch. Im Ordner `stack/`: `docker compose up -d`, dann warten, bis `[init] Fertig.` in der letzten Zeile von `docker compose logs init` steht. |
| Prozesstest in C#: `Zugangsdaten für die Engine fehlen` | Im Terminal fehlen die Umgebungsvariablen. Setzt sie oder nehmt User Secrets (Befehle unter [Hinweise zu Übung 8](kapitel-11-lokales-setup.md#hinweise)). |
| Prozesstest in C#: `lieferte 401 Unauthorized` | Benutzer oder Passwort falsch. `dotnet user-secrets list --project src/GenehmigungWorker` zeigt, was gesetzt ist. |
| `dotnet test` meldet euren fertigen Test als übersprungen | `Skip` steht noch am `[Fact]`. |
| Die Instanz hängt bei „Genehmigung verbuchen“ | In dieser Reihenfolge prüfen: die Version (liegt „Genehmigung verbuchen“ als Aufgabe in der Aufgabenliste, läuft die Instanz auf der Version vor dem Umbau, stellt nach dem `deploy` einen neuen Antrag), Schreibweise des Topics in Modell und `appsettings.json`, `EngineUrl`, Lock (ein abgestürzter Worker hält ihn bis zu 30 Sekunden), `failure` mit fünf Minuten Pause. |

### Meldungen aus dem Prozesstest in Java

Nur lesen, wenn es rot wird. Unter dem Baum steht im Block „Results“ je rotem Test eine Zeile: Klasse, Methode, Zeilennummer und die Meldung.

| Was ihr seht | Woran es liegt, was ihr tut |
|---|---|
| `↷` vor eurem Test, `Skipped` zählt ihn noch | `@Disabled` steht noch über der Methode. |
| `to be waiting at exactly [Task_Pruefen], but it is actually waiting at [StartEvent_Antrag]` | Der Speicherpunkt nach dem Start fehlt: `speicherpunktAnstossen(antrag, START_EVENT_ANTRAG)`. Allgemein nennt `actually waiting at [...]` die Stelle, an der die Instanz wirklich wartet. |
| `to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Nachbessern]` | Der Speicherpunkt nach „Antrag nachbessern“ fehlt: `speicherpunktAnstossen(antrag, TASK_NACHBESSERN)`. |
| `to be waiting at exactly [Task_Pruefen], but it is actually waiting at [Task_Pruefen, Task_Erinnern]` | Nach dem Timer sind zwei Aufgaben offen. Prüft beide IDs: `isWaitingAtExactly(TASK_PRUEFEN.getValue(), TASK_ERINNERN.getValue())`. |
| `Query return 2 results instead of max 1` | `task()` ohne ID bei zwei offenen Aufgaben. Nehmt `task(TASK_ERINNERN.getValue(), antrag)`. |
| `by less than 10000ms but difference was 259020004ms` bei der Fälligkeit des Timers | Ihr erwartet drei Tage. „3 Tage ohne Entscheidung“ steht im Modell fürs Training auf `PT3M`, erwartet sind drei Minuten: `Duration.ofMinutes(3)`. |
| `to be ended, but it is not!` | Nach dem Abschließen von „Antrag prüfen“ fehlt `speicherpunktAnstossen(antrag, TASK_PRUEFEN)`, das Gateway hat noch nicht entschieden. Oder auf dem Pfad ist noch eine Aufgabe offen, etwa „Ablehnung mitteilen“ ohne `complete(task())`. Den Zusatz `Please make sure you have set the history service ...` ignoriert ihr, die History ist eingeschaltet. |
| `ENGINE-02004 No outgoing sequence flow for the element with id 'Gateway_Entscheidung' could be selected for continuing the process.` | Kein Pfeil passt zur `entscheidung`. Die Werte heißen genau `genehmigt`, `abgelehnt` und `nachbessern`, kleingeschrieben. |
| `Illegal call of execute(job = 'null') - must not be null!` | Diesen Job gibt es gerade nicht, etwa `speicherpunktAnstossen(antrag, TASK_PRUEFEN)`, bevor ihr die Aufgabe abgeschlossen habt. |
| `Cannot invoke "org.cibseven.bpm.engine.runtime.Job.getDuedate()" because "timer" is null` | Den Timer gibt es erst, wenn die Instanz bei „Antrag prüfen“ wartet. Stoßt vorher den Speicherpunkt nach dem Start an. |
| `Illegal call of complete(task = 'null'` | Es wartet keine Aufgabe. Mit Variablen endet die Meldung auf `both must not be null!`. Prüft vorher mit `isWaitingAtExactly`, wo die Instanz steht. |
| `Call a process instance assertion first - e.g. assertThat(processInstance)... !` | `task()` weiß nicht, welche Instanz gemeint ist. Ruft vorher `assertThat(antrag)` auf, `speicherpunktAnstossen` tut das auch. Ohne `assertThat` nimmt `task()` die Instanz aus dem letzten `assertThat`, notfalls aus dem vorigen Test. Diese Meldung seht ihr deshalb nur, wenn der Test allein läuft, sonst kommt meist `Illegal call of complete(task = 'null'`. |
| `to have passed activities [Ablehnung mitteilen] at least once, but actually we found that it passed [StartEvent_Antrag, Task_Pruefen, Gateway_Entscheidung, Task_Ablehnen, End_Abgelehnt]` | Beschriftung statt ID. Nehmt die Konstante, etwa `TASK_ABLEHNEN.getValue()`. Die Liste dahinter zeigt die IDs des Pfads, den die Instanz genommen hat. |
| `to be unfinished, but found that it already finished!` | Die Instanz ist schon zu Ende, der Test fragt aber nach einem Wartezustand. Nach `complete(task())` an „Ablehnung mitteilen“ ist die Instanz zu Ende, dann prüft ihr `isEnded()`. |
| `COMPILATION ERROR` mit Datei und `[Zeile,Spalte]`, etwa `';' erwartet` (englisch: `';' expected`) | Java-Syntax an dieser Stelle: Semikolon, Klammer oder Anführungszeichen fehlt. |
| `Symbol nicht gefunden`, darunter `Symbol: Variable TASK_PRUEFEN` (englisch: `cannot find symbol`, `symbol: variable TASK_PRUEFEN`) | Diese Konstante gibt es nicht: ein Tippfehler, oder die ID heißt im Modell anders, etwa `Task_Pruefen2`, dann heißt die Konstante `TASK_PRUEFEN_2`. Hat jemand eine ID im Modell geändert, nennt Maven jede Zeile, die noch die alte nutzt. Alle Namen stehen unter `target/generated-test-sources/bpmn-to-code/` in `ProcessGenehmigungProcessApi.java`, Abschnitt `Elements`. |
| `Inkompatible Typen: java.lang.String kann nicht in io.miragon.bpmn.runtime.ElementId konvertiert werden` (englisch: `incompatible types: java.lang.String cannot be converted to io.miragon.bpmn.runtime.ElementId`) | Die ID als Text, etwa `speicherpunktAnstossen(antrag, "Task_Pruefen")`. Die Hilfsmethode nimmt die Konstante: `speicherpunktAnstossen(antrag, TASK_PRUEFEN)`. |
| `kann nicht auf die angegebenen Typen angewendet werden` oder `Methode für task(io.miragon.bpmn.runtime.ElementId,...) nicht geeignet`, darunter `ElementId kann nicht in java.lang.String konvertiert werden` (englisch: `cannot be applied to given types`, `no suitable method found for task(...)`, `ElementId cannot be converted to java.lang.String`) | `.getValue()` fehlt. Prüfungen, `task(...)` und `job(...)` erwarten die ID als Text: `isWaitingAtExactly(TASK_PRUEFEN.getValue())`. |
| Eure IDE markiert `TASK_PRUEFEN` oder `ProcessGenehmigungProcessApi` rot, `./mvnw test` läuft aber | Die Klasse entsteht erst beim Lauf. Startet `./mvnw test` einmal und ladet danach das Maven-Projekt in der IDE neu. |
| `Methode für assertThat(java.util.Date) nicht geeignet` (englisch: `no suitable method found for assertThat(java.util.Date)`), darunter fünf Methoden aus `BpmnAwareTests` | Das importierte `assertThat` kennt nur Objekte der Engine wie Instanz, Aufgabe und Job. Für Datum und Listen schreibt ihr `Assertions.assertThat(...)`. |
| `Fatal error compiling: error: release version 21 not supported` | Maven läuft mit einem älteren JDK. `java -version` muss 21 oder neuer zeigen, sonst setzt ihr `JAVA_HOME` auf das JDK 21. |
| `The JAVA_HOME environment variable is not defined correctly` | Maven findet kein JDK. JDK 21 installieren oder `JAVA_HOME` auf sein Verzeichnis setzen, dann ein neues Terminal öffnen. |
| Der erste Lauf scheitert mit `PKIX path building failed` oder `Could not transfer artifact` | Ihr sitzt hinter einem Proxy, oder eure IT prüft verschlüsselte Verbindungen. Tragt den Proxy in `~/.m2/settings.xml` ein oder startet den ersten Lauf außerhalb des Behördennetzes, etwa über einen Hotspot, siehe [README des Projekts](../prozesstest-java/README.md#hinter-einem-proxy). Klappt es am Schulungstag nicht, schreibt ihr den Java-Teil zu zweit am Rechner eurer Nachbarn. |

## Bonus: Idempotenz

Für alle, die schneller fertig sind. Stirbt der Worker nach dem Verbuchen und vor `complete`, läuft der Lock ab, und derselbe Task kommt erneut. Die Buchung darf dann nicht doppelt entstehen.

1. Die Simulation merkt sich Schlüssel und Nummer. Kommt derselbe Schlüssel noch einmal, liefert sie dieselbe Nummer, statt ein zweites Mal zu buchen.
2. Damit das einen Neustart des Workers übersteht, schreibt sie beides in eine Datei, nicht nur in den Speicher. Gebt den Dateipfad im Konstruktor mit. `Program.cs` nimmt eine Datei neben der DLL, etwa `Path.Combine(AppContext.BaseDirectory, "buchungen.json")`, der Test eine eigene Datei unter `Path.GetTempPath()`. `.gitignore` hält `buchungen.json` aus dem Repo.
3. Speichert die Buchung, bevor ihr die Nummer zurückgebt. Sonst hilft die Datei im entscheidenden Moment nicht.
4. Ein Unit-Test: zweimal derselbe Schlüssel, beide Male dieselbe Nummer, auch mit einer neuen Instanz der Simulation auf derselben Datei. Ein anderer Schlüssel bekommt die nächste Nummer.

Im Formular-Lauf ausprobieren:

1. Baut in `Program.cs` direkt nach `var ergebnis = handler.Handle(task);` vorübergehend eine Pause ein: `Thread.Sleep(TimeSpan.FromSeconds(20));`
2. Worker starten, Antrag stellen und genehmigen. Sobald das Log die Buchung zeigt, beendet ihr den Worker hart: Terminal schließen, in VS Code das Papierkorb-Symbol am Terminal. Strg+C reicht nicht, dann wartet der Worker die Pause ab und schickt `complete`.
3. Worker in einem neuen Terminal wieder starten, im Ordner `worker/`. Sind die Zugangsdaten Umgebungsvariablen, setzt sie dort neu. Nach Ablauf des Locks, höchstens 30 Sekunden nach dem ersten Holen, kommt der Task erneut. Eure Simulation erkennt den Schlüssel, bucht nicht noch einmal, und der Task endet mit derselben Nummer. Die Musterlösung loggt dazu `Schlüssel ... ist schon verbucht als B-2026-0001, keine zweite Buchung.`
4. Pause wieder entfernen.

Der Schlüssel ist im Formular-Lauf die Prozessinstanz-ID, weil das Startformular keinen Business Key setzt.

## Bonus: Fachlicher Fehler

Der zweite Bonus, für alle, die noch Zeit haben. Nicht jeder Fehler ist technisch. Lehnt das Fachsystem eine Buchung ab, etwa weil das Budget der Kostenstelle nicht reicht, ändert ein zweiter Versuch nichts daran. Mit `failure` würde der Worker alle fünf Minuten dasselbe Nein abholen, bis die Engine einen Incident anlegt. Stattdessen meldet er einen BPMN-Fehler, und das Modell führt die Instanz auf einen eigenen Pfad.

Diesen Pfad hat nur die Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` aus Kapitel 04 und 11: der Ausschnitt ab „Genehmigung erteilt“. Am Service Task „Genehmigung verbuchen“ hängt das Error-Boundary „Buchung abgelehnt“ für den errorCode `BUCHUNG_ABGELEHNT`. Es legt Code und Grund in den Variablen `errorCode` und `errorMessage` ab und führt zu „Buchung klären“ für die Gruppe `genehmiger`. Die Variante hat eine eigene Process ID, `Process_VerbuchenFehlerpfad`, aber dasselbe Topic `genehmigung-verbuchen`. Euer Worker bedient deshalb beide Modelle, ohne dass ihr `appsettings.json` ändert.

1. **Simulation ablehnen lassen.** Legt unter `Fachsystem/` eine eigene Exception an, etwa `BuchungAbgelehntException`, mit dem Grund als Message. `BuchungssystemSimulation.Verbuchen` wirft sie direkt nach der Idempotenzprüfung, wenn der Betrag über einem festen Budget liegt, etwa 50.000 Euro je Buchung. Die Meldung nennt den Grund: `Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei`. Eine abgelehnte Buchung speichert die Simulation nicht. Am Handler ändert ihr nichts, die Exception fliegt durch ihn hindurch bis in die Schleife.
2. **Vierte Methode im `ExternalTaskClient`.** `BpmnErrorAsync(ExternalTask task, string errorCode, string meldung)` schickt `POST /engine-rest/external-task/{id}/bpmnError` mit `workerId`, `errorCode` und `errorMessage`. Vorbild ist `FailureAsync`.
3. **Catch in der Schleife.** In `Program.cs` direkt vor `catch (Exception ex)` ein `catch (BuchungAbgelehntException abgelehnt)` einsetzen. Es ruft `BpmnErrorAsync(task, "BUCHUNG_ABGELEHNT", abgelehnt.Message)` und schreibt eine Log-Zeile. Die Reihenfolge zählt: C# nimmt den ersten passenden `catch`, und `catch (Exception)` passt auf alles.
4. **Variante deployen.** Mit einem Pfad dahinter spielt `deploy` diese Datei ein statt `prozess/genehmigungsworkflow.bpmn`. Das Deployment heißt dann wie die Datei, euer Modell bleibt, wie es ist.
   ```bash
   dotnet run --project src/GenehmigungWorker -- deploy prozess/varianten/verbuchen-fehlerpfad.bpmn
   ```
   Den Pfad schreibt ihr ab dem Repo-Root, auch im Ordner `worker/`: `deploy` sucht die Datei vom aktuellen Ordner aus nach oben. Beim ersten Mal meldet `deploy` `Neue Version: Process_VerbuchenFehlerpfad, Version 1`, danach „Modell unverändert“.
5. **Worker starten.** Läuft noch der alte, stoppt ihn mit Strg+C, dann: `dotnet run --project src/GenehmigungWorker`
6. **Variante per REST starten.** Die Variante hat weder Startformular noch Initiator, deshalb gebt ihr `antragsteller`, `betrag` und `begruendung` selbst mit. Fehlt eine davon, scheitert der Handler mit `KeyNotFoundException`, und der Worker meldet `failure`. Am bequemsten geht das mit der http-Datei `http/genehmigungsworkflow.http`: B2 startet die Variante, B3 bis B5 prüfen das Ergebnis, B1 deployt sie wie Schritt 4. Oder im Terminal:
   ```bash
   # bash, zsh, Git Bash
   curl -u anna:anna -H "Content-Type: application/json" \
     -d '{"variables":{"antragsteller":{"value":"anna","type":"String"},"betrag":{"value":60000,"type":"Long"},"begruendung":{"value":"Neue Serverhardware","type":"String"}}}' \
     http://localhost:8080/engine-rest/process-definition/key/Process_VerbuchenFehlerpfad/start
   ```
   ```powershell
   # PowerShell
   $anmeldung = "Basic " + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("anna:anna"))
   $antrag = '{"variables":{"antragsteller":{"value":"anna","type":"String"},"betrag":{"value":60000,"type":"Long"},"begruendung":{"value":"Neue Serverhardware","type":"String"}}}'
   Invoke-RestMethod -Method Post -Headers @{ Authorization = $anmeldung } -ContentType "application/json" -Body $antrag -Uri http://localhost:8080/engine-rest/process-definition/key/Process_VerbuchenFehlerpfad/start
   ```
7. **Prüfen.** Nach wenigen Sekunden zeigt das Worker-Log den geholten Task und eure Ablehnung, in der Musterlösung `Task ... fachlich abgelehnt: Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei. bpmnError BUCHUNG_ABGELEHNT gemeldet.` Meldet euch als `gerda` an: In „Aufgaben bearbeiten“ wartet im Filter „Aufgaben meiner Gruppen“ die Aufgabe „Buchung klären“ aus „Genehmigung verbuchen mit Fehlerpfad“. Sie hat kein Formular, nach „Mir zuweisen“ zeigt CIB flow sie als „Leere Aufgabe“ mit „Abschließen“. Den Grund seht ihr im Cockpit: Öffnet die Prozessliste unter http://localhost:7083/client/#/seven/auth/processes/list, wählt „Genehmigung verbuchen mit Fehlerpfad“ und öffnet die Instanz über das Augen-Symbol. Im Reiter „Variablen“ stehen `errorCode` mit `BUCHUNG_ABGELEHNT` und `errorMessage` mit dem Grund. In der http-Datei zeigen B3 die Aufgabe und B4 die Variablen.
8. **Gegenprobe.** Startet die Variante noch einmal, diesmal mit `betrag` 1200 statt 60000, in B2 der http-Datei oder im Befehl aus Schritt 6. Jetzt verbucht der Worker, und die Instanz endet bei „Antrag genehmigt“. B5 meldet `COMPLETED`, B4 zeigt `buchungsnummer` und `genehmigungMitgeteilt`.

Warum die Variante? Mit 60.000 Euro aus dem Startformular kommt auch euer Modell bis zu „Genehmigung verbuchen“, aber dort fängt kein Error-Boundary `BUCHUNG_ABGELEHNT`. Dann beendet die Engine die Instanz still am Service Task: Im Cockpit steht sie als abgeschlossen, ohne „Antrag genehmigt“ und ohne `buchungsnummer`. Es gibt keinen Vorfall, und den Grund findet ihr nur im Log der Engine: `docker compose logs flow-cibseven-spring` im Ordner `stack/` zeigt `ENGINE-02001 ... but no catching boundary event was defined. Execution is ended`. Ein `bpmnError` braucht also ein Boundary im Modell, das seinen Code fängt.

## Musterlösung

`loesung/` enthält die Musterlösung als zwei vollständige Projekte, aufgebaut wie der Startstand: `loesung/worker/` mit Solution, `src/` und `tests/`, und `loesung/prozesstest-java/` mit Maven Wrapper, `pom.xml`, den Kopien der Modelle und allen Tests. Ihr baut und testet sie direkt in diesen Ordnern, ohne etwas zu kopieren. Die README des Java-Projekts liegt nur im Startstand. Sonst weichen vom Startstand nur diese Dateien ab, alle anderen sind gleich:

| Datei | Was die Musterlösung macht |
|---|---|
| `loesung/worker/src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs` | wie auf der Folie, dazu `betrag` kulturunabhängig als Text oder Zahl |
| `loesung/worker/src/GenehmigungWorker/Program.cs` | Schleife von der Folie, dazu je eine Log-Zeile für geholt, erledigt und fehlgeschlagen, für den Bonus fachlicher Fehler `catch (BuchungAbgelehntException)` mit `bpmnError` |
| `loesung/worker/src/GenehmigungWorker/ExternalTaskClient.cs` | wie im Startstand, dazu `BpmnErrorAsync` |
| `loesung/worker/src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` | fortlaufende Nummern je Jahr, idempotent über `buchungen.json` neben der DLL, lehnt über 50.000 Euro je Buchung ab, Beträge in Meldung und Log immer deutsch |
| `loesung/worker/src/GenehmigungWorker/Fachsystem/BuchungAbgelehntException.cs` | neu: die fachliche Ablehnung mit Grund |
| `loesung/worker/tests/GenehmigungWorker.Tests/BuchungssystemFake.cs` | liefert `B-2026-0001` und merkt sich jeden Aufruf |
| `loesung/worker/tests/GenehmigungWorker.Tests/GenehmigungVerbuchenHandlerTests.cs` | Test der Folie, ohne Business Key, `betrag` als Text `"1234.5"` und als Zahl `1200L` auf einem deutschen Rechner, fehlende Variable, Idempotenz der Simulation, Ablehnung über dem Budget in Simulation und Handler |
| `loesung/worker/tests/GenehmigungWorker.Tests/ExternalTaskClientTests.cs` | neu: `BpmnErrorAsync` schickt Pfad und Body, ohne Engine |
| `loesung/worker/tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs` | Prozesstest der Folie und Gegenprobe mit `abgelehnt` |
| `loesung/worker/tests/GenehmigungWorker.Tests/FehlerpfadTests.cs` | neu: Prozesstest gegen die Variante, deployt sie selbst und prüft „Buchung klären“ mit `errorCode` und `errorMessage` |
| `loesung/prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java` | alle vier Testfälle: Happy Path, Ablehnung, Nachbesserung, Timer |
| `loesung/prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/FehlerpfadTest.java` | neu, Bonus: Prozesstest gegen die Variante, `bpmnError` mit `BUCHUNG_ABGELEHNT` führt zu „Buchung klären“, Gegenprobe mit `complete` endet bei „Antrag genehmigt“ |
| `loesung/prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTag1Test.java` und `loesung/prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn` | neu, nur für die Demo in Kapitel 10: dieselben vier Testfälle am fertigen Modell nach Übung 7, ohne External Task |
| `loesung/genehmigungsworkflow-entwickler.bpmn` | Lösung von Übung 8: das Modell mit „Genehmigung verbuchen“ als External Task, Quelle der Modellkopien in beiden Java-Projekten |

**Vergleichen:** In VS Code beide Dateien im Explorer markieren, Rechtsklick, „Ausgewählte vergleichen“. Oder im Terminal im Repo-Root, bash und PowerShell gleich:

```bash
git diff --no-index worker/src/GenehmigungWorker/Program.cs loesung/worker/src/GenehmigungWorker/Program.cs
git diff --no-index prozesstest-java/src/test/java loesung/prozesstest-java/src/test/java
```

Ganze Ordner vergleicht ihr genauso: `git diff --no-index worker/src loesung/worker/src` und `git diff --no-index worker/tests loesung/worker/tests`. Habt ihr schon gebaut, listet dieser Vergleich auch die Build-Ausgaben unter `bin/` und `obj/` auf, denn `--no-index` beachtet `.gitignore` nicht.

**Bauen und testen:** Die Musterlösung ist ein eigenes Projekt. Ihr wechselt in ihren Ordner und nehmt dieselben Befehle wie in `worker/`. Stoppt vorher euren eigenen Worker, beide hören auf dasselbe Topic.

```bash
# im Repo-Root, bash und PowerShell gleich
cd loesung/worker
dotnet test --filter "Kategorie!=Prozesstest"     # ohne Engine
dotnet test                                       # mit laufendem Stack und bereitgestelltem Modell
dotnet run --project src/GenehmigungWorker        # der Worker der Musterlösung
```

Für Java, wieder vom Repo-Root aus:

```bash
cd loesung/prozesstest-java
./mvnw test          # Windows PowerShell: .\mvnw.cmd test
```

Eure User Secrets aus Übung 8 gelten für beide Projekte, `worker/` und `loesung/worker/` haben dieselbe `UserSecretsId`. Arbeitet ihr mit eurem eigenen Projekt, tragt ihr euren `ProzessKey` auch in `loesung/worker/src/GenehmigungWorker/appsettings.json` ein.

In `loesung/worker/` laufen `dotnet test --filter "Kategorie!=Prozesstest"` ohne Engine (9 Tests) und `dotnet test` mit laufendem Stack und bereitgestelltem Modell (12 Tests) grün. Die Variante für den Prozesstest zum fachlichen Fehler deployt der Test selbst. In `loesung/prozesstest-java/` meldet `./mvnw test` 10 bestandene Tests: vier am Modell mit External Task, vier der Demo am Modell ohne External Task, zwei des Fehlerpfads. Genau das prüft auch die GitHub Action des Repos bei jedem Push.

**Einzelne Dateien übernehmen:** Wollt ihr eine Datei der Musterlösung in eurem Stand haben, kopiert ihr sie im Repo-Root, etwa `cp loesung/worker/src/GenehmigungWorker/Program.cs worker/src/GenehmigungWorker/` (PowerShell: `Copy-Item loesung\worker\src\GenehmigungWorker\Program.cs worker\src\GenehmigungWorker\`). Das überschreibt eure Fassung dieser Datei, sichert oder committet sie vorher. Achtet dabei auf Paare, die zusammengehören:

- `Program.cs` ruft den Konstruktor der Simulation mit Dateipfad auf, fängt `BuchungAbgelehntException` und ruft `BpmnErrorAsync`. Übernehmt `BuchungssystemSimulation.cs`, `Fachsystem/BuchungAbgelehntException.cs` und `ExternalTaskClient.cs` mit.
- `GenehmigungVerbuchenHandlerTests.cs` braucht den Fake der Musterlösung (`Aufrufe`), ihre Simulation und die Exception.
- `ExternalTaskClientTests.cs` braucht den `ExternalTaskClient` der Musterlösung, `FehlerpfadTests.cs` dazu Simulation und Exception.
- Die drei Java-Dateien stehen jede für sich. `FehlerpfadTest.java` braucht nur die Kopie der Variante, die schon im Startstand liegt. `GenehmigungsworkflowTag1Test.java` braucht `genehmigungsworkflow-tag1.bpmn`, die Datei liegt nur in der Musterlösung. Die Klassen mit den IDs erzeugt der Startstand selbst, seine `pom.xml` ist dieselbe.

**Zurück zum Startstand:** Im Repo-Root mit `git restore worker prozesstest-java` und `git clean -fd worker prozesstest-java`, in bash und PowerShell gleich. Das verwirft alle eure Änderungen in diesen Ordnern und löscht Dateien, die dort neu dazugekommen sind, auch eure eigenen. Ohne `git clean` bleiben übernommene Dateien wie `ExternalTaskClientTests.cs` und `FehlerpfadTests.cs` aus der Musterlösung liegen, und der Startstand baut nicht mehr. In Java bliebe `FehlerpfadTest.java` liegen und liefe weiter mit. Die Build-Ausgaben unter `bin/`, `obj/` und `target/` lässt `git clean` stehen, Git ignoriert sie. Die Musterlösung unter `loesung/` bleibt dabei, wie sie ist, ebenso `prozess/`: Dort liegt euer umgebautes Modell aus Übung 8.

Der Worker der Musterlösung loggt jeden Task:

```
15:19:47 Worker genehmigung-worker-1 holt Tasks vom Topic genehmigung-verbuchen bei http://localhost:8080. Beenden mit Strg+C.
15:19:47 Buchungen der Simulation: .../src/GenehmigungWorker/bin/Debug/net10.0/buchungen.json
15:19:47 Task 39770f19-... geholt: Business Key (keiner), Prozessinstanz 240ce15c-..., Retries (noch keine)
15:19:47 Verbucht: B-2026-0001 für anna, 1.234,50 Euro, "Fachtagung Prozessautomatisierung" (Schlüssel 240ce15c-...)
15:19:47 Task 39770f19-... erledigt: buchungsnummer = B-2026-0001
15:26:04 Task 5252edd3-... geholt: Business Key (keiner), Prozessinstanz 52527898-..., Retries (noch keine)
15:26:04 Task 5252edd3-... fachlich abgelehnt: Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei. bpmnError BUCHUNG_ABGELEHNT gemeldet.
```

Kommt derselbe Schlüssel noch einmal, meldet die Simulation `Schlüssel ... ist schon verbucht als B-2026-0001, keine zweite Buchung.`, und der Worker schließt den Task mit derselben Nummer ab. Scheitert der Handler, etwa an einer fehlenden Variablen, schickt der Worker `failure`: beim ersten Mal mit 3 verbleibenden Versuchen, danach herunterzählend, dazwischen fünf Minuten Pause. Bei 0 legt die Engine einen Incident an. Liegt der Betrag über 50.000 Euro, lehnt die Simulation ab, und der Worker meldet `bpmnError` mit `BUCHUNG_ABGELEHNT` und dem Grund als `errorMessage`. In der Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` wartet danach „Buchung klären“. In eurem Modell fängt kein Error-Boundary den Fehler, die Engine beendet die Instanz dann still am Service Task, ohne Incident.

## Zum Nachschlagen

### Aufbau von Worker und Tests

```
worker/
├── GenehmigungWorker.sln                    # Solution mit Worker und Tests, hier laufen die dotnet-Befehle
├── src/GenehmigungWorker/
│   ├── Program.cs                           # Worker-Schleife, mit "deploy" das Deployment
│   ├── ExternalTaskClient.cs                # fetchAndLock, complete, failure und der Record ExternalTask
│   ├── Deploy.cs                            # spielt prozess/genehmigungsworkflow.bpmn ein, mit Pfad eine andere Datei
│   ├── Einstellungen.cs                     # liest appsettings.json, User Secrets und Umgebung
│   ├── Handlers/
│   │   └── GenehmigungVerbuchenHandler.cs   # lesen, verbuchen, Ergebnis zurückgeben
│   ├── Fachsystem/
│   │   ├── IBuchungssystem.cs               # die eine Stelle nach außen
│   │   └── BuchungssystemSimulation.cs      # simulierte Buchung statt echtem Fachsystem
│   └── appsettings.json                     # EngineUrl, ProzessKey, Topic, WorkerId
└── tests/GenehmigungWorker.Tests/
    ├── GenehmigungVerbuchenHandlerTests.cs  # Unit-Test für den Handler, ohne Engine
    ├── BuchungssystemFake.cs                # Fake statt Fachsystem
    ├── GenehmigungsworkflowTests.cs         # Prozesstest per REST gegen eure lokale Engine
    └── EngineHelfer.cs                      # Test-Helfer für den Prozesstest, fertig vorgegeben
prozesstest-java/
├── pom.xml                                  # Engine im Speicher, cibseven-bpm-junit5, cibseven-bpm-assert, bpmn-to-code
├── mvnw, mvnw.cmd                           # Maven Wrapper, lädt Maven beim ersten Lauf
├── src/main/resources/
│   ├── genehmigungsworkflow.bpmn            # Kopie der Entwickler-Fassung, byte-gleich
│   └── verbuchen-fehlerpfad.bpmn            # Kopie der Variante für den Bonus, byte-gleich
├── src/test/java/io/miragon/schulung/genehmigung/
│   └── GenehmigungsworkflowTest.java        # Prozesstest mit der Engine im Speicher
└── target/generated-test-sources/bpmn-to-code/   # entsteht beim Lauf: Klassen mit den IDs der Modelle, nicht im Repo
```

Die Musterlösung ist genauso aufgebaut: `loesung/worker/` und `loesung/prozesstest-java/`.

### Tests

Im Ordner `worker/`, für die Musterlösung in `loesung/worker/`:

```bash
dotnet test                                               # alle Tests, die Prozesstests brauchen die Engine
dotnet test --filter "Kategorie!=Prozesstest"             # nur die Unit-Tests, ohne Engine
dotnet test --filter "Kategorie=Prozesstest"              # nur die Prozesstests
```

Den Prozesstest in Java startet ihr im Ordner `prozesstest-java/` mit `./mvnw test`, in PowerShell mit `.\mvnw.cmd test`, für die Musterlösung in `loesung/prozesstest-java/`.

Die Unit-Tests brauchen weder Engine noch Zugangsdaten und laufen in Millisekunden. Die Prozesstests tragen `[Trait("Kategorie", "Prozesstest")]` und laufen per REST gegen die lokale Engine: Der Stack muss laufen, das Modell bereitgestellt, die Zugangsdaten gesetzt (dieselben wie für den Worker, auch als User Secrets) und euer Worker gestoppt sein. Jeder Test startet eine eigene Instanz mit Business Key `prozesstest-...`, holt den External Task mit Filter auf diesen Business Key unter der Worker-ID `prozesstest` und löscht am Ende, was von seinen Instanzen noch offen ist. Läuft die Engine nicht, fehlt das Modell oder stimmen die Zugangsdaten nicht, nennt die Fehlermeldung des Tests die Ursache und den nächsten Schritt.

| Stand | Ordner | `dotnet test --filter "Kategorie!=Prozesstest"` | `dotnet test` mit laufendem Stack | `./mvnw test` |
|---|---|---|---|---|
| Startstand | `worker/` und `prozesstest-java/` | 1 übersprungen | 3 übersprungen | 1 bestanden, 3 übersprungen |
| Musterlösung | `loesung/worker/` und `loesung/prozesstest-java/` | 9 bestanden | 12 bestanden | 10 bestanden |

Der Prozesstest in Java braucht weder Stack noch Zugangsdaten noch einen gestoppten Worker: `cibseven-bpm-junit5` startet für die Testklasse eine Engine mit H2 im Speicher und deployt für jeden Testfall die Kopie des Modells aus `src/main/resources/`. Einen Job Executor hat diese Engine nicht, Speicherpunkte und Timer stößt der Test selbst an.

Den Test-Helfer `EngineHelfer.cs` (im Test `_engine`) bekommt ihr fertig: je Methode ein REST-Endpunkt, etwa `StartAsync`, `GetTaskAsync`, `CompleteTaskAsync`, `FetchAndLockAsync`, `CompleteAsync`, `GetHistoryAsync`, `GetVariableAsync` und für die Gegenprobe `GetExternalTasksAsync`. Die lesenden Methoden warten auf den Zustand, statt nur einmal zu fragen: `GetTaskAsync` fragt bis zu zehn Sekunden lang nach, bis die Aufgabe da ist. `GetHistoryAsync`, `GetVariableAsync` und `GetExternalTasksAsync` warten vorher, bis an der Instanz kein Speicherpunkt mehr aussteht. Das braucht euer Modell: Der easyForm-Baustein setzt nach dem Start-Event, nach „Antrag prüfen“ und nach „Antrag nachbessern“ je einen Speicherpunkt, die Engine antwortet dort schon, und den Rest führt ihr Job Executor kurz danach im Hintergrund aus. Kommt der Zustand nicht, nennt die Fehlermeldung, was erwartet war und wo die Instanz steht. `FetchAndLockAsync` fragt bis zu 45 Sekunden lang nach, statt nach einem leeren fetchAndLock sofort aufzugeben: Die letzte Long-Polling-Anfrage eines eben gestoppten Workers bleibt in der Engine bis zu zehn Sekunden offen und kann den Task des Tests noch für 30 Sekunden sperren.

### Entscheidungen, wo die Folien offen sind

- `Einstellungen.cs` liest die Konfiguration und baut den `HttpClient` mit `EngineUrl` als `BaseAddress` und Basic Auth. Worker, Deployment und Prozesstest nutzen sie gemeinsam.
- `lockDuration` (30 s), `maxTasks` (5) und `asyncResponseTimeout` (10 s) stehen wie auf der Folie im Code von `FetchAndLockAsync`, nicht in `appsettings.json`.
- `ExternalTask.Variables` ist ein `Dictionary<string, object>` mit ausgepackten Werten: Text als `string`, ganze Zahlen als `long`, andere Zahlen als `double`, Wahrheitswerte als `bool`. Variablen ohne Wert (`null`) lässt `ToTask` weg, der Handler scheitert dann laut an einer fehlenden Variable. So baut der Handler von der Folie ohne Nullable-Warnungen.
- `betrag` liest der Handler der Musterlösung als Text mit `decimal.Parse` und als Zahl mit `Convert.ToDecimal`, beides mit `CultureInfo.InvariantCulture`. Das easyForm speichert auch ein Feld vom Typ „Zahl“ als Text.
- `CompleteAsync` schickt die Werte typisiert: `string` als String, `int` als Integer, `long` als Long, `decimal` und `double` als Double, `bool` als Boolean.
- `IBuchungssystem` und die Simulation liegen unter `Fachsystem/`, getrennt von den Handlern.
- Der Prozesstest in C# nimmt Prozess-Key und Topic aus der Konfiguration (`_engine.ProzessKey`, `_engine.Topic`). Auf der Folie stehen sie ausgeschrieben.
- Der Prozesstest in Java liest Process ID, IDs, Topic und Fehlercode aus Klassen, die bpmn-to-code von Miragon bei jedem Lauf aus den Modellkopien erzeugt, etwa `TASK_PRUEFEN.getValue()` statt `"Task_Pruefen"`. Variablennamen wie `entscheidung` bleiben Text, das Modell legt sie nicht als Ein- oder Ausgabe fest. Die Demo in Kapitel 10 (`GenehmigungsworkflowTag1Test`) schreibt die IDs als Text: Dort macht eine geänderte ID den Test erst beim Lauf rot, mit den Konstanten fällt sie schon beim Übersetzen auf.
- Die Tests im Startstand sind mit `Skip` markiert statt rot, in Java mit `@Disabled`. So laufen `dotnet test` und `./mvnw test` von Anfang an sauber durch, und ihr seht, welche Tests noch fehlen.
- Die Prozesstests tragen den Trait `Kategorie=Prozesstest`. Damit trennt `--filter` sie von den Unit-Tests, etwa auf einem Rechner ohne Engine.
- Die Simulation speichert ihre Buchungen in `buchungen.json` neben der DLL (in der Musterlösung `loesung/worker/src/GenehmigungWorker/bin/Debug/net10.0/`). Den Pfad gibt `Program.cs` im Konstruktor mit und loggt ihn beim Start. So findet der Worker die Datei, egal wo ihr ihn startet, und sie landet nie im Repo. Der Worker unter `worker/` und der unter `loesung/worker/` haben damit je eine eigene Datei. Löscht ihr die Datei, beginnen die Nummern wieder bei 0001. Gezählt wird je Kalenderjahr: B-2026-0001, B-2026-0002 und so weiter.
- Die Simulation speichert die Buchung, bevor sie die Nummer zurückgibt. Stirbt der Worker zwischen Verbuchen und `complete`, bekommt der nächste Versuch dieselbe Nummer.
- Die Schleife in der Musterlösung ist die von der Folie, ergänzt um je eine Log-Zeile für geholt, erledigt und fehlgeschlagen.
- Der Unit-Test-Fake merkt sich seine Aufrufe. Damit prüft ein Test, dass der Handler ohne Business Key unter der Prozessinstanz-ID verbucht, wie beim Start über das Formular.
- `deploy` mit Pfad spielt eine andere Datei ein, etwa eine Variante unter `prozess/varianten/`. Deployment und Ressource heißen dann wie die Datei, nicht wie der `ProzessKey`.
- Für den Bonus fachlicher Fehler lehnt die Simulation der Musterlösung jede Buchung über 50.000 Euro ab (`BudgetJeBuchung`) und speichert sie nicht. Die Ablehnung ist eine eigene Exception unter `Fachsystem/`, `BuchungAbgelehntException`. Der Handler bleibt, wie er ist, erst die Schleife macht aus der Exception ein `bpmnError`. Die Beträge in Meldung und Log stehen immer im deutschen Format, egal wie der Rechner eingestellt ist.
- Der Prozesstest in Java testet die Kopie der Entwickler-Fassung in `prozesstest-java/src/main/resources/`, nicht euer Modell in der Engine. Die GitHub Action hält die Kopie byte-gleich zu `loesung/genehmigungsworkflow-entwickler.bpmn`, ebenso die Kopie der Variante und die Kopien unter `loesung/prozesstest-java/`.
- Die Musterlösung ist ein vollständiges, baubares Projekt neben dem Startstand, kein Satz einzelner Dateien. `.github/scripts/loesung-abgleich.sh` prüft, auch in der GitHub Action: Die Lösung enthält jede Datei des Startstands und weicht nur in den Übungsdateien ab. Die Liste der Übungsdateien steht im Skript.
- Die Demo in Kapitel 10 läuft in der Musterlösung, `loesung/prozesstest-java/`, mit der Klasse `GenehmigungsworkflowTag1Test` am Modell ohne External Task. Der Trainer ändert vorübergehend nur `loesung/prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn` und setzt mit `git restore loesung/prozesstest-java` zurück. Der Startstand `prozesstest-java/` bleibt für die Übung unberührt, siehe [README des Projekts](../prozesstest-java/README.md#demo-kapitel-10-trainer).
- Im Java-Bonus holt der Test den External Task selbst mit `fetchAndLock` und antwortet mit `handleBpmnError` oder `complete`, wie der Worker. Einen Handler oder Fake aus C# braucht er nicht.
