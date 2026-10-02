namespace GenehmigungWorker.Tests;

/// <summary>
/// Prozesstest zum Bonus fachlicher Fehler, gegen die Variante prozess/varianten/verbuchen-fehlerpfad.bpmn.
/// Die Variante spielt der Test selbst ein. Sonst gilt dasselbe wie für die anderen Prozesstests:
/// Stack läuft, Zugangsdaten gesetzt, euer Worker ist gestoppt.
/// </summary>
[Trait("Kategorie", "Prozesstest")]
public class FehlerpfadTests : IDisposable
{
    private const string Variante = "prozess/varianten/verbuchen-fehlerpfad.bpmn";
    private const string VarianteKey = "Process_VerbuchenFehlerpfad";

    private readonly EngineHelfer _engine = new();
    // Eigene Datei für die Simulation, damit der Test die Buchungen des Workers nicht berührt
    private readonly string _buchungen = Path.Combine(Path.GetTempPath(), $"buchungen-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task Abgelehnte_Buchung_fuehrt_zu_Buchung_klaeren()
    {
        using var http = Einstellungen.Laden().ErzeugeHttpClient();

        // Deployen: unverändert legt die Engine keine neue Version an
        await Deploy.AusfuehrenAsync(http, VarianteKey, Variante);

        // Starten: Die Variante hat weder Startformular noch Initiator, der Test gibt alles mit
        var instanz = await _engine.StartAsync(VarianteKey, new()
        {
            ["antragsteller"] = "anna", ["betrag"] = 60000m, ["begruendung"] = "Neue Serverhardware"
        });

        // Verbuchen: Über dem Budget lehnt die Simulation ab, der Handler reicht das durch
        var task = await _engine.FetchAndLockAsync(_engine.Topic, instanz.BusinessKey);
        var handler = new GenehmigungVerbuchenHandler(new BuchungssystemSimulation(_buchungen));
        var abgelehnt = Assert.Throws<BuchungAbgelehntException>(() => handler.Handle(task));

        // Melden wie die Schleife, unter der Worker-ID des Tests: Ihr gehört der Lock
        var client = new ExternalTaskClient(http, EngineHelfer.WorkerId, _engine.Topic);
        await client.BpmnErrorAsync(task, "BUCHUNG_ABGELEHNT", abgelehnt.Message);

        // Prüfen: Das Error-Boundary fängt den Fehler, "Buchung klären" wartet mit Code und Grund
        var aufgabe = await _engine.GetTaskAsync(instanz.Id);
        Assert.Equal("Task_BuchungKlaeren", aufgabe.TaskDefinitionKey);
        Assert.Equal("BUCHUNG_ABGELEHNT", await _engine.GetVariableAsync(instanz.Id, "errorCode"));
        Assert.Contains("Budget", (string?) await _engine.GetVariableAsync(instanz.Id, "errorMessage"));
        Assert.Null(await _engine.GetVariableAsync(instanz.Id, "buchungsnummer"));
    }

    public void Dispose()
    {
        _engine.Dispose();
        File.Delete(_buchungen);
    }
}
