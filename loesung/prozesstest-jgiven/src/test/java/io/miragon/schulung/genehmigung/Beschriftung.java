package io.miragon.schulung.genehmigung;

import static org.cibseven.bpm.engine.test.assertions.bpmn.BpmnAwareTests.repositoryService;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;
import java.util.Arrays;
import java.util.stream.Collectors;

import com.tngtech.jgiven.annotation.Format;
import com.tngtech.jgiven.format.ArgumentFormatter;
import org.cibseven.bpm.engine.repository.ProcessDefinition;
import org.cibseven.bpm.model.xml.instance.ModelElementInstance;

/**
 * Im Code steht die ID aus dem Modell, im Bericht die Beschriftung: aus "Task_Verbuchen" wird
 * „Genehmigung verbuchen“. Der Test hängt weiter an der ID, im Bericht steht der Name.
 * Die Beschriftung kommt aus dem Modell, das die Engine gerade deployt hat. Findet sie die ID dort nicht,
 * etwa nach einer geänderten ID, steht die ID im Bericht.
 */
@Format(value = Beschriftung.AusDemModell.class)
@Retention(RetentionPolicy.RUNTIME)
@Target({ElementType.PARAMETER, ElementType.ANNOTATION_TYPE, ElementType.FIELD})
public @interface Beschriftung {

    class AusDemModell implements ArgumentFormatter<Object> {

        @Override
        public String format(Object argument, String... formatterArguments) {
            if (argument instanceof Object[] ids) {
                return Arrays.stream(ids).map(AusDemModell::beschriftung).collect(Collectors.joining(", "));
            }
            return beschriftung(argument);
        }

        private static String beschriftung(Object id) {
            String name = null;
            for (ProcessDefinition definition : repositoryService().createProcessDefinitionQuery().latestVersion().list()) {
                ModelElementInstance element = repositoryService().getBpmnModelInstance(definition.getId())
                    .getModelElementById(String.valueOf(id));
                if (element != null) {
                    name = element.getAttributeValue("name");
                    break;
                }
            }
            return "„" + (name != null ? name : id) + "“";
        }
    }
}
