package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.complete;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.execute;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.externalTask;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.job;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.runtimeService;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.task;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.withVariables;

import org.cibseven.bpm.engine.runtime.ProcessInstance;
import org.cibseven.bpm.engine.test.Deployment;
import org.cibseven.bpm.engine.test.junit5.ProcessEngineExtension;
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
 * Jede Prüfung beantwortet eine von drei Fragen an die Engine:
 * Wartezustand (isWaitingAt), Pfad (hasPassed, hasNotPassed), Variablen (variables).
 * Der Test findet Elemente über die ID aus dem Modell, nie über die Beschriftung.
 */
@ExtendWith(ProcessEngineExtension.class)
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
        speicherpunktAnstossen(antrag, "StartEvent_Antrag");
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen");
        assertThat(antrag).task().hasCandidateGroup("genehmiger");

        // 3. Entscheiden: wie die genehmigende Stelle, danach entscheidet erst das Gateway
        complete(task(), withVariables("entscheidung", "genehmigt"));
        speicherpunktAnstossen(antrag, "Task_Pruefen");

        // 4. Verbuchen: External Task erreicht, "entscheidung" steht auf genehmigt (Variablen),
        //    dann abschließen wie der Worker
        assertThat(antrag).isWaitingAtExactly("Task_Verbuchen")
            .variables().containsEntry("entscheidung", "genehmigt");
        assertThat(antrag).externalTask().hasTopicName("genehmigung-verbuchen");
        complete(externalTask(), withVariables("buchungsnummer", "B-2026-0001"));

        // 5. Beenden: Ende bei "Antrag genehmigt", der Pfad "abgelehnt" blieb unberührt (Pfad)
        assertThat(antrag).isEnded()
            .hasPassed("Task_Pruefen", "Gateway_Entscheidung", "Task_Verbuchen", "End_Genehmigt")
            .hasNotPassed("Task_Ablehnen", "End_Abgelehnt");
    }

    @Test
    @Order(2)
    @DisplayName("Ablehnung: „Ablehnung mitteilen“, nie verbuchen")
    void abgelehnterAntragWirdMitgeteilt() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, "StartEvent_Antrag");
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen");

        complete(task(), withVariables("entscheidung", "abgelehnt"));
        speicherpunktAnstossen(antrag, "Task_Pruefen");

        // Pfad: "abgelehnt" ja, "genehmigt" nein
        assertThat(antrag).isEnded()
            .hasPassed("Task_Ablehnen", "End_Abgelehnt")
            .hasNotPassed("Task_Verbuchen", "End_Genehmigt");
        // Variablen: der Platzhalter an "Ablehnung mitteilen" lief, verbucht wurde nichts
        assertThat(antrag).variables()
            .containsEntry("ablehnungMitgeteilt", true)
            .doesNotContainKey("buchungsnummer");
    }

    @Test
    @Order(3)
    @DisplayName("Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“")
    void nachbesserungFuehrtZurueckZurPruefung() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, "StartEvent_Antrag");
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen");

        complete(task(), withVariables("entscheidung", "nachbessern"));
        speicherpunktAnstossen(antrag, "Task_Pruefen");

        // "Antrag nachbessern" geht an ${antragsteller}
        assertThat(antrag).isWaitingAtExactly("Task_Nachbessern");
        assertThat(antrag).task().isAssignedTo(ANTRAGSTELLER);

        complete(task());

        assertThat(antrag).isWaitingAtExactly("Task_Pruefen")
            .hasPassed("Task_Nachbessern")
            .hasNotPassed("Task_Verbuchen", "Task_Ablehnen");
    }

    @Test
    @Order(4)
    @DisplayName("Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen")
    void timerSendetErinnerung() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, "StartEvent_Antrag");
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen");

        // Nicht drei Tage warten: den Timer-Job gezielt ausführen
        execute(job("Boundary_Timer", antrag));

        // Non-interrupting: Die Erinnerung lief, "Antrag prüfen" wartet weiter und wurde nie beendet
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen")
            .hasPassed("Task_Erinnern", "End_Erinnert")
            .hasNotPassed("Task_Pruefen")
            .variables().containsEntry("erinnerungGesendet", true);
    }

    /**
     * Startet wie das Startformular. antragsteller legt im laufenden System das Start-Event ab
     * (die angemeldete Person). Im Test ist niemand angemeldet, deshalb setzt ihn der Test selbst.
     */
    private static ProcessInstance antragStarten() {
        return runtimeService().startProcessInstanceByKey("Process_Genehmigung", withVariables(
            "betrag", 1200,
            "begruendung", "Dienstreise",
            "antragsteller", ANTRAGSTELLER));
    }

    /**
     * Stößt den Speicherpunkt (asyncAfter) hinter einem Element an. Im laufenden System erledigt das
     * der Job Executor, die Engine im Speicher hat keinen.
     */
    private static void speicherpunktAnstossen(ProcessInstance antrag, String hinterElement) {
        assertThat(antrag).isWaitingAtExactly(hinterElement);
        execute(job(hinterElement, antrag));
    }
}
