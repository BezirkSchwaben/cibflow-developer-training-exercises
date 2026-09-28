using System.Net.Http.Json;

namespace GenehmigungWorker;

/// <summary>
/// Spielt prozess/genehmigungsworkflow.bpmn als neue Version in die Engine ein.
/// Aufruf: dotnet run -- deploy (im Projektordner)
///     oder dotnet run --project src/GenehmigungWorker -- deploy (im Repo-Root)
/// </summary>
public static class Deploy
{
    public const string BpmnPfad = "prozess/genehmigungsworkflow.bpmn";

    public static async Task AusfuehrenAsync(HttpClient http, string prozessKey)
    {
        // BPMN aus dem Repo in die Engine einspielen
        var datei = FindeImRepo(BpmnPfad);
        var bpmn = new ByteArrayContent(
            await File.ReadAllBytesAsync(datei));

        using var formular = new MultipartFormDataContent
        {
            { new StringContent(prozessKey), "deployment-name" },  // ProzessKey aus appsettings.json
            { new StringContent("true"), "enable-duplicate-filtering" },
            { bpmn, "data", "genehmigungsworkflow.bpmn" }
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
        if (neu.All(d => d.Key != prozessKey))
        {
            Console.WriteLine(
                $"Achtung: ProzessKey in appsettings.json ist {prozessKey}, das Modell hat aber " +
                $"die Process ID {string.Join(", ", neu.Select(d => d.Key))}. " +
                "Tragt die Process ID eures Modells als ProzessKey in appsettings.json ein.");
        }
    }

    /// <summary>
    /// Sucht die Datei vom aktuellen Ordner aus nach oben, danach vom Programmordner (bin/...) aus.
    /// So klappt der Aufruf im Repo-Root, im Projektordner und aus der IDE.
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
            "oder in einem Ordner darunter, etwa im Repo-Root oder in src/GenehmigungWorker.",
            relativerPfad);
    }

    private sealed record DeploymentDto(
        string Id,
        Dictionary<string, ProzessdefinitionDto>? DeployedProcessDefinitions);

    private sealed record ProzessdefinitionDto(string Key, int Version);
}
