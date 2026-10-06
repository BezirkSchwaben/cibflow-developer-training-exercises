// Mitte des Sechsecks, Domäne: die fachliche Ablehnung. Sie gehört zum Vertrag der Ports:
// Der ausgehende Adapter (Adapter/Fachsystem/BuchungssystemSimulation.cs) wirft sie, der Use Case reicht sie durch,
// und der eingehende Adapter (Adapter/Engine/ExternalTaskWorker.cs) macht daraus einen bpmnError. Weil sie hier liegt,
// kennen beide Seiten sie, ohne einander zu kennen.
namespace GenehmigungWorker.Domaene;

/// <summary>
/// Das Fachsystem lehnt die Buchung ab, etwa weil das Budget der Kostenstelle nicht reicht.
/// Ein fachlicher Fehler, kein Bug: Ein zweiter Versuch ändert nichts daran.
/// Die Domäne sagt nur "abgelehnt" und warum. Den errorCode BUCHUNG_ABGELEHNT kennt nur der Engine-Adapter,
/// er ist ein Teil des Modells, nicht der Fachlogik.
/// </summary>
/// <param name="grund">Warum das Fachsystem ablehnt, im Modell danach in der Variablen errorMessage</param>
public sealed class BuchungAbgelehntException(string grund) : Exception(grund);
