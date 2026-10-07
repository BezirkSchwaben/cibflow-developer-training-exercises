// Einstiegspunkt des Workers in der hexagonalen Fassung, und nur noch Zusammenbau (Composition Root):
// Einstellungen lesen, auf Wunsch deployen, die Adapter erzeugen und mit dem Kern verdrahten, Worker starten.
// Nur hier werden Kern und Adapter erzeugt und verbunden, keine Seite des Sechsecks erzeugt die andere selbst.
// Die Schleife steht in Adapter/Engine/ExternalTaskWorker.cs, in loesung/worker steht sie noch hier.
//   dotnet run                    Worker-Schleife starten, beenden mit Strg+C
//   dotnet run -- deploy          prozess/genehmigungsworkflow.bpmn in die Engine einspielen
//   dotnet run -- deploy <pfad>   eine andere BPMN-Datei einspielen, etwa
//                                 prozess/varianten/verbuchen-fehlerpfad.bpmn
// Im Ordner loesung/worker-hexagonal/ jeweils mit --project src/GenehmigungWorker, etwa:
//   dotnet run --project src/GenehmigungWorker -- deploy
using GenehmigungWorker;
using GenehmigungWorker.Adapter.Engine;
using GenehmigungWorker.Adapter.Fachsystem;
using GenehmigungWorker.Domaene;
using GenehmigungWorker.Domaene.Ports;

// Umlaute auch in der Windows-Konsole richtig anzeigen
Console.OutputEncoding = System.Text.Encoding.UTF8;

Einstellungen einstellungen;
try
{
    einstellungen = Einstellungen.Laden();
}
catch (KonfigurationsFehler fehler)
{
    Console.Error.WriteLine(fehler.Message);
    return 1;
}

// Ein HttpClient für alles: BaseAddress ist EngineUrl (ohne /engine-rest), dazu Basic Auth
using var http = einstellungen.ErzeugeHttpClient();

if (args is ["deploy", ..])
{
    // Ohne weiteres Argument das Modell unter prozess/, sonst die Datei aus args[1]
    await Deploy.AusfuehrenAsync(http, einstellungen.ProzessKey, args.Length > 1 ? args[1] : null);
    return 0;
}

// Zusammenbau von außen nach innen und wieder hinaus:
// ausgehender Adapter -> Kern -> eingehender Adapter -> Schleife mit der Engine.
// Die Simulation merkt sich ihre Buchungen in einer Datei neben der DLL (bin/...).
// So bleibt die Buchung auch über einen Neustart des Workers idempotent.
// Löscht ihr die Datei, beginnt die Simulation wieder bei 0001.
var buchungen = Path.Combine(AppContext.BaseDirectory, "buchungen.json");
IBuchungssystem buchungssystem = new BuchungssystemSimulation(buchungen);
IGenehmigungVerbuchen useCase = new GenehmigungVerbuchen(buchungssystem);
var adapter = new GenehmigungVerbuchenAdapter(useCase);
var client = new ExternalTaskClient(http, einstellungen.WorkerId, einstellungen.Topic);
var worker = new ExternalTaskWorker(client, adapter);

// Strg+C beendet die Schleife sauber, siehe ExternalTaskWorker.LaufenAsync
using var abbruch = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    abbruch.Cancel();
};

Log($"Worker {einstellungen.WorkerId} holt Tasks vom Topic {einstellungen.Topic} " +
    $"bei {einstellungen.EngineUrl}. Beenden mit Strg+C.");
Log($"Buchungen der Simulation: {buchungen}");

await worker.LaufenAsync(abbruch.Token);

Log("Worker beendet.");
return 0;

static void Log(string text) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {text}");
