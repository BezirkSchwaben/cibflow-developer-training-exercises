// Außen am Sechseck, ausgehender (getriebener) Adapter: Der Kern treibt ihn über den Port IBuchungssystem.
// Er erfüllt den Port mit einer Technik, hier einer JSON-Datei statt einer echten Anbindung.
// Kommt das echte Fachsystem, ersetzt ein neuer Adapter diese Datei, Domäne und Engine-Adapter bleiben.
// Verhalten, Meldungen und Logzeilen sind dieselben wie in loesung/worker, nur Verbuchen nimmt eine Genehmigung.
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using GenehmigungWorker.Domaene;
using GenehmigungWorker.Domaene.Ports;

namespace GenehmigungWorker.Adapter.Fachsystem;

/// <summary>
/// Simuliert das Fachsystem: Statt einer echten Anbindung vergibt sie fortlaufende
/// Buchungsnummern (B-2026-0001, B-2026-0002, ...) und schreibt jede Buchung ins Log.
///
/// Idempotent: Kommt derselbe Schlüssel noch einmal, liefert Verbuchen die Nummer der ersten
/// Buchung, statt ein zweites Mal zu buchen. Die Buchungen liegen in einer JSON-Datei,
/// deshalb gilt das auch nach einem Neustart des Workers.
///
/// Fachlicher Fehler: Liegt der Betrag über BudgetJeBuchung, lehnt die Simulation ab und
/// wirft BuchungAbgelehntException mit dem Grund. Eine abgelehnte Buchung speichert sie nicht.
/// </summary>
public class BuchungssystemSimulation : IBuchungssystem
{
    /// <summary>Mehr gibt die Kostenstelle je Buchung nicht her, darüber lehnt die Simulation ab</summary>
    public const decimal BudgetJeBuchung = 50_000m;

    // Beträge in Meldung und Log immer deutsch (60.000,00), egal wie der Rechner eingestellt ist
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        // Umlaute lesbar in die Datei schreiben, nicht als ü
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _datei;
    private readonly Dictionary<string, Buchung> _buchungen;

    /// <param name="datei">
    /// JSON-Datei mit allen bisherigen Buchungen je Schlüssel. Gibt es sie noch nicht,
    /// legt die erste Buchung sie an. Löscht ihr sie, beginnt die Simulation von vorn.
    /// </param>
    public BuchungssystemSimulation(string datei)
    {
        _datei = datei;
        _buchungen = File.Exists(datei)
            ? JsonSerializer.Deserialize<Dictionary<string, Buchung>>(File.ReadAllText(datei)) ?? []
            : [];
    }

    public string Verbuchen(Genehmigung genehmigung)
    {
        var (schluessel, antragsteller, betrag, begruendung) = genehmigung;

        // Derselbe Schlüssel noch einmal, etwa nach abgelaufenem Lock: keine zweite Buchung
        if (_buchungen.TryGetValue(schluessel, out var vorhanden))
        {
            Log($"Schlüssel {schluessel} ist schon verbucht als {vorhanden.Buchungsnummer}, keine zweite Buchung.");
            return vorhanden.Buchungsnummer;
        }

        // Über dem Budget lehnt das Fachsystem ab: nichts speichern, keine Nummer.
        // Die Ausnahme gehört zum Vertrag des Ports, der Engine-Adapter meldet daraus bpmnError.
        if (betrag > BudgetJeBuchung)
        {
            throw new BuchungAbgelehntException(
                $"Budget der Kostenstelle reicht nicht: {betrag.ToString("N2", Deutsch)} Euro beantragt, " +
                $"{BudgetJeBuchung.ToString("N2", Deutsch)} Euro frei");
        }

        // Fortlaufend je Jahr: B-2026-0001, B-2026-0002, ...
        var praefix = $"B-{DateTime.Now.Year}-";
        var laufend = _buchungen.Values.Count(b => b.Buchungsnummer.StartsWith(praefix, StringComparison.Ordinal)) + 1;
        var nummer = $"{praefix}{laufend:D4}";

        // Erst speichern, dann die Nummer zurückgeben. Stirbt der Worker danach und vor complete,
        // kennt die Datei die Buchung schon, und der nächste Versuch bekommt dieselbe Nummer.
        _buchungen[schluessel] = new Buchung(nummer, antragsteller, betrag, begruendung, DateTimeOffset.Now);
        Speichern();

        Log($"Verbucht: {nummer} für {antragsteller}, {betrag.ToString("N2", Deutsch)} Euro, \"{begruendung}\" (Schlüssel {schluessel})");
        return nummer;
    }

    private void Speichern()
    {
        // Erst in eine Hilfsdatei schreiben, dann umbenennen:
        // So bleibt die Datei lesbar, auch wenn der Worker mitten im Schreiben abbricht.
        var hilfsdatei = _datei + ".tmp";
        File.WriteAllText(hilfsdatei, JsonSerializer.Serialize(_buchungen, Format));
        File.Move(hilfsdatei, _datei, overwrite: true);
    }

    private static void Log(string text) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {text}");

    /// <summary>
    /// Eine Buchung, so wie sie in der Datei steht. Das Dateiformat gehört dem Adapter, nicht der Domäne,
    /// es ist dasselbe wie in loesung/worker.
    /// </summary>
    public record Buchung(
        string Buchungsnummer,
        string Antragsteller,
        decimal Betrag,
        string Begruendung,
        DateTimeOffset Verbucht);
}
