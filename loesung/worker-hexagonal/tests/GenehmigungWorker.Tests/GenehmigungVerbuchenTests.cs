// Testseite, Mitte des Sechsecks: Unit-Tests für den Use Case. Sie bauen weder einen ExternalTask
// noch ein Dictionary mit Variablen, sie kennen nur Genehmigung, die Ports und einen Fake.
// Das ist der Gewinn des Schnitts: Die Fachlogik läuft ohne Engine, ohne Netz und ohne den Worker.
namespace GenehmigungWorker.Tests;

/// <summary>
/// Unit-Tests für GenehmigungVerbuchen: nur Domäne und Fake des ausgehenden Ports.
/// </summary>
public class GenehmigungVerbuchenTests
{
    [Fact]
    public void Verbucht_die_Genehmigung_im_Fachsystem_und_liefert_die_Buchungsnummer()
    {
        // gegeben: der Use Case mit einem Fake statt des Fachsystems
        var fachsystem = new BuchungssystemFake();
        IGenehmigungVerbuchen useCase = new GenehmigungVerbuchen(fachsystem);
        var genehmigung = new Genehmigung("A-2026-0815", "huber", 1200m, "Dienstreise");
        // wenn
        var nummer = useCase.Verbuchen(genehmigung);
        // dann: die Nummer des Fachsystems, und dort ist genau diese Genehmigung angekommen
        Assert.Equal("B-2026-0001", nummer);
        Assert.Equal(genehmigung, Assert.Single(fachsystem.Aufrufe));
    }

    [Fact]
    public void Reicht_die_Ablehnung_des_Fachsystems_durch()
    {
        // gegeben: ein Fachsystem, das ablehnt
        var fachsystem = new BuchungssystemFake { Ablehnung = "Budget der Kostenstelle reicht nicht" };
        var useCase = new GenehmigungVerbuchen(fachsystem);
        // wenn, dann: keine Nummer, sondern die Ablehnung mit Grund. Was daraus in der Engine wird,
        // entscheidet der Engine-Adapter, nicht der Use Case.
        var fehler = Assert.Throws<BuchungAbgelehntException>(
            () => useCase.Verbuchen(new Genehmigung("A-2026-0817", "anna", 60000m, "Neue Serverhardware")));
        Assert.Equal("Budget der Kostenstelle reicht nicht", fehler.Message);
    }
}
