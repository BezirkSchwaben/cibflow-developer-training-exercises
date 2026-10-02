namespace GenehmigungWorker.Fachsystem;

/// <summary>
/// Die eine Stelle nach außen: das Fachsystem, in dem der Worker die Genehmigung verbucht.
/// Im Worker steckt dahinter die BuchungssystemSimulation, im Unit-Test ein Fake,
/// später das echte Fachsystem.
/// </summary>
public interface IBuchungssystem
{
    /// <summary>
    /// Verbucht die Genehmigung und liefert die Buchungsnummer.
    /// </summary>
    /// <param name="schluessel">
    /// Idempotenz-Schlüssel je Antrag (Business Key oder Prozessinstanz-ID):
    /// Kommt derselbe Schlüssel noch einmal, liefert das Fachsystem dieselbe Nummer.
    /// </param>
    string Verbuchen(string schluessel, string antragsteller, decimal betrag, string begruendung);
}
