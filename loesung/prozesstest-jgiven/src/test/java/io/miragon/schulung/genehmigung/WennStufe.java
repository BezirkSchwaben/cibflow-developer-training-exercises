package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;

import com.tngtech.jgiven.annotation.Format;
import com.tngtech.jgiven.format.PrintfFormatter;
import org.cibseven.bpm.engine.variable.Variables;

/** Stufe "Wenn": was die Beteiligten tun. Der Test spielt sie alle selbst. */
public class WennStufe extends GenehmigungsStufe<WennStufe> {

    /**
     * Wie die genehmigende Stelle im Formular an "Antrag prüfen": entscheidung setzen, Aufgabe abschließen.
     * task_is_completed_with_variables aus ProcessStage stößt danach den Speicherpunkt hinter der Aufgabe an,
     * erst dort entscheidet das Gateway.
     */
    public WennStufe die_genehmigende_Stelle_mit_$_entscheidet(
            @Format(value = PrintfFormatter.class, args = "„%s“") String entscheidung) {
        assertThat(antrag()).isWaitingAtExactly("Task_Pruefen");
        return task_is_completed_with_variables(
            "Task_Pruefen", Variables.putValue("entscheidung", entscheidung), hatSpeicherpunkt("Task_Pruefen"));
    }

    /** Eine Aufgabe ohne Eingaben abschließen, etwa "Genehmigung verbuchen" oder "Ablehnung mitteilen". */
    public WennStufe die_Aufgabe_$_erledigt_wird(@Beschriftung String aufgabe) {
        assertThat(antrag()).isWaitingAtExactly(aufgabe);
        return task_is_completed_with_variables(aufgabe, Variables.createVariables(), hatSpeicherpunkt(aufgabe));
    }
}
