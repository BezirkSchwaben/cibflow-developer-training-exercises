# Kapitel 11 · Übung: Lokales Setup

Tag 2, Entwickler-Track, am Ende von Kapitel 11 „External Tasks“ (13:30 bis 14:15 Uhr).

Ab jetzt läuft alles auf eurem Laptop: CIB flow mit eurem Projekt und der Worker. Er holt den Task „Genehmigung verbuchen“, verbucht aber noch nichts. Das Verbuchen und die Tests folgen in [Kapitel 12](kapitel-12-worker-und-tests.md).

Topic, Variablen und IDs sind die aus der Schulung: Topic `genehmigung-verbuchen`, Variablen `antragsteller`, `betrag`, `begruendung` und `entscheidung`, Aufgabe `Task_Pruefen`. Im Repo liegt die Vorlage mit der Process ID `Process_Genehmigung`. Arbeitet ihr mit eurem eigenen Projekt, tragt ihr in Schritt 6 euren Key ein, z. B. `mm-genehmigung`.

## Ausgangslage

Den Code für diese Übung bekommt ihr fertig. Ihr schreibt hier noch nichts, ihr bringt alles zum Laufen und schaut dem Worker zu.

| Datei | Was drinsteht | Folie |
|---|---|---|
| `src/GenehmigungWorker/appsettings.json` | `EngineUrl`, `ProzessKey`, `Topic`, `WorkerId` | Projektstruktur für den C#-Worker |
| `src/GenehmigungWorker/Einstellungen.cs` | liest `appsettings.json`, User Secrets und Umgebung, baut den `HttpClient` | |
| `src/GenehmigungWorker/ExternalTaskClient.cs` | `FetchAndLockAsync`, `CompleteAsync`, `FailureAsync` und der Record `ExternalTask` | fetchAndLock in C#, complete und failure |
| `src/GenehmigungWorker/Deploy.cs` | spielt `prozess/genehmigungsworkflow.bpmn` per Multipart-Request ein | Deployment aus der IDE |
| `src/GenehmigungWorker/Program.cs` | Skeleton-Schleife: holt Tasks und loggt sie, schickt aber kein `complete` | |

Ihr braucht Docker Desktop mit mindestens 8 GB Speicher für Docker, die Zugangsdaten für `harbor.cib.de` aus der Setup-Mail, das .NET SDK 10, Git und VS Code. Die Ports 8080, 7083, 7086 und 7088 bis 7091 müssen frei sein.

## Das macht ihr

Alle Befehle laufen im Repo-Root, außer es steht etwas anderes dabei. Wo sich bash (auch zsh und Git Bash) und PowerShell unterscheiden, stehen beide Varianten da.

### 1. Werkzeuge prüfen

```bash
docker compose version
dotnet --version           # 10.0.x
git --version
```

### 2. Repo klonen und CIB flow starten

```bash
git clone https://github.com/miragon-trainings/cibflow-developer-training-exercises.git
cd cibflow-developer-training-exercises/stack
docker login harbor.cib.de      # Benutzer und Passwort aus der Setup-Mail
docker compose up -d
```

Beim ersten Start lädt Docker sieben Images, zusammen mehrere GB. Danach braucht der Stack etwa eine Minute. Fertig ist er, wenn im Ordner `stack/` gilt:

- `docker compose ps -a` zeigt sieben Dienste mit `Up` und die Hilfsdienste `rechte` und `init` mit `Exited (0)`.
- `docker compose logs init` endet mit `[init] Fertig. Benutzer: anna, gerda (Gruppe genehmiger), worker. Passwort jeweils wie der Benutzername.`
- Die REST-API der Engine antwortet ohne Anmeldung mit 401, mit Anmeldung mit ihrer Version:
  ```bash
  curl -i http://localhost:8080/engine-rest/version                  # HTTP/1.1 401
  curl -u worker:worker http://localhost:8080/engine-rest/version    # {"version":"2.1.4-ee"}
  ```
  In der Windows PowerShell schreibt ihr `curl.exe` statt `curl`.

Geht danach zurück in den Repo-Root: `cd ..`

Während ihr wartet, lest `ExternalTaskClient.cs` und `Deploy.cs`. Ihr findet dort den Code von den Folien wieder. Konten, Adressen und Probleme mit dem Stack stehen in [stack/README.md](../stack/README.md).

### 3. Anmelden

http://localhost:7083/client öffnen und als `demo` mit Passwort `demo` anmelden. Die Startseite zeigt die Kacheln „Aufgaben bearbeiten“, „Cockpit“, „Admin“, „Prozess modellieren“, „Easy Form“, „Ressourcen“ und „Prozessmanagement“. „Prozess starten“ kommt dazu, sobald ein Prozess bereitgestellt ist.

### 4. Projekt importieren

Ihr importiert euer Projekt als ZIP. Damit kommen Modell und easyForms auf einmal in die lokale Instanz.

- **Euer eigenes Projekt:** In der gemeinsamen CIB flow Umgebung öffnet ihr euer Projekt im Prozessmanagement und ladet es mit „Projekt herunterladen“ als ZIP herunter, mit dem Stand der Formulare von heute Vormittag.
- **Kein eigenes ZIP zur Hand:** Nehmt `prozess/genehmigungsworkflow-projekt.zip` aus dem Repo. Darin stecken die Vorlage `prozess/genehmigungsworkflow.bpmn` und die beiden easyForms `antragsformular` (Betrag, Begründung, Anlage) und `genehmigungsformular` (Entscheidung).

So importiert ihr, angemeldet als `demo`:

1. Kachel „Prozessmanagement“, dann „Lokale Datei importieren“.
2. Die ZIP auswählen. Der Schalter „Automatisch bereitstellen“ steht schon an, lasst ihn so.
3. „Importieren“ klicken.

Das Ergebnis zeigt unter „Erstellte Diagramme“ das Modell, unter „Erstellte Formulare“ die easyForms und unter „AUTO-DEPLOY“ `1 Diagramm(e) erfolgreich bereitgestellt.` Das Projekt heißt wie die ZIP-Datei. Damit ist das Modell in der Engine, ein `deploy` aus dem Repo braucht ihr dafür nicht.

Mit eurem eigenen Projekt prüft ihr zusätzlich:

- Der Service Task „Genehmigung verbuchen“ ist External mit Topic `genehmigung-verbuchen`.
- „Antrag prüfen“ geht an die Gruppe `genehmiger`. Die ZIP enthält keine Benutzer und Gruppen, lokal sind `gerda` in `genehmiger` und `anna` als Antragstellerin angelegt.

### 5. Bauen

```bash
dotnet build
```

Der Build baut Worker und Tests und endet ohne Fehler und ohne Warnungen.

### 6. appsettings.json prüfen und Zugangsdaten setzen

Mit der Vorlage passt `src/GenehmigungWorker/appsettings.json` schon:

| Schlüssel | Wert | Wann ihr ihn ändert |
|---|---|---|
| `EngineUrl` | `http://localhost:8080` | Nur wenn die Engine auf einem anderen Port läuft. Immer ohne `/engine-rest`, der Code ruft `/engine-rest/...` auf. |
| `ProzessKey` | `Process_Genehmigung` | Mit eigenem Projekt: dessen Process ID, z. B. `mm-genehmigung` |
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

Fehlen sie, bricht der Worker mit einer Meldung ab, die genau diese Befehle nennt. Gelesen wird in der Reihenfolge `appsettings.json`, User Secrets, Umgebungsvariablen, der spätere Wert gewinnt. So überschreibt ihr jeden Wert für einen Lauf, etwa mit `WorkerId=genehmigung-worker-2` für eine zweite Instanz. Der Prozesstest in Kapitel 12 liest dieselbe Konfiguration, auch dieselben User Secrets.

### 7. Worker starten

```bash
dotnet run --project src/GenehmigungWorker
```

```
13:58:02 Worker genehmigung-worker-1 holt Tasks vom Topic genehmigung-verbuchen bei http://localhost:8080. Beenden mit Strg+C.
```

Der Worker holt bis zu fünf Tasks je Anfrage, hält die Anfrage per Long Polling bis zu zehn Sekunden offen und sperrt jeden Task für 30 Sekunden. Lasst dieses Terminal offen. Für die nächsten Schritte nehmt ihr den Browser und ein zweites Terminal.

### 8. Antrag stellen und genehmigen

1. Oben rechts über den Namen abmelden und als `anna` mit Passwort `anna` anmelden.
2. Kachel „Prozess starten“, bei „Genehmigungsworkflow“ auf „Starten“ klicken. Die Schaltfläche erscheint, wenn ihr mit der Maus über die Karte fahrt. In „Aufgaben bearbeiten“ gibt es dafür oben rechts ebenfalls „Prozess starten“.
3. Im Startformular „Betrag in Euro“ und „Begründung“ ausfüllen, wer mag, legt unter „Anlage“ ein PDF dazu. „Abschließen“ klicken. Die Meldung „Prozess gestartet“ erscheint.
4. Abmelden und als `gerda` mit Passwort `gerda` anmelden.
5. Kachel „Aufgaben bearbeiten“, links den Filter „Aufgaben meiner Gruppen“ wählen, dann die Aufgabe „Antrag prüfen“.
6. „Mir zuweisen“ klicken. Erst danach erscheint das Formular. Bei „Entscheidung“ „Genehmigt“ wählen und „Abschließen“ klicken.

### 9. Dem Worker zuschauen

Wenige Sekunden nach dem Abschließen loggt der Worker den Task mit seinen Variablen:

```
13:59:40 Task 39770f19-... geholt: Business Key (keiner), Prozessinstanz 240ce15c-..., Retries (noch keine)
13:59:40   easyStartFormName = antragsformular
13:59:40   antragsteller = anna
13:59:40   betrag = 1234.5
13:59:40   initiator = anna
13:59:40   entscheidung = genehmigt
13:59:40   begruendung = Fachtagung Prozessautomatisierung
13:59:40   _locale = de
14:00:10 Task 39770f19-... geholt: Business Key (keiner), Prozessinstanz 240ce15c-..., Retries (noch keine)
```

`easyStartFormName`, `initiator` und `_locale` legen Startformular und CIB flow dazu, euer Worker braucht sie nicht. Die Anlage fehlt im Log: Datei-Variablen liefert fetchAndLock ohne Wert, der Worker lässt sie weg.

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
- `betrag` kommt aus dem easyForm als Text, etwa `"1234.5"`, immer mit Punkt, auch wenn das Feld vom Typ „Zahl“ ist. Startet ihr per REST, etwa mit der http-Datei, kommt eine Zahl an. Wie der Handler mit beidem umgeht, steht in [Kapitel 12](kapitel-12-worker-und-tests.md#1-handler-schreiben).
- Einen Business Key setzt das Startformular nicht, deshalb loggt der Worker `Business Key (keiner)`. Warum das in Kapitel 12 wichtig wird, zeigt die Folie „Der Handler“.
- Der easyForm-Baustein setzt nach dem Start-Event und nach „Antrag prüfen“ je einen Speicherpunkt („Asynchronous continuations: After“). Die Engine antwortet dort schon, den Rest führt ihr Job Executor Sekundenbruchteile später aus. Die Aufgabe oder der External Task erscheinen deshalb einen Moment nach dem Klick.
- Ändert ihr das Modell im Repo, spielt ihr es mit `dotnet run --project src/GenehmigungWorker -- deploy` ein. Sonst braucht ihr `deploy` nicht: Direkt nach dem Import der Rückfall-ZIP meldet es `Modell unverändert, die Engine hat keine neue Version angelegt.`, weil dasselbe Modell schon bereitsteht. `deploy` sucht `prozess/genehmigungsworkflow.bpmn` vom aktuellen Ordner aus nach oben und danach vom Programmordner aus, klappt also im Repo-Root, im Projektordner und aus der IDE. Weicht die Process ID des Modells von `ProzessKey` ab, weist `deploy` darauf hin. Die easyForms bringt `deploy` nicht mit, die kommen nur mit dem Projekt-ZIP.
- Solange euer Worker läuft, holt er auch die Tasks, die ihr mit der http-Datei (Schritte 6 und 7) oder dem Smoke-Test holen wollt. Stoppt ihn vorher mit Strg+C. Seine letzte Long-Polling-Anfrage bleibt in der Engine noch bis zu zehn Sekunden offen und kann in dieser Zeit einen neuen Task sperren, dann für 30 Sekunden. Wartet deshalb nach dem Stoppen gut zehn Sekunden.

## Durchlauf mit der http-Datei

Jeden Schritt einzeln als REST-Aufruf zeigt `http/genehmigungsworkflow.http`. Öffnet die Datei in VS Code mit der Erweiterung REST Client. Über jedem Request steht „Send Request“. Klickt die Requests von oben nach unten, sie folgen den Folien:

| Schritt | Request | Als |
|---|---|---|
| 1 | Engine-Version prüfen | `worker` |
| 2 | BPMN aus `prozess/` deployen (`deployment/create`, multipart) | `worker` |
| 3 | Prozess starten, die Engine legt `anna` in `antragsteller` ab | `anna` |
| 4 | Aufgabe „Antrag prüfen“ finden | `gerda` |
| 5 | Aufgabe mit `entscheidung` abschließen | `gerda` |
| 6 | External Task holen (`fetchAndLock`, Topic `genehmigung-verbuchen`) | `worker` |
| 7 | External Task mit `buchungsnummer` abschließen | `worker` |
| 8 und 9 | History prüfen: Instanz `COMPLETED`, `buchungsnummer` gesetzt | `worker` |
| B1 bis B5 | Bonus fachlicher Fehler in Kapitel 12: Variante deployen, mit Variablen starten, „Buchung klären“, Variablen und History prüfen | `worker`, `anna`, `gerda` |

Spätere Requests lesen IDs aus den Antworten früherer Requests (`# @name`). Schickt ihr einen Request ab, bevor sein Vorgänger gelaufen ist, fehlt ihm diese ID, und er schlägt fehl. Für die anderen Pfade tragt ihr in Schritt 5 `abgelehnt` oder `nachbessern` ein. Dann entsteht kein External Task, und Schritt 6 liefert `[]`. Der Start per REST umgeht das Startformular: `betrag` kommt als Zahl an, eine Anlage gibt es nicht.

## Typische Stolpersteine

| Was ihr seht | Woran es liegt, was ihr tut |
|---|---|
| `docker compose up -d` bricht mit `unauthorized` oder `401` ab | Nicht bei `harbor.cib.de` angemeldet. `docker login harbor.cib.de` mit den Zugangsdaten aus der Setup-Mail. |
| `docker compose up -d` meldet `port is already allocated` | Ein Port ist belegt. Lösung in [stack/README.md](../stack/README.md#typische-probleme). |
| Weboberfläche oder REST-API antworten nicht | Der Stack startet noch. Wartet, bis `docker compose logs init` mit `[init] Fertig.` endet. |
| `init` steht auf `Exited (1)` | `docker compose logs init` nennt den Grund. Ein weiteres `docker compose up -d` startet `init` noch einmal. |
| `docker compose logs init` zeigt `set: line 9: illegal option -` | Das Skript hat Windows-Zeilenenden (CRLF). `.gitattributes` sorgt beim Klonen für LF. Hat ein Editor die Datei mit CRLF gespeichert: in VS Code unten rechts `CRLF` auf `LF` umstellen, speichern, `docker compose up -d`. Oder `git checkout -- stack/init/benutzer-anlegen.sh`. |
| Der Worker bricht sofort ab: `Zugangsdaten für die Engine fehlen` | Schritt 6 fehlt, oder ihr habt Umgebungsvariablen in einem anderen Terminal gesetzt. User Secrets gelten überall. |
| Der Worker endet mit `Response status code does not indicate success: 401.` | Benutzer oder Passwort falsch. `dotnet user-secrets list --project src/GenehmigungWorker` zeigt, was gesetzt ist. |
| Der Worker endet mit `HttpRequestException` und `Connection refused` | Die Engine läuft nicht, oder `EngineUrl` zeigt woandershin. |
| Die Kachel „Prozess starten“ fehlt, oder „Genehmigungsworkflow“ ist nicht dabei | Das Projekt ist nicht importiert oder nicht bereitgestellt. Schritt 4. |
| „Prozess starten“ meldet „Das Formular wurde nicht gefunden“ | Das Modell ist in der Engine, die easyForms fehlen, etwa nach einem `deploy` ohne Import. Importiert das Projekt-ZIP, Schritt 4. |
| `deploy` meldet `prozess/genehmigungsworkflow.bpmn nicht gefunden` | Ihr startet außerhalb des Repos. Startet im Repo-Root oder in `src/GenehmigungWorker`. |
| Die Instanz steht bei „Genehmigung verbuchen“, der Worker loggt nichts | Tippfehler im Topic, in `appsettings.json` oder im Modell. Der `curl`-Befehl aus Schritt 9 ohne `?topicName=...` zeigt alle wartenden External Tasks mit ihrem `topicName`. |
| „Antrag prüfen“ fehlt in „Aufgaben bearbeiten“ | Als `gerda` angemeldet? Filter „Aufgaben meiner Gruppen“ gewählt? Mit eigenem Modell: Geht die Aufgabe an die Gruppe `genehmiger`? |
| Das Formular von „Antrag prüfen“ bleibt leer | Oben steht „Aufgabe ist Ihnen nicht zugewiesen“. „Mir zuweisen“ klicken. |
| Eure Engine läuft unter einem Pfad-Präfix, etwa `http://host/prefix/engine-rest` | Die Pfade im Code beginnen mit `/`, deshalb verwirft der `HttpClient` das Präfix aus `EngineUrl`. Setzt `EngineUrl` mit Präfix und abschließendem Slash und schreibt die Pfade im Code ohne führenden Slash. Im lokalen Stack gibt es kein Präfix. |

Weitere Probleme rund um den Stack stehen in [stack/README.md](../stack/README.md#typische-probleme).

## Wer früher fertig ist

Verkürzt die Lock-Dauer und beobachtet, wann der Task erneut kommt:

1. In `src/GenehmigungWorker/ExternalTaskClient.cs` in `FetchAndLockAsync` `lockDuration = 30_000` auf `10_000` setzen.
2. Worker neu starten. Der wartende Task aus Schritt 8 kommt, sobald der alte Lock abgelaufen ist, danach alle zehn Sekunden erneut.
3. Wieder auf `30_000` zurückstellen.

Überlegt dabei: Was passiert, wenn die Arbeit länger dauert als der Lock? Die Antwort steht auf der Folie „Topic, Lock, Retry, Timeout“.

## Musterlösung

Für Kapitel 11 gibt es keine eigene Musterlösung: Der Startstand im Repo ist das Ende dieser Übung. Die Musterlösung unter `loesung/` gehört zu [Kapitel 12](kapitel-12-worker-und-tests.md#musterlösung).

Weiter mit [Kapitel 12 · Übung: Der Worker](kapitel-12-worker-und-tests.md).
