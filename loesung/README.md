# Musterlösung zu Kapitel 12

Zwei vollständige Projekte mit demselben Aufbau wie der Startstand: `worker/` (C#) und `prozesstest-java/` (Java). Ihr baut und testet sie hier, ohne etwas über euren Stand zu kopieren.

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
./mvnw test                                     # 6 bestandene Tests, in PowerShell: .\mvnw.cmd test
```

Die User Secrets aus Kapitel 11 gelten auch hier, beide Worker-Projekte haben dieselbe `UserSecretsId`. Mit eigenem Projekt tragt ihr euren `ProzessKey` auch in `loesung/worker/src/GenehmigungWorker/appsettings.json` ein.

## Mit eurem Stand vergleichen

Im Repo-Root, bash und PowerShell gleich:

```bash
git diff --no-index worker/src loesung/worker/src
git diff --no-index worker/tests loesung/worker/tests
git diff --no-index prozesstest-java/src loesung/prozesstest-java/src
```

Nach einem Build zeigen die ersten beiden Befehle auch die Build-Ausgaben unter `bin/` und `obj/`. Vergleicht dann einzelne Dateien, etwa `git diff --no-index worker/src/GenehmigungWorker/Program.cs loesung/worker/src/GenehmigungWorker/Program.cs`.

Was jede Datei macht und wie ihr einzelne übernehmt, steht im Aufgabenblatt unter [Kapitel 12, Musterlösung](../aufgaben/kapitel-12-worker-und-tests.md#musterlösung).

Für Trainer: `.github/scripts/loesung-abgleich.sh` prüft, auch in der CI, dass die Musterlösung jede Datei des Startstands enthält und nur in den Übungsdateien abweicht. Die Liste der Übungsdateien steht im Skript.
