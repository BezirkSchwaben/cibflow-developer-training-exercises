namespace GenehmigungWorker.Tests;

/// <summary>
/// Prozesstests per REST gegen eure lokale Engine: Modell, Engine und Handler im Zusammenspiel.
/// Voraussetzung: Stack läuft, Modell ist bereitgestellt (Projekt-ZIP importiert oder dotnet run -- deploy),
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
        var ergebnis = new GenehmigungVerbuchenHandler(new BuchungssystemFake()).Handle(task);
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

        Assert.Empty(await _engine.GetExternalTasksAsync(instanz.Id));
        Assert.Equal("COMPLETED", (await _engine.GetHistoryAsync(instanz.Id)).State);
        Assert.Null(await _engine.GetVariableAsync(instanz.Id, "buchungsnummer"));
    }

    public void Dispose() => _engine.Dispose();
}
