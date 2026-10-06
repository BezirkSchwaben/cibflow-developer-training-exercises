// Testseite, ausgehender Adapter: Unit-Tests für die Simulation des Fachsystems, ohne Engine.
// Sie prüfen, dass der Adapter den Vertrag des Ports IBuchungssystem erfüllt: idempotent je Schlüssel,
// fachliche Ablehnung über dem Budget. In loesung/worker stehen dieselben Tests in GenehmigungVerbuchenHandlerTests.cs.
namespace GenehmigungWorker.Tests;

/// <summary>
/// Unit-Tests für BuchungssystemSimulation, jeder Test mit einer eigenen, frischen Datei.
/// </summary>
public class BuchungssystemSimulationTests
{
    [Fact]
    public void Gleicher_Schluessel_liefert_dieselbe_Buchungsnummer()
    {
        // gegeben: die Simulation mit einer frischen Datei
        var datei = Path.Combine(Path.GetTempPath(), $"buchungen-{Guid.NewGuid():N}.json");
        try
        {
            var simulation = new BuchungssystemSimulation(datei);
            var antrag = new Genehmigung("A-2026-0815", "huber", 1200m, "Dienstreise");
            // wenn: derselbe Antrag zweimal, etwa nach abgelaufenem Lock, dazwischen ein anderer
            var erste = simulation.Verbuchen(antrag);
            var zweite = simulation.Verbuchen(antrag);
            var andere = simulation.Verbuchen(new Genehmigung("A-2026-0816", "anna", 80m, "Fachbuch"));
            // und noch einmal nach einem Neustart des Workers: neue Instanz, gleiche Datei
            var nachNeustart = new BuchungssystemSimulation(datei).Verbuchen(antrag);
            // dann: dieselbe Nummer, und nur eine Buchung für A-2026-0815
            Assert.Matches(@"^B-\d{4}-0001$", erste);
            Assert.Equal(erste, zweite);
            Assert.Equal(erste, nachNeustart);
            Assert.EndsWith("-0002", andere);
        }
        finally
        {
            File.Delete(datei);
        }
    }

    [Fact]
    public void Ueber_dem_Budget_lehnt_die_Simulation_ab_und_speichert_nichts()
    {
        // gegeben: die Simulation mit einer frischen Datei
        var datei = Path.Combine(Path.GetTempPath(), $"buchungen-{Guid.NewGuid():N}.json");
        try
        {
            var simulation = new BuchungssystemSimulation(datei);
            // wenn: ein Antrag über dem Budget von 50.000 Euro je Buchung
            var fehler = Assert.Throws<BuchungAbgelehntException>(
                () => simulation.Verbuchen(new Genehmigung("A-2026-0817", "anna", 60000m, "Neue Serverhardware")));
            // dann: der Grund steht in der Meldung, und gespeichert ist nichts
            Assert.Equal(
                "Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei",
                fehler.Message);
            Assert.False(File.Exists(datei));
            // die nächste Buchung bekommt deshalb die erste Nummer
            Assert.EndsWith("-0001", simulation.Verbuchen(new Genehmigung("A-2026-0818", "anna", 1200m, "Dienstreise")));
        }
        finally
        {
            File.Delete(datei);
        }
    }
}
