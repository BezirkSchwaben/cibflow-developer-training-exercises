# CIB flow lokal

Dieser Ordner startet CIB flow auf eurem Laptop: die Engine, die Weboberfläche und die Werkzeuge (Modeler, easyForm, Prozessmanagement, Ressourcen). Nur für die Schulung, nicht produktiv verwenden.

Ob auf eurem Rechner alles läuft, vom Startstand bis zur Musterlösung, prüft ihr mit dem [Setup-Check](SETUP-CHECK.md).

## Voraussetzungen

- Docker braucht mindestens 8 GB Speicher, der Stack belegt im Leerlauf rund 5,5 GB. So stellt ihr das ein:
  - macOS: in Docker Desktop unter Settings, Resources, Advanced, Memory limit.
  - Windows mit WSL 2, dem Standard: Docker Desktop hat dort keinen Regler, WSL bekommt die Hälfte des Arbeitsspeichers. Legt `%UserProfile%\.wslconfig` an mit den Zeilen `[wsl2]` und `memory=8GB`, führt in PowerShell `wsl --shutdown` aus und startet Docker Desktop neu.
- Rund 6 GB freier Plattenplatz. Beim ersten Start lädt Docker sieben CIB-flow-Images von `harbor.cib.de` und zwei kleine Hilfsimages von Docker Hub, zusammen mehrere GB. Plant dafür je nach Leitung einige Minuten ein.
- Mac mit Apple Silicon: Die Images gibt es nur für Intel und AMD (`linux/amd64`). Docker startet sie emuliert, das funktioniert, dauert aber etwas länger.
- Die Ports 8080, 7083, 7086, 7088, 7089, 7090 und 7091 sind frei.
- Die Zugangsdaten für `harbor.cib.de` aus der Setup-Mail.

## Starten

1. Einmal bei der Registry anmelden, mit den Zugangsdaten aus der Setup-Mail:
   ```bash
   docker login harbor.cib.de
   ```
2. Im Ordner `stack/` den Stack starten:
   ```bash
   docker compose up -d
   ```
3. Etwa eine Minute warten. Fertig ist der Stack, wenn `docker compose logs init` mit dieser Zeile endet:
   ```
   [init] Fertig. Benutzer: anna, gerda (Gruppe genehmiger), worker. Passwort jeweils wie der Benutzername.
   ```
4. http://localhost:7083/client öffnen und anmelden, etwa als `demo` mit Passwort `demo`.

## Konten

Passwort jeweils gleich dem Benutzernamen. Die Konten gibt es nur auf eurem Laptop.

| Benutzer | Rolle | Wofür |
|---|---|---|
| `demo` | Admin | Administration, Prozessmanagement, Import des Projekts |
| `anna` | Antragstellerin | stellt Anträge über „Prozess starten“, bekommt „Antrag nachbessern“ und „Genehmigende Stelle benachrichtigen“ |
| `gerda` | Genehmigerin, Gruppe `genehmiger` | bearbeitet „Antrag prüfen“, „Ablehnung mitteilen“, „Erinnerung senden“ und vor Übung 8 „Genehmigung verbuchen“ |
| `worker` | technischer Benutzer | für euren C#-Worker, die Tests und REST-Aufrufe |

Die Autorisierung ist lokal aus: Jeder angemeldete Benutzer sieht alle Kacheln und darf alles. Wem eine Aufgabe gehört, zeigen in „Aufgaben bearbeiten“ die Filter „Meine Aufgaben“ und „Aufgaben meiner Gruppen“.

## Adressen

| Was | Adresse |
|---|---|
| Weboberfläche mit allen Kacheln | http://localhost:7083/client |
| REST-API der Engine (Basic Auth, etwa `worker`/`worker`) | http://localhost:8080/engine-rest |
| easyForm, Kachel „Easy Form“ | http://localhost:7086/easy-form |
| Modeler, Kachel „Prozess modellieren“ | http://localhost:7088/flow-modeler |
| Prozessmanagement, Kachel „Prozessmanagement“ | http://localhost:7089/flow-process-management |
| Ressourcen, Kachel „Ressourcen“ | http://localhost:7090/flow-resource |
| UI Element Templates (zeigt die Formulare an) | http://localhost:7091/ui-element-templates |

Die Werkzeuge ab Port 7086 öffnet ihr über die Kacheln der Weboberfläche, sie melden euch dort mit an. Direkt braucht ihr nur die Weboberfläche und die REST-API. Alle Ports hören nur auf `localhost`, von außen ist nichts erreichbar.

## Stoppen und zurücksetzen

Im Ordner `stack/`:

```bash
docker compose down       # Container entfernen, Projekte, Formulare und Instanzen bleiben erhalten
docker compose down -v    # alles zurück auf null: löscht Deployments, Projekte, Formulare, Instanzen und Benutzer
```

Nach `down -v` legt der nächste `docker compose up -d` die Benutzer neu an. Solange ihr den Stack nicht mit `down` entfernt, startet er nach einem Neustart von Docker von selbst wieder. Nur anhalten: `docker compose stop`.

## Typische Probleme

**Der Pull bricht mit `unauthorized` oder `401 Unauthorized` ab.** Ihr seid nicht bei `harbor.cib.de` angemeldet, oder die Anmeldung ist abgelaufen. `docker login harbor.cib.de` mit den Zugangsdaten aus der Setup-Mail wiederholen, dann `docker compose up -d`.

**Ein Port ist belegt.** `docker compose up -d` meldet `port is already allocated` oder `address already in use`. Findet das Programm mit `lsof -i :8080` (macOS, Linux) oder `netstat -ano | findstr :8080` (Windows, die letzte Spalte ist die PID) und beendet es, unter Windows mit `taskkill /PID <PID> /F` oder im Task-Manager im Reiter „Details“. Geht das nicht, legt den Dienst auf einen anderen Port: In `docker-compose.yml` die linke Portnummer ändern, etwa `"127.0.0.1:8081:8080"`, und in `config/common-config.yaml` die `external-url` des Dienstes anpassen. Bei der Engine zusätzlich `EngineUrl` in `worker/src/GenehmigungWorker/appsettings.json` und in `loesung/worker/src/GenehmigungWorker/appsettings.json` sowie `@baseUrl` in `http/genehmigungsworkflow.http`.

**Dienste starten immer wieder neu, die Oberfläche bleibt unvollständig.** Docker hat zu wenig Speicher. `docker compose ps` zeigt `Restarting` oder `Exited (137)`. Gebt Docker mindestens 8 GB, wie unter [Voraussetzungen](#voraussetzungen) beschrieben, und startet mit `docker compose up -d` neu.

**Das Formular einer Aufgabe ist ausgegraut.** Die Aufgabe ist euch noch nicht zugewiesen, darüber steht der Hinweis „Aufgabe ist Ihnen nicht zugewiesen“. Klickt auf „Mir zuweisen“, dann könnt ihr das Formular ausfüllen. Die Aufgabe steht danach unter „Meine Aufgaben“, deshalb zeigt „Aufgaben meiner Gruppen“ sie nicht mehr.

**„Prozess starten“ meldet „Das Formular wurde nicht gefunden“.** Das Modell ist in der Engine, die easyForms dazu fehlen. Importiert das Projekt-ZIP im Prozessmanagement: angemeldet als `demo` Kachel „Prozessmanagement“, „Lokale Datei importieren“, das ZIP wählen, „Automatisch bereitstellen“ an lassen, „Importieren“.

**Im Cockpit steht unter „Metriken“ „Lizenz erforderlich“.** Das ist lokal so gewollt: Der Stack läuft ohne Lizenzdatei. Alles, was die Übungen brauchen, funktioniert trotzdem.

**Kacheln und Menüs sind englisch.** CIB flow folgt der Sprache des Browsers. Stellt oben rechts über das Globus-Symbol auf „Deutsch“ um.

Mehr zeigen die Logs: `docker compose logs --tail 100 <dienst>`, etwa `flow-cibseven-spring` für die Engine oder `init` für das Anlegen der Benutzer.

## Herkunft der Konfiguration

Die Dateien unter `config/` beruhen auf der Docker-Compose-Vorlage, die CIB software GmbH für CIB flow bereitstellt, und sind für die Schulung angepasst. Was geändert ist, steht im Kopf jeder Datei. Sie fallen nicht unter die MIT-Lizenz dieses Repos.
