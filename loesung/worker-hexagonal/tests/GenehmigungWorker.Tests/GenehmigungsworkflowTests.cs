// Testseite, quer durch das Sechseck: Prozesstests mit der echten Engine als externem System.
// Dieselben Fälle wie in loesung/worker, nur verbucht hier der Engine-Adapter über den Use Case statt des Handlers.
// Das Fachsystem bleibt ein Fake: Der Test prüft Modell, Engine und die Übersetzung an der Grenze im Zusammenspiel.
namespace GenehmigungWorker.Tests;

/// <summary>
/// Prozesstests per REST gegen eure lokale Engine: Modell, Engine, Adapter und Use Case im Zusammenspiel.
/// Voraussetzung: Stack läuft, Modell mit dem Umbau aus Übung 8 ist bereitgestellt (dotnet run -- deploy),
/// euer Worker ist gestoppt.
/// Nur die Unit-Tests ohne Engine: dotnet test --filter "Kategorie!=Prozesstest"
/// </summary>
[Trait("Kategorie", "Prozesstest")]
public class GenehmigungsworkflowTests : IDisposable
{
    private readonly EngineHelfer _engine = new();

    [Fact]
    public async Task Genehmigter_Antrag_wird_verbucht()
    {
        var instanz = await _engine.StartAsync(_engine.ProzessKey,
            new() { ["betrag"] = 1200m, ["begruendung"] = "Dienstreise" });
        var aufgabe = await _engine.GetTaskAsync(instanz.Id);
        Assert.Equal("Task_Pruefen", aufgabe.TaskDefinitionKey);
        await _engine.CompleteTaskAsync(aufgabe.Id, new() { ["entscheidung"] = "genehmigt" });

        var task = await _engine.FetchAndLockAsync(_engine.Topic, instanz.BusinessKey);
        var adapter = new GenehmigungVerbuchenAdapter(new GenehmigungVerbuchen(new BuchungssystemFake()));
        var ergebnis = adapter.Handle(task);
        await _engine.CompleteAsync(task, ergebnis);

        Assert.Equal("COMPLETED", (await _engine.GetHistoryAsync(instanz.Id)).State);
        Assert.Equal("B-2026-0001", await _engine.GetVariableAsync(instanz.Id, "buchungsnummer"));
    }

    [Fact]
    public async Task Abgelehnter_Antrag_bekommt_keinen_External_Task()
    {
        // Gegenprobe: Mit abgelehnt nimmt die Instanz den anderen Weg am Gateway
        var instanz = await _engine.StartAsync(_engine.ProzessKey,
            new() { ["betrag"] = 1200m, ["begruendung"] = "Dienstreise" });
        var aufgabe = await _engine.GetTaskAsync(instanz.Id);
        Assert.Equal("Task_Pruefen", aufgabe.TaskDefinitionKey);
        await _engine.CompleteTaskAsync(aufgabe.Id, new() { ["entscheidung"] = "abgelehnt" });

        // "Ablehnung mitteilen" wartet als Aufgabe, einen External Task gibt es auf diesem Weg nie
        var mitteilen = await _engine.GetTaskAsync(instanz.Id);
        Assert.Equal("Task_Ablehnen", mitteilen.TaskDefinitionKey);
        Assert.Empty(await _engine.GetExternalTasksAsync(instanz.Id));
        Assert.Equal("ACTIVE", (await _engine.GetHistoryAsync(instanz.Id)).State);

        // Erst wenn auch diese Aufgabe abgeschlossen ist, endet die Instanz
        await _engine.CompleteTaskAsync(mitteilen.Id, new());
        Assert.Equal("COMPLETED", (await _engine.GetHistoryAsync(instanz.Id)).State);
        Assert.Null(await _engine.GetVariableAsync(instanz.Id, "buchungsnummer"));
    }

    public void Dispose() => _engine.Dispose();
}
