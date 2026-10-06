// Testseite, Fake des ausgehenden Ports IBuchungssystem: Er steht im Test dort, wo im Worker die Simulation steht.
// Damit läuft der Kern echt, nur das Fachsystem ist gespielt. Für den Use Case ist das kein Unterschied,
// er kennt nur den Port.
namespace GenehmigungWorker.Tests;

/// <summary>
/// Ersetzt das Fachsystem im Test. Liefert immer B-2026-0001 und merkt sich jede Genehmigung,
/// damit ein Test prüfen kann, was beim Fachsystem angekommen ist. Weil Genehmigung ein Record ist,
/// vergleicht Assert.Equal sie Wert für Wert.
/// Mit gesetzter Ablehnung lehnt der Fake jede Buchung mit diesem Grund ab, wie das Fachsystem über dem Budget.
/// </summary>
public class BuchungssystemFake : IBuchungssystem
{
    public List<Genehmigung> Aufrufe { get; } = [];

    /// <summary>Ist ein Grund gesetzt, wirft Verbuchen BuchungAbgelehntException mit diesem Grund</summary>
    public string? Ablehnung { get; init; }

    public string Verbuchen(Genehmigung genehmigung)
    {
        Aufrufe.Add(genehmigung);
        if (Ablehnung is not null) throw new BuchungAbgelehntException(Ablehnung);
        return "B-2026-0001";
    }
}
