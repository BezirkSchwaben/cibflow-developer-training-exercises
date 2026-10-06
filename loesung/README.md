# Musterlösung zu Übung 8 und 9

Für Übung 8 das umgebaute Modell `genehmigungsworkflow-entwickler.bpmn`. Für Übung 9 zwei vollständige Projekte mit demselben Aufbau wie der Startstand: `worker/` (C#) und `prozesstest-java/` (Java). Ihr baut und testet sie hier, ohne etwas über euren Stand zu kopieren. Dazu liegt unter `worker-hexagonal/` derselbe Worker noch einmal, nach Ports und Adaptern geschnitten, zum Lesen und Vergleichen. Unter `element-template/` liegt die Lösung des Bonus „Euer Worker als Baustein“.

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

Nach dem Lauf liegt je Testklasse ein Abdeckungsbericht unter `loesung/prozesstest-java/target/process-test-coverage/<Testklasse>/report.html`, etwa `io.miragon.schulung.genehmigung.GenehmigungsworkflowTest/report.html`. Öffnet ihn im Browser: Er zeigt das Modell, darin grün, was die Tests durchlaufen haben, und die Abdeckung in Prozent. In der Musterlösung sind es für `GenehmigungsworkflowTest` und `GenehmigungsworkflowTag1Test` je 21 von 27, `77.78%`, weiß bleibt die Rücknahme, für `FehlerpfadTest` 12 von 12, `100.00%`. Was der Bericht zeigt, steht in [prozesstest-java/README.md](../prozesstest-java/README.md#abdeckung-im-modell).

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

## Hexagonale Fassung

`worker-hexagonal/` macht dasselbe wie `worker/`, mit denselben Logzeilen, Variablen und Fehlern. Die Fachlogik steht dort in einem eigenen Projekt, `GenehmigungWorker.Domaene`, das die Engine nicht kennt: Zwischen Engine und Fachlogik stehen Adapter, und die Fachlogik spricht nur über ihre Ports nach außen. In Übung 9 baut ihr die Schichten-Fassung wie unter `worker/`, umbauen müsst ihr nichts. Was anders ist, warum und wo der Schnitt an Grenzen stößt, steht in [worker-hexagonal/README.md](worker-hexagonal/README.md).

Vom Repo-Root aus, in bash und PowerShell gleich:

```bash
cd loesung/worker-hexagonal
dotnet test --filter "Kategorie!=Prozesstest"   # ohne Engine: 14 Tests
dotnet test                                     # mit laufendem Stack und bereitgestelltem Modell: 17 Tests
dotnet run --project src/GenehmigungWorker      # startet den Worker der hexagonalen Fassung
```

Zugangsdaten und `ProzessKey` gelten wie für `worker/`, und auch hier stoppt ihr vorher euren eigenen Worker. Für Trainer: `.github/scripts/loesung-abgleich.sh` prüft auch, dass die Dateien, die `worker-hexagonal/` unverändert aus `worker/` übernimmt, gleich bleiben.

## Element Template

`element-template/genehmigung-verbuchen.json` macht euren Worker zu einem Baustein im Katalog des Modelers: Der Service Task bekommt `camunda:type` external und das Topic `genehmigung-verbuchen` fest aus der Vorlage, der Katalog zeigt ihn im Abschnitt „Genehmigungsworkflow“ als „Extern“ mit Version 1.0.0. Hochladen, anwenden und deployen erklärt das Aufgabenblatt unter [Bonus: Euer Worker als Baustein](../aufgaben/kapitel-12-worker-und-tests.md#bonus-euer-worker-als-baustein). Für Trainer: Die GitHub Action prüft, dass Typ und Topic der Vorlage zu `Task_Verbuchen` in `genehmigungsworkflow-entwickler.bpmn` passen.
