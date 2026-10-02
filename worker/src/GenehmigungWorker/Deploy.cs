using System.Net.Http.Json;

namespace GenehmigungWorker;

/// <summary>
/// Spielt prozess/genehmigungsworkflow.bpmn als neue Version in die Engine ein,
/// mit Pfad eine andere BPMN-Datei, etwa eine Variante aus prozess/varianten/.
/// Aufruf: dotnet run -- deploy (im Projektordner)
///     oder dotnet run --project src/GenehmigungWorker -- deploy (im Ordner worker/)
///     oder dotnet run -- deploy prozess/varianten/verbuchen-fehlerpfad.bpmn
/// </summary>
public static class Deploy
{
    public const string BpmnPfad = "prozess/genehmigungsworkflow.bpmn";

    /// <param name="pfad">
    /// Ohne Pfad das Modell unter prozess/, deployment-name ist der ProzessKey.
    /// Mit Pfad diese Datei, deployment-name ist ihr Dateiname ohne Endung.
    /// </param>
    public static async Task AusfuehrenAsync(HttpClient http, string prozessKey, string? pfad = null)
    {
        // BPMN aus dem Repo in die Engine einspielen
        var datei = FindeImRepo(pfad ?? BpmnPfad);
        var bpmn = new ByteArrayContent(
            await File.ReadAllBytesAsync(datei));

        // deployment-name: ohne Pfad der ProzessKey aus appsettings.json, mit Pfad der Dateiname,
        // etwa verbuchen-fehlerpfad. Der Duplikatfilter vergleicht mit früheren Deployments gleichen Namens.
        var deploymentName = pfad is null ? prozessKey : Path.GetFileNameWithoutExtension(datei);

        using var formular = new MultipartFormDataContent
        {
            { new StringContent(deploymentName), "deployment-name" },
            { new StringContent("true"), "enable-duplicate-filtering" },
            { bpmn, "data", Path.GetFileName(datei) }
        };

        var antwort = await http.PostAsync("/engine-rest/deployment/create", formular);
        if (!antwort.IsSuccessStatusCode)
        {
            // Die Engine sagt, was ihr am Modell nicht passt, etwa einen Parse-Fehler
            Console.Error.WriteLine($"Die Engine lehnt das Deployment ab: {await antwort.Content.ReadAsStringAsync()}");
        }
        antwort.EnsureSuccessStatusCode();

        var deployment = await antwort.Content.ReadFromJsonAsync<DeploymentDto>();
        var neu = deployment?.DeployedProcessDefinitions?.Values.ToList() ?? [];
        Console.WriteLine($"Deployment {deployment?.Id} aus {datei}");
        if (neu.Count == 0)
        {
            // enable-duplicate-filtering: gleicher Name, gleicher Inhalt, keine neue Version
            Console.WriteLine("Modell unverändert, die Engine hat keine neue Version angelegt.");
            return;
        }
        foreach (var definition in neu)
        {
            Console.WriteLine($"Neue Version: {definition.Key}, Version {definition.Version}");
        }
        // Nur beim Modell unter prozess/ muss die Process ID zum ProzessKey passen,
        // eine Variante hat ihre eigene
        if (pfad is null && neu.All(d => d.Key != prozessKey))
        {
            Console.WriteLine(
                $"Achtung: ProzessKey in appsettings.json ist {prozessKey}, das Modell hat aber " +
                $"die Process ID {string.Join(", ", neu.Select(d => d.Key))}. " +
                "Tragt die Process ID eures Modells als ProzessKey in appsettings.json ein.");
        }
    }

    /// <summary>
    /// Sucht die Datei vom aktuellen Ordner aus nach oben, danach vom Programmordner (bin/...) aus.
    /// So klappt der Aufruf im Ordner worker/, im Projektordner und aus der IDE.
    /// </summary>
    public static string FindeImRepo(string relativerPfad)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var ordner = new DirectoryInfo(start); ordner is not null; ordner = ordner.Parent)
            {
                var kandidat = Path.Combine(ordner.FullName, relativerPfad);
                if (File.Exists(kandidat)) return kandidat;
            }
        }
        throw new FileNotFoundException(
            $"{relativerPfad} nicht gefunden. Legt euer Modell dort ab und startet im Repo " +
            "oder in einem Ordner darunter, etwa in worker/ oder in worker/src/GenehmigungWorker.",
            relativerPfad);
    }

    private sealed record DeploymentDto(
        string Id,
        Dictionary<string, ProzessdefinitionDto>? DeployedProcessDefinitions);

    private sealed record ProzessdefinitionDto(string Key, int Version);
}
