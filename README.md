# CIB flow Developer Training Exercises

Übungs-Repo für den Entwickler-Track der CIB flow Intensivschulung. Hier startet ihr am zweiten Tag CIB flow lokal, importiert euer Projekt, macht in Übung 8 „Genehmigung verbuchen“ selbst zum External Task und schreibt in Übung 9 den External Task Worker in C# für den Genehmigungsworkflow, samt Unit-Test und Prozesstests in C# und in Java.

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

1. Repo klonen und bei der Registry anmelden. Schon geklont? Dann im Repo-Root `git pull`.
   ```bash
   git clone https://github.com/miragon-trainings/cibflow-developer-training-exercises.git
   cd cibflow-developer-training-exercises
   docker login harbor.cib.de
   ```
2. CIB flow starten. Beim ersten Mal lädt Docker die Images, das dauert einige Minuten. Fertig ist der Stack, sobald `[init] Fertig.` in der letzten Zeile von `docker compose logs init` steht. Wiederholt den Befehl, bis es so weit ist:
   ```bash
   cd stack
   docker compose up -d
   docker compose logs init
   cd ..
   ```
3. http://localhost:7083/client öffnen und als `demo` mit Passwort `demo` anmelden.
4. Projekt importieren: Kachel „Prozessmanagement“, „Lokale Datei importieren“, euer Projekt-ZIP oder `prozess/genehmigungsworkflow-projekt.zip` wählen, „Automatisch bereitstellen“ an lassen, „Importieren“. Mit eigenem Projekt tragt ihr dessen Process ID als `ProzessKey` in `worker/src/GenehmigungWorker/appsettings.json` ein.
5. Zugangsdaten setzen. Alle `dotnet`-Befehle laufen im Ordner `worker/`:
   ```bash
   cd worker
   dotnet user-secrets set EngineBenutzer worker --project src/GenehmigungWorker
   dotnet user-secrets set EnginePasswort worker --project src/GenehmigungWorker
   ```
6. „Genehmigung verbuchen“ zum External Task machen: im Modeler umbauen, das heruntergeladene Modell als `prozess/genehmigungsworkflow.bpmn` ablegen und einspielen. Wie das geht, zeigt die Übungsanleitung eurer Schulung zu Übung 8. Vorher bekommt der Worker keinen Task.
   ```bash
   dotnet run --project src/GenehmigungWorker -- deploy
   ```
7. Worker starten:
   ```bash
   dotnet run --project src/GenehmigungWorker
   ```

Den Prozesstest in Java lasst ihr einmal vorab laufen, am besten gleich nach dem Klonen. Der erste Lauf lädt Maven und die Bibliotheken, rund 70 MB, danach geht es auch ohne Netz. Nach jedem `git pull` startet ihr ihn noch einmal mit Netz: Er lädt nach, was im neuen Stand dazugekommen ist. Stack und Worker braucht er nicht. Läuft euer Worker schon, nehmt ein zweites Terminal. Im Repo-Root:

```bash
# macOS, Linux, Git Bash
cd prozesstest-java
./mvnw test
cd ..
```

```powershell
# Windows PowerShell
cd prozesstest-java
.\mvnw.cmd test
cd ..
```

Erwartet: `[WARNING] Tests run: 4, Failures: 0, Errors: 0, Skipped: 3`. Das `[WARNING]` kommt von den drei übersprungenen Tests und ist kein Fehler, die schreibt ihr in Übung 9. Scheitert der erste Lauf mit `PKIX path building failed` oder `Could not transfer artifact`, sitzt ihr hinter einem Proxy. Was dann hilft, steht in [prozesstest-java/README.md](prozesstest-java/README.md#hinter-einem-proxy).

Konten, Adressen und typische Probleme mit dem Stack stehen in [stack/README.md](stack/README.md). Jeden Schritt ausführlich, mit PowerShell-Varianten, zeigen für Übung 8 die Übungsanleitung eurer Schulung und für Übung 9 das Aufgabenblatt.

## Die Übungen

| Kapitel (Tag 2) | Übung | Anleitung | Am Ende |
|---|---|---|---|
| 11 · External Tasks | Übung 8: Lokales Setup und External Task | Übungsanleitung eurer Schulung, zum Nachschlagen: [aufgaben/kapitel-11-lokales-setup.md](aufgaben/kapitel-11-lokales-setup.md) | CIB flow läuft lokal mit eurem Projekt, „Genehmigung verbuchen“ ist External Task, der Worker holt den Task und loggt ihn |
| 12 · Worker und Tests | Übung 9: Worker und Tests | [aufgaben/kapitel-12-worker-und-tests.md](aufgaben/kapitel-12-worker-und-tests.md) | Der Worker verbucht jede Genehmigung, Unit-Test und Prozesstests in C# und Java laufen grün |

Die Schritte von Übung 8 stehen in der Übungsanleitung eurer Schulung. Das Blatt dazu sammelt, was ihr nebenbei nachschlagt: Dateien im Repo, wie sich Worker, Stack und `deploy` verhalten, den Durchlauf per REST und die typischen Stolpersteine. Das Blatt zu Übung 9 nennt Ausgangslage, Schritte, woran ihr seht, dass ihr fertig seid, und die typischen Stolpersteine.

## Was wo liegt

Ihr arbeitet in zwei Ordnern: `worker/` für C# und `prozesstest-java/` für Java. Die fertige Fassung beider Projekte liegt unter `loesung/`, dazu der Worker noch einmal nach Ports und Adaptern geschnitten. Wie ihr sie baut und mit eurem Stand vergleicht, steht in [loesung/README.md](loesung/README.md).

```
cibflow-developer-training-exercises/
├── aufgaben/                           # Übung 8 zum Nachschlagen, Aufgabenblatt zu Übung 9
├── stack/                              # CIB flow lokal per Docker Compose, Anleitung in stack/README.md
│   ├── docker-compose.yml              # Engine, Weboberfläche, Werkzeuge, Benutzer-Init
│   ├── config/                         # Konfiguration der CIB flow Dienste für die Schulung
│   ├── init/benutzer-anlegen.sh        # legt anna, gerda, worker, die Gruppe genehmiger und zwei Filter an
│   ├── smoke-test.sh                   # Werkzeug für Trainer: prüft alle drei Pfade per REST
│   └── SETUP-CHECK.md                  # prüft in fünf Schritten, ob das Repo auf einem Rechner läuft
├── prozess/
│   ├── genehmigungsworkflow.bpmn       # Startstand: fertiges Modell nach Übung 7, nur User Tasks, Process ID Process_Genehmigung, Umbau in Übung 8
│   ├── genehmigungsworkflow-projekt.zip  # Projekt-ZIP zum Import, falls ihr kein eigenes habt
│   ├── formulare/                      # die drei easyForms im Projekt-ZIP: antragsformular, genehmigungsformular, nachbesserungsformular
│   ├── projekt-zip-bauen.py            # baut das Projekt-ZIP neu, nach Änderungen an Modell oder Formularen
│   └── varianten/verbuchen-fehlerpfad.bpmn  # Variante für den Bonus fachlicher Fehler
├── http/genehmigungsworkflow.http      # alle REST-Schritte zum Durchklicken in VS Code
├── worker/                             # C#: euer Worker und seine Tests, hier laufen alle dotnet-Befehle
│   ├── GenehmigungWorker.sln           # Solution für den Worker und seine Tests
│   ├── src/GenehmigungWorker/          # der External Task Worker (Konsolen-App, .NET 10)
│   └── tests/GenehmigungWorker.Tests/  # Unit-Tests und Prozesstests (xUnit)
├── prozesstest-java/                   # Java: eure Prozesstests, Engine im Speicher, Anleitung in prozesstest-java/README.md
│   ├── mvnw, mvnw.cmd, pom.xml         # Maven Wrapper und Projektdatei, mit bpmn-to-code für die IDs der Modelle
│   ├── src/main/resources/             # Kopien der Entwickler-Fassung und der Variante
│   └── src/test/java/                  # die Prozesstests (JUnit 5)
├── loesung/                            # Musterlösung zu Übung 8 und 9, Anleitung in loesung/README.md
│   ├── genehmigungsworkflow-entwickler.bpmn  # Lösung von Übung 8: „Genehmigung verbuchen“ als External Task, Rückfall
│   ├── worker/                         # der fertige Worker mit allen Tests, gleicher Aufbau wie worker/
│   ├── worker-hexagonal/               # derselbe Worker nach Ports und Adaptern geschnitten, zum Vergleich, Anleitung in seiner README.md
│   ├── element-template/               # Bonus in Übung 9: euer Worker als Baustein im Katalog des Modelers
│   └── prozesstest-java/               # die fertigen Prozesstests in Java, hier läuft die Demo zu Kapitel 10
└── .github/                            # CI: baut und testet Startstand und Musterlösung, in C# und Java
```

## Für Trainer

Ob das Repo auf einem Rechner läuft, prüft ihr in fünf Schritten mit dem [Setup-Check](stack/SETUP-CHECK.md): Startstand, CIB flow, Worker und Musterlösung.

`stack/smoke-test.sh` prüft einen laufenden Stack per REST, mit allen drei Pfaden. Aufruf im Ordner `stack/` mit `./smoke-test.sh`, unter Windows in Git Bash mit `bash smoke-test.sh`. Stoppt vorher einen laufenden Worker. Mit `ENGINE_URL`, `PROZESS_KEY` und `BPMN` richtet ihr es auf eine andere Engine oder ein anderes Modell. Der Smoke-Test spielt die Entwickler-Fassung `loesung/genehmigungsworkflow-entwickler.bpmn` ein, danach ist sie die neueste Version in der Engine. Zeigt ihr auf diesem Rechner danach Übung 8, setzt ihr den Stack im Ordner `stack/` mit `docker compose down -v` zurück und importiert das Projekt-ZIP neu: Der Smoke-Test hat „Genehmigung verbuchen“ schon als External Task eingespielt.

Die Demo in Kapitel 10 läuft in `loesung/prozesstest-java/` mit der Klasse `GenehmigungsworkflowTag1Test` am Modell ohne External Task, dort liegt der fertige Test schon. Das Modell ändert ihr vorübergehend nur in `loesung/prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn`, zurück geht es im Repo-Root mit `git restore loesung/prozesstest-java`. Der Startstand `prozesstest-java/` bleibt für die Übung unberührt. Vorbereitung, Ablauf und die erwarteten Meldungen stehen in [prozesstest-java/README.md](prozesstest-java/README.md#demo-kapitel-10-trainer).

Die GitHub Action `.github/workflows/build.yml` baut und testet bei jedem Push Startstand und Musterlösung, jeweils in C# und in Java, in C# auch die hexagonale Fassung unter `loesung/worker-hexagonal/`. Warum sie für die Musterlösung eine eigene Engine aus `.github/ci-stack/` startet, steht im Kommentar der Datei. Dazu prüft `.github/scripts/loesung-abgleich.sh`, auch in der CI, dass die Musterlösung jede Datei des Startstands enthält und nur in den Übungsdateien abweicht, und dass die hexagonale Fassung die Dateien, die sie aus `loesung/worker/` übernimmt, unverändert lässt. Die Listen stehen im Skript.

## Lizenz

MIT, siehe [LICENSE](LICENSE). Ausgenommen sind die Konfigurationsdateien unter `stack/config/`: Sie beruhen auf der Docker-Compose-Vorlage von CIB software GmbH für CIB flow.
