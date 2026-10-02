using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace GenehmigungWorker;

/// <summary>
/// Die Konfiguration des Workers. Quellen in dieser Reihenfolge, die spätere gewinnt:
/// appsettings.json, User Secrets, Umgebungsvariablen.
/// Zugangsdaten stehen nie in appsettings.json, die Datei liegt im Repo.
/// </summary>
public sealed class Einstellungen
{
    /// <summary>Basis-URL der Engine ohne /engine-rest, etwa http://localhost:8080</summary>
    public required string EngineUrl { get; init; }

    /// <summary>Process ID eures Modells, zugleich deployment-name beim Deployment</summary>
    public required string ProzessKey { get; init; }

    /// <summary>Topic des External Tasks, exakt wie im Modell</summary>
    public required string Topic { get; init; }

    /// <summary>Je Worker-Instanz eindeutig: Die Engine merkt sich, wer den Lock hält</summary>
    public required string WorkerId { get; init; }

    /// <summary>Benutzer für Basic Auth gegen /engine-rest, aus Umgebung oder User Secrets</summary>
    public required string EngineBenutzer { get; init; }

    /// <summary>Passwort für Basic Auth gegen /engine-rest, aus Umgebung oder User Secrets</summary>
    public required string EnginePasswort { get; init; }

    public static Einstellungen Laden()
    {
        var konfiguration = new ConfigurationBuilder()
            // appsettings.json liegt nach dem Build neben der DLL, egal wo ihr startet
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets(typeof(Einstellungen).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var fehlt = new List<string>();
        string Lesen(string schluessel)
        {
            var wert = konfiguration[schluessel];
            if (string.IsNullOrWhiteSpace(wert)) fehlt.Add(schluessel);
            return wert?.Trim() ?? "";
        }

        var einstellungen = new Einstellungen
        {
            EngineUrl = Lesen("EngineUrl"),
            ProzessKey = Lesen("ProzessKey"),
            Topic = Lesen("Topic"),
            WorkerId = Lesen("WorkerId"),
            EngineBenutzer = Lesen("EngineBenutzer"),
            EnginePasswort = Lesen("EnginePasswort"),
        };

        var zugangsdaten = fehlt.Where(s => s is "EngineBenutzer" or "EnginePasswort").ToList();
        var sonstige = fehlt.Except(zugangsdaten).ToList();

        if (sonstige.Count > 0)
        {
            throw new KonfigurationsFehler(
                $"In appsettings.json fehlt: {string.Join(", ", sonstige)}.");
        }

        if (zugangsdaten.Count > 0)
        {
            throw new KonfigurationsFehler(
                $"""
                Zugangsdaten für die Engine fehlen: {string.Join(", ", zugangsdaten)}.
                Setzt sie als Umgebungsvariablen oder User Secrets, nicht in appsettings.json:
                  bash:          export EngineBenutzer=worker EnginePasswort=worker
                  PowerShell:    $env:EngineBenutzer = "worker"; $env:EnginePasswort = "worker"
                  User Secrets, im Ordner worker/:
                                 dotnet user-secrets set EngineBenutzer worker --project src/GenehmigungWorker
                                 dotnet user-secrets set EnginePasswort worker --project src/GenehmigungWorker
                """);
        }

        if (!Uri.TryCreate(einstellungen.EngineUrl, UriKind.Absolute, out _))
        {
            throw new KonfigurationsFehler(
                $"EngineUrl \"{einstellungen.EngineUrl}\" ist keine gültige Adresse, erwartet etwa http://localhost:8080");
        }

        return einstellungen;
    }

    /// <summary>
    /// Ein HttpClient mit EngineUrl als BaseAddress und Basic Auth.
    /// Die Pfade im Code beginnen deshalb mit "/engine-rest/...".
    /// </summary>
    public HttpClient ErzeugeHttpClient()
    {
        var http = new HttpClient { BaseAddress = new Uri(EngineUrl) };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{EngineBenutzer}:{EnginePasswort}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        return http;
    }
}

/// <summary>Konfiguration unvollständig. Die Meldung sagt, was fehlt und wie ihr es setzt.</summary>
public sealed class KonfigurationsFehler(string meldung) : Exception(meldung);
