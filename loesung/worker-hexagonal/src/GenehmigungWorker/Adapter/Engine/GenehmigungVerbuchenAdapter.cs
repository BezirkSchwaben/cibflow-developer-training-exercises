// Außen am Sechseck, eingehender (treibender) Adapter: Er treibt den Kern.
// Die Engine ist kein Adapter, sie ist das externe System. Dieser Adapter steht zwischen ihr und dem Kern
// und übersetzt an der Grenze: Variablen des External Tasks rein, Genehmigung zum Kern, Buchungsnummer
// als Variable zurück. Nur hier stehen die Variablennamen des Modells (antragsteller, betrag, begruendung,
// buchungsnummer). In loesung/worker macht das der Handler, dort zusammen mit dem Aufruf des Fachsystems.
using System.Globalization;
using GenehmigungWorker.Domaene;
using GenehmigungWorker.Domaene.Ports;

namespace GenehmigungWorker.Adapter.Engine;

/// <summary>
/// Adapter für das Topic genehmigung-verbuchen: Variablen lesen, den Use Case über seinen Port rufen,
/// das Ergebnis als Variablen zurückgeben. Mit der Engine spricht er nicht, complete, failure und bpmnError
/// schickt ExternalTaskWorker.
/// </summary>
/// <param name="useCase">eingehender Port des Kerns, im Worker der Use Case GenehmigungVerbuchen</param>
public class GenehmigungVerbuchenAdapter(IGenehmigungVerbuchen useCase)
{
    public Dictionary<string, object> Handle(ExternalTask task)
    {
        // Übersetzen: Aus den Variablen der Engine wird ein Domänenobjekt.
        // Idempotenz-Schlüssel wie in loesung/worker: Business Key, wenn die Instanz einen hat, sonst die
        // Prozessinstanz-ID. Über das Startformular gestartet ist BusinessKey meist null.
        // Fehlt eine Variable, scheitert der Adapter laut mit KeyNotFoundException, die Schleife meldet failure.
        // Danach den Kern rufen, nur über den eingehenden Port, und das Ergebnis zurückübersetzen:
        // buchungsnummer ist Teil des Vertrags mit dem Modell.
        var genehmigung = new Genehmigung(
            Schluessel: task.BusinessKey ?? task.ProcessInstanceId,
            Antragsteller: (string) task.Variables["antragsteller"],
            Betrag: BetragLesen(task.Variables["betrag"]),
            Begruendung: (string) task.Variables["begruendung"]);
        var nummer = useCase.Verbuchen(genehmigung);
        return new() { ["buchungsnummer"] = nummer };
    }

    // betrag kommt als Text aus dem easyForm ("1234.5", immer mit Punkt)
    // oder als Zahl, etwa beim Start per REST. Beides kulturunabhängig lesen,
    // sonst wird auf einem deutschen Rechner aus "1234.5" 12345.
    private static decimal BetragLesen(object betrag) => betrag switch
    {
        string text => decimal.Parse(text, CultureInfo.InvariantCulture),
        var zahl => Convert.ToDecimal(zahl, CultureInfo.InvariantCulture),
    };
}
