using System.Globalization;
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
        // betrag kommt als Text aus dem easyForm ("1234.5", immer mit Punkt)
        // oder als Zahl, etwa beim Start per REST. Beides kulturunabhängig lesen,
        // sonst wird auf einem deutschen Rechner aus "1234.5" 12345.
        var betrag = task.Variables["betrag"] switch
        {
            string text => decimal.Parse(text, CultureInfo.InvariantCulture),
            var zahl => Convert.ToDecimal(zahl, CultureInfo.InvariantCulture),
        };
        var begruendung = (string) task.Variables["begruendung"];

        // Arbeit tun: die eine Stelle nach außen
        var nummer = _buchung.Verbuchen(
            schluessel, antragsteller, betrag, begruendung);

        // Ergebnis zurückgeben: buchungsnummer ist Teil des Vertrags mit dem Modell
        return new() { ["buchungsnummer"] = nummer };
    }
}
