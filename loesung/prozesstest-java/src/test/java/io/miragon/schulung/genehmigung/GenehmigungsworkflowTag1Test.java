package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.complete;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.execute;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.job;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.runtimeService;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.task;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.withVariables;

import java.time.Duration;
import java.time.Instant;
import java.util.Date;

import org.assertj.core.api.Assertions;
import org.cibseven.bpm.engine.runtime.Job;
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
 * Prozesstest für die Demo in Kapitel 10: das fertige Modell nach Übung 7, nur mit User Tasks.
 * "Tag1" im Namen steht für dieses Modell ohne External Task. Engine und Datenbank im Speicher, pro Test frisch deployt.
 * Der Test spielt alle Beteiligten selbst: Antragsteller:in und genehmigende Stelle. Jede Aufgabe schließt er selbst ab.
 * Die Tests für das Modell mit External Task (Übung 9) stehen in GenehmigungsworkflowTest.
 *
 * Die Prüfungen beantworten drei Fragen an die Engine:
 * Wartezustand (isWaitingAt), Pfad (hasPassed, hasNotPassed), Variablen (variables).
 * Beim Timer kommt seine Fälligkeit dazu.
 * Der Test findet Elemente über die ID aus dem Modell, nie über die Beschriftung.
 */
@ExtendWith(ProcessEngineExtension.class)
@Deployment(resources = "genehmigungsworkflow-tag1.bpmn")
@TestMethodOrder(MethodOrderer.OrderAnnotation.class)
@DisplayName("Genehmigungsworkflow, Modell ohne External Task")
class GenehmigungsworkflowTag1Test {

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

        // 4. Verbuchen: "Genehmigung verbuchen" wartet als Aufgabe, "entscheidung" steht auf genehmigt (Variablen),
        //    dann abschließen wie ein Mensch in der Aufgabenliste
        assertThat(antrag).isWaitingAtExactly("Task_Verbuchen")
            .variables().containsEntry("entscheidung", "genehmigt");
        complete(task());

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

        // Ablehnung: "Ablehnung mitteilen" wartet als Aufgabe, erst danach ist Schluss
        assertThat(antrag).isWaitingAtExactly("Task_Ablehnen");
        complete(task());
        assertThat(antrag).isEnded()
            .hasPassed("Task_Ablehnen", "End_Abgelehnt")
            .hasNotPassed("Task_Verbuchen", "End_Genehmigt");
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
        speicherpunktAnstossen(antrag, "Task_Nachbessern");

        // Wieder bei "Antrag prüfen", die Aufgabe liegt wieder bei der genehmigenden Stelle
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen")
            .hasPassed("Task_Nachbessern")
            .hasNotPassed("Task_Verbuchen", "Task_Ablehnen");
        assertThat(antrag).task().hasCandidateGroup("genehmiger");
    }

    @Test
    @Order(4)
    @DisplayName("Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen")
    void timerSendetErinnerung() {
        ProcessInstance antrag = antragStarten();
        speicherpunktAnstossen(antrag, "StartEvent_Antrag");
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen");

        // Fällig in drei Minuten (im Modell PT3M, fachlich 3 Tage). Nicht warten: den Timer-Job gezielt ausführen
        Job timer = job("Boundary_Timer", antrag);
        Date inDreiMinuten = Date.from(Instant.now().plus(Duration.ofMinutes(3)));
        Assertions.assertThat(timer.getDuedate()).as("Fälligkeit des Timers")
            .isCloseTo(inDreiMinuten, Duration.ofSeconds(10).toMillis());
        execute(timer);

        // Timer: nicht unterbrechend, danach sind zwei Aufgaben offen
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen", "Task_Erinnern");
        complete(task("Task_Erinnern", antrag));

        // Die Erinnerung ist erledigt, "Antrag prüfen" wartet weiter und wurde nie beendet
        assertThat(antrag).isWaitingAtExactly("Task_Pruefen")
            .hasPassed("Task_Erinnern", "End_Erinnert")
            .hasNotPassed("Task_Pruefen");
    }

    /**
     * Startet wie das Startformular. antragsteller legt im laufenden System das Start-Event ab
     * (die angemeldete Person). Im Test ist niemand angemeldet, deshalb setzt ihn der Test selbst.
     * Der Business Key "Antrag-1" macht die Instanz in den Meldungen erkennbar.
     */
    private static ProcessInstance antragStarten() {
        return runtimeService().startProcessInstanceByKey("Process_Genehmigung", "Antrag-1", withVariables(
            "betrag", 1200,
            "begruendung", "Dienstreise",
            "antragsteller", ANTRAGSTELLER));
    }

    /**
     * Prüft, dass die Instanz genau an diesem Speicherpunkt steht (Wartezustand), und stößt ihn dann an.
     * Der Speicherpunkt (asyncAfter) liegt hinter dem Element. Das Anstoßen erledigt im laufenden System
     * der Job Executor, die Engine im Speicher hat keinen.
     */
    private static void speicherpunktAnstossen(ProcessInstance antrag, String hinterElement) {
        assertThat(antrag).isWaitingAtExactly(hinterElement);
        execute(job(hinterElement, antrag));
    }
}
