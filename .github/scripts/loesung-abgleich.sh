#!/usr/bin/env bash
# Prüft, dass die Musterlösung unter loesung/ ein vollständiges Projekt bleibt und nur dort vom Startstand
# abweicht, wo die Übung in Kapitel 12 etwas ändert.
#
#   loesung/worker/            gleicher Aufbau wie worker/
#   loesung/prozesstest-java/  gleicher Aufbau wie prozesstest-java/
#
# Ändert ihr eine Datei im Startstand, die nicht zur Übung gehört (etwa EngineHelfer.cs oder pom.xml),
# kopiert sie nach loesung/. Kommt eine Übungsdatei dazu, tragt sie unten ein.
#
# Dazu: loesung/worker-hexagonal/ ist derselbe Worker nach Ports und Adaptern geschnitten, mit eigenem Aufbau.
# Einige Dateien übernimmt sie unverändert aus loesung/worker/, ExternalTaskClient.cs mit anderem Namespace.
# Ändert ihr eine davon in loesung/worker/, kopiert sie mit (Liste HEXAGONAL_GLEICH unten).
# Aufruf im Repo-Root oder irgendwo darunter: .github/scripts/loesung-abgleich.sh
set -euo pipefail
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"

# Diese Dateien bearbeitet ihr in der Übung, hier darf die Lösung vom Startstand abweichen.
ABWEICHEND="
worker/src/GenehmigungWorker/ExternalTaskClient.cs
worker/src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs
worker/src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs
worker/src/GenehmigungWorker/Program.cs
worker/tests/GenehmigungWorker.Tests/BuchungssystemFake.cs
worker/tests/GenehmigungWorker.Tests/GenehmigungVerbuchenHandlerTests.cs
worker/tests/GenehmigungWorker.Tests/GenehmigungsworkflowTests.cs
prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTest.java
"

# Diese Dateien gibt es nur in der Lösung.
NUR_LOESUNG="
worker/src/GenehmigungWorker/Fachsystem/BuchungAbgelehntException.cs
worker/tests/GenehmigungWorker.Tests/ExternalTaskClientTests.cs
worker/tests/GenehmigungWorker.Tests/FehlerpfadTests.cs
prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/FehlerpfadTest.java
prozesstest-java/src/main/resources/genehmigungsworkflow-tag1.bpmn
prozesstest-java/src/test/java/io/miragon/schulung/genehmigung/GenehmigungsworkflowTag1Test.java
"

# Diese Dateien gibt es nur im Startstand.
NUR_STARTSTAND="
prozesstest-java/README.md
"

steht_in() { printf '%s\n' "$2" | grep -qxF "$1"; }

fehler=0
melde() { echo "::error::$1"; fehler=1; }

for datei in $(git ls-files worker prozesstest-java); do
  if steht_in "$datei" "$NUR_STARTSTAND"; then
    continue
  fi
  if [ ! -f "loesung/$datei" ]; then
    melde "loesung/$datei fehlt. Die Musterlösung ist ein vollständiges Projekt: cp $datei loesung/$datei"
  elif cmp -s "$datei" "loesung/$datei"; then
    if steht_in "$datei" "$ABWEICHEND"; then
      melde "loesung/$datei ist gleich dem Startstand, steht aber als Übungsdatei in .github/scripts/loesung-abgleich.sh"
    fi
  elif ! steht_in "$datei" "$ABWEICHEND"; then
    melde "loesung/$datei weicht vom Startstand ab, gehört aber nicht zur Übung. Angleichen: cp $datei loesung/$datei (nach der Demo in Kapitel 10: git restore loesung/prozesstest-java)"
  fi
done

for datei in $(git ls-files loesung/worker loesung/prozesstest-java); do
  start="${datei#loesung/}"
  if [ ! -f "$start" ] && ! steht_in "$start" "$NUR_LOESUNG"; then
    melde "$datei gibt es nur in der Musterlösung, steht aber nicht in .github/scripts/loesung-abgleich.sh"
  fi
done

for datei in $NUR_LOESUNG; do
  if [ ! -f "loesung/$datei" ]; then
    melde "loesung/$datei fehlt, steht aber als Datei der Musterlösung in .github/scripts/loesung-abgleich.sh"
  fi
done

# Die hexagonale Fassung übernimmt diese Dateien aus loesung/worker/: links dort, rechts in loesung/worker-hexagonal/.
HEXAGONAL_GLEICH="
src/GenehmigungWorker/Einstellungen.cs:src/GenehmigungWorker/Einstellungen.cs
src/GenehmigungWorker/Deploy.cs:src/GenehmigungWorker/Deploy.cs
src/GenehmigungWorker/appsettings.json:src/GenehmigungWorker/appsettings.json
tests/GenehmigungWorker.Tests/EngineHelfer.cs:tests/GenehmigungWorker.Tests/EngineHelfer.cs
tests/GenehmigungWorker.Tests/ExternalTaskClientTests.cs:tests/GenehmigungWorker.Tests/ExternalTaskClientTests.cs
"
for paar in $HEXAGONAL_GLEICH; do
  vorlage="loesung/worker/${paar%%:*}"
  kopie="loesung/worker-hexagonal/${paar#*:}"
  if ! cmp -s "$vorlage" "$kopie"; then
    melde "$kopie weicht von $vorlage ab. Angleichen: cp $vorlage $kopie"
  fi
done

# ExternalTaskClient.cs liegt in der hexagonalen Fassung im Engine-Adapter, nur die namespace-Zeile ist anders
vorlage=loesung/worker/src/GenehmigungWorker/ExternalTaskClient.cs
kopie=loesung/worker-hexagonal/src/GenehmigungWorker/Adapter/Engine/ExternalTaskClient.cs
if ! sed 's/^namespace GenehmigungWorker;$/namespace GenehmigungWorker.Adapter.Engine;/' "$vorlage" | cmp -s - "$kopie"; then
  melde "$kopie weicht von $vorlage ab, nicht nur im Namespace. Angleichen: sed 's/^namespace GenehmigungWorker;\$/namespace GenehmigungWorker.Adapter.Engine;/' $vorlage > $kopie"
fi

if [ "$fehler" -eq 0 ]; then
  echo "Musterlösung vollständig, Abweichungen nur in den Übungsdateien. Die hexagonale Fassung teilt ihre Kopien unverändert."
fi
exit "$fehler"
