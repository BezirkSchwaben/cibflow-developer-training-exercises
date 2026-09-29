namespace GenehmigungWorker.Fachsystem;

/// <summary>
/// Das Fachsystem lehnt die Buchung ab, etwa weil das Budget der Kostenstelle nicht reicht.
/// Ein fachlicher Fehler, kein Bug: Ein zweiter Versuch ändert nichts daran.
/// Die Schleife in Program.cs meldet ihn deshalb nicht per failure, sondern per bpmnError
/// mit errorCode BUCHUNG_ABGELEHNT, die Message geht als errorMessage mit.
/// </summary>
/// <param name="grund">Warum das Fachsystem ablehnt, im Modell danach in der Variablen errorMessage</param>
public sealed class BuchungAbgelehntException(string grund) : Exception(grund);
