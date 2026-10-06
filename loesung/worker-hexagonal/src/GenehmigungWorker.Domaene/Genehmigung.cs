// Mitte des Sechsecks, Domäne: das Domänenobjekt, mit dem der Kern arbeitet.
// Es kennt weder ExternalTask noch Variablen der Engine. Die Adapter übersetzen an der Grenze:
// Der eingehende Adapter baut es aus den Variablen des External Tasks, der ausgehende reicht es ans Fachsystem.
namespace GenehmigungWorker.Domaene;

/// <summary>
/// Eine erteilte Genehmigung, die verbucht werden soll.
/// </summary>
/// <param name="Schluessel">
/// Idempotenz-Schlüssel je Antrag: Kommt derselbe Schlüssel noch einmal, etwa nach abgelaufenem Lock,
/// liefert das Fachsystem dieselbe Buchungsnummer, statt ein zweites Mal zu buchen.
/// Woher er kommt (Business Key oder Prozessinstanz-ID), entscheidet der Adapter, nicht die Domäne.
/// </param>
/// <param name="Antragsteller">Wer den Antrag gestellt hat</param>
/// <param name="Betrag">Beantragter Betrag in Euro</param>
/// <param name="Begruendung">Begründung aus dem Antrag</param>
public sealed record Genehmigung(string Schluessel, string Antragsteller, decimal Betrag, string Begruendung);
