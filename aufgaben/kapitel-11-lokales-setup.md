# Übung 8 · Lokales Setup und External Task: zum Nachschlagen

Tag 2, Entwickler-Track, Kapitel 11 „External Tasks“.

Die Schritte von Übung 8 stehen in der Übungsanleitung eurer Schulung: Repo holen, CIB flow lokal starten, euer Projekt importieren, „Genehmigung verbuchen“ zum External Task umbauen, das Modell aus dem Repo deployen und dem Worker beim Holen zuschauen. Hier schlagt ihr nebenbei nach: welche Datei im Repo was tut, wie sich Worker, Stack und `deploy` verhalten, wie der Durchlauf per REST geht und was bei Stack, Build und Worker hakt. Das Verbuchen und die Tests folgen in Übung 9, [Kapitel 12](kapitel-12-worker-und-tests.md).

Bis Übung 7 ist „Genehmigung verbuchen“ ein User Task, in Übung 8 wird er zum External Task. Topic, Variablen und IDs sind die aus der Schulung: Topic `genehmigung-verbuchen`, Variablen `antragsteller`, `betrag`, `begruendung` und `entscheidung`, Aufgaben `Task_Pruefen` und `Task_Verbuchen`. Im Repo liegt das fertige Modell nach Übung 7, nur mit User Tasks, mit der Process ID `Process_Genehmigung`. Arbeitet ihr mit eurem eigenen Projekt, tragt ihr dessen Process ID als `ProzessKey` in `appsettings.json` ein, z. B. `mm-genehmigung`.

## Dateien im Repo

Den Code für Übung 8 bekommt ihr fertig. Code schreibt ihr hier noch nicht, am Modell ändert ihr einen Task.

| Datei | Was drinsteht | Folie |
|---|---|---|
| `prozess/genehmigungsworkflow.bpmn` | Im frischen Klon das fertige Modell nach Übung 7, nur mit User Tasks. Nach dem Umbau legt ihr euren Download hier ab, nur diesen Pfad liest `deploy`. | Projektstruktur für den C#-Worker |
| `prozess/genehmigungsworkflow-projekt.zip` | Projekt zum Import, wenn ihr kein eigenes ZIP habt: das fertige Modell nach Übung 7 (`prozess/genehmigungsworkflow.bpmn`, nur User Tasks) und die drei easyForms `antragsformular` (Betrag und Begründung), `genehmigungsformular` (Betrag und Begründung gesperrt, dazu die Entscheidung und die Begründung der Ablehnung, die nur bei „Abgelehnt“ erscheint) und `nachbesserungsformular` (Betrag und Begründung zum Überarbeiten, die Schaltflächen „Antrag zurückziehen“ und „Abschließen“). Die easyForms liegen auch einzeln unter `prozess/formulare/`. | |
| `loesung/genehmigungsworkflow-entwickler.bpmn` | das Modell nach dem Umbau, siehe [Musterlösung](#musterlösung) | Der Umbau im Modeler |
| `worker/src/GenehmigungWorker/appsettings.json` | `EngineUrl` (immer ohne `/engine-rest`, der Code ruft `/engine-rest/...` auf), `ProzessKey` (die Process ID eures Modells), `Topic` (`genehmigung-verbuchen`, zeichengleich mit dem Topic im Modell), `WorkerId` | Projektstruktur für den C#-Worker |
| `worker/src/GenehmigungWorker/Einstellungen.cs` | liest `appsettings.json`, User Secrets und Umgebung, baut den `HttpClient` | |
| `worker/src/GenehmigungWorker/ExternalTaskClient.cs` | `FetchAndLockAsync`, `CompleteAsync`, `FailureAsync` und der Record `ExternalTask` | fetchAndLock in C#, complete und failure |
| `worker/src/GenehmigungWorker/Deploy.cs` | spielt `prozess/genehmigungsworkflow.bpmn` per Multipart-Request ein | Deployment aus der IDE |
| `worker/src/GenehmigungWorker/Program.cs` | Skeleton-Schleife: holt Tasks und loggt sie, schickt aber kein `complete`. Mit dem Argument `deploy` startet das Programm stattdessen das Deployment. | |
| `http/genehmigungsworkflow.http` | jeden Schritt als REST-Aufruf, siehe [Durchlauf mit der http-Datei](#durchlauf-mit-der-http-datei) | |
| `stack/` | CIB flow lokal per Docker Compose. Voraussetzungen, Konten, Adressen und Zurücksetzen stehen in [stack/README.md](../stack/README.md). | |

## Hinweise

- Auf eurer Instanz läuft nur euer Worker. `fetchAndLock` braucht deshalb nur das Topic, keinen weiteren Filter. Der Worker holt bis zu fünf Tasks je Anfrage, hält die Anfrage per Long Polling bis zu zehn Sekunden offen und sperrt jeden Task für 30 Sekunden. Schickt er kein `complete`, kommt derselbe Task nach Ablauf des Locks erneut: die doppelte Auslieferung von der Folie „Warum Worker idempotent sein müssen“.
- Der Worker liest seine Konfiguration aus `appsettings.json`, dann aus den User Secrets, dann aus Umgebungsvariablen, der spätere Wert gewinnt. Die Zugangsdaten für `/engine-rest`, `EngineBenutzer` und `EnginePasswort`, lokal beide `worker`, gehören nicht in `appsettings.json`. User Secrets liegen in eurem Benutzerprofil, nicht im Repo, und gelten in jedem Terminal. Umgebungsvariablen gelten nur im Terminal, in dem ihr sie setzt: in bash `export EngineBenutzer=worker EnginePasswort=worker`, in PowerShell `$env:EngineBenutzer = "worker"; $env:EnginePasswort = "worker"`. So überschreibt ihr auch einen Wert für einen Lauf, etwa für eine zweite Worker-Instanz in einem weiteren Terminal im Ordner `worker/`: in bash `WorkerId=genehmigung-worker-2 dotnet run --project src/GenehmigungWorker`, in PowerShell `$env:WorkerId = "genehmigung-worker-2"; dotnet run --project src/GenehmigungWorker`. Der Prozesstest in C# aus Übung 9 liest dieselbe Konfiguration, auch dieselben User Secrets.
- `antragsteller` legt das Start-Event selbst ab (`camunda:initiator`): Es ist der Benutzer, der den Prozess gestartet hat, etwa `anna`.
- `betrag` kommt aus dem easyForm als Text, immer mit Punkt und mit so vielen Nachkommastellen, wie ihr eingebt, etwa `"1234.50"`. Das gilt auch, wenn das Feld vom Typ „Zahl“ ist. Im Log des Workers seht ihr den Unterschied nicht: Der Text `"1200"` und die Zahl `1200` stehen beide als `1200` da. Startet ihr per REST, etwa mit der http-Datei, kommt eine Zahl an. Wie der Handler mit beidem umgeht, steht in [Übung 9, Schritt 1](kapitel-12-worker-und-tests.md#1-handler-schreiben).
- Einen Business Key setzt das Startformular nicht, deshalb loggt der Worker `Business Key (keiner)`. Warum das in Übung 9 wichtig wird, zeigt die Folie „Der Handler“.
- Der easyForm-Baustein setzt nach dem Start-Event, nach „Antrag prüfen“ und nach „Antrag nachbessern“ je einen Speicherpunkt („Asynchronous continuations: After“). Die Engine antwortet dort schon, den Rest führt ihr Job Executor Sekundenbruchteile später aus. Die Aufgabe oder der External Task erscheinen deshalb einen Moment nach dem Klick.
- `deploy` spielt `prozess/genehmigungsworkflow.bpmn` ein, im Ordner `worker/` mit `dotnet run --project src/GenehmigungWorker -- deploy`. Nach dem Import meldet das erste `deploy` Version 2, auch bei unverändertem Modell: Das Prozessmanagement stellt mit eigener Quelle und unter einem anderen Dateinamen bereit, deshalb erkennt der Duplikatfilter der Engine das Modell nicht wieder. Das schadet nicht, die easyForms bleiben verknüpft. Ab dem zweiten Aufruf mit derselben Datei meldet es `Modell unverändert, die Engine hat keine neue Version angelegt.` Das meldet es auch, wenn der Smoke-Test genau diese Datei schon eingespielt hat, denn er stellt unter demselben Namen bereit wie `deploy`. Dann bleibt die neueste Version, was danach kam, etwa ein Import mit User Task. `deploy` sucht `prozess/genehmigungsworkflow.bpmn` vom aktuellen Ordner aus nach oben und danach vom Programmordner aus, klappt also im Ordner `worker/`, im Projektordner `worker/src/GenehmigungWorker` und aus der IDE. Weicht die Process ID des Modells von `ProzessKey` ab, weist `deploy` darauf hin. Die easyForms bringt `deploy` nicht mit, die kommen nur mit dem Projekt-ZIP.
- Solange euer Worker läuft, holt er auch die Tasks, die ihr mit der http-Datei (Schritte 6 und 7) oder dem Smoke-Test holen wollt. Stoppt ihn vorher mit Strg+C. Seine letzte Long-Polling-Anfrage bleibt in der Engine noch bis zu zehn Sekunden offen und kann in dieser Zeit einen neuen Task sperren, dann für 30 Sekunden. Wartet deshalb nach dem Stoppen gut zehn Sekunden.

## Durchlauf mit der http-Datei

Jeden Schritt einzeln als REST-Aufruf zeigt `http/genehmigungsworkflow.http`. Öffnet die Datei in VS Code mit der Erweiterung REST Client. Über jedem Request steht „Send Request“. Klickt die Requests von oben nach unten, sie folgen den Folien. Mit eigenem Projekt tragt ihr vorher oben in der Datei bei `@prozessKey` eure Process ID ein, wie `ProzessKey` in `appsettings.json`.

| Schritt | Request | Als |
|---|---|---|
| 1 | Engine-Version prüfen | `worker` |
| 2 | BPMN aus `prozess/` deployen (`deployment/create`, multipart) | `worker` |
| 3 und 3a | Prozess starten, die Engine legt `anna` in `antragsteller` ab, 3a zeigt die Variable | `anna` |
| 4 | Aufgabe „Antrag prüfen“ finden | `gerda` |
| 5 | Aufgabe mit `entscheidung` abschließen | `gerda` |
| 6 | External Task holen (`fetchAndLock`, Topic `genehmigung-verbuchen`) | `worker` |
| 7 | External Task mit `buchungsnummer` abschließen | `worker` |
| 8 und 9 | History prüfen: Instanz `COMPLETED`, `buchungsnummer` gesetzt | `worker` |
| B1 bis B5 | Bonus fachlicher Fehler in Übung 9: Variante deployen, mit Variablen starten, „Buchung klären“, Variablen und History prüfen | `worker`, `anna`, `gerda` |

Schritt 2 spielt ein, was unter `prozess/` liegt. Die Schritte 6 und 7 brauchen das Modell nach dem Umbau: Liegt dort noch das Modell ohne External Task, liefert Schritt 6 dauerhaft `[]`.

Spätere Requests lesen IDs aus den Antworten früherer Requests (`# @name`). Schickt ihr einen Request ab, bevor sein Vorgänger gelaufen ist, fehlt ihm diese ID, und er schlägt fehl. Für die anderen Pfade tragt ihr in Schritt 5 `abgelehnt` oder `nachbessern` ein. Dann entsteht kein External Task, und Schritt 6 liefert `[]`. Bei `abgelehnt` wartet „Ablehnung mitteilen“ als Aufgabe bei der Gruppe `genehmiger`: Schritt 4 noch einmal senden zeigt sie, die Instanz ist dann nicht `COMPLETED`. Der Start per REST umgeht das Startformular: `betrag` kommt als Zahl an.

## Typische Stolpersteine

Hier stehen Meldungen von Docker, vom Stack, vom Build und vom laufenden Worker. Was beim Export, Import, Umbau oder Antrag hakt, steht bei den Schritten in der Übungsanleitung eurer Schulung. Die `docker compose`-Befehle in dieser Tabelle laufen im Ordner `stack/`, die `dotnet`-Befehle im Ordner `worker/`.

| Was ihr seht | Woran es liegt, was ihr tut |
|---|---|
| `docker compose up -d` bricht mit `unauthorized` oder `401` ab | Nicht bei `harbor.cib.de` angemeldet. `docker login harbor.cib.de` mit den Zugangsdaten aus der Setup-Mail. |
| `docker compose up -d` meldet `port is already allocated` | Ein Port ist belegt. Lösung in [stack/README.md](../stack/README.md#typische-probleme). |
| Weboberfläche oder REST-API antworten nicht | Der Stack startet noch, nach dem Laden der Images braucht er etwa eine Minute. Wartet, bis `docker compose logs init` mit `[init] Fertig.` endet. |
| `init` steht auf `Exited (1)` | `docker compose logs init` nennt den Grund. Ein weiteres `docker compose up -d` startet `init` noch einmal. |
| `docker compose logs init` zeigt `illegal option -` | Das Skript hat Windows-Zeilenenden (CRLF). `.gitattributes` sorgt beim Klonen für LF. Hat ein Editor die Datei mit CRLF gespeichert: in VS Code unten rechts `CRLF` auf `LF` umstellen, speichern, `docker compose up -d`. Oder im Repo-Root `git checkout -- stack/init/benutzer-anlegen.sh`. |
| Der Worker bricht sofort ab: `Zugangsdaten für die Engine fehlen` | Die User Secrets fehlen, oder ihr habt Umgebungsvariablen in einem anderen Terminal gesetzt. User Secrets gelten überall: `dotnet user-secrets set EngineBenutzer worker --project src/GenehmigungWorker`, ebenso `EnginePasswort`. Mehr unter [Hinweise](#hinweise). |
| Der Worker endet mit `Response status code does not indicate success: 401 (Unauthorized).` | Benutzer oder Passwort falsch. `dotnet user-secrets list --project src/GenehmigungWorker` zeigt, was gesetzt ist. |
| Der Worker endet mit `HttpRequestException` und `Connection refused` (Windows: `actively refused it` oder `Zielcomputer die Verbindung verweigerte`) | Die Engine läuft nicht, oder `EngineUrl` zeigt woandershin. |
| `deploy` meldet `prozess/genehmigungsworkflow.bpmn nicht gefunden` | Ihr startet außerhalb des Repos. Startet im Ordner `worker/` oder in `worker/src/GenehmigungWorker`. |
| Kacheln und Menüs sind englisch | Die Sprache folgt dem Browser. Oben rechts über das Globus-Symbol auf „Deutsch“ umstellen, CIB flow merkt sich das. |
| Eure Engine läuft unter einem Pfad-Präfix, etwa `http://host/prefix/engine-rest` | Die Pfade im Code beginnen mit `/`, deshalb verwirft der `HttpClient` das Präfix aus `EngineUrl`. Setzt `EngineUrl` mit Präfix und abschließendem Slash und schreibt die Pfade im Code ohne führenden Slash. Im lokalen Stack gibt es kein Präfix. |

Weitere Probleme rund um den Stack stehen in [stack/README.md](../stack/README.md#typische-probleme).

## Wer früher fertig ist

Verkürzt die Lock-Dauer und beobachtet, wann der Task erneut kommt:

1. In `worker/src/GenehmigungWorker/ExternalTaskClient.cs` in `FetchAndLockAsync` `lockDuration = 30_000` auf `10_000` setzen.
2. Worker neu starten. Der Task eures Antrags, der noch bei „Genehmigung verbuchen“ wartet, kommt, sobald der alte Lock abgelaufen ist, danach alle zehn Sekunden erneut.
3. Wieder auf `30_000` zurückstellen.

Überlegt dabei: Was passiert, wenn die Arbeit länger dauert als der Lock? Die Antwort steht auf der Folie „Topic, Lock, Retry, Timeout“.

## Musterlösung

Die Lösung des Umbaus ist `loesung/genehmigungsworkflow-entwickler.bpmn`: das fertige Modell nach Übung 7 mit „Genehmigung verbuchen“ als External Task auf dem Topic `genehmigung-verbuchen`. Sie trägt die Process ID `Process_Genehmigung` und passt damit zum Projekt-ZIP aus dem Repo, nicht zu einem eigenen Projekt mit eigenem Key. Wie ihr sie als Rückfall ablegt und deployt, steht in [loesung/README.md](../loesung/README.md#lösung-von-übung-8). Worker-Code ändert ihr in Übung 8 nicht. Die Musterlösung unter `loesung/worker/` und `loesung/prozesstest-java/` gehört zu [Übung 9](kapitel-12-worker-und-tests.md#musterlösung).

Weiter mit [Kapitel 12 · Übung 9: Worker und Tests](kapitel-12-worker-und-tests.md).
