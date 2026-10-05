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
    // Der Test-Helfer ist fertig: je Methode ein REST-Endpunkt, er wartet auf den Zustand. Siehe EngineHelfer.cs
    private readonly EngineHelfer _engine = new();

    // TODO Kapitel 12, Schritt 4: Test schreiben, Methode async machen, danach "(Skip = ...)" entfernen
    [Fact(Skip = "TODO Kapitel 12, Schritt 4: Prozesstest schreiben, dann Skip entfernen")]
    public Task Genehmigter_Antrag_wird_verbucht()
    {
        // Prozesstest in fünf Schritten (public async Task Genehmigter_Antrag_wird_verbucht()):
        //   Starten:     var instanz = await _engine.StartAsync(_engine.ProzessKey,
        //                    new() { ["betrag"] = 1200m, ["begruendung"] = "Dienstreise" });
        //   Warten:      var aufgabe = await _engine.GetTaskAsync(instanz.Id);
        //                Assert.Equal("Task_Pruefen", aufgabe.TaskDefinitionKey);
        //   Entscheiden: await _engine.CompleteTaskAsync(aufgabe.Id, new() { ["entscheidung"] = "genehmigt" });
        //   Verbuchen:   var task = await _engine.FetchAndLockAsync(_engine.Topic, instanz.BusinessKey);
        //                var ergebnis = new GenehmigungVerbuchenHandler(new BuchungssystemFake()).Handle(task);
        //                await _engine.CompleteAsync(task, ergebnis);
        //   Beenden:     Assert.Equal("COMPLETED", (await _engine.GetHistoryAsync(instanz.Id)).State);
        //                Assert.Equal("B-2026-0001", await _engine.GetVariableAsync(instanz.Id, "buchungsnummer"));
        Assert.Fail("TODO Kapitel 12: Prozesstest schreiben");
        return Task.CompletedTask;
    }

    // TODO Kapitel 12, Gegenprobe (wenn Zeit bleibt): Test schreiben, danach "(Skip = ...)" entfernen
    [Fact(Skip = "TODO Kapitel 12, Gegenprobe: Test schreiben, dann Skip entfernen")]
    public Task Abgelehnter_Antrag_bekommt_keinen_External_Task()
    {
        // Wie oben starten und bei Task_Pruefen warten, dann mit entscheidung "abgelehnt" abschließen.
        // Danach wartet die Aufgabe "Ablehnung mitteilen", einen External Task hat die Instanz nicht:
        //   var mitteilen = await _engine.GetTaskAsync(instanz.Id);
        //   Assert.Equal("Task_Ablehnen", mitteilen.TaskDefinitionKey);
        //   Assert.Empty(await _engine.GetExternalTasksAsync(instanz.Id));
        // Erst wenn auch diese Aufgabe abgeschlossen ist, ist die Instanz beendet:
        //   await _engine.CompleteTaskAsync(mitteilen.Id, new());
        //   Assert.Equal("COMPLETED", (await _engine.GetHistoryAsync(instanz.Id)).State);
        Assert.Fail("TODO Kapitel 12: Gegenprobe schreiben");
        return Task.CompletedTask;
    }

    public void Dispose() => _engine.Dispose();
}
