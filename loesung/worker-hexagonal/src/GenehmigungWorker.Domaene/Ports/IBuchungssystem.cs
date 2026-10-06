// Rand der Mitte, ausgehender Port: Das braucht der Kern von außen, beschrieben mit seinen eigenen Worten.
// Die Domäne besitzt dieses Interface, ein Adapter erfüllt es: im Worker die Simulation
// (Adapter/Fachsystem/BuchungssystemSimulation.cs), im Unit-Test ein Fake, später das echte Fachsystem.
// Anders als in loesung/worker nimmt Verbuchen ein Domänenobjekt statt vier einzelner Werte.
namespace GenehmigungWorker.Domaene.Ports;

/// <summary>
/// Ausgehender Port: das Fachsystem, in dem der Kern die Genehmigung verbucht.
/// </summary>
public interface IBuchungssystem
{
    /// <summary>
    /// Verbucht die Genehmigung und liefert die Buchungsnummer.
    /// Kommt derselbe <see cref="Genehmigung.Schluessel"/> noch einmal, liefert das Fachsystem dieselbe Nummer.
    /// Lehnt es ab, wirft es <see cref="BuchungAbgelehntException"/> mit dem Grund.
    /// </summary>
    string Verbuchen(Genehmigung genehmigung);
}
