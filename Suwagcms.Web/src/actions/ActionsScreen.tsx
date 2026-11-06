import { useAvailability } from "@/api/availability";
import { useResourceEditor } from "@/api/resourceEditor";
import { Alert } from "@/design/alert";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { EditorPanels } from "@/design/editorPanels";

import { emptyForm, formOf } from "./actionForm";
import { ActionForm } from "./ActionForm";
import { ActionsView } from "./ActionsView";
import { actionsApi, isCode } from "./api";
import { CodeActionPanel } from "./CodeActionPanel";
import { TestRunPanel } from "./TestRunPanel";

export function ActionsScreen({ bot }: { bot: string }) {
    const availability = useAvailability(bot);
    const editor = useResourceEditor({
        bot,
        api: actionsApi,
        idOf: (action) => action.id,
        usage: "actions",
        onRenamed: availability.reload,
    });

    if (editor.failure !== null) return <Alert variant="destructive">{editor.failure}</Alert>;
    if (editor.items === null) return <p className="text-muted-foreground">Loading actions…</p>;

    const action = editor.item;
    return (
        <ActionsView
            bot={bot}
            actions={editor.items}
            isOff={(id) => availability.isOff("action", id)}
            onAdd={editor.add}
            onOpen={editor.edit}
            form={
                <EditorPanels
                    noun="action"
                    {...editor.panels}
                    editable={action !== null && !isCode(action)}
                    notice={
                        <AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />
                    }
                    panel={
                        action !== null && (
                            <AvailabilityPanel
                                id={action.id}
                                off={availability.isOff("action", action.id)}
                                delivery={availability.delivery}
                                usedBy={editor.panels.usage?.usage?.usedBy ?? null}
                                onTurn={(off) => {
                                    availability.turn("action", action.id, off);
                                }}
                            />
                        )
                    }
                >
                    {action !== null && isCode(action) ? (
                        <>
                            <CodeActionPanel action={action} onClose={editor.close} />
                            <TestRunPanel bot={bot} action={action} unsaved={false} />
                        </>
                    ) : (
                        editor.isOpen && (
                            <ActionForm
                                key={action?.id ?? "add"}
                                bot={bot}
                                initial={action === null ? emptyForm() : formOf(action)}
                                {...editor.formProps}
                                {...(action === null
                                    ? {}
                                    : { testRun: (unsaved: boolean) => <TestRunPanel bot={bot} action={action} unsaved={unsaved} /> })}
                            />
                        )
                    )}
                </EditorPanels>
            }
        />
    );
}
