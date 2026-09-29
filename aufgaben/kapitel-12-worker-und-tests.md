# Kapitel 12 · Übung: Der Worker

Tag 2, Entwickler-Track, Kapitel 12 „Worker und Tests“ (14:15 bis 15:15 Uhr, Hands-on 60 Minuten).

Euer Worker aus Kapitel 11 holt den Task schon, verbucht aber nichts. Am Ende verbucht er jede Genehmigung, und zwei Tests belegen das.

## Vorab

- Der Prozesstest nimmt den Prozess-Key als `_engine.ProzessKey` aus `appsettings.json`: `Process_Genehmigung` mit der Vorlage, euer eigener Key mit eurem Projekt. Auf der Folie steht an dieser Stelle `"mm-genehmigung"`.
- `betrag` kommt je nach Weg verschieden an: aus dem easyForm als Text, etwa `"1234.5"`, immer mit Punkt, per REST und im Prozesstest als Zahl (`long`). Der Handler muss beides lesen, Schritt 1 zeigt wie.

## Ausgangslage

Aus Kapitel 11 läuft der Stack, euer Projekt ist importiert, und die Zugangsdaten sind gesetzt. Prüft das kurz: In der Weboberfläche zeigt die Kachel „Prozess starten“ euren Prozess, und dieser Befehl zeigt `EngineBenutzer` und `EnginePasswort`:

```bash
dotnet user-secrets list --project src/GenehmigungWorker
```

Steigt ihr erst jetzt ein, macht zuerst die Schritte 2 bis 6 aus [Kapitel 11](kapitel-11-lokales-setup.md#das-macht-ihr).

Im Startstand tragen diese Dateien Kommentare `TODO Kapitel 12, Schritt ...`. Sie bauen, aber die Arbeit darin fehlt:

| Schritt | Datei | Stand |
|---|---|---|
| 1 | `src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs` | `Handle` wirft `NotImplementedException` |
| 2 | `tests/GenehmigungWorker.Tests/BuchungssystemFake.cs` | `Verbuchen` wirft `NotImplementedException` |
| 2 | `tests/GenehmigungWorker.Tests/GenehmigungVerbuchenHandlerTests.cs` | Unit-Test mit `Skip` |
| 3 | `src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` | `Verbuchen` wirft `NotImplementedException` |
| 3 | `src/GenehmigungWorker/Program.cs` | Skeleton-Schleife aus Kapitel 11 |
| 4 | `tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs` | Prozesstest und Gegenprobe mit `Skip` |

Fertig vorgegeben sind `Fachsystem/IBuchungssystem.cs` (die Signatur von `Verbuchen`), `ExternalTaskClient.cs` aus Kapitel 11 und der Test-Helfer `tests/GenehmigungWorker.Tests/EngineHelfer.cs`, im Prozesstest `_engine`. Alle TODOs findet ihr in VS Code mit Strg+Umschalt+F (macOS: Cmd+Umschalt+F) und dem Suchtext `TODO Kapitel 12`.

`dotnet test` meldet im Startstand 3 übersprungene Tests und keinen Fehler.

## Das macht ihr

Die Reihenfolge folgt der Empfehlung aus dem Kapitel: zuerst Handler und Unit-Test, die ohne Engine laufen, dann die Schleife, dann der Prozesstest, zum Schluss der Lauf über das Formular. Die Folien ab „Der Handler“ sind eure Hilfestellung.

### 1. Handler schreiben

Datei `src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs`, Folie „Der Handler“.

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
   Auf der Folie steht `Convert.ToDecimal(task.Variables["betrag"])`. Das reicht für Zahlen, aber nicht für den Text aus dem easyForm: Ohne `CultureInfo.InvariantCulture` liest ein Rechner mit deutscher Einstellung den Punkt in `"1234.5"` als Tausendertrennzeichen und verbucht 12.345 Euro statt 1.234,50 Euro.
3. `_buchung.Verbuchen(schluessel, antragsteller, betrag, begruendung)` rufen.
4. `new() { ["buchungsnummer"] = nummer }` zurückgeben.

### 2. Fake und Unit-Test

Dateien `tests/GenehmigungWorker.Tests/BuchungssystemFake.cs` und `GenehmigungVerbuchenHandlerTests.cs`, Folie „Unit-Test für den Handler“.

1. `BuchungssystemFake.Verbuchen` gibt immer `"B-2026-0001"` zurück.
2. Den Test `Verbucht_Genehmigung_und_liefert_Buchungsnummer` schreiben: Task von Hand bauen, Handler mit dem Fake, `Assert.Equal("B-2026-0001", ergebnis["buchungsnummer"])`.
3. Aus `[Fact(Skip = "...")]` wird `[Fact]`. Solange `Skip` dasteht, führt xUnit den Test nicht aus.

```bash
dotnet test --filter "Kategorie!=Prozesstest"
```

Erwartet: 1 Test bestanden, keiner fehlgeschlagen. Dieser Aufruf braucht weder Engine noch Zugangsdaten.

### 3. Simulation und Worker-Schleife

Dateien `src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` und `src/GenehmigungWorker/Program.cs`, Folie „Die Worker-Schleife“.

1. `BuchungssystemSimulation.Verbuchen` vergibt fortlaufende Nummern, `B-2026-0001`, dann `B-2026-0002` und so weiter, und schreibt jede Buchung ins Log (Schlüssel, `antragsteller`, `betrag`, `begruendung`, Nummer). Ein Zähler im Speicher reicht für den Anfang. Die Datei kommt im Bonus.
2. In `Program.cs` den Handler anlegen: `new GenehmigungVerbuchenHandler(new BuchungssystemSimulation())`, dazu oben `using GenehmigungWorker.Fachsystem;` und `using GenehmigungWorker.Handlers;`.
3. In der Schleife den `try`/`catch` von der Folie einsetzen: Erfolg meldet `CompleteAsync`, Fehler `FailureAsync` mit `task.Retries is int r ? r - 1 : 3` und `TimeSpan.FromMinutes(5)`.

Startet den Worker:

```bash
dotnet run --project src/GenehmigungWorker
```

Warten noch Anträge aus Kapitel 11 am Service Task, verbucht er sie gleich beim Start. Im Log steht eure Buchung, und der Task taucht nicht mehr alle 30 Sekunden wieder auf.

### 4. Prozesstest

Datei `tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs`, Folien „Prozesstest in fünf Schritten“ und „Prozesstest in C#“.

1. Stoppt euren Worker mit Strg+C. Er hört auf dasselbe Topic und würde dem Test den Task wegschnappen.
2. `Genehmigter_Antrag_wird_verbucht` schreiben: Starten, Warten (`Task_Pruefen`), Entscheiden (`genehmigt`), Verbuchen (Handler mit dem Fake), Beenden (`COMPLETED`, `buchungsnummer`). Nehmt `_engine.ProzessKey` und `_engine.Topic` statt der Texte von der Folie. Der Code steht als Kommentar in der Datei.
3. Die Methode wird `public async Task Genehmigter_Antrag_wird_verbucht()`, die Zeilen `Assert.Fail(...)` und `return Task.CompletedTask;` fallen weg, `Skip` auch.

```bash
dotnet test
```

Erwartet: Unit-Test und Prozesstest bestanden, keiner fehlgeschlagen. Die Gegenprobe bleibt übersprungen, bis ihr sie schreibt. Nur die Prozesstests startet `dotnet test --filter "Kategorie=Prozesstest"`.

Der Prozesstest braucht, was auch der Worker braucht: laufenden Stack, bereitgestelltes Modell, Zugangsdaten. Er liest dieselbe Konfiguration, auch dieselben User Secrets. Jeder Lauf startet eine eigene Instanz mit Business Key `prozesstest-...` und räumt am Ende auf.

### 5. End-to-end über das Formular

Folie „End-to-end: vom Formular bis zum Worker“.

1. Worker starten und das Log offen lassen: `dotnet run --project src/GenehmigungWorker`
2. Als `anna` einen Antrag stellen, als `gerda` „Antrag prüfen“ mit `genehmigt` abschließen, genau wie in [Kapitel 11, Schritt 8](kapitel-11-lokales-setup.md#8-antrag-stellen-und-genehmigen).
3. Nach wenigen Sekunden zeigt das Log den geholten Task, eure Buchung und das `complete`.
4. Prüfen im Cockpit, das in CIB flow in der Weboberfläche steckt: Die Liste der Prozesse öffnet ihr direkt unter http://localhost:7083/client/#/seven/auth/processes/list, dort „Genehmigungsworkflow“ wählen. Links in der „Versionshistorie“ ist die neueste Version gewählt. Stehen dort mehrere Versionen, etwa weil `deploy` nach dem Import Version 2 angelegt hat, wählt die Version, auf der eure Instanz lief. Im Reiter „Instanzen“ steht eure Instanz mit Enddatum. Das Augen-Symbol öffnet sie, der Reiter „Variablen“ zeigt `buchungsnummer`.

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
- [ ] Unit-Test und Prozesstest laufen grün: `dotnet test` meldet keinen Fehler.

## Hinweise

- In Kapitel 11 stand der `try`/`catch` mit `complete` und `failure` noch im Handler. Jetzt gibt der Handler nur das Ergebnis zurück, zurückgemeldet wird einmal in der Schleife. Deshalb lässt sich der Handler ohne Engine testen.
- `BusinessKey ?? ProcessInstanceId`: Das Startformular setzt keinen Business Key, der Worker loggt `Business Key (keiner)`. Im Formular-Lauf ist der Schlüssel deshalb die Prozessinstanz-ID. Der Prozesstest setzt seinen Business Key selbst.
- `failure`: Beim ersten Fehler ist `Retries` null, der Worker meldet 3 verbleibende Versuche, danach zählt er herunter. Dazwischen liegen fünf Minuten, bei 0 legt die Engine einen Incident an. Wie viele Versuche übrig sind und warum es scheiterte, zeigt `curl -u worker:worker "http://localhost:8080/engine-rest/external-task?topicName=genehmigung-verbuchen"` (PowerShell: `curl.exe`) in `retries` und `errorMessage`, Incidents seht ihr im Cockpit unter „Vorfälle“.
- Idempotenz: Scheitert `CompleteAsync` nach einer erfolgreichen Buchung, läuft der `catch`, der Task kommt erneut, und der Handler bucht ein zweites Mal. Dagegen hilft nur ein Fachsystem, das den Schlüssel kennt. Genau das baut ihr im Bonus.
- Habt ihr den Worker eben erst gestoppt, kann der Prozesstest rund 30 Sekunden brauchen. Die letzte Long-Polling-Anfrage des Workers bleibt in der Engine noch bis zu zehn Sekunden offen und kann den Task des Tests holen. Dann gehört er für 30 Sekunden dem gestoppten Worker. Der Test-Helfer fragt deshalb bis zu 45 Sekunden lang nach.
- Die Tests im Startstand stehen auf `Skip`, damit `dotnet test` von Anfang an sauber durchläuft. Ein übersprungener Test ist kein grüner Test.

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
| Im Cockpit steht `betrag` schon falsch, etwa `123450` statt `1234.50`, oder die Instanz aus dem Formular endet ohne `buchungsnummer` | Ihr habt den Betrag in einem Browser mit englischer Spracheinstellung mit Komma eingegeben, das Zahlenfeld hat das Komma verschluckt. Schreibt ihn dort mit Punkt, siehe [Kapitel 11, Schritt 8](kapitel-11-lokales-setup.md#8-antrag-stellen-und-genehmigen). Über 50.000 Euro lehnt die Simulation der Musterlösung ab, und die Vorlage beendet die Instanz dann still, siehe Bonus fachlicher Fehler. |
| Prozesstest: `Kein External Task auf Topic ... auch nicht nach 45 Sekunden` | Euer Worker läuft noch und hat den Task schon verbucht. Stoppt ihn. Oder die Instanz steht gar nicht am Service Task, dann stimmen Entscheidung oder Modell nicht. |
| Prozesstest: `lieferte 404 NotFound` beim Start, mit der Frage, ob das Modell bereitgestellt ist | Modell nicht bereitgestellt (Projekt-ZIP importieren, [Kapitel 11, Schritt 4](kapitel-11-lokales-setup.md#4-projekt-importieren)), oder `ProzessKey` passt nicht zur Process ID des Modells. |
| Prozesstest: `Die Engine unter http://localhost:8080/ antwortet nicht` | Der Stack läuft nicht oder startet noch. Im Ordner `stack/`: `docker compose up -d`, dann warten, bis `docker compose logs init` mit `[init] Fertig.` endet. |
| Prozesstest: `Zugangsdaten für die Engine fehlen` | Im Terminal fehlen die Umgebungsvariablen. Setzt sie oder nehmt User Secrets ([Kapitel 11, Schritt 6](kapitel-11-lokales-setup.md#6-appsettingsjson-prüfen-und-zugangsdaten-setzen)). |
| Prozesstest: `lieferte 401 Unauthorized` | Benutzer oder Passwort falsch. `dotnet user-secrets list --project src/GenehmigungWorker` zeigt, was gesetzt ist. |
| `dotnet test` meldet euren fertigen Test als übersprungen | `Skip` steht noch am `[Fact]`. |
| Die Instanz hängt am Service Task „Genehmigung verbuchen“ | In dieser Reihenfolge prüfen: Schreibweise des Topics in Modell und `appsettings.json`, `EngineUrl`, Lock (ein abgestürzter Worker hält ihn bis zu 30 Sekunden), `failure` mit fünf Minuten Pause. |

## Bonus: Idempotenz

Für alle, die schneller fertig sind. Stirbt der Worker nach dem Verbuchen und vor `complete`, läuft der Lock ab, und derselbe Task kommt erneut. Die Buchung darf dann nicht doppelt entstehen.

1. Die Simulation merkt sich Schlüssel und Nummer. Kommt derselbe Schlüssel noch einmal, liefert sie dieselbe Nummer, statt ein zweites Mal zu buchen.
2. Damit das einen Neustart des Workers übersteht, schreibt sie beides in eine Datei, nicht nur in den Speicher. Gebt den Dateipfad im Konstruktor mit. `Program.cs` nimmt eine Datei neben der DLL, etwa `Path.Combine(AppContext.BaseDirectory, "buchungen.json")`, der Test eine eigene Datei unter `Path.GetTempPath()`. `.gitignore` hält `buchungen.json` aus dem Repo.
3. Speichert die Buchung, bevor ihr die Nummer zurückgebt. Sonst hilft die Datei im entscheidenden Moment nicht.
4. Ein Unit-Test: zweimal derselbe Schlüssel, beide Male dieselbe Nummer, auch mit einer neuen Instanz der Simulation auf derselben Datei. Ein anderer Schlüssel bekommt die nächste Nummer.

Im Formular-Lauf ausprobieren:

1. Baut in `Program.cs` direkt nach `var ergebnis = handler.Handle(task);` vorübergehend eine Pause ein: `Thread.Sleep(TimeSpan.FromSeconds(20));`
2. Worker starten, Antrag stellen und genehmigen. Sobald das Log die Buchung zeigt, beendet ihr den Worker hart: Terminal schließen, in VS Code das Papierkorb-Symbol am Terminal. Strg+C reicht nicht, dann wartet der Worker die Pause ab und schickt `complete`.
3. Worker in einem neuen Terminal wieder starten. Sind die Zugangsdaten Umgebungsvariablen, setzt sie dort neu. Nach Ablauf des Locks, höchstens 30 Sekunden nach dem ersten Holen, kommt der Task erneut. Eure Simulation erkennt den Schlüssel, bucht nicht noch einmal, und der Task endet mit derselben Nummer. Die Musterlösung loggt dazu `Schlüssel ... ist schon verbucht als B-2026-0001, keine zweite Buchung.`
4. Pause wieder entfernen.

Der Schlüssel ist im Formular-Lauf die Prozessinstanz-ID, weil das Startformular keinen Business Key setzt.

## Bonus: Fachlicher Fehler

Der zweite Bonus, für alle, die noch Zeit haben. Nicht jeder Fehler ist technisch. Lehnt das Fachsystem eine Buchung ab, etwa weil das Budget der Kostenstelle nicht reicht, ändert ein zweiter Versuch nichts daran. Mit `failure` würde der Worker alle fünf Minuten dasselbe Nein abholen, bis die Engine einen Incident anlegt. Stattdessen meldet er einen BPMN-Fehler, und das Modell führt die Instanz auf einen eigenen Pfad.

Diesen Pfad hat nur die Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` aus Kapitel 04 und 11: der Ausschnitt ab „Genehmigung erteilt“. Am Service Task „Genehmigung verbuchen“ hängt das Error-Boundary „Buchung abgelehnt“ für den errorCode `BUCHUNG_ABGELEHNT`. Es legt Code und Grund in den Variablen `errorCode` und `errorMessage` ab und führt zu „Buchung klären“ für die Gruppe `genehmiger`. Die Variante hat eine eigene Process ID, `Process_VerbuchenFehlerpfad`, aber dasselbe Topic `genehmigung-verbuchen`. Euer Worker bedient deshalb beide Modelle, ohne dass ihr `appsettings.json` ändert.

1. **Simulation ablehnen lassen.** Legt unter `Fachsystem/` eine eigene Exception an, etwa `BuchungAbgelehntException`, mit dem Grund als Message. `BuchungssystemSimulation.Verbuchen` wirft sie direkt nach der Idempotenzprüfung, wenn der Betrag über einem festen Budget liegt, etwa 50.000 Euro je Buchung. Die Meldung nennt den Grund: `Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei`. Eine abgelehnte Buchung speichert die Simulation nicht. Am Handler ändert ihr nichts, die Exception fliegt durch ihn hindurch bis in die Schleife.
2. **Vierte Methode im `ExternalTaskClient`.** `BpmnErrorAsync(ExternalTask task, string errorCode, string meldung)` schickt `POST /engine-rest/external-task/{id}/bpmnError` mit `workerId`, `errorCode` und `errorMessage`. Vorbild ist `FailureAsync`.
3. **Catch in der Schleife.** In `Program.cs` direkt vor `catch (Exception ex)` ein `catch (BuchungAbgelehntException abgelehnt)` einsetzen. Es ruft `BpmnErrorAsync(task, "BUCHUNG_ABGELEHNT", abgelehnt.Message)` und schreibt eine Log-Zeile. Die Reihenfolge zählt: C# nimmt den ersten passenden `catch`, und `catch (Exception)` passt auf alles.
4. **Variante deployen.** Mit einem Pfad dahinter spielt `deploy` diese Datei ein statt `prozess/genehmigungsworkflow.bpmn`. Das Deployment heißt dann wie die Datei, die Vorlage bleibt, wie sie ist.
   ```bash
   dotnet run --project src/GenehmigungWorker -- deploy prozess/varianten/verbuchen-fehlerpfad.bpmn
   ```
   Beim ersten Mal meldet `deploy` `Neue Version: Process_VerbuchenFehlerpfad, Version 1`, danach „Modell unverändert“.
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

Warum die Variante? Mit 60.000 Euro aus dem Startformular kommt auch die Vorlage bis zu „Genehmigung verbuchen“, aber dort fängt kein Error-Boundary `BUCHUNG_ABGELEHNT`. Dann beendet die Engine die Instanz still am Service Task: Im Cockpit steht sie als abgeschlossen, ohne „Antrag genehmigt“ und ohne `buchungsnummer`. Es gibt keinen Vorfall, und den Grund findet ihr nur im Log der Engine: `docker compose logs flow-cibseven-spring` im Ordner `stack/` zeigt `ENGINE-02001 ... but no catching boundary event was defined. Execution is ended`. Ein `bpmnError` braucht also ein Boundary im Modell, das seinen Code fängt.

## Musterlösung

`loesung/` enthält die fertigen Fassungen aller Dateien, die sich gegenüber dem Startstand ändern oder neu dazukommen, unter denselben Pfaden:

| Datei | Was die Musterlösung macht |
|---|---|
| `loesung/src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs` | wie auf der Folie, dazu `betrag` kulturunabhängig als Text oder Zahl |
| `loesung/src/GenehmigungWorker/Program.cs` | Schleife von der Folie, dazu je eine Log-Zeile für geholt, erledigt und fehlgeschlagen, für den Bonus fachlicher Fehler `catch (BuchungAbgelehntException)` mit `bpmnError` |
| `loesung/src/GenehmigungWorker/ExternalTaskClient.cs` | wie im Startstand, dazu `BpmnErrorAsync` |
| `loesung/src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` | fortlaufende Nummern je Jahr, idempotent über `buchungen.json` neben der DLL, lehnt über 50.000 Euro je Buchung ab, Beträge in Meldung und Log immer deutsch |
| `loesung/src/GenehmigungWorker/Fachsystem/BuchungAbgelehntException.cs` | neu: die fachliche Ablehnung mit Grund |
| `loesung/tests/GenehmigungWorker.Tests/BuchungssystemFake.cs` | liefert `B-2026-0001` und merkt sich jeden Aufruf |
| `loesung/tests/GenehmigungWorker.Tests/GenehmigungVerbuchenHandlerTests.cs` | Test der Folie, ohne Business Key, `betrag` als Text `"1234.5"` und als Zahl `1200L` auf einem deutschen Rechner, fehlende Variable, Idempotenz der Simulation, Ablehnung über dem Budget in Simulation und Handler |
| `loesung/tests/GenehmigungWorker.Tests/ExternalTaskClientTests.cs` | neu: `BpmnErrorAsync` schickt Pfad und Body, ohne Engine |
| `loesung/tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs` | Prozesstest der Folie und Gegenprobe mit `abgelehnt` |
| `loesung/tests/GenehmigungWorker.Tests/FehlerpfadTests.cs` | neu: Prozesstest gegen die Variante, deployt sie selbst und prüft „Buchung klären“ mit `errorCode` und `errorMessage` |

**Vergleichen:** In VS Code beide Dateien im Explorer markieren, Rechtsklick, „Ausgewählte vergleichen“. Oder im Terminal, bash und PowerShell gleich:

```bash
git diff --no-index src/GenehmigungWorker/Program.cs loesung/src/GenehmigungWorker/Program.cs
```

**Übernehmen:** Im Repo-Root kopiert ihr die Musterlösung über den Startstand. Das überschreibt eure Fassungen dieser Dateien, sichert oder committet sie vorher.

```bash
# bash, zsh, Git Bash
cp -R loesung/src loesung/tests .
```

```powershell
# PowerShell
Copy-Item -Path loesung\src, loesung\tests -Destination . -Recurse -Force
```

Einzelne Dateien übernehmt ihr genauso, etwa `cp loesung/src/GenehmigungWorker/Program.cs src/GenehmigungWorker/` (PowerShell: `Copy-Item loesung\src\GenehmigungWorker\Program.cs src\GenehmigungWorker\`). Achtet dabei auf Paare, die zusammengehören:

- `Program.cs` ruft den Konstruktor der Simulation mit Dateipfad auf, fängt `BuchungAbgelehntException` und ruft `BpmnErrorAsync`. Übernehmt `BuchungssystemSimulation.cs`, `Fachsystem/BuchungAbgelehntException.cs` und `ExternalTaskClient.cs` mit.
- `GenehmigungVerbuchenHandlerTests.cs` braucht den Fake der Musterlösung (`Aufrufe`), ihre Simulation und die Exception.
- `ExternalTaskClientTests.cs` braucht den `ExternalTaskClient` der Musterlösung, `FehlerpfadTests.cs` dazu Simulation und Exception.

Mit der Musterlösung laufen `dotnet test --filter "Kategorie!=Prozesstest"` ohne Engine (9 Tests) und `dotnet test` mit laufendem Stack und bereitgestelltem Modell (12 Tests) grün. Die Variante für den Prozesstest zum fachlichen Fehler deployt der Test selbst. Genau das prüft auch die GitHub Action des Repos bei jedem Push.

Zurück zum Startstand kommt ihr mit `git restore src tests` und `git clean -fd src tests`, in bash und PowerShell gleich. Das verwirft alle eure Änderungen in diesen Ordnern und löscht Dateien, die dort neu dazugekommen sind, auch eure eigenen. Ohne `git clean` bleiben etwa `ExternalTaskClientTests.cs` und `FehlerpfadTests.cs` aus der Musterlösung liegen, und der Startstand baut nicht mehr.

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

Kommt derselbe Schlüssel noch einmal, meldet die Simulation `Schlüssel ... ist schon verbucht als B-2026-0001, keine zweite Buchung.`, und der Worker schließt den Task mit derselben Nummer ab. Scheitert der Handler, etwa an einer fehlenden Variablen, schickt der Worker `failure`: beim ersten Mal mit 3 verbleibenden Versuchen, danach herunterzählend, dazwischen fünf Minuten Pause. Bei 0 legt die Engine einen Incident an. Liegt der Betrag über 50.000 Euro, lehnt die Simulation ab, und der Worker meldet `bpmnError` mit `BUCHUNG_ABGELEHNT` und dem Grund als `errorMessage`. In der Variante `prozess/varianten/verbuchen-fehlerpfad.bpmn` wartet danach „Buchung klären“. In der Vorlage fängt kein Error-Boundary den Fehler, die Engine beendet die Instanz dann still am Service Task, ohne Incident.

## Zum Nachschlagen

### Aufbau von Worker und Tests

```
src/GenehmigungWorker/
├── Program.cs                           # Worker-Schleife, mit "deploy" das Deployment
├── ExternalTaskClient.cs                # fetchAndLock, complete, failure und der Record ExternalTask
├── Deploy.cs                            # spielt prozess/genehmigungsworkflow.bpmn ein, mit Pfad eine andere Datei
├── Einstellungen.cs                     # liest appsettings.json, User Secrets und Umgebung
├── Handlers/
│   └── GenehmigungVerbuchenHandler.cs   # lesen, verbuchen, Ergebnis zurückgeben
├── Fachsystem/
│   ├── IBuchungssystem.cs               # die eine Stelle nach außen
│   └── BuchungssystemSimulation.cs      # simulierte Buchung statt echtem Fachsystem
└── appsettings.json                     # EngineUrl, ProzessKey, Topic, WorkerId
tests/GenehmigungWorker.Tests/
├── GenehmigungVerbuchenHandlerTests.cs  # Unit-Test für den Handler, ohne Engine
├── BuchungssystemFake.cs                # Fake statt Fachsystem
├── GenehmigungsworkflowTests.cs         # Prozesstest per REST gegen eure lokale Engine
└── EngineHelfer.cs                      # Test-Helfer für den Prozesstest, fertig vorgegeben
```

### Tests

```bash
dotnet test                                               # alle Tests, die Prozesstests brauchen die Engine
dotnet test --filter "Kategorie!=Prozesstest"             # nur die Unit-Tests, ohne Engine
dotnet test --filter "Kategorie=Prozesstest"              # nur die Prozesstests
```

Die Unit-Tests brauchen weder Engine noch Zugangsdaten und laufen in Millisekunden. Die Prozesstests tragen `[Trait("Kategorie", "Prozesstest")]` und laufen per REST gegen die lokale Engine: Der Stack muss laufen, das Modell bereitgestellt, die Zugangsdaten gesetzt (dieselben wie für den Worker, auch als User Secrets) und euer Worker gestoppt sein. Jeder Test startet eine eigene Instanz mit Business Key `prozesstest-...`, holt den External Task mit Filter auf diesen Business Key unter der Worker-ID `prozesstest` und löscht am Ende, was von seinen Instanzen noch offen ist. Läuft die Engine nicht, fehlt das Modell oder stimmen die Zugangsdaten nicht, nennt die Fehlermeldung des Tests die Ursache und den nächsten Schritt.

| Stand | `dotnet test --filter "Kategorie!=Prozesstest"` | `dotnet test` mit laufendem Stack |
|---|---|---|
| Startstand | 1 übersprungen | 3 übersprungen |
| Musterlösung | 9 bestanden | 12 bestanden |

Den Test-Helfer `EngineHelfer.cs` (im Test `_engine`) bekommt ihr fertig: je Methode ein REST-Endpunkt, etwa `StartAsync`, `GetTaskAsync`, `CompleteTaskAsync`, `FetchAndLockAsync`, `CompleteAsync`, `GetHistoryAsync`, `GetVariableAsync` und für die Gegenprobe `GetExternalTasksAsync`. Die lesenden Methoden warten auf den Zustand, statt nur einmal zu fragen: `GetTaskAsync` fragt bis zu zehn Sekunden lang nach, bis die Aufgabe da ist. `GetHistoryAsync`, `GetVariableAsync` und `GetExternalTasksAsync` warten vorher, bis an der Instanz kein Speicherpunkt mehr aussteht. Das braucht die Vorlage: Der easyForm-Baustein setzt nach dem Start-Event und nach „Antrag prüfen“ je einen Speicherpunkt, die Engine antwortet dort schon, und den Rest führt ihr Job Executor kurz danach im Hintergrund aus. Kommt der Zustand nicht, nennt die Fehlermeldung, was erwartet war und wo die Instanz steht. `FetchAndLockAsync` fragt bis zu 45 Sekunden lang nach, statt nach einem leeren fetchAndLock sofort aufzugeben: Die letzte Long-Polling-Anfrage eines eben gestoppten Workers bleibt in der Engine bis zu zehn Sekunden offen und kann den Task des Tests noch für 30 Sekunden sperren.

### Entscheidungen, wo die Folien offen sind

- `Einstellungen.cs` liest die Konfiguration und baut den `HttpClient` mit `EngineUrl` als `BaseAddress` und Basic Auth. Worker, Deployment und Prozesstest nutzen sie gemeinsam.
- `lockDuration` (30 s), `maxTasks` (5) und `asyncResponseTimeout` (10 s) stehen wie auf der Folie im Code von `FetchAndLockAsync`, nicht in `appsettings.json`.
- `ExternalTask.Variables` ist ein `Dictionary<string, object>` mit ausgepackten Werten: Text als `string`, ganze Zahlen als `long`, andere Zahlen als `double`, Wahrheitswerte als `bool`. Variablen ohne Wert lässt `ToTask` weg, etwa die Anlage aus dem easyForm, der Handler scheitert dann laut an einer fehlenden Variable. So baut der Handler von der Folie ohne Nullable-Warnungen.
- `betrag` liest der Handler der Musterlösung als Text mit `decimal.Parse` und als Zahl mit `Convert.ToDecimal`, beides mit `CultureInfo.InvariantCulture`. Das easyForm speichert auch ein Feld vom Typ „Zahl“ als Text.
- `CompleteAsync` schickt die Werte typisiert: `string` als String, `int` als Integer, `long` als Long, `decimal` und `double` als Double, `bool` als Boolean.
- `IBuchungssystem` und die Simulation liegen unter `Fachsystem/`, getrennt von den Handlern.
- Der Prozesstest nimmt Prozess-Key und Topic aus der Konfiguration (`_engine.ProzessKey`, `_engine.Topic`). Auf der Folie stehen sie ausgeschrieben.
- Die Tests im Startstand sind mit `Skip` markiert statt rot. So läuft `dotnet test` von Anfang an sauber durch, und ihr seht, welche Tests noch fehlen.
- Die Prozesstests tragen den Trait `Kategorie=Prozesstest`. Damit trennt `--filter` sie von den Unit-Tests, etwa auf einem Rechner ohne Engine.
- Die Simulation speichert ihre Buchungen in `buchungen.json` neben der DLL (`src/GenehmigungWorker/bin/Debug/net10.0/`). Den Pfad gibt `Program.cs` im Konstruktor mit und loggt ihn beim Start. So findet der Worker die Datei, egal wo ihr ihn startet, und sie landet nie im Repo. Löscht ihr die Datei, beginnen die Nummern wieder bei 0001. Gezählt wird je Kalenderjahr: B-2026-0001, B-2026-0002 und so weiter.
- Die Simulation speichert die Buchung, bevor sie die Nummer zurückgibt. Stirbt der Worker zwischen Verbuchen und `complete`, bekommt der nächste Versuch dieselbe Nummer.
- Die Schleife in der Musterlösung ist die von der Folie, ergänzt um je eine Log-Zeile für geholt, erledigt und fehlgeschlagen.
- Der Unit-Test-Fake merkt sich seine Aufrufe. Damit prüft ein Test, dass der Handler ohne Business Key unter der Prozessinstanz-ID verbucht, wie beim Start über das Formular.
- `deploy` mit Pfad spielt eine andere Datei ein, etwa eine Variante unter `prozess/varianten/`. Deployment und Ressource heißen dann wie die Datei, nicht wie der `ProzessKey` der Vorlage.
- Für den Bonus fachlicher Fehler lehnt die Simulation der Musterlösung jede Buchung über 50.000 Euro ab (`BudgetJeBuchung`) und speichert sie nicht. Die Ablehnung ist eine eigene Exception unter `Fachsystem/`, `BuchungAbgelehntException`. Der Handler bleibt, wie er ist, erst die Schleife macht aus der Exception ein `bpmnError`. Die Beträge in Meldung und Log stehen immer im deutschen Format, egal wie der Rechner eingestellt ist.
