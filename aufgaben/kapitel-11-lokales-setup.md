# Kapitel 11 · Übung: Lokales Setup

Tag 2, Entwickler-Track, am Ende von Kapitel 11 „External Tasks“ (13:30 bis 14:15 Uhr).

Ab jetzt läuft alles auf eurem Laptop: die lokale Instanz mit eurem Projekt und der Worker. Er holt den Task „Genehmigung verbuchen“, verbucht aber noch nichts. Das Verbuchen und die Tests folgen in [Kapitel 12](kapitel-12-worker-und-tests.md).

## Übergangs-Setup: CIB seven statt CIB flow

Bis das CIB flow Setup bereitsteht, startet der Ordner `stack/` eine lokale CIB seven Instanz. Deshalb läuft einiges anders als auf den Folien:

| Auf den Folien | Im Übergangs-Setup |
|---|---|
| Lokale CIB flow Instanz | CIB seven Run 2.2.0 mit PostgreSQL, gestartet per Docker Compose aus `stack/` |
| Projekt in der gemeinsamen Umgebung als ZIP exportieren und lokal importieren | Entfällt. Das Modell liegt schon unter `prozess/genehmigungsworkflow.bpmn`. |
| Euer Startformular und euer Genehmigungsformular (easyForms) | Generated Forms in der CIB seven Webapp: „Betrag in Euro“ und „Grund des Antrags“ beim Start, „Entscheidung“ bei „Antrag prüfen“ |
| Euer eigener Key, z. B. `mm-genehmigung` | `Process_Genehmigung`, die Process ID der Vorlage |
| Euer Benutzer muss lokal in der Gruppe `genehmiger` sein | Der Dienst `init` legt `anna` (stellt Anträge) und `gerda` (Gruppe `genehmiger`) an |
| `java -version` zeigt 21.x | Nicht nötig, die Engine läuft im Container |

Topic, Variablen und IDs sind die der Vorlage aus der Schulung: Topic `genehmigung-verbuchen`, Variablen `antragsteller`, `betrag`, `begruendung` und `entscheidung`, Aufgabe `Task_Pruefen`.

## Ausgangslage

Den Code für diese Übung bekommt ihr fertig. Ihr schreibt hier noch nichts, ihr bringt alles zum Laufen und schaut dem Worker zu.

| Datei | Was drinsteht | Folie |
|---|---|---|
| `src/GenehmigungWorker/appsettings.json` | `EngineUrl`, `ProzessKey`, `Topic`, `WorkerId` | Projektstruktur für den C#-Worker |
| `src/GenehmigungWorker/Deploy.cs` | spielt `prozess/genehmigungsworkflow.bpmn` per Multipart-Request ein | Deployment aus der IDE |
| `src/GenehmigungWorker/ExternalTaskClient.cs` | `FetchAndLockAsync`, `CompleteAsync`, `FailureAsync` und der Record `ExternalTask` | fetchAndLock in C#, complete und failure |
| `src/GenehmigungWorker/Program.cs` | Skeleton-Schleife: holt Tasks und loggt sie, schickt aber kein `complete` | |

Ihr braucht eine Container-Laufzeit mit Compose (Docker Desktop, Podman Desktop oder Rancher Desktop), das .NET SDK 10, Git und VS Code. Port 8080 muss frei sein.

## Das macht ihr

Alle Befehle laufen im Repo-Root, außer es steht etwas anderes dabei. Wo sich bash (auch zsh und Git Bash) und PowerShell unterscheiden, stehen beide Varianten da.

### 1. Werkzeuge prüfen

```bash
docker compose version     # Podman: podman compose version
dotnet --version           # 10.0.x
git --version
```

### 2. Repo klonen und die lokale Instanz starten

```bash
git clone https://github.com/miragon-trainings/cibflow-developer-training-exercises.git
cd cibflow-developer-training-exercises/stack
docker compose up -d
```

Beim ersten Start lädt Compose die Images, rund 1,6 GB. Danach braucht die Engine ein bis zwei Minuten. Fertig ist sie, wenn im Ordner `stack/` gilt:

- `docker compose ps -a` zeigt `cibseven` und `postgres` als `healthy` und `init` als `Exited (0)`.
- `docker compose logs init` endet mit `[init] Fertig. Benutzer: anna, gerda (Gruppe genehmiger), worker. Passwort jeweils wie der Benutzername.`

Geht danach zurück in den Repo-Root: `cd ..`

Während ihr wartet, lest `ExternalTaskClient.cs` und `Deploy.cs`. Ihr findet dort den Code von den Folien wieder.

### 3. Bauen

```bash
dotnet build
```

Der Build baut Worker und Tests und endet ohne Fehler und ohne Warnungen.

### 4. appsettings.json prüfen und Zugangsdaten setzen

Im Übergangs-Setup passt `src/GenehmigungWorker/appsettings.json` schon:

| Schlüssel | Wert | Wann ihr ihn ändert |
|---|---|---|
| `EngineUrl` | `http://localhost:8080` | Engine auf einem anderen Port, später die Adresse eurer lokalen CIB flow Instanz. Immer ohne `/engine-rest`. |
| `ProzessKey` | `Process_Genehmigung` | Mit eigenem Modell: dessen Process ID, z. B. `mm-genehmigung` |
| `Topic` | `genehmigung-verbuchen` | Nie, es muss exakt wie im Modell heißen |
| `WorkerId` | `genehmigung-worker-1` | Für eine zweite Worker-Instanz |

Die Zugangsdaten für `/engine-rest` gehören nicht in diese Datei. Lokal nehmt ihr den Benutzer `worker` mit Passwort `worker`. Am bequemsten sind User Secrets: Sie liegen in eurem Benutzerprofil, nicht im Repo, und gelten in jedem Terminal. Ihr braucht gleich zwei Terminals.

```bash
# bash und PowerShell gleich
dotnet user-secrets set EngineBenutzer worker --project src/GenehmigungWorker
dotnet user-secrets set EnginePasswort worker --project src/GenehmigungWorker
dotnet user-secrets list --project src/GenehmigungWorker    # zeigt beide Werte
```

Oder als Umgebungsvariablen. Die gelten nur im Terminal, in dem ihr sie setzt:

```bash
# bash, zsh, Git Bash
export EngineBenutzer=worker EnginePasswort=worker
```

```powershell
# PowerShell
$env:EngineBenutzer = "worker"; $env:EnginePasswort = "worker"
```

### 5. Projekt exportieren und importieren (erst mit CIB flow)

Im Übergangs-Setup überspringt ihr diesen Schritt. Mit dem CIB flow Setup läuft er so:

1. In der gemeinsamen CIB flow Umgebung exportiert ihr euer Projekt im Prozessmanagement als ZIP, mit dem Stand der Formulare von heute Vormittag.
2. In der Prozessmanagement-Ansicht der lokalen Instanz importiert ihr die ZIP. easyForms und Ressourcen sind danach sofort da, das Diagramm muss noch in die Engine.
3. Das BPMN aus dem Ordner `diagrams` der ZIP legt ihr als `prozess/genehmigungsworkflow.bpmn` ab. Diesen Pfad liest `Deploy.cs`.
4. Die Process ID eures Modells tragt ihr als `ProzessKey` in `appsettings.json` ein. Prüft im Modell: Der Service Task „Genehmigung verbuchen“ ist External mit Topic `genehmigung-verbuchen`.
5. Die ZIP enthält keine Benutzer und Gruppen. Euer lokaler Benutzer muss in der Gruppe `genehmiger` sein, sonst taucht „Antrag prüfen“ nicht in seiner Tasklist auf.

### 6. Modell deployen

```bash
dotnet run --project src/GenehmigungWorker -- deploy
```

Im Ordner `src/GenehmigungWorker` genügt `dotnet run -- deploy`. Die Ausgabe nennt die neue Version:

```
Deployment 3f2a9c1e-... aus .../prozess/genehmigungsworkflow.bpmn
Neue Version: Process_Genehmigung, Version 1
```

Ruft ihr `deploy` ein zweites Mal auf, meldet es `Modell unverändert, die Engine hat keine neue Version angelegt.` Das ist `enable-duplicate-filtering` von der Folie.

### 7. Worker starten

```bash
dotnet run --project src/GenehmigungWorker
```

```
13:58:02 Worker genehmigung-worker-1 holt Tasks vom Topic genehmigung-verbuchen bei http://localhost:8080. Beenden mit Strg+C.
```

Lasst dieses Terminal offen. Für die nächsten Schritte nehmt ihr den Browser und ein zweites Terminal.

### 8. Antrag stellen und genehmigen

1. http://localhost:8080/webapp/ öffnen und als `anna` mit Passwort `anna` anmelden.
2. „Prozess starten“, bei „Genehmigungsworkflow“ auf Starten klicken. Im Startformular „Betrag in Euro“ und „Grund des Antrags“ ausfüllen, dann „Starten“.
3. Abmelden (oben rechts über den Namen) und als `gerda` mit Passwort `gerda` anmelden.
4. Tasklist öffnen, links den Filter „Aufgaben meiner Gruppen“ wählen, dann die Aufgabe „Antrag prüfen“.
5. „Mir zuweisen“ klicken, bei „Entscheidung“ `genehmigt` wählen und „Abschließen“ klicken.

### 9. Dem Worker zuschauen

Wenige Sekunden nach dem Abschließen loggt der Worker den Task mit seinen Variablen:

```
13:59:40 Task 9bb40e5d-... geholt: Business Key (keiner), Prozessinstanz 9b811764-..., Retries (noch keine)
13:59:40   antragsteller = anna
13:59:40   betrag = 1200
13:59:40   entscheidung = genehmigt
13:59:40   begruendung = Dienstreise zur Fachtagung
14:00:10 Task 9bb40e5d-... geholt: Business Key (keiner), Prozessinstanz 9b811764-..., Retries (noch keine)
```

Der Worker schickt noch kein `complete`. Nach 30 Sekunden läuft der Lock ab, und derselbe Task kommt erneut: die doppelte Auslieferung von der Folie „Warum Worker idempotent sein müssen“.

Wer den Lock gerade hält, zeigt die Engine. Im zweiten Terminal:

```bash
# bash, zsh, Git Bash
curl -u worker:worker "http://localhost:8080/engine-rest/external-task?topicName=genehmigung-verbuchen"
```

```powershell
# PowerShell: curl.exe, nicht curl
curl.exe -u worker:worker "http://localhost:8080/engine-rest/external-task?topicName=genehmigung-verbuchen"
```

In der Antwort stehen `"workerId":"genehmigung-worker-1"` und `lockExpirationTime`, der Zeitpunkt, an dem der Lock abläuft.

### 10. Worker stoppen

Strg+C im Terminal des Workers. Er meldet `Worker beendet.` Die Instanz wartet weiter am Service Task „Genehmigung verbuchen“. In Kapitel 12 verbucht euer Worker sie.

## Fertig, wenn

- [ ] Das Startformular startet den Prozess auf eurer lokalen Instanz, und „Antrag prüfen“ liegt bei `gerda`.
- [ ] Der Worker loggt den geholten Task mit seinen Variablen `antragsteller`, `betrag`, `begruendung` und `entscheidung`.
- [ ] `GET /engine-rest/external-task` zeigt eure Worker-ID `genehmigung-worker-1` am Task (Befehl in Schritt 9).

## Hinweise

- Auf eurer Instanz läuft nur euer Worker. `fetchAndLock` braucht deshalb nur das Topic, keinen weiteren Filter.
- `antragsteller` legt das Start-Event selbst ab (`camunda:initiator`): Es ist der Benutzer, der den Prozess gestartet hat, hier `anna`.
- `betrag` kommt als ganze Zahl an, weil das Feld im Startformular vom Typ `long` ist.
- Einen Business Key setzt das Startformular nicht, deshalb loggt der Worker `Business Key (keiner)`. Warum das in Kapitel 12 wichtig wird, zeigt die Folie „Der Handler“.
- Solange euer Worker läuft, holt er auch die Tasks, die ihr mit der http-Datei (Schritte 6 und 7) oder dem Smoke-Test holen wollt. Stoppt ihn vorher.
- Jeden Schritt einzeln als REST-Call seht ihr in `http/genehmigungsworkflow.http` (README, Abschnitt „Durchlauf mit der http-Datei“).

## Typische Stolpersteine

| Was ihr seht | Woran es liegt, was ihr tut |
|---|---|
| `docker compose up -d` bricht ab mit `address already in use` | Port 8080 ist belegt. Lösung in der README unter „Fehlerbilder“. |
| Webapp und REST-API antworten nicht | Die Engine startet noch. `docker compose ps` zeigt `health: starting`, wartet auf `healthy`. |
| Der Worker bricht sofort ab: `Zugangsdaten für die Engine fehlen` | Schritt 4 fehlt, oder ihr habt Umgebungsvariablen in einem anderen Terminal gesetzt. User Secrets gelten überall. |
| Der Worker endet mit `Response status code does not indicate success: 401.` | Benutzer oder Passwort falsch. `dotnet user-secrets list --project src/GenehmigungWorker` zeigt, was gesetzt ist. |
| Der Worker endet mit `HttpRequestException` und `Connection refused` | Die Engine läuft nicht, oder `EngineUrl` zeigt woandershin. |
| „Genehmigungsworkflow“ fehlt unter „Prozess starten“ | Das Modell ist nicht deployt. Schritt 6. |
| `deploy` meldet `prozess/genehmigungsworkflow.bpmn nicht gefunden` | Ihr startet außerhalb des Repos. Startet im Repo-Root oder in `src/GenehmigungWorker`. |
| Die Instanz steht bei „Genehmigung verbuchen“, der Worker loggt nichts | Tippfehler im Topic, in `appsettings.json` oder im Modell. Der `curl`-Befehl aus Schritt 9 ohne `?topicName=...` zeigt alle wartenden External Tasks mit ihrem `topicName`. |
| „Antrag prüfen“ fehlt in der Tasklist | Als `gerda` angemeldet? Filter „Aufgaben meiner Gruppen“ gewählt? Mit CIB flow: Ist euer Benutzer lokal in der Gruppe `genehmiger`? |
| Das Formular von „Antrag prüfen“ ist ausgegraut | Die Aufgabe ist euch noch nicht zugewiesen. „Mir zuweisen“ klicken. |
| Mit CIB flow: Die Engine läuft unter einem Pfad-Präfix, etwa `http://host/prefix/engine-rest` | Die Pfade im Code beginnen mit `/`, deshalb verwirft der `HttpClient` das Präfix aus `EngineUrl`. Setzt `EngineUrl` mit Präfix und abschließendem Slash und schreibt die Pfade im Code ohne führenden Slash. Im Übergangs-Setup gibt es kein Präfix. |

Weitere Fehlerbilder rund um den Stack stehen in der [README](../README.md#fehlerbilder).

## Wer früher fertig ist

Verkürzt die Lock-Dauer und beobachtet, wann der Task erneut kommt:

1. In `src/GenehmigungWorker/ExternalTaskClient.cs` in `FetchAndLockAsync` `lockDuration = 30_000` auf `10_000` setzen.
2. Worker neu starten. Der wartende Task aus Schritt 8 kommt, sobald der alte Lock abgelaufen ist, danach alle zehn Sekunden erneut.
3. Wieder auf `30_000` zurückstellen.

Überlegt dabei: Was passiert, wenn die Arbeit länger dauert als der Lock? Die Antwort steht auf der Folie „Topic, Lock, Retry, Timeout“.

## Musterlösung

Für Kapitel 11 gibt es keine eigene Musterlösung: Der Startstand im Repo ist das Ende dieser Übung. Die Musterlösung unter `loesung/` gehört zu [Kapitel 12](kapitel-12-worker-und-tests.md#musterlösung).

Weiter mit [Kapitel 12 · Übung: Der Worker](kapitel-12-worker-und-tests.md).
