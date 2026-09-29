#!/bin/sh
# Legt die Schulungsbenutzer und die Gruppe genehmiger in der Engine an.
#
# Läuft im Compose-Dienst "init" (Image curlimages/curl, nur sh und curl):
# in stack/ gegen CIB flow, in .github/ci-stack/ gegen CIB seven.
# Idempotent: Was schon existiert, bleibt unverändert. Deshalb schadet ein
# zweiter Lauf nicht, etwa nach "docker compose up -d" auf einem bestehenden Stack.
#
# Passwort jeweils gleich dem Benutzernamen. Nur lokal verwenden.
set -eu

ENGINE_URL="${ENGINE_URL:-http://flow-cibseven-spring:8080/engine-rest}"
ADMIN="${ADMIN_USER:-demo}:${ADMIN_PASSWORD:-demo}"
MAX_VERSUCHE="${MAX_VERSUCHE:-120}"
ANTWORT=/tmp/antwort.json

log() { echo "[init] $*"; }

fehler() {
  log "FEHLER: $*"
  if [ -s "$ANTWORT" ]; then log "Antwort der Engine: $(cat "$ANTWORT")"; fi
  exit 1
}

# Schickt einen Request als Admin und gibt nur den HTTP-Status aus (000 = keine Verbindung).
# Aufruf: anfrage METHODE PFAD [JSON-BODY]
anfrage() {
  : > "$ANTWORT"
  if [ $# -ge 3 ]; then
    curl -s --max-time 15 -o "$ANTWORT" -w '%{http_code}' -u "$ADMIN" -X "$1" \
      -H 'Content-Type: application/json' --data "$3" "$ENGINE_URL$2" || true
  else
    curl -s --max-time 15 -o "$ANTWORT" -w '%{http_code}' -u "$ADMIN" -X "$1" "$ENGINE_URL$2" || true
  fi
}

# 1. Warten, bis die Engine antwortet und der Admin-Benutzer angelegt ist
log "Warte auf die Engine unter $ENGINE_URL (höchstens $MAX_VERSUCHE Versuche, etwa zehn Minuten)"
# Gezählt werden Versuche, nicht die Uhrzeit: Schläft der Rechner während des Starts,
# verbraucht das keine Versuche. Jede Anfrage endet spätestens nach 15 s (--max-time),
# eine hängende Verbindung blockiert das Warten also nicht.
versuch=0
until [ "$(anfrage GET /version)" = "200" ]; do
  versuch=$((versuch + 1))
  if [ "$versuch" -ge "$MAX_VERSUCHE" ]; then
    fehler "Engine antwortet nach $MAX_VERSUCHE Versuchen nicht mit 200 auf GET /version"
  fi
  sleep 5
done
log "Engine bereit: $(cat "$ANTWORT")"

# 2. Gruppe anlegen
gruppe_anlegen() { # id name
  code=$(anfrage GET "/group/$1")
  case "$code" in
    200) log "Gruppe $1 gibt es schon" ;;
    404)
      code=$(anfrage POST /group/create "{\"id\":\"$1\",\"name\":\"$2\",\"type\":\"WORKFLOW\"}")
      [ "$code" = "204" ] || fehler "Gruppe $1 anlegen: HTTP $code"
      log "Gruppe $1 angelegt" ;;
    *) fehler "Gruppe $1 prüfen: HTTP $code" ;;
  esac
}

# 3. Benutzer anlegen, Passwort = Benutzername
benutzer_anlegen() { # id vorname nachname
  code=$(anfrage GET "/user/$1/profile")
  case "$code" in
    200) log "Benutzer $1 gibt es schon" ;;
    404)
      code=$(anfrage POST /user/create "{\"profile\":{\"id\":\"$1\",\"firstName\":\"$2\",\"lastName\":\"$3\",\"email\":\"$1@example.org\"},\"credentials\":{\"password\":\"$1\"}}")
      [ "$code" = "204" ] || fehler "Benutzer $1 anlegen: HTTP $code"
      log "Benutzer $1 angelegt" ;;
    *) fehler "Benutzer $1 prüfen: HTTP $code" ;;
  esac
}

# 4. Mitgliedschaft anlegen, wenn sie fehlt
mitglied_machen() { # gruppe benutzer
  code=$(anfrage GET "/group/count?id=$1&member=$2")
  [ "$code" = "200" ] || fehler "Mitgliedschaft $2 in $1 prüfen: HTTP $code"
  if grep -q '"count":1' "$ANTWORT"; then
    log "$2 ist schon in $1"
  else
    code=$(anfrage PUT "/group/$1/members/$2")
    [ "$code" = "204" ] || fehler "$2 in $1 aufnehmen: HTTP $code"
    log "$2 in $1 aufgenommen"
  fi
}

# 5. Tasklist-Filter anlegen, wenn es noch keinen mit diesem Namen gibt.
# Die Engine legt beim ersten Start nur "All tasks" an. Diese beiden Filter zeigen
# die Aufgaben so, wie das Modell sie verteilt: "Antrag prüfen" bei der Gruppe
# genehmiger, "Antrag nachbessern" direkt bei der Antragstellerin.
filter_anlegen() { # name priorität query-json
  name_url=$(printf '%s' "$1" | sed 's/ /%20/g')
  code=$(anfrage GET "/filter/count?resourceType=Task&name=$name_url")
  [ "$code" = "200" ] || fehler "Filter $1 prüfen: HTTP $code"
  if grep -q '"count":0' "$ANTWORT"; then
    code=$(anfrage POST /filter/create "{\"resourceType\":\"Task\",\"name\":\"$1\",\"owner\":null,\"query\":$3,\"properties\":{\"priority\":$2}}")
    [ "$code" = "200" ] || fehler "Filter $1 anlegen: HTTP $code"
    log "Filter $1 angelegt"
  else
    log "Filter $1 gibt es schon"
  fi
}

gruppe_anlegen genehmiger "Genehmiger"

benutzer_anlegen anna "Anna" "Antragstellerin"
benutzer_anlegen gerda "Gerda" "Genehmigerin"
benutzer_anlegen worker "Worker" "Technischer Benutzer"

mitglied_machen genehmiger gerda

# Einfache Anführungszeichen: ${...} ist hier ein Ausdruck der Engine, keine Shell-Variable.
# candidateUser statt candidateGroups: Die Engine sucht die Gruppen des Benutzers selbst.
# ${currentUserGroups()} scheitert dagegen bei Benutzern ohne Gruppe (anna, worker).
filter_anlegen "Meine Aufgaben" 1 '{"assigneeExpression":"${currentUser()}"}'
filter_anlegen "Aufgaben meiner Gruppen" 2 '{"candidateUserExpression":"${currentUser()}"}'

log "Fertig. Benutzer: anna, gerda (Gruppe genehmiger), worker. Passwort jeweils wie der Benutzername."
