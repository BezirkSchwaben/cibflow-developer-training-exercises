package io.miragon.schulung.genehmigung;

import com.tngtech.jgiven.annotation.ScenarioState;
import com.tngtech.jgiven.junit5.lang.de.SzenarioTest;
import org.cibseven.bpm.engine.ProcessEngine;
import org.cibseven.bpm.engine.test.Deployment;
import org.cibseven.community.process_test_coverage.junit5.platform7.ProcessEngineCoverageExtension;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.DisplayNameGeneration;
import org.junit.jupiter.api.DisplayNameGenerator;
import org.junit.jupiter.api.MethodOrderer;
import org.junit.jupiter.api.Order;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.TestMethodOrder;
import org.junit.jupiter.api.extension.RegisterExtension;

/**
 * Ausblick in Kapitel 10, kein Teil einer Übung: der Prozesstest als Szenario mit JGiven.
 * Dieselben Fälle wie Happy Path und Ablehnung in GenehmigungsworkflowTag1Test (loesung/prozesstest-java),
 * am selben Modell ohne External Task (genehmigungsworkflow-tag1.bpmn), mit derselben Engine im Speicher.
 *
 * Wozu: Die Testmethode liest sich in der Sprache des Prozesses: Angenommen, Wenn, Dann. Was dahinter passiert
 * (starten, Aufgaben abschließen, Speicherpunkte anstoßen, prüfen), steht in den Stufen AngenommenStufe,
 * WennStufe und DannStufe. Aus den Unterstrichen im Methodennamen werden Leerzeichen, an jedes $ setzt JGiven
 * den Wert. Im Code steht die ID, im Bericht die Beschriftung aus dem Modell (siehe Beschriftung).
 *
 * Starten im Ordner loesung/prozesstest-jgiven mit ./mvnw test (Windows: .\mvnw.cmd test). Die Konsole zeigt
 * die Szenarien als Text über dem Testbaum. Danach baut ./mvnw jgiven:report den HTML-Bericht unter
 * target/jgiven-reports/html/index.html. Den Abdeckungsbericht schreibt schon der Testlauf, unter
 * target/process-test-coverage/io.miragon.schulung.genehmigung.GenehmigungsworkflowSzenarioTest/report.html.
 */
@Deployment(resources = "genehmigungsworkflow-tag1.bpmn")
@DisplayName("Genehmigungsworkflow als Szenario (JGiven)")
@DisplayNameGeneration(DisplayNameGenerator.ReplaceUnderscores.class)
@TestMethodOrder(MethodOrderer.OrderAnnotation.class)
class GenehmigungsworkflowSzenarioTest extends SzenarioTest<AngenommenStufe, WennStufe, DannStufe> {

    /**
     * Die Engine aus camunda.cfg.xml, mit Abdeckungsbericht. Hier als statisches Feld statt mit @ExtendWith wie in
     * loesung/prozesstest-java: JGiven liest das Feld camunda schon, bevor @ExtendWith die Engine dort eintrüge.
     * builder().build() baut die Engine sofort.
     */
    @RegisterExtension
    static ProcessEngineCoverageExtension engine = ProcessEngineCoverageExtension.builder().build();

    /** ProcessStage erwartet die Engine unter diesem Namen (@ExpectedScenarioState camunda). */
    @ScenarioState
    ProcessEngine camunda = engine.getProcessEngine();

    @Test
    @Order(1)
    void genehmigter_Antrag_wird_verbucht() {
        angenommen().ein_Antrag_über_$_Euro_für_$_ist_gestellt(1200, "Dienstreise");
        wenn().die_genehmigende_Stelle_mit_$_entscheidet("genehmigt");
        dann().wartet_der_Antrag_bei_$("Task_Verbuchen")
            .und().die_Entscheidung_steht_auf_$("genehmigt");
        wenn().die_Aufgabe_$_erledigt_wird("Task_Verbuchen");
        dann().endet_der_Antrag_bei_$("End_Genehmigt")
            .und().kam_nie_an_$_vorbei("Task_Ablehnen");
    }

    @Test
    @Order(2)
    void abgelehnter_Antrag_wird_mitgeteilt() {
        angenommen().ein_Antrag_über_$_Euro_für_$_ist_gestellt(1200, "Dienstreise");
        wenn().die_genehmigende_Stelle_mit_$_entscheidet("abgelehnt");
        dann().wartet_der_Antrag_bei_$("Task_Ablehnen");
        wenn().die_Aufgabe_$_erledigt_wird("Task_Ablehnen");
        dann().endet_der_Antrag_bei_$("End_Abgelehnt")
            .und().kam_nie_an_$_vorbei("Task_Verbuchen");
    }
}
