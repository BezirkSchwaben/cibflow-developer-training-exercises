# CIB flow Developer Training Exercises

Übungs-Repo für den Entwickler-Track der CIB flow Intensivschulung. Hier startet ihr eure lokale Engine, gegen die ihr am zweiten Tag den External Task Worker in C# für den Genehmigungsworkflow schreibt und testet.

> **Übergangs-Setup mit CIB seven, CIB flow folgt.**
> Der Ordner `stack/` startet vorerst ein reines CIB seven 2.2 (Distribution Run) mit PostgreSQL.
> Das CIB flow Setup ersetzt ihn, sobald es bereitsteht.

## Was drin ist

```
cibflow-developer-training-exercises/
├── stack/
│   ├── docker-compose.yml          # PostgreSQL, CIB seven Run 2.2.0 und der Init-Dienst
│   ├── cibseven/default.yml        # Run-Konfiguration: REST mit Anmeldung, Webapps, Admin demo
│   ├── init/benutzer-anlegen.sh    # legt Benutzer, Gruppe genehmiger und Tasklist-Filter an
│   └── smoke-test.sh               # Trainer-Werkzeug: prüft alle drei Pfade automatisch
├── prozess/
│   └── genehmigungsworkflow.bpmn   # Übergangsfassung des Modells mit Generated Forms
└── http/
    └── genehmigungsworkflow.http   # alle REST-Schritte zum Durchklicken in VS Code
```

## Voraussetzungen

- Eine Container-Laufzeit mit Compose: Docker Desktop, Podman Desktop oder Rancher Desktop
- VS Code mit der Erweiterung REST Client (`humao.rest-client`)
- Für die Übungen: .NET SDK (aktuelle LTS-Version) und Git
- Port 8080 frei und rund 1,6 GB Speicherplatz für die Images

Prüft die Werkzeuge vorab im Terminal:

```bash
docker compose version     # Podman: podman compose version
dotnet --version
git --version
```

> Docker Desktop ist nur für kleine Unternehmen (weniger als 250 Beschäftigte und weniger als 10 Mio. USD Jahresumsatz), private Nutzung, Bildung und nichtkommerzielle Open-Source-Projekte kostenlos.
> Größere Organisationen und Behörden brauchen ein kostenpflichtiges Abo, siehe [Docker Desktop License Agreement](https://docs.docker.com/subscription/desktop-license/).
> Podman Desktop und Rancher Desktop sind kostenlos.

## Quick Start

```bash
git clone https://github.com/miragon-trainings/cibflow-developer-training-exercises.git
cd cibflow-developer-training-exercises/stack
docker compose up -d
```

Mit Podman lautet der letzte Befehl `podman compose up -d`. `podman compose` ruft einen installierten Compose-Provider auf (`docker-compose` oder `podman-compose`), unter macOS und Windows muss außerdem die Podman Machine laufen (`podman machine start`).

Beim ersten Start lädt Compose die Images. Danach braucht die Engine ein bis zwei Minuten, bis sie antwortet, auf langsameren Rechnern auch länger. Der Dienst `init` wartet bis zu zehn Minuten darauf, legt Benutzer, Gruppe und Filter an und beendet sich. Geht der Rechner währenddessen in den Ruhezustand, dauert es entsprechend länger.

## Adressen

| Was | Adresse |
|---|---|
| CIB seven Webapp (Prozess starten, Tasklist, Cockpit, Admin) | http://localhost:8080/webapp/ |
| REST-API der Engine | http://localhost:8080/engine-rest |

Die REST-API verlangt Basic Auth mit einem der Benutzer unten. PostgreSQL ist nur im Compose-Netz erreichbar, nicht von eurem Rechner aus.

## Benutzer

Passwort jeweils gleich dem Benutzernamen. Das ist nur für euren Laptop gedacht.

| Benutzer | Rolle | Wofür |
|---|---|---|
| `demo` | Admin | CIB seven Standard, Administration in der Webapp |
| `anna` | Antragstellerin | startet Anträge und bekommt „Antrag nachbessern“ |
| `gerda` | Genehmigerin, Gruppe `genehmiger` | bearbeitet „Antrag prüfen“ |
| `worker` | technischer Benutzer | für euren C#-Worker und die REST-Aufrufe |

Autorisierungen sind in diesem Setup aus: Jeder angemeldete Benutzer darf alles. Wem eine Aufgabe gehört, zeigen die Tasklist-Filter „Meine Aufgaben“ und „Aufgaben meiner Gruppen“. Der Filter „All tasks“ zeigt alle Aufgaben.

## Prüfen, ob alles läuft

Alle Befehle im Ordner `stack/`.

1. **Container:** `docker compose ps -a` zeigt `cibseven` und `postgres` als `healthy` und `init` als `Exited (0)`. `docker compose logs init` endet mit der Zeile `[init] Fertig. Benutzer: anna, gerda (Gruppe genehmiger), worker.`
2. **REST-API:** Ohne Anmeldung antwortet die Engine mit 401, mit Anmeldung mit ihrer Version.
   ```bash
   curl -i http://localhost:8080/engine-rest/version                  # HTTP/1.1 401
   curl -u worker:worker http://localhost:8080/engine-rest/version    # {"version":"2.2.0"}
   ```
   In der Windows PowerShell schreibt ihr `curl.exe` statt `curl`.
3. **Webapp:** http://localhost:8080/webapp/ öffnen und als `gerda` mit Passwort `gerda` anmelden. Die Startseite zeigt die Kacheln Prozess starten, Tasklist und Cockpit. In der Tasklist stehen links die Filter „All tasks“, „Meine Aufgaben“ und „Aufgaben meiner Gruppen“.

## Durchlauf mit der http-Datei

Öffnet `http/genehmigungsworkflow.http` in VS Code. Über jedem Request steht „Send Request“. Klickt die Requests von oben nach unten, sie folgen den Folien:

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

Spätere Requests lesen IDs aus den Antworten früherer Requests (`# @name`). Schickt ihr einen Request ab, bevor sein Vorgänger gelaufen ist, fehlt ihm diese ID, und er schlägt fehl.

Für die anderen Pfade tragt ihr in Schritt 5 `abgelehnt` oder `nachbessern` ein. Dann entsteht kein External Task, und Schritt 6 liefert `[]`.

Derselbe Ablauf in der Webapp:

1. Als `anna` anmelden, Prozess starten, bei „Genehmigungsworkflow“ auf Starten klicken. Das Startformular fragt „Betrag in Euro“ und „Grund des Antrags“ ab.
2. Als `gerda` anmelden, Tasklist öffnen, im Filter „Aufgaben meiner Gruppen“ die Aufgabe „Antrag prüfen“ wählen. Erst „Mir zuweisen“ (danach steht die Aufgabe unter „Meine Aufgaben“), dann die Entscheidung wählen und abschließen.
3. Bei `genehmigt` wartet die Instanz am External Task „Genehmigung verbuchen“, bis ein Worker ihn holt: Schritte 6 und 7 der http-Datei oder später euer C#-Worker. Im Cockpit seht ihr, wo die Instanz steht.

## Stoppen und Zurücksetzen

Im Ordner `stack/`:

```bash
docker compose down       # Container entfernen, die Datenbank bleibt im Volume erhalten
docker compose down -v    # alles zurück auf null: löscht Deployments, Instanzen und Benutzer
```

Nach `down -v` legt der nächste `docker compose up -d` die Benutzer neu an. Engine und Datenbank starten nach einem Neustart von Docker automatisch wieder, bis ihr sie mit `docker compose down` entfernt oder mit `docker compose stop` anhaltet.

## Fehlerbilder

**Port 8080 ist belegt.** `docker compose up -d` bricht beim Start von `cibseven` ab, etwa mit `ports are not available: exposing port TCP 0.0.0.0:8080 ... bind: address already in use`. Sucht den Prozess mit `lsof -i :8080` (macOS, Linux) oder `netstat -ano | findstr :8080` (Windows) und beendet ihn. Oder legt die Engine auf einen anderen Port: In `stack/docker-compose.yml` `"8081:8080"` statt `"8080:8080"` eintragen und in der http-Datei `@baseUrl` anpassen.

**Die Engine ist noch nicht bereit.** Direkt nach dem Start antworten Webapp und REST-API noch nicht, `docker compose ps` zeigt bei `cibseven` den Zustand `health: starting`. Wartet, bis `docker compose logs --tail 80 cibseven` die Zeile `Started CamundaBpmRun` zeigt. Beim ersten Start mit leerer Datenbank stehen davor Meldungen wie `ERROR: relation "mod_element_templates" does not exist`. Die sind harmlos: Der Modeler der Webapp wartet auf seine Tabelle, die die Engine erst danach anlegt. Steht `init` auf `Exited (1)`, nennt `docker compose logs init` den Grund. Ein weiteres `docker compose up -d` startet `init` noch einmal.

**Das Init-Skript bricht wegen CRLF ab.** `docker compose logs init` zeigt `set: line 9: illegal option -`. Das Skript hat Windows-Zeilenenden (CRLF), die Shell im Container versteht nur LF. Die Datei `.gitattributes` sorgt beim Klonen für LF. Hat ein Editor die Datei trotzdem mit CRLF gespeichert: In VS Code unten rechts `CRLF` auf `LF` umstellen, speichern und `docker compose up -d` erneut ausführen. Oder die Datei mit `git checkout -- stack/init/benutzer-anlegen.sh` zurückholen.

**Umlaute in Formular-Labels sehen kaputt aus.** Die CIB seven Webapp 2.2.0 zeigt Umlaute in den Labels von Generated Forms falsch an (aus „Begründung“ wird „BegrÃ¼ndung“). Die Übergangsfassung des Modells verzichtet deshalb in den Labels auf Umlaute. Eingaben mit Umlauten kommen korrekt in der Engine an.

**Das Formular einer Aufgabe ist ausgegraut.** Die Aufgabe ist euch noch nicht zugewiesen. Klickt oben auf „Mir zuweisen“.

## Für Trainer: Smoke-Test

`stack/smoke-test.sh` prüft einen laufenden Stack ohne Klicken: Anmeldung, Deployment und alle drei Pfade (genehmigt mit External Task und `buchungsnummer`, abgelehnt, nachbessern mit Assignee `anna`). Das Skript braucht bash und curl, unter Windows Git Bash.

```bash
cd stack
./smoke-test.sh           # Exit-Code 0: alles grün, 1: mindestens eine Prüfung fehlgeschlagen
```

Es legt eigene Instanzen mit Business Key `smoke-...` an und löscht am Ende, was davon noch offen ist. Stoppt vorher einen laufenden Worker, sonst holt er dem Test womöglich den External Task weg. Mit `ENGINE_URL`, `PROZESS_KEY` und `BPMN` lässt es sich auf eine andere Engine oder ein anderes Modell richten.

## Ausblick

- `src/GenehmigungWorker/` und `tests/GenehmigungWorker.Tests/` kommen mit den Übungen dazu: der C#-Worker für das Topic `genehmigung-verbuchen` und seine Tests.
- Unter `prozess/` legt ihr in der Übung euer eigenes Modell aus dem ZIP-Export ab, mit eurem Prozess-Key statt `Process_Genehmigung`.
- Das CIB flow Setup ersetzt den Ordner `stack/`.

## Lizenz

MIT, siehe [LICENSE](LICENSE).
