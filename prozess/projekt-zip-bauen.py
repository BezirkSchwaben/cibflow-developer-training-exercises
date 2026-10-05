#!/usr/bin/env python3
"""Baut prozess/genehmigungsworkflow-projekt.zip, das Projekt-ZIP für das Prozessmanagement.

Aufbau wie ein Projekt-Export aus CIB flow:

    diagrams/genehmigungsworkflow.bpmn   aus prozess/genehmigungsworkflow.bpmn
    forms/antragsformular.json           aus prozess/formulare/antragsformular.json
    forms/genehmigungsformular.json      aus prozess/formulare/genehmigungsformular.json
    forms/nachbesserungsformular.json    aus prozess/formulare/nachbesserungsformular.json

Beim Import heißt jedes Formular wie seine Datei, das Modell verweist über diese Namen darauf.
Feste Zeitstempel und keine Kompression: Dieselben Dateien ergeben byte-gleich dasselbe ZIP.

Aufruf im Repo-Root, nach jeder Änderung an Modell oder Formularen:

    python3 prozess/projekt-zip-bauen.py
"""
import sys
import zipfile
from pathlib import Path

PROZESS = Path(__file__).resolve().parent
ZIEL = PROZESS / "genehmigungsworkflow-projekt.zip"
ZEITSTEMPEL = (2026, 1, 1, 0, 0, 0)

INHALT = {
    "diagrams/genehmigungsworkflow.bpmn": PROZESS / "genehmigungsworkflow.bpmn",
    "forms/antragsformular.json": PROZESS / "formulare" / "antragsformular.json",
    "forms/genehmigungsformular.json": PROZESS / "formulare" / "genehmigungsformular.json",
    "forms/nachbesserungsformular.json": PROZESS / "formulare" / "nachbesserungsformular.json",
}


def eintrag(name: str, rechte: int) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(name, date_time=ZEITSTEMPEL)
    info.create_system = 3  # Unix, auf jedem Betriebssystem gleich
    info.external_attr = rechte << 16
    info.compress_type = zipfile.ZIP_STORED
    return info


def main() -> None:
    ziel = Path(sys.argv[1]) if len(sys.argv) > 1 else ZIEL
    geschrieben = set()
    with zipfile.ZipFile(ziel, "w") as zip_datei:
        for name, quelle in sorted(INHALT.items()):
            # Jeder Ordner als eigener Eintrag vor seinen Dateien, wie in einem gepackten Projektordner
            ordner = name.split("/")[0] + "/"
            if ordner not in geschrieben:
                zip_datei.writestr(eintrag(ordner, 0o40755), b"")
                geschrieben.add(ordner)
            zip_datei.writestr(eintrag(name, 0o100644), quelle.read_bytes())
    print(f"{ziel.name} gebaut: {', '.join(sorted(INHALT))}")


if __name__ == "__main__":
    main()
