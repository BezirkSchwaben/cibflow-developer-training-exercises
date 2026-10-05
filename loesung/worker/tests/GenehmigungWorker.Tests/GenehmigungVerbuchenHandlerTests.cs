using System.Globalization;

namespace GenehmigungWorker.Tests;

/// <summary>
/// Unit-Tests für den Handler und die Simulation: ohne Engine, ohne Netz, laufen in Millisekunden.
/// </summary>
public class GenehmigungVerbuchenHandlerTests
{
    [Fact]
    public void Verbucht_Genehmigung_und_liefert_Buchungsnummer()
    {
        // gegeben: ein Task wie aus fetchAndLock, ein Fake statt des Fachsystems
        var task = new ExternalTask("t-1", "genehmigung-verbuchen", null, "A-2026-0815",
            "pi-4711", new() { ["antragsteller"] = "huber", ["betrag"] = 1200m,
                               ["begruendung"] = "Dienstreise" });
        var handler = new GenehmigungVerbuchenHandler(new BuchungssystemFake());
        // wenn
        var ergebnis = handler.Handle(task);
        // dann
        Assert.Equal("B-2026-0001", ergebnis["buchungsnummer"]);
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
        new GenehmigungVerbuchenHandler(fake).Handle(task);
        // dann: genau eine Buchung, mit der Prozessinstanz-ID als Schlüssel und den Antragsdaten
        var aufruf = Assert.Single(fake.Aufrufe);
        Assert.Equal(new BuchungssystemFake.Aufruf("pi-4712", "anna", 1200m, "Fachtagung"), aufruf);
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
            new GenehmigungVerbuchenHandler(fake).Handle(task);
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
        var handler = new GenehmigungVerbuchenHandler(fake);
        // wenn, dann: laut scheitern statt still etwas Falsches verbuchen
        var fehler = Assert.Throws<KeyNotFoundException>(() => handler.Handle(task));
        Assert.Contains("betrag", fehler.Message);
        Assert.Empty(fake.Aufrufe);
    }

    [Fact]
    public void Gleicher_Schluessel_liefert_dieselbe_Buchungsnummer()
    {
        // gegeben: die Simulation mit einer frischen Datei
        var datei = Path.Combine(Path.GetTempPath(), $"buchungen-{Guid.NewGuid():N}.json");
        try
        {
            var simulation = new BuchungssystemSimulation(datei);
            // wenn: derselbe Antrag zweimal, etwa nach abgelaufenem Lock, dazwischen ein anderer
            var erste = simulation.Verbuchen("A-2026-0815", "huber", 1200m, "Dienstreise");
            var zweite = simulation.Verbuchen("A-2026-0815", "huber", 1200m, "Dienstreise");
            var andere = simulation.Verbuchen("A-2026-0816", "anna", 80m, "Fachbuch");
            // und noch einmal nach einem Neustart des Workers: neue Instanz, gleiche Datei
            var nachNeustart = new BuchungssystemSimulation(datei)
                .Verbuchen("A-2026-0815", "huber", 1200m, "Dienstreise");
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
                () => simulation.Verbuchen("A-2026-0817", "anna", 60000m, "Neue Serverhardware"));
            // dann: der Grund steht in der Meldung, und gespeichert ist nichts
            Assert.Equal(
                "Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei",
                fehler.Message);
            Assert.False(File.Exists(datei));
            // die nächste Buchung bekommt deshalb die erste Nummer
            Assert.EndsWith("-0001", simulation.Verbuchen("A-2026-0818", "anna", 1200m, "Dienstreise"));
        }
        finally
        {
            File.Delete(datei);
        }
    }

    [Fact]
    public void Abgelehnte_Buchung_reicht_der_Handler_an_die_Schleife_durch()
    {
        // gegeben: ein Antrag über dem Budget, betrag als long wie aus der Engine, dazu die echte Simulation
        var datei = Path.Combine(Path.GetTempPath(), $"buchungen-{Guid.NewGuid():N}.json");
        try
        {
            var task = new ExternalTask("t-4", "genehmigung-verbuchen", null, "A-2026-0819",
                "pi-4714", new() { ["antragsteller"] = "anna", ["betrag"] = 60000L,
                                   ["begruendung"] = "Neue Serverhardware" });
            var handler = new GenehmigungVerbuchenHandler(new BuchungssystemSimulation(datei));
            // wenn, dann: kein Ergebnis, sondern die Ablehnung mit Grund. Die Schleife meldet sie per bpmnError.
            var fehler = Assert.Throws<BuchungAbgelehntException>(() => handler.Handle(task));
            Assert.StartsWith("Budget der Kostenstelle reicht nicht", fehler.Message);
        }
        finally
        {
            File.Delete(datei);
        }
    }
}
