// Testseite, eingehender Adapter: Unit-Tests für die Übersetzung an der Grenze, ohne Engine.
// Hier geht es nur um die Abbildung: Welche Variable des External Tasks landet in welchem Feld der Genehmigung,
// welcher Schlüssel gilt, und was passiert, wenn eine Variable fehlt. Die Fachlogik prüft GenehmigungVerbuchenTests.cs.
using System.Globalization;

namespace GenehmigungWorker.Tests;

/// <summary>
/// Unit-Tests für GenehmigungVerbuchenAdapter: ein Task wie aus fetchAndLock, der echte Use Case, ein Fake als Fachsystem,
/// für die Ablehnung die echte Simulation.
/// </summary>
public class GenehmigungVerbuchenAdapterTests
{
    [Fact]
    public void Liest_die_Variablen_und_liefert_die_Buchungsnummer()
    {
        // gegeben: ein Task wie aus fetchAndLock, Use Case und Fake statt des Fachsystems
        var task = new ExternalTask("t-1", "genehmigung-verbuchen", null, "A-2026-0815",
            "pi-4711", new() { ["antragsteller"] = "huber", ["betrag"] = 1200m,
                               ["begruendung"] = "Dienstreise" });
        var fake = new BuchungssystemFake();
        var adapter = new GenehmigungVerbuchenAdapter(new GenehmigungVerbuchen(fake));
        // wenn
        var ergebnis = adapter.Handle(task);
        // dann: buchungsnummer als Variable zurück, beim Fachsystem genau diese Genehmigung
        Assert.Equal("B-2026-0001", ergebnis["buchungsnummer"]);
        Assert.Equal(new Genehmigung("A-2026-0815", "huber", 1200m, "Dienstreise"), Assert.Single(fake.Aufrufe));
    }

    [Fact]
    public void Ohne_Business_Key_verbucht_unter_der_Prozessinstanz_ID()
    {
        // gegeben: ein Antrag ohne Business Key wie aus dem Startformular, betrag hier als long
        // wie nach einem Start per REST, so wie ExternalTaskClient ihn aus der Engine auspackt
        var task = new ExternalTask("t-2", "genehmigung-verbuchen", null, null,
            "pi-4712", new() { ["antragsteller"] = "anna", ["betrag"] = 1200L,
                               ["begruendung"] = "Fachtagung" });
        var fake = new BuchungssystemFake();
        // wenn
        new GenehmigungVerbuchenAdapter(new GenehmigungVerbuchen(fake)).Handle(task);
        // dann: genau eine Buchung, mit der Prozessinstanz-ID als Schlüssel und den Antragsdaten
        Assert.Equal(new Genehmigung("pi-4712", "anna", 1200m, "Fachtagung"), Assert.Single(fake.Aufrufe));
    }

    [Theory]
    [InlineData("1234.5", 1234.5)] // aus dem easyForm-Feld "Zahl": Text, immer mit Punkt
    [InlineData(1200L, 1200.0)]    // per REST gestartet: ganze Zahl, wie ExternalTaskClient sie auspackt
    public void Liest_betrag_als_Text_und_als_Zahl_auch_auf_einem_deutschen_Rechner(object betrag, double erwartet)
    {
        // gegeben: ein Rechner mit deutscher Kultur, auf dem "1234.5" sonst 12345 ergäbe
        var vorher = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var task = new ExternalTask("t-5", "genehmigung-verbuchen", null, null,
                "pi-4715", new() { ["antragsteller"] = "anna", ["betrag"] = betrag,
                                   ["begruendung"] = "Fachtagung" });
            var fake = new BuchungssystemFake();
            // wenn
            new GenehmigungVerbuchenAdapter(new GenehmigungVerbuchen(fake)).Handle(task);
            // dann: der Betrag kommt unverändert beim Fachsystem an
            Assert.Equal((decimal) erwartet, Assert.Single(fake.Aufrufe).Betrag);
        }
        finally
        {
            CultureInfo.CurrentCulture = vorher;
        }
    }

    [Fact]
    public void Fehlende_Variable_scheitert_laut()
    {
        // gegeben: betrag fehlt, etwa weil das Formular das Feld nicht mehr hat
        var task = new ExternalTask("t-3", "genehmigung-verbuchen", null, "A-2026-0816",
            "pi-4713", new() { ["antragsteller"] = "huber", ["begruendung"] = "Dienstreise" });
        var fake = new BuchungssystemFake();
        var adapter = new GenehmigungVerbuchenAdapter(new GenehmigungVerbuchen(fake));
        // wenn, dann: laut scheitern statt still etwas Falsches verbuchen. Der Kern sieht davon nichts.
        var fehler = Assert.Throws<KeyNotFoundException>(() => adapter.Handle(task));
        Assert.Contains("betrag", fehler.Message);
        Assert.Empty(fake.Aufrufe);
    }

    [Fact]
    public void Abgelehnte_Buchung_reicht_der_Adapter_an_die_Schleife_durch()
    {
        // gegeben: ein Antrag über dem Budget, betrag als long wie aus der Engine,
        // dazu der echte Use Case und die echte Simulation
        var datei = Path.Combine(Path.GetTempPath(), $"buchungen-{Guid.NewGuid():N}.json");
        try
        {
            var task = new ExternalTask("t-4", "genehmigung-verbuchen", null, "A-2026-0819",
                "pi-4714", new() { ["antragsteller"] = "anna", ["betrag"] = 60000L,
                                   ["begruendung"] = "Neue Serverhardware" });
            var adapter = new GenehmigungVerbuchenAdapter(
                new GenehmigungVerbuchen(new BuchungssystemSimulation(datei)));
            // wenn, dann: kein Ergebnis, sondern die Ablehnung mit Grund. Die Schleife meldet sie per bpmnError.
            var fehler = Assert.Throws<BuchungAbgelehntException>(() => adapter.Handle(task));
            Assert.StartsWith("Budget der Kostenstelle reicht nicht", fehler.Message);
        }
        finally
        {
            File.Delete(datei);
        }
    }
}
