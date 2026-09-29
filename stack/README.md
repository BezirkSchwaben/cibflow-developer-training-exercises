# CIB flow lokal

Dieser Ordner startet CIB flow auf eurem Laptop: die Engine, die Weboberfläche und die Werkzeuge (Modeler, easyForm, Prozessmanagement, Ressourcen). Nur für die Schulung, nicht produktiv verwenden.

## Voraussetzungen

- Docker Desktop mit mindestens 8 GB Speicher für Docker (Einstellungen, Resources, Memory). Der Stack braucht im Leerlauf rund 5,5 GB.
- Rund 6 GB freier Plattenplatz. Beim ersten Start lädt Docker sieben Images, zusammen mehrere GB. Plant dafür je nach Leitung einige Minuten ein.
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
| `anna` | Antragstellerin | stellt Anträge über „Prozess starten“, bekommt „Antrag nachbessern“ |
| `gerda` | Genehmigerin, Gruppe `genehmiger` | bearbeitet „Antrag prüfen“ |
| `worker` | technischer Benutzer | für euren C#-Worker, die Tests und REST-Aufrufe |

Die Autorisierung ist lokal aus: Jeder angemeldete Benutzer sieht alle Kacheln und darf alles. Wem eine Aufgabe gehört, zeigen in „Aufgaben bearbeiten“ die Filter „Meine Aufgaben“ und „Aufgaben meiner Gruppen“.

## Adressen

| Was | Adresse |
|---|---|
| Weboberfläche mit allen Kacheln | http://localhost:7083/client |
| REST-API der Engine (Basic Auth, etwa `worker`/`worker`) | http://localhost:8080/engine-rest |
| easyForm | http://localhost:7086/easy-form |
| Modeler | http://localhost:7088/flow-modeler |
| Prozessmanagement | http://localhost:7089/flow-process-management |
| Ressourcen | http://localhost:7090/flow-resource |
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

**Ein Port ist belegt.** `docker compose up -d` meldet `port is already allocated` oder `address already in use`. Findet das Programm mit `lsof -i :8080` (macOS, Linux) oder `netstat -ano | findstr :8080` (Windows) und beendet es. Geht das nicht, legt den Dienst auf einen anderen Port: In `docker-compose.yml` die linke Portnummer ändern, etwa `"127.0.0.1:8081:8080"`, und in `config/common-config.yaml` die `external-url` des Dienstes anpassen. Bei der Engine zusätzlich `EngineUrl` in `src/GenehmigungWorker/appsettings.json` und `@baseUrl` in `http/genehmigungsworkflow.http`.

**Dienste starten immer wieder neu, die Oberfläche bleibt unvollständig.** Docker hat zu wenig Speicher. `docker compose ps` zeigt `Restarting` oder `Exited (137)`. Gebt Docker in Docker Desktop mindestens 8 GB (Einstellungen, Resources, Memory) und startet mit `docker compose up -d` neu.

**Das Formular einer Aufgabe bleibt leer.** Die Aufgabe ist euch noch nicht zugewiesen, oben steht der Hinweis „Aufgabe ist Ihnen nicht zugewiesen“. Klickt auf „Mir zuweisen“, dann erscheint das Formular.

**„Prozess starten“ meldet „Das Formular wurde nicht gefunden“.** Das Modell ist in der Engine, die easyForms dazu fehlen. Importiert das Projekt-ZIP im Prozessmanagement, wie im Aufgabenblatt zu [Kapitel 11](../aufgaben/kapitel-11-lokales-setup.md) beschrieben.

Mehr sehen die Logs: `docker compose logs --tail 100 <dienst>`, etwa `flow-cibseven-spring` für die Engine oder `init` für das Anlegen der Benutzer.
