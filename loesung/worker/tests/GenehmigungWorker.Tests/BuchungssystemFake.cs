namespace GenehmigungWorker.Tests;

/// <summary>
/// Ersetzt das Fachsystem im Test: Der Handler läuft echt, nur die Buchung ist gespielt.
/// Liefert immer B-2026-0001 und merkt sich jeden Aufruf, damit ein Test prüfen kann,
/// was der Handler übergeben hat.
/// </summary>
public class BuchungssystemFake : IBuchungssystem
{
    public List<Aufruf> Aufrufe { get; } = [];

    public string Verbuchen(string schluessel, string antragsteller, decimal betrag, string begruendung)
    {
        Aufrufe.Add(new Aufruf(schluessel, antragsteller, betrag, begruendung));
        return "B-2026-0001";
    }

    /// <summary>Ein Aufruf von Verbuchen mit seinen Argumenten</summary>
    public record Aufruf(string Schluessel, string Antragsteller, decimal Betrag, string Begruendung);
}
