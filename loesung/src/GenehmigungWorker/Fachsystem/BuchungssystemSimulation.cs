using System.Text.Encodings.Web;
using System.Text.Json;

namespace GenehmigungWorker.Fachsystem;

/// <summary>
/// Simuliert das Fachsystem: Statt einer echten Anbindung vergibt sie fortlaufende
/// Buchungsnummern (B-2026-0001, B-2026-0002, ...) und schreibt jede Buchung ins Log.
///
/// Idempotent: Kommt derselbe Schlüssel noch einmal, liefert Verbuchen die Nummer der ersten
/// Buchung, statt ein zweites Mal zu buchen. Die Buchungen liegen in einer JSON-Datei,
/// deshalb gilt das auch nach einem Neustart des Workers.
/// </summary>
public class BuchungssystemSimulation : IBuchungssystem
{
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

    public string Verbuchen(string schluessel, string antragsteller, decimal betrag, string begruendung)
    {
        // Derselbe Schlüssel noch einmal, etwa nach abgelaufenem Lock: keine zweite Buchung
        if (_buchungen.TryGetValue(schluessel, out var vorhanden))
        {
            Log($"Schlüssel {schluessel} ist schon verbucht als {vorhanden.Buchungsnummer}, keine zweite Buchung.");
            return vorhanden.Buchungsnummer;
        }

        // Fortlaufend je Jahr: B-2026-0001, B-2026-0002, ...
        var praefix = $"B-{DateTime.Now.Year}-";
        var laufend = _buchungen.Values.Count(b => b.Buchungsnummer.StartsWith(praefix, StringComparison.Ordinal)) + 1;
        var nummer = $"{praefix}{laufend:D4}";

        // Erst speichern, dann die Nummer zurückgeben. Stirbt der Worker danach und vor complete,
        // kennt die Datei die Buchung schon, und der nächste Versuch bekommt dieselbe Nummer.
        _buchungen[schluessel] = new Buchung(nummer, antragsteller, betrag, begruendung, DateTimeOffset.Now);
        Speichern();

        Log($"Verbucht: {nummer} für {antragsteller}, {betrag:0.00} Euro, \"{begruendung}\" (Schlüssel {schluessel})");
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

    /// <summary>Eine Buchung, so wie sie in der Datei steht</summary>
    public record Buchung(
        string Buchungsnummer,
        string Antragsteller,
        decimal Betrag,
        string Begruendung,
        DateTimeOffset Verbucht);
}
