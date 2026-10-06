// Mitte des Sechsecks, Domäne: der Use Case "Genehmigung verbuchen".
// Er erfüllt den eingehenden Port und ruft den ausgehenden. Mehr kennt er nicht:
// keine Engine, keinen ExternalTask, kein HTTP, keine Variablennamen des Modells.
// Deshalb testet ihn GenehmigungVerbuchenTests.cs mit nichts als einer Genehmigung und einem Fake.
using GenehmigungWorker.Domaene.Ports;

namespace GenehmigungWorker.Domaene;

/// <summary>
/// Verbucht eine erteilte Genehmigung im Fachsystem.
/// Heute reicht der Use Case nur durch, die Schichten-Fassung hatte an dieser Stelle auch keine Regel.
/// Kommt eine fachliche Regel dazu, etwa eine Prüfung vor dem Verbuchen, gehört sie hierher, nie in einen Adapter.
/// </summary>
/// <param name="buchungssystem">ausgehender Port, im Worker die Simulation, im Test ein Fake</param>
public sealed class GenehmigungVerbuchen(IBuchungssystem buchungssystem) : IGenehmigungVerbuchen
{
    public string Verbuchen(Genehmigung genehmigung)
    {
        // Der Kern spricht nur in Domänenobjekten: Genehmigung rein, Buchungsnummer raus
        return buchungssystem.Verbuchen(genehmigung);
    }
}
