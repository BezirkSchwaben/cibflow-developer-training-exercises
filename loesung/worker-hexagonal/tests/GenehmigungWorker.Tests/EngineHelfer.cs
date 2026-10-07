using System.Net;
using System.Net.Http.Json;

namespace GenehmigungWorker.Tests;

/// <summary>
/// Test-Helfer für den Prozesstest: je Methode ein Endpunkt der REST-API eurer lokalen Engine.
/// Fertig vorgegeben, ihr müsst hier nichts ändern.
///
/// Liest EngineUrl, ProzessKey, Topic und die Zugangsdaten wie der Worker
/// (appsettings.json, User Secrets, Umgebungsvariablen).
/// Die lesenden Methoden warten auf den Zustand, den der Test prüft, statt nur einmal zu fragen.
/// Instanzen, die ein Test offen zurücklässt (etwa nach einem roten Lauf), löscht Dispose.
/// Antwortet die Engine nicht, lehnt sie einen Call ab oder kommt ein Zustand nicht,
/// sagt die Fehlermeldung des Tests, was er vorgefunden hat und was ihr tun könnt.
/// </summary>
public sealed class EngineHelfer : IDisposable
{
    /// <summary>Eigene Worker-ID des Tests, damit ihr im Cockpit seht, wer den Lock hält</summary>
    public const string WorkerId = "prozesstest";

    // Warum warten: An einem Speicherpunkt (Asynchronous continuations, etwa "After" am Start-Event)
    // antwortet die Engine schon, bevor der Rest gelaufen ist. Den Rest führt der Job Executor der Engine
    // kurz danach im Hintergrund aus. Wer sofort fragt, sieht einen Zwischenstand: Der Test wird rot oder flackert.
    private static readonly TimeSpan Wartezeit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(200);

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
    /// Die Engine antwortet am ersten Wartezustand oder Speicherpunkt.
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
    /// Die eine offene Aufgabe der Instanz. Ist noch keine da, fragt der Helfer bis zu 10 Sekunden
    /// lang nach. Gibt es dann keine oder gibt es mehrere, scheitert der Test.
    /// GET /engine-rest/task?processInstanceId={instanzId}
    /// </summary>
    public async Task<Aufgabe> GetTaskAsync(string instanzId)
    {
        var aufgaben = await WarteAufAsync(
            () => AbfragenAsync<List<Aufgabe>>($"/engine-rest/task?processInstanceId={instanzId}"),
            gefunden => gefunden.Count > 0);
        if (aufgaben.Count == 1) return aufgaben[0];

        throw new InvalidOperationException(aufgaben.Count == 0
            ? $"Erwartet genau eine offene Aufgabe in Instanz {instanzId}, gefunden: 0, auch nach " +
              $"{Wartezeit.TotalSeconds:0} Sekunden Warten. {await ZustandAsync(instanzId)}"
            : $"Erwartet genau eine offene Aufgabe in Instanz {instanzId}, gefunden: {aufgaben.Count} " +
              $"({string.Join(", ", aufgaben.Select(a => $"„{a.Name}“ {a.TaskDefinitionKey}"))}).");
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
    /// dann für 30 Sekunden. Danach bekommt ihn der Test. Ein Speicherpunkt vor dem Service Task ist so mit abgedeckt.
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

        var instanzen = await AbfragenAsync<List<HistorischeInstanz>>(
            $"/engine-rest/history/process-instance?processInstanceBusinessKey={Uri.EscapeDataString(businessKey)}");
        var zustand = instanzen.Count == 1
            ? await ZustandAsync(instanzen[0].Id)
            : $"Instanzen mit diesem Business Key: {instanzen.Count}.";
        throw new InvalidOperationException(
            $"Kein External Task auf Topic {topic} für Business Key {businessKey}, auch nicht nach 45 Sekunden. " +
            $"{zustand} Läuft euer Worker noch? Dann holt er den Task vor dem Test weg. Stoppt ihn für den Testlauf. " +
            "Oder liegt „Genehmigung verbuchen“ in der Aufgabenliste? Dann ist der Umbau aus Übung 8 nicht deployt.");
    }

    /// <summary>
    /// Meldet den External Task als erledigt, gleiche Signatur wie im ExternalTaskClient.
    /// POST /engine-rest/external-task/{id}/complete
    /// </summary>
    public Task CompleteAsync(ExternalTask task, Dictionary<string, object> variablen)
        => _client.CompleteAsync(task, variablen);

    /// <summary>
    /// Die External Tasks, die in der Instanz gerade warten, ohne etwas zu sperren.
    /// Für die Gegenprobe: Nach abgelehnt muss die Liste leer sein. Damit leer nicht nur
    /// "noch nicht da" heißt, wartet der Helfer vorher, bis kein Speicherpunkt mehr aussteht.
    /// GET /engine-rest/external-task?processInstanceId={instanzId}
    /// </summary>
    public async Task<List<WartenderExternalTask>> GetExternalTasksAsync(string instanzId)
    {
        await WarteBisSpeicherpunkteErledigtAsync(instanzId);
        return await AbfragenAsync<List<WartenderExternalTask>>($"/engine-rest/external-task?processInstanceId={instanzId}");
    }

    /// <summary>
    /// Die Instanz aus der History, auch nach ihrem Ende. State ist etwa ACTIVE oder COMPLETED.
    /// Steht an der Instanz noch ein Speicherpunkt aus, wartet der Helfer vorher bis zu 10 Sekunden darauf.
    /// GET /engine-rest/history/process-instance/{instanzId}
    /// </summary>
    public async Task<HistorischeInstanz> GetHistoryAsync(string instanzId)
    {
        await WarteBisSpeicherpunkteErledigtAsync(instanzId);
        return await AbfragenAsync<HistorischeInstanz>($"/engine-rest/history/process-instance/{instanzId}");
    }

    /// <summary>
    /// Wert einer Variablen aus der History, ausgepackt wie im ExternalTask (etwa string oder long).
    /// Gibt es die Variable nicht, ist das Ergebnis null. Wartet vorher wie GetHistoryAsync.
    /// GET /engine-rest/history/variable-instance?processInstanceId=...&amp;variableName=...
    /// </summary>
    public async Task<object?> GetVariableAsync(string instanzId, string name)
    {
        await WarteBisSpeicherpunkteErledigtAsync(instanzId);
        var variablen = await AbfragenAsync<List<VariableDto>>(
            $"/engine-rest/history/variable-instance?processInstanceId={instanzId}&variableName={Uri.EscapeDataString(name)}");
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

    // Fragt ab, bis erreicht(ergebnis) gilt oder die Wartezeit um ist, und liefert das letzte Ergebnis.
    // Die kurze Pause schont die Engine. Ein festes Thread.Sleep im Test braucht es so nicht.
    private static async Task<T> WarteAufAsync<T>(Func<Task<T>> abfrage, Func<T, bool> erreicht)
    {
        var ende = DateTime.UtcNow + Wartezeit;
        while (true)
        {
            var ergebnis = await abfrage();
            if (erreicht(ergebnis) || DateTime.UtcNow >= ende) return ergebnis;
            await Task.Delay(Pause);
        }
    }

    // Wartet, bis an der Instanz kein Speicherpunkt mehr ansteht: kein Job einer Asynchronous
    // continuation, den der Job Executor jetzt ausführen kann. Timer und gescheiterte Jobs ohne
    // Versuche zählen nicht, auf die wartet der Test nicht.
    private async Task WarteBisSpeicherpunkteErledigtAsync(string instanzId)
    {
        var offen = await WarteAufAsync(
            async () => (await AbfragenAsync<Anzahl>(
                $"/engine-rest/job/count?processInstanceId={instanzId}&messages=true&executable=true")).Count,
            anzahl => anzahl == 0);
        if (offen == 0) return;

        throw new InvalidOperationException(
            $"Erwartet, dass die Engine die Speicherpunkte in Instanz {instanzId} abarbeitet, gefunden: " +
            $"{(offen == 1 ? "ein offener Job" : $"{offen} offene Jobs")}, auch nach {Wartezeit.TotalSeconds:0} Sekunden Warten. " +
            await ZustandAsync(instanzId));
    }

    // Beschreibt für eine Fehlermeldung, was der Helfer in der Engine vorgefunden hat
    private async Task<string> ZustandAsync(string instanzId)
    {
        var instanz = await AbfragenAsync<HistorischeInstanz>($"/engine-rest/history/process-instance/{instanzId}");
        if (instanz.State != "ACTIVE") return $"Die Instanz ist schon beendet, Zustand {instanz.State}.";

        var aktivitaeten = await AbfragenAsync<List<Aktivitaet>>(
            $"/engine-rest/history/activity-instance?processInstanceId={instanzId}&unfinished=true");
        var text = aktivitaeten.Count == 0
            ? "Die Instanz läuft und steht zwischen zwei Elementen."
            : $"Die Instanz steht bei {string.Join(", ", aktivitaeten.Select(a => $"„{a.ActivityName ?? a.ActivityId}“ {a.ActivityId}"))}.";

        var gesperrt = await AbfragenAsync<List<WartenderExternalTask>>(
            $"/engine-rest/external-task?processInstanceId={instanzId}&locked=true");
        foreach (var task in gesperrt)
        {
            text += $" Den External Task an {task.ActivityId} hat gerade Worker {task.WorkerId} gesperrt.";
        }

        var jobs = await AbfragenAsync<List<Job>>($"/engine-rest/job?processInstanceId={instanzId}&messages=true");
        foreach (var job in jobs)
        {
            // Die Job-Definition sagt, an welchem Element der Speicherpunkt sitzt: async-before oder async-after
            var definition = await AbfragenAsync<JobDefinition>($"/engine-rest/job-definition/{job.JobDefinitionId}");
            var ort = $"{(definition.JobConfiguration == "async-before" ? "vor" : "nach")} {definition.ActivityId}";
            var bei = job.FailedActivityId is { } f && f != definition.ActivityId ? $" bei {f}" : "";
            text += job switch
            {
                { Retries: 0 } =>
                    $" Am Speicherpunkt {ort} ist ein Job gescheitert{bei}, ohne Versuche übrig. Im Cockpit steht ein Vorfall: {job.ExceptionMessage}",
                { ExceptionMessage: not null } =>
                    $" Am Speicherpunkt {ort} ist ein Job gescheitert{bei}, die Engine versucht es noch {job.Retries}-mal: {job.ExceptionMessage}",
                _ => $" Am Speicherpunkt {ort} wartet ein Job auf den Job Executor der Engine."
            };
        }
        return text;
    }

    // GET auf die REST-API, mit denselben verständlichen Fehlermeldungen wie RufeAsync
    private async Task<T> AbfragenAsync<T>(string pfad)
        => await LeseAsync<T>(await RufeAsync(() => _http.GetAsync(pfad)));

    // Schickt den Call und prüft die Antwort wie EnsureSuccessStatusCode, aber mit verständlicher
    // Meldung: Engine nicht erreichbar, falsche Zugangsdaten, Modell nicht bereitgestellt
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
                "docker compose logs init mit \"[init] Fertig.\" endet. Nur die Unit-Tests ohne Engine: " +
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
                " Ist das Modell bereitgestellt (Projekt-ZIP importiert oder im Ordner worker/ dotnet run --project src/GenehmigungWorker -- deploy) " +
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

    // Nur für die Fehlermeldungen und das Warten, deshalb nicht öffentlich
    private sealed record Anzahl(long Count);
    private sealed record Aktivitaet(string ActivityId, string? ActivityName);
    private sealed record Job(string JobDefinitionId, int Retries, string? ExceptionMessage, string? FailedActivityId);
    private sealed record JobDefinition(string ActivityId, string? JobConfiguration);
}

/// <summary>Eine gestartete Prozessinstanz</summary>
public record Instanz(string Id, string BusinessKey);

/// <summary>Eine offene Benutzeraufgabe, TaskDefinitionKey ist die ID im Modell (etwa Task_Pruefen)</summary>
public record Aufgabe(string Id, string TaskDefinitionKey, string Name);

/// <summary>Ein External Task, der in einer Instanz wartet. ActivityId ist die ID im Modell (etwa Task_Verbuchen)</summary>
public record WartenderExternalTask(string Id, string TopicName, string ActivityId, string? WorkerId);

/// <summary>Eine Prozessinstanz aus der History</summary>
public record HistorischeInstanz(string Id, string? BusinessKey, string State);
