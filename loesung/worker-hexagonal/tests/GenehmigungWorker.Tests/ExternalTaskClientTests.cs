using System.Net;
using System.Text.Json;

namespace GenehmigungWorker.Tests;

/// <summary>
/// Unit-Test für den ExternalTaskClient: ohne Engine. Statt ins Netz geht der Call an einen
/// HttpMessageHandler, der die Anfrage mitschneidet und wie die Engine antwortet.
/// </summary>
public class ExternalTaskClientTests
{
    [Fact]
    public async Task BpmnError_schickt_workerId_errorCode_und_errorMessage()
    {
        // gegeben: ein Client mit mitschneidendem HttpClient und ein gesperrter Task
        var mitschnitt = new Mitschnitt();
        using var http = new HttpClient(mitschnitt) { BaseAddress = new Uri("http://localhost:8080") };
        var client = new ExternalTaskClient(http, "genehmigung-worker-1", "genehmigung-verbuchen");
        var task = new ExternalTask("t-5", "genehmigung-verbuchen", null, "A-2026-0820", "pi-4715", []);
        // wenn
        await client.BpmnErrorAsync(task, "BUCHUNG_ABGELEHNT", "Budget der Kostenstelle reicht nicht");
        // dann: genau ein POST auf bpmnError des Tasks, im Body Worker-ID, Code und Grund
        var anfrage = Assert.Single(mitschnitt.Anfragen);
        Assert.Equal(HttpMethod.Post, anfrage.Methode);
        Assert.Equal("/engine-rest/external-task/t-5/bpmnError", anfrage.Pfad);
        using var body = JsonDocument.Parse(anfrage.Body);
        Assert.Equal("genehmigung-worker-1", body.RootElement.GetProperty("workerId").GetString());
        Assert.Equal("BUCHUNG_ABGELEHNT", body.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal("Budget der Kostenstelle reicht nicht", body.RootElement.GetProperty("errorMessage").GetString());
    }

    /// <summary>Schneidet jede Anfrage mit und antwortet wie die Engine auf bpmnError: 204 No Content</summary>
    private sealed class Mitschnitt : HttpMessageHandler
    {
        public List<Anfrage> Anfragen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage anfrage, CancellationToken abbruch)
        {
            var body = anfrage.Content is null ? "" : await anfrage.Content.ReadAsStringAsync(abbruch);
            Anfragen.Add(new Anfrage(anfrage.Method, anfrage.RequestUri!.AbsolutePath, body));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    /// <summary>Eine mitgeschnittene Anfrage: Methode, Pfad ohne Host, Body als Text</summary>
    private sealed record Anfrage(HttpMethod Methode, string Pfad, string Body);
}
