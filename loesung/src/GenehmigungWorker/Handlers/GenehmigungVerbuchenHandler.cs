using GenehmigungWorker.Fachsystem;

namespace GenehmigungWorker.Handlers;

/// <summary>
/// Handler für das Topic genehmigung-verbuchen: lesen, verbuchen, Ergebnis zurückgeben.
/// Mit der Engine spricht der Handler nicht, complete und failure schickt die Schleife in Program.cs.
/// </summary>
public class GenehmigungVerbuchenHandler
{
    private readonly IBuchungssystem _buchung;
    public GenehmigungVerbuchenHandler(IBuchungssystem buchung)
        => _buchung = buchung;

    public Dictionary<string, object> Handle(ExternalTask task)
    {
        // Idempotenz-Schlüssel: Business Key, wenn die Instanz einen hat, sonst die Prozessinstanz-ID.
        // Über das Startformular gestartet ist BusinessKey meist null.
        var schluessel = task.BusinessKey ?? task.ProcessInstanceId;

        // Eingabe lesen. Fehlt eine Variable, scheitert der Handler laut mit KeyNotFoundException,
        // die Schleife meldet dann failure.
        var antragsteller = (string) task.Variables["antragsteller"];
        var betrag = Convert.ToDecimal(task.Variables["betrag"]);
        var begruendung = (string) task.Variables["begruendung"];

        // Arbeit tun: die eine Stelle nach außen
        var nummer = _buchung.Verbuchen(
            schluessel, antragsteller, betrag, begruendung);

        // Ergebnis zurückgeben: buchungsnummer ist Teil des Vertrags mit dem Modell
        return new() { ["buchungsnummer"] = nummer };
    }
}
