package io.miragon.schulung.genehmigung;

import static io.miragon.schulung.genehmigung.api.ProcessGenehmigungProcessApi.Elements.*;
import static io.miragon.schulung.genehmigung.api.ProcessGenehmigungProcessApi.PROCESS_ID;
import static io.miragon.schulung.genehmigung.api.ProcessGenehmigungProcessApi.ServiceTasks.GENEHMIGUNG_VERBUCHEN;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.complete;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.execute;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.externalTask;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.job;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.runtimeService;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.task;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.withVariables;

import java.time.Duration;
import java.time.Instant;
import java.util.Date;

import io.miragon.bpmn.runtime.ElementId;
import org.assertj.core.api.Assertions;
import org.cibseven.bpm.engine.runtime.Job;
import org.cibseven.bpm.engine.runtime.ProcessInstance;
import org.cibseven.bpm.engine.test.Deployment;
import org.cibseven.community.process_test_coverage.junit5.platform7.ProcessEngineCoverageExtension;
import org.junit.jupiter.api.Disabled;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.MethodOrderer;
import org.junit.jupiter.api.Order;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.TestMethodOrder;
import org.junit.jupiter.api.extension.ExtendWith;

/**
 * Prozesstest für den Genehmigungsworkflow: Engine und Datenbank im Speicher, pro Test frisch deployt.
 * Der Test spielt alle Beteiligten selbst: Antragsteller:in, genehmigende Stelle, Worker.
 *
 * Die Prüfungen beantworten drei Fragen an die Engine:
 * Wartezustand (isWaitingAt), Pfad (hasPassed, hasNotPassed), Variablen (variables).
 * Beim Timer kommt seine Fälligkeit dazu.
 * Der Test findet Elemente über die ID aus dem Modell, nie über die Beschriftung.
 *
 * IDs, Process ID und Topic stehen nicht als Text im Test. Sie kommen aus ProcessGenehmigungProcessApi, die
 * bpmn-to-code bei jedem Lauf aus src/main/resources/genehmigungsworkflow.bpmn erzeugt, abgelegt unter
 * target/generated-test-sources/bpmn-to-code. Aus der ID Task_Pruefen wird die Konstante TASK_PRUEFEN.
 * Die Prüfungen der Engine erwarten die ID als Text, deshalb TASK_PRUEFEN.getValue().
 * Ändert jemand eine ID im Modell, übersetzt der Test nicht mehr, und Maven nennt jede Zeile mit der alten ID.
 */
@ExtendWith(ProcessEngineCoverageExtension.class)
@Deployment(resources = "genehmigungsworkflow.bpmn")
@TestMethodOrder(MethodOrderer.OrderAnnotation.class)
@DisplayName("Genehmigungsworkflow")
class GenehmigungsworkflowTest {

    private static final String ANTRAGSTELLER = "anna";

    @Test
    @Order(1)
    @DisplayName("Happy Path: Antrag genehmigt und verbucht")
    void genehmigterAntragWirdVerbucht() {
        // 1. Starten: wie das Startformular, dazu antragsteller
        ProcessInstance antrag = antragStarten();

        // 2. Warten: nach dem Speicherpunkt bei "Antrag prüfen" und nirgends sonst (Wartezustand)
        speicherpunktAnstossen(antrag, START_EVENT_ANTRAG);
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue());
        assertThat(antrag).task().hasCandidateGroup("genehmiger");

        // 3. Entscheiden: wie die genehmigende Stelle, danach entscheidet erst das Gateway
        complete(task(), withVariables("entscheidung", "genehmigt"));
        speicherpunktAnstossen(antrag, TASK_PRUEFEN);

        // 4. Verbuchen: External Task erreicht, "entscheidung" steht auf genehmigt (Variablen),
        //    dann abschließen wie der Worker
        assertThat(antrag).isWaitingAtExactly(TASK_VERBUCHEN.getValue())
            .variables().containsEntry("entscheidung", "genehmigt");
        assertThat(antrag).externalTask().hasTopicName(GENEHMIGUNG_VERBUCHEN);
        complete(externalTask(), withVariables("buchungsnummer", "B-2026-0001"));

        // 5. Beenden: Ende bei "Antrag genehmigt", der Pfad "abgelehnt" blieb unberührt (Pfad)
        assertThat(antrag).isEnded()
            .hasPassed(TASK_PRUEFEN.getValue(), GATEWAY_ENTSCHEIDUNG.getValue(), TASK_VERBUCHEN.getValue(), END_GENEHMIGT.getValue())
            .hasNotPassed(TASK_ABLEHNEN.getValue(), END_ABGELEHNT.getValue());
    }

    // TODO Kapitel 12, Schritt 5: Die drei Testfälle unten schreibt ihr, Vorbild ist der Happy Path oben.
    // Die Hilfsmethoden antragStarten und speicherpunktAnstossen stehen am Ende der Klasse,
    // die Imports für alle drei Tests stehen schon oben, mit Elements.* alle IDs des Modells.
    // Die Kommentare in jeder Methode sagen, was ihr startet, wo die Instanz wartet und was ihr prüft.
    // IDs schreibt ihr wie im Happy Path als Konstante aus dem Modell, nie als Text: in den Prüfungen mit .getValue(),
    // etwa isWaitingAtExactly(TASK_ABLEHNEN.getValue()), in speicherpunktAnstossen ohne, etwa (antrag, TASK_PRUEFEN).
    // Ist ein Test fertig, löscht ihr seine Zeile @Disabled. Solange sie dasteht, überspringt JUnit den Test.
    // Löscht sie nicht vorher: Eine leere Methode ohne @Disabled läuft grün durch und belegt nichts.

    @Test
    @Order(2)
    @Disabled("TODO Kapitel 12, Schritt 5")
    @DisplayName("Ablehnung: „Ablehnung mitteilen“, nie verbuchen")
    void abgelehnterAntragWirdMitgeteilt() {
        // Starten:     antragStarten(), dann den Speicherpunkt hinter START_EVENT_ANTRAG anstoßen
        // Warten:      genau bei TASK_PRUEFEN
        // Entscheiden: die Aufgabe mit entscheidung = "abgelehnt" abschließen,
        //              danach den Speicherpunkt hinter TASK_PRUEFEN anstoßen
        // Warten:      genau bei TASK_ABLEHNEN: "Ablehnung mitteilen" ist eine Aufgabe, kein External Task
        // Mitteilen:   die Aufgabe ohne Variablen abschließen, complete(task())
        // Prüfen:      erst jetzt ist die Instanz beendet (isEnded)
        //              Pfad: TASK_ABLEHNEN und END_ABGELEHNT durchlaufen (hasPassed),
        //                    TASK_VERBUCHEN und END_GENEHMIGT nicht (hasNotPassed)
        //              Variablen: buchungsnummer gibt es nicht (doesNotContainKey)
    }

    @Test
    @Order(3)
    @Disabled("TODO Kapitel 12, Schritt 5")
    @DisplayName("Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“")
    void nachbesserungFuehrtZurueckZurPruefung() {
        // Starten:     wie oben, bis die Instanz genau bei TASK_PRUEFEN wartet
        // Entscheiden: die Aufgabe mit entscheidung = "nachbessern" abschließen,
        //              danach den Speicherpunkt hinter TASK_PRUEFEN anstoßen
        // Warten:      genau bei TASK_NACHBESSERN, die Aufgabe gehört der Antragsteller:in
        //              (assertThat(antrag).task().isAssignedTo(ANTRAGSTELLER))
        // Nachbessern: die Aufgabe ohne Variablen abschließen, complete(task()),
        //              danach den Speicherpunkt hinter TASK_NACHBESSERN anstoßen: Seit Übung 7 hängt dort ein easyForm
        // Prüfen:      wieder genau bei TASK_PRUEFEN
        //              Pfad: TASK_NACHBESSERN durchlaufen, TASK_VERBUCHEN und TASK_ABLEHNEN nicht
        //              Aufgabe: "Antrag prüfen" ist wieder offen bei der Gruppe genehmiger
        //              (assertThat(antrag).task().hasCandidateGroup("genehmiger"), wie im Happy Path)
    }

    @Test
    @Order(4)
    @Disabled("TODO Kapitel 12, Schritt 5, für alle, die schneller sind")
    @DisplayName("Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen")
    void timerSendetErinnerung() {
        // Starten:     wie oben, bis die Instanz genau bei TASK_PRUEFEN wartet. Die Aufgabe bleibt offen.
        // Timer holen: Job timer = job(BOUNDARY_TIMER.getValue(), antrag);
        // Fälligkeit:  timer.getDuedate() liegt drei Minuten in der Zukunft (im Modell PT3M, fachlich 3 Tage),
        //              auf zehn Sekunden genau:
        //              Date inDreiMinuten = Date.from(Instant.now().plus(Duration.ofMinutes(3)));
        //              Assertions.assertThat(timer.getDuedate()).isCloseTo(inDreiMinuten, Duration.ofSeconds(10).toMillis());
        //              Hier mit "Assertions.": Das importierte assertThat kennt kein Datum.
        // Auslösen:    execute(timer), statt zu warten
        // Warten:      jetzt an zwei Stellen, isWaitingAtExactly(TASK_PRUEFEN.getValue(), TASK_ERINNERN.getValue()):
        //              Der Timer unterbricht nicht, "Erinnerung senden" kommt als zweite Aufgabe dazu
        // Erinnern:    complete(task(TASK_ERINNERN.getValue(), antrag)). task() ohne ID scheitert bei zwei offenen Aufgaben
        // Prüfen:      wieder genau bei TASK_PRUEFEN
        //              Pfad: TASK_ERINNERN und END_ERINNERT durchlaufen, TASK_PRUEFEN nie beendet (hasNotPassed)
    }

    /**
     * Startet wie das Startformular. antragsteller legt im laufenden System das Start-Event ab
     * (die angemeldete Person). Im Test ist niemand angemeldet, deshalb setzt ihn der Test selbst.
     * Der Business Key "Antrag-1" macht die Instanz in den Meldungen erkennbar.
     */
    private static ProcessInstance antragStarten() {
        return runtimeService().startProcessInstanceByKey(PROCESS_ID.getValue(), "Antrag-1", withVariables(
            "betrag", 1200,
            "begruendung", "Dienstreise",
            "antragsteller", ANTRAGSTELLER));
    }

    /**
     * Prüft, dass die Instanz genau an diesem Speicherpunkt steht (Wartezustand), und stößt ihn dann an.
     * Der Speicherpunkt (asyncAfter) liegt hinter dem Element. Das Anstoßen erledigt im laufenden System
     * der Job Executor, die Engine im Speicher hat keinen. Das Element kommt als Konstante, etwa TASK_PRUEFEN.
     */
    private static void speicherpunktAnstossen(ProcessInstance antrag, ElementId hinterElement) {
        assertThat(antrag).isWaitingAtExactly(hinterElement.getValue());
        execute(job(hinterElement.getValue(), antrag));
    }
}
