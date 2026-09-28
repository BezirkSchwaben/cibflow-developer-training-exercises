// Einstiegspunkt des Workers.
//   dotnet run              Worker-Schleife starten, beenden mit Strg+C
//   dotnet run -- deploy    prozess/genehmigungsworkflow.bpmn in die Engine einspielen
// Im Repo-Root jeweils mit --project src/GenehmigungWorker, etwa:
//   dotnet run --project src/GenehmigungWorker -- deploy
using GenehmigungWorker;

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

// TODO Kapitel 12, Schritt 3: Handler anlegen, etwa
//   var handler = new GenehmigungVerbuchenHandler(new BuchungssystemSimulation());
//   (dazu oben: using GenehmigungWorker.Fachsystem; using GenehmigungWorker.Handlers;)

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

try
{
    while (!stop.IsCancellationRequested)
    {
        foreach (var task in await client.FetchAndLockAsync(stop))
        {
            // Kapitel 11: Task holen und loggen, noch kein complete.
            // Der Lock läuft deshalb nach 30 s ab, danach holt der Worker denselben Task erneut.
            Log($"Task {task.Id} geholt: Business Key {task.BusinessKey ?? "(keiner)"}, " +
                $"Prozessinstanz {task.ProcessInstanceId}, Retries {task.Retries?.ToString() ?? "(noch keine)"}");
            foreach (var (name, wert) in task.Variables)
            {
                Log($"  {name} = {wert}");
            }

            // TODO Kapitel 12, Schritt 3: Worker-Schleife anschließen
            //   try
            //   {
            //       var ergebnis = handler.Handle(task);
            //       await client.CompleteAsync(task, ergebnis);
            //   }
            //   catch (Exception ex)
            //   {
            //       var verbleibend = task.Retries is int r ? r - 1 : 3;
            //       await client.FailureAsync(task, ex.Message, verbleibend, TimeSpan.FromMinutes(5));
            //   }
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
