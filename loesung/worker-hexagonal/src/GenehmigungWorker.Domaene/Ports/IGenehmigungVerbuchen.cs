// Rand der Mitte, eingehender Port: So lässt sich der Kern von außen treiben.
// Die Domäne besitzt dieses Interface, der Use Case GenehmigungVerbuchen erfüllt es.
// Wer den Kern ruft, kennt nur diesen Port: heute der Engine-Adapter (Adapter/Engine/GenehmigungVerbuchenAdapter.cs),
// morgen vielleicht ein REST-Controller oder ein Consumer für Nachrichten. Am Kern ändert das nichts.
namespace GenehmigungWorker.Domaene.Ports;

/// <summary>
/// Eingehender Port: eine Genehmigung verbuchen.
/// </summary>
public interface IGenehmigungVerbuchen
{
    /// <summary>
    /// Verbucht die Genehmigung und liefert die Buchungsnummer.
    /// Lehnt das Fachsystem ab, fliegt <see cref="BuchungAbgelehntException"/> mit dem Grund.
    /// </summary>
    string Verbuchen(Genehmigung genehmigung);
}
