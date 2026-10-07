package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.withVariables;

import com.tngtech.jgiven.annotation.Format;
import com.tngtech.jgiven.format.PrintfFormatter;
import org.cibseven.bpm.engine.runtime.ProcessInstance;
import org.cibseven.community.bpm.extension.jgiven.DefaultInstanceSupplier;

/** Stufe "Angenommen": der Ausgangszustand, ein gestellter Antrag. */
public class AngenommenStufe extends GenehmigungsStufe<AngenommenStufe> {

    /** Im laufenden System legt das Start-Event die angemeldete Person ab, im Test setzt die Stufe sie selbst. */
    static final String ANTRAGSTELLER = "anna";

    /**
     * Startet wie das Startformular, mit betrag, begruendung und antragsteller, und stößt den Speicherpunkt
     * hinter "Antrag gestellt" an. Danach wartet der Antrag bei "Antrag prüfen".
     * Der Business Key "Antrag-1" macht die Instanz in den Meldungen erkennbar.
     */
    public AngenommenStufe ein_Antrag_über_$_Euro_für_$_ist_gestellt(
            int betrag,
            @Format(value = PrintfFormatter.class, args = "„%s“") String begruendung) {
        ProcessInstance antrag = camunda.getRuntimeService().startProcessInstanceByKey(
            "Process_Genehmigung", "Antrag-1",
            withVariables("betrag", betrag, "begruendung", begruendung, "antragsteller", ANTRAGSTELLER));
        processInstanceSupplier = new DefaultInstanceSupplier(antrag);
        speicherpunktAnstossen("StartEvent_Antrag");
        return self();
    }
}
