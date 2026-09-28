#!/usr/bin/env bash
# Trainer-Werkzeug: prüft den laufenden Stack mit allen drei Pfaden des Genehmigungsworkflows.
#
#   a) genehmigt:   External Task holen (fetchAndLock als worker), complete mit buchungsnummer
#   b) abgelehnt:   Instanz endet bei "Antrag abgelehnt", ablehnungMitgeteilt ist gesetzt
#   c) nachbessern: "Antrag nachbessern" liegt bei anna
#
# Aufruf im Ordner stack/, nachdem "docker compose up -d" durch ist:
#   ./smoke-test.sh          (Windows: in Git Bash "bash smoke-test.sh")
#
# Braucht nur bash und curl. Deployt prozess/genehmigungsworkflow.bpmn (unverändert
# erzeugt das keine neue Version), legt eigene Instanzen mit Business Key "smoke-..."
# an und löscht am Ende alle Instanzen dieses Laufs, die noch offen sind.
# Exit-Code 0: alles grün. Exit-Code 1: mindestens eine Prüfung fehlgeschlagen.
set -u

ENGINE_URL="${ENGINE_URL:-http://localhost:8080/engine-rest}"
PROZESS_KEY="${PROZESS_KEY:-Process_Genehmigung}"
TOPIC="${TOPIC:-genehmigung-verbuchen}"
# Relativ zum Ordner stack/, das funktioniert auch mit curl unter Git Bash (Windows)
cd "$(dirname "$0")" || exit 1
BPMN="${BPMN:-../prozess/genehmigungsworkflow.bpmn}"
WORKER_ID="smoke-test"
LAUF="smoke-$(date +%Y%m%d%H%M%S)-$$"

FEHLER=0
ANTWORT=""
STATUS=""

ok()     { echo "  OK      $*"; }
falsch() { echo "  FEHLER  $*"; FEHLER=$((FEHLER + 1)); }

# Request mit Basic Auth. Setzt STATUS (HTTP-Code) und ANTWORT (Body).
# Aufruf: rest BENUTZER METHODE PFAD [JSON-BODY]
rest() {
  local benutzer="$1" methode="$2" pfad="$3" ausgabe
  if [ $# -ge 4 ]; then
    ausgabe=$(curl -s --max-time 30 -w '\n%{http_code}' -u "$benutzer:$benutzer" -X "$methode" \
      -H 'Content-Type: application/json' --data "$4" "$ENGINE_URL$pfad")
  else
    ausgabe=$(curl -s --max-time 30 -w '\n%{http_code}' -u "$benutzer:$benutzer" -X "$methode" "$ENGINE_URL$pfad")
  fi
  STATUS="${ausgabe##*$'\n'}"
  ANTWORT="${ausgabe%$'\n'*}"
}

# Erster Wert eines JSON-Felds aus ANTWORT (Text, Zahl oder true/false). Reicht für die flachen Antworten der Engine.
wert() {
  printf '%s' "$ANTWORT" | grep -o -E "\"$1\":(\"[^\"]*\"|[^,}\"]*)" | head -n 1 | sed "s/^\"$1\"://; s/^\"//; s/\"$//"
}

pruefe() { # beschreibung erwartet ist
  if [ "$2" = "$3" ]; then ok "$1"; else falsch "$1 (erwartet: $2, ist: ${3:-leer})"; fi
}

# Startet als anna, prüft antragsteller und schließt "Antrag prüfen" als gerda ab.
# Setzt PI (Prozessinstanz-ID). Aufruf: antrag PFADNAME ENTSCHEIDUNG
antrag() {
  local bk="$LAUF-$1"
  PI=""
  rest anna POST "/process-definition/key/$PROZESS_KEY/start" \
    "{\"businessKey\":\"$bk\",\"variables\":{\"betrag\":{\"value\":1200,\"type\":\"Long\"},\"begruendung\":{\"value\":\"Smoke-Test $1\",\"type\":\"String\"}}}"
  pruefe "Start als anna" 200 "$STATUS"
  PI=$(wert id)
  [ -n "$PI" ] || { falsch "keine Prozessinstanz-ID in der Antwort: $ANTWORT"; return 1; }

  rest anna GET "/process-instance/$PI/variables/antragsteller"
  pruefe "antragsteller ist anna" anna "$(wert value)"

  rest gerda GET "/task?processInstanceId=$PI&candidateGroup=genehmiger"
  pruefe "\"Antrag prüfen\" liegt bei der Gruppe genehmiger" Task_Pruefen "$(wert taskDefinitionKey)"
  local task
  task=$(wert id)
  rest gerda GET "/task?processInstanceId=$PI&candidateUser=gerda"
  pruefe "gerda findet die Aufgabe als Kandidatin" "$task" "$(wert id)"

  rest gerda POST "/task/$task/complete" "{\"variables\":{\"entscheidung\":{\"value\":\"$2\",\"type\":\"String\"}}}"
  pruefe "gerda schließt mit entscheidung=$2 ab" 204 "$STATUS"
}

echo "Smoke-Test gegen $ENGINE_URL (Lauf $LAUF)"

echo "0) Engine und Anmeldung"
STATUS=$(curl -s --max-time 10 -o /dev/null -w '%{http_code}' "$ENGINE_URL/version")
pruefe "ohne Anmeldung abgewiesen" 401 "$STATUS"
rest worker GET /version
pruefe "Anmeldung als worker" 200 "$STATUS"
if [ "$STATUS" != "200" ]; then
  echo "Engine nicht erreichbar oder Benutzer fehlen. Läuft der Stack? docker compose ps; docker compose logs --tail 80 init"
  exit 1
fi

echo "   Deployment von $BPMN"
# deployment-name ist der Prozess-Key, wie bei "dotnet run -- deploy" im Worker und in der http-Datei
STATUS=$(curl -s --max-time 30 -o /dev/null -w '%{http_code}' -u worker:worker \
  -F "deployment-name=$PROZESS_KEY" -F "enable-duplicate-filtering=true" \
  -F "data=@$BPMN;filename=genehmigungsworkflow.bpmn" "$ENGINE_URL/deployment/create")
pruefe "deployment/create" 200 "$STATUS"

echo "a) genehmigt"
if antrag genehmigt genehmigt; then
  rest worker POST /external-task/fetchAndLock \
    "{\"workerId\":\"$WORKER_ID\",\"maxTasks\":1,\"asyncResponseTimeout\":5000,\"topics\":[{\"topicName\":\"$TOPIC\",\"lockDuration\":60000,\"businessKey\":\"$LAUF-genehmigt\"}]}"
  pruefe "fetchAndLock als worker" 200 "$STATUS"
  pruefe "External Task gehört zur Instanz" "$PI" "$(wert processInstanceId)"
  pruefe "betrag kommt mit" 1200 "$(printf '%s' "$ANTWORT" | grep -o '"betrag":{[^}]*}' | grep -o '"value":[0-9]*' | sed 's/"value"://')"
  ext=$(wert id)
  rest worker POST "/external-task/$ext/complete" \
    "{\"workerId\":\"$WORKER_ID\",\"variables\":{\"buchungsnummer\":{\"value\":\"B-SMOKE-0001\",\"type\":\"String\"}}}"
  pruefe "complete mit buchungsnummer" 204 "$STATUS"
  rest worker GET "/history/process-instance/$PI"
  pruefe "Instanz beendet" COMPLETED "$(wert state)"
  rest worker GET "/history/activity-instance?processInstanceId=$PI&activityId=End_Genehmigt"
  pruefe "Ende \"Antrag genehmigt\" erreicht" End_Genehmigt "$(wert activityId)"
  rest worker GET "/history/variable-instance?processInstanceId=$PI&variableName=buchungsnummer"
  pruefe "buchungsnummer in der History" B-SMOKE-0001 "$(wert value)"
fi

echo "b) abgelehnt"
if antrag abgelehnt abgelehnt; then
  rest worker GET "/history/process-instance/$PI"
  pruefe "Instanz beendet" COMPLETED "$(wert state)"
  rest worker GET "/history/activity-instance?processInstanceId=$PI&activityId=End_Abgelehnt"
  pruefe "Ende \"Antrag abgelehnt\" erreicht" End_Abgelehnt "$(wert activityId)"
  rest worker GET "/history/variable-instance?processInstanceId=$PI&variableName=ablehnungMitgeteilt"
  pruefe "ablehnungMitgeteilt gesetzt" true "$(wert value)"
  rest worker GET "/history/activity-instance?processInstanceId=$PI&activityId=Task_Verbuchen"
  pruefe "Gegenprobe: kein External Task" "[]" "$ANTWORT"
fi

echo "c) nachbessern"
if antrag nachbessern nachbessern; then
  rest anna GET "/task?processInstanceId=$PI"
  pruefe "\"Antrag nachbessern\" ist offen" Task_Nachbessern "$(wert taskDefinitionKey)"
  pruefe "Assignee ist anna" anna "$(wert assignee)"
fi

echo "Aufräumen"
rest demo GET "/process-instance?businessKeyLike=$LAUF%25"
for offen in $(printf '%s' "$ANTWORT" | grep -o -E '"id":"[^"]*"' | sed 's/^"id":"//; s/"$//'); do
  rest demo DELETE "/process-instance/$offen?skipCustomListeners=true"
  pruefe "offene Instanz $offen gelöscht" 204 "$STATUS"
done

echo
if [ "$FEHLER" -eq 0 ]; then
  echo "Alles grün."
  exit 0
fi
echo "$FEHLER Prüfung(en) fehlgeschlagen."
exit 1
