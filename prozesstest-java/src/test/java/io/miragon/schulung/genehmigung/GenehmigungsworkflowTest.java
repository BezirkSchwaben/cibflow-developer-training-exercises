package io.miragon.schulung.genehmigung;

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

import org.assertj.core.api.Assertions;
import org.cibseven.bpm.engine.runtime.Job;
import org.cibseven.bpm.engine.runtime.ProcessInstance;
import org.cibseven.bpm.engine.test.Deployment;
import org.cibseven.bpm.engine.test.junit5.ProcessEngineExtension;
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

    // TODO Kapitel 12, Schritt 5: Die drei Testfälle unten schreibt ihr, Vorbild ist der Happy Path oben.
    // Die Hilfsmethoden antragStarten und speicherpunktAnstossen stehen am Ende der Klasse,
    // die Imports für alle drei Tests stehen schon oben. Die Kommentare in jeder Methode sagen,
    // was ihr startet, wo die Instanz wartet und was ihr prüft.
    // Ist ein Test fertig, löscht ihr seine Zeile @Disabled. Solange sie dasteht, überspringt JUnit den Test.
    // Löscht sie nicht vorher: Eine leere Methode ohne @Disabled läuft grün durch und belegt nichts.

    @Test
    @Order(2)
    @Disabled("TODO Kapitel 12, Schritt 5")
    @DisplayName("Ablehnung: „Ablehnung mitteilen“, nie verbuchen")
    void abgelehnterAntragWirdMitgeteilt() {
        // Starten:     antragStarten(), dann den Speicherpunkt hinter "StartEvent_Antrag" anstoßen
        // Warten:      genau bei "Task_Pruefen"
        // Entscheiden: die Aufgabe mit entscheidung = "abgelehnt" abschließen,
        //              danach den Speicherpunkt hinter "Task_Pruefen" anstoßen
        // Warten:      genau bei "Task_Ablehnen": "Ablehnung mitteilen" ist eine Aufgabe, kein External Task
        // Mitteilen:   die Aufgabe ohne Variablen abschließen, complete(task())
        // Prüfen:      erst jetzt ist die Instanz beendet (isEnded)
        //              Pfad: "Task_Ablehnen" und "End_Abgelehnt" durchlaufen (hasPassed),
        //                    "Task_Verbuchen" und "End_Genehmigt" nicht (hasNotPassed)
        //              Variablen: buchungsnummer gibt es nicht (doesNotContainKey)
    }

    @Test
    @Order(3)
    @Disabled("TODO Kapitel 12, Schritt 5")
    @DisplayName("Nachbesserung: zurück an die Antragsteller:in, danach wieder „Antrag prüfen“")
    void nachbesserungFuehrtZurueckZurPruefung() {
        // Starten:     wie oben, bis die Instanz genau bei "Task_Pruefen" wartet
        // Entscheiden: die Aufgabe mit entscheidung = "nachbessern" abschließen,
        //              danach den Speicherpunkt hinter "Task_Pruefen" anstoßen
        // Warten:      genau bei "Task_Nachbessern", die Aufgabe gehört der Antragsteller:in
        //              (assertThat(antrag).task().isAssignedTo(ANTRAGSTELLER))
        // Nachbessern: die Aufgabe ohne Variablen abschließen, complete(task()),
        //              danach den Speicherpunkt hinter "Task_Nachbessern" anstoßen: Seit Übung 7 hängt dort ein easyForm
        // Prüfen:      wieder genau bei "Task_Pruefen"
        //              Pfad: "Task_Nachbessern" durchlaufen, "Task_Verbuchen" und "Task_Ablehnen" nicht
        //              Aufgabe: "Antrag prüfen" ist wieder offen bei der Gruppe genehmiger
        //              (assertThat(antrag).task().hasCandidateGroup("genehmiger"), wie im Happy Path)
    }

    @Test
    @Order(4)
    @Disabled("TODO Kapitel 12, Schritt 5, für alle, die schneller sind")
    @DisplayName("Timer nach 3 Tagen: „Erinnerung senden“, Aufgabe bleibt offen")
    void timerSendetErinnerung() {
        // Starten:     wie oben, bis die Instanz genau bei "Task_Pruefen" wartet. Die Aufgabe bleibt offen.
        // Timer holen: Job timer = job("Boundary_Timer", antrag);
        // Fälligkeit:  timer.getDuedate() liegt drei Tage in der Zukunft, auf eine Minute genau:
        //              Date inDreiTagen = Date.from(Instant.now().plus(Duration.ofDays(3)));
        //              Assertions.assertThat(timer.getDuedate()).isCloseTo(inDreiTagen, Duration.ofMinutes(1).toMillis());
        //              Hier mit "Assertions.": Das importierte assertThat kennt kein Datum.
        // Auslösen:    execute(timer), statt drei Tage zu warten
        // Warten:      jetzt an zwei Stellen, isWaitingAtExactly("Task_Pruefen", "Task_Erinnern"):
        //              Der Timer unterbricht nicht, "Erinnerung senden" kommt als zweite Aufgabe dazu
        // Erinnern:    complete(task("Task_Erinnern", antrag)). task() ohne ID scheitert bei zwei offenen Aufgaben
        // Prüfen:      wieder genau bei "Task_Pruefen"
        //              Pfad: "Task_Erinnern" und "End_Erinnert" durchlaufen, "Task_Pruefen" nie beendet (hasNotPassed)
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
