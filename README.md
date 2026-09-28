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
├── http/
│   └── genehmigungsworkflow.http   # alle REST-Schritte zum Durchklicken in VS Code
├── aufgaben/
│   ├── kapitel-11-lokales-setup.md     # Übung: Lokales Setup
│   └── kapitel-12-worker-und-tests.md  # Übung: Der Worker
├── GenehmigungWorker.sln           # Solution für den C#-Worker und seine Tests
├── src/GenehmigungWorker/          # der External Task Worker (Konsolen-App, .NET 10)
├── tests/GenehmigungWorker.Tests/  # Unit-Tests und Prozesstests (xUnit)
├── loesung/                        # Musterlösung zu Kapitel 12, gleiche Pfade wie oben
└── .github/workflows/build.yml     # GitHub Action: baut und testet Startstand und Musterlösung
```

## Die Übungen

| Kapitel | Zeit (Tag 2) | Aufgabenblatt | Am Ende |
|---|---|---|---|
| 11 · External Tasks | 13:30 bis 14:15 | [Übung: Lokales Setup](aufgaben/kapitel-11-lokales-setup.md) | Die lokale Instanz läuft, der Worker holt den Task „Genehmigung verbuchen“ und loggt ihn |
| 12 · Worker und Tests | 14:15 bis 15:15 | [Übung: Der Worker](aufgaben/kapitel-12-worker-und-tests.md) | Der Worker verbucht jede Genehmigung, Unit-Test und Prozesstest laufen grün |

Jedes Aufgabenblatt nennt Ausgangslage, die Schritte mit allen Befehlen für bash und PowerShell, woran ihr seht, dass ihr fertig seid, und die typischen Stolpersteine. Die Abschnitte unten sind das Nachschlagewerk dazu.

## Voraussetzungen

- Eine Container-Laufzeit mit Compose: Docker Desktop, Podman Desktop oder Rancher Desktop
- VS Code mit der Erweiterung REST Client (`humao.rest-client`)
- Für die Übungen: .NET SDK 10 (aktuelle LTS-Version) und Git
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

1. **Container:** `docker compose ps -a` zeigt `cibseven` und `postgres` als `healthy` und `init` als `Exited (0)`. `docker compose logs init` endet mit der Zeile `[init] Fertig. Benutzer: anna, gerda (Gruppe genehmiger), worker. Passwort jeweils wie der Benutzername.`
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

## Der Worker in C#

Ab Kapitel 11 schreibt ihr den External Task Worker für das Topic `genehmigung-verbuchen`. Die Solution `GenehmigungWorker.sln` im Repo-Root enthält zwei Projekte:

```
src/GenehmigungWorker/
├── Program.cs                           # Worker-Schleife, mit "deploy" das Deployment
├── ExternalTaskClient.cs                # fetchAndLock, complete, failure und der Record ExternalTask
├── Deploy.cs                            # spielt prozess/genehmigungsworkflow.bpmn ein
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

Der Startstand entspricht dem Ende der Übung in Kapitel 11: Konfiguration, Deployment und der `ExternalTaskClient` mit fetchAndLock, complete und failure funktionieren. Die Schleife in `Program.cs` holt Tasks und loggt sie, ruft aber noch keinen Handler und schickt kein `complete`. Handler, Simulation, Fake und Tests tragen Kommentare `TODO Kapitel 12`. Sie bauen, die Arbeit darin fehlt noch. Die Tests sind deshalb mit `Skip` markiert: `dotnet test` meldet sie als übersprungen, nicht als rot. Die Musterlösung liegt unter `loesung/`.

### Konfiguration

`appsettings.json` enthält nur Werte, die ins Repo dürfen:

| Schlüssel | Wert im Repo | Bedeutung |
|---|---|---|
| `EngineUrl` | `http://localhost:8080` | Basis-URL der Engine ohne `/engine-rest`, der Code ruft `/engine-rest/...` auf |
| `ProzessKey` | `Process_Genehmigung` | Process ID des Modells unter `prozess/`, zugleich `deployment-name` |
| `Topic` | `genehmigung-verbuchen` | exakt wie im Modell |
| `WorkerId` | `genehmigung-worker-1` | je Worker-Instanz eindeutig, die Engine merkt sich, wer den Lock hält |

Legt ihr euer eigenes Modell unter `prozess/` ab, tragt ihr dessen Process ID als `ProzessKey` ein.

Die Zugangsdaten für `/engine-rest` gehören nicht in diese Datei. Setzt `EngineBenutzer` und `EnginePasswort` als User Secrets oder als Umgebungsvariablen, lokal mit dem Benutzer `worker`. User Secrets sind bequemer: Sie gelten in jedem Terminal, und in den Übungen arbeitet ihr mit zweien.

```bash
# User Secrets, bash und PowerShell gleich: gelten dauerhaft und liegen in eurem Benutzerprofil, nicht im Repo
dotnet user-secrets set EngineBenutzer worker --project src/GenehmigungWorker
dotnet user-secrets set EnginePasswort worker --project src/GenehmigungWorker
dotnet user-secrets list --project src/GenehmigungWorker    # zeigt, was gesetzt ist
```

```bash
# Umgebungsvariablen in bash, zsh, Git Bash: gelten nur für dieses Terminal
export EngineBenutzer=worker EnginePasswort=worker
```

```powershell
# Umgebungsvariablen in PowerShell: gelten nur für dieses Terminal
$env:EngineBenutzer = "worker"; $env:EnginePasswort = "worker"
```

Fehlen sie, bricht der Worker mit einer Meldung ab, die genau diese Befehle nennt. Gelesen wird in der Reihenfolge `appsettings.json`, User Secrets, Umgebungsvariablen, der spätere Wert gewinnt. So überschreibt ihr jeden Wert für einen Lauf, etwa mit `WorkerId=genehmigung-worker-2` für eine zweite Instanz. Der Prozesstest liest dieselbe Konfiguration, auch dieselben User Secrets.

### Bauen, deployen, starten

Im Repo-Root:

```bash
dotnet build                                              # baut Worker und Tests
dotnet run --project src/GenehmigungWorker -- deploy      # BPMN aus prozess/ einspielen
dotnet run --project src/GenehmigungWorker                # Worker starten, beenden mit Strg+C
```

Im Ordner `src/GenehmigungWorker` genügen `dotnet run -- deploy` und `dotnet run`.

`deploy` sucht `prozess/genehmigungsworkflow.bpmn` vom aktuellen Ordner aus nach oben und danach vom Programmordner aus. Deshalb klappt der Aufruf im Repo-Root, im Projektordner und aus der IDE, auch wenn sie im Ordner `bin/...` startet. Ist das Modell unverändert, legt die Engine dank `enable-duplicate-filtering` keine neue Version an, und `deploy` meldet „Modell unverändert“. Weicht die Process ID des Modells von `ProzessKey` ab, weist `deploy` darauf hin.

Der Worker holt bis zu fünf Tasks je Anfrage, hält die Anfrage per Long Polling bis zu zehn Sekunden offen und sperrt jeden Task für 30 Sekunden. Im Startstand loggt er Business Key, Prozessinstanz und Variablen:

```
10:00:39 Worker genehmigung-worker-1 holt Tasks vom Topic genehmigung-verbuchen bei http://localhost:8080. Beenden mit Strg+C.
10:00:39 Task 9bb40e5d-... geholt: Business Key (keiner), Prozessinstanz 9b811764-..., Retries (noch keine)
10:00:39   antragsteller = anna
10:00:39   betrag = 1200
10:00:39   entscheidung = genehmigt
10:00:39   begruendung = Dienstreise zur Fachtagung
10:01:09 Task 9bb40e5d-... geholt: Business Key (keiner), Prozessinstanz 9b811764-..., Retries (noch keine)
```

Weil er noch kein `complete` schickt, läuft der Lock nach 30 Sekunden ab, und derselbe Task kommt erneut: die doppelte Auslieferung aus Kapitel 11. Wer den Lock gerade hält, zeigt die Engine in `workerId` und `lockExpirationTime`:

```bash
curl -u worker:worker "http://localhost:8080/engine-rest/external-task?topicName=genehmigung-verbuchen"
```

Solange euer Worker läuft, holt er auch die Tasks, die ihr mit der http-Datei (Schritte 6 und 7), mit dem Smoke-Test oder im Prozesstest holen wollt. Stoppt ihn vorher mit Strg+C. Seine letzte Long-Polling-Anfrage bleibt in der Engine danach noch bis zu zehn Sekunden offen und kann in dieser Zeit einen neuen Task sperren, dann für 30 Sekunden. Wartet deshalb nach dem Stoppen gut zehn Sekunden, bevor ihr per http-Datei oder Smoke-Test Tasks holt. Der Prozesstest fängt das selbst ab, er fragt bis zu 45 Sekunden lang nach.

### Tests

```bash
dotnet test                                               # alle Tests, die Prozesstests brauchen die Engine
dotnet test --filter "Kategorie!=Prozesstest"             # nur die Unit-Tests, ohne Engine
dotnet test --filter "Kategorie=Prozesstest"              # nur die Prozesstests
```

Die Unit-Tests brauchen weder Engine noch Zugangsdaten und laufen in Millisekunden. Die Prozesstests tragen `[Trait("Kategorie", "Prozesstest")]` und laufen per REST gegen die lokale Engine: Der Stack muss laufen, das Modell deployt, die Zugangsdaten gesetzt (dieselben wie für den Worker, auch als User Secrets) und euer Worker gestoppt sein. Jeder Test startet eine eigene Instanz mit Business Key `prozesstest-...`, holt den External Task mit Filter auf diesen Business Key unter der Worker-ID `prozesstest` und löscht am Ende, was von seinen Instanzen noch offen ist. Kommt kein Task, fragt der Test bis zu 45 Sekunden lang nach. Habt ihr den Worker eben erst gestoppt, dauert der Prozesstest deshalb rund 30 Sekunden, scheitert aber nicht (siehe oben). Läuft die Engine nicht, fehlt das Deployment oder stimmen die Zugangsdaten nicht, nennt die Fehlermeldung des Tests die Ursache und den nächsten Schritt.

| Stand | `dotnet test --filter "Kategorie!=Prozesstest"` | `dotnet test` mit laufendem Stack |
|---|---|---|
| Startstand | 1 übersprungen | 3 übersprungen |
| Musterlösung | 4 bestanden | 6 bestanden |

Den Test-Helfer `EngineHelfer.cs` (im Test `_engine`) bekommt ihr fertig: je Methode ein REST-Call, etwa `StartAsync`, `GetTaskAsync`, `CompleteTaskAsync`, `FetchAndLockAsync`, `CompleteAsync`, `GetHistoryAsync`, `GetVariableAsync` und für die Gegenprobe `GetExternalTasksAsync`.

### Übung Kapitel 12

Die Schritte stehen als `TODO Kapitel 12, Schritt ...` im Code, ausführlich mit Befehlen und Stolpersteinen im Aufgabenblatt [Übung: Der Worker](aufgaben/kapitel-12-worker-und-tests.md). Die Reihenfolge folgt der Empfehlung aus dem Kapitel: zuerst, was ohne Engine läuft.

| Schritt | Dateien | Was ihr tut |
|---|---|---|
| 1 | `Handlers/GenehmigungVerbuchenHandler.cs` | Variablen lesen, `Verbuchen` rufen, `buchungsnummer` zurückgeben |
| 2 | `BuchungssystemFake.cs`, `GenehmigungVerbuchenHandlerTests.cs` | Fake und Unit-Test schreiben, `Skip` entfernen, `dotnet test --filter "Kategorie!=Prozesstest"` |
| 3 | `Fachsystem/BuchungssystemSimulation.cs`, `Program.cs` | Buchung simulieren, in der Schleife den Handler rufen und `complete` oder `failure` schicken |
| 4 | `GenehmigungsworkflowTests.cs` | Prozesstest schreiben, `Skip` entfernen, Worker stoppen, `dotnet test` |
| 5 | Webapp | Worker starten, als `anna` einen Antrag stellen, als `gerda` genehmigen, im Cockpit prüfen |

Wer schneller ist: die Gegenprobe im Prozesstest (mit `abgelehnt` entsteht kein External Task) und der [Bonus Idempotenz](aufgaben/kapitel-12-worker-und-tests.md#bonus-idempotenz).

### Musterlösung

`loesung/` enthält die fertigen Fassungen der Dateien, die sich gegenüber dem Startstand ändern, unter denselben Pfaden. Wie ihr vergleicht und übernehmt, steht auch im [Aufgabenblatt](aufgaben/kapitel-12-worker-und-tests.md#musterlösung).

```
loesung/
├── src/GenehmigungWorker/
│   ├── Program.cs                               # Schleife mit Handler, complete und failure
│   ├── Handlers/GenehmigungVerbuchenHandler.cs  # wie auf der Folie
│   └── Fachsystem/BuchungssystemSimulation.cs   # fortlaufende Nummern, idempotent per Datei
└── tests/GenehmigungWorker.Tests/
    ├── BuchungssystemFake.cs                    # liefert B-2026-0001 und merkt sich die Aufrufe
    ├── GenehmigungVerbuchenHandlerTests.cs      # Test der Folie, ohne Business Key, fehlende Variable, Idempotenz
    └── GenehmigungsworkflowTests.cs             # Prozesstest der Folie und Gegenprobe mit abgelehnt
```

`IBuchungssystem.cs`, `EngineHelfer.cs` und alle übrigen Dateien sind schon im Startstand fertig. Die Solution baut nur `src/` und `tests/`, der Ordner `loesung/` stört den Build nicht.

Zum Vergleichen markiert ihr in VS Code beide Dateien im Explorer und wählt per Rechtsklick „Ausgewählte vergleichen“. Oder im Terminal:

```bash
git diff --no-index src/GenehmigungWorker/Program.cs loesung/src/GenehmigungWorker/Program.cs
```

Zum Übernehmen kopiert ihr die Musterlösung im Repo-Root über den Startstand. Das überschreibt eure Fassungen dieser sechs Dateien, sichert oder committet sie vorher:

```bash
# bash, zsh, Git Bash
cp -R loesung/src loesung/tests .
```

```powershell
# PowerShell
Copy-Item -Path loesung\src, loesung\tests -Destination . -Recurse -Force
```

Eine einzelne Datei übernehmt ihr genauso, etwa `cp loesung/src/GenehmigungWorker/Program.cs src/GenehmigungWorker/` (PowerShell: `Copy-Item loesung\src\GenehmigungWorker\Program.cs src\GenehmigungWorker\`). `Program.cs` braucht dann auch die `BuchungssystemSimulation.cs` der Musterlösung (Konstruktor mit Dateipfad), `GenehmigungVerbuchenHandlerTests.cs` braucht deren Simulation und Fake. Mit der Musterlösung laufen `dotnet test --filter "Kategorie!=Prozesstest"` ohne Engine und `dotnet test` mit laufendem Stack und deploytem Modell grün. Zurück zum Startstand kommt ihr mit `git restore src tests`, das verwirft eure Änderungen in diesen Ordnern.

Der Worker der Musterlösung loggt jeden Task:

```
10:14:52 Worker genehmigung-worker-1 holt Tasks vom Topic genehmigung-verbuchen bei http://localhost:8080. Beenden mit Strg+C.
10:14:52 Buchungen der Simulation: .../src/GenehmigungWorker/bin/Debug/net10.0/buchungen.json
10:14:52 Task aea3b3dd-... geholt: Business Key (keiner), Prozessinstanz ae9c60c4-..., Retries (noch keine)
10:14:52 Verbucht: B-2026-0001 für anna, 1200,00 Euro, "Dienstreise zur Fachtagung" (Schlüssel ae9c60c4-...)
10:14:52 Task aea3b3dd-... erledigt: buchungsnummer = B-2026-0001
```

Kommt derselbe Schlüssel noch einmal, meldet die Simulation `Schlüssel ... ist schon verbucht als B-2026-0001, keine zweite Buchung.`, und der Worker schließt den Task mit derselben Nummer ab. Scheitert der Handler, etwa an einer fehlenden Variablen, schickt der Worker `failure`: beim ersten Mal mit 3 verbleibenden Versuchen, danach herunterzählend, dazwischen fünf Minuten Pause. Bei 0 legt die Engine einen Incident an.

### Entscheidungen, wo die Folien offen sind

- `Einstellungen.cs` liest die Konfiguration und baut den `HttpClient` mit `EngineUrl` als `BaseAddress` und Basic Auth. Worker, Deployment und Prozesstest nutzen sie gemeinsam.
- `lockDuration` (30 s), `maxTasks` (5) und `asyncResponseTimeout` (10 s) stehen wie auf der Folie im Code von `FetchAndLockAsync`, nicht in `appsettings.json`.
- `ExternalTask.Variables` ist ein `Dictionary<string, object>` mit ausgepackten Werten: Text als `string`, ganze Zahlen als `long`, andere Zahlen als `double`, Wahrheitswerte als `bool`. Variablen ohne Wert lässt `ToTask` weg, der Handler scheitert dann laut an der fehlenden Variable. So baut der Handler von der Folie ohne Nullable-Warnungen.
- `CompleteAsync` schickt die Werte typisiert: `string` als String, `int` als Integer, `long` als Long, `decimal` und `double` als Double, `bool` als Boolean.
- `IBuchungssystem` und die Simulation liegen unter `Fachsystem/`, getrennt von den Handlern.
- Der Prozesstest nimmt Prozess-Key und Topic aus der Konfiguration (`_engine.ProzessKey`, `_engine.Topic`). Auf der Folie stehen sie ausgeschrieben.
- Die Tests im Startstand sind mit `Skip` markiert statt rot. So läuft `dotnet test` von Anfang an sauber durch, und ihr seht, welche Tests noch fehlen.
- Die Prozesstests tragen den Trait `Kategorie=Prozesstest`. Damit trennt `--filter` sie von den Unit-Tests, etwa auf einem Rechner ohne Engine.
- Die Simulation speichert ihre Buchungen in `buchungen.json` neben der DLL (`src/GenehmigungWorker/bin/Debug/net10.0/`). Den Pfad gibt `Program.cs` im Konstruktor mit und loggt ihn beim Start. So findet der Worker die Datei, egal wo ihr ihn startet, und sie landet nie im Repo. Löscht ihr die Datei, beginnen die Nummern wieder bei 0001. Gezählt wird je Kalenderjahr: B-2026-0001, B-2026-0002 und so weiter.
- Die Simulation speichert die Buchung, bevor sie die Nummer zurückgibt. Stirbt der Worker zwischen Verbuchen und `complete`, bekommt der nächste Versuch dieselbe Nummer.
- Die Schleife in der Musterlösung ist die von der Folie, ergänzt um je eine Log-Zeile für geholt, erledigt und fehlgeschlagen.
- Der Unit-Test-Fake merkt sich seine Aufrufe. Damit prüft ein Test, dass der Handler ohne Business Key unter der Prozessinstanz-ID verbucht, wie beim Start über das Formular.
- `EngineHelfer.FetchAndLockAsync` fragt bis zu 45 Sekunden lang nach, statt nach einem leeren fetchAndLock sofort aufzugeben. Grund: Die letzte Long-Polling-Anfrage eines eben gestoppten Workers bleibt in der Engine bis zu zehn Sekunden offen und kann den Task des Tests noch für 30 Sekunden sperren. Ohne Nachfragen scheitert der Prozesstest dann, obwohl alles stimmt.

## Stoppen und Zurücksetzen

Im Ordner `stack/`:

```bash
docker compose down       # Container entfernen, die Datenbank bleibt im Volume erhalten
docker compose down -v    # alles zurück auf null: löscht Deployments, Instanzen und Benutzer
```

Nach `down -v` legt der nächste `docker compose up -d` die Benutzer neu an. Engine und Datenbank starten nach einem Neustart von Docker automatisch wieder, bis ihr sie mit `docker compose down` entfernt oder mit `docker compose stop` anhaltet.

## Fehlerbilder

**Port 8080 ist belegt.** `docker compose up -d` bricht beim Start von `cibseven` ab, etwa mit `ports are not available: exposing port TCP 0.0.0.0:8080 ... bind: address already in use`. Sucht den Prozess mit `lsof -i :8080` (macOS, Linux) oder `netstat -ano | findstr :8080` (Windows) und beendet ihn. Oder legt die Engine auf einen anderen Port: In `stack/docker-compose.yml` `"8081:8080"` statt `"8080:8080"` eintragen und in der http-Datei `@baseUrl` sowie im Worker `EngineUrl` anpassen.

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

Es legt eigene Instanzen mit Business Key `smoke-...` an und löscht am Ende, was davon noch offen ist. Stoppt vorher einen laufenden Worker und wartet danach gut zehn Sekunden, sonst holt er dem Test womöglich den External Task weg. Mit `ENGINE_URL`, `PROZESS_KEY` und `BPMN` lässt es sich auf eine andere Engine oder ein anderes Modell richten.

## Automatische Prüfung (GitHub Actions)

`.github/workflows/build.yml` läuft bei jedem Push und Pull Request auf `main`, von Hand unter Actions, Build, „Run workflow“. Beide Jobs laufen auf `ubuntu-latest` mit .NET 10.

| Job | Was er prüft |
|---|---|
| Startstand bauen und testen | `dotnet build`, dann `dotnet test`: Der Startstand baut ohne Fehler, die TODO-Tests sind übersprungen, keiner ist rot. |
| Musterlösung gegen die lokale Engine | übernimmt `loesung/` mit `cp -R loesung/src loesung/tests .`, baut, führt die Unit-Tests ohne Engine und ohne Zugangsdaten aus, startet `stack/` mit `docker compose up -d`, wartet, bis `init` mit Exit-Code 0 fertig ist, deployt mit `dotnet run -- deploy` (erwartet `Neue Version: Process_Genehmigung, Version 1`), führt `stack/smoke-test.sh` und danach die Prozesstests aus. Am Ende baut `docker compose down -v` den Stack ab. |

Die Zugangsdaten setzt der Workflow als Umgebungsvariablen `EngineBenutzer` und `EnginePasswort` mit dem lokalen Dev-Benutzer `worker`, nur für Deployment und Prozesstests. Schlägt ein Schritt fehl, zeigt der Job `docker compose ps -a` und die letzten Log-Zeilen des Stacks. `dotnet run -- deploy`, die http-Datei und der Smoke-Test deployen alle mit dem Prozess-Key als `deployment-name`. Deshalb legt nur das erste Deployment eine Version an, die übrigen meldet die Engine als unverändert.

## Ausblick

- Unter `prozess/` legt ihr in der Übung euer eigenes Modell aus dem ZIP-Export ab, mit eurem Prozess-Key statt `Process_Genehmigung`. Denselben Key tragt ihr als `ProzessKey` in `src/GenehmigungWorker/appsettings.json` ein.
- Das CIB flow Setup ersetzt den Ordner `stack/`.

## Lizenz

MIT, siehe [LICENSE](LICENSE).
