# Musterlösung zu Übung 9, hexagonal geschnitten

Derselbe Worker wie in [`loesung/worker/`](../worker/), nach Ports und Adaptern geschnitten. Er tut genau dasselbe: gleiche Logzeilen, gleiche Variablen, gleicher Idempotenz-Schlüssel (Business Key, sonst Prozessinstanz-ID), gleiche Fehlerbehandlung mit `complete`, `failure` und `bpmnError`. Anders ist nur, wo welcher Code liegt: Die Fachlogik steht in einem eigenen Projekt, das die Engine nie kennt.

In Übung 9 baut ihr die Schichten-Fassung, so wie sie unter `worker/` angelegt ist. Diese Fassung ist zum Lesen und Vergleichen da, niemand muss umbauen.

## Das Sechseck in diesem Worker

```
 eingehend: treibt den Kern

 Engine ──▶ ExternalTaskWorker ──▶ GenehmigungVerbuchenAdapter ──▶ IGenehmigungVerbuchen  (Port)
 (externes  Adapter/Engine/        Adapter/Engine/                        │
  System)   Schleife, complete,    Variablen ▶ Genehmigung,               ▼
            failure, bpmnError     buchungsnummer ◀ Nummer         GenehmigungVerbuchen   (Kern, Use Case)
                                                                          │
                                                                          ▼
 Fachsystem ◀── BuchungssystemSimulation ◀──────────────────────── IBuchungssystem        (Port)
 (externes      Adapter/Fachsystem/
  System)

 ausgehend: vom Kern getrieben
```

Die Pfeile zeigen, wer wen antreibt, nicht die Richtung der HTTP-Aufrufe: Den Task holt `ExternalTaskWorker` selbst per fetchAndLock.

- **Die Engine ist kein Adapter, sie ist das externe System.** Die Adapter stehen zwischen ihr und dem Kern.
- **Eingehend treibt den Kern:** `ExternalTaskWorker` holt den Task, `GenehmigungVerbuchenAdapter` übersetzt die Variablen in eine `Genehmigung` und ruft den Port `IGenehmigungVerbuchen`.
- **Ausgehend wird vom Kern getrieben:** Der Use Case `GenehmigungVerbuchen` ruft den Port `IBuchungssystem`, die `BuchungssystemSimulation` erfüllt ihn.
- **Der Kern spricht nur in Domänenobjekten:** `Genehmigung` rein, Buchungsnummer raus, bei einer Ablehnung `BuchungAbgelehntException`. `ExternalTask`, Variablennamen des Modells, HTTP und JSON bleiben in den Adaptern.
- **Die Abhängigkeit zeigt nach innen:** `GenehmigungWorker` referenziert `GenehmigungWorker.Domaene`, nie umgekehrt. Dass beide Namen mit `GenehmigungWorker` beginnen, ordnet nur und schafft keine Abhängigkeit. Die Ports gehören der Domäne.

## Aufbau

```
loesung/worker-hexagonal/
├── GenehmigungWorker.sln                          # alle drei Projekte, hier laufen die dotnet-Befehle
├── src/GenehmigungWorker.Domaene/                 # Mitte des Sechsecks, ohne PackageReference und ohne ProjectReference
│   ├── Genehmigung.cs                             # Domänenobjekt: Schluessel, Antragsteller, Betrag, Begruendung
│   ├── GenehmigungVerbuchen.cs                    # der Use Case, erfüllt den eingehenden Port, ruft den ausgehenden
│   ├── BuchungAbgelehntException.cs               # fachliche Ablehnung, Teil des Port-Vertrags
│   └── Ports/
│       ├── IGenehmigungVerbuchen.cs               # eingehender Port
│       └── IBuchungssystem.cs                     # ausgehender Port
├── src/GenehmigungWorker/                         # Konsolen-App: Adapter und Zusammenbau
│   ├── Program.cs                                 # nur Zusammenbau: Einstellungen, deploy, Adapter verdrahten, Worker starten
│   ├── Einstellungen.cs, Deploy.cs                # unverändert aus loesung/worker
│   ├── appsettings.json                           # unverändert aus loesung/worker
│   └── Adapter/
│       ├── Engine/
│       │   ├── ExternalTaskClient.cs              # aus loesung/worker, nur der Namespace ist anders
│       │   ├── ExternalTaskWorker.cs              # die Schleife: fetchAndLock, complete, failure, bpmnError
│       │   └── GenehmigungVerbuchenAdapter.cs     # Variablen lesen, Use Case rufen, buchungsnummer zurück
│       └── Fachsystem/
│           └── BuchungssystemSimulation.cs        # erfüllt IBuchungssystem, buchungen.json neben der DLL
└── tests/GenehmigungWorker.Tests/
    ├── GenehmigungVerbuchenTests.cs               # Use Case mit Fake, ohne Engine und ohne ExternalTask
    ├── GenehmigungVerbuchenAdapterTests.cs        # Abbildung der Variablen: Text, Zahl, de-DE, fehlende Variable
    ├── BuchungssystemSimulationTests.cs           # Idempotenz und Ablehnung der Simulation
    ├── ArchitekturTests.cs                        # "Die Domäne kennt die Engine nicht"
    ├── ExternalTaskClientTests.cs, EngineHelfer.cs  # unverändert aus loesung/worker
    ├── BuchungssystemFake.cs                      # Fake des ausgehenden Ports
    ├── GenehmigungsworkflowTests.cs               # Prozesstests gegen die Engine, über Adapter und Use Case
    └── FehlerpfadTests.cs                         # Prozesstest zum fachlichen Fehler, über Adapter und Use Case
```

## Schichten-Fassung und hexagonale Fassung

| Schichten-Fassung (`loesung/worker/`) | Hexagonale Fassung (`loesung/worker-hexagonal/`) | Was sich ändert |
|---|---|---|
| `src/GenehmigungWorker/Handlers/GenehmigungVerbuchenHandler.cs` | `src/GenehmigungWorker/Adapter/Engine/GenehmigungVerbuchenAdapter.cs` und `src/GenehmigungWorker.Domaene/GenehmigungVerbuchen.cs` | Der Handler liest Variablen und ruft das Fachsystem. Jetzt liest der Adapter die Variablen und baut eine `Genehmigung`, der Use Case verbucht sie. |
| (vier einzelne Werte) | `src/GenehmigungWorker.Domaene/Genehmigung.cs` | neu: das Domänenobjekt, das durch den Kern läuft |
| (keine Datei) | `src/GenehmigungWorker.Domaene/Ports/IGenehmigungVerbuchen.cs` | neu: der eingehende Port, über ihn ruft der Adapter den Kern |
| `src/GenehmigungWorker/Fachsystem/IBuchungssystem.cs` | `src/GenehmigungWorker.Domaene/Ports/IBuchungssystem.cs` | gehört jetzt der Domäne, `Verbuchen` nimmt eine `Genehmigung` statt vier Werten |
| `src/GenehmigungWorker/Fachsystem/BuchungAbgelehntException.cs` | `src/GenehmigungWorker.Domaene/BuchungAbgelehntException.cs` | gehört jetzt der Domäne, als Teil des Port-Vertrags |
| `src/GenehmigungWorker/Fachsystem/BuchungssystemSimulation.cs` | `src/GenehmigungWorker/Adapter/Fachsystem/BuchungssystemSimulation.cs` | ausgehender Adapter, nimmt eine `Genehmigung`, sonst gleich |
| `src/GenehmigungWorker/Program.cs` | `src/GenehmigungWorker/Program.cs` und `src/GenehmigungWorker/Adapter/Engine/ExternalTaskWorker.cs` | `Program.cs` baut nur noch zusammen, die Schleife ist ein eigener Adapter, Logzeilen gleich |
| `src/GenehmigungWorker/ExternalTaskClient.cs` | `src/GenehmigungWorker/Adapter/Engine/ExternalTaskClient.cs` | nur der Namespace |
| `Einstellungen.cs`, `Deploy.cs`, `appsettings.json` | gleich | unverändert |
| `tests/.../GenehmigungVerbuchenHandlerTests.cs` | `GenehmigungVerbuchenTests.cs`, `GenehmigungVerbuchenAdapterTests.cs`, `BuchungssystemSimulationTests.cs` | aufgeteilt nach Kern, eingehendem und ausgehendem Adapter |
| `tests/.../BuchungssystemFake.cs` | `tests/.../BuchungssystemFake.cs` | merkt sich jede `Genehmigung` und kann ablehnen, damit der Use Case ohne Simulation testbar ist |
| (keine Datei) | `tests/.../ArchitekturTests.cs` | neu: prüft die Regel bei jedem Testlauf |
| `tests/.../ExternalTaskClientTests.cs`, `EngineHelfer.cs` | gleich | unverändert |
| `tests/.../GenehmigungsworkflowTests.cs`, `FehlerpfadTests.cs` | gleich benannt | dieselben Fälle, über Adapter und Use Case statt Handler |

Die übernommenen Dateien sprechen in Kommentaren und Meldungen weiter von der Schichten-Fassung: Mit dem Handler ist hier der `GenehmigungVerbuchenAdapter` gemeint, mit dem Ordner `worker/` der Ordner `loesung/worker-hexagonal/`.

Den Unterschied seht ihr im Repo-Root, bash und PowerShell gleich: `git diff --no-index loesung/worker/src loesung/worker-hexagonal/src`. Nach einem Build zeigt das auch `bin/` und `obj/`, vergleicht dann einzelne Dateien.

## Warum so geschnitten

- **Engine-neutral.** Schon in der Schichten-Fassung spricht der Handler nicht mit der Engine, aber er bekommt einen `ExternalTask` und kennt die Variablennamen des Modells. Hier kennt nur das Projekt `GenehmigungWorker` die Engine, ihre REST-API und ihre Begriffe: die Adapter unter `Adapter/Engine/`, dazu `Deploy.cs` und `Einstellungen.cs`. Wechselt die Engine, tauscht ihr diese Dateien, die Domäne bleibt, wie sie ist.
- **Ohne Engine testbar.** `GenehmigungVerbuchenTests` baut weder einen Task noch ein Dictionary mit Variablen, nur eine `Genehmigung` und einen Fake. Wächst die Fachlogik, wächst sie im Use Case, und ihre Tests bleiben so einfach.
- **Compiler und Architekturtest sichern die Regel.** `GenehmigungWorker.Domaene.csproj` hat weder PackageReference noch ProjectReference: Ein `using GenehmigungWorker.Adapter.Engine;` in der Domäne baut nicht. Was der Compiler zulässt, etwa ein neues Paket, sobald die Domäne einen Typ daraus benutzt, oder `HttpClient` aus .NET selbst, fängt `ArchitekturTests`: Die Domäne referenziert nur Assemblies von .NET, kein HTTP und kein JSON, und alle ihre Typen liegen in `GenehmigungWorker.Domaene`. Auf der JVM leistet ArchUnit dasselbe.
- **Die Ablehnung bleibt fachlich.** Die Domäne sagt nur „abgelehnt“ und warum. Dass daraus `bpmnError` mit `BUCHUNG_ABGELEHNT` wird, entscheidet `ExternalTaskWorker`, denn der Code gehört zum Modell.

## Wo es endet

- **Das Modell zieht nicht automatisch mit.** Bei einem Wechsel der Engine müsst ihr das Modell oft anpassen: andere Erweiterungen am Service Task, andere Formulare, andere Ausdrücke. Der Schnitt schützt euren Code, nicht das BPMN.
- **Eine Remote-Engine bleibt eventually consistent.** Der Worker verbucht und meldet danach `complete`, das sind zwei Schritte ohne gemeinsame Transaktion. Stirbt er dazwischen, kommt der Task noch einmal. Deshalb trägt die `Genehmigung` den Idempotenz-Schlüssel, daran ändert kein Schnitt etwas.
- **Was über den gemeinsamen Nenner der Engines hinausgeht, bleibt im Adapter.** Retries, Incidents und `bpmnError` sind Begriffe der Engine. Ein neuer Adapter muss sie für seine Engine neu abbilden.
- **Mehr Dateien, ein Umweg mehr.** Für einen Worker mit einer einzigen Regel ist die Schichten-Fassung genauso gut. Der Schnitt lohnt sich, wenn die Fachlogik wächst, wenn mehrere Eingänge denselben Use Case rufen, etwa ein REST-Controller neben dem Worker, oder wenn ein Wechsel der Engine absehbar ist.

Mehr dazu im Miragon-Blog: [Camunda-7-Migration: Neue Engine, alte Abhängigkeit?](https://miragon.io/blog/camunda-7-migration-neue-engine-alte-abhaengigkeit/)

## Bauen und testen

Im Repo-Root, mit denselben Zugangsdaten wie für `loesung/worker/`. Beide Worker haben dieselbe `UserSecretsId`, eure User Secrets aus Übung 8 gelten also auch hier. Mit eigenem Projekt tragt ihr euren `ProzessKey` auch in `loesung/worker-hexagonal/src/GenehmigungWorker/appsettings.json` ein. Stoppt vorher jeden anderen Worker, alle hören auf dasselbe Topic.

```bash
# bash, zsh, Git Bash
cd loesung/worker-hexagonal
dotnet build
dotnet test --filter "Kategorie!=Prozesstest"   # ohne Engine: 14 Tests
dotnet test                                     # mit laufendem Stack und bereitgestelltem Modell: 17 Tests
dotnet run --project src/GenehmigungWorker      # startet den Worker der hexagonalen Fassung
```

```powershell
# PowerShell
cd loesung\worker-hexagonal
dotnet build
dotnet test --filter "Kategorie!=Prozesstest"   # ohne Engine: 14 Tests
dotnet test                                     # mit laufendem Stack und bereitgestelltem Modell: 17 Tests
dotnet run --project src\GenehmigungWorker      # startet den Worker der hexagonalen Fassung
```

Nur die Architekturtests: `dotnet test --filter "FullyQualifiedName~ArchitekturTests"`. Der Worker legt seine Buchungen in `src/GenehmigungWorker/bin/Debug/net10.0/buchungen.json` ab, eine eigene Datei, getrennt von der in `loesung/worker/`. Die beiden Fassungen kennen ihre Buchungen gegenseitig nicht, die Nummern beginnen deshalb wieder bei 0001.
