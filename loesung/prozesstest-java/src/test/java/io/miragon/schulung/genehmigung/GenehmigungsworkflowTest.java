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

    @Test
    @Order(2)
    @DisplayName("Ablehnung: „Ablehnung mitteilen“, nie verbuchen")
    void abgelehnterAntragWirdMitgeteilt() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, START_EVENT_ANTRAG);
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue());

        complete(task(), withVariables("entscheidung", "abgelehnt"));
        speicherpunktAnstossen(antrag, TASK_PRUEFEN);

        // Ablehnung: "Ablehnung mitteilen" wartet als Aufgabe, erst danach ist Schluss
        assertThat(antrag).isWaitingAtExactly(TASK_ABLEHNEN.getValue());
        complete(task());
        assertThat(antrag).isEnded()
            .hasPassed(TASK_ABLEHNEN.getValue(), END_ABGELEHNT.getValue())
            .hasNotPassed(TASK_VERBUCHEN.getValue(), END_GENEHMIGT.getValue())
            .variables().doesNotContainKey("buchungsnummer");
    }

    @Test
    @Order(3)
    @DisplayName("Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“")
    void nachbesserungFuehrtZurueckZurPruefung() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, START_EVENT_ANTRAG);
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue());

        complete(task(), withVariables("entscheidung", "nachbessern"));
        speicherpunktAnstossen(antrag, TASK_PRUEFEN);

        // "Antrag nachbessern" geht an ${antragsteller}
        assertThat(antrag).isWaitingAtExactly(TASK_NACHBESSERN.getValue());
        assertThat(antrag).task().isAssignedTo(ANTRAGSTELLER);

        complete(task());
        speicherpunktAnstossen(antrag, TASK_NACHBESSERN);

        // Wieder bei "Antrag prüfen", die Aufgabe liegt wieder bei der genehmigenden Stelle
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue())
            .hasPassed(TASK_NACHBESSERN.getValue())
            .hasNotPassed(TASK_VERBUCHEN.getValue(), TASK_ABLEHNEN.getValue());
        assertThat(antrag).task().hasCandidateGroup("genehmiger");
    }

    @Test
    @Order(4)
    @DisplayName("Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen")
    void timerSendetErinnerung() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, START_EVENT_ANTRAG);
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue());

        // Fällig in drei Minuten (im Modell PT3M, fachlich 3 Tage). Nicht warten: den Timer-Job gezielt ausführen
        Job timer = job(BOUNDARY_TIMER.getValue(), antrag);
        Date inDreiMinuten = Date.from(Instant.now().plus(Duration.ofMinutes(3)));
        Assertions.assertThat(timer.getDuedate()).as("Fälligkeit des Timers")
            .isCloseTo(inDreiMinuten, Duration.ofSeconds(10).toMillis());
        execute(timer);

        // Timer: nicht unterbrechend, danach sind zwei Aufgaben offen
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue(), TASK_ERINNERN.getValue());
        complete(task(TASK_ERINNERN.getValue(), antrag));

        // Die Erinnerung ist erledigt, "Antrag prüfen" wartet weiter und wurde nie beendet
        assertThat(antrag).isWaitingAtExactly(TASK_PRUEFEN.getValue())
            .hasPassed(TASK_ERINNERN.getValue(), END_ERINNERT.getValue())
            .hasNotPassed(TASK_PRUEFEN.getValue());
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
