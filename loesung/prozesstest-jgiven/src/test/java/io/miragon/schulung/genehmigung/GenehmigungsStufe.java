package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.assertThat;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.execute;
import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.job;

import com.tngtech.jgiven.annotation.Hidden;
import com.tngtech.jgiven.annotation.IntroWord;
import org.cibseven.bpm.engine.runtime.ProcessInstance;
import org.cibseven.bpm.model.bpmn.instance.FlowNode;
import org.cibseven.community.bpm.extension.jgiven.DefaultInstanceSupplier;
import org.cibseven.community.bpm.extension.jgiven.ProcessStage;

/**
 * Gemeinsame Basis der drei Stufen (Angenommen, Wenn, Dann).
 *
 * ProcessStage aus CIB seven BPM JGiven bringt mit: die Engine im Feld camunda (kommt aus der Testklasse),
 * die Instanz im Feld processInstanceSupplier (JGiven reicht sie von Stufe zu Stufe weiter) und fertige
 * Schritte wie task_is_completed_with_variables, process_is_finished oder process_has_not_passed.
 * Deren Texte sind englisch ("process waits in ..."). Die Stufen hier rufen sie deshalb nur intern auf
 * und geben den Schritten fachliche deutsche Namen. Im Bericht steht nur der äußere Schritt.
 */
public class GenehmigungsStufe<SELF extends GenehmigungsStufe<SELF>>
        extends ProcessStage<SELF, DefaultInstanceSupplier> {

    /** "und" statt "and": ProcessStage erbt die englischen Füllwörter von JGiven, das deutsche "und" fehlt dort. */
    @IntroWord
    public SELF und() {
        return self();
    }

    /** Die Instanz dieses Szenarios, gestartet in der Stufe Angenommen. */
    @Hidden
    public ProcessInstance antrag() {
        return processInstanceSupplier.get();
    }

    /**
     * Ob das Modell hinter diesem Element einen Speicherpunkt setzt (camunda:asyncAfter).
     * Die Stufe liest das aus dem deployten Modell, der Test muss es nicht wissen.
     */
    @Hidden
    public boolean hatSpeicherpunkt(String elementId) {
        FlowNode element = camunda.getRepositoryService()
            .getBpmnModelInstance(antrag().getProcessDefinitionId())
            .getModelElementById(elementId);
        return element != null && element.isCamundaAsyncAfter();
    }

    /**
     * Wie speicherpunktAnstossen in GenehmigungsworkflowTag1Test: prüfen, dass die Instanz genau dort steht,
     * dann den Job ausführen. Die Engine im Speicher hat keinen Job Executor.
     */
    @Hidden
    public void speicherpunktAnstossen(String hinterElement) {
        assertThat(antrag()).isWaitingAtExactly(hinterElement);
        execute(job(hinterElement, antrag()));
    }
}
