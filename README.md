# CIB flow Developer Training Exercises

Übungs-Repo für den Entwickler-Track der CIB flow Intensivschulung. Hier startet ihr CIB flow lokal, importiert euer Projekt und schreibt am zweiten Tag den External Task Worker in C# für den Genehmigungsworkflow, samt Unit-Test und Prozesstests in C# und in Java.

## Voraussetzungen

- Docker Desktop mit mindestens 8 GB Speicher für Docker und rund 6 GB freiem Plattenplatz. Wo ihr den Speicher unter macOS und Windows einstellt, steht in [stack/README.md](stack/README.md#voraussetzungen).
- Zugangsdaten für `harbor.cib.de` aus der Setup-Mail
- .NET SDK 10, Git und VS Code mit der Erweiterung REST Client (`humao.rest-client`)
- JDK 21 für den Prozesstest in Java, etwa Eclipse Temurin. Maven braucht ihr nicht, das Projekt bringt den Maven Wrapper mit.

```bash
docker compose version
dotnet --version     # 10.0.x
java -version        # 21 oder neuer
git --version
```

> Für Behörden und für Unternehmen ab 250 Beschäftigten oder 10 Mio. USD Jahresumsatz kostet Docker Desktop eine Lizenz, siehe [Docker Desktop License Agreement](https://docs.docker.com/subscription/desktop-license/). Podman Desktop und Rancher Desktop sind kostenlos, mit diesem Stack aber nicht getestet.

## Schnellstart

1. Repo klonen und bei der Registry anmelden:
   ```bash
   git clone https://github.com/miragon-trainings/cibflow-developer-training-exercises.git
   cd cibflow-developer-training-exercises
   docker login harbor.cib.de
   ```
2. CIB flow starten. Beim ersten Mal lädt Docker die Images, das dauert einige Minuten. Fertig ist der Stack, wenn `docker compose logs init` mit `[init] Fertig.` endet. Wiederholt den Befehl, bis es so weit ist:
   ```bash
   cd stack
   docker compose up -d
   docker compose logs init
   cd ..
   ```
3. http://localhost:7083/client öffnen und als `demo` mit Passwort `demo` anmelden.
4. Projekt importieren: Kachel „Prozessmanagement“, „Lokale Datei importieren“, euer Projekt-ZIP oder `prozess/genehmigungsworkflow-projekt.zip` wählen, „Automatisch bereitstellen“ an lassen, „Importieren“. Mit eigenem Projekt tragt ihr dessen Process ID als `ProzessKey` in `src/GenehmigungWorker/appsettings.json` ein.
5. Worker starten:
   ```bash
   dotnet user-secrets set EngineBenutzer worker --project src/GenehmigungWorker
   dotnet user-secrets set EnginePasswort worker --project src/GenehmigungWorker
   dotnet run --project src/GenehmigungWorker
   ```

Den Prozesstest in Java lasst ihr einmal vorab laufen, am besten gleich nach dem Klonen. Der erste Lauf lädt Maven und die Bibliotheken, rund 55 MB, danach geht es auch ohne Netz. Stack und Worker braucht er nicht. Läuft euer Worker schon, nehmt ein zweites Terminal. Im Repo-Root:

```bash
# macOS, Linux, Git Bash
cd java-prozesstest
./mvnw test
cd ..
```

```powershell
# Windows PowerShell
cd java-prozesstest
.\mvnw.cmd test
cd ..
```

Erwartet: `[WARNING] Tests run: 4, Failures: 0, Errors: 0, Skipped: 3`. Das `[WARNING]` kommt von den drei übersprungenen Tests und ist kein Fehler, die schreibt ihr in Kapitel 12. Scheitert der erste Lauf mit `PKIX path building failed` oder `Could not transfer artifact`, sitzt ihr hinter einem Proxy. Was dann hilft, steht in [java-prozesstest/README.md](java-prozesstest/README.md#hinter-einem-proxy).

Konten, Adressen und typische Probleme mit dem Stack stehen in [stack/README.md](stack/README.md). Jeden Schritt ausführlich, mit PowerShell-Varianten, zeigen die Aufgabenblätter.

## Die Übungen

| Kapitel | Zeit (Tag 2) | Aufgabenblatt | Am Ende |
|---|---|---|---|
| 11 · External Tasks | 13:30 bis 14:15 | [Übung: Lokales Setup](aufgaben/kapitel-11-lokales-setup.md) | CIB flow läuft lokal mit eurem Projekt, der Worker holt den Task „Genehmigung verbuchen“ und loggt ihn |
| 12 · Worker und Tests | 14:15 bis 15:15 | [Übung: Der Worker](aufgaben/kapitel-12-worker-und-tests.md) | Der Worker verbucht jede Genehmigung, Unit-Test und Prozesstests in C# und Java laufen grün |

Jedes Aufgabenblatt nennt Ausgangslage, Schritte, woran ihr seht, dass ihr fertig seid, und die typischen Stolpersteine.

## Was wo liegt

```
cibflow-developer-training-exercises/
├── stack/                              # CIB flow lokal per Docker Compose, Anleitung in stack/README.md
│   ├── docker-compose.yml              # Engine, Weboberfläche, Werkzeuge, Benutzer-Init
│   ├── config/                         # Konfiguration der CIB flow Dienste für die Schulung
│   ├── init/benutzer-anlegen.sh        # legt anna, gerda, worker, die Gruppe genehmiger und zwei Filter an
│   └── smoke-test.sh                   # Werkzeug für Trainer: prüft alle drei Pfade per REST
├── prozess/
│   ├── genehmigungsworkflow.bpmn       # Vorlage des Modells, Process ID Process_Genehmigung
│   ├── genehmigungsworkflow-projekt.zip  # Projekt-ZIP zum Import, falls ihr kein eigenes habt
│   ├── formulare/                      # die beiden easyForms im Projekt-ZIP
│   ├── projekt-zip-bauen.py            # baut das Projekt-ZIP neu, nach Änderungen an Modell oder Formularen
│   └── varianten/verbuchen-fehlerpfad.bpmn  # Variante für den Bonus fachlicher Fehler
├── http/genehmigungsworkflow.http      # alle REST-Schritte zum Durchklicken in VS Code
├── aufgaben/                           # die Aufgabenblätter zu Kapitel 11 und 12
├── GenehmigungWorker.sln               # Solution für den Worker und seine Tests
├── src/GenehmigungWorker/              # der External Task Worker (Konsolen-App, .NET 10)
├── tests/GenehmigungWorker.Tests/      # Unit-Tests und Prozesstests (xUnit)
├── java-prozesstest/                   # Prozesstest in Java, Engine im Speicher: Übung Kapitel 12, Demo Kapitel 10
├── loesung/                            # Musterlösung zu Kapitel 12, gleiche Pfade wie src/, tests/ und java-prozesstest/
└── .github/                            # CI: baut und testet Startstand und Musterlösung, in C# und Java
```

## Für Trainer

`stack/smoke-test.sh` prüft einen laufenden Stack per REST, mit allen drei Pfaden. Aufruf im Ordner `stack/` mit `./smoke-test.sh`, unter Windows in Git Bash mit `bash smoke-test.sh`. Stoppt vorher einen laufenden Worker. Mit `ENGINE_URL`, `PROZESS_KEY` und `BPMN` richtet ihr es auf eine andere Engine oder ein anderes Modell.

Die Demo in Kapitel 10 läuft mit dem Projekt aus der Übung, `java-prozesstest/`, und dem Test aus seiner Musterlösung. Vorbereitung, Ablauf, die erwarteten Meldungen und das Zurücksetzen stehen in seiner [README](java-prozesstest/README.md#demo-kapitel-10-trainer).

Die GitHub Action `.github/workflows/build.yml` baut und testet bei jedem Push Startstand und Musterlösung, jeweils in C# und in Java. Warum sie für die Musterlösung eine eigene Engine aus `.github/ci-stack/` startet, steht im Kommentar der Datei.

## Lizenz

MIT, siehe [LICENSE](LICENSE). Ausgenommen sind die Konfigurationsdateien unter `stack/config/`: Sie beruhen auf der Docker-Compose-Vorlage von CIB software GmbH für CIB flow.
