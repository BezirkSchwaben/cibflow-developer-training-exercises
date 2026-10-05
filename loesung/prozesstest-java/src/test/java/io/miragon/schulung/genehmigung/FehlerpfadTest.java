package io.miragon.schulung.genehmigung;

import static io.miragon.schulung.genehmigung.api.fehlerpfad.ProcessVerbuchenFehlerpfadProcessApi.Elements.*;
import static io.miragon.schulung.genehmigung.api.fehlerpfad.ProcessVerbuchenFehlerpfadProcessApi.Errors.BUCHUNG_ABGELEHNT;
import static io.miragon.schulung.genehmigung.api.fehlerpfad.ProcessVerbuchenFehlerpfadProcessApi.PROCESS_ID;
import static io.miragon.schulung.genehmigung.api.fehlerpfad.ProcessVerbuchenFehlerpfadProcessApi.ServiceTasks.GENEHMIGUNG_VERBUCHEN;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.complete;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.externalTaskService;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.fetchAndLock;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.runtimeService;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.task;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.withVariables;

import java.util.List;

import org.assertj.core.api.Assertions;
import org.cibseven.bpm.engine.externaltask.LockedExternalTask;
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
 * Bonus zu Kapitel 12, Schritt 5: Prozesstest für die Variante prozess/varianten/verbuchen-fehlerpfad.bpmn,
 * den Ausschnitt ab "Genehmigung erteilt" mit dem Error-Boundary "Buchung abgelehnt".
 *
 * Der Test spielt den Worker: Er holt den External Task wie der Worker (fetchAndLock) und antwortet
 * einmal mit dem BPMN-Fehler BUCHUNG_ABGELEHNT und einmal, als Gegenprobe, mit complete.
 * Die Kopie des Modells in src/main/resources prüft die CI auf Byte-Gleichheit mit der Variante.
 *
 * IDs, Process ID, Topic und Fehlercode kommen aus ProcessVerbuchenFehlerpfadProcessApi, die bpmn-to-code bei jedem
 * Lauf aus src/main/resources/verbuchen-fehlerpfad.bpmn erzeugt: BUCHUNG_ABGELEHNT.getCode() ist der errorCode
 * des Modells, TASK_BUCHUNG_KLAEREN.getValue() die ID von "Buchung klären".
 */
@ExtendWith(ProcessEngineExtension.class)
@Deployment(resources = "verbuchen-fehlerpfad.bpmn")
@TestMethodOrder(MethodOrderer.OrderAnnotation.class)
@DisplayName("Fehlerpfad (Variante)")
class FehlerpfadTest {

    private static final String WORKER_ID = "prozesstest";
    private static final String GRUND =
        "Budget der Kostenstelle reicht nicht: 60.000,00 Euro beantragt, 50.000,00 Euro frei";

    @Test
    @Order(1)
    @DisplayName("Buchung abgelehnt: BPMN-Fehler BUCHUNG_ABGELEHNT, danach wartet „Buchung klären“")
    void abgelehnteBuchungFuehrtZurKlaerung() {
        ProcessInstance genehmigung = genehmigungStarten(60000);
        assertThat(genehmigung).isWaitingAtExactly(TASK_VERBUCHEN.getValue());
        assertThat(genehmigung).externalTask().hasTopicName(GENEHMIGUNG_VERBUCHEN);

        // Wie der Worker bei einer fachlichen Ablehnung: Task holen, dann bpmnError statt complete
        LockedExternalTask verbuchen = taskHolenWieDerWorker();
        externalTaskService().handleBpmnError(verbuchen.getId(), WORKER_ID, BUCHUNG_ABGELEHNT.getCode(), GRUND);

        // Das Error-Boundary fängt den Code, legt Code und Grund ab und führt zu "Buchung klären"
        assertThat(genehmigung).isWaitingAtExactly(TASK_BUCHUNG_KLAEREN.getValue())
            .hasPassed(BOUNDARY_BUCHUNG_ABGELEHNT.getValue())
            .hasNotPassed(TASK_GENEHMIGUNG_MITTEILEN.getValue(), END_GENEHMIGT.getValue())
            .variables()
                .containsEntry("errorCode", BUCHUNG_ABGELEHNT.getCode())
                .containsEntry("errorMessage", GRUND)
                .doesNotContainKey("buchungsnummer");
        assertThat(genehmigung).task().hasCandidateGroup("genehmiger");

        // Geklärt: Die genehmigende Stelle schließt die Aufgabe ab, die Instanz endet bei "Buchung geklärt"
        complete(task());
        assertThat(genehmigung).isEnded()
            .hasPassed(TASK_BUCHUNG_KLAEREN.getValue(), END_BUCHUNG_GEKLAERT.getValue());
    }

    @Test
    @Order(2)
    @DisplayName("Gegenprobe: verbucht, „Genehmigung mitteilen“, Ende bei „Antrag genehmigt“")
    void verbuchteGenehmigungWirdMitgeteilt() {
        ProcessInstance genehmigung = genehmigungStarten(1200);
        assertThat(genehmigung).isWaitingAtExactly(TASK_VERBUCHEN.getValue());

        // Wie der Worker nach einer erfolgreichen Buchung: Task holen, complete mit buchungsnummer
        LockedExternalTask verbuchen = taskHolenWieDerWorker();
        complete(verbuchen, withVariables("buchungsnummer", "B-2026-0001"));

        assertThat(genehmigung).isEnded()
            .hasPassed(TASK_VERBUCHEN.getValue(), TASK_GENEHMIGUNG_MITTEILEN.getValue(), END_GENEHMIGT.getValue())
            .hasNotPassed(TASK_BUCHUNG_KLAEREN.getValue(), END_BUCHUNG_GEKLAERT.getValue())
            .variables()
                .containsEntry("buchungsnummer", "B-2026-0001")
                .containsEntry("genehmigungMitgeteilt", true)
                .doesNotContainKey("errorCode");
    }

    /**
     * Startet die Variante wie per REST im Bonus "Fachlicher Fehler": Sie hat weder Startformular noch
     * Initiator, deshalb gibt der Test antragsteller, betrag und begruendung selbst mit.
     */
    private static ProcessInstance genehmigungStarten(long betrag) {
        return runtimeService().startProcessInstanceByKey(PROCESS_ID.getValue(), "Genehmigung-1", withVariables(
            "antragsteller", "anna",
            "betrag", betrag,
            "begruendung", "Neue Serverhardware"));
    }

    /**
     * Holt den External Task wie der Worker: fetchAndLock auf das Topic unter einer eigenen Worker-ID.
     * Nur wer den Task gesperrt hat, darf ihn mit complete oder bpmnError beantworten.
     */
    private static LockedExternalTask taskHolenWieDerWorker() {
        List<LockedExternalTask> tasks = fetchAndLock(GENEHMIGUNG_VERBUCHEN, WORKER_ID, 1);
        Assertions.assertThat(tasks).as("External Tasks auf dem Topic " + GENEHMIGUNG_VERBUCHEN).hasSize(1);
        return tasks.get(0);
    }
}
