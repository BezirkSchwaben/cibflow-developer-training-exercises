namespace GenehmigungWorker.Fachsystem;

/// <summary>
/// Simuliert das Fachsystem: Statt einer echten Anbindung vergibt sie eine Buchungsnummer
/// und schreibt die Buchung ins Log.
/// </summary>
public class BuchungssystemSimulation : IBuchungssystem
{
    public string Verbuchen(string schluessel, string antragsteller, decimal betrag, string begruendung)
    {
        // TODO Kapitel 12, Schritt 3: Buchung simulieren
        //   Eine fortlaufende Buchungsnummer vergeben, etwa "B-2026-0001", dann "B-2026-0002",
        //   und die Buchung ins Log schreiben (Schlüssel, antragsteller, betrag, begruendung, Nummer).
        //
        // Bonus Idempotenz: Kommt derselbe Schlüssel noch einmal, liefert Verbuchen dieselbe
        //   Nummer statt einer zweiten Buchung. Damit das einen Neustart des Workers übersteht,
        //   legt ihr Schlüssel und Nummer in einer Datei ab, nicht nur im Speicher.
        //   Den Dateipfad gebt ihr am besten im Konstruktor mit, dann nimmt der Test eine eigene Datei.
        throw new NotImplementedException("TODO Kapitel 12: BuchungssystemSimulation.Verbuchen schreiben");
    }
}
