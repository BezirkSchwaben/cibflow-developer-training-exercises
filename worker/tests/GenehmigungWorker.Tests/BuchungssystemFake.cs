namespace GenehmigungWorker.Tests;

/// <summary>
/// Ersetzt das Fachsystem im Test: Der Handler läuft echt, nur die Buchung ist gespielt.
/// </summary>
public class BuchungssystemFake : IBuchungssystem
{
    public string Verbuchen(string schluessel, string antragsteller, decimal betrag, string begruendung)
    {
        // TODO Kapitel 12, Schritt 2: immer "B-2026-0001" zurückgeben
        throw new NotImplementedException("TODO Kapitel 12: BuchungssystemFake.Verbuchen schreiben");
    }
}
