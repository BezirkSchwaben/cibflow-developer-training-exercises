# Musterlösung zu Übung 8 und 9

Für Übung 8 das umgebaute Modell `genehmigungsworkflow-entwickler.bpmn`. Für Übung 9 zwei vollständige Projekte mit demselben Aufbau wie der Startstand: `worker/` (C#) und `prozesstest-java/` (Java). Ihr baut und testet sie hier, ohne etwas über euren Stand zu kopieren.

## Lösung von Übung 8

`genehmigungsworkflow-entwickler.bpmn` ist das Modell nach dem Umbau in Übung 8: das fertige Modell nach Übung 7 mit „Genehmigung verbuchen“ als External Task auf dem Topic `genehmigung-verbuchen`. Hakt der Umbau, legt ihr diese Fassung im Repo-Root als euer Modell ab:

```bash
# bash, zsh, Git Bash
cp loesung/genehmigungsworkflow-entwickler.bpmn prozess/genehmigungsworkflow.bpmn
```

```powershell
# PowerShell
Copy-Item loesung\genehmigungsworkflow-entwickler.bpmn prozess\genehmigungsworkflow.bpmn
```

Danach deployt ihr sie im Ordner `worker/` mit `dotnet run --project src/GenehmigungWorker -- deploy`. Sie trägt die Process ID `Process_Genehmigung` und passt damit zum Projekt-ZIP aus dem Repo, `prozess/genehmigungsworkflow-projekt.zip`, nicht zu einem eigenen Projekt mit eigenem Key. `ProzessKey` in `appsettings.json` bleibt dann `Process_Genehmigung`.

## Bauen und testen

Jeweils vom Repo-Root aus, in bash und PowerShell gleich bis auf den Maven Wrapper:

```bash
cd loesung/worker
dotnet test --filter "Kategorie!=Prozesstest"   # ohne Engine: 9 Tests
dotnet test                                     # mit laufendem Stack und bereitgestelltem Modell: 12 Tests
dotnet run --project src/GenehmigungWorker      # startet den Worker der Musterlösung
```

```bash
cd loesung/prozesstest-java
./mvnw test                                     # 10 bestandene Tests, in PowerShell: .\mvnw.cmd test
```

Die Java-Tests `GenehmigungsworkflowTest` und `FehlerpfadTest` lesen Process ID, IDs, Topic und Fehlercode aus Klassen, die bpmn-to-code bei jedem Lauf aus den Modellkopien erzeugt, etwa `TASK_PRUEFEN.getValue()` statt `"Task_Pruefen"`. Wie das geht, steht in [prozesstest-java/README.md](../prozesstest-java/README.md#ids-aus-dem-modell). Die Demo-Klasse `GenehmigungsworkflowTag1Test` schreibt die IDs bewusst als Text, siehe [Demo](../prozesstest-java/README.md#demo-kapitel-10-trainer).

Die User Secrets aus Übung 8 gelten auch hier, beide Worker-Projekte haben dieselbe `UserSecretsId`. Mit eigenem Projekt tragt ihr euren `ProzessKey` auch in `loesung/worker/src/GenehmigungWorker/appsettings.json` ein.

## Mit eurem Stand vergleichen

Im Repo-Root, bash und PowerShell gleich:

```bash
git diff --no-index worker/src loesung/worker/src
git diff --no-index worker/tests loesung/worker/tests
git diff --no-index prozesstest-java/src loesung/prozesstest-java/src
```

Nach einem Build zeigen die ersten beiden Befehle auch die Build-Ausgaben unter `bin/` und `obj/`. Vergleicht dann einzelne Dateien, etwa `git diff --no-index worker/src/GenehmigungWorker/Program.cs loesung/worker/src/GenehmigungWorker/Program.cs`.

Was jede Datei macht und wie ihr einzelne übernehmt, steht im Aufgabenblatt unter [Übung 9, Musterlösung](../aufgaben/kapitel-12-worker-und-tests.md#musterlösung).

Für Trainer: `.github/scripts/loesung-abgleich.sh` prüft, auch in der CI, dass die Musterlösung jede Datei des Startstands enthält und nur in den Übungsdateien abweicht. Die Liste der Übungsdateien steht im Skript.
