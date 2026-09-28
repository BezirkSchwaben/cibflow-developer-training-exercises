// Einstiegspunkt des Workers.
//   dotnet run              Worker-Schleife starten, beenden mit Strg+C
//   dotnet run -- deploy    prozess/genehmigungsworkflow.bpmn in die Engine einspielen
// Im Repo-Root jeweils mit --project src/GenehmigungWorker, etwa:
//   dotnet run --project src/GenehmigungWorker -- deploy
using GenehmigungWorker;
using GenehmigungWorker.Fachsystem;
using GenehmigungWorker.Handlers;

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
    await Deploy.AusfuehrenAsync(http, einstellungen.ProzessKey);
    return 0;
}

var client = new ExternalTaskClient(http, einstellungen.WorkerId, einstellungen.Topic);

// Die Simulation merkt sich ihre Buchungen in einer Datei neben der DLL (bin/...).
// So bleibt die Buchung auch über einen Neustart des Workers idempotent.
// Löscht ihr die Datei, beginnt die Simulation wieder bei 0001.
var buchungen = Path.Combine(AppContext.BaseDirectory, "buchungen.json");
var handler = new GenehmigungVerbuchenHandler(new BuchungssystemSimulation(buchungen));

// Strg+C beendet die Schleife sauber: Die Schleife prüft stop vor jedem Fetch,
// ein gerade wartendes fetchAndLock bricht mit OperationCanceledException ab.
using var abbruch = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    abbruch.Cancel();
};
var stop = abbruch.Token;

Log($"Worker {einstellungen.WorkerId} holt Tasks vom Topic {einstellungen.Topic} " +
    $"bei {einstellungen.EngineUrl}. Beenden mit Strg+C.");
Log($"Buchungen der Simulation: {buchungen}");

try
{
    while (!stop.IsCancellationRequested)
    {
        foreach (var task in await client.FetchAndLockAsync(stop))
        {
            Log($"Task {task.Id} geholt: Business Key {task.BusinessKey ?? "(keiner)"}, " +
                $"Prozessinstanz {task.ProcessInstanceId}, Retries {task.Retries?.ToString() ?? "(noch keine)"}");
            try
            {
                var ergebnis = handler.Handle(task);
                await client.CompleteAsync(task, ergebnis);
                Log($"Task {task.Id} erledigt: {string.Join(", ", ergebnis.Select(e => $"{e.Key} = {e.Value}"))}");
            }
            catch (Exception ex)
            {
                // Technischer Fehler: beim ersten Mal 3 Versuche, danach herunterzählen.
                // Bei 0 legt die Engine einen Incident an, zu sehen im Cockpit.
                var verbleibend = task.Retries is int r ? r - 1 : 3;
                await client.FailureAsync(task, ex.Message, verbleibend, TimeSpan.FromMinutes(5));
                Log($"Task {task.Id} fehlgeschlagen: {ex.Message} " +
                    (verbleibend > 0
                        ? $"Noch {verbleibend} Versuche, der nächste in fünf Minuten."
                        : "Keine Versuche mehr, die Engine legt einen Incident an."));
            }
        }
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    // Strg+C während eines wartenden fetchAndLock: gewollt, kein Fehler
}

Log("Worker beendet.");
return 0;

static void Log(string text) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {text}");
