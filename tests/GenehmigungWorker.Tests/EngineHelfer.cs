using System.Net;
using System.Net.Http.Json;

namespace GenehmigungWorker.Tests;

/// <summary>
/// Test-Helfer für den Prozesstest: je Methode ein REST-Call gegen eure lokale Engine.
/// Fertig vorgegeben, ihr müsst hier nichts ändern.
///
/// Liest EngineUrl, ProzessKey, Topic und die Zugangsdaten wie der Worker
/// (appsettings.json, User Secrets, Umgebungsvariablen).
/// Instanzen, die ein Test offen zurücklässt (etwa nach einem roten Lauf), löscht Dispose.
/// Antwortet die Engine nicht oder lehnt sie einen Call ab, sagt die Fehlermeldung des Tests,
/// woran es liegt und was ihr tun könnt.
/// </summary>
public sealed class EngineHelfer : IDisposable
{
    /// <summary>Eigene Worker-ID des Tests, damit ihr im Cockpit seht, wer den Lock hält</summary>
    public const string WorkerId = "prozesstest";

    private readonly HttpClient _http;
    private readonly ExternalTaskClient _client;
    private readonly List<string> _gestartet = [];

    public EngineHelfer()
    {
        var einstellungen = Einstellungen.Laden();
        ProzessKey = einstellungen.ProzessKey;
        Topic = einstellungen.Topic;
        _http = einstellungen.ErzeugeHttpClient();
        _client = new ExternalTaskClient(_http, WorkerId, Topic);
    }

    /// <summary>ProzessKey aus appsettings.json, die Process ID eures Modells</summary>
    public string ProzessKey { get; }

    /// <summary>Topic aus appsettings.json</summary>
    public string Topic { get; }

    /// <summary>
    /// Startet eine Instanz wie das Startformular, mit eigenem Business Key je Lauf.
    /// POST /engine-rest/process-definition/key/{prozessKey}/start
    /// </summary>
    public async Task<Instanz> StartAsync(string prozessKey, Dictionary<string, object> variablen)
    {
        var businessKey = $"prozesstest-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var antwort = await RufeAsync(() => _http.PostAsJsonAsync(
            $"/engine-rest/process-definition/key/{prozessKey}/start",
            new { businessKey, variables = ExternalTaskClient.Typisiert(variablen) }));
        var instanz = await LeseAsync<Instanz>(antwort);
        _gestartet.Add(instanz.Id);
        return instanz;
    }

    /// <summary>
    /// Die eine offene Aufgabe der Instanz. Gibt es keine oder mehrere, scheitert der Test.
    /// GET /engine-rest/task?processInstanceId={instanzId}
    /// </summary>
    public async Task<Aufgabe> GetTaskAsync(string instanzId)
    {
        var antwort = await RufeAsync(() => _http.GetAsync($"/engine-rest/task?processInstanceId={instanzId}"));
        var aufgaben = await LeseAsync<List<Aufgabe>>(antwort);
        return aufgaben.Count == 1
            ? aufgaben[0]
            : throw new InvalidOperationException(
                $"Erwartet genau eine offene Aufgabe in Instanz {instanzId}, gefunden: {aufgaben.Count}.");
    }

    /// <summary>
    /// Schließt eine Aufgabe ab, etwa "Antrag prüfen" mit entscheidung.
    /// POST /engine-rest/task/{aufgabeId}/complete
    /// </summary>
    public async Task CompleteTaskAsync(string aufgabeId, Dictionary<string, object> variablen)
    {
        await RufeAsync(() => _http.PostAsJsonAsync(
            $"/engine-rest/task/{aufgabeId}/complete",
            new { variables = ExternalTaskClient.Typisiert(variablen) }));
    }

    /// <summary>
    /// Holt den External Task der eigenen Instanz: fetchAndLock, im Topic-Eintrag nach
    /// businessKey gefiltert. Wartende Anträge anderer Instanzen bleiben unberührt.
    /// Kommt kein Task, fragt der Helfer bis zu 45 Sekunden lang erneut. Das braucht ihr, wenn ihr
    /// euren Worker gerade erst mit Strg+C gestoppt habt: Seine letzte Long-Polling-Anfrage bleibt
    /// in der Engine noch bis zu zehn Sekunden offen, kann den Task des Tests holen und sperrt ihn
    /// dann für 30 Sekunden. Danach bekommt ihn der Test.
    /// POST /engine-rest/external-task/fetchAndLock
    /// </summary>
    public async Task<ExternalTask> FetchAndLockAsync(string topic, string businessKey)
    {
        var anfrage = new
        {
            workerId = WorkerId,
            maxTasks = 1,
            asyncResponseTimeout = 5_000,
            topics = new[] { new { topicName = topic, lockDuration = 30_000, businessKey } }
        };
        var ende = DateTime.UtcNow.AddSeconds(45);
        do
        {
            // Long Polling: Liegt nichts bereit, antwortet die Engine erst nach 5 s mit []
            var antwort = await RufeAsync(() => _http.PostAsJsonAsync("/engine-rest/external-task/fetchAndLock", anfrage));
            var dtos = await LeseAsync<List<LockedTaskDto>>(antwort);
            if (dtos.Count == 1) return ExternalTaskClient.ToTask(dtos[0]);
        }
        while (DateTime.UtcNow < ende);

        throw new InvalidOperationException(
            $"Kein External Task auf Topic {topic} für Business Key {businessKey}, auch nicht nach 45 Sekunden. " +
            "Steht die Instanz am Service Task? Läuft euer Worker noch? Dann holt er den Task " +
            "vor dem Test weg. Stoppt ihn für den Testlauf.");
    }

    /// <summary>
    /// Meldet den External Task als erledigt, gleiche Signatur wie im ExternalTaskClient.
    /// POST /engine-rest/external-task/{id}/complete
    /// </summary>
    public Task CompleteAsync(ExternalTask task, Dictionary<string, object> variablen)
        => _client.CompleteAsync(task, variablen);

    /// <summary>
    /// Die External Tasks, die in der Instanz gerade warten, ohne etwas zu sperren.
    /// Für die Gegenprobe: Nach abgelehnt muss die Liste leer sein.
    /// GET /engine-rest/external-task?processInstanceId={instanzId}
    /// </summary>
    public async Task<List<WartenderExternalTask>> GetExternalTasksAsync(string instanzId)
    {
        var antwort = await RufeAsync(() => _http.GetAsync($"/engine-rest/external-task?processInstanceId={instanzId}"));
        return await LeseAsync<List<WartenderExternalTask>>(antwort);
    }

    /// <summary>
    /// Die Instanz aus der History, auch nach ihrem Ende. State ist etwa ACTIVE oder COMPLETED.
    /// GET /engine-rest/history/process-instance/{instanzId}
    /// </summary>
    public async Task<HistorischeInstanz> GetHistoryAsync(string instanzId)
    {
        var antwort = await RufeAsync(() => _http.GetAsync($"/engine-rest/history/process-instance/{instanzId}"));
        return await LeseAsync<HistorischeInstanz>(antwort);
    }

    /// <summary>
    /// Wert einer Variablen aus der History, ausgepackt wie im ExternalTask (etwa string oder long).
    /// Gibt es die Variable nicht, ist das Ergebnis null.
    /// GET /engine-rest/history/variable-instance?processInstanceId=...&amp;variableName=...
    /// </summary>
    public async Task<object?> GetVariableAsync(string instanzId, string name)
    {
        var antwort = await RufeAsync(() => _http.GetAsync(
            $"/engine-rest/history/variable-instance?processInstanceId={instanzId}&variableName={Uri.EscapeDataString(name)}"));
        var variablen = await LeseAsync<List<VariableDto>>(antwort);
        return variablen.Count == 0 ? null : ExternalTaskClient.Auspacken(variablen[0].Value);
    }

    /// <summary>
    /// Löscht die Instanzen dieses Tests, die noch laufen. Beendete Instanzen gibt es
    /// nur noch in der History, dort antwortet DELETE mit 404, das ist in Ordnung.
    /// </summary>
    public void Dispose()
    {
        foreach (var id in _gestartet)
        {
            using var anfrage = new HttpRequestMessage(
                HttpMethod.Delete, $"/engine-rest/process-instance/{id}?skipCustomListeners=true");
            try
            {
                using var _ = _http.Send(anfrage);
            }
            catch (HttpRequestException)
            {
                // Engine nicht erreichbar: Dann gibt es auch nichts aufzuräumen
            }
        }
        _http.Dispose();
    }

    // Schickt den Call und prüft die Antwort wie EnsureSuccessStatusCode, aber mit verständlicher
    // Meldung: Engine nicht erreichbar, falsche Zugangsdaten, Modell nicht deployt
    private async Task<HttpResponseMessage> RufeAsync(Func<Task<HttpResponseMessage>> call)
    {
        HttpResponseMessage antwort;
        try
        {
            antwort = await call();
        }
        catch (HttpRequestException fehler) when (fehler.StatusCode is null)
        {
            throw new HttpRequestException(
                $"Die Engine unter {_http.BaseAddress} antwortet nicht ({fehler.Message}). " +
                "Läuft der Stack? Im Ordner stack/: docker compose up -d, dann warten, bis " +
                "docker compose ps bei cibseven healthy zeigt. Nur die Unit-Tests ohne Engine: " +
                "dotnet test --filter \"Kategorie!=Prozesstest\"",
                fehler);
        }

        if (antwort.IsSuccessStatusCode) return antwort;

        var text = await antwort.Content.ReadAsStringAsync();
        var hinweis = antwort.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                " Stimmen EngineBenutzer und EnginePasswort (Umgebungsvariablen oder User Secrets)?",
            HttpStatusCode.NotFound when antwort.RequestMessage?.RequestUri?.AbsolutePath.Contains("/process-definition/key/") == true =>
                " Ist das Modell deployt (dotnet run --project src/GenehmigungWorker -- deploy) " +
                "und steht dessen Process ID als ProzessKey in appsettings.json?",
            _ => ""
        };
        throw new HttpRequestException(
            $"{antwort.RequestMessage?.Method} {antwort.RequestMessage?.RequestUri} lieferte " +
            $"{(int) antwort.StatusCode} {antwort.StatusCode}" +
            (string.IsNullOrWhiteSpace(text) ? "." : $": {text}") + hinweis,
            null,
            antwort.StatusCode);
    }

    private static async Task<T> LeseAsync<T>(HttpResponseMessage antwort)
        => await antwort.Content.ReadFromJsonAsync<T>()
           ?? throw new InvalidOperationException($"Leere Antwort von {antwort.RequestMessage?.RequestUri}");
}

/// <summary>Eine gestartete Prozessinstanz</summary>
public record Instanz(string Id, string BusinessKey);

/// <summary>Eine offene Benutzeraufgabe, TaskDefinitionKey ist die ID im Modell (etwa Task_Pruefen)</summary>
public record Aufgabe(string Id, string TaskDefinitionKey, string Name);

/// <summary>Ein External Task, der in einer Instanz wartet. ActivityId ist die ID im Modell (etwa Task_Verbuchen)</summary>
public record WartenderExternalTask(string Id, string TopicName, string ActivityId, string? WorkerId);

/// <summary>Eine Prozessinstanz aus der History</summary>
public record HistorischeInstanz(string Id, string? BusinessKey, string State);
