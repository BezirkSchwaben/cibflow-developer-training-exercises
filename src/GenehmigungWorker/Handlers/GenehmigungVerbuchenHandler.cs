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
        // TODO Kapitel 12, Schritt 1: Handler schreiben
        //   1. Schlüssel für die Idempotenz: task.BusinessKey, wenn es keinen gibt task.ProcessInstanceId
        //   2. Variablen aus task.Variables lesen: antragsteller und begruendung (string), dazu betrag.
        //        betrag kommt aus dem easyForm als Text ("1234.5", immer mit Punkt), per REST als Zahl.
        //        Text: decimal.Parse(text, CultureInfo.InvariantCulture)
        //        Zahl: Convert.ToDecimal(zahl, CultureInfo.InvariantCulture)
        //   3. _buchung.Verbuchen(schluessel, antragsteller, betrag, begruendung) rufen
        //   4. Ergebnis zurückgeben: new() { ["buchungsnummer"] = nummer }
        throw new NotImplementedException(
            "TODO Kapitel 12: GenehmigungVerbuchenHandler.Handle schreiben");
    }
}
