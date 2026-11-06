import { useResourceEditor } from "@/api/resourceEditor";
import { Alert } from "@/design/alert";
import { EditorPanels } from "@/design/editorPanels";

import { factsApi } from "./api";
import { emptyForm, formOf } from "./factForm";
import { FactForm } from "./FactForm";
import { FactsView } from "./FactsView";

export function FactsScreen({ bot }: { bot: string }) {
    const editor = useResourceEditor({ bot, api: factsApi, idOf: (fact) => fact.name, usage: "facts" });

    if (editor.failure !== null) return <Alert variant="destructive">{editor.failure}</Alert>;
    if (editor.items === null) return <p className="text-muted-foreground">Loading facts…</p>;

    return (
        <FactsView
            bot={bot}
            facts={editor.items}
            onAdd={editor.add}
            onEdit={editor.edit}
            form={
                <EditorPanels noun="fact" {...editor.panels}>
                    {editor.isOpen && (
                        <FactForm
                            key={editor.item?.name ?? "add"}
                            bot={bot}
                            initial={editor.item === null ? emptyForm() : formOf(editor.item)}
                            {...editor.formProps}
                        />
                    )}
                </EditorPanels>
            }
        />
    );
}
