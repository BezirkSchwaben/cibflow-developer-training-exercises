// Außen am Sechseck, eingehende Seite: die Worker-Schleife, das Protokoll mit der Engine.
// Sie holt Tasks per ExternalTaskClient, gibt jeden an den GenehmigungVerbuchenAdapter und meldet das
// Ergebnis an die Engine zurück: complete, failure mit Retries oder bpmnError. Retries, Incidents und
// errorCodes sind Begriffe der Engine und des Modells, deshalb stehen sie hier und nicht in der Domäne.
// In loesung/worker steht dieselbe Schleife in Program.cs, mit denselben Logzeilen.
using GenehmigungWorker.Domaene;

namespace GenehmigungWorker.Adapter.Engine;

/// <summary>
/// Die Schleife des Workers: fetchAndLock, Adapter rufen, complete, failure oder bpmnError.
/// </summary>
/// <param name="client">die REST-Calls des External-Task-Protokolls</param>
/// <param name="adapter">übersetzt jeden Task in einen Aufruf des Use Case</param>
public class ExternalTaskWorker(ExternalTaskClient client, GenehmigungVerbuchenAdapter adapter)
{
    /// <summary>
    /// Holt und bearbeitet Tasks, bis stop ausgelöst wird (Strg+C).
    /// Die Schleife prüft stop vor jedem Fetch, ein gerade wartendes fetchAndLock bricht mit
    /// OperationCanceledException ab.
    /// </summary>
    public async Task LaufenAsync(CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                foreach (var task in await client.FetchAndLockAsync(stop))
                {
                    await BearbeitenAsync(task);
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Strg+C während eines wartenden fetchAndLock: gewollt, kein Fehler
        }
    }

    private async Task BearbeitenAsync(ExternalTask task)
    {
        Log($"Task {task.Id} geholt: Business Key {task.BusinessKey ?? "(keiner)"}, " +
            $"Prozessinstanz {task.ProcessInstanceId}, Retries {task.Retries?.ToString() ?? "(noch keine)"}");
        try
        {
            var ergebnis = adapter.Handle(task);
            await client.CompleteAsync(task, ergebnis);
            Log($"Task {task.Id} erledigt: {string.Join(", ", ergebnis.Select(e => $"{e.Key} = {e.Value}"))}");
        }
        catch (BuchungAbgelehntException abgelehnt)
        {
            // Fachlicher Fehler, kein Bug: Ein Retry hilft nicht, deshalb bpmnError statt failure.
            // Die Domäne sagt nur "abgelehnt", erst hier wird daraus der errorCode des Modells.
            // Dieser catch steht vor catch (Exception), sonst fängt der allgemeine auch die Ablehnung.
            // In der Variante prozess/varianten/verbuchen-fehlerpfad.bpmn fängt das Error-Boundary
            // BUCHUNG_ABGELEHNT und führt zu "Buchung klären".
            // Ohne passendes Error-Boundary, etwa in eurem Genehmigungsworkflow, beendet die Engine die Instanz still
            // am Service Task: abgeschlossen, aber ohne "Antrag genehmigt", ohne Incident, und der Grund
            // steht nur im Log der Engine. bpmnError also nur, wenn das Modell den Code auch fängt.
            await client.BpmnErrorAsync(task, "BUCHUNG_ABGELEHNT", abgelehnt.Message);
            Log($"Task {task.Id} fachlich abgelehnt: {abgelehnt.Message}. bpmnError BUCHUNG_ABGELEHNT gemeldet.");
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

    private static void Log(string text) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {text}");
}
