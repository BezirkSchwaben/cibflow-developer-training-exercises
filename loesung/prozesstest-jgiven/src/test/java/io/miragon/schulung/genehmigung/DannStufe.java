package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;

import com.tngtech.jgiven.annotation.Format;
import com.tngtech.jgiven.format.PrintfFormatter;

/** Stufe "Dann": die drei Fragen an die Engine, Wartezustand, Pfad und Variablen. */
public class DannStufe extends GenehmigungsStufe<DannStufe> {

    /** Wartezustand: genau hier und nirgends sonst. */
    public DannStufe wartet_der_Antrag_bei_$(@Beschriftung String element) {
        assertThat(antrag()).isWaitingAtExactly(element);
        return self();
    }

    /** Variablen: was im Prozesskontext steht. variable_is_set kommt aus ProcessStage. */
    public DannStufe die_Entscheidung_steht_auf_$(
            @Format(value = PrintfFormatter.class, args = "„%s“") String entscheidung) {
        return variable_is_set("entscheidung", entscheidung);
    }

    /** Pfad: beendet, zuletzt im End-Event. process_is_finished kommt aus ProcessStage. */
    public DannStufe endet_der_Antrag_bei_$(@Beschriftung String ende) {
        return process_is_finished(ende);
    }

    /** Pfad, die andere Richtung: nie durchlaufen. process_has_not_passed kommt aus ProcessStage. */
    public DannStufe kam_nie_an_$_vorbei(@Beschriftung String... elemente) {
        return process_has_not_passed(elemente);
    }
}
