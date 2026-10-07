using System.Net.Http.Json;
using System.Text.Json;

namespace GenehmigungWorker.Adapter.Engine;

/// <summary>
/// Ein gesperrter External Task, so wie der Handler ihn bekommt.
/// Die Variablen sind schon ausgepackt: Text als string, ganze Zahlen als long,
/// andere Zahlen als double, Wahrheitswerte als bool.
/// Variablen ohne Wert (null) fehlen im Dictionary, der Handler scheitert dann laut
/// mit KeyNotFoundException, statt still mit null weiterzurechnen.
/// </summary>
public record ExternalTask(
    string Id,
    string TopicName,
    int? Retries,
    string? BusinessKey,
    string ProcessInstanceId,
    Dictionary<string, object> Variables);

/// <summary>Ein Eintrag aus der Antwort von fetchAndLock, so wie die Engine ihn als JSON schickt.</summary>
public record LockedTaskDto(
    string Id,
    string TopicName,
    int? Retries,
    string? BusinessKey,
    string ProcessInstanceId,
    Dictionary<string, VariableDto>? Variables);

/// <summary>Eine Prozessvariable im Format der Engine: { "value": 1200, "type": "Long" }</summary>
public record VariableDto(JsonElement Value, string? Type);

/// <summary>
/// Die vier REST-Calls des External-Task-Protokolls gegen /engine-rest:
/// fetchAndLock, complete, failure und bpmnError.
/// </summary>
/// <param name="http">HttpClient mit EngineUrl als BaseAddress (ohne /engine-rest) und Basic Auth</param>
/// <param name="workerId">aus appsettings.json, je Instanz eindeutig</param>
/// <param name="topic">aus appsettings.json, exakt wie im Modell</param>
public class ExternalTaskClient(HttpClient http, string workerId, string topic)
{
    /// <summary>Holt bis zu fünf Tasks vom Topic und sperrt sie für 30 Sekunden.</summary>
    public async Task<List<ExternalTask>> FetchAndLockAsync(CancellationToken stop)
    {
        // Arbeit holen und für lockDuration Millisekunden sperren
        var anfrage = new
        {
            workerId,                        // aus appsettings.json, je Instanz eindeutig
            maxTasks = 5,
            asyncResponseTimeout = 10_000,   // Long Polling: bis zu 10 s offen halten
            topics = new[] { new {
                topicName = topic,           // aus appsettings.json
                lockDuration = 30_000 } }    // 30 s gehört der Task euch
        };

        var antwort = await http.PostAsJsonAsync(
            "/engine-rest/external-task/fetchAndLock", anfrage, stop);
        antwort.EnsureSuccessStatusCode();
        // Variablen kommen als { value, type }: DTO lesen, ToTask packt sie aus
        var dtos = await antwort.Content.ReadFromJsonAsync<List<LockedTaskDto>>(stop) ?? [];
        return dtos.Select(ToTask).ToList();
    }

    /// <summary>Erfolg: Ergebnisvariablen zurückschreiben, der Token läuft weiter.</summary>
    public async Task CompleteAsync(ExternalTask task, Dictionary<string, object> variablen)
    {
        var antwort = await http.PostAsJsonAsync(
            $"/engine-rest/external-task/{task.Id}/complete",
            new { workerId, variables = Typisiert(variablen) });
        antwort.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Technischer Fehler: Die Engine liefert den Task nach retryTimeout erneut aus.
    /// Bei retries = 0 entsteht ein Incident.
    /// </summary>
    public async Task FailureAsync(ExternalTask task, string meldung, int retries, TimeSpan retryTimeout)
    {
        var antwort = await http.PostAsJsonAsync(
            $"/engine-rest/external-task/{task.Id}/failure",
            new
            {
                workerId,
                errorMessage = meldung,
                retries,
                retryTimeout = (long) retryTimeout.TotalMilliseconds
            });
        antwort.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Fachlicher Fehler: Die Engine wirft am Service Task den BPMN-Fehler mit errorCode.
    /// Ein Error-Boundary mit diesem Code fängt ihn, und der Token nimmt dessen Pfad.
    /// Kein Retry, kein Incident. Fängt ihn kein Boundary, endet die Instanz still am Service Task.
    /// </summary>
    public async Task BpmnErrorAsync(ExternalTask task, string errorCode, string meldung)
    {
        var antwort = await http.PostAsJsonAsync(
            $"/engine-rest/external-task/{task.Id}/bpmnError",
            new
            {
                workerId,
                errorCode,                // etwa BUCHUNG_ABGELEHNT, exakt wie im Modell
                errorMessage = meldung    // der Grund, das Boundary legt ihn in errorMessage ab
            });
        antwort.EnsureSuccessStatusCode();
    }

    /// <summary>Übersetzt die Antwort von fetchAndLock in den Record, den der Handler bekommt.</summary>
    public static ExternalTask ToTask(LockedTaskDto dto)
    {
        var variablen = new Dictionary<string, object>();
        foreach (var (name, variable) in dto.Variables ?? [])
        {
            // Variablen ohne Wert (null) lässt ToTask weg
            if (Auspacken(variable.Value) is { } wert) variablen[name] = wert;
        }
        return new ExternalTask(
            dto.Id, dto.TopicName, dto.Retries, dto.BusinessKey, dto.ProcessInstanceId, variablen);
    }

    /// <summary>Holt den Wert aus dem JsonElement: Text, Zahl, bool oder null.</summary>
    public static object? Auspacken(JsonElement wert) => wert.ValueKind switch
    {
        JsonValueKind.String => wert.GetString(),
        // ganze Zahlen als long (etwa betrag vom Typ Long), sonst double
        JsonValueKind.Number => wert.TryGetInt64(out var ganzeZahl) ? ganzeZahl : (object) wert.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        // Objekte und Listen (etwa Variablen vom Typ Json) bleiben JSON-Text
        _ => wert.GetRawText()
    };

    /// <summary>
    /// Verpackt Werte in typisierte Variablen der Engine: { "value": ..., "type": "String" }.
    /// Mit Typ muss die Engine nicht raten.
    /// </summary>
    public static Dictionary<string, object> Typisiert(Dictionary<string, object> werte) =>
        werte.ToDictionary(
            w => w.Key,
            w => (object) new { value = w.Value, type = EngineTyp(w.Value) });

    private static string EngineTyp(object? wert) => wert switch
    {
        null => "Null",
        string => "String",
        bool => "Boolean",
        short => "Short",
        int => "Integer",
        long => "Long",
        decimal or double or float => "Double",
        _ => throw new ArgumentException(
            $"Variablen vom Typ {wert.GetType().Name} unterstützt dieser Client nicht. " +
            "Möglich sind string, bool, short, int, long, decimal, double und float.")
    };
}
