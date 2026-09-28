namespace GenehmigungWorker.Tests;

/// <summary>
/// Unit-Test für den Handler: ohne Engine, ohne Netz, läuft in Millisekunden.
/// </summary>
public class GenehmigungVerbuchenHandlerTests
{
    // TODO Kapitel 12, Schritt 2: Test schreiben und danach "(Skip = ...)" entfernen
    [Fact(Skip = "TODO Kapitel 12, Schritt 2: Unit-Test schreiben, dann Skip entfernen")]
    public void Verbucht_Genehmigung_und_liefert_Buchungsnummer()
    {
        // gegeben: ein Task wie aus fetchAndLock, ein Fake statt des Fachsystems
        //   var task = new ExternalTask("t-1", "genehmigung-verbuchen", null, "A-2026-0815",
        //       "pi-4711", new() { ["antragsteller"] = "huber", ["betrag"] = 1200m,
        //                          ["begruendung"] = "Dienstreise" });
        //   var handler = new GenehmigungVerbuchenHandler(new BuchungssystemFake());
        // wenn
        //   var ergebnis = handler.Handle(task);
        // dann
        //   Assert.Equal("B-2026-0001", ergebnis["buchungsnummer"]);
        Assert.Fail("TODO Kapitel 12: Unit-Test für den Handler schreiben");
    }

    // Weitere Tests, wenn Zeit bleibt (Vorschläge in loesung/tests/GenehmigungWorker.Tests):
    //   - Ohne Business Key verbucht der Handler unter der Prozessinstanz-ID.
    //     Dafür merkt sich der Fake die Argumente, ein Assert vergleicht sie.
    //   - Fehlt eine Variable, etwa betrag, scheitert Handle laut mit KeyNotFoundException
    //     und verbucht nichts.
    //   - Bonus Idempotenz: zweimal derselbe Schlüssel an die BuchungssystemSimulation,
    //     beide Male dieselbe Nummer, auch nach einem Neustart (neue Instanz, gleiche Datei).
}
